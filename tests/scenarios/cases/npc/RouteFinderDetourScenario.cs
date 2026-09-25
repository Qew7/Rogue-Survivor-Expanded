using System.Drawing;
using djack.RogueSurvivor.Data;

static class RouteFinderDetourScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/route-finder-detour", () => new ScenarioWorld(7128,
            ".....", ".###.", ".###.", ".###.", "....."), world =>
        {
            Actor npc = world.Place("npc", 0, 2);
            RouteFinderProbe route = new RouteFinderProbe(world.Game, npc);
            Check.Equal(false, route.CanReach(new Point(4, 2)),
                "greedy AI movement cannot take a detour that starts sideways");
            Check.Equal(true, route.CanReach(new Point(0, 1)),
                "adjacent open tile remains reachable");
        });
    }
}
