using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.AI;
using djack.RogueSurvivor.Gameplay.AI;
using djack.RogueSurvivor.Gameplay.AI.Sensors;
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

    public static void RunMemory(int turns, bool collect)
    {
        Stopwatch interval = Stopwatch.StartNew();
        int previousGen2 = GC.CollectionCount(2);
        RunOne(false, 5, 70, GameOptions.SimRatio.FULL, turns: turns, botPlayer: true,
            afterTurn: (session, turn) =>
            {
                if (turn % 100 != 0 && turn != turns) return;
                double intervalMs = interval.Elapsed.TotalMilliseconds;
                int facts = 0, events = 0;
                int entries = 0;
                foreach (var record in session.ResidentRecords.Residents) entries += record.EntryCount;
                for (int x = 0; x < session.World.Size; x++)
                    for (int y = 0; y < session.World.Size; y++)
                        foreach (Map map in session.World[x, y].Maps)
                            foreach (Actor actor in map.Actors)
                                if (actor.Personality != null)
                                {
                                    facts += actor.Personality.Knowledge.Facts.Count;
                                    events += actor.Personality.Events.Count;
                                }
                double beforeCollect = GC.GetTotalMemory(false) / 1048576.0;
                double gcMs = 0;
                if (collect)
                {
                    Stopwatch gc = Stopwatch.StartNew();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    gcMs = gc.Elapsed.TotalMilliseconds;
                }
                Process process = Process.GetCurrentProcess();
                process.Refresh();
                Console.WriteLine("REST MEMORY wait {0}/{1}: {2:F0} ms/interval, managed {3:F1}->{4:F1} MiB, RSS {5:F1} MiB, gen2 +{6}, GC {7:F0} ms, facts {8}, events {9}, records {10}, entries {11}",
                    turn, turns, intervalMs, beforeCollect,
                    GC.GetTotalMemory(false) / 1048576.0, process.WorkingSet64 / 1048576.0,
                    GC.CollectionCount(2) - previousGen2, gcMs, facts, events,
                    session.ResidentRecords.Residents.Count, entries);
                if (turn == turns)
                {
                    var kinds = new System.Collections.Generic.Dictionary<string, int>();
                    var uniqueTexts = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
                    int duplicateTexts = 0;
                    foreach (var record in session.ResidentRecords.Residents)
                        foreach (var entry in record.Entries)
                        {
                            int count;
                            kinds.TryGetValue(entry.Kind, out count);
                            kinds[entry.Kind] = count + 1;
                            if (!uniqueTexts.Add(entry.Text)) duplicateTexts++;
                        }
                    var ranked = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>>(kinds);
                    ranked.Sort((a, b) => b.Value.CompareTo(a.Value));
                    Console.WriteLine("REST ARCHIVE duplicate texts {0}/{1}, unique {2}",
                        duplicateTexts, entries, uniqueTexts.Count);
                    for (int i = 0; i < Math.Min(12, ranked.Count); i++)
                        Console.WriteLine("  {0}: {1}", ranked[i].Key, ranked[i].Value);
                }
                interval.Restart();
                previousGen2 = GC.CollectionCount(2);
            });
    }

    public static void Profile()
    {
        double elapsed = RunOne(true);
        Console.WriteLine("PROFILE_REST {0} waits, {1} districts, unadjusted stopwatch {2:F1} ms",
            Turns, CitySize * CitySize, elapsed);
    }

    public static void ProfileOneDistrict()
    {
        double elapsed = RunOne(true, 1, 15, GameOptions.SimRatio.FULL);
        Console.WriteLine("PROFILE_REST_ONE {0} waits, 1 district, unadjusted stopwatch {1:F1} ms",
            Turns, elapsed);
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

    static double RunOne(bool profile, int citySize, int civiliansPerDistrict, GameOptions.SimRatio ratio,
        PhaseSample phases = null, int turns = Turns, int workers = 0, bool report = false,
        string snapshotPath = null, bool restoreSnapshot = false, Action<Session> afterRun = null,
        Action<Session, int> afterTurn = null, bool botPlayer = false, bool parallelRest = false)
    {
        ScenarioWorld fixture = TownScenarioFactory.Create(7971, false);
        GamePreset preset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
        preset.ParallelDistantSimulationDuringRest = parallelRest;
        Session.Get.GamePreset = preset;
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
        player.IsBotPlayer = botPlayer;
        if (botPlayer)
        {
            // The long-running fixture reaches refugee checks; regular world generation fills these slots.
            object uniques = Session.Get.UniqueActors;
            foreach (PropertyInfo slot in uniques.GetType().GetProperties())
            {
                object unique = Activator.CreateInstance(slot.PropertyType, true);
                slot.PropertyType.GetProperty("IsSpawned").SetValue(unique, true, null);
                slot.SetValue(uniques, unique, null);
            }
        }
        player.IsInvincible = true;
        for (int y = 0; y < map.Height && player.Location.Map == null; y++)
            for (int x = 0; x < map.Width && player.Location.Map == null; x++)
                if (map.IsWalkable(x, y) && map.GetActorAt(x, y) == null)
                    world.Place(player, x, y);
        world.SetPlayer(player);
        if (snapshotPath != null)
        {
            if (restoreSnapshot)
            {
                Session loaded = BinarySaveStore.LoadExact<Session>(snapshotPath);
                world = NpcIntentSupport.Restore(world, loaded);
                city = loaded.World;
                player = world.Game.Player;
                for (int x = 0; x < city.Size; x++) for (int y = 0; y < city.Size; y++)
                    foreach (Map restoredMap in city[x, y].Maps) restoredMap.ReconstructAuxiliaryFields();
                GamePreset restoredPreset = loaded.GamePreset;
                restoredPreset.ParallelDistantSimulationDuringRest = parallelRest;
                loaded.GamePreset = restoredPreset;
            }
            else BinarySaveStore.Save(snapshotPath, Session.Get);
        }
        GC.Collect();
        GC.WaitForPendingFinalizers();
        if (phases != null) phases.Start();
        double startAge = profile ? ProcessAgeSeconds() : 0;
        Stopwatch timer = Stopwatch.StartNew();
        if (parallelRest)
        {
            typeof(RogueGame).GetField("ParallelRestWorkers", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(world.Game, Math.Max(1, workers));
        }
        try
        {
            RunRest(world.Game, city[0, 0], player, turns, afterTurn);
        }
        finally { if (phases != null) phases.Stop(); }
        timer.Stop();
        if (phases != null) phases.Total = timer.Elapsed.TotalMilliseconds;
        if (profile) Console.WriteLine("PROFILE_WINDOW {0:F3} {1:F3}", startAge, ProcessAgeSeconds());
        for (int x = 0; x < citySize; x++) for (int y = 0; y < citySize; y++)
            if (city[x, y].EntryMap.LocalTime.TurnCounter != turns)
                throw new InvalidOperationException("Rest did not advance district " + x + "," + y);
        if (report) ReportParallelOutcome(city);
        if (afterRun != null) afterRun(Session.Get);
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
    static void RunRest(RogueGame game, District district, Actor player, int turns,
        Action<Session, int> afterTurn = null)
    {
        for (int turn = 0; turn < turns; turn++)
        {
            player.ActionPoints = Rules.BASE_ACTION_COST;
            game.DoWait(player);
            Advance.Invoke(game, new[] { (object)district, Active });
            if (afterTurn != null) afterTurn(Session.Get, turn + 1);
        }
    }
}
