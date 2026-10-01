using System;
using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class FoodPlanOperators
    {
        public static void Seed(NpcPlanDomain d)
        {
            int food = 0;
            foreach (Item item in d.Owner.Inventory.Items)
                if (item is ItemFood && !item.IsEquipped && !d.Game.Rules.IsFoodSpoiled((ItemFood)item, d.Turn)) food += item.Quantity;
            if (food > 0) d.Initial |= (ulong)NpcPlanFact.Food;
            if (food >= 2 && !d.Game.Rules.IsActorHungry(d.Owner)) d.Initial |= (ulong)NpcPlanFact.SpareFood;
            if (d.Goal.Progress >= 2) d.Initial |= (ulong)NpcPlanFact.Delivered;
            if (d.Goal.Announced) d.Initial |= (ulong)NpcPlanFact.Requested;
        }
        public static void Acquisition(NpcPlanDomain d)
        {
            bool allowTarget = (d.Desired & (ulong)NpcPlanFact.Delivered).Empty;
            int food = 0;
            foreach (Item item in d.Owner.Inventory.Items)
                if (item is ItemFood && !item.IsEquipped && !d.Game.Rules.IsFoodSpoiled((ItemFood)item, d.Turn)) food += item.Quantity;
                foreach (NpcFact offer in d.Owner.Personality.Knowledge.Facts)
                {
                    if (offer.Kind != "food_offered" || offer.OtherId != d.Owner.PersonalityIdentity || offer.Confidence < 40 || d.Turn - offer.EventTurn > 60) continue;
                    NpcKnownPerson seller = d.Owner.Personality.Knowledge.Person(offer.SubjectId); if (seller == null || seller.Dead) continue;
                    bool hasPayment = false; foreach (Item item in d.Owner.Inventory.Items) if (!(item is ItemFood) && !item.IsEquipped && !item.IsUnique) hasPayment = true;
                    if (!hasPayment) continue;
                    ulong at = d.At(seller.Place); if (at == 0) continue;
                    d.Travel(seller.Place, seller.Id, at);
                    d.Add(NpcPlanAction.BarterFood, seller.Place, seller.Id, at, 0,
                        (ulong)(NpcPlanFact.Food | NpcPlanFact.SpareFood), 0, 12 - PersonalitySystem.Bias(d.Owner, DecisionKind.Trade) / 3);
                }
                foreach (NpcKnownPlace cache in d.Owner.Personality.Knowledge.Places)
                {
                    if (cache.Kind != "food" || cache.Units <= 0 || d.Turn - cache.SeenTurn > 180) continue;
                    if (NpcResourceCompetition.RespectRefusal(d.Owner, cache.Place)) continue;
                    ulong at = d.At(cache.Place); if (at == 0) continue;
                    d.Travel(cache.Place, Guid.Empty, at);
                    ulong gain = (ulong)NpcPlanFact.Food;
                    if (food + cache.Units >= 2) gain |= (ulong)NpcPlanFact.SpareFood;
                    d.Add(NpcPlanAction.PickupFood, cache.Place, Guid.Empty, at, 0, gain, 0,
                        9 - PersonalitySystem.Bias(d.Owner, DecisionKind.Explore) / 4 - PersonalitySystem.Bias(d.Owner, DecisionKind.Supplies) / 4 +
                        cache.Risk * Math.Max(0, 6 + PersonalitySystem.Bias(d.Owner, DecisionKind.Law) / 3 - PersonalitySystem.Bias(d.Owner, DecisionKind.Courage) / 4));
                }
                var peers = new List<Actor>(d.Visible); peers.Sort((a, b) => a.PersonalityIdentity.CompareTo(b.PersonalityIdentity));
                int n = 0;
                foreach (Actor peer in peers)
                {
                    if (n >= NpcFactLayout.MaximumQuestions || peer.IsSleeping || peer == d.Owner ||
                        (peer.PersonalityIdentity == d.Goal.TargetId && !allowTarget) ||
                        !peer.Model.Abilities.IsIntelligent || d.Game.Rules.AreEnemies(d.Owner, peer) ||
                        d.Owner.Personality.Knowledge.WasTold(-d.Goal.Sequence, peer.PersonalityIdentity)) continue;
                    ulong at = d.At(peer.Location), asked = NpcFactLayout.Question(n++);
                    d.Travel(peer.Location, peer.PersonalityIdentity, at);
                    int cost = 15 - PersonalitySystem.Bias(d.Owner, DecisionKind.Group) / 3 - PersonalitySystem.Bias(d.Owner, DecisionKind.Compassion) / 5 -
                        PersonalitySystem.Attitude(d.Owner, peer) / 5 - (d.Owner.Leader == peer ? 5 : 0);
                    d.Add(NpcPlanAction.AskFood, peer.Location, peer.PersonalityIdentity, at, asked | (ulong)NpcPlanFact.Food,
                        asked | (ulong)NpcPlanFact.Food, 0, cost);
                    d.Add(NpcPlanAction.AskFood, peer.Location, peer.PersonalityIdentity, at | (ulong)NpcPlanFact.Food,
                        asked | (ulong)NpcPlanFact.SpareFood, asked | (ulong)NpcPlanFact.SpareFood, 0, cost);
                }
        }
        public static void Delivery(NpcPlanDomain d)
        {
            ulong targetAt = d.At(d.Goal.LastKnown);
                d.Travel(d.Goal.LastKnown, d.Goal.TargetId, targetAt);
                d.Add(NpcPlanAction.GiveFood, d.Goal.LastKnown, d.Goal.TargetId, targetAt | (ulong)NpcPlanFact.SpareFood,
                    (ulong)NpcPlanFact.Delivered, (ulong)NpcPlanFact.Delivered, (ulong)NpcPlanFact.SpareFood, 2);
        }
    }
}
