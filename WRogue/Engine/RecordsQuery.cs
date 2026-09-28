using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Engine
{
    enum RecordsSort { Name, Items, Memories, Days, Events, Interesting, Encounters, Kills, Help, Resolved, TraitChanges }
    enum RecordsLife { Any, Alive, Dead }
    enum RecordsEventFilter { All, Memories, Combat, Help, Encounters, World, Life }

    sealed class RecordsProfile
    {
        public readonly ResidentRecord Resident;
        public readonly double Days;
        public readonly bool DaysKnown;
        public readonly int Events, Memories, Resolved, TraitChanges, DirectEvents, Encounters, Kills, Help, WorldEvents, Diversity, Score;
        public RecordsProfile(ResidentRecord resident, int savedTurn)
        {
            Resident = resident;
            DaysKnown = resident.DeathTurn != -2;
            int end = resident.DeathTurn >= 0 ? Math.Min(savedTurn, resident.DeathTurn) : savedTurn;
            Days = Math.Max(0, (long)end - resident.SpawnTurn) / (double)WorldTime.TURNS_PER_DAY;
            HashSet<Guid> people = new HashSet<Guid>(); HashSet<string> kinds = new HashSet<string>();
            foreach (ResidentEntry entry in resident.Entries)
            {
                if (entry.Turn > savedTurn) continue;
                if (entry.Kind == "memory") { Memories++; continue; }
                if (entry.Kind == "resolved")
                {
                    Resolved++;
                    if (entry.GainedTrait) TraitChanges++;
                    continue;
                }
                if (entry.Kind == "spawn" || entry.Kind == "initial-trait") continue;
                Events++; kinds.Add(entry.Kind);
                if (entry.Direct) DirectEvents++;
                if (entry.Kind == "kill_human" && entry.OtherId == resident.Identity) Kills++;
                if (entry.Kind == "helped" && entry.OtherId == resident.Identity) Help++;
                if (IsWorld(entry.Kind)) WorldEvents++;
                if (entry.SubjectId != Guid.Empty && entry.SubjectId != resident.Identity) people.Add(entry.SubjectId);
                if (entry.OtherId != Guid.Empty && entry.OtherId != resident.Identity) people.Add(entry.OtherId);
            }
            Encounters = people.Count; Diversity = kinds.Count;
            // Caps stop repeated routine events, loot farming or age alone dominating a story.
            Score = 8 * Math.Min(Diversity, 12) + 2 * Math.Min(DirectEvents, 40) +
                5 * Math.Min(Memories, 20) + 6 * Math.Min(Resolved, 12) + 8 * Math.Min(TraitChanges, 10) +
                3 * Math.Min(Encounters, 15) + 4 * Math.Min(WorldEvents, 15) + 4 * Math.Min(Help, 12);
        }
        internal static bool IsWorld(string kind)
        {
            return !String.IsNullOrEmpty(kind) && (kind.EndsWith("_raid", StringComparison.Ordinal) || kind.EndsWith("_arrival", StringComparison.Ordinal) ||
                kind == "raid" || kind == "zombie_invasion" || kind == "army_supplies" || kind == "floods" || kind == "craps");
        }
        public string Summary()
        {
            return Resident.Name + " [" + Resident.Identity.ToString("N").Substring(0, 8) + "] | " +
                (Resident.DeathTurn == -1 ? "alive" : "dead") + " | days " + (DaysKnown ? Days.ToString("F1") : "?") +
                " | items " + Resident.ItemsReceived + " | memories " + Memories + " | interest " + Score;
        }
        public IList<string> Details()
        {
            return new[] { Summary(), "Faction: " + Resident.FactionName + " | Group: " + (String.IsNullOrEmpty(Resident.GroupName) ? "none" : Resident.GroupName),
                "Items received over life: " + Resident.ItemsReceived + " (repeat pickups count again); last inventory: " +
                    Resident.InventoryUnits + " units in " + Resident.InventoryStacks + " stacks.",
                "Events: " + Events + " | direct: " + DirectEvents + " | kinds: " + Diversity + " | Unique participants in events: " + Encounters,
                "Memories: " + Memories + " | resolved: " + Resolved + " | gained traits: " + TraitChanges + " | current/last traits: " + Resident.Traits,
                "Human kills: " + Kills + " | help given: " + Help + " | world events: " + WorldEvents,
                "Interest score " + Score + ": diversity*8 (cap12), direct*2 (40), memories*5 (20), resolved*6 (12),",
                "gained traits*8 (10), encounters*3 (15), world events*4 (15), help given*4 (12).", "" };
        }
    }

    sealed partial class RecordsQuery
    {
        public string Name = "", Faction = "", Group = "";
        public RecordsLife Life;
        public RecordsSort Sort;
        public bool Reverse;
        public long MinItems;
        public int MinMemories, MinEvents, MinEncounters, MinKills, MinResolved, MinTraitChanges;
        public double MinDays, MaxDays = Double.MaxValue;
        static bool Contains(string value, string query)
        { return String.IsNullOrEmpty(query) || (value != null && value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0); }
        public List<RecordsProfile> Select(RecordsSave save)
        {
            List<RecordsProfile> selected = new List<RecordsProfile>();
            foreach (RecordsProfile p in save.Profiles)
            {
                ResidentRecord r = p.Resident;
                bool alive = r.DeathTurn == -1;
                if (!Contains(r.Name, Name) || !Contains(r.FactionName, Faction) || !Contains(r.GroupName, Group) ||
                    (Life == RecordsLife.Alive && !alive) || (Life == RecordsLife.Dead && alive) ||
                    r.ItemsReceived < MinItems || p.Memories < MinMemories || p.Events < MinEvents ||
                    p.Encounters < MinEncounters || p.Kills < MinKills || p.Resolved < MinResolved ||
                    p.TraitChanges < MinTraitChanges || (!p.DaysKnown && (MinDays > 0 || MaxDays < Double.MaxValue)) ||
                    p.Days < MinDays || p.Days > MaxDays) continue;
                selected.Add(p);
            }
            selected.Sort((a, b) => Compare(a, b)); return selected;
        }
        int Compare(RecordsProfile a, RecordsProfile b)
        {
            int result;
            if (Sort == RecordsSort.Name) result = String.Compare(a.Resident.Name, b.Resident.Name, StringComparison.OrdinalIgnoreCase);
            else if (Sort == RecordsSort.Items) result = b.Resident.ItemsReceived.CompareTo(a.Resident.ItemsReceived);
            else if (Sort == RecordsSort.Days && a.DaysKnown != b.DaysKnown) return a.DaysKnown ? -1 : 1;
            else result = Metric(b).CompareTo(Metric(a));
            if (Reverse) result = -result;
            if (result != 0) return result;
            result = String.Compare(a.Resident.Name, b.Resident.Name, StringComparison.OrdinalIgnoreCase);
            return result != 0 ? result : a.Resident.Identity.CompareTo(b.Resident.Identity);
        }
        double Metric(RecordsProfile p)
        {
            switch (Sort)
            {
                case RecordsSort.Items: return p.Resident.ItemsReceived;
                case RecordsSort.Memories: return p.Memories;
                case RecordsSort.Days: return p.Days;
                case RecordsSort.Events: return p.Events;
                case RecordsSort.Interesting: return p.Score;
                case RecordsSort.Encounters: return p.Encounters;
                case RecordsSort.Kills: return p.Kills;
                case RecordsSort.Help: return p.Help;
                case RecordsSort.Resolved: return p.Resolved;
                case RecordsSort.TraitChanges: return p.TraitChanges;
                default: return 0;
            }
        }
        public RecordsProfile MostInteresting(RecordsSave save)
        {
            List<RecordsProfile> matches = Select(save);
            matches.Sort((a, b) => { int score = b.Score.CompareTo(a.Score);
                if (score != 0) return score; int name = String.Compare(a.Resident.Name, b.Resident.Name, StringComparison.OrdinalIgnoreCase);
                return name != 0 ? name : a.Resident.Identity.CompareTo(b.Resident.Identity); });
            return matches.Count == 0 ? null : matches[0];
        }
    }
}
