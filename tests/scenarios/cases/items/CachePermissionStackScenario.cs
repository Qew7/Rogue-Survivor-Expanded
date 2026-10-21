using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

static class CachePermissionStackScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("items/cache-permission-stack", () => TownScenarioFactory.Arena(4759,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1);
            Actor taker = NpcIntentSupport.Actor(world, "taker", 3, 1);
            NpcIntentSupport.Player(world, 6, 2);
            StorageContainer cache = new StorageContainer("fridge", "closed", "open", 80, 2);
            Point at = new Point(2, 1);
            world.Map.PlaceMapObjectAt(cache, at);
            world.Try(new ActionUseStorageContainer(owner, world.Game, cache, true));
            ItemFood food = NpcIntentSupport.Food(world, owner, 2);
            world.Map.PlaceActorAt(owner, at);
            world.Try(new ActionDropItem(owner, world.Game, food));
            world.Map.PlaceActorAt(owner, new Point(1, 1));
            taker.Personality.Permit(new NpcSupplyPermission {
                Grantor = owner.PersonalityIdentity, Place = cache.Location,
                Resource = "food", Units = 1, ExpiresTurn = 100, CauseId = 42 });
            Check.Equal(true, world.Try(new ActionGetFromContainer(taker, world.Game, at)),
                "taker takes a two-unit stack with one-unit permission");
            Check.Equal(0, cache.Contents.CountItems, "both transferred units leave the cache");
            Check.Equal(2, taker.Inventory.CountItems,
                "lawful and stolen portions remain separate stacks");
            int clean = 0, stolen = 0;
            foreach (Item item in taker.Inventory.Items)
            {
                if (item.IsStolen) stolen += item.Quantity;
                else clean += item.Quantity;
            }
            Check.Equal(1, clean, "only the permitted unit stays clean");
            Check.Equal(1, stolen, "only the excess unit is marked stolen");
            Check.Equal(1, owner.Personality.Knowledge.Facts.Find(f => f.Kind == "permission_used").Units,
                "permission event reports the lawful quantity");
            Check.Equal(1, owner.Personality.Knowledge.Facts.Find(f => f.Kind == "cache_theft").Units,
                "theft event reports only the excess quantity");
            Check.Equal(0, taker.Personality.Permissions.Count, "permission is consumed once");

            string path = Path.Combine(Path.GetTempPath(), "cache-permission-" + Guid.NewGuid().ToString("N"));
            try
            {
                Guid takerId = taker.PersonalityIdentity;
                BinarySaveStore.Save(path, world.Map);
                Map loaded = BinarySaveStore.Load<Map>(path);
                loaded.ReconstructAuxiliaryFields();
                Actor restored = NpcIntentSupport.Find(loaded, takerId);
                int savedClean = 0, savedStolen = 0;
                foreach (Item item in restored.Inventory.Items)
                {
                    if (item.IsStolen) savedStolen += item.Quantity;
                    else savedClean += item.Quantity;
                }
                Check.Equal(1, savedClean, "lawful portion survives save");
                Check.Equal(1, savedStolen, "stolen portion survives save");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
