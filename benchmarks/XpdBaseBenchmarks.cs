using System;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

static class XpdBaseBenchmarks
{
    public static void Run()
    {
        foreach (int size in new[] { 40, 100 })
        {
            string[] rows = new string[size];
            for (int y = 0; y < size; y++) rows[y] = new string('.', size);
            ScenarioWorld world = TownScenarioFactory.Arena(7150 + size, rows);
            for (int y = 10; y <= 20; y++)
                for (int x = 10; x <= 20; x++)
                {
                    bool wall = x == 10 || y == 10 || x == 20 || y == 20;
                    if (wall) world.Map.SetTileModelAt(x, y, world.Game.GameTiles.WALL_BRICK);
                    else world.Map.GetTileAt(x, y).IsInside = true;
                }
            Actor claimant = SkillScenario.Actor(world);
            world.Place(claimant, 15, 15);
            string reason;
            if (XpdBasePlanner.Preview(world.Map, claimant, world.Game.Rules, out reason) == null)
                throw new InvalidOperationException("Base benchmark fixture: " + reason);
            PerformanceBenchmarks.Measure("base preview " + size + "x" + size, 100, () =>
                XpdBasePlanner.Preview(world.Map, claimant, world.Game.Rules, out reason));

            world.Map.DropItemAt(new ItemFood(world.Game.GameItems.GROCERIES),
                new Point(size - 3, size - 3));
            Location supply;
            Item item;
            PerformanceBenchmarks.Measure("sparse supply " + size + "x" + size, 100, () =>
                XpdSupplyRoutes.FindSupply(world.Map, world.Map.District, out supply, out item));
        }
    }
}
