using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

static class CacheInspectionScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/cache-inspection", () => TownScenarioFactory.Arena(4741,
            "......", "......", "......", "......"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor author = NpcIntentSupport.Actor(world, "author", 1, 1);
            Actor reader = NpcIntentSupport.Actor(world, "reader", 2, 1);
            StorageContainer cache = new StorageContainer("drawer", "closed", "open", 40, 3);
            world.Map.PlaceMapObjectAt(cache, new Point(3, 1));
            ItemFood food = new ItemFood(world.Game.GameItems.GROCERIES);
            cache.Contents.AddAll(food);
            ItemNote note = new ItemNote(world.Game.GameItems.NOTE);
            note.Write(author, NoteTopic.CacheSupplies, cache.Location, cache.Id, "food", 1, false);
            reader.Inventory.AddAll(note);
            Check.Equal(0, reader.Personality.Knowledge.Caches.Count,
                "carrying a note does not reveal its contents");
            Check.Equal(true, world.Try(new ActionUseItem(reader, world.Game, note)),
                "reader uses the physical note");
            NpcKnownCache known = reader.Personality.Knowledge.Caches[0];
            Check.Equal(NpcKnowledgeSource.Told, known.Source, "note remains a report");
            Check.Equal(new Point(3, 1), known.Place.Position, "note stores the old location");
            Check.Equal(1, known.Units, "note supplies are a snapshot");
            int revision = reader.Personality.Knowledge.Revision;
            world.Try(new ActionUseItem(reader, world.Game, note));
            Check.Equal(revision, reader.Personality.Knowledge.Revision,
                "reading the same note twice gives no new knowledge");
            Check.Equal(false, world.Try(new ActionInspectStorage(reader, world.Game, cache)),
                "closed contents cannot be inspected");
            Check.Equal(1, reader.Personality.Knowledge.Caches.Count,
                "failed inspection gives no new knowledge");

            world.Map.PlaceMapObjectAt(cache, new Point(3, 2));
            Check.Equal(new Point(3, 1), known.Place.Position,
                "moving furniture does not update remembered location");
            Check.Equal(true, world.Try(new ActionUseStorageContainer(reader, world.Game, cache, true)),
                "reader opens the relocated drawer");
            Check.Equal(true, world.Try(new ActionInspectStorage(reader, world.Game, cache)),
                "inspection observes current contents");
            known = reader.Personality.Knowledge.Caches[0];
            Check.Equal(NpcKnowledgeSource.Witness, known.Source, "inspection is direct observation");
            Check.Equal(new Point(3, 2), known.Place.Position, "inspection refreshes location");
            cache.Contents.RemoveAllQuantity(food);
            Check.Equal(true, world.Try(new ActionInspectStorage(reader, world.Game, cache)),
                "empty container can still be inspected");
            Check.Equal(0, reader.Personality.Knowledge.Caches[0].Units,
                "empty inspection clears remembered supplies");
            world.Try(new ActionUseItem(reader, world.Game, note));
            Check.Equal(0, reader.Personality.Knowledge.Caches[0].Units,
                "stale note cannot restore removed supplies");

            string path = Path.Combine(Path.GetTempPath(), "cache-knowledge-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, world.Map);
                Map loaded = BinarySaveStore.Load<Map>(path);
                loaded.ReconstructAuxiliaryFields();
                Actor savedReader = loaded.GetActorAt(2, 1);
                NpcKnownCache saved = savedReader.Personality.Knowledge.Caches[0];
                Check.Equal(0, saved.Units, "empty knowledge survives save");
                Check.Equal(new Point(3, 2), saved.Place.Position, "known location survives save");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
