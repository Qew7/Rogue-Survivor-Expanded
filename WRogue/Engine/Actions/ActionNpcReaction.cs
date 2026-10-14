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
            NpcIntentSystem.CanSee(m_Game, m_Actor, target) && !m_Game.Rules.AreEnemies(m_Actor, target) &&
            (!reaction.AttackTarget || m_Game.Rules.CanActorMeleeAttack(m_Actor, target)); }
        public override void Perform()
        {
            if (!IsLegal()) return;
            if (reaction.Kind == "patience_lost" || reaction.Kind == "patience_escalated" ||
                reaction.Kind == "boundary_escalated" || reaction.Kind == "boundary_defied")
                m_Actor.Personality.ChangeFrustration(m_Actor.Location.Map.LocalTime.TurnCounter, -100);
            if (reaction.AttackTarget)
            {
                m_Actor.Personality.Reactions.Remove(reaction);
                m_Game.DoSay(m_Actor, target, reaction.Text,
                    RogueGame.Sayflags.IS_STORY | RogueGame.Sayflags.IS_FREE_ACTION,
                    reaction.CauseId, reaction.StoryId);
                PersonalitySystem.Report(m_Game, new SignificantEvent(reaction.Kind, m_Actor, target,
                    m_Actor.Location.Map, m_Actor.Location.Position, m_Actor.Location.Map.LocalTime.TurnCounter,
                    causeId: reaction.CauseId, storyId: reaction.StoryId));
                m_Game.DoMeleeAttack(m_Actor, target, causeId: reaction.CauseId, storyId: reaction.StoryId);
                return;
            }
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
