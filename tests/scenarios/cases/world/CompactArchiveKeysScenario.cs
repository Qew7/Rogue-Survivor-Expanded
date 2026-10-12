using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class CompactArchiveKeysScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("storage/compact-archive-keys", () => TownScenarioFactory.Arena(4582, "....", "...."), world =>
        {
            Actor actor = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "archived resident", true, false, 0);
            world.Place(actor, 1, 1);
            ResidentRecord record = Session.Get.ResidentRecords.Register(actor);
            record.Add("note:once", 1, "A note.");
            record.Add("story:shared-cause", 1, "A stage.",
                new ObservedEvent("story_stage", 1, actor.Name, null, true, eventId: 42));
            var event42 = new ObservedEvent("raid", 2, actor.Name, null, true, eventId: 42);
            Session.Get.ResidentRecords.Observe(actor, event42);
            int count = record.EntryCount;

            // Recreate the old field layout, then load it through the real graph reader.
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            foreach (ResidentEntry entry in record.Entries) entry.EventDedupKey = false;
            typeof(ResidentRecord).GetField("m_Keys", fields).SetValue(record,
                new Dictionary<string, bool> { { "spawn", true }, { "note:once", true },
                    { "story:shared-cause", true }, { "event:42", true } });
            typeof(ResidentRecord).GetField("m_KeySet", fields).SetValue(record, null);
            typeof(ResidentRecord).GetField("m_EventKeys", fields).SetValue(record, null);
            using (var stream = new MemoryStream())
            {
                ObjectGraphStore.Write(stream, Session.Get.ResidentRecords);
                stream.Position = 0;
                ResidentRecords old = (ResidentRecords)ObjectGraphStore.Read(stream);
                old.CompactKeys();
                ResidentRecord restored = new List<ResidentRecord>(old.Residents)[0];
                Check.Equal(null, typeof(ResidentRecord).GetField("m_Keys", fields).GetValue(restored),
                    "legacy key dictionary is removed after migration");
                Check.Equal(false, ((HashSet<string>)typeof(ResidentRecord).GetField("m_KeySet", fields)
                    .GetValue(restored)).Contains("event:42"), "event ID does not need a saved string key");
                restored.Add("note:once", 3, "Duplicate note.");
                restored.Add("event:42", 3, "Duplicate event.", event42);
                Check.Equal(count, restored.EntryCount, "legacy note and event remain deduplicated");
                restored.Add("event:43", 3, "New event.", new ObservedEvent("raid", 3, actor.Name, null, true, eventId: 43));
                Check.Equal(count + 1, restored.EntryCount, "new event is still recorded");
            }

            string path = Path.Combine(Path.GetTempPath(), "compact-keys-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                ResidentRecord loaded = new List<ResidentRecord>(BinarySaveStore.LoadExact<Session>(path)
                    .ResidentRecords.Residents)[0];
                loaded.Add("note:once", 4, "Duplicate note.");
                loaded.Add("event:42", 4, "Duplicate event.", event42);
                Check.Equal(count, loaded.EntryCount, "new save rebuilds event deduplication");
                loaded.Add("story:later-cause", 4, "Another stage.",
                    new ObservedEvent("story_stage", 4, actor.Name, null, true, eventId: 44));
                loaded.Add("event:44", 4, "A distinct observation.",
                    new ObservedEvent("raid", 4, actor.Name, null, true, eventId: 44));
                Check.Equal(count + 2, loaded.EntryCount,
                    "a story cause ID does not suppress a distinct observation");
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        });
    }
}
