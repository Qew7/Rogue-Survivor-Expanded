using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class ShelterCareDeadlineScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/shelter-care-deadline", () => TownScenarioFactory.Arena(4698,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 8, 2);
            world.Map.GetTileAt(6, 1).IsInside = true;
            Actor patient = NpcIntentSupport.Actor(world, "patient", 1, 1, "sociable", "trusting");
            Actor provider = NpcIntentSupport.Actor(world, "provider", 2, 1, "sociable", "honest", "humble");
            Actor listener = NpcIntentSupport.Actor(world, "listener", 3, 1, "lawful");
            patient.HitPoints = 1;
            var medicine = new ItemMedicine(world.Game.GameItems.MEDIKIT); provider.Inventory.AddAll(medicine);
            provider.Personality.Knowledge.RememberPlace(new NpcKnownPlace(new Location(world.Map, new Point(6, 1)), "shelter", 0));
            PersonalitySystem.Report(world.Game, new SignificantEvent("requested_medicine", patient, provider,
                world.Map, patient.Location.Position, 0));
            NpcIntentSupport.Turn(world, provider);
            world.Map.LocalTime.TurnCounter = 1; NpcIntentSupport.Turn(world, patient);
            Check.Equal(NpcServiceStatus.Accepted, patient.Personality.ServiceAgreements[0].Status, "patient accepted a real offer");
            world.Map.LocalTime.TurnCounter = 181;
            NpcServices.Expire(world.Game.NpcContent, patient); NpcServices.Expire(world.Game.NpcContent, provider);
            Check.Equal(NpcServiceStatus.Failed, patient.Personality.ServiceAgreements[0].Status,
                "uncompleted accepted agreement expires");
            Check.Equal(true, patient.Personality.Person(provider.PersonalityIdentity).Grievance > 0,
                "missed deadline creates a personal grievance");
            Check.Equal(true, NpcIntentSupport.HasEvent(patient, "shelter_care_failed"), "private conclusion is archived");
            Check.Equal(false, NpcIntentSupport.HasEvent(listener, "shelter_care_failed"),
                "deadline alone does not publicly accuse provider");
            Check.Equal(true, provider.Inventory.Contains(medicine), "failed service does not create or consume medicine");
            NpcFact assessment = patient.Personality.Knowledge.Facts.Find(f => f.Kind == "shelter_care_failed");
            int before = listener.Personality.Person(provider.PersonalityIdentity) == null ? 0 :
                listener.Personality.Person(provider.PersonalityIdentity).Grievance;
            Check.Equal(true, world.Try(new ActionNpcTell(patient, world.Game, listener, assessment)),
                "patient can later tell the private assessment");
            Check.Equal(true, listener.Personality.Person(provider.PersonalityIdentity).Grievance > before,
                "told assessment changes reputation without making listener a witness");
        });
    }
}
