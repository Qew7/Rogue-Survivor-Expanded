using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class ServiceAgreementCapacityScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/service-agreement-capacity", () => TownScenarioFactory.Arena(4864,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 8, 2);
            for (int x = 5; x <= 6; x++) for (int y = 1; y <= 2; y++) world.Map.GetTileAt(x, y).IsInside = true;
            Actor patient = NpcIntentSupport.Actor(world, "patient", 1, 1, "sociable", "trusting");
            Actor provider = NpcIntentSupport.Actor(world, "provider", 5, 1, "sociable", "honest", "humble");
            NpcIntentSupport.Turn(world, provider);
            Check.Equal(true, provider.Personality.Knowledge.Places.Exists(p => p.Kind == "shelter"),
                "provider knows the shelter");
            world.Place(provider, 2, 1);
            provider.Inventory.AddAll(new ItemMedicine(world.Game.GameItems.MEDIKIT));
            var request = new SignificantEvent("requested_medicine", patient, provider, world.Map,
                patient.Location.Position, 0);

            for (int i = 0; i < 8; i++)
                Check.Equal(true, provider.Personality.RememberService(new NpcServiceAgreement {
                    Id = 5000 + i, Provider = provider.PersonalityIdentity, Patient = patient.PersonalityIdentity,
                    Status = NpcServiceStatus.Accepted }), "existing accepted agreement is retained");
            Check.Equal(false, NpcServices.TryPrepare(world.Game, provider, request),
                "provider with eight active agreements cannot offer another");
            Check.Equal(0, provider.Personality.Reactions.Count, "no unrecordable offer is queued");

            provider.Personality.ServiceAgreements.Clear();
            for (int i = 0; i < 8; i++)
                patient.Personality.RememberService(new NpcServiceAgreement {
                    Id = 5100 + i, Provider = provider.PersonalityIdentity, Patient = patient.PersonalityIdentity,
                    Status = NpcServiceStatus.Accepted });
            Check.Equal(false, NpcServices.TryPrepare(world.Game, provider, request),
                "patient with eight active agreements cannot receive another offer");
            Check.Equal(false, NpcServices.CanAccept(patient, provider),
                "an acceptance requires matching retained offers on both sides");
        });
    }
}
