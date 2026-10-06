using System;
using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.MapObjects;

static class FieldOfViewEquivalenceScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/field-of-view-equivalence", () => TownScenarioFactory.Arena(4594,
            "...........", "..##.......", "...#.......", "...........",
            ".......#...", ".......##..", "..........."), world =>
        {
            Actor observer = SkillScenario.Actor(world);
            world.Map.Lighting = Lighting.LIT;
            DoorWindow door = new DoorWindow("door", "closed", "open", "broken", 40);
            world.Map.PlaceMapObjectAt(door, new Point(5, 3));
            Point[] origins = { new Point(0, 0), new Point(4, 3),
                new Point(8, 4), new Point(10, 6) };
            foreach (Point origin in origins)
            {
                MoveTo(world, observer, origin);
                Compare(world, observer, "closed door at " + origin);
            }
            MoveTo(world, observer, new Point(4, 3));
            Check.Equal(true, world.Try(new ActionOpenDoor(observer, world.Game, door)),
                "door opens in the same turn");
            foreach (Point origin in origins)
            {
                MoveTo(world, observer, origin);
                Compare(world, observer, "open door at " + origin);
            }
            MoveTo(world, observer, new Point(4, 3));
            Check.Equal(true, world.Try(new ActionCloseDoor(observer, world.Game, door)),
                "door closes in the same turn");
            Compare(world, observer, "closed again");
            observer.IsSleeping = true;
            Compare(world, observer, "zero-range sleeping observer");
            observer.IsSleeping = false;
            world.Map.Lighting = Lighting.DARKNESS;
            Compare(world, observer, "darkness changes the cached ray radius");
            world.Map.Lighting = Lighting.LIT;
            Compare(world, observer, "restored lighting restores the old radius");
        });
    }

    static void MoveTo(ScenarioWorld world, Actor observer, Point origin)
    {
        if (observer.Location.Position != origin) world.Map.PlaceActorAt(observer, origin);
    }

    static void Compare(ScenarioWorld world, Actor observer, string caseName)
    {
        HashSet<Point> actual = LOS.ComputeFOVFor(world.Game.Rules, observer,
            world.Map.LocalTime, Weather.CLEAR);
        HashSet<Point> expected = OriginalFov(world, observer);
        Check.Equal(true, actual.SetEquals(expected), caseName + " retains every visible tile");
    }

    // Reference implementation of the original ray casting and wall fix.
    static HashSet<Point> OriginalFov(ScenarioWorld world, Actor observer)
    {
        Map map = world.Map;
        Point from = observer.Location.Position;
        int range = world.Game.Rules.ActorFOV(observer, map.LocalTime, Weather.CLEAR);
        int xmin = from.X - range, xmax = from.X + range;
        int ymin = from.Y - range, ymax = from.Y + range;
        map.TrimToBounds(ref xmin, ref ymin);
        map.TrimToBounds(ref xmax, ref ymax);
        HashSet<Point> visible = new HashSet<Point>();
        List<Point> walls = new List<Point>();
        Point goal = Point.Empty;
        Func<int, int, bool> trace = (x, y) =>
        {
            bool through = (x == goal.X && y == goal.Y) || map.IsTransparent(x, y);
            if (through) visible.Add(new Point(x, y));
            return through;
        };
        for (int x = xmin; x <= xmax; x++)
            for (int y = ymin; y <= ymax; y++)
            {
                Point to = new Point(x, y);
                int dx = x - from.X, dy = y - from.Y;
                if (0.75f * (dx * dx + dy * dy) > range * range || visible.Contains(to))
                    continue;
                goal = to;
                if (LOS.AsymetricBresenhamTrace(range, map, from.X, from.Y,
                    x, y, null, trace))
                {
                    visible.Add(to);
                    continue;
                }
                Tile tile = map.GetTileAt(x, y);
                if ((!tile.Model.IsTransparent && !tile.Model.IsWalkable) ||
                    map.GetMapObjectAt(x, y) != null)
                    walls.Add(to);
            }
        List<Point> fixedWalls = new List<Point>();
        foreach (Point wall in walls)
        {
            int count = 0;
            foreach (Direction direction in Direction.COMPASS)
            {
                Point next = wall + direction;
                if (!visible.Contains(next)) continue;
                Tile tile = map.GetTileAt(next.X, next.Y);
                if (tile.Model.IsTransparent && tile.Model.IsWalkable) count++;
            }
            if (count >= 3) fixedWalls.Add(wall);
        }
        foreach (Point wall in fixedWalls) visible.Add(wall);
        return visible;
    }
}
