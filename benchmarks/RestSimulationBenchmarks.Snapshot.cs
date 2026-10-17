using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static partial class RestSimulationBenchmarks
{
    static string[][] LastDigestLines;
    public static void TimeProductionSchedule()
    {
        const int turns = 30;
        string start = Path.Combine(Path.GetTempPath(), "rogue-parallel-timing-" + Guid.NewGuid().ToString("N"));
        try
        {
            RunOne(false, 5, 70, GameOptions.SimRatio.FULL, turns: 0, snapshotPath: start);
            foreach (int workers in new[] { 1, 2, 4, 8 })
            {
                double[] samples = new double[3];
                double[] foreground = new double[3];
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] = RunOne(false, 5, 70, GameOptions.SimRatio.FULL,
                        turns: turns, workers: workers, snapshotPath: start,
                        restoreSnapshot: true, parallelRest: true);
                    foreground[i] = LastForegroundMs;
                }
                Array.Sort(samples);
                Array.Sort(foreground);
                Console.WriteLine("REST TIMING exact arrivals, {0} workers: {1:F1}, {2:F1}, {3:F1} ms; median {2:F1} ms",
                    workers, samples[0], samples[1], samples[2]);
                Console.WriteLine("  foreground median {0:F1} ms ({1:F1} ms/wait)",
                    foreground[1], foreground[1] / turns);
            }
        }
        finally
        {
            if (File.Exists(start)) File.Delete(start);
            if (File.Exists(start + ".bak")) File.Delete(start + ".bak");
        }
    }

    public static bool CompareProductionSnapshot()
    {
        const int turns = 30;
        string start = Path.Combine(Path.GetTempPath(), "rogue-parallel-start-" + Guid.NewGuid().ToString("N"));
        try
        {
            RunOne(false, 5, 70, GameOptions.SimRatio.FULL, turns: 0, snapshotPath: start);
            Console.WriteLine("SAME START: one saved 5x5 city, {0} waits per run", turns);
            string firstActors = null, firstRumors = null, firstStories = null, firstWorld = null;
            var baselineTurns = new List<string>();
            var baselineParts = new List<string[]>();
            var baselineDetails = new List<string[][]>();
            bool equivalent = true;
            foreach (int workers in new[] { 1, 1, 2, 4, 8 })
            {
                string actors = null, rumors = null, stories = null, worldState = null;
                int firstDifferentTurn = 0;
                double elapsed;
                try
                {
                    elapsed = RunOne(false, 5, 70, GameOptions.SimRatio.FULL,
                        turns: turns, workers: workers, report: true,
                        snapshotPath: start, restoreSnapshot: true, parallelRest: true,
                        afterRun: session => DigestWorld(session, out actors, out rumors, out stories, out worldState),
                        afterTurn: (session, turn) =>
                        {
                            string turnActors, turnRumors, turnStories, turnWorld;
                            DigestWorld(session, out turnActors, out turnRumors, out turnStories, out turnWorld);
                            string digest = turnActors + turnRumors + turnStories + turnWorld;
                            if (workers == 1 && baselineTurns.Count < turns)
                            {
                                baselineTurns.Add(digest);
                                baselineParts.Add(new[] { turnActors, turnRumors, turnStories, turnWorld });
                                baselineDetails.Add(LastDigestLines);
                            }
                            else if (firstDifferentTurn == 0 && digest != baselineTurns[turn - 1])
                            {
                                firstDifferentTurn = turn;
                                string[] parts = { turnActors, turnRumors, turnStories, turnWorld };
                                string[] labels = { "actors", "rumors", "stories", "world/radio/archive" };
                                for (int part = 0; part < parts.Length; part++)
                                    if (parts[part] != baselineParts[turn - 1][part])
                                    {
                                        Console.WriteLine("    first wait differs in {0}", labels[part]);
                                        string[] oldLines = baselineDetails[turn - 1][part], newLines = LastDigestLines[part];
                                        int oldAt = 0, newAt = 0;
                                        while (oldAt < oldLines.Length && newAt < newLines.Length && oldLines[oldAt] == newLines[newAt])
                                        { oldAt++; newAt++; }
                                        Console.WriteLine("      1 worker: {0}", oldAt < oldLines.Length ? oldLines[oldAt] : "<end>");
                                        Console.WriteLine("      {0} worker: {1}", workers, newAt < newLines.Length ? newLines[newAt] : "<end>");
                                        if (part == 2)
                                        {
                                            var oldSet = new HashSet<string>(oldLines);
                                            var newSet = new HashSet<string>(newLines);
                                            int shown = 0;
                                            foreach (string line in oldLines) if (!newSet.Contains(line) && shown++ < 4)
                                                Console.WriteLine("      only 1 worker: {0}", line);
                                            shown = 0;
                                            foreach (string line in newLines) if (!oldSet.Contains(line) && shown++ < 4)
                                                Console.WriteLine("      only {0} worker: {1}", workers, line);
                                        }
                                    }
                            }
                        });
                }
                catch (Exception error)
                {
                    equivalent = false;
                    Console.WriteLine("  {0} worker(s): simulation failed after wait {1}: {2}",
                        workers, firstDifferentTurn, error.GetBaseException().Message);
                    continue;
                }
                if (firstActors == null) { firstActors = actors; firstRumors = rumors; firstStories = stories; firstWorld = worldState; }
                bool matches = actors == firstActors && rumors == firstRumors && stories == firstStories && worldState == firstWorld;
                if (!matches) equivalent = false;
                Console.WriteLine("  {0} worker(s): {1:F1} ms; actors {2}; rumors {3}; stories {4}; world {5}; logical match: {6}",
                    workers, elapsed, actors, rumors, stories, worldState, matches);
                if (firstDifferentTurn != 0)
                {
                    equivalent = false;
                    Console.WriteLine("    first state divergence after wait {0}", firstDifferentTurn);
                }
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

    static void DigestWorld(Session session, out string actors, out string rumors, out string stories, out string worldState)
    {
        var actorLines = new List<string>();
        var rumorLines = new List<string>();
        var storyLines = new List<string>();
        var worldLines = new List<string>();
        World city = session.World;
        for (int x = 0; x < city.Size; x++) for (int y = 0; y < city.Size; y++)
        {
            DiceRoller roller = city[x, y].SimulationDiceRoller(session.Seed);
            worldLines.Add("district-dice:" + x + "," + y + ":" +
                typeof(DiceRoller).GetField("m_State", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(roller));
            foreach (Map map in city[x, y].Maps)
            {
                worldLines.Add(x + "," + y + ":" + map.Name + ":turn:" + map.LocalTime.TurnCounter);
                foreach (Actor actor in map.Actors)
                {
                    string place = x + "," + y + ":" + map.Name + ":" + actor.Location.Position;
                    string who = place + ":" + actor.Name + ":" + actor.Model.ID + ":" + actor.ExistingPersonalityIdentity;
                    actorLines.Add(who + ":" + actor.HitPoints + ":" + actor.FoodPoints + ":" +
                        actor.SleepPoints + ":" + actor.Sanity + ":" + actor.ActionPoints + ":" + actor.IsDead);
                    if (actor.Personality == null) continue;
                    int factIndex = 0;
                    foreach (NpcFact fact in actor.Personality.Knowledge.Facts)
                        rumorLines.Add(who + ":" + fact.Kind + ":" + fact.SubjectName + ":" + fact.OtherName +
                            ":" + fact.EventTurn + ":" + fact.LearnedTurn + ":" + fact.Source + ":" +
                            fact.Confidence + ":" + fact.Hops + ":" + fact.EventId + ":" + fact.CauseId +
                            ":" + fact.StoryId + ":" + factIndex++);
                    int intentIndex = 0;
                    foreach (NpcIntent intent in actor.Personality.Intents)
                        storyLines.Add(who + ":intent:" + intent.DefinitionId + ":" + intent.TargetName +
                            ":" + intent.StartedTurn + ":" + intent.Status + ":" + intent.Progress +
                            ":" + intent.Outcome + ":" + intent.FinishedTurn + ":" + intent.StoryId +
                            ":" + intentIndex++);
                    foreach (ObservedEvent observed in actor.Personality.Events)
                        storyLines.Add(who + ":event:" + observed.Kind + ":" + observed.Turn + ":" +
                            observed.Subject + ":" + observed.Other + ":" + observed.Direct);
                    worldLines.Add(who + ":radio-drop:" + actor.Personality.LastRadioDropTurn);
                    foreach (HeardJournalEntry heard in actor.Personality.HeardJournal)
                        if (heard.Kind == "radio")
                            worldLines.Add(who + ":radio:" + heard.Turn + ":" + heard.Speaker + ":" + heard.Text);
                }
            }
        }
        int storyIndex = 0;
        foreach (NpcStory story in session.NpcDirector.Stories)
            storyLines.Add("story:" + story.Id + ":" + story.Template + ":" + story.Stage + ":" + story.StartedTurn +
                ":" + story.FinishedTurn + ":" + story.Roles.Count + ":" + storyIndex++);
        foreach (ResidentRecord record in session.ResidentRecords.Residents)
            foreach (ResidentEntry entry in record.Entries)
                worldLines.Add("record:" + record.Name + ":" + entry.Turn + ":" + entry.Kind + ":" + entry.Text);
        worldLines.Add("drop:" + session.RadioDropTurn + ":" + session.RadioDropDistrict);
        foreach (KeyValuePair<long, RadioProgram> row in session.RadioProgramHistory)
            worldLines.Add("radio-program:" + row.Key + ":" + row.Value.EventId + ":" + row.Value.Text);
        actors = DigestLines(actorLines);
        rumors = DigestLines(rumorLines);
        stories = DigestLines(storyLines);
        worldState = DigestLines(worldLines);
        LastDigestLines = new[] { actorLines.ToArray(), rumorLines.ToArray(), storyLines.ToArray(), worldLines.ToArray() };
    }

    static string DigestLines(List<string> lines)
    {
        lines.Sort(StringComparer.Ordinal);
        byte[] bytes = Encoding.UTF8.GetBytes(String.Join("\n", lines.ToArray()));
        using (SHA256 sha = SHA256.Create())
            return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").Substring(0, 16).ToLowerInvariant();
    }

    static void ReportParallelOutcome(World city)
    {
        int actors = 0, facts = 0, intents = 0, events = 0, foreignToldFacts = 0;
        for (int x = 0; x < city.Size; x++) for (int y = 0; y < city.Size; y++)
            foreach (Map map in city[x, y].Maps)
                foreach (Actor actor in map.Actors)
                {
                    actors++;
                    if (actor.Personality == null) continue;
                    facts += actor.Personality.Knowledge.Facts.Count;
                    foreach (NpcFact fact in actor.Personality.Knowledge.Facts)
                        if (fact.Source == NpcKnowledgeSource.Told && fact.Place.Map != null &&
                            fact.Place.Map.District != map.District) foreignToldFacts++;
                    intents += actor.Personality.Intents.Count;
                    events += actor.Personality.Events.Count;
                }
        Console.WriteLine("  outcome: {0} actors, {1} facts, {2} intents, {3} events, {4} records, {5} told facts from other districts",
            actors, facts, intents, events, Session.Get.ResidentRecords.Residents.Count, foreignToldFacts);
    }
}
