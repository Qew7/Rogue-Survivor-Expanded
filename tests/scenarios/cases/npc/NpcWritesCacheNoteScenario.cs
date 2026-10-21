using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

static class NpcWritesCacheNoteScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/writes-cache-note", () => TownScenarioFactory.Arena(4772,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor petr = NpcIntentSupport.Actor(world, "Petr", 1, 1, "curious");
            NpcIntentSupport.Player(world, 6, 2);
            StorageContainer cache = new StorageContainer("drawer", "closed", "open", 40, 3);
            world.Map.PlaceMapObjectAt(cache, new Point(2, 1));
            world.Try(new ActionUseStorageContainer(petr, world.Game, cache, true));
            ItemFood food = NpcIntentSupport.Food(world, petr, 1);
            world.Map.PlaceActorAt(petr, new Point(2, 1));
            world.Try(new ActionDropItem(petr, world.Game, food));
            world.Map.PlaceActorAt(petr, new Point(1, 1));
            ItemNote paper = new ItemNote(world.Game.GameItems.NOTE);
            petr.Inventory.AddAll(paper);
            for (int i = 0; i < 12 && paper.IsBlank; i++)
            {
                petr.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(petr);
                world.Map.LocalTime.TurnCounter++;
            }
            Check.Equal(false, paper.IsBlank, "NPC writes on physical blank paper");
            Check.Equal(NoteTopic.CacheSupplies, paper.Topic, "note describes observed supplies");
            Check.Equal(cache.Id, paper.CacheId, "note refers to the physical cache");
            Check.Equal(true, petr.Personality.Knowledge.Caches[0].HasWrittenNote,
                "NPC remembers the recorded cache state");
            int writtenUnits = petr.Personality.Knowledge.Caches[0].WrittenUnits;
            petr.Inventory.AddAll(new ItemNote(world.Game.GameItems.NOTE));
            for (int i = 0; i < 4; i++)
            {
                petr.ActionPoints = Rules.BASE_ACTION_COST;
                world.NpcTurn(petr);
                world.Map.LocalTime.TurnCounter++;
            }
            Check.Equal(writtenUnits, petr.Personality.Knowledge.Caches[0].WrittenUnits,
                "unchanged cache state does not generate another revision");

            string path = Path.Combine(Path.GetTempPath(), "npc-cache-written-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, world.Map);
                Map loaded = BinarySaveStore.Load<Map>(path);
                loaded.ReconstructAuxiliaryFields();
                Actor saved = NpcIntentSupport.Find(loaded, petr.PersonalityIdentity);
                Check.Equal(true, saved.Personality.Knowledge.Caches[0].HasWrittenNote,
                    "written revision survives save and load");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
