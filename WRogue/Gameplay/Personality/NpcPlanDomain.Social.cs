using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Items;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class NpcPlanDomain
    {
        void MedicalContacts(IList<Actor> visible)
        {
            foreach (Actor peer in visible)
            {
                if (peer == owner || peer.IsSleeping || peer.Model.Abilities.IsUndead || !peer.Model.Abilities.IsIntelligent ||
                    game.Rules.AreEnemies(owner, peer) || peer.PersonalityIdentity == goal.TargetId && goal.TargetId != owner.PersonalityIdentity) continue;
                ulong at = At(peer.Location); if (at == 0) continue; Travel(peer.Location, peer.PersonalityIdentity, at);
                if (!owner.Personality.Knowledge.WasTold(-goal.Sequence, peer.PersonalityIdentity))
                    Add(NpcPlanAction.AskMedicine, peer.Location, peer.PersonalityIdentity, at, (ulong)NpcPlanFact.Medicine,
                        (ulong)NpcPlanFact.Medicine, 0, 17 - PersonalitySystem.Bias(owner, DecisionKind.Group) / 3 - NpcValues.KnownAttitude(owner, peer.PersonalityIdentity) / 5);
                if (owner.Personality.Knowledge.Facts.Exists(f => f.Kind == "medicine_offered" && f.SubjectId == peer.PersonalityIdentity && f.OtherId == owner.PersonalityIdentity && turn - f.EventTurn < 60))
                    Add(NpcPlanAction.BarterMedicine, peer.Location, peer.PersonalityIdentity, at, (ulong)NpcPlanFact.Medicine,
                        (ulong)NpcPlanFact.Medicine, 0, 12 - PersonalitySystem.Bias(owner, DecisionKind.Trade) / 3);
            }
        }
        void ValuedItem()
        {
            int model = goal.Generated.ModelId;
            foreach (Item item in owner.Inventory.Items) if (item.Model.ID == model && (goal.Generated.ItemId == Guid.Empty || item.StoryIdentity == goal.Generated.ItemId)) Initial |= (ulong)NpcPlanFact.ValuedItem;
            foreach (NpcKnownPlace place in owner.Personality.Knowledge.Places)
            {
                string kind = goal.Generated.ItemId == Guid.Empty ? "item:" + model : "item:" + goal.Generated.ItemId.ToString("N");
                if (place.Kind != kind || place.Units == 0 || turn - place.SeenTurn > 180 || NpcSocialSystem.RespectRefusal(owner, place.Place)) continue;
                ulong at = At(place.Place); if (at == 0) continue; Travel(place.Place, Guid.Empty, at);
                Add(NpcPlanAction.PickupValuedItem, place.Place, Guid.Empty, at, (ulong)NpcPlanFact.ValuedItem,
                    (ulong)NpcPlanFact.ValuedItem, 0, 5 + place.Risk * Math.Max(0, 5 + PersonalitySystem.Bias(owner, DecisionKind.Law) / 3));
            }
        }
    }
}
