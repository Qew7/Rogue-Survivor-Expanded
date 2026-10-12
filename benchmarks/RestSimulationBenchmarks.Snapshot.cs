using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static partial class RestSimulationBenchmarks
{
    public static bool CompareSnapshot()
    {
        const int turns = 30;
        string start = Path.Combine(Path.GetTempPath(), "rogue-parallel-start-" + Guid.NewGuid().ToString("N"));
        try
        {
            RunOne(false, 5, 70, GameOptions.SimRatio.FULL, turns: 0, snapshotPath: start);
            Console.WriteLine("SAME START: one saved 5x5 city, {0} waits per run", turns);
            string firstActors = null, firstRumors = null, firstStories = null;
            var baselineTurns = new List<string>();
            bool equivalent = true;
            foreach (int workers in new[] { 0, 0, 1, 2, 4 })
            {
                string actors = null, rumors = null, stories = null;
                int firstDifferentTurn = 0;
                double elapsed;
                try
                {
                    elapsed = RunOne(false, 5, 70, GameOptions.SimRatio.FULL,
                        turns: turns, workers: workers, report: true,
                        snapshotPath: start, restoreSnapshot: true,
                        afterRun: session => DigestWorld(session, out actors, out rumors, out stories),
                        afterTurn: (session, turn) =>
                        {
                            string turnActors, turnRumors, turnStories;
                            DigestWorld(session, out turnActors, out turnRumors, out turnStories);
                            string digest = turnActors + turnRumors + turnStories;
                            if (workers == 0 && baselineTurns.Count < turns) baselineTurns.Add(digest);
                            else if (firstDifferentTurn == 0 && digest != baselineTurns[turn - 1])
                                firstDifferentTurn = turn;
                        });
                }
                catch (Exception error)
                {
                    equivalent = false;
                    Console.WriteLine("  {0} worker(s): simulation failed after wait {1}: {2}",
                        workers, firstDifferentTurn, error.GetBaseException().Message);
                    continue;
                }
                if (firstActors == null) { firstActors = actors; firstRumors = rumors; firstStories = stories; }
                bool matches = actors == firstActors && rumors == firstRumors && stories == firstStories;
                if (!matches) equivalent = false;
                Console.WriteLine("  {0} worker(s): {1:F1} ms; actors {2}; rumors {3}; stories {4}; logical match: {5}",
                    workers, elapsed, actors, rumors, stories, matches);
                if (firstDifferentTurn != 0)
                    Console.WriteLine("    first state divergence after wait {0}", firstDifferentTurn);
            }
            Console.WriteLine("PARALLEL EQUIVALENCE: {0}", equivalent ? "PASS" : "FAIL");
            return equivalent;
        }
        finally
        {
            if (File.Exists(start)) File.Delete(start);
            if (File.Exists(start + ".bak")) File.Delete(start + ".bak");
        }
    }

    static void DigestWorld(Session session, out string actors, out string rumors, out string stories)
    {
        var actorLines = new List<string>();
        var rumorLines = new List<string>();
        var storyLines = new List<string>();
        World city = session.World;
        for (int x = 0; x < city.Size; x++) for (int y = 0; y < city.Size; y++)
            foreach (Map map in city[x, y].Maps)
                foreach (Actor actor in map.Actors)
                {
                    string place = x + "," + y + ":" + map.Name + ":" + actor.Location.Position;
                    string who = place + ":" + actor.Name + ":" + actor.Model.ID;
                    actorLines.Add(who + ":" + actor.HitPoints + ":" + actor.FoodPoints + ":" +
                        actor.SleepPoints + ":" + actor.Sanity + ":" + actor.ActionPoints + ":" + actor.IsDead);
                    if (actor.Personality == null) continue;
                    foreach (NpcFact fact in actor.Personality.Knowledge.Facts)
                        rumorLines.Add(who + ":" + fact.Kind + ":" + fact.SubjectName + ":" + fact.OtherName +
                            ":" + fact.EventTurn + ":" + fact.LearnedTurn + ":" + fact.Source + ":" +
                            fact.Confidence + ":" + fact.Hops);
                    foreach (NpcIntent intent in actor.Personality.Intents)
                        storyLines.Add(who + ":intent:" + intent.DefinitionId + ":" + intent.TargetName +
                            ":" + intent.StartedTurn + ":" + intent.Status + ":" + intent.Progress +
                            ":" + intent.Outcome + ":" + intent.FinishedTurn);
                    foreach (ObservedEvent observed in actor.Personality.Events)
                        storyLines.Add(who + ":event:" + observed.Kind + ":" + observed.Turn + ":" +
                            observed.Subject + ":" + observed.Other + ":" + observed.Direct);
                }
        foreach (NpcStory story in session.NpcDirector.Stories)
            storyLines.Add("story:" + story.Template + ":" + story.Stage + ":" + story.StartedTurn +
                ":" + story.FinishedTurn + ":" + story.Roles.Count);
        actors = DigestLines(actorLines);
        rumors = DigestLines(rumorLines);
        stories = DigestLines(storyLines);
    }

    static string DigestLines(List<string> lines)
    {
        lines.Sort(StringComparer.Ordinal);
        byte[] bytes = Encoding.UTF8.GetBytes(String.Join("\n", lines.ToArray()));
        using (SHA256 sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").Substring(0, 16).ToLowerInvariant();
    }
}
