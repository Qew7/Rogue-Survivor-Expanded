using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

static class StorageContainerDestroyScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("items/storage-destruction", () => TownScenarioFactory.Arena(4733,
            "......", "......", "......"), world =>
        {
            Point place = new Point(2, 1);
            StorageContainer fridge = new StorageContainer("fridge", "closed", "open", 240, 2);
            world.Map.PlaceMapObjectAt(fridge, place);
            ItemFood groundItem = new ItemFood(world.Game.GameItems.GROCERIES);
            ItemFood storedFood = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            ItemRangedWeapon storedGun = new ItemRangedWeapon(world.Game.GameItems.PISTOL);
            world.Map.DropItemAt(groundItem, place);
            world.Map.GetItemsAt(place).MaxCapacity = 1;
            fridge.Contents.AddAll(storedFood);
            fridge.Contents.AddAll(storedGun);
            Check.Call(world.Game, "DoDestroyObject", fridge);
            Inventory spilled = world.Map.GetItemsAt(place);
            Check.Equal(null, world.Map.GetMapObjectAt(place), "destroyed fridge disappears");
            Check.Equal(true, spilled.Contains(groundItem), "existing ground item survives");
            Check.Equal(true, spilled.Contains(storedFood), "stored food spills");
            Check.Equal(true, spilled.Contains(storedGun), "stored gun spills");
            Check.Equal(true, fridge.Contents.IsEmpty, "destroyed object no longer holds items");

            Point crowdedPlace = new Point(0, 1);
            StorageContainer crowdedFridge = new StorageContainer("fridge", "closed", "open", 240, 7);
            world.Map.PlaceMapObjectAt(crowdedFridge, crowdedPlace);
            for (int i = 0; i < 8; i++)
                world.Map.DropItemAt(new ItemRangedWeapon(world.Game.GameItems.PISTOL), crowdedPlace);
            for (int i = 0; i < 7; i++)
                crowdedFridge.Contents.AddAll(new ItemRangedWeapon(world.Game.GameItems.PISTOL));
            Check.Equal(8, world.Map.GetItemsAt(crowdedPlace).CountItems,
                "eight nonstacking items occupy eight ground slots");
            Check.Equal(7, crowdedFridge.Contents.CountItems,
                "seven nonstacking items fit in the fridge");
            Check.Call(world.Game, "DoDestroyObject", crowdedFridge);
            Check.Equal(15, world.Map.GetItemsAt(crowdedPlace).CountItems,
                "all seven stored stacks spill beside the eight ground stacks");
            Check.Equal(15, world.Map.GetItemsAt(crowdedPlace).MaxCapacity,
                "destruction expands ground capacity to preserve every stack");
            Check.Equal(true, crowdedFridge.Contents.IsEmpty, "crowded fridge releases its contents");

            Actor actor = SkillScenario.Actor(world);
            world.Place(actor, 3, 1);
            MapObject shelf = new MapObject("shelf", "shelf") { IsContainer = true };
            world.Map.PlaceMapObjectAt(shelf, new Point(4, 1));
            ItemFood shelfFood = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            world.Map.DropItemAt(shelfFood, new Point(4, 1));
            Check.Equal(true, world.Try(new ActionGetFromContainer(actor, world.Game, new Point(4, 1))),
                "legacy shelf still uses ground stack");
            Check.Equal(true, actor.Inventory.Contains(shelfFood), "shelf item is taken");
        });
    }
}
