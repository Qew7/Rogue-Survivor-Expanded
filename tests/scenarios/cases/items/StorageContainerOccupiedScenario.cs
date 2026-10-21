using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

static class StorageContainerOccupiedScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("items/storage-occupied-close", () => TownScenarioFactory.Arena(4732,
            ".....", ".....", "....."), world =>
        {
            Actor actor = SkillScenario.Actor(world);
            world.Place(actor, 1, 1);
            StorageContainer fridge = new StorageContainer("fridge", "closed", "open", 240, 1);
            world.Map.PlaceMapObjectAt(fridge, new Point(2, 1));
            Check.Equal(true, world.Try(new ActionUseStorageContainer(actor, world.Game, fridge, true)),
                "fridge opens");
            world.Place(actor, 2, 1);
            Check.Equal(false, world.Try(new ActionUseStorageContainer(actor, world.Game, fridge, false)),
                "occupied fridge cannot close");
            Check.Equal(true, fridge.IsOpen, "failed close preserves state");
            ItemFood first = new ItemFood(world.Game.GameItems.GROCERIES);
            ItemFood second = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            actor.Inventory.AddAll(first);
            actor.Inventory.AddAll(second);
            Check.Equal(true, world.Try(new ActionDropItem(actor, world.Game, first)), "first item fits");
            Check.Equal(false, world.Try(new ActionDropItem(actor, world.Game, second)), "full fridge rejects item");
            Check.Equal(true, actor.Inventory.Contains(second), "failed drop keeps item");
            Check.Equal(true, fridge.Contents.Contains(first), "failed drop keeps existing item");
            world.Place(actor, 1, 1);
            Check.Equal(true, world.Try(new ActionUseStorageContainer(actor, world.Game, fridge, false)),
                "fridge closes from adjacent tile");
            Check.Equal(false, fridge.IsWalkable, "closed fridge blocks walking");
        });
    }
}
