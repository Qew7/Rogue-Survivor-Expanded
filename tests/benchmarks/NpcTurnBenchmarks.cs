using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.AI;
using djack.RogueSurvivor.Gameplay.AI;
using djack.RogueSurvivor.Gameplay.AI.Sensors;
using djack.RogueSurvivor.Gameplay.Generators;

// Times actual NPC decisions and performed actions, not repeated choices on a frozen map.
static partial class NpcTurnBenchmarks
{
    const int Turns = 8;
    const int Samples = 5;
    static readonly FieldInfo OptionsField = typeof(RogueGame).GetField("s_Options",
        BindingFlags.Static | BindingFlags.NonPublic);
    static readonly Type SimFlags = typeof(RogueGame).GetNestedType("SimFlags", BindingFlags.NonPublic);
    static readonly MethodInfo NextMapTurn = typeof(RogueGame).GetMethod("NextMapTurn",
        BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(Map), SimFlags }, null);
    static readonly object FullTurn = Enum.Parse(SimFlags, "NOT_SIMULATING");
    static Role currentRole;

    sealed class Role
    {
        public int Actions, FovCalls, RouteCalls, IntentCalls;
        public long Sense, Fov, Decide, Route, Intent, Choose, Legal, Perform;
        public readonly Dictionary<string, ActionCost> ByAction = new Dictionary<string, ActionCost>();

        public void AddAction(ActorAction action, long ticks)
        {
            string name = action == null ? "null" : action.GetType().Name;
            ActionCost cost;
            if (!ByAction.TryGetValue(name, out cost)) ByAction[name] = cost = new ActionCost();
            cost.Count++;
            cost.Ticks += ticks;
        }
    }

    sealed class ActionCost
    {
        public int Count;
        public long Ticks;
    }

    sealed class Sample
    {
        public long Total, MapTurn, Dispatch;
        public int TurnsDone, InitialCivilians, InitialZombies;
        public readonly Role Civilians = new Role();
        public readonly Role Zombies = new Role();
    }

    public static void Run()
    {
        LOSSensor.ProfileFov = RecordFov;
        BaseAI.ProfileRouteCheck = RecordRoute;
        CivilianAI.ProfileIntentPrep = RecordIntent;
        try
        {
            RunOne(); // JIT and model warmup, excluded from the samples.
            Sample[] samples = new Sample[Samples];
            for (int i = 0; i < Samples; i++) samples[i] = RunOne();
            Array.Sort(samples, (a, b) => a.Total.CompareTo(b.Total));
            Sample median = samples[Samples / 2];
            Console.WriteLine("NPC TURN 100x100, seed 7360, {0} civilians + {1} zombies, {2} turns; median of {3} fresh runs",
                median.InitialCivilians, median.InitialZombies, Turns, Samples);
            Console.WriteLine("Full runs (ms): {0:F1}, {1:F1}, {2:F1}, {3:F1}, {4:F1}",
                Ms(samples[0].Total), Ms(samples[1].Total), Ms(samples[2].Total),
                Ms(samples[3].Total), Ms(samples[4].Total));
            Console.WriteLine("Median run: {0:F1} ms total, {1:F1} ms/map turn, {2} civilian and {3} zombie actions",
                Ms(median.Total), Ms(median.Total) / median.TurnsDone,
                median.Civilians.Actions, median.Zombies.Actions);
            Print("map upkeep", median.MapTurn, median.Total, median.TurnsDone);
            Print("actor dispatch", median.Dispatch, median.Total, median.Civilians.Actions + median.Zombies.Actions);
            PrintRole("civilian", median.Civilians, median.Total);
            PrintRole("zombie", median.Zombies, median.Total);
            Console.WriteLine("Subphases are inclusive and can overlap: FOV is inside sense; route and intent preparation are inside decide.");
            Console.WriteLine("Fixture setup, rendering and other districts are excluded.");
        }
        finally
        {
            currentRole = null;
            LOSSensor.ProfileFov = null;
            BaseAI.ProfileRouteCheck = null;
            CivilianAI.ProfileIntentPrep = null;
        }
    }

    static void RecordFov(long ticks) { currentRole.Fov += ticks; currentRole.FovCalls++; }
    static void RecordRoute(long ticks) { currentRole.Route += ticks; currentRole.RouteCalls++; }
    static void RecordIntent(long ticks) { currentRole.Intent += ticks; currentRole.IntentCalls++; }

    static double Ms(long ticks) { return ticks * 1000.0 / Stopwatch.Frequency; }

    static void Print(string name, long ticks, long total, int count)
    {
        Console.WriteLine("  {0,-24} {1,8:F1} ms  {2,5:F1}%  {3,8:F1} us/call ({4} calls)",
            name, Ms(ticks), 100.0 * ticks / total, Ms(ticks) * 1000 / count, count);
    }

    static void PrintRole(string name, Role role, long total)
    {
        Console.WriteLine("{0} ({1} actions)", name, role.Actions);
        Print("sense / FOV", role.Sense, total, role.Actions);
        Print("  FOV computation", role.Fov, total, role.FovCalls);
        Print("decide / plan / route", role.Decide, total, role.Actions);
        if (role.IntentCalls > 0) Print("  intent preparation", role.Intent, total, role.IntentCalls);
        if (role.RouteCalls > 0) Print("  reachability checks", role.Route, total, role.RouteCalls);
        Print("choose total", role.Choose, total, role.Actions);
        Print("action legal", role.Legal, total, role.Actions);
        Print("action perform", role.Perform, total, role.Actions);
        var actions = new List<KeyValuePair<string, ActionCost>>(role.ByAction);
        actions.Sort((a, b) => b.Value.Ticks.CompareTo(a.Value.Ticks));
        for (int i = 0; i < Math.Min(5, actions.Count); i++)
            Console.WriteLine("    decide -> {0,-22} {1,8:F1} ms  {2,5} calls",
                actions[i].Key, Ms(actions[i].Value.Ticks), actions[i].Value.Count);
    }
}
