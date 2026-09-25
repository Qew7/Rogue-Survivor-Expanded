using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;

static class MouseRouteScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("movement/mouse-route", () => TownScenarioFactory.Arena(4534,
            ".....", ".###.", ".....", ".....", "....."), world =>
        {
            Actor player = SkillScenario.Actor(world);
            world.Place(player, 0, 2);
            world.SetPlayer(player);
            world.Game.ComputeViewRect(player.Location.Position);
            Check.Call(world.Game, "UpdatePlayerFOV", player);
            Point goal = new Point(4, 2);
            List<Point> route = (List<Point>)Check.Call(world.Game, "FindMouseMovePath",
                player, goal);
            Check.Equal(true, route != null && route.Count > 0, "visible floor has a route");
            Check.Equal(goal, route[route.Count - 1], "route ends at target");
            foreach (Point step in route)
                Check.Equal(true, world.Map.IsWalkable(step), "route avoids walls");
            world.SetTile(4, 2, '#');
            Check.Equal(null, Check.Call(world.Game, "FindMouseMovePath", player, goal),
                "blocked destination has no route");
        });
    }
}
