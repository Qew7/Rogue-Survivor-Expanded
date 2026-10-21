using System;
using System.Drawing;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

static class StorageContainerMoveScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("items/storage-moves-with-object", () => TownScenarioFactory.Arena(4731,
            ".....", ".....", "....."), world =>
        {
            Actor actor = SkillScenario.Actor(world);
            world.Place(actor, 1, 1);
            world.SetPlayer(actor);
            StorageContainer drawer = new StorageContainer("drawer", "closed", "open", 40, 2);
            world.Map.PlaceMapObjectAt(drawer, new Point(2, 1));
            ItemFood food = new ItemFood(world.Game.GameItems.GROCERIES);
            drawer.Contents.AddAll(food);
            Guid id = drawer.Id;
            Check.Equal(false, world.Try(new ActionGetFromContainer(actor, world.Game, new Point(2, 1))),
                "closed contents cannot be taken");
            Check.Equal(true, world.Try(new ActionPush(actor, world.Game, drawer, Direction.E)),
                "drawer can be pushed");
            Check.Equal(new Point(3, 1), drawer.Location.Position, "drawer moves");
            Check.Equal(true, drawer.Contents.Contains(food), "food moves with drawer");
            Check.Equal(null, world.Map.GetItemsAt(new Point(2, 1)), "old tile has no food");
            Check.Equal(true, world.Try(new ActionUseStorageContainer(actor, world.Game, drawer, true)),
                "drawer opens");
            Check.Equal(true, drawer.IsWalkable, "open drawer is walkable");
            Check.Equal(true, world.Try(new ActionGetFromContainer(actor, world.Game, new Point(3, 1))),
                "food can be taken from opened drawer");
            Check.Equal(true, actor.Inventory.Contains(food), "actor receives food");
            world.Place(actor, 3, 1);
            Check.Equal(true, world.Try(new ActionDropItem(actor, world.Game, food)),
                "dropping inside drawer stores food");
            Check.Equal(true, drawer.Contents.Contains(food), "food returns to drawer");
            Check.Equal(null, world.Map.GetItemsAt(new Point(3, 1)), "stored food is not ground loot");
            string path = Path.Combine(Path.GetTempPath(), "storage-move-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, world.Map);
                Map loaded = BinarySaveStore.Load<Map>(path);
                loaded.ReconstructAuxiliaryFields();
                StorageContainer restored = (StorageContainer)loaded.GetMapObjectAt(3, 1);
                Check.Equal(id, restored.Id, "object identity survives save");
                Check.Equal(true, restored.IsOpen, "open state survives save");
                Check.Equal(1, restored.Contents.CountItems, "contents survive save");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
