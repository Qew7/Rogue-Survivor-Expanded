using System.Drawing;
using djack.RogueSurvivor.Data;

static class RouteFinderScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/route-finder", () => new ScenarioWorld(4583,
            ".....", ".###.", ".....", ".###.", "....."), world =>
        {
            Actor npc = world.Place("npc", 0, 0);
            RouteFinderProbe route = new RouteFinderProbe(world.Game, npc);
            Check.Equal(true, route.CanReach(new Point(4, 0)), "open row reachable");
            Check.Equal(false, route.CanReach(new Point(2, 1)),
                "wall tile is unreachable");
            Check.Equal(true, route.CanReach(new Point(1, 1), 1 << 4),
                "adjacent tile counts as goal when requested");
        });
    }
}
