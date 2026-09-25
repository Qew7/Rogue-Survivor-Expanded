using System.Drawing;
using djack.RogueSurvivor.Data;

static class ExitRegionScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/exit-region", () => TownScenarioFactory.Arena(4586,
            "....", "....", "....", "...."), world =>
        {
            world.Map.SetExitAt(new Point(2, 2), new Exit(world.Map, new Point(0, 0)));
            world.Map.SetExitAt(new Point(0, 0), new Exit(world.Map, new Point(2, 2)));
            Check.Equal(true, world.Map.HasAnExitIn(new Rectangle(1, 1, 2, 2)),
                "region containing exit matches");
            Check.Equal(false, world.Map.HasAnExitIn(new Rectangle(1, 1, 1, 1)),
                "right and bottom edges are exclusive");
            Check.Equal(false, world.Map.HasAnExitIn(new Rectangle(3, 3, 1, 1)),
                "empty region has no exit");
            Check.Equal(true, world.Map.HasAnExitIn(new Rectangle(0, 0, 1, 1)),
                "small region finds one of several exits");
        });
    }
}
