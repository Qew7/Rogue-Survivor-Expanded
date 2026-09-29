using System;
using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class PossessionPlanOperators
    {
        public static void Build(NpcPlanDomain d)
        {
            int model = d.Goal.Generated.ModelId;
            foreach (Item item in d.Owner.Inventory.Items) if (item.Model.ID == model && (d.Goal.Generated.ItemId == Guid.Empty || item.StoryIdentity == d.Goal.Generated.ItemId)) d.Initial |= (ulong)NpcPlanFact.ValuedItem;
            foreach (NpcKnownPlace place in d.Owner.Personality.Knowledge.Places)
            {
                string kind = d.Goal.Generated.ItemId == Guid.Empty ? "item:" + model : "item:" + d.Goal.Generated.ItemId.ToString("N");
                if (place.Kind != kind || place.Units == 0 || d.Turn - place.SeenTurn > 180 || NpcResourceCompetition.RespectRefusal(d.Owner, place.Place)) continue;
                ulong at = d.At(place.Place); if (at == 0) continue; d.Travel(place.Place, Guid.Empty, at);
                d.Add(NpcPlanAction.PickupValuedItem, place.Place, Guid.Empty, at, (ulong)NpcPlanFact.ValuedItem,
                    (ulong)NpcPlanFact.ValuedItem, 0, 5 + place.Risk * Math.Max(0, 5 + PersonalitySystem.Bias(d.Owner, DecisionKind.Law) / 3));
            }
        }
    }
}
