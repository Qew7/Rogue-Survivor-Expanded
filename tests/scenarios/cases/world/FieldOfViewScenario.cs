using System.Drawing;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class FieldOfViewScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/field-of-view", () => TownScenarioFactory.Arena(4587,
            ".......", ".......", "...#...", ".......", "......."), world =>
        {
            Actor observer = SkillScenario.Actor(world);
            world.Place(observer, 1, 2);
            HashSet<Point> visible = LOS.ComputeFOVFor(world.Game.Rules, observer,
                world.Map.LocalTime, Weather.CLEAR);
            Check.Equal(true, visible.Contains(new Point(2, 2)), "near clear tile visible");
            Check.Equal(false, visible.Contains(new Point(5, 2)),
                "tile behind wall is hidden");
        });
    }
}
