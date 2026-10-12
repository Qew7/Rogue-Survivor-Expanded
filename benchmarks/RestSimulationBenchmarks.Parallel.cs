using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static partial class RestSimulationBenchmarks
{
    public static void RunParallel()
    {
        Console.WriteLine("EXPERIMENT ONLY: shared game state is not safe for production parallel turns.");
        const int turns = 30;
        const int samples = 3;
        foreach (int workers in new[] { 0, 1, 2, 4 })
        {
            RunOne(false, 5, 70, GameOptions.SimRatio.FULL, turns: turns, workers: workers);
            double[] elapsed = new double[samples];
            for (int sample = 0; sample < samples; sample++)
            {
                Console.WriteLine("  workers {0}, sample {1}", workers, sample + 1);
                elapsed[sample] = RunOne(false, 5, 70, GameOptions.SimRatio.FULL,
                    turns: turns, workers: workers, report: true);
            }
            Array.Sort(elapsed);
            Console.WriteLine("REST PARALLEL {0} workers: {1:F1}, {2:F1}, {3:F1} ms; median {2:F1} ms",
                workers, elapsed[0], elapsed[1], elapsed[2]);
        }
    }

    static void RunRestParallel(RogueGame game, World city, Actor player, int turns, int workers,
        Action<Session, int> afterTurn = null)
    {
        // A color group has no adjacent districts, but global story, radio,
        // record, and RNG state can still be touched by concurrent turns.
        var colors = new List<District>[4];
        for (int i = 0; i < colors.Length; i++) colors[i] = new List<District>();
        for (int x = 0; x < city.Size; x++) for (int y = 0; y < city.Size; y++)
            if (x != 0 || y != 0) colors[(x & 1) * 2 + (y & 1)].Add(city[x, y]);
        var options = new ParallelOptions { MaxDegreeOfParallelism = workers };
        for (int turn = 0; turn < turns; turn++)
        {
            player.ActionPoints = Rules.BASE_ACTION_COST;
            game.DoWait(player);
            Advance.Invoke(game, new[] { (object)city[0, 0], Active });
            if (workers == 1)
            {
                for (int x = 0; x < city.Size; x++)
                    for (int y = 0; y < city.Size; y++)
                        if (x != 0 || y != 0)
                            Advance.Invoke(game, new[] { (object)city[x, y], Active });
            }
            else
                foreach (List<District> group in colors)
                    Parallel.ForEach(group, options,
                        district => Advance.Invoke(game, new[] { (object)district, Active }));
            if (afterTurn != null) afterTurn(Session.Get, turn + 1);
        }
    }

    static void ReportParallelOutcome(World city)
    {
        int actors = 0, facts = 0, intents = 0, events = 0;
        for (int x = 0; x < city.Size; x++) for (int y = 0; y < city.Size; y++)
            foreach (Map map in city[x, y].Maps)
                foreach (Actor actor in map.Actors)
                {
                    actors++;
                    if (actor.Personality == null) continue;
                    facts += actor.Personality.Knowledge.Facts.Count;
                    intents += actor.Personality.Intents.Count;
                    events += actor.Personality.Events.Count;
                }
        Console.WriteLine("  outcome: {0} actors, {1} facts, {2} intents, {3} events, {4} records",
            actors, facts, intents, events, Session.Get.ResidentRecords.Residents.Count);
    }
}
