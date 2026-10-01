using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class MedicalMethodsScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/medical-methods", () => TownScenarioFactory.Arena(4661, ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 8, 2);
            Actor patient = NpcIntentSupport.Actor(world, "patient", 2, 1);
            Actor carer = NpcIntentSupport.Actor(world, "carer", 1, 1, "kind");
            patient.HitPoints = 1;
            NpcGoalGenerator.Refresh(world.Game, patient);
            var kit = new ItemMedicine(world.Game.GameItems.MEDIKIT); carer.Inventory.AddAll(kit);
            NpcIntentSupport.Turn(world, carer);
            Check.Equal(true, patient.HitPoints > 1, "compassion selects and performs direct treatment");
            Check.Equal(false, carer.Inventory.Contains(kit), "direct treatment consumes the real medicine");
            Check.Equal(true, NpcIntentSupport.HasEvent(patient, "treated_person"), "recipient observes actual treatment");
            Check.Equal(false, NpcIntentSupport.HasEvent(patient, "treated_wounds"), "external treatment does not invent self-use of medicine");
            Check.Equal(NpcIntentStatus.Active, NpcIntentSupport.Intent(patient, "restore_health").Status, "partial external healing preserves the remaining recovery need");
            Actor giver = NpcIntentSupport.Actor(world, "giver", 3, 1, "generous");
            giver.Personality.Opinion(patient.PersonalityIdentity, patient.UnmodifiedName).AdjustSocial(attachment: 60);
            patient.HitPoints = 1; giver.Inventory.AddAll(new ItemMedicine(world.Game.GameItems.MEDIKIT));
            NpcIntentSupport.Turn(world, giver);
            Check.Equal(1, patient.HitPoints, "a generous donor chooses transfer without inventing treatment");
            Check.Equal(true, NpcIntentSupport.HasEvent(patient, "shared_medicine"), "different traits produce a different actual method");
            Check.Equal(1, patient.Inventory.CountItems, "a single actual medicine arrives");
            NpcIntentSupport.Turn(world, patient);
            Check.Equal(true, patient.HitPoints > 1, "recipient uses the received medicine through production AI");
            Check.Equal(0, patient.Inventory.CountItems, "received medicine is consumed once");
        });
    }
}
