using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay.AI.Sensors;
using djack.RogueSurvivor.Gameplay.Personality;

static class CacheLossDiscoveryScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/cache-loss-discovered", () => TownScenarioFactory.Arena(4764,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Map.Lighting = Lighting.LIT;
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1);
            Actor thief = NpcIntentSupport.Actor(world, "thief", 3, 1);
            NpcIntentSupport.Player(world, 8, 2);
            Point at = new Point(2, 1);
            StorageContainer cache = new StorageContainer("drawer", "closed", "open", 40, 3);
            world.Map.PlaceMapObjectAt(cache, at);
            Check.Equal(true, world.Try(new ActionUseStorageContainer(owner, world.Game, cache, true)),
                "owner opens the drawer");
            ItemFood food = NpcIntentSupport.Food(world, owner, 1);
            world.Map.PlaceActorAt(owner, at);
            Check.Equal(true, world.Try(new ActionDropItem(owner, world.Game, food)),
                "owner stores claimed food");
            Check.Equal(1, owner.Personality.Knowledge.Caches[0].OwnUnits,
                "owner remembers one personal unit");

            world.Map.PlaceActorAt(owner, new Point(6, 1));
            world.SetTile(4, 1, '#');
            Check.Equal(true, world.Try(new ActionGetFromContainer(thief, world.Game, at)),
                "thief removes food out of sight");
            Check.Equal(false, owner.Personality.Knowledge.Facts.Exists(f => f.Kind == "cache_theft"),
                "ownership does not reveal an unseen theft");
            world.Map.PlaceActorAt(thief, at);
            ItemMedicine replacement = new ItemMedicine(world.Game.GameItems.MEDIKIT);
            thief.Inventory.AddAll(replacement);
            Check.Equal(true, world.Try(new ActionDropItem(thief, world.Game, replacement)),
                "thief stores visibly different supplies");
            ItemNote note = new ItemNote(world.Game.GameItems.NOTE);
            note.Write(thief, NoteTopic.Location, cache.Location, cache.Id, null, 0, false);
            thief.Inventory.AddAll(note);
            Check.Equal(true, world.Try(new ActionDropItem(thief, world.Game, note)),
                "thief leaves a note");
            world.Map.PlaceActorAt(thief, new Point(7, 1));
            world.Map.PlaceActorAt(owner, new Point(1, 1));
            var sensor = new LOSSensor(LOSSensor.SensingFilter.ACTORS);
            NpcKnowledgeSystem.Perceive(world.Game, owner,
                sensor.Sense(world.Game, owner), sensor.FOV);
            Check.Equal("food", owner.Personality.Knowledge.Caches[0].OwnResource,
                "ordinary sight does not erase the remembered personal resource");
            Check.Equal(true, world.Try(new ActionInspectStorage(owner, world.Game, cache)),
                "owner inspects a nonempty drawer");
            NpcFact loss = owner.Personality.Knowledge.Facts.Find(f => f.Kind == "cache_loss_discovered");
            Check.Equal(true, loss != null, "unrelated contents do not hide the loss");
            Check.Equal(1, loss.Units, "only the missing personal unit is reported");
            Check.Equal(Guid.Empty, loss.OtherId, "inspection cannot name an unseen thief");
            Check.Equal(0, owner.Personality.Knowledge.Caches[0].OwnUnits,
                "inspection updates expected personal contents");
            Check.Equal(true, world.Try(new ActionInspectStorage(owner, world.Game, cache)),
                "owner may inspect again");
            Check.Equal(1, owner.Personality.Knowledge.Facts.FindAll(f => f.Kind == "cache_loss_discovered").Count,
                "repeated inspection does not duplicate the loss");

            string path = Path.Combine(Path.GetTempPath(), "cache-loss-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, world.Map);
                Map loaded = BinarySaveStore.Load<Map>(path);
                loaded.ReconstructAuxiliaryFields();
                Actor saved = loaded.GetActorAt(1, 1);
                Check.Equal(0, saved.Personality.Knowledge.Caches[0].OwnUnits,
                    "updated expectation survives save and load");
                Check.Equal(1, saved.Personality.Knowledge.Facts.FindAll(f => f.Kind == "cache_loss_discovered").Count,
                    "one anonymous loss survives save and load");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
