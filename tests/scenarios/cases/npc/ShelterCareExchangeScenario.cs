using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class ShelterCareExchangeScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/shelter-care-exchange", () => TownScenarioFactory.Arena(4696,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 8, 2);
            for (int x = 5; x <= 6; x++) for (int y = 1; y <= 2; y++) world.Map.GetTileAt(x, y).IsInside = true;
            Actor patient = NpcIntentSupport.Actor(world, "patient", 1, 1, "sociable", "trusting");
            Actor provider = NpcIntentSupport.Actor(world, "provider", 5, 1, "sociable", "honest", "humble");
            NpcIntentSupport.Turn(world, provider);
            Check.Equal(true, provider.Personality.Knowledge.Places.Exists(p => p.Kind == "shelter"),
                "provider knows shelter from a real visit");
            world.Place(provider, 2, 1);
            patient.HitPoints = 1;
            var medicine = new ItemMedicine(world.Game.GameItems.MEDIKIT); provider.Inventory.AddAll(medicine);
            NpcIntentSupport.Turn(world, patient);
            Check.Equal(true, NpcIntentSupport.HasEvent(provider, "requested_medicine"), "patient actually requests medicine");
            NpcIntentSupport.Turn(world, provider);
            Check.Equal(true, NpcIntentSupport.HasEvent(patient, "shelter_care_offered"), "provider speaks an exchange offer");
            Check.Equal(true, provider.Inventory.Contains(medicine), "speech does not transfer medicine");
            world.Map.LocalTime.TurnCounter = 1; NpcIntentSupport.Turn(world, patient);
            Check.Equal(true, NpcIntentSupport.HasEvent(provider, "shelter_care_accepted"), "patient actually accepts");
            Check.Equal(NpcServiceStatus.Accepted, provider.Personality.ServiceAgreements[0].Status,
                "both participants retain an active obligation");
            Check.Equal(true, NpcIntentSupport.Intent(provider, "escort_service_shelter") != null &&
                NpcIntentSupport.Intent(patient, "reach_service_shelter") != null, "both acquire independent travel goals");
            for (int turn = 2; turn < 30 && provider.Personality.ServiceAgreements[0].Status == NpcServiceStatus.Accepted; turn++)
            {
                world.Map.LocalTime.TurnCounter = turn;
                NpcIntentSupport.Turn(world, patient);
                NpcIntentSupport.Turn(world, provider);
            }
            Check.Equal(NpcServiceStatus.Completed, provider.Personality.ServiceAgreements[0].Status,
                "real arrival and medical aid complete the contract");
            Check.Equal(true, world.Map.GetTileAt(patient.Location.Position).IsInside &&
                world.Map.GetTileAt(provider.Location.Position).IsInside, "both reached actual indoor shelter");
            Check.Equal(true, NpcIntentSupport.HasEvent(patient, "shared_medicine") || NpcIntentSupport.HasEvent(patient, "treated_person"),
                "completion requires an actual medicine transfer or treatment");
            Check.Equal(true, patient.Personality.Person(provider.PersonalityIdentity).Trust > 0,
                "keeping the exchange improves personal trust");
        });
    }
}
