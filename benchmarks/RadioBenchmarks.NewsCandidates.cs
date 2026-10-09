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

}
