using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.AI;
using djack.RogueSurvivor.Gameplay.AI.Sensors;
using djack.RogueSurvivor.Gameplay.Generators;

static class AIAndGenerationBenchmarks
{
    public static void Run()
    {
        string[] rows = new string[40];
        for (int i = 0; i < rows.Length; i++) rows[i] = new string('.', 40);
        ScenarioWorld world = TownScenarioFactory.Arena(7116, rows);
        Actor observer = SkillScenario.Actor(world);
        world.Place(observer, 20, 20);
        for (int i = 0; i < 30; i++)
        {
            int x = 17 + i % 6;
            int y = 17 + i / 6;
            if (x == 20 && y == 20) x = 23;
            world.Place(SkillScenario.Actor(world), x, y);
        }
        int fasterActors = 0;
        foreach (Actor actor in world.Map.Actors)
            if (world.Game.Rules.ActorSpeed(actor) > Rules.BASE_ACTION_COST)
                fasterActors++;
        Console.WriteLine("PROFILE actors faster than one action/turn: {0}/{1}",
            fasterActors, world.Map.CountActors);
        LOSSensor sight = new LOSSensor(LOSSensor.SensingFilter.ACTORS |
            LOSSensor.SensingFilter.ITEMS);
        PerformanceBenchmarks.Measure("NPC sight, 30 actors, 40x40", 1000,
            () => sight.Sense(world.Game, observer));
        LOSSensor actorsOnly = new LOSSensor(LOSSensor.SensingFilter.ACTORS);
        PerformanceBenchmarks.Measure("NPC sight, actors only", 1000,
            () => actorsOnly.Sense(world.Game, observer));
        LOSSensor itemsOnly = new LOSSensor(LOSSensor.SensingFilter.ITEMS);
        PerformanceBenchmarks.Measure("NPC sight, items only", 1000,
            () => itemsOnly.Sense(world.Game, observer));

        observer.Controller = new CivilianAI();
        PerformanceBenchmarks.Measure("civilian action, 30 actors", 1000,
            () => observer.Controller.GetAction(world.Game));
        ChoiceProbeAI classifier = new ChoiceProbeAI();
        observer.Controller = classifier;
        List<djack.RogueSurvivor.Engine.AI.Percept> seen = sight.Sense(world.Game, observer);
        if (seen.Count == 0) throw new InvalidOperationException("Sight benchmark has no percepts");
        PerformanceBenchmarks.Measure("classify NPC percepts, 30 actors", 1000,
            () => classifier.Classify(world.Game, seen));

        RouteFinderProbe route = new RouteFinderProbe(world.Game, observer);
        Point[] destinations = { new Point(35, 35), new Point(36, 35),
            new Point(37, 35), new Point(38, 35) };
        PerformanceBenchmarks.Measure("NPC four reachability checks", 1000, () =>
        {
            foreach (Point destination in destinations) route.CanReach(destination);
        });
        CachePotentialBenchmarks.Run(world, observer, sight, route, destinations);

        BaseTownGenerator.Parameters parameters = BaseTownGenerator.DEFAULT_PARAMS;
        parameters.MapWidth = 40;
        parameters.MapHeight = 40;
        parameters.District = new District(new Point(0, 0), DistrictKind.RESIDENTIAL);
        parameters.GenerateHospital = false;
        parameters.GeneratePoliceStation = false;
        BaseTownGenerator generator = new BaseTownGenerator(world.Game, parameters);
        MeasureFresh("generate residential surface 40x40", 5,
            () => generator.Generate(7117));

        FieldInfo optionsField = typeof(RogueGame).GetField("s_Options",
            BindingFlags.Static | BindingFlags.NonPublic);
        GameOptions previous = RogueGame.Options;
        GameOptions populated = previous;
        populated.MaxCivilians = 30;
        populated.MaxUndeads = 0;
        try
        {
            optionsField.SetValue(null, populated);
            StdTownGenerator populatedGenerator = new StdTownGenerator(world.Game, parameters);
            MeasureFresh("generate populated surface 40x40", 5,
                () => populatedGenerator.Generate(7117));
        }
        finally { optionsField.SetValue(null, previous); }
        MeasureActorPlacement(world, rows, generator);
    }

    static void MeasureActorPlacement(ScenarioWorld world, string[] rows,
        BaseTownGenerator generator)
    {
        double[] samples = new double[5];
        for (int run = -1; run < samples.Length; run++)
        {
            ScenarioWorld dense = new ScenarioWorld(7124, rows);
            for (int y = 0; y < 40; y++)
                for (int x = 0; x < 40; x++)
                    if ((x + y) % 16 != 0) dense.Place("blocker", x, y);
            Actor candidate = SkillScenario.Actor(world);
            GC.Collect();
            Stopwatch watch = Stopwatch.StartNew();
            bool placed = generator.ActorPlace(new DiceRoller(7124), 16000,
                dense.Map, candidate);
            watch.Stop();
            if (!placed) throw new InvalidOperationException("dense placement failed");
            if (run >= 0) samples[run] = watch.Elapsed.TotalMilliseconds;
        }
        Array.Sort(samples);
        Console.WriteLine("BENCH place actor on 94% occupied map: {0:F2} ms / call (median of 5)",
            samples[2]);
    }

    static void MeasureFresh(string name, int count, Action action)
    {
        action(); // JIT and data warmup, outside timed samples.
        double[] samples = new double[count];
        for (int i = 0; i < count; i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Stopwatch watch = Stopwatch.StartNew();
            action();
            watch.Stop();
            samples[i] = watch.Elapsed.TotalMilliseconds;
        }
        Array.Sort(samples);
        Console.WriteLine("BENCH {0}: {1:F2} ms / call (median of {2})",
            name, samples[count / 2], count);
    }
}
