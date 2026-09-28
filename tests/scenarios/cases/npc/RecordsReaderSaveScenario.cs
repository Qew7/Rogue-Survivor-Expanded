using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class RecordsReaderSaveScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/records-reader-save", () => TownScenarioFactory.Arena(4561,
            ".....", ".....", "....."), world =>
        {
            Session original = Session.Get;
            original.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor actor = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "resident", true, false, 0);
            actor.Personality = new PersonalityState();
            world.Place(actor, 1, 1);
            PersonalitySystem.Report(world.Game, new SignificantEvent("starvation", actor, null,
                world.Map, actor.Location.Position, 1));
            original.WorldTime.TurnCounter = 1;
            string directory = Path.Combine(Path.GetTempPath(), "records-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                string path = Path.Combine(directory, "chronicle.dat");
                world.Map.RemoveActor(actor);
                BinarySaveStore.Save(path, original);
                RecordsSave saved = RecordsReader.Load(path);
                Check.Same(original, Session.Get, "reading records leaves active session untouched");
                Check.Same(world.Map, original.CurrentMap,
                    "reader leaves current map untouched");
                Check.Equal(1, RecordsReader.Residents(saved).Count, "saved resident is loaded independently");
                string all = String.Join(" ", new List<string>(RecordsReader.Lines(saved, null)).ToArray());
                Check.Equal(true, all.Contains("faced starvation"), "saved event can be read");
                Check.Equal(false, saved.Records.IsPartial, "new save has complete recorded history");
                world.Place(actor, 1, 1);
                typeof(Session).GetField("m_ResidentRecords", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(original, null);
                BinarySaveStore.Save(Path.Combine(directory, "older.dat"), original);
                RecordsSave older = RecordsReader.Load(Path.Combine(directory, "older.dat"));
                Check.Equal(true, older.Records.IsPartial, "older save receives a partial archive");
                Check.Equal(true, String.Join(" ", new List<string>(RecordsReader.Lines(older, null)).ToArray())
                    .Contains("faced starvation"), "available observations are recovered from older save");
                GamePreset disabled = GamePreset.BuiltIn(GameMode.GM_STANDARD);
                disabled.NpcPersonalitiesEnabled = false;
                original.GamePreset = disabled;
                BinarySaveStore.Save(Path.Combine(directory, "disabled.dat"), original);
                File.WriteAllText(Path.Combine(directory, "broken.dat"), "not a save");
                File.Copy(path, Path.Combine(directory, "broken.dat.bak"));
                bool rejected = false;
                try { RecordsReader.Load(Path.Combine(directory, "broken.dat")); }
                catch (Exception) { rejected = true; }
                Check.Equal(true, rejected, "reader does not silently substitute a corrupt save's backup");
                int skipped;
                List<RecordsSave> found = RecordsReader.Find(directory, out skipped);
                Check.Equal(3, found.Count, "reader lists new, older and explicitly selected backup saves");
                Check.Equal(2, skipped, "disabled and corrupt primary saves are skipped");
                Check.Same(original, Session.Get, "scanning also leaves session untouched");
            }
            finally { Directory.Delete(directory, true); }
        });
    }
}
