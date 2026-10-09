using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.AI;
using djack.RogueSurvivor.Gameplay.Generators;

static partial class RestSimulationBenchmarks
{
    const int CitySize = 3;
    const int Turns = 4;
    static readonly Type Flags = typeof(RogueGame).GetNestedType("SimFlags", BindingFlags.NonPublic);
    static readonly MethodInfo Advance = typeof(RogueGame).GetMethod("AdvancePlay",
        BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(District), Flags }, null);
    static readonly object Active = Enum.Parse(Flags, "NOT_SIMULATING");
    static int initialLiving, initialUndead, connections;

    public static void Run()
    {
        RunOne(false); // Warm models and JIT outside the samples.
        double[] samples = new double[5];
        for (int i = 0; i < samples.Length; i++) samples[i] = RunOne(false);
        Array.Sort(samples);
        Console.WriteLine("REST SIMULATION {0}x{0} districts, {1} waits, {2} living, {3} undead, {4} border links",
            CitySize, Turns, initialLiving, initialUndead, connections);
        Console.WriteLine("Median {0:F1} ms ({1:F1} ms/wait)", samples[2], samples[2] / Turns);
        Console.WriteLine("Samples (ms): {0:F1}, {1:F1}, {2:F1}, {3:F1}, {4:F1}",
            samples[0], samples[1], samples[2], samples[3], samples[4]);
        ProbeFovReuse();
    }

    public static void RunLarge()
    {
        const int size = 5;
        const int civiliansPerDistrict = 70;
        RunOne(false, size, civiliansPerDistrict, GameOptions.SimRatio.FULL); // Warm models and JIT.
        double[] samples = new double[3];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = RunOne(false, size, civiliansPerDistrict, GameOptions.SimRatio.FULL);
        Array.Sort(samples);
        Console.WriteLine("REST LARGE {0}x{0} districts ({1} beyond the nearby radius), {2} waits, {3} living, {4} undead, {5} border links, FULL simulation",
            size, size * size - 4, Turns, initialLiving, initialUndead, connections);
        Console.WriteLine("Median {0:F1} ms ({1:F1} ms/wait); samples {2:F1}, {3:F1}, {4:F1} ms",
            samples[1], samples[1] / Turns, samples[0], samples[1], samples[2]);
    }

    public static void Profile()
    {
        double elapsed = RunOne(true);
        Console.WriteLine("PROFILE_REST {0} waits, {1} districts, unadjusted stopwatch {2:F1} ms",
            Turns, CitySize * CitySize, elapsed);
    }

    public static void ProfileLarge()
    {
        double elapsed = RunOne(true, 5, 70, GameOptions.SimRatio.FULL);
        Console.WriteLine("PROFILE_REST_LARGE {0} waits, 25 districts, unadjusted stopwatch {1:F1} ms",
            Turns, elapsed);
    }

    static double RunOne(bool profile)
    {
        return RunOne(profile, CitySize, 15, GameOptions.SimRatio.FULL);
    }

    static double RunOne(bool profile, int citySize, int civiliansPerDistrict, GameOptions.SimRatio ratio)
    {
        ScenarioWorld fixture = TownScenarioFactory.Create(7971, false);
        Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
        GameOptions options = RogueGame.Options;
        options.MaxCivilians = civiliansPerDistrict;
        options.MaxUndeads = 20;
        options.SimulateDistricts = ratio;
        options.SimThread = false;
        typeof(RogueGame).GetField("s_Options", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, options);
        World city = new World(citySize);
        for (int x = 0; x < citySize; x++) for (int y = 0; y < citySize; y++)
            city[x, y] = new District(new Point(x, y), DistrictKind.RESIDENTIAL);
        Session.Get.World = city;
        for (int x = 0; x < citySize; x++) for (int y = 0; y < citySize; y++)
        {
            BaseTownGenerator.Parameters parameters = BaseTownGenerator.DEFAULT_PARAMS;
            parameters.MapWidth = parameters.MapHeight = 40;
            parameters.District = city[x, y];
            parameters.GenerateHospital = false;
            parameters.GeneratePoliceStation = false;
            city[x, y].EntryMap = new StdTownGenerator(fixture.Game, parameters).Generate(7972 + x * citySize + y);
            Map sewers = new Map(8000 + x * citySize + y, "sewers", 40, 40);
            for (int row = 0; row < 40; row++) for (int col = 0; col < 40; col++)
                sewers.SetTileModelAt(col, row, fixture.Game.GameTiles.FLOOR_ASPHALT);
            city[x, y].SewersMap = sewers;
        }
        connections = ConnectCity(city);
        initialLiving = initialUndead = 0;
        for (int x = 0; x < citySize; x++) for (int y = 0; y < citySize; y++)
            foreach (Actor actor in city[x, y].EntryMap.Actors)
                if (actor.Model.Abilities.IsUndead) initialUndead++; else initialLiving++;
        Map map = city[0, 0].EntryMap;
        Session.Get.CurrentMap = map;
        ScenarioWorld world = new ScenarioWorld(7971, map, fixture.Game);
        Actor player = new Actor(world.Game.GameActors.MaleCivilian,
            world.Game.GameFactions.TheCivilians, "observer", false, false, 0);
        player.Controller = new PlayerController();
        player.IsInvincible = true;
        for (int y = 0; y < map.Height && player.Location.Map == null; y++)
            for (int x = 0; x < map.Width && player.Location.Map == null; x++)
                if (map.IsWalkable(x, y) && map.GetActorAt(x, y) == null)
                    world.Place(player, x, y);
        world.SetPlayer(player);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        double startAge = profile ? ProcessAgeSeconds() : 0;
        Stopwatch timer = Stopwatch.StartNew();
        RunRest(world.Game, city[0, 0], player);
        timer.Stop();
        if (profile) Console.WriteLine("PROFILE_WINDOW {0:F3} {1:F3}", startAge, ProcessAgeSeconds());
        for (int x = 0; x < citySize; x++) for (int y = 0; y < citySize; y++)
            if (city[x, y].EntryMap.LocalTime.TurnCounter != Turns)
                throw new InvalidOperationException("Rest did not advance district " + x + "," + y);
        return timer.Elapsed.TotalMilliseconds;
    }

    static int ConnectCity(World city)
    {
        int links = 0;
        for (int x = 0; x < city.Size; x++) for (int y = 0; y < city.Size; y++)
        {
            Map from = city[x, y].EntryMap;
            if (x + 1 < city.Size)
            {
                Map to = city[x + 1, y].EntryMap;
                for (int row = 0; row < from.Height; row++)
                    if (from.IsWalkable(from.Width - 1, row) && to.IsWalkable(0, row))
                    {
                        from.SetExitAt(new Point(from.Width, row), new Exit(to, new Point(0, row)) { IsAnAIExit = true });
                        to.SetExitAt(new Point(-1, row), new Exit(from, new Point(from.Width - 1, row)) { IsAnAIExit = true });
                        links++;
                        break;
                    }
            }
            if (y + 1 < city.Size)
            {
                Map to = city[x, y + 1].EntryMap;
                for (int col = 0; col < from.Width; col++)
                    if (from.IsWalkable(col, from.Height - 1) && to.IsWalkable(col, 0))
                    {
                        from.SetExitAt(new Point(col, from.Height), new Exit(to, new Point(col, 0)) { IsAnAIExit = true });
                        to.SetExitAt(new Point(col, -1), new Exit(from, new Point(col, from.Height - 1)) { IsAnAIExit = true });
                        links++;
                        break;
                    }
            }
        }
        return links;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void RunRest(RogueGame game, District district, Actor player)
    {
        for (int turn = 0; turn < Turns; turn++)
        {
            player.ActionPoints = Rules.BASE_ACTION_COST;
            game.DoWait(player);
            Advance.Invoke(game, new[] { (object)district, Active });
        }
    }
}
