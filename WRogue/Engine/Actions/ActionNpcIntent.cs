using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Engine.Actions
{
    sealed class ActionNpcIntent : ActorAction
    {
        readonly NpcIntent intent;
        readonly Actor target;
        readonly ItemFood food;
        public ActionNpcIntent(Actor actor, RogueGame game, NpcIntent intent, Actor target, ItemFood food = null) : base(actor, game)
        { this.intent = intent; this.target = target; this.food = food; }
        public override bool IsLegal()
        {
            if (!NpcIntentSystem.Enabled(m_Actor) || m_Actor.IsSleeping || intent == null || intent.Finished || intent.Status == NpcIntentStatus.Paused ||
                !m_Actor.Personality.Intents.Contains(intent) || m_Actor.Location.Map.LocalTime.TurnCounter >= intent.Deadline ||
                m_Actor.Location.Map.LocalTime.TurnCounter < intent.NextAttempt) return false;
            NpcIntentDefinition definition = NpcIntentContent.Find(intent.DefinitionId);
            if (definition == null || target == null || target.PersonalityIdentity != intent.TargetId) return false;
            if (definition.Score(m_Actor, intent) < definition.ThresholdFor(intent)) return false;
            if (definition.Method == NpcIntentMethod.LeaveGroup) return m_Actor.Leader == target;
            if (!NpcIntentSystem.CanSee(m_Game, m_Actor, target) || target.IsSleeping ||
                m_Game.Rules.GridDistance(m_Actor.Location.Position, target.Location.Position) > 1 ||
                m_Game.Rules.AreEnemies(m_Actor, target)) return false;
            if (definition.Method == NpcIntentMethod.RequestFood)
                return !intent.Announced && m_Game.Rules.IsActorHungry(m_Actor) && !NpcIntentSystem.HasFood(m_Game, m_Actor);
            return food != null && m_Actor.Inventory.Contains(food) && NpcIntentSystem.SpareFood(m_Game, m_Actor, target) == food;
        }
        public override void Perform()
        {
            if (!IsLegal()) return;
            m_Game.DoNpcIntent(m_Actor, target, intent, food);
        }
    }
    sealed class ActionNpcReaction : ActorAction
    {
        readonly NpcReaction reaction;
        readonly Actor target;
        public ActionNpcReaction(Actor actor, RogueGame game, NpcReaction reaction, Actor target) : base(actor, game)
        { this.reaction = reaction; this.target = target; }
        public override bool IsLegal()
        { return NpcIntentSystem.Enabled(m_Actor) && reaction != null && target != null && target.Personality != null &&
            ((reaction.Kind != "food_promised" && reaction.Kind != "medicine_promised") ||
                m_Actor.Personality.CanRememberCommitment && target.Personality.CanRememberCommitment) &&
            target.PersonalityIdentity == reaction.TargetId && !m_Actor.IsSleeping && !target.IsSleeping &&
            m_Actor.Personality.Reactions.Contains(reaction) && m_Actor.Location.Map.LocalTime.TurnCounter <= reaction.Deadline &&
            NpcIntentSystem.CanSee(m_Game, m_Actor, target) && !m_Game.Rules.AreEnemies(m_Actor, target); }
        public override void Perform()
        {
            if (!IsLegal()) return;
            m_Game.DoSay(m_Actor, target, reaction.Text, RogueGame.Sayflags.NONE);
            m_Actor.Personality.Reactions.Remove(reaction);
            var source = new SignificantEvent(reaction.Kind, m_Actor, target, m_Actor.Location.Map, m_Actor.Location.Position,
                m_Actor.Location.Map.LocalTime.TurnCounter, causeId: reaction.CauseId, storyId: reaction.StoryId)
                { ResourcePlace = reaction.ResourcePlace, Resource = reaction.Resource };
            PersonalitySystem.Report(m_Game, source);
            if (reaction.Kind == "resource_yielded") Session.Get.NpcDirector.ReleaseOwned(reaction.ResourcePlace, m_Actor.PersonalityIdentity);
            if (reaction.ReportedPerson != null) NpcKnowledgeSystem.HearLocation(target, m_Actor, reaction.ReportedPerson, source.Id);
        }
    }
}
