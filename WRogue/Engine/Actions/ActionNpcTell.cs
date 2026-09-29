using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Engine.Actions
{
    sealed class ActionNpcTell : ActorAction
    {
        readonly Actor target;
        readonly NpcFact fact;
        public ActionNpcTell(Actor actor, RogueGame game, Actor target, NpcFact fact) : base(actor, game) { this.target = target; this.fact = fact; }
        public override bool IsLegal()
        { return NpcIntentSystem.Enabled(m_Actor) && !m_Actor.IsSleeping && target != null && !target.IsSleeping && fact != null &&
            target.Model.Abilities.IsIntelligent && !target.Model.Abilities.IsUndead && NpcIntentSystem.CanSee(m_Game, m_Actor, target) &&
            m_Game.Rules.GridDistance(m_Actor.Location.Position, target.Location.Position) <= 4 && !m_Game.Rules.AreEnemies(m_Actor, target) &&
            m_Actor.Personality.Knowledge.Facts.Contains(fact) && fact.Confidence >= 40 && fact.Hops < 3 &&
            m_Actor.Location.Map.LocalTime.TurnCounter - fact.EventTurn <= 2 * WorldTime.TURNS_PER_DAY &&
            !m_Actor.Personality.Knowledge.WasTold(fact.EventId, target.PersonalityIdentity); }
        public override void Perform()
        {
            if (!IsLegal()) return;
            string report = NpcRecordDescriptions.Report(m_Game.NpcContent, fact);
            m_Game.DoSay(m_Actor, target, (fact.Source == NpcKnowledgeSource.Told ? "I was told that " : fact.Source == NpcKnowledgeSource.Inferred ? "As far as I know, " : "I saw that ") + report +
                " near " + fact.Place.Map.Name + ".", RogueGame.Sayflags.NONE);
            if (target.Personality == null) target.Personality = new PersonalityState();
            NpcKnowledgeSystem.Hear(m_Game, target, m_Actor, fact);
            int turn = m_Actor.Location.Map.LocalTime.TurnCounter;
            m_Actor.Personality.Knowledge.Told(fact.EventId, target.PersonalityIdentity, turn);
            m_Actor.Personality.Knowledge.NextTalkTurn = turn + 30;
            NpcEvents.Publish(m_Game, "rumor_shared", m_Actor, target, fact.EventId, fact.StoryId);
        }
    }
}
