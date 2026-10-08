using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.MapObjects;

static class RadioBenchmarks
{
    const int Broadcasts = 80;
    const int Samples = 5;
    static readonly MethodInfo Broadcast = typeof(RogueGame).GetMethod("BroadcastRadio",
        BindingFlags.Instance | BindingFlags.NonPublic);

    sealed class Fixture
    {
        public ScenarioWorld World;
        public Point Position;
        public int Station;
        public Actor[] Sources;
    }

    public static void Run()
    {
        Measure(new Point(2, 1), 0);
        double[] samples = new double[Samples];
        for (int station = 0; station < 4; station++)
        {
            for (int i = 0; i < Samples; i++) samples[i] = Measure(new Point(2, 1), station);
            Array.Sort(samples);
            Console.WriteLine("RADIO station {0}, 9 maps, 216 sources, 3456 facts, 20 listeners, {1} broadcasts across 40 hourly slots; median of {2}: {3:F1} ms ({4:F3} ms/broadcast)",
                station, Broadcasts, Samples, samples[Samples / 2], samples[Samples / 2] / Broadcasts);
        }
        for (int i = 0; i < Samples; i++) samples[i] = Measure(new Point(13, 7), 0);
        Array.Sort(samples);
        Console.WriteLine("RADIO empty audience, same city, {0} broadcasts; median of {1}: {2:F1} ms ({3:F3} ms/broadcast)",
            Broadcasts, Samples, samples[Samples / 2], samples[Samples / 2] / Broadcasts);
    }

    // The two candidate stores share the same rolling source facts and ranking work.
    // This isolates the cost of keeping news available as rumors arrive hourly.
    public static void CompareNewsStrategies()
    {
        const int hours = 96;
        const int arrivalsPerHour = 12;
        const int repeats = 5;
        double[] scanned = new double[repeats], indexed = new double[repeats], daily = new double[repeats];
        long scanSeen = 0, indexSeen = 0, dailySeen = 0;
        for (int sample = 0; sample < repeats + 1; sample++)
        {
            Fixture fixture = Create();
            var index = new NewsCandidates(fixture.Sources, false);
            var snapshot = new NewsCandidates(fixture.Sources, true);
            double scanMs = 0, indexMs = 0, dailyMs = 0;
            long nextId = 100000;
            for (int hour = 24; hour < 24 + hours; hour++)
            {
                int turn = hour * WorldTime.TURNS_PER_HOUR;
                Session.Get.WorldTime.TurnCounter = turn;
                for (int a = 0; a < arrivalsPerHour; a++)
                {
                    int sourceIndex = (hour * arrivalsPerHour + a) % fixture.Sources.Length;
                    Actor source = fixture.Sources[sourceIndex];
                    source.Personality.Knowledge.Learn(new NpcFact {
                        EventId = nextId++, Kind = a % 4 == 0 ? "bikers_raid" : "shared_food",
                        StoryId = "rolling-" + sourceIndex, EventTurn = turn, LearnedTurn = turn,
                        Place = source.Location, Source = NpcKnowledgeSource.Told,
                        Confidence = 60, Hops = 1, SubjectName = "survivor"
                    });
                }
                // Four station queries per hour; renewal and incremental updates are timed.
                long start = Stopwatch.GetTimestamp();
                scanSeen += ScanNews(fixture.Sources, turn);
                scanMs += (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                start = Stopwatch.GetTimestamp();
                indexSeen += index.Query(turn);
                indexMs += (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                start = Stopwatch.GetTimestamp();
                dailySeen += snapshot.Query(turn);
                dailyMs += (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            }
            if (sample > 0) { scanned[sample - 1] = scanMs; indexed[sample - 1] = indexMs; daily[sample - 1] = dailyMs; }
        }
        Array.Sort(scanned); Array.Sort(indexed); Array.Sort(daily);
        Console.WriteLine("RADIO NEWS 216 sources, 3456 initial facts, 12 rumors/hour, 96 hours, four station scans/hour; median of {0}", repeats);
        Console.WriteLine("  current full scan: {0:F2} ms", scanned[repeats / 2]);
        Console.WriteLine("  incremental index: {0:F2} ms", indexed[repeats / 2]);
        Console.WriteLine("  previous-day snapshot: {0:F2} ms", daily[repeats / 2]);
        Console.WriteLine("  candidates examined (all samples): scan {0}, index {1}, snapshot {2}", scanSeen, indexSeen, dailySeen);
    }

    public static void RollingBroadcasts()
    {
        const int samples = 5;
        double[] times = new double[samples];
        int newsCount = 0;
        for (int sample = 0; sample <= samples; sample++)
        {
            Fixture fixture = Create();
            long nextId = 100000;
            double elapsed = 0;
            int aired = 0;
            for (int hour = 24; hour < 120; hour++)
            {
                int turn = hour * WorldTime.TURNS_PER_HOUR;
                fixture.World.Map.LocalTime.TurnCounter = turn;
                Session.Get.WorldTime.TurnCounter = turn;
                for (int a = 0; a < 12; a++)
                {
                    int sourceIndex = (hour * 12 + a) % fixture.Sources.Length;
                    Actor source = fixture.Sources[sourceIndex];
                    source.Personality.Knowledge.Learn(new NpcFact {
                        EventId = nextId++, Kind = a % 4 == 0 ? "bikers_raid" : "shared_food",
                        StoryId = "rolling-" + sourceIndex, EventTurn = turn, LearnedTurn = turn,
                        Place = source.Location, Source = NpcKnowledgeSource.Told,
                        Confidence = 60, Hops = 1, SubjectName = "survivor"
                    });
                }
                long start = Stopwatch.GetTimestamp();
                for (int station = 0; station < 4; station++)
                    Broadcast.Invoke(fixture.World.Game, new object[] { station, fixture.World.Map, fixture.Position, null });
                elapsed += (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
                for (int station = 0; station < 4; station++)
                    if (Session.Get.RadioPrograms[station] != null && Session.Get.RadioPrograms[station].Facts != null)
                        aired++;
            }
            if (sample > 0) { times[sample - 1] = elapsed; newsCount = aired; }
        }
        Array.Sort(times);
        Console.WriteLine("RADIO ROLLING 216 sources, 3456 initial facts, 12 new rumors/hour, 96 hours, 4 stations, 20 listeners: {0:F2} ms median of {1}; {2} news programs",
            times[samples / 2], samples, newsCount);
    }

    static int ScanNews(Actor[] sources, int turn)
    {
        int examined = 0;
        for (int station = 0; station < 4; station++)
        {
            int best = Int32.MinValue;
            for (int i = 0; i < sources.Length; i++)
                foreach (NpcFact fact in sources[i].Personality.Knowledge.Facts)
                {
                    int age = turn - fact.EventTurn;
                    if (fact.Source != NpcKnowledgeSource.Told || fact.Confidence < 40 || fact.Hops >= 3 ||
                        fact.Place.Map == null || age > 2 * WorldTime.TURNS_PER_DAY ||
                        !RadioKindMatches(station, fact.Kind)) continue;
                    examined++;
                    int score = (2 * WorldTime.TURNS_PER_DAY - Math.Max(0, age)) / 30 +
                        (int)((uint)(fact.EventId * 1103515245L + turn * 101L + station * 37L + 7822) % 64u);
                    if (score > best) best = score;
                }
        }
        return examined;
    }

    sealed class NewsCandidates
    {
        readonly Actor[] sources;
        readonly bool daily;
        readonly int[] revisions;
        readonly List<NpcFact>[][] facts;
        int day = -1;

        public NewsCandidates(Actor[] sources, bool daily)
        {
            this.sources = sources; this.daily = daily;
            revisions = new int[sources.Length];
            facts = new List<NpcFact>[sources.Length][];
        }

        public int Query(int turn)
        {
            int currentDay = turn / WorldTime.TURNS_PER_DAY;
            int cutoff = currentDay * WorldTime.TURNS_PER_DAY;
            bool renew = day != currentDay;
            if (renew) day = currentDay;
            for (int i = 0; i < sources.Length; i++)
            {
                var knowledge = sources[i].Personality.Knowledge;
                if (!renew && (daily || revisions[i] == knowledge.Revision)) continue;
                revisions[i] = knowledge.Revision;
                var byStation = new List<NpcFact>[4];
                for (int station = 0; station < 4; station++) byStation[station] = new List<NpcFact>();
                foreach (NpcFact fact in knowledge.Facts)
                {
                    if (fact.Source != NpcKnowledgeSource.Told || fact.Confidence < 40 || fact.Hops >= 3 ||
                        fact.Place.Map == null || turn - fact.EventTurn > 2 * WorldTime.TURNS_PER_DAY ||
                        daily && (fact.LearnedTurn >= cutoff || fact.EventTurn >= cutoff)) continue;
                    byStation[0].Add(fact);
                    for (int station = 1; station < 4; station++)
                        if (RadioKindMatches(station, fact.Kind)) byStation[station].Add(fact);
                }
                facts[i] = byStation;
            }
            int examined = 0;
            for (int station = 0; station < 4; station++)
            {
                int best = Int32.MinValue;
                for (int i = 0; i < sources.Length; i++)
                    foreach (NpcFact fact in facts[i][station])
                    {
                        int age = turn - fact.EventTurn;
                        if (age > 2 * WorldTime.TURNS_PER_DAY) continue;
                        examined++;
                        int score = (2 * WorldTime.TURNS_PER_DAY - Math.Max(0, age)) / 30 +
                            (int)((uint)(fact.EventId * 1103515245L + turn * 101L + station * 37L + 7822) % 64u);
                        if (score > best) best = score;
                    }
            }
            return examined;
        }
    }

    static bool RadioKindMatches(int station, string kind)
    {
        switch (station)
        {
            case 1: return kind == "army_supplies" || kind == "national_guard_arrival" || kind == "blackops_raid";
            case 2: return kind == "shared_food" || kind.StartsWith("requested_", StringComparison.Ordinal) ||
                kind.Contains("medicine") || kind.Contains("shelter") || kind.Contains("food");
            case 3: return kind.Contains("raid") || kind == "attack" || kind == "murder" ||
                kind.Contains("theft") || kind.StartsWith("stolen_goods_", StringComparison.Ordinal) ||
                kind == "supplies_lost" || kind == "base_robbed";
            default: return true;
        }
    }

    public static void Profile()
    {
        Fixture fixture = Create();
        RunBroadcasts(fixture, 1);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        double start = ProcessAgeSeconds();
        RunBroadcasts(fixture, Broadcasts, 26);
        Console.WriteLine("PROFILE_WINDOW {0:F3} {1:F3}", start, ProcessAgeSeconds());
        Console.WriteLine("PROFILE_RADIO {0} broadcasts across 40 hourly slots, 9 maps, 216 sources, 3456 facts, 20 listeners", Broadcasts);
    }

    static double Measure(Point position, int station)
    {
        Fixture fixture = Create();
        fixture.Position = position;
        fixture.Station = station;
        RunBroadcasts(fixture, 1);
        GC.Collect();
        long start = Stopwatch.GetTimestamp();
        RunBroadcasts(fixture, Broadcasts, 26);
        return (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
    }

    static double ProcessAgeSeconds()
    { return (DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void RunBroadcasts(Fixture fixture, int count, int firstHour = 25)
    {
        for (int i = 0; i < count; i++)
        {
            // Two receivers hear each shared hourly program; the next pair forces a new selection.
            fixture.World.Map.LocalTime.TurnCounter = WorldTime.TURNS_PER_HOUR * (firstHour + i / 2);
            Session.Get.WorldTime.TurnCounter = WorldTime.TURNS_PER_HOUR * (firstHour + i / 2);
            Broadcast.Invoke(fixture.World.Game, new object[] { fixture.Station, fixture.World.Map, fixture.Position, null });
        }
    }

    static Fixture Create()
    {
        ScenarioWorld world = TownScenarioFactory.Arena(7822,
            "..............", "..............", "..............", "..............",
            "..............", "..............", "..............", "..............");
        Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
        NpcIntentSupport.Player(world, 1, 1);
        for (int i = 0; i < 19; i++)
            NpcIntentSupport.Actor(world, "listener", 2 + i % 5, 1 + i / 5);
        District district = world.Map.District;
        Actor host = null;
        Actor[] sources = new Actor[216];
        for (int m = 0; m < 9; m++)
        {
            Map map = new Map(7823 + m, "radio-source", 8, 8);
            for (int y = 0; y < map.Height; y++) for (int x = 0; x < map.Width; x++)
                map.SetTileModelAt(x, y, world.Game.GameTiles.FLOOR_ASPHALT);
            district.AddUniqueMap(map);
            for (int a = 0; a < 24; a++)
            {
                Actor source = new Actor(world.Game.GameActors.MaleCivilian,
                    world.Game.GameFactions.TheCivilians, "source", true, false, 0);
                source.Personality = new PersonalityState();
                map.PlaceActorAt(source, new Point(a % 8, a / 8));
                sources[m * 24 + a] = source;
                if (host == null) host = source;
                for (int f = 0; f < 16; f++)
                    source.Personality.Knowledge.Facts.Add(new NpcFact {
                        EventId = 1 + m * 384 + a * 16 + f, Kind = "shared_food",
                        StoryId = "story-" + m + "-" + a, EventTurn = f,
                        Place = source.Location, Source = NpcKnowledgeSource.Told,
                        Confidence = 60, Hops = 1, SubjectName = "survivor"
                    });
            }
        }
        Session.Get.RadioHostId = host.PersonalityIdentity;
        world.Map.LocalTime.TurnCounter = 30;
        Session.Get.WorldTime.TurnCounter = 30;
        return new Fixture { World = world, Position = new Point(2, 1), Sources = sources };
    }
}
