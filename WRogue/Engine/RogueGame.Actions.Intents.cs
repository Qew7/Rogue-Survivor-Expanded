using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Engine
{
    partial class RogueGame
    {
        internal void DoNpcIntent(Actor actor, Actor target, NpcIntent intent, ItemFood food)
        {
            NpcIntentDefinition definition = NpcIntentContent.Find(intent.DefinitionId);
            if (definition.Method == NpcIntentMethod.RequestFood)
            {
                string text = PersonalitySystem.Bias(actor, DecisionKind.Compassion) < 0 ? "Can you spare food? I need it." :
                    "I'm hungry, " + target.UnmodifiedName + ". Could you spare some food?";
                DoSay(actor, target, text, Sayflags.NONE);
                intent.Announced = true; intent.Status = NpcIntentStatus.Waiting;
                NpcPlanExecution.Publish(this, "requested_food", actor, target, intent);
                return;
            }
            if (definition.Method == NpcIntentMethod.LeaveGroup)
            {
                if (NpcIntentSystem.CanSee(this, actor, target))
                    DoSay(actor, target, "After what happened, I won't stay in your group.", Sayflags.IS_FREE_ACTION);
                SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);
                actor.SetTrustIn(target, actor.TrustInLeader); target.RemoveFollower(actor); actor.TrustInLeader = Rules.TRUST_NEUTRAL;
                BaseAI ai = actor.Controller as BaseAI; if (ai != null) ai.SetOrder(null);
                NpcPlanExecution.Publish(this, "left_group", actor, target, intent);
                NpcIntentSystem.Finish(actor, intent, NpcIntentStatus.Completed, "left the unsafe leader");
                return;
            }
            // Transfer one unit directly: ground stacking must not duplicate the gift or discard existing loot.
            ItemFood gift = NpcIntentSystem.OneFood(food);
            bool needed = m_Rules.IsActorHungry(target);
            if (!target.Inventory.AddAll(gift))
            { NpcIntentSystem.Finish(actor, intent, NpcIntentStatus.Failed, "food transfer failed"); return; }
            actor.Inventory.Consume(food);
            SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);
            if (needed) NpcPlanExecution.Publish(this, "helped", target, actor, intent);
            if (NpcIntentSystem.CanSee(this, actor, target))
                DoSay(actor, target, intent.DefinitionId == NpcIntentContent.Repay.Id ?
                    "You helped me before. Here, take this food." : "Here, I can spare some food.", Sayflags.IS_FREE_ACTION);
            SignificantEvent transferred = NpcPlanExecution.Publish(this, "shared_food", actor, target, intent);
            NpcSocialSystem.Delivery(this, actor, target, "food", transferred.Id, intent.StoryId);
            if (intent.Generated != null && intent.Generated.Value == NpcGoalValue.Restitution)
            {
                NpcKnownPerson owner = actor.Personality.Knowledge.Person(target.PersonalityIdentity);
                if (owner != null) owner.LossUnits = Math.Max(0, owner.LossUnits - 1);
                NpcPlanExecution.Publish(this, "restitution_given", actor, target, intent);
                if (owner != null && owner.LossUnits > 0) { intent.Plan.Invalidate(); intent.Plan.NextPlanningTurn = actor.Location.Map.LocalTime.TurnCounter; return; }
            }
            if (definition.Method == NpcIntentMethod.GatherFood)
            { intent.Progress = 2; return; }
            if (intent.Generated != null && intent.Generated.Value == NpcGoalValue.Reciprocity &&
                actor.Personality.Person(target.PersonalityIdentity).Debt > intent.Generated.Desired)
            { if (intent.Plan != null) { intent.Plan.Invalidate(); intent.Plan.NextPlanningTurn = actor.Location.Map.LocalTime.TurnCounter; } return; }
            NpcIntentSystem.Finish(actor, intent, NpcIntentStatus.Completed, "transferred one food unit");
        }
    }
}
