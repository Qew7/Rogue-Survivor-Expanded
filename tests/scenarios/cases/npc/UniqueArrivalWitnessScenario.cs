using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class UniqueArrivalWitnessScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/unique-arrival-witness", () => TownScenarioFactory.Arena(4822,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor unique = NpcIntentSupport.Actor(world, "unique", 1, 1);
            Actor witness = NpcIntentSupport.Actor(world, "witness", 2, 1);
            PersonalitySystem.Report(world.Game, new SignificantEvent("unique_arrival", unique, null,
                world.Map, unique.Location.Position, world.Map.LocalTime.TurnCounter, false, false));
            ResidentRecord own = Session.Get.ResidentRecords.Register(unique);
            ResidentRecord other = Session.Get.ResidentRecords.Register(witness);
            foreach (ResidentEntry entry in own.Entries)
                Check.Equal(false, entry.Kind == "unique_arrival", "unique does not witness own arrival");
            bool witnessed = false;
            foreach (ResidentEntry entry in other.Entries)
                if (entry.Kind == "unique_arrival") witnessed = true;
            Check.Equal(true, witnessed, "neighbor witnesses unique arrival");
            own.Add("legacy-self-witness", 0, "Witnessed my own arrival.",
                new ObservedEvent("unique_arrival", 0, unique.UnmodifiedName, null, false,
                    subjectId: unique.PersonalityIdentity));
            IList<string> lines = RecordsReader.Lines(new RecordsSave("old", 0, Session.Get.ResidentRecords), own);
            Check.Equal(false, String.Join(" ", new List<string>(lines).ToArray()).Contains("Witnessed my own arrival"),
                "Read Records hides self-witness entries already present in a save");
        });
    }
}
