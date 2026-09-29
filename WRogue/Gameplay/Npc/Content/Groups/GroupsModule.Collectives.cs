using System;
using djack.RogueSurvivor.Data;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class GroupsModule
    {
        void RegisterCollectives(NpcCatalogBuilder catalog)
        {
            RegisterProtection(catalog);
            new NpcSupplyTask("group_supplies", "food", "supplies_requested", "gather_group_supplies",
                c => PersonalitySystem.Bias(c.Leader, DecisionKind.Group) + PersonalitySystem.Bias(c.Leader, DecisionKind.Compassion) + c.Policy.Supply,
                (p, t) => p.FoodNeed > 0 && t - p.FoodNeedTurn <= 60, p => p.NeedCause).Register(catalog);
            var medicine = new NpcIntentDefinition("gather_group_medicine", "Gather medicine for a companion", WorldTime.TURNS_PER_DAY, 180) {
                Result = (c, g) => (ulong)(NpcPlanFact.Delivered | NpcPlanFact.Reported), Resource = "medicine", ReportAfterDelivery = true };
            catalog.Capability(medicine);
            catalog.Event(new NpcEventDefinition("group_medicine_requested", NpcRecordCategory.Help, false,
                e => (e.Subject ?? "Someone") + " asked " + (e.Other ?? "someone") + " to fetch medicine for a companion."));
            new NpcSupplyTask("group_medicine", "medicine", "group_medicine_requested", medicine.Id,
                c => PersonalitySystem.Bias(c.Leader, DecisionKind.Group) + PersonalitySystem.Bias(c.Leader, DecisionKind.Compassion) + c.Policy.Care,
                (p, t) => p.MedicalNeed > 0 && t - p.MedicalTurn <= 60, p => p.MedicalCause).Register(catalog);
            var factionMedicine = new NpcIntentDefinition("gather_faction_medicine", "Deliver medicine to a faction member", WorldTime.TURNS_PER_DAY, 180) {
                Result = (c, g) => (ulong)(NpcPlanFact.Delivered | NpcPlanFact.Reported), Resource = "medicine", ReportAfterDelivery = true };
            catalog.Capability(factionMedicine);
            catalog.Event(new NpcEventDefinition("faction_medicine_requested", NpcRecordCategory.Help, false,
                e => (e.Subject ?? "Someone") + " asked a faction ally to fetch medicine for an injured colleague."));
            new NpcSupplyTask("faction_medicine", "medicine", "faction_medicine_requested", factionMedicine.Id,
                c => 10 + c.Policy.Care + PersonalitySystem.Bias(c.Leader, DecisionKind.Compassion),
                (p, t) => p.MedicalNeed > 0 && t - p.MedicalTurn <= 60, p => p.MedicalCause, NpcCollectiveScope.Faction).Register(catalog);
            catalog.Collective(new NpcCollectiveDefinition("group_shelter", "shelter_suggested", ShelterOffer,
                (a, b, p) => true, (a, p) => "Let's take shelter near " + p.Destination.Map.Name + ".", NpcGroupTasks.AcceptShelter));
            catalog.On("shelter_suggested", NpcObservationPhase.Knowledge, o => NpcStorySystem.AcceptCollective(o.Game, o.Owner, o.Source));
        }
        static NpcCollectiveOffer ShelterOffer(NpcCollectiveContext c)
        {
            int priority = PersonalitySystem.Bias(c.Leader, DecisionKind.Group) + c.Policy.Shelter;
            if (priority < 15 || c.Leader.Location.Map.GetTileAt(c.Leader.Location.Position).IsInside) return null;
            NpcFact threat = c.Knowledge.Facts.Find(f => (f.Kind == "attack" || f.Kind == "murder") && c.Turn - f.EventTurn < 180);
            NpcKnownPlace shelter = c.Knowledge.Places.Find(p => p.Kind == "shelter" && p.Place != c.Leader.Location);
            return threat == null || shelter == null ? null : new NpcCollectiveOffer(new NpcGroupPlan { Kind = "group_shelter", Stage = "proposed",
                CollectorId = c.Leader.PersonalityIdentity, BeneficiaryId = c.Leader.PersonalityIdentity,
                Destination = shelter.Place, CauseId = threat.EventId, Deadline = c.Turn + 180 }, priority);
        }
    }
}
