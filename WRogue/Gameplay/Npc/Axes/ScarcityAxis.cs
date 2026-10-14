using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Items;

namespace djack.RogueSurvivor.Gameplay.Personality.Axes
{
    static class ScarcityAxis
    {
        public static int Faction(NpcFactionPolicy policy) { return policy.Scarcity; }
        public static int Context(Actor actor)
        {
            if (!actor.Model.Abilities.HasToEat || actor.FoodPoints >= actor.Sheet.BaseFoodPoints / 4)
                return 0;
            // Food in hand eases the immediate shortage, but spoiled food does not.
            int turn = actor.Location.Map == null ? 0 : actor.Location.Map.LocalTime.TurnCounter;
            if (actor.Inventory != null)
                foreach (Item item in actor.Inventory.Items)
                {
                    ItemFood food = item as ItemFood;
                    if (food != null && (!food.IsPerishable ||
                        turn < 2 * food.BestBefore.TurnCounter))
                        return 5;
                }
            return 15;
        }

        public static int Trait(TraitEffect effect)
        {
            if (effect.Axis == NpcPersonalityAxis.Scarcity) return effect.Amount;
            if (effect.Axis == NpcPersonalityAxis.Supplies) return effect.Amount * 3 / 4;
            if (effect.Axis == NpcPersonalityAxis.Trade) return -effect.Amount / 4;
            return 0;
        }

        public static int ImmediateSanity(Actor observer, SignificantEvent e, int value)
        {
            if (observer != e.Subject) return 0;
            if (e.Kind == "starvation") return -2;
            if (e.Kind == "supplies_lost" || e.Kind == "base_theft")
                return value >= 20 || observer.Personality.HasTrait("frugal") ? -2 : -1;
            return 0;
        }
    }
}
