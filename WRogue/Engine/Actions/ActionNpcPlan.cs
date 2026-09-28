using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Engine.Actions
{
    // Revalidate the chosen action against actual state immediately before performing it.
    sealed class ActionNpcPlan : ActorAction
    {
        readonly NpcIntent goal;
        readonly NpcPlan plan;
        readonly NpcPlanStep step;
        readonly Actor target;
        readonly ItemFood food;
        readonly ActorAction movement;
        public ActionNpcPlan(Actor actor, RogueGame game, NpcIntent goal, NpcPlanStep step,
            Actor target = null, ItemFood food = null, ActorAction movement = null) : base(actor, game)
        { this.goal = goal; plan = goal.Plan; this.step = step; this.target = target; this.food = food; this.movement = movement; }
        public override bool IsLegal()
        {
            if (!NpcPlanExecution.Owned(m_Game, m_Actor, goal) || plan == null || goal.Plan != plan || plan.Current != step) return false;
            if (movement != null) return (step.Action == NpcPlanAction.Travel || step.Action == NpcPlanAction.Retreat) && movement.IsLegal();
            if (step.Action == NpcPlanAction.PickupFood)
            {
                string reason;
                return (plan.Desired != (ulong)NpcPlanFact.Food || !NpcIntentSystem.HasFood(m_Game, m_Actor)) &&
                    food != null && NpcPlanExecution.Near(m_Game, m_Actor, step.Place) && NpcPlanExecution.FoodOnGround(m_Game, m_Actor, step.Place) == food &&
                    m_Game.Rules.CanActorGetItem(m_Actor, food, out reason);
            }
            if (step.Action == NpcPlanAction.AskLocation) return new ActionNpcAskLocation(m_Actor, m_Game, target, goal).IsLegal();
            if (step.Action == NpcPlanAction.EnterShelter || step.Action == NpcPlanAction.ConfirmSafety)
                return new ActionNpcStory(m_Actor, m_Game, goal).IsLegal();
            if (target == null || target.PersonalityIdentity != step.Target || !NpcIntentSystem.CanSee(m_Game, m_Actor, target) || target.IsSleeping ||
                !target.Model.Abilities.IsIntelligent || m_Game.Rules.AreEnemies(m_Actor, target) || !NpcPlanExecution.Near(m_Game, m_Actor, target.Location)) return false;
            if (step.Action == NpcPlanAction.AskFood)
                return !m_Actor.Personality.Knowledge.WasTold(-goal.Sequence, target.PersonalityIdentity) &&
                    (plan.Desired != (ulong)NpcPlanFact.Food || !NpcIntentSystem.HasFood(m_Game, m_Actor));
            if (step.Action == NpcPlanAction.GiveFood) return target.PersonalityIdentity == goal.TargetId && food != null &&
                NpcIntentSystem.SpareFood(m_Game, m_Actor, target) == food && goal.Progress < 2;
            if (step.Action == NpcPlanAction.ReportDelivery) return goal.Progress >= 2 && target.PersonalityIdentity == goal.CoordinatorId;
            return new ActionNpcStory(m_Actor, m_Game, goal, target).IsLegal();
        }
        public override void Perform()
        {
            if (!IsLegal()) return;
            int turn = m_Actor.Location.Map.LocalTime.TurnCounter;
            if (movement != null)
            {
                movement.Perform();
                bool arrived = step.Action == NpcPlanAction.Travel ? NpcPlanExecution.Near(m_Game, m_Actor, step.Place) :
                    m_Actor.Location.Map != goal.LastKnown.Map || m_Game.Rules.GridDistance(m_Actor.Location.Position, goal.LastKnown.Position) >= 5;
                if (arrived && (NpcIntentContent.Find(goal.DefinitionId).Method != NpcIntentMethod.ReachShelter || m_Actor.Location.Map.GetTileAt(m_Actor.Location.Position).IsInside)) plan.Cursor++;
                return;
            }
            if (step.Action == NpcPlanAction.PickupFood)
            {
                long before = m_Actor.Inventory.TotalReceived;
                m_Game.DoTakeItem(m_Actor, step.Place.Position, food, causeId: plan.LastEventId > 0 ? plan.LastEventId : goal.CauseId, storyId: goal.StoryId);
                if (m_Actor.Inventory.TotalReceived <= before) { plan.Reject(step, turn); return; }
                if (goal.Progress < 1) goal.Progress = 1;
                NpcPlanExecution.Publish(m_Game, "supplies_acquired", m_Actor, null, goal);
                if (plan.Desired == (ulong)NpcPlanFact.Food && NpcIntentSystem.HasFood(m_Game, m_Actor))
                    NpcIntentSystem.Finish(m_Actor, goal, NpcIntentStatus.Completed, "actually acquired usable food");
            }
            else if (step.Action == NpcPlanAction.AskFood)
            {
                bool own = NpcIntentContent.Find(goal.DefinitionId).Method == NpcIntentMethod.RequestFood;
                m_Game.DoSay(m_Actor, target, own || goal.DefinitionId == NpcIntentContent.Obtain.Id ? "Could you spare some food?" : "Could you spare food? I'm trying to help someone.", RogueGame.Sayflags.NONE);
                if (own) { goal.Announced = true; goal.Status = NpcIntentStatus.Waiting; }
                m_Actor.Personality.Knowledge.Told(-goal.Sequence, target.PersonalityIdentity, turn);
                NpcPlanExecution.Publish(m_Game, "requested_food", m_Actor, target, goal); goal.NextAttempt = turn + 8;
            }
            else if (step.Action == NpcPlanAction.AskLocation) new ActionNpcAskLocation(m_Actor, m_Game, target, goal).Perform();
            else if (step.Action == NpcPlanAction.GiveFood) m_Game.DoNpcIntent(m_Actor, target, goal, food);
            else if (step.Action == NpcPlanAction.ReportDelivery)
            {
                m_Game.DoSay(m_Actor, target, "The food was delivered.", RogueGame.Sayflags.NONE);
                NpcPlanExecution.Publish(m_Game, "supplies_delivered", m_Actor, target, goal);
                NpcIntentSystem.Finish(m_Actor, goal, NpcIntentStatus.Completed, "reported actual delivery");
            }
            else new ActionNpcStory(m_Actor, m_Game, goal, target).Perform();
            if (!goal.Finished && goal.Plan == plan && plan.Current == step) plan.Cursor++;
            goal.BlockedAttempts = 0;
        }
    }
}
