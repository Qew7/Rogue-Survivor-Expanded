using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Items;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class StockpilePlanOperators
    {
        static int Count(NpcPlanDomain d)
        { int units = 0; foreach (Item item in d.Owner.Inventory.Items)
            if (item is ItemFood && !d.Game.Rules.IsFoodSpoiled((ItemFood)item, d.Turn)) units += item.Quantity;
            return Math.Min(6, units); }
        public static void Seed(NpcPlanDomain d)
        {
            if (!d.Desired.Contains(d.Catalog.Facts.Mask("FoodReserve"))) return;
            int units = Count(d), desired = d.Goal.Generated == null ? 3 : d.Goal.Generated.Desired;
            d.InitialState |= d.Catalog.Facts.Mask("FoodStock" + units);
            if (units >= desired) d.InitialState |= d.Catalog.Facts.Mask("FoodReserve");
        }
        public static void Build(NpcPlanDomain d)
        {
            int desired = d.Goal.Generated == null ? 3 : Math.Min(6, d.Goal.Generated.Desired);
            int index = 0;
            foreach (NpcKnownPlace cache in d.Owner.Personality.Knowledge.Places)
            {
                if (cache.Kind != "food" || cache.Units < 1 || d.Turn - cache.SeenTurn > 180 || NpcResourceCompetition.RespectRefusal(d.Owner, cache.Place)) continue;
                if (index >= NpcFactLayout.MaximumQuestions) break;
                ulong used = NpcFactLayout.Question(index++), at = d.At(cache.Place); if (at == 0) continue; d.Travel(cache.Place, Guid.Empty, at);
                for (int count = 0; count < desired; count++)
                {
                    NpcPlanningState before = d.Catalog.Facts.Mask("FoodStock" + count), after = d.Catalog.Facts.Mask("FoodStock" + Math.Min(6, count + cache.Units));
                    if (count + cache.Units >= desired) after |= d.Catalog.Facts.Mask("FoodReserve");
                    d.Add("food.take", cache.Place, Guid.Empty, at | before, used, after | used, before, 5 - PersonalitySystem.Bias(d.Owner, DecisionKind.Supplies) / 5);
                }
            }
        }
    }
}
