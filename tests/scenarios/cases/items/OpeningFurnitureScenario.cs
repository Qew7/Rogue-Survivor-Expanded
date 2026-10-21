using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay;
using djack.RogueSurvivor.Gameplay.Generators;

static class OpeningFurnitureScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("items/opening-furniture", () => TownScenarioFactory.Arena(4791,
            "............", "............", "............"), world =>
        {
            GamePreset preset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Session.Get.GamePreset = preset;
            BaseTownGenerator generator = new BaseTownGenerator(world.Game, BaseTownGenerator.DEFAULT_PARAMS);
            string[] images = { GameImages.OBJ_WARDROBE, GameImages.OBJ_HOSPITAL_WARDROBE,
                GameImages.OBJ_NIGHT_TABLE, GameImages.OBJ_HOSPITAL_NIGHT_TABLE };
            string[] methods = { "MakeObjWardrobe", "MakeObjWardrobe", "MakeObjNightTable", "MakeObjNightTable" };
            StorageContainer[] storage = new StorageContainer[images.Length];
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1);
            Actor thief = NpcIntentSupport.Actor(world, "thief", 3, 1);
            NpcIntentSupport.Player(world, 11, 2);
            for (int i = 0; i < images.Length; i++)
            {
                Point at = new Point(2 + 2 * i, 1);
                storage[i] = (StorageContainer)Check.CallOn(typeof(BaseMapGenerator), generator, methods[i], images[i]);
                world.Map.PlaceMapObjectAt(storage[i], at);
                Check.Equal(true, storage[i].IsOpen, "empty furniture starts open");
                Check.Equal(images[i] + "_open", storage[i].ImageID, "open sprite follows furniture variant");
                world.Map.PlaceActorAt(owner, new Point(at.X, 2));
                Check.Equal(false, world.Try(new ActionUseStorageContainer(owner, world.Game, storage[i], true)),
                    "already open furniture rejects duplicate opening");
                Check.Equal(true, world.Try(new ActionUseStorageContainer(owner, world.Game, storage[i], false)),
                    "furniture closes through the shared action");
                Check.Equal(images[i], storage[i].ImageID, "closed sprite follows furniture variant");
                Check.Equal(false, storage[i].IsWalkable, "closed furniture blocks movement");
                Check.Equal(true, world.Try(new ActionUseStorageContainer(owner, world.Game, storage[i], true)),
                    "furniture reopens through the shared action");
                if (i == 0) continue;
                ItemFood startingFood = new ItemFood(world.Game.GameItems.CANNED_FOOD);
                Check.CallOn(typeof(BaseMapGenerator), generator, "AddStartingContainerItem",
                    new DiceRoller(4791 + i), world.Map, at, storage[i], startingFood);
                Check.Equal(true, storage[i].Contents.Contains(startingFood), "starting item is inside furniture");
                Check.Equal(null, world.Map.GetItemsAt(at), "starting item is not ground loot");
            }

            world.Map.PlaceActorAt(owner, new Point(1, 1));
            ItemFood claimedFood = NpcIntentSupport.Food(world, owner, 2);
            world.Map.PlaceActorAt(owner, new Point(2, 1));
            Check.Equal(true, world.Try(new ActionDropItem(owner, world.Game, claimedFood)),
                "owner stores food in wardrobe");
            world.Map.PlaceActorAt(owner, new Point(1, 1));
            Check.Equal(owner.PersonalityIdentity, claimedFood.StorageClaimantId,
                "wardrobe preserves the owner's claim");

            string path = Path.Combine(Path.GetTempPath(), "opening-furniture-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, world.Map);
                Map loaded = BinarySaveStore.Load<Map>(path);
                loaded.ReconstructAuxiliaryFields();
                for (int i = 0; i < images.Length; i++)
                {
                    StorageContainer saved = (StorageContainer)loaded.GetMapObjectAt(2 + 2 * i, 1);
                    Check.Equal(storage[i].Id, saved.Id, "furniture identity survives save");
                    Check.Equal(storage[i].ImageID, saved.ImageID, "furniture sprite survives save");
                    Check.Equal(storage[i].IsOpen, saved.IsOpen, "furniture state survives save");
                    Check.Equal(storage[i].Contents.CountItems, saved.Contents.CountItems,
                        "furniture contents survive save");
                }
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }

            Check.Equal(true, world.Try(new ActionGetFromContainer(thief, world.Game, new Point(2, 1))),
                "thief takes claimed food from wardrobe");
            Check.Equal(true, claimedFood.IsStolen, "wardrobe theft marks stolen provenance");
            Check.Equal(true, owner.Personality.Knowledge.Facts.Exists(f => f.Kind == "cache_theft"),
                "social system witnesses wardrobe theft");

            Session.Get.GamePreset.CachesEnabled = false;
            for (int i = 0; i < images.Length; i++)
            {
                MapObject legacy = (MapObject)Check.CallOn(typeof(BaseMapGenerator), generator, methods[i], images[i]);
                Check.Equal(false, legacy is StorageContainer, "disabled caches keep legacy furniture");
            }

            for (int variant = 0; variant < 2; variant++)
            {
                ModCatalog.Select(variant == 0 ? null : ModCatalog.Discover("mods"));
                try
                {
                    for (int i = 0; i < images.Length; i++)
                    {
                        string pathToSprite = ModCatalog.Resolve("Images", images[i] + "_open.png");
                        Check.Equal(true, File.Exists(pathToSprite), "open sprite exists in selected graphics mode");
                        using (Bitmap sprite = new Bitmap(pathToSprite))
                        {
                            Check.Equal(32, sprite.Width, "open sprite is one tile wide");
                            Check.Equal(32, sprite.Height, "open sprite is one tile high");
                        }
                    }
                }
                finally { ModCatalog.Select(null); }
            }
        });
    }
}
