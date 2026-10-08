using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
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

    public static void Profile()
    {
        Fixture fixture = Create();
        RunBroadcasts(fixture, 1);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        double start = ProcessAgeSeconds();
        RunBroadcasts(fixture, Broadcasts, 2);
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
        RunBroadcasts(fixture, Broadcasts, 2);
        return (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
    }

    static double ProcessAgeSeconds()
    { return (DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalSeconds; }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void RunBroadcasts(Fixture fixture, int count, int firstHour = 1)
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
        return new Fixture { World = world, Position = new Point(2, 1) };
    }
}
