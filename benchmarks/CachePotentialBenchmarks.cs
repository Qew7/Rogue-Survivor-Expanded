using System;
using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.AI;
using djack.RogueSurvivor.Gameplay.AI.Sensors;

// Best-case bounds only: these probes deliberately hold the map and actor still.
static class CachePotentialBenchmarks
{
    public static void Run(ScenarioWorld world, Actor observer, LOSSensor sight,
        RouteFinderProbe route, Point[] destinations)
    {
        HashSet<Point> cachedFov = sight.FOV;
        List<Percept> fresh = sight.Sense(world.Game, observer);
        List<Percept> replayed = ReadCachedFov(world, observer, cachedFov);
        if (fresh.Count != replayed.Count)
            throw new InvalidOperationException("Cached FOV probe changed percept count");
        for (int i = 0; i < fresh.Count; i++)
            if (fresh[i].Percepted != replayed[i].Percepted)
                throw new InvalidOperationException("Cached FOV probe changed percept order");
        PerformanceBenchmarks.Measure("recompute FOV only", 1000,
            () => LOS.ComputeFOVFor(world.Game.Rules, observer,
                world.Map.LocalTime, world.Game.Session.World.Weather));
        PerformanceBenchmarks.Measure("scan unchanged cached FOV", 1000,
            () => ReadCachedFov(world, observer, cachedFov));

        Point target = destinations[0];
        PerformanceBenchmarks.Measure("four repeated route checks", 1000, () =>
        {
            for (int i = 0; i < 4; i++) route.CanReach(target);
        });
        Dictionary<Point, bool> answers = new Dictionary<Point, bool>();
        foreach (Point destination in destinations)
            answers[destination] = route.CanReach(destination);
        bool answer;
        PerformanceBenchmarks.Measure("four unchanged route lookups", 1000, () =>
        {
            foreach (Point destination in destinations)
                if (!answers.TryGetValue(destination, out answer))
                    throw new InvalidOperationException("Missing route answer");
        });
    }

    static List<Percept> ReadCachedFov(ScenarioWorld world, Actor observer,
        HashSet<Point> visible)
    {
        Map map = world.Map;
        int turn = map.LocalTime.TurnCounter;
        int range = world.Game.Rules.ActorFOV(observer, map.LocalTime,
            world.Game.Session.World.Weather);
        List<Percept> result = new List<Percept>();
        if (range * range < map.CountActors)
        {
            foreach (Point point in visible)
            {
                Actor other = map.GetActorAt(point);
                if (other != null && other != observer)
                    result.Add(new Percept(other, turn, other.Location));
            }
        }
        else
        {
            foreach (Actor other in map.Actors)
                if (other != observer &&
                    world.Game.Rules.LOSDistance(observer.Location.Position,
                        other.Location.Position) <= range &&
                    visible.Contains(other.Location.Position))
                    result.Add(new Percept(other, turn, other.Location));
        }
        foreach (Point point in visible)
        {
            Inventory items = map.GetItemsAt(point);
            if (items != null && !items.IsEmpty)
                result.Add(new Percept(items, turn, new Location(map, point)));
        }
        return result;
    }
}
