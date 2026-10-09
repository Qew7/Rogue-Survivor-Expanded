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

}
