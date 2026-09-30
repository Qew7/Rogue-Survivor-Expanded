using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Items;

static class MouseTrapRouteScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("movement/mouse-trap-route", () => TownScenarioFactory.Arena(4542,
            ".....", ".....", ".....", ".....", "....."), world =>
        {
            Actor player = SkillScenario.Actor(world);
            world.Place(player, 0, 2);
            world.SetPlayer(player);
            world.Game.ComputeViewRect(player.Location.Position);
            Check.Call(world.Game, "UpdatePlayerFOV", player);
            ItemTrap trap = new ItemTrap(world.Game.GameItems.BEAR_TRAP);
            trap.Activate(null);
            world.Map.DropItemAt(trap, new Point(2, 2));
            List<Point> route = (List<Point>)Check.Call(world.Game, "FindMouseMovePath",
                player, new Point(4, 2));
            Check.Equal(4, route.Count, "safe detour keeps shortest distance");
            Check.Equal(false, route.Contains(new Point(2, 2)), "active foreign trap avoided");
            trap.Desactivate();
            route = (List<Point>)Check.Call(world.Game, "FindMouseMovePath",
                player, new Point(4, 2));
            Check.Equal(true, route.Contains(new Point(2, 2)), "inactive trap does not distort route");
            for (int y = 1; y <= 3; y++)
            {
                ItemTrap barrier = new ItemTrap(world.Game.GameItems.BEAR_TRAP);
                barrier.Activate(null);
                world.Map.DropItemAt(barrier, new Point(1, y));
            }
            route = (List<Point>)Check.Call(world.Game, "FindMouseMovePath",
                player, new Point(4, 2));
            Check.Equal(true, route.Count > 4, "safe route is preferred even when longer");
            foreach (Point step in route)
                Check.Equal(false, step.X == 1 && step.Y >= 1 && step.Y <= 3,
                    "longer route avoids active traps");
        });
    }
}
