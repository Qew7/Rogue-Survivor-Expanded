using System;
using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class MedicalPlanOperators
    {
        public static void Build(NpcPlanDomain d, bool self, bool treatment)
        {
            foreach (Item item in d.Owner.Inventory.Items)
                if (item is ItemMedicine && ((ItemMedicine)item).Healing > 0 && !item.IsEquipped) d.Initial |= (ulong)NpcPlanFact.Medicine;
            if (d.Owner.HitPoints >= d.Game.Rules.ActorMaxHPs(d.Owner)) d.Initial |= (ulong)NpcPlanFact.Healthy;
            foreach (NpcKnownPlace place in d.Owner.Personality.Knowledge.Places)
            {
                if (place.Kind != "medicine" || place.Units <= 0 || d.Turn - place.SeenTurn > 180) continue;
                if (NpcResourceCompetition.RespectRefusal(d.Owner, place.Place)) continue;
                ulong at = d.At(place.Place); if (at == 0) continue;
                d.Travel(place.Place, Guid.Empty, at);
                d.Add(NpcPlanAction.PickupMedicine, place.Place, Guid.Empty, at, (ulong)NpcPlanFact.Medicine,
                    (ulong)NpcPlanFact.Medicine, 0, 8 - PersonalitySystem.Bias(d.Owner, DecisionKind.Explore) / 4 +
                    place.Risk * Math.Max(0, 6 + PersonalitySystem.Bias(d.Owner, DecisionKind.Law) / 3));
            }
            Contacts(d);
            if (self)
                d.Add(NpcPlanAction.UseMedicine, d.Owner.Location, d.Owner.PersonalityIdentity, (ulong)NpcPlanFact.Medicine,
                    (ulong)NpcPlanFact.Healthy, (ulong)NpcPlanFact.Healthy, (ulong)NpcPlanFact.Medicine, 1);
            else
            {
                ulong at = d.At(d.Goal.LastKnown); d.Travel(d.Goal.LastKnown, d.Goal.TargetId, at);
                ulong desired = d.Goal.Generated == null ? (ulong)NpcPlanFact.Helped : d.Goal.Generated.Result;
                d.Add(NpcPlanAction.GiveMedicine, d.Goal.LastKnown, d.Goal.TargetId, at | (ulong)NpcPlanFact.Medicine, desired,
                    desired, (ulong)NpcPlanFact.Medicine, 5 + PersonalitySystem.Bias(d.Owner, DecisionKind.Supplies) / 5 - PersonalitySystem.Bias(d.Owner, DecisionKind.Trade) / 5);
                if (treatment)
                    d.Add(NpcPlanAction.TreatPerson, d.Goal.LastKnown, d.Goal.TargetId, at | (ulong)NpcPlanFact.Medicine, desired,
                        desired, (ulong)NpcPlanFact.Medicine, 7 - PersonalitySystem.Bias(d.Owner, DecisionKind.Compassion) / 3 + PersonalitySystem.Bias(d.Owner, DecisionKind.Courage) / 10);
            }
        }
        static void Contacts(NpcPlanDomain d)
        {
            IList<Actor> visible = d.Visible;
            foreach (Actor peer in visible)
            {
                if (peer == d.Owner || peer.IsSleeping || peer.Model.Abilities.IsUndead || !peer.Model.Abilities.IsIntelligent ||
                    d.Game.Rules.AreEnemies(d.Owner, peer) || peer.PersonalityIdentity == d.Goal.TargetId && d.Goal.TargetId != d.Owner.PersonalityIdentity) continue;
                ulong at = d.At(peer.Location); if (at == 0) continue; d.Travel(peer.Location, peer.PersonalityIdentity, at);
                if (!d.Owner.Personality.Knowledge.WasTold(-d.Goal.Sequence, peer.PersonalityIdentity))
                    d.Add(NpcPlanAction.AskMedicine, peer.Location, peer.PersonalityIdentity, at, (ulong)NpcPlanFact.Medicine,
                        (ulong)NpcPlanFact.Medicine, 0, 17 - PersonalitySystem.Bias(d.Owner, DecisionKind.Group) / 3 - NpcValues.KnownAttitude(d.Owner, peer.PersonalityIdentity) / 5);
                if (d.Owner.Personality.Knowledge.Facts.Exists(f => f.Kind == "medicine_offered" && f.SubjectId == peer.PersonalityIdentity && f.OtherId == d.Owner.PersonalityIdentity && d.Turn - f.EventTurn < 60))
                    d.Add(NpcPlanAction.BarterMedicine, peer.Location, peer.PersonalityIdentity, at, (ulong)NpcPlanFact.Medicine,
                        (ulong)NpcPlanFact.Medicine, 0, 12 - PersonalitySystem.Bias(d.Owner, DecisionKind.Trade) / 3);
            }
        }
    }
}
