using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Engine.Actions
{
    sealed class ActionNpcValuedItem : ActorAction
    {
        readonly NpcIntent goal;
        readonly NpcPlanStep step;
        Item item;
        public ActionNpcValuedItem(Actor actor, RogueGame game, NpcIntent goal, NpcPlanStep step) : base(actor, game) { this.goal = goal; this.step = step; }
        public override bool IsLegal()
        {
            if (!NpcPlanExecution.Owned(m_Game, m_Actor, goal) || goal.Plan == null || goal.Plan.Current != step || goal.Generated == null ||
                !NpcPlanExecution.Near(m_Game, m_Actor, step.Place) || !NpcKnowledgeSystem.Visible(m_Game, m_Actor, step.Place)) return false;
            Inventory ground = step.Place.Map.GetItemsAt(step.Place.Position); item = null; string reason;
            if (ground != null) foreach (Item candidate in ground.Items)
                if (candidate.Model.ID == goal.Generated.ModelId && (goal.Generated.ItemId == System.Guid.Empty || candidate.StoryIdentity == goal.Generated.ItemId) &&
                    m_Game.Rules.CanActorGetItem(m_Actor, candidate, out reason)) { item = candidate; return true; }
            return false;
        }
        public override void Perform()
        {
            if (!IsLegal()) return;
            long before = m_Actor.Inventory.TotalReceived;
            m_Game.DoTakeItem(m_Actor, step.Place.Position, item, causeId: goal.CauseId, storyId: goal.StoryId);
            if (m_Actor.Inventory.TotalReceived == before) return;
            NpcSocialSystem.Taken(m_Game, m_Actor, goal, step.Place, "item");
            NpcPlanExecution.Publish(m_Game, "valued_item_acquired", m_Actor, null, goal);
            NpcIntentSystem.Finish(m_Actor, goal, NpcIntentStatus.Completed, "actually acquired the valued item model");
        }
    }
}
