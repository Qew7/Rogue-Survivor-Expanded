using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

static class PerformanceBenchmarks
{
    public static void Run()
    {
        string[] rows = new string[40];
        for (int i = 0; i < rows.Length; i++) rows[i] = new string('.', 40);
        ScenarioWorld world = TownScenarioFactory.Arena(4536, rows);
        Actor player = SkillScenario.Actor(world);
        world.Place(player, 20, 20);
        world.SetPlayer(player);
        world.Game.ComputeViewRect(player.Location.Position);
        for (int x = 0; x < 40; x++)
            for (int y = 0; y < 40; y++)
                world.Map.GetTileAt(x, y).IsVisited = true;

        GameOptions before = RogueGame.Options;
        GameOptions options = before;
        options.IsMinimapOn = true;
        typeof(RogueGame).GetField("s_Options", System.Reflection.BindingFlags.Static |
            System.Reflection.BindingFlags.NonPublic).SetValue(null, options);
        try
        {
            Measure("minimap 40x40", 300, () => world.Game.DrawMiniMap(world.Map));
        }
        finally
        {
            typeof(RogueGame).GetField("s_Options", System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic).SetValue(null, before);
        }

        Rectangle bounds = new Rectangle(0, 0, 25, 25);
        Measure("mouse route 25x25", 1000, () => MouseMovePath.Find(
            new Point(0, 0), new Point(24, 24), bounds, point => true));

        ChoiceProbeAI ai = new ChoiceProbeAI();
        List<int> choices = new List<int> { 1, 3, 5, 7, 9, 2, 4, 6, 8, 10 };
        Func<int, bool> valid = value => true;
        Func<int, float> score = value => value;
        Measure("AI choose 10 candidates", 100000,
            () => ai.Pick(world.Game, choices, valid, score));

        for (int i = 0; i < 300; i++)
            world.Map.AddZone(new Zone("benchmark-" + i,
                new Rectangle(i % 40, i / 40, 1, 1)));
        Measure("zone lookup 300 zones", 100000,
            () => world.Map.GetZonesAt(20, 20));
        Measure("zone name 300 zones", 100000,
            () => world.Map.HasZonePartiallyNamedAt(new Point(20, 20), "benchmark"));
        Measure("absent zone name 300 zones", 100000,
            () => world.Map.HasZonePartiallyNamedAt(new Point(20, 20), "patient room"));

        Measure("scent insert 1600 tiles", 1, () =>
        {
            for (int x = 0; x < 40; x++)
                for (int y = 0; y < 40; y++)
                    world.Map.ModifyScentAt(Odor.LIVING, 1, new Point(x, y));
            foreach (OdorScent scent in new List<OdorScent>(world.Map.Scents))
                world.Map.RemoveScent(scent);
        });

        Actor routeActor = SkillScenario.Actor(world);
        world.Place(routeActor, 1, 1);
        RouteFinderProbe route = new RouteFinderProbe(world.Game, routeActor);
        Measure("NPC route 40x40", 1000,
            () => route.CanReach(new Point(38, 38)));

        for (int x = 0; x < 40; x++)
            for (int y = 0; y < 20; y++)
                world.Map.DropItemAt(new ItemFood(world.Game.GameItems.GROCERIES),
                    new Point(x, y));
        Type flags = typeof(RogueGame).GetNestedType("SimFlags", BindingFlags.NonPublic);
        object fullTurn = Enum.Parse(flags, "NOT_SIMULATING");
        MethodInfo nextTurn = typeof(RogueGame).GetMethod("NextMapTurn",
            BindingFlags.Instance | BindingFlags.NonPublic,
            null, new[] { typeof(Map), flags }, null);
        Point blastPoint = new Point(35, 35);
        Measure("one blast, 800 ground stacks", 20, () =>
        {
            ItemGrenadePrimed grenade = new ItemGrenadePrimed(world.Game.GameItems.GRENADE_PRIMED);
            grenade.FuseTimeLeft = 1;
            world.Map.DropItemAt(grenade, blastPoint);
            nextTurn.Invoke(world.Game, new[] { (object)world.Map, fullTurn });
        });
        Measure("presave 40x40 map", 100, () => world.Map.OptimizeBeforeSaving());
        GraphicsAndWorldBenchmarks.Run(world, player);
        AIAndGenerationBenchmarks.Run();
    }

    public static void Measure(string name, int iterations, Action action)
    {
        action(); // JIT warmup
        double[] samples = new double[5];
        for (int run = 0; run < samples.Length; run++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            Stopwatch watch = Stopwatch.StartNew();
            for (int i = 0; i < iterations; i++) action();
            watch.Stop();
            samples[run] = watch.Elapsed.TotalMilliseconds;
        }
        Array.Sort(samples);
        Console.WriteLine("BENCH {0}: {1:F2} ms / {2} calls (median of 5)",
            name, samples[2], iterations);
    }
}
