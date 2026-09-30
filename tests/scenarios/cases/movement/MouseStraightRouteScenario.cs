using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;

static class MouseStraightRouteScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("movement/mouse-straight-route", () => TownScenarioFactory.Arena(4541,
            ".....", ".....", "....."), world =>
        {
            Actor player = SkillScenario.Actor(world);
            world.SetPlayer(player);
            world.Game.ComputeViewRect(player.Location.Position);
            Check.Call(world.Game, "UpdatePlayerFOV", player);
            List<Point> route = (List<Point>)Check.Call(world.Game, "FindMouseMovePath",
                player, new Point(4, 1));
            Check.Equal(4, route.Count, "shortest four-step route");
            Check.Equal(new Point(1, 0), route[0], "diagonal is placed near the line, not at the start");
            Check.Equal(new Point(4, 1), route[3], "route reaches goal");
            Check.Equal(null, Check.Call(world.Game, "FindMouseMovePath", player, new Point(5, 1)),
                "outside map has no route");
        });
    }
}
