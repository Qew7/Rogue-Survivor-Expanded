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
                NpcIntentSystem.Publish(this, "requested_food", actor, target, intent.CauseId, intent.StoryId);
                return;
            }
            if (definition.Method == NpcIntentMethod.LeaveGroup)
            {
                if (NpcIntentSystem.CanSee(this, actor, target))
                    DoSay(actor, target, "After what happened, I won't stay in your group.", Sayflags.IS_FREE_ACTION);
                SpendActorActionPoints(actor, Rules.BASE_ACTION_COST);
                actor.SetTrustIn(target, actor.TrustInLeader); target.RemoveFollower(actor); actor.TrustInLeader = Rules.TRUST_NEUTRAL;
                BaseAI ai = actor.Controller as BaseAI; if (ai != null) ai.SetOrder(null);
                NpcIntentSystem.Publish(this, "left_group", actor, target, intent.CauseId, intent.StoryId);
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
            if (needed) NpcIntentSystem.Publish(this, "helped", target, actor, intent.CauseId, intent.StoryId);
            if (NpcIntentSystem.CanSee(this, actor, target))
                DoSay(actor, target, intent.DefinitionId == NpcIntentContent.Repay.Id ?
                    "You helped me before. Here, take this food." : "Here, I can spare some food.", Sayflags.IS_FREE_ACTION);
            NpcIntentSystem.Publish(this, "shared_food", actor, target, intent.CauseId, intent.StoryId);
            NpcIntentSystem.Finish(actor, intent, NpcIntentStatus.Completed, "transferred one food unit");
        }
    }
}
