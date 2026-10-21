using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

static class CacheNoteDecisionScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/reads-carried-note", () => TownScenarioFactory.Arena(4745,
            "..........", "..........", ".........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor reader = NpcIntentSupport.Actor(world, "reader", 1, 1, "curious");
            Actor author = NpcIntentSupport.Actor(world, "author", 7, 1);
            NpcIntentSupport.Player(world, 9, 2);
            StorageContainer cache = new StorageContainer("drawer", "closed", "open", 40, 2);
            world.Map.PlaceMapObjectAt(cache, new Point(6, 1));
            cache.Contents.AddAll(new ItemFood(world.Game.GameItems.CANNED_FOOD));
            ItemNote note = new ItemNote(world.Game.GameItems.NOTE);
            note.Write(author, NoteTopic.CacheSupplies, cache.Location, cache.Id, "food", 1, false);
            reader.Inventory.AddAll(note);
            Check.Equal(0, reader.Personality.Knowledge.Caches.Count,
                "carrying the note does not grant cache knowledge");
            for (int i = 0; i < 8 && !reader.Personality.Knowledge.HasReadNote(note.Id); i++)
            {
                reader.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(reader);
                world.Map.LocalTime.TurnCounter++;
            }
            Check.Equal(true, reader.Personality.Knowledge.HasReadNote(note.Id),
                "civilian AI chooses a real reading action");
            Check.Equal(1, reader.Personality.Knowledge.Caches.Count,
                "reading supplies one bounded cache belief");
            NpcKnownCache known = reader.Personality.Knowledge.Caches[0];
            Check.Equal(cache.Id, known.Id, "reader learns the described physical cache ID");
            Check.Equal(NpcKnowledgeSource.Told, known.Source, "paper is a report, not direct sight");
            Check.Equal(Guid.Empty, note.SignedAuthorId, "anonymous note does not reveal the author");
            int revision = reader.Personality.Knowledge.Revision;
            reader.ActionPoints = Rules.BASE_ACTION_COST;
            world.NpcTurn(reader);
            Check.Equal(revision, reader.Personality.Knowledge.Revision,
                "AI does not reread a completed note goal");

            string path = Path.Combine(Path.GetTempPath(), "npc-note-" + Guid.NewGuid().ToString("N"));
            try
            {
                Guid readerId = reader.PersonalityIdentity;
                BinarySaveStore.Save(path, world.Map);
                Map loaded = BinarySaveStore.Load<Map>(path);
                loaded.ReconstructAuxiliaryFields();
                Actor restored = NpcIntentSupport.Find(loaded, readerId);
                Check.Equal(true, restored.Personality.Knowledge.HasReadNote(note.Id),
                    "read-once mark survives save");
                Check.Equal(cache.Id, restored.Personality.Knowledge.Caches[0].Id,
                    "reported cache identity survives save");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
