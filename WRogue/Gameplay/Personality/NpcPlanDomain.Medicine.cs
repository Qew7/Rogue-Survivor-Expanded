using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Items;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class NpcPlanDomain
    {
        void Medicine()
        {
            foreach (Item item in owner.Inventory.Items)
                if (item is ItemMedicine && ((ItemMedicine)item).Healing > 0 && !item.IsEquipped) Initial |= (ulong)NpcPlanFact.Medicine;
            if (owner.HitPoints >= game.Rules.ActorMaxHPs(owner)) Initial |= (ulong)NpcPlanFact.Healthy;
            foreach (NpcKnownPlace place in owner.Personality.Knowledge.Places)
            {
                if (place.Kind != "medicine" || place.Units <= 0 || turn - place.SeenTurn > 180) continue;
                ulong at = At(place.Place); if (at == 0) continue;
                Travel(place.Place, Guid.Empty, at);
                Add(NpcPlanAction.PickupMedicine, place.Place, Guid.Empty, at, (ulong)NpcPlanFact.Medicine,
                    (ulong)NpcPlanFact.Medicine, 0, 8 - PersonalitySystem.Bias(owner, DecisionKind.Explore) / 4 +
                    place.Risk * Math.Max(0, 6 + PersonalitySystem.Bias(owner, DecisionKind.Law) / 3));
            }
            Add(NpcPlanAction.UseMedicine, owner.Location, owner.PersonalityIdentity, (ulong)NpcPlanFact.Medicine,
                (ulong)NpcPlanFact.Healthy, (ulong)NpcPlanFact.Healthy, (ulong)NpcPlanFact.Medicine, 1);
        }
    }
}
