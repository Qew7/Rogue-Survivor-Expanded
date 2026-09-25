using System.Drawing;
using djack.RogueSurvivor.Data;

static class RouteFinderReuseScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/route-finder-reuse", () => new ScenarioWorld(4591,
            ".....", ".....", "....."), world =>
        {
            Actor npc = world.Place("npc", 0, 1);
            RouteFinderProbe route = new RouteFinderProbe(world.Game, npc);
            Check.Equal(true, route.CanReach(new Point(4, 1)), "initial route");
            world.Map.PlaceActorAt(npc, new Point(3, 1));
            Check.Equal(true, route.CanReach(new Point(0, 1)), "route after moving actor");
            Check.Equal(true, route.CanReach(new Point(4, 1)), "second target after moving");
            world.SetTile(4, 1, '#');
            Check.Equal(false, route.CanReach(new Point(4, 1)), "blocked destination");
            world.SetTile(4, 1, '.');
            Check.Equal(true, route.CanReach(new Point(4, 1)), "route after reopening tile");
        });
    }
}
