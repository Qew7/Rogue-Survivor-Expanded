using System;
using System.Collections.Generic;
using System.Diagnostics;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.AI;
using djack.RogueSurvivor.Gameplay.AI.Sensors;

static partial class RestSimulationBenchmarks
{
    sealed class PhaseSample
    {
        sealed class Counter { public long Ticks; public int Calls; }

        readonly Dictionary<string, Counter> phases = new Dictionary<string, Counter>();
        int collectionsBefore;
        public double Total;
        public int Gen0Collections;

        public void Start()
        {
            collectionsBefore = GC.CollectionCount(0);
            RogueGame.ProfileSimulationPhase = Record;
            LOSSensor.ProfileFov = ticks => Record("fov", ticks);
            CivilianAI.ProfileIntentPrep = ticks => Record("intent", ticks);
            BaseAI.ProfileRouteCheck = ticks => Record("route", ticks);
            Map.ProfileZoneLookup = ticks => Record("zone", ticks);
        }

        public void Stop()
        {
            RogueGame.ProfileSimulationPhase = null;
            LOSSensor.ProfileFov = null;
            CivilianAI.ProfileIntentPrep = null;
            BaseAI.ProfileRouteCheck = null;
            Map.ProfileZoneLookup = null;
            Gen0Collections = GC.CollectionCount(0) - collectionsBefore;
        }

        void Record(string name, long ticks)
        {
            Counter counter;
            if (!phases.TryGetValue(name, out counter)) phases[name] = counter = new Counter();
            counter.Calls++;
            counter.Ticks += ticks;
        }

        public void Print()
        {
            string[] names = { "map turn", "choose", "sense", "fov", "decide", "intent", "knowledge",
                "knowledge expire", "knowledge percepts", "knowledge person", "knowledge items", "knowledge surroundings",
                "maintain", "decision content", "goal refresh", "goal evaluate", "goal context", "goal pending", "goal apply",
                "apply refresh", "apply stale", "apply no viable", "apply sort", "apply admit",
                "food care offer", "food repay offer", "route",
                "legal", "perform", "rumor share", "event report", "radio hour", "zone" };
            foreach (string name in names)
            {
                Counter counter;
                if (!phases.TryGetValue(name, out counter)) continue;
                double ms = counter.Ticks * 1000.0 / Stopwatch.Frequency;
                Console.WriteLine("  {0,-13} {1,7:F1} ms  {2,6} calls", name, ms, counter.Calls);
            }
            foreach (KeyValuePair<string, Counter> pair in phases)
                if (pair.Key.StartsWith("source ", StringComparison.Ordinal))
                    Console.WriteLine("  {0,-27} {1,7:F1} ms  {2,6} calls", pair.Key,
                        pair.Value.Ticks * 1000.0 / Stopwatch.Frequency, pair.Value.Calls);
            foreach (KeyValuePair<string, Counter> pair in phases)
                if (pair.Key.StartsWith("decision ", StringComparison.Ordinal))
                    Console.WriteLine("  {0,-27} {1,7:F1} ms  {2,6} calls", pair.Key,
                        pair.Value.Ticks * 1000.0 / Stopwatch.Frequency, pair.Value.Calls);
            long exclusive = 0;
            foreach (string name in new[] { "map turn", "choose", "legal", "perform" })
            {
                Counter counter;
                if (phases.TryGetValue(name, out counter)) exclusive += counter.Ticks;
            }
            Console.WriteLine("  other         {0,7:F1} ms (district scheduling, actor dispatch, and other work)",
                Total - exclusive * 1000.0 / Stopwatch.Frequency);
            Console.WriteLine("  Gen0 collections: {0}", Gen0Collections);
        }
    }

    public static void RunPhases()
    {
        const int turns = 30;
        const int samples = 3;
        RunOne(false, 5, 70, GameOptions.SimRatio.FULL, turns: turns);
        double[] plain = new double[samples];
        PhaseSample[] measured = new PhaseSample[samples];
        for (int i = 0; i < samples; i++)
        {
            plain[i] = RunOne(false, 5, 70, GameOptions.SimRatio.FULL, turns: turns);
            measured[i] = new PhaseSample();
            RunOne(false, 5, 70, GameOptions.SimRatio.FULL, measured[i], turns);
        }
        Array.Sort(plain);
        Array.Sort(measured, (a, b) => a.Total.CompareTo(b.Total));
        Console.WriteLine("REST PHASES 5x5 districts, {0} waits, full NPC and rumor simulation", turns);
        Console.WriteLine("Uninstrumented: {0:F1}, {1:F1}, {2:F1} ms; median {1:F1} ms",
            plain[0], plain[1], plain[2]);
        Console.WriteLine("Phase probes: {0:F1}, {1:F1}, {2:F1} ms; median {1:F1} ms",
            measured[0].Total, measured[1].Total, measured[2].Total);
        measured[1].Print();
        Console.WriteLine("Subphases overlap: FOV is in sense; sense and intent are in choose; rumors and events are in perform.");
    }
}
