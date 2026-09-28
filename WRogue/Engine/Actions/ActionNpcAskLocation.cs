using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Engine.Actions
{
    sealed class ActionNpcAskLocation : ActorAction
    {
        readonly Actor listener;
        readonly NpcIntent intent;
        public ActionNpcAskLocation(Actor actor, RogueGame game, Actor listener, NpcIntent intent) : base(actor, game)
        { this.listener = listener; this.intent = intent; }
        public override bool IsLegal()
        { return NpcIntentSystem.Enabled(m_Actor) && !m_Actor.IsSleeping && intent != null && !intent.Finished && intent.Status != NpcIntentStatus.Paused &&
            m_Actor.Personality.Intents.Contains(intent) && intent.DefinitionId == NpcIntentContent.Seek.Id &&
            m_Actor.Location.Map.LocalTime.TurnCounter < intent.Deadline && m_Actor.Location.Map.LocalTime.TurnCounter >= intent.NextAttempt &&
            NpcIntentContent.Seek.Score(m_Actor, intent) >= NpcIntentContent.Seek.Threshold &&
            listener != null && !listener.IsPlayer && !listener.IsSleeping && listener.Personality != null && listener.PersonalityIdentity != intent.TargetId &&
            NpcIntentSystem.CanSee(m_Game, m_Actor, listener) && m_Game.Rules.GridDistance(m_Actor.Location.Position, listener.Location.Position) <= 4 &&
            !m_Game.Rules.AreEnemies(m_Actor, listener) && !m_Actor.Personality.Knowledge.WasTold(-intent.Sequence, listener.PersonalityIdentity); }
        public override void Perform()
        {
            if (!IsLegal()) return;
            m_Game.DoSay(m_Actor, listener, "Have you seen " + intent.TargetName + "?", RogueGame.Sayflags.NONE);
            SignificantEvent source = NpcIntentSystem.Publish(m_Game, "asked_location", m_Actor, listener, intent.CauseId, intent.StoryId);
            m_Actor.Personality.Knowledge.Told(-intent.Sequence, listener.PersonalityIdentity, source.Turn);
            NpcKnownPerson known = listener.Personality.Knowledge.Person(intent.TargetId);
            if (listener.Personality.Reactions.Count < 4)
            {
                NpcKnownPerson report = known == null ? null : new NpcKnownPerson { Id = known.Id, Name = known.Name, Place = known.Place,
                    SeenTurn = known.SeenTurn, Dead = known.Dead, Confidence = known.Confidence, Source = known.Source };
                listener.Personality.Reactions.Add(new NpcReaction(m_Actor, report == null ? "I don't know where they are." :
                    report.Dead ? "I'm sorry. They died." : "I last saw them near " + report.Place.Map.Name + ".",
                    source.Id, source.Turn, "location_reported", intent.StoryId, report));
            }
            intent.NextAttempt = source.Turn + 8;
        }
    }
}
