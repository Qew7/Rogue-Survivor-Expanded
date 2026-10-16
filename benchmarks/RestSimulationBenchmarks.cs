using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
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
    static readonly MethodInfo StartBackground = typeof(RogueGame).GetMethod("StartSimThread",
        BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly MethodInfo StopBackground = typeof(RogueGame).GetMethod("StopSimThread",
        BindingFlags.Instance | BindingFlags.NonPublic);
    static int initialLiving, initialUndead, connections;
    static double LastForegroundMs;

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
    }

    static double ProcessAgeSeconds()
    {
        return (DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds;
    }

    public static void RunLarge()
    {
        const int size = 5;
        const int civiliansPerDistrict = 70;
        RunOne(false, size, civiliansPerDistrict, GameOptions.SimRatio.FULL); // Warm models and JIT.
        double[] samples = new double[3];
        double[] foreground = new double[3];
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = RunOne(false, size, civiliansPerDistrict, GameOptions.SimRatio.FULL);
            foreground[i] = LastForegroundMs;
        }
        Array.Sort(samples);
        Array.Sort(foreground);
        Console.WriteLine("REST LARGE {0}x{0} districts ({1} beyond the nearby radius), {2} waits, {3} living, {4} undead, {5} border links, FULL simulation",
            size, size * size - 4, Turns, initialLiving, initialUndead, connections);
        Console.WriteLine("Median {0:F1} ms ({1:F1} ms/wait); samples {2:F1}, {3:F1}, {4:F1} ms",
            samples[1], samples[1] / Turns, samples[0], samples[1], samples[2]);
        Console.WriteLine("Foreground median {0:F1} ms ({1:F1} ms/wait)", foreground[1], foreground[1] / Turns);
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
                    var refusedTradeIds = new System.Collections.Generic.HashSet<long>();
                    var refusedTradePairs = new System.Collections.Generic.HashSet<string>();
                    var refusedTradeInitiators = new System.Collections.Generic.HashSet<Guid>();
                    var eventIdsByKind = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.HashSet<long>>();
                    var actorEventsByKind = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<Guid, System.Collections.Generic.HashSet<long>>>();
                    var chatTimes = new System.Collections.Generic.HashSet<string>();
                    var tradedTimes = new System.Collections.Generic.HashSet<string>();
                    var pairCounts = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.Dictionary<string, int>>();
                    var rumorAudience = new System.Collections.Generic.Dictionary<long, int>();
                    int rumorDirect = 0, repeatedRumorCause = 0, repeatedRumorText = 0, rumorWithoutCause = 0;
                    int rumorListeners = 0, rumorMaxPerListener = 0;
                    int duplicateTexts = 0;
                    foreach (var record in session.ResidentRecords.Residents)
                    {
                        var heardCauses = new System.Collections.Generic.HashSet<long>();
                        var heardTexts = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
                        int listenerRumors = 0;
                        foreach (var entry in record.Entries)
                        {
                            int count;
                            kinds.TryGetValue(entry.Kind, out count);
                            kinds[entry.Kind] = count + 1;
                            if (entry.EventId != 0)
                            {
                                System.Collections.Generic.HashSet<long> ids;
                                if (!eventIdsByKind.TryGetValue(entry.Kind, out ids))
                                    eventIdsByKind[entry.Kind] = ids = new System.Collections.Generic.HashSet<long>();
                                bool first = ids.Add(entry.EventId);
                                if (first && (entry.Kind == "chat" || entry.Kind == "traded" || entry.Kind == "trade_refused") &&
                                    entry.SubjectId != Guid.Empty && entry.OtherId != Guid.Empty)
                                {
                                    Guid a = entry.SubjectId, b = entry.OtherId;
                                    if (a.CompareTo(b) > 0) { a = entry.OtherId; b = entry.SubjectId; }
                                    string pair = a.ToString("N") + ":" + b.ToString("N");
                                    string time = entry.Turn + ":" + pair;
                                    if (entry.Kind == "chat") chatTimes.Add(time);
                                    if (entry.Kind == "traded") tradedTimes.Add(time);
                                    System.Collections.Generic.Dictionary<string, int> counts;
                                    if (!pairCounts.TryGetValue(entry.Kind, out counts))
                                        pairCounts[entry.Kind] = counts = new System.Collections.Generic.Dictionary<string, int>();
                                    int repeats;
                                    counts.TryGetValue(pair, out repeats);
                                    counts[pair] = repeats + 1;
                                }
                                if (entry.SubjectId != Guid.Empty)
                                {
                                    System.Collections.Generic.Dictionary<Guid, System.Collections.Generic.HashSet<long>> actors;
                                    if (!actorEventsByKind.TryGetValue(entry.Kind, out actors))
                                        actorEventsByKind[entry.Kind] = actors = new System.Collections.Generic.Dictionary<Guid, System.Collections.Generic.HashSet<long>>();
                                    if (!actors.TryGetValue(entry.SubjectId, out ids))
                                        actors[entry.SubjectId] = ids = new System.Collections.Generic.HashSet<long>();
                                    ids.Add(entry.EventId);
                                }
                            }
                            if (entry.Kind == "trade_refused" && entry.EventId != 0) {
                                refusedTradeIds.Add(entry.EventId);
                                refusedTradePairs.Add(entry.SubjectId.ToString("N") + ":" + entry.OtherId.ToString("N"));
                                refusedTradeInitiators.Add(entry.OtherId);
                            }
                            if (!uniqueTexts.Add(entry.Text)) duplicateTexts++;
                            if (entry.Kind == "heard_rumor")
                            {
                                listenerRumors++;
                                if (entry.Direct) rumorDirect++;
                                if (entry.CauseId == 0) rumorWithoutCause++;
                                else if (!heardCauses.Add(entry.CauseId)) repeatedRumorCause++;
                                if (!heardTexts.Add(entry.Text)) repeatedRumorText++;
                                int audience;
                                rumorAudience.TryGetValue(entry.EventId, out audience);
                                rumorAudience[entry.EventId] = audience + 1;
                            }
                        }
                        if (listenerRumors > 0) rumorListeners++;
                        rumorMaxPerListener = Math.Max(rumorMaxPerListener, listenerRumors);
                    }
                    var audienceSizes = new System.Collections.Generic.List<int>(rumorAudience.Values);
                    audienceSizes.Sort();
                    Console.WriteLine("REST RUMORS speeches {0}, listeners {1}, audience median {2}, p90 {3}, max {4}; direct {5}, repeated cause {6}, repeated text {7}, missing cause {8}, max/listener {9}",
                        audienceSizes.Count, rumorListeners,
                        audienceSizes.Count == 0 ? 0 : audienceSizes[audienceSizes.Count / 2],
                        audienceSizes.Count == 0 ? 0 : audienceSizes[(audienceSizes.Count - 1) * 9 / 10],
                        audienceSizes.Count == 0 ? 0 : audienceSizes[audienceSizes.Count - 1],
                        rumorDirect, repeatedRumorCause, repeatedRumorText, rumorWithoutCause, rumorMaxPerListener);
                    var ranked = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, int>>(kinds);
                    ranked.Sort((a, b) => b.Value.CompareTo(a.Value));
                    Console.WriteLine("REST ARCHIVE duplicate texts {0}/{1}, unique {2}",
                        duplicateTexts, entries, uniqueTexts.Count);
                    Console.WriteLine("REST TRADE REFUSALS unique events {0}, ordered pairs {1}, initiators {2}",
                        refusedTradeIds.Count, refusedTradePairs.Count, refusedTradeInitiators.Count);
                    int chatTrades = 0;
                    foreach (string time in tradedTimes) if (chatTimes.Contains(time)) chatTrades++;
                    Console.WriteLine("REST SOCIAL same-turn chat/trade pairs {0}/{1} trades", chatTrades, tradedTimes.Count);
                    foreach (string kind in new[] { "chat", "traded", "trade_refused" })
                    {
                        System.Collections.Generic.Dictionary<string, int> counts;
                        if (!pairCounts.TryGetValue(kind, out counts)) continue;
                        int max = 0, repeated = 0, frequent = 0;
                        string mostRepeatedPair = null;
                        foreach (var pair in counts)
                        {
                            if (pair.Value > max) { max = pair.Value; mostRepeatedPair = pair.Key; }
                            repeated += pair.Value - 1;
                            if (pair.Value >= 5) frequent++;
                        }
                        Console.WriteLine("REST SOCIAL {0}: {1} pairs, {2} repeat events, {3} pairs with 5+ events, max {4} per pair",
                            kind, counts.Count, repeated, frequent, max);
                        if (mostRepeatedPair != null)
                        {
                            int chats = 0, trades = 0, refusals = 0;
                            System.Collections.Generic.Dictionary<string, int> others;
                            if (pairCounts.TryGetValue("chat", out others)) others.TryGetValue(mostRepeatedPair, out chats);
                            if (pairCounts.TryGetValue("traded", out others)) others.TryGetValue(mostRepeatedPair, out trades);
                            if (pairCounts.TryGetValue("trade_refused", out others)) others.TryGetValue(mostRepeatedPair, out refusals);
                            Console.WriteLine("    busiest pair: {0} chats, {1} trades, {2} refusals", chats, trades, refusals);
                        }
                    }
                    for (int i = 0; i < Math.Min(15, ranked.Count); i++)
                    {
                        string kind = ranked[i].Key;
                        System.Collections.Generic.HashSet<long> ids;
                        System.Collections.Generic.Dictionary<Guid, System.Collections.Generic.HashSet<long>> actors;
                        int maxActor = 0;
                        if (actorEventsByKind.TryGetValue(kind, out actors))
                            foreach (var actor in actors.Values) maxActor = Math.Max(maxActor, actor.Count);
                        Console.WriteLine("  {0}: {1} archive entries, {2} events, {3} distinct actors, max {4} events per actor",
                            kind, ranked[i].Value, eventIdsByKind.TryGetValue(kind, out ids) ? ids.Count : 0,
                            actors == null ? 0 : actors.Count, maxActor);
                    }
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
        LastForegroundMs = 0;
        for (int turn = 0; turn < turns; turn++)
        {
            player.ActionPoints = Rules.BASE_ACTION_COST;
            game.DoWait(player);
            Stopwatch foreground = Stopwatch.StartNew();
            Advance.Invoke(game, new[] { (object)district, Active });
            LastForegroundMs += foreground.Elapsed.TotalMilliseconds;
            StartBackground.Invoke(game, null);
            try
            {
                int target = Session.Get.WorldTime.TurnCounter;
                if (!SpinWait.SpinUntil(() => AllDistrictsAt(target), 120000))
                    throw new InvalidOperationException("Background districts did not catch up to turn " + target);
            }
            finally { StopBackground.Invoke(game, new object[] { false }); }
            if (afterTurn != null) afterTurn(Session.Get, turn + 1);
        }
    }

    static bool AllDistrictsAt(int turn)
    {
        World city = Session.Get.World;
        for (int x = 0; x < city.Size; x++) for (int y = 0; y < city.Size; y++)
            if (city[x, y].EntryMap.LocalTime.TurnCounter < turn) return false;
        return true;
    }
}
