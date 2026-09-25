using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class FieldOfViewCornerScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/field-of-view-corner", () => TownScenarioFactory.Arena(4593,
            ".......", "..#....", "..#....", "..#....", "......."), world =>
        {
            Actor observer = SkillScenario.Actor(world);
            world.Place(observer, 1, 2);
            HashSet<Point> first = LOS.ComputeFOVFor(world.Game.Rules, observer,
                world.Map.LocalTime, Weather.CLEAR);
            Check.Equal(true, first.Contains(new Point(2, 2)), "blocking wall visible");
            Check.Equal(false, first.Contains(new Point(3, 2)), "behind wall hidden");
            world.Map.PlaceActorAt(observer, new Point(4, 2));
            HashSet<Point> second = LOS.ComputeFOVFor(world.Game.Rules, observer,
                world.Map.LocalTime, Weather.CLEAR);
            Check.Equal(true, second.Contains(new Point(3, 2)), "new side visible");
            Check.Equal(false, second.Contains(new Point(1, 2)), "old side now hidden");
        });
    }
}
