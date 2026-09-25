using System.Drawing;
using System.IO;
using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

static class GroundInventoryPositionScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/ground-inventory-position", () =>
            TownScenarioFactory.Arena(4585, "...", "...", "..."), world =>
        {
            Point tile = new Point(1, 1);
            ItemFood food = new ItemFood(world.Game.GameItems.GROCERIES);
            world.Map.DropItemAt(food, tile);
            Inventory stack = world.Map.GetItemsAt(tile);
            Check.Equal((Point?)tile, world.Map.GetGroundInventoryPosition(stack),
                "ground stack position resolves");
            world.Map.RemoveItemAt(food, tile);
            Check.Equal(null, world.Map.GetGroundInventoryPosition(stack),
                "removed stack no longer resolves");
            ItemFood next = new ItemFood(world.Game.GameItems.GROCERIES);
            world.Map.DropItemAt(next, tile);
            Check.Equal((Point?)tile, world.Map.GetGroundInventoryPosition(world.Map.GetItemsAt(tile)),
                "new stack at same tile resolves");
            string path = Path.Combine(Path.GetTempPath(), "ground-position-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, world.Map);
                Map loaded = (Map)BinarySaveStore.Load(path, null);
                loaded.ReconstructAuxiliaryFields();
                Check.Equal((Point?)tile,
                    loaded.GetGroundInventoryPosition(loaded.GetItemsAt(tile)),
                    "reverse index rebuilds after load");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
