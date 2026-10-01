using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Engine.Actions
{
    sealed class ActionNpcResourceConflict : ActorAction
    {
        readonly NpcIntent goal;
        readonly NpcPlanStep step;
        readonly Actor holder;
        readonly string resource;
        public ActionNpcResourceConflict(Actor actor, RogueGame game, NpcIntent goal, NpcPlanStep step, Actor holder, string resource) : base(actor, game)
        { this.goal = goal; this.step = step; this.holder = holder; this.resource = resource; }
        public override bool IsLegal()
        {
            if (!NpcPlanExecution.Owned(m_Game, m_Actor, goal) || goal.Plan == null || goal.Plan.Current != step || holder == null || holder == m_Actor ||
                holder.IsSleeping || holder.IsPlayer || !NpcIntentSystem.CanSee(m_Game, m_Actor, holder) || !NpcKnowledgeSystem.Visible(m_Game, m_Actor, step.Place) ||
                m_Game.Rules.GridDistance(m_Actor.Location.Position, holder.Location.Position) > 4 || m_Game.Rules.AreEnemies(m_Actor, holder) ||
                Session.Get.NpcDirector.ReservationOwner(step.Place, goal.StoryId) != holder.PersonalityIdentity) return false;
            NpcResourceDispute dispute = m_Actor.Personality.Dispute(step.Place, holder.PersonalityIdentity);
            if (dispute != null && m_Actor.Location.Map.LocalTime.TurnCounter - dispute.Turn < 180) return false;
            if (resource == "food") return NpcPlanExecution.FoodOnGround(m_Game, m_Actor, step.Place) != null;
            if (resource == "medicine") return NpcPlanExecution.Medicine(m_Game, m_Actor, step.Place) != null;
            Inventory ground = step.Place.Map.GetItemsAt(step.Place.Position);
            if (ground != null && goal.Generated != null) foreach (Item item in ground.Items)
                if (item.Model.ID == goal.Generated.ModelId && (goal.Generated.ItemId == System.Guid.Empty || item.StoryIdentity == goal.Generated.ItemId)) return true;
            return false;
        }
        public override void Perform()
        {
            if (!IsLegal()) return;
            m_Game.DoSay(m_Actor, holder, "I need those " + resource + " supplies too. Can you leave them for me?", RogueGame.Sayflags.IS_STORY | RogueGame.Sayflags.IS_REQUEST,
                goal.CauseId, goal.StoryId);
            var source = new SignificantEvent("resource_contested", m_Actor, holder, m_Actor.Location.Map, m_Actor.Location.Position,
                m_Actor.Location.Map.LocalTime.TurnCounter, causeId: goal.CauseId, storyId: goal.StoryId) {
                    ResourcePlace = step.Place, Resource = resource,
                    ModelId = goal.Generated == null ? -1 : goal.Generated.ModelId,
                    ItemId = goal.Generated == null ? System.Guid.Empty : goal.Generated.ItemId };
            PersonalitySystem.Report(m_Game, source);
            goal.Plan.Invalidate(); goal.NextAttempt = source.Turn + 8;
        }
    }
}
