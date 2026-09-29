using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Engine.Actions
{
    sealed class ActionNpcMedicine : ActorAction
    {
        readonly NpcIntent goal;
        readonly NpcPlanStep step;
        public ActionNpcMedicine(Actor actor, RogueGame game, NpcIntent goal, NpcPlanStep step) : base(actor, game)
        { this.goal = goal; this.step = step; }
        public override bool IsLegal()
        {
            if (!NpcPlanExecution.Owned(m_Game, m_Actor, goal) || goal.Plan == null || goal.Plan.Current != step ||
                goal.Generated != null && goal.Generated.Value == NpcGoalValue.Recovery && m_Actor.HitPoints >= m_Game.Rules.ActorMaxHPs(m_Actor)) return false;
            bool use = step.Action == NpcPlanAction.UseMedicine;
            if (!use && step.Action != NpcPlanAction.PickupMedicine) return false;
            if (use && m_Actor.HitPoints >= m_Game.Rules.ActorMaxHPs(m_Actor)) return false;
            ItemMedicine medicine = NpcPlanExecution.Medicine(m_Game, m_Actor, step.Place, use);
            string reason;
            return medicine != null && (use ? m_Game.Rules.CanActorUseItem(m_Actor, medicine, out reason) :
                NpcPlanExecution.Near(m_Game, m_Actor, step.Place) && m_Game.Rules.CanActorGetItem(m_Actor, medicine, out reason));
        }
        public override void Perform()
        {
            if (!IsLegal()) return;
            bool use = step.Action == NpcPlanAction.UseMedicine;
            ItemMedicine medicine = NpcPlanExecution.Medicine(m_Game, m_Actor, step.Place, use);
            if (use) m_Game.DoUseItem(m_Actor, medicine);
            else
            {
                long before = m_Actor.Inventory.TotalReceived;
                m_Game.DoTakeItem(m_Actor, step.Place.Position, medicine, causeId: goal.CauseId, storyId: goal.StoryId);
                if (before == m_Actor.Inventory.TotalReceived) return;
                NpcSocialSystem.Taken(m_Game, m_Actor, goal, step.Place, "medicine");
                NpcPlanExecution.Publish(m_Game, "medicine_acquired", m_Actor, null, goal);
            }
            if (!goal.Finished) { goal.Plan.Invalidate(); goal.Plan.NextPlanningTurn = m_Actor.Location.Map.LocalTime.TurnCounter; }
        }
    }
}
