using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Engine
{
    partial class RogueGame
    {
        internal void DoNpcBarter(Actor buyer, Actor seller, Item payment, ItemFood food, NpcIntent goal)
        {
            // Both complete additions were checked by the action; inventories belong to distinct actors.
            ItemFood gift = NpcFoodSupply.OneFood(food); gift.Quantity = 2;
            if (!buyer.Inventory.CanAddAll(gift) || !seller.Inventory.CanAddAll(payment)) return;
            if (!buyer.Inventory.AddAll(gift)) return;
            seller.Inventory.AddAll(payment); buyer.Inventory.RemoveAllQuantity(payment);
            seller.Inventory.Consume(food); seller.Inventory.Consume(food);
            SpendActorActionPoints(buyer, Rules.BASE_ACTION_COST);
            DoSay(buyer, seller, "Agreed. These supplies for your food.", Sayflags.IS_FREE_ACTION);
            NpcPlanExecution.Publish(this, "bartered_food", buyer, seller, goal);
            if (goal.DefinitionId == NpcIntentContent.Obtain.Id || goal.DefinitionId == NpcIntentContent.Request.Id)
                NpcIntentSystem.Finish(buyer, goal, NpcIntentStatus.Completed, "actually exchanged supplies for food");
        }
    }
}
