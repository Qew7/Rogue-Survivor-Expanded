using System;
using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.MapObjects;

static partial class RadioBenchmarks
{
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

}
