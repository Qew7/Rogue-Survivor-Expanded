using System;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class MedicineRequestPacingScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/medicine-request-pacing", () => TownScenarioFactory.Arena(8191,
            ".........", ".........", "........."), world =>
        {
            Session original = Session.Get;
            original.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 8, 2);
            Actor patient = NpcIntentSupport.Actor(world, "patient", 1, 1, "sociable");
            Actor neighbor = NpcIntentSupport.Actor(world, "neighbor", 2, 1);
            patient.HitPoints = 1;
            NpcIntentSupport.Turn(world, patient);
            Check.Equal(true, NpcIntentSupport.HasEvent(neighbor, "requested_medicine"),
                "a wounded NPC can really ask a nearby person for medicine");
            int nextGlobal = patient.Personality.Knowledge.NextMedicineRequestTurn;
            NpcKnownPerson known = patient.Personality.Knowledge.Person(neighbor.PersonalityIdentity);
            Check.Equal(4 * WorldTime.TURNS_PER_HOUR, nextGlobal,
                "a request starts a four-hour pause before asking anyone again");
            Check.Equal(WorldTime.TURNS_PER_DAY, known.NextMedicineRequestTurn,
                "the same person is not asked again until the next day, even under a new goal");
            Actor other = NpcIntentSupport.Actor(world, "other", 5, 1);
            patient.Personality.Knowledge.See(other, 1);
            Check.Equal(false, NpcMedicineActions.CanAsk(patient, other.PersonalityIdentity, nextGlobal - 1),
                "a new recipient does not bypass the global pause");
            Check.Equal(true, NpcMedicineActions.CanAsk(patient, other.PersonalityIdentity, nextGlobal),
                "another contact becomes available when the global pause ends");
            Check.Equal(false, NpcMedicineActions.CanAsk(patient, neighbor.PersonalityIdentity, nextGlobal),
                "recreating the goal cannot immediately ask the original recipient again");
            patient.Personality.Knowledge.LearnPerson(new NpcKnownPerson {
                Id = neighbor.PersonalityIdentity, Name = neighbor.UnmodifiedName, Place = neighbor.Location,
                SeenTurn = 1, Confidence = 100 });
            Check.Equal(WorldTime.TURNS_PER_DAY,
                patient.Personality.Knowledge.Person(neighbor.PersonalityIdentity).NextMedicineRequestTurn,
                "a fresh sighting preserves the request history");
            Guid patientId = patient.PersonalityIdentity;
            string path = Path.Combine(Path.GetTempPath(), "medicine-pacing-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, original);
                Session loaded = BinarySaveStore.Load<Session>(path);
                Map map = loaded.World[0, 0].EntryMap; map.ReconstructAuxiliaryFields();
                Actor restored = NpcIntentSupport.Find(map, patientId);
                Check.Equal(nextGlobal, restored.Personality.Knowledge.NextMedicineRequestTurn,
                    "the global request pause survives save and load");
                Check.Equal(WorldTime.TURNS_PER_DAY,
                    restored.Personality.Knowledge.Person(neighbor.PersonalityIdentity).NextMedicineRequestTurn,
                    "the recipient's daily limit survives save and load");
            }
            finally
            {
                Session.Restore(original);
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
