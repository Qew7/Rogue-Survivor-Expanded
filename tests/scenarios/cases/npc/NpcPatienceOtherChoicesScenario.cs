using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class NpcPatienceOtherChoicesScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/patience-other-choices", () => TownScenarioFactory.Arena(8196,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 8, 2);
            Actor apologizer = NpcIntentSupport.Actor(world, "apologizer", 1, 1);
            Actor patient = NpcIntentSupport.Actor(world, "patient", 2, 1, "patient");
            Actor impatient = NpcIntentSupport.Actor(world, "impatient", 3, 1, "hotheaded");
            patient.Personality.Opinion(apologizer.PersonalityIdentity, apologizer.UnmodifiedName).Grievance = 30;
            impatient.Personality.Opinion(apologizer.PersonalityIdentity, apologizer.UnmodifiedName).Grievance = 30;
            NpcEvents.Publish(world.Game, "apologized", apologizer, patient);
            NpcIntentSupport.Turn(world, patient);
            Check.Equal(true, NpcIntentSupport.HasEvent(apologizer, "apology_accepted"),
                "a patient person accepts an apology for a moderate grievance");
            NpcEvents.Publish(world.Game, "apologized", apologizer, impatient);
            NpcIntentSupport.Turn(world, impatient);
            Check.Equal(true, NpcIntentSupport.HasEvent(apologizer, "apology_refused"),
                "an impatient person keeps the same grievance despite the apology");

            Location place = new Location(world.Map, new Point(5, 1));
            patient.Personality.RememberDispute(new NpcResourceDispute {
                Place = place, Other = apologizer.PersonalityIdentity, Response = "resource_refused", Turn = 0 });
            impatient.Personality.RememberDispute(new NpcResourceDispute {
                Place = place, Other = apologizer.PersonalityIdentity, Response = "resource_refused", Turn = 0 });
            world.Map.LocalTime.TurnCounter = 100;
            Check.Equal(true, NpcResourceCompetition.RespectRefusal(patient, place),
                "the patient NPC still honors a refusal after one hundred turns");
            Check.Equal(false, NpcResourceCompetition.RespectRefusal(impatient, place),
                "the impatient NPC stops waiting on the same refusal sooner");
            int civilian = PersonalitySystem.Disposition(patient, NpcPersonalityAxis.Patience, world.Game.NpcContent);
            patient.Faction = world.Game.GameFactions.ThePolice;
            Check.Equal(true, PersonalitySystem.Disposition(patient, NpcPersonalityAxis.Patience, world.Game.NpcContent) > civilian,
                "faction changes the starting patience baseline");
            patient.Faction = world.Game.GameFactions.TheCivilians;
            patient.Personality.RemoveTrait("patient");
            Check.Equal(true, PersonalitySystem.Disposition(patient, NpcPersonalityAxis.Patience, world.Game.NpcContent) < civilian,
                "losing the trait changes patience without rewriting experience");
        });
    }
}
