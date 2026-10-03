using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
                AssertArchiveStoredOnce(path, original);
                RecordsSave saved = RecordsReader.Load(path);
                Check.Same(original, Session.Get, "reading records leaves active session untouched");
                Check.Same(world.Map, original.CurrentMap,
                    "reader leaves current map untouched");
                Check.Equal(1, RecordsReader.Residents(saved).Count, "saved resident is loaded independently");
                string all = String.Join(" ", new List<string>(RecordsReader.Lines(saved, null)).ToArray());
                Check.Equal(true, all.Contains("faced starvation"), "saved event can be read");
                IList<string> returned = RecordsReader.Lines(saved, null);
                returned.Clear();
                Check.Equal(true, String.Join(" ", RecordsReader.Lines(saved, null)).Contains("faced starvation"),
                    "changing returned lines does not change cached archive text");
                RecordsReader.Residents(saved)[0].Add("note:cache", 1, "Archive cache refreshes.");
                Check.Equal(true, String.Join(" ", RecordsReader.Lines(saved, null)).Contains("Archive cache refreshes."),
                    "new archive entries invalidate cached lines");
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
                Check.Equal(original.ResidentRecords.Residents.Count,
                    BinarySaveStore.LoadExact<Session>(Path.Combine(directory, "disabled.dat")).ResidentRecords.Residents.Count,
                    "disabled preset still preserves existing archive on full load");
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

    static void AssertArchiveStoredOnce(string path, Session original)
    {
        using (FileStream file = File.OpenRead(path))
        {
            BinaryReader reader = new BinaryReader(file);
            reader.ReadBytes(5); reader.ReadString();
            int mods = reader.ReadInt32();
            for (int i = 0; i < mods; i++) { reader.ReadString(); reader.ReadString(); }
            Check.Equal(true, reader.ReadBoolean(), "session has an independent records section");
            Check.Equal(true, reader.ReadBoolean(), "records section is enabled");
            reader.ReadInt32(); long length = reader.ReadInt64(); file.Position += length;
            using (GZipStream gzip = new GZipStream(file, CompressionMode.Decompress))
            {
                Session graph = (Session)ObjectGraphStore.Read(gzip);
                Check.Equal(null, typeof(Session).GetField("m_ResidentRecords",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(graph),
                    "world graph does not duplicate the resident archive");
            }
            Check.Equal(1, original.ResidentRecords.Residents.Count,
                "writing the independent section keeps the live archive");
        }
    }
}
