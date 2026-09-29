using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Engine.Actions
{
    sealed class ActionNpcReaction : ActorAction
    {
        readonly NpcReaction reaction;
        readonly Actor target;
        public ActionNpcReaction(Actor actor, RogueGame game, NpcReaction reaction, Actor target) : base(actor, game)
        { this.reaction = reaction; this.target = target; }
        public override bool IsLegal()
        { return NpcIntentSystem.Enabled(m_Actor) && reaction != null && target != null && target.Personality != null &&
            (m_Game.NpcContent.Event(reaction.Kind) == null || m_Game.NpcContent.Event(reaction.Kind).CanReply == null ||
                m_Game.NpcContent.Event(reaction.Kind).CanReply(m_Actor, target)) &&
            target.PersonalityIdentity == reaction.TargetId && !m_Actor.IsSleeping && !target.IsSleeping &&
            m_Actor.Personality.Reactions.Contains(reaction) && m_Actor.Location.Map.LocalTime.TurnCounter <= reaction.Deadline &&
            NpcIntentSystem.CanSee(m_Game, m_Actor, target) && !m_Game.Rules.AreEnemies(m_Actor, target); }
        public override void Perform()
        {
            if (!IsLegal()) return;
            m_Game.DoSay(m_Actor, target, reaction.Text, RogueGame.Sayflags.IS_STORY |
                (reaction.ReportedPerson == null ? RogueGame.Sayflags.NONE : RogueGame.Sayflags.IS_RUMOR),
                reaction.CauseId, reaction.StoryId);
            m_Actor.Personality.Reactions.Remove(reaction);
            var source = new SignificantEvent(reaction.Kind, m_Actor, target, m_Actor.Location.Map, m_Actor.Location.Position,
                m_Actor.Location.Map.LocalTime.TurnCounter, causeId: reaction.CauseId, storyId: reaction.StoryId)
                { ResourcePlace = reaction.ResourcePlace, Resource = reaction.Resource };
            PersonalitySystem.Report(m_Game, source);
            if (reaction.ReportedPerson != null)
                foreach (Actor hearer in m_Actor.Location.Map.Actors)
                    if (hearer != m_Actor && !hearer.IsDead && !hearer.IsSleeping &&
                        hearer.Model.Abilities.IsIntelligent && !hearer.Model.Abilities.IsUndead &&
                        m_Game.Rules.StdDistance(hearer.Location.Position, m_Actor.Location.Position) <= hearer.AudioRange)
                        NpcKnowledgeSystem.HearLocation(hearer, m_Actor, reaction.ReportedPerson, source.Id, m_Game.NpcContent);
        }
    }
}
