using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

static class StorageClaimsScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("items/storage-claims", () => TownScenarioFactory.Arena(4743,
            ".....", ".....", "....."), world =>
        {
            Actor anna = NpcIntentSupport.Actor(world, "Anna", 1, 1);
            Actor boris = NpcIntentSupport.Actor(world, "Boris", 3, 1);
            Actor clara = NpcIntentSupport.Actor(world, "Clara", 4, 1);
            world.SetPlayer(anna);
            StorageContainer cache = new StorageContainer("fridge", "closed", "open", 80, 2);
            world.Map.PlaceMapObjectAt(cache, new Point(2, 1));
            Check.Equal(true, world.Try(new ActionUseStorageContainer(anna, world.Game, cache, true)),
                "owner opens the fridge");
            ItemFood annaFood = NpcIntentSupport.Food(world, anna, 1);
            world.Map.PlaceActorAt(anna, new Point(2, 1));
            Check.Equal(true, world.Try(new ActionDropItem(anna, world.Game, annaFood)),
                "first claimant stores food");
            Check.Equal(anna.PersonalityIdentity, annaFood.StorageClaimantId,
                "placed stack retains first claimant");
            world.Map.PlaceActorAt(anna, new Point(1, 1));
            world.Map.PlaceActorAt(boris, new Point(2, 1));
            ItemFood borisFood = NpcIntentSupport.Food(world, boris, 1);
            Check.Equal(true, world.Try(new ActionDropItem(boris, world.Game, borisFood)),
                "second claimant stores identical food");
            Check.Equal(2, cache.Contents.CountItems, "different claimants do not merge");
            Check.Equal(boris.PersonalityIdentity, borisFood.StorageClaimantId,
                "second stack retains second claimant");

            ItemFood moreBoris = NpcIntentSupport.Food(world, boris, 1);
            Check.Equal(true, world.Try(new ActionDropItem(boris, world.Game, moreBoris)),
                "full fridge accepts more of the same claimant's stack");
            Check.Equal(2, cache.Contents.CountItems, "same claimant's food merges");
            Check.Equal(2, borisFood.Quantity, "quantity merged into Boris's stack");
            world.Map.PlaceActorAt(boris, new Point(3, 1));
            world.Map.PlaceActorAt(clara, new Point(2, 1));
            ItemFood claraFood = NpcIntentSupport.Food(world, clara, 1);
            Check.Equal(false, world.Try(new ActionDropItem(clara, world.Game, claraFood)),
                "full fridge rejects a different claimant without losing food");
            Check.Equal(true, clara.Inventory.Contains(claraFood), "failed drop retains item");
            Check.Equal(2, cache.Contents.CountItems, "failed drop leaves both claims intact");

            string path = Path.Combine(Path.GetTempPath(), "storage-claims-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, world.Map);
                Map loaded = BinarySaveStore.Load<Map>(path);
                loaded.ReconstructAuxiliaryFields();
                StorageContainer saved = (StorageContainer)loaded.GetMapObjectAt(2, 1);
                Check.Equal(2, saved.Contents.CountItems, "separate stacks survive save");
                bool sawAnna = false, sawBoris = false;
                foreach (Item item in saved.Contents.Items)
                {
                    sawAnna |= item.StorageClaimantId == anna.PersonalityIdentity;
                    sawBoris |= item.StorageClaimantId == boris.PersonalityIdentity;
                }
                Check.Equal(true, sawAnna && sawBoris, "claimants survive save");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
            world.Map.PlaceActorAt(clara, new Point(4, 1));
            Check.Equal(true, world.Try(new ActionGetFromContainer(anna, world.Game, new Point(2, 1))),
                "another character can physically take the top stack");
            Check.Equal(boris.PersonalityIdentity, borisFood.StorageClaimantId,
                "taking does not erase Boris's claim");
            world.Map.PlaceActorAt(anna, new Point(2, 1));
            Check.Equal(true, world.Try(new ActionDropItem(anna, world.Game, borisFood)),
                "taken goods can be returned to storage");
            Check.Equal(boris.PersonalityIdentity, borisFood.StorageClaimantId,
                "returning does not transfer the claim to Anna");
        });
    }
}
