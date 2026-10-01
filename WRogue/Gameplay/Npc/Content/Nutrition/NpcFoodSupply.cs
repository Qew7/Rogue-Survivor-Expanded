using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcFoodSupply
    {
        public static bool HasFood(RogueGame game, Actor actor)
        {
            if (actor.Inventory == null) return false;
            foreach (Item item in actor.Inventory.Items)
                if (item is ItemFood && item.Quantity > 0 && !game.Rules.IsFoodSpoiled((ItemFood)item,
                    actor.Location.Map.LocalTime.TurnCounter)) return true;
            return false;
        }
        public static ItemFood SpareFood(RogueGame game, Actor owner, Actor target)
        {
            if (owner.Inventory == null || target.Inventory == null || game.Rules.IsActorHungry(owner)) return null;
            int units = 0; ItemFood best = null;
            foreach (Item item in owner.Inventory.Items)
            {
                ItemFood food = item as ItemFood;
                if (food == null || food.IsEquipped || game.Rules.IsFoodSpoiled(food, owner.Location.Map.LocalTime.TurnCounter)) continue;
                units += food.Quantity; string reason;
                if (food.Quantity > 0 && game.Rules.CanActorGiveItemTo(owner, target, OneFood(food), out reason) && best == null) best = food;
            }
            return units >= 2 ? best : null;
        }
        public static ItemFood OneFood(ItemFood food)
        { return food.IsPerishable ? new ItemFood(food.Model, food.BestBefore.TurnCounter) : new ItemFood(food.Model); }
    }
}
