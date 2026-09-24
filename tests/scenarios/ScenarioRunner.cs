using System;
using System.Collections.Generic;
using System.Reflection;

static class ScenarioRunner
{
    sealed class Scenario
    {
        public string Name;
        public Action<ScenarioWorld> Run;
        public Func<ScenarioWorld> Create;
    }

    static readonly List<Scenario> scenarios = new List<Scenario>();

    public static void RegisterAll()
    {
        Type[] types = Assembly.GetExecutingAssembly().GetTypes();
        Array.Sort(types, (a, b) => String.CompareOrdinal(a.FullName, b.FullName));
        foreach (Type type in types)
        {
            if (!type.Name.EndsWith("Scenario", StringComparison.Ordinal) || !type.IsAbstract || !type.IsSealed)
                continue;
            MethodInfo register = type.GetMethod("Register", BindingFlags.Public | BindingFlags.Static,
                null, Type.EmptyTypes, null);
            if (register != null) register.Invoke(null, null);
        }
    }

    public static void Add(string name, Func<ScenarioWorld> create, Action<ScenarioWorld> run)
    {
        if (String.IsNullOrWhiteSpace(name) || create == null || run == null)
            throw new ArgumentException("Scenario needs a name, world factory, and action");
        foreach (Scenario scenario in scenarios)
            if (scenario.Name == name) throw new ArgumentException("Duplicate scenario: " + name);
        scenarios.Add(new Scenario { Name = name, Create = create, Run = run });
    }

    public static void List()
    {
        foreach (Scenario scenario in scenarios) Console.WriteLine(scenario.Name);
    }

    public static bool Contains(string name)
    {
        foreach (Scenario scenario in scenarios)
            if (scenario.Name == name) return true;
        return false;
    }

    public static int Run(string name)
    {
        int count = 0;
        foreach (Scenario scenario in scenarios)
        {
            if (name != "--all" && name != scenario.Name) continue;
            count++;
            ScenarioWorld world = null;
            try
            {
                world = scenario.Create();
                scenario.Run(world);
                Console.WriteLine("PASS " + scenario.Name + " (seed " + world.Seed + ")");
            }
            catch (Exception error)
            {
                Console.Error.WriteLine("FAIL " + scenario.Name + " (seed " + (world == null ? "unknown" : world.Seed.ToString()) + ")");
                Console.Error.WriteLine(error);
                if (world != null) Console.Error.WriteLine("Map:\n" + world.Draw());
                return 1;
            }
        }
        if (count == 0)
        {
            Console.Error.WriteLine("Unknown scenario: " + name + ". Use --list to see names.");
            return 2;
        }
        Console.WriteLine(count + " scenario(s) passed");
        return 0;
    }
}
