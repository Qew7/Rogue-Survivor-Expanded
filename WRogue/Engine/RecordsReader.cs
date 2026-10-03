using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Gameplay.Personality;

namespace djack.RogueSurvivor.Engine
{
    sealed class RecordsSave
    {
        public readonly string Path;
        readonly int m_SavedTurn;
        readonly Session m_LiveSession;
        public int Turn
        {
            get { return m_LiveSession == null ? m_SavedTurn : m_LiveSession.WorldTime.TurnCounter; }
        }
        public readonly ResidentRecords Records;
        internal readonly Dictionary<ResidentEntry, string> Display = new Dictionary<ResidentEntry, string>();
        internal readonly Dictionary<ResidentEntry, string> Context = new Dictionary<ResidentEntry, string>();
        internal Dictionary<long, ResidentEntry> Causes;
        int m_CachedTurn = -1, m_CachedResidents = -1, m_CachedEntries = -1;
        internal void Prepare()
        {
            int entries = 0;
            foreach (ResidentRecord resident in Records.Residents) entries += resident.EntryCount;
            if (Causes != null && m_CachedTurn == Turn && m_CachedResidents == Records.Residents.Count &&
                m_CachedEntries == entries) return;

            Display.Clear(); Context.Clear();
            Causes = RecordsReader.CauseIndex(this);
            foreach (ResidentRecord resident in Records.Residents)
                foreach (ResidentEntry entry in resident.Entries)
                {
                    if (entry.Turn > Turn) continue;
                    Display.Add(entry, RecordsReader.DisplayText(entry));
                    Context.Add(entry, RecordsReader.CauseContext(entry, Causes));
                }
            m_CachedTurn = Turn; m_CachedResidents = Records.Residents.Count; m_CachedEntries = entries;
            m_Profiles = null;
        }
        List<RecordsProfile> m_Profiles;
        public IList<RecordsProfile> Profiles
        {
            get
            {
                Prepare();
                if (m_Profiles == null)
                {
                    m_Profiles = new List<RecordsProfile>();
                    foreach (ResidentRecord resident in Records.Residents)
                        m_Profiles.Add(new RecordsProfile(resident, Turn));
                }
                return m_Profiles;
            }
        }
        public RecordsSave(string path, Session session)
        { Path = path; m_LiveSession = session; Records = session.ResidentRecords; }
        public RecordsSave(string path, int turn, ResidentRecords records)
        { Path = path; m_SavedTurn = turn; Records = records; }
    }

    static partial class RecordsReader
    {
        public static RecordsSave Load(string path)
        {
            // Deliberately do not restore Session, mods, options, player, or simulation.
            return BinarySaveStore.ReadRecords(path);
        }

        public static List<RecordsSave> Find(string directory, out int skipped)
        {
            List<RecordsSave> saves = new List<RecordsSave>();
            skipped = 0;
            if (!Directory.Exists(directory)) return saves;
            string[] files = Directory.GetFiles(directory);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            foreach (string file in files)
            {
                string candidate = file;
                if (candidate.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                    candidate = candidate.Substring(0, candidate.Length - 4);
                string extension = System.IO.Path.GetExtension(candidate);
                if (!extension.Equals(".dat", StringComparison.OrdinalIgnoreCase) &&
                    !extension.Equals(".sav", StringComparison.OrdinalIgnoreCase)) continue;
                try { saves.Add(Load(file)); }
                catch (Exception) { skipped++; }
            }
            return saves;
        }

        static bool EntryMatches(RecordsSave save, ResidentEntry entry, string search, RecordsEventFilter filter)
        {
            if (filter != RecordsEventFilter.All &&
                !NpcRecordDescriptions.Matches(entry, (NpcRecordCategory)(1 << ((int)filter - 1)))) return false;

            if (String.IsNullOrEmpty(search)) return true;
            if (entry.Text.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (entry.StoryId != null && entry.StoryId.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            if (save.Display[entry].IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            string context = save.Context[entry];
            return context != null && context.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static List<ResidentRecord> Residents(RecordsSave save)
        {
            List<ResidentRecord> people = new List<ResidentRecord>(save.Records.Residents);
            people.Sort((a, b) => {
                int name = String.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                if (name != 0) return name;
                return a.Identity.CompareTo(b.Identity);
            });
            return people;
        }

        public static IList<string> Lines(RecordsSave save, ResidentRecord selected)
        { return Lines(save, selected, null, "", RecordsEventFilter.All); }

        public static IList<string> Lines(RecordsSave save, ResidentRecord selected, RecordsQuery query,
            string search, RecordsEventFilter filter)
        {
            save.Prepare();
            List<KeyValuePair<ResidentRecord, ResidentEntry>> entries = MatchingEntries(save, selected, query, search, filter);
            entries.Sort(CompareEntries);

            List<string> lines = new List<string>();
            if (save.Records.IsPartial)
                lines.Add("Partial history: only recoverable records were available when the archive was rebuilt.");
            foreach (KeyValuePair<ResidentRecord, ResidentEntry> pair in entries)
                AddWrappedLine(lines, EntryLine(save, pair.Key, pair.Value));
            if (entries.Count == 0) lines.Add("No recorded events before this save.");
            return lines;
        }

        static List<KeyValuePair<ResidentRecord, ResidentEntry>> MatchingEntries(RecordsSave save,
            ResidentRecord selected, RecordsQuery query, string search, RecordsEventFilter filter)
        {
            IEnumerable<ResidentRecord> people = new List<ResidentRecord>(save.Records.Residents);
            if (selected != null) people = new[] { selected };
            else if (query != null) people = query.Select(save).ConvertAll(p => p.Resident);

            var entries = new List<KeyValuePair<ResidentRecord, ResidentEntry>>();
            foreach (ResidentRecord resident in people)
                foreach (ResidentEntry entry in resident.Entries)
                    if (entry.Turn <= save.Turn && !SelfEncounter(resident, entry) &&
                        EntryMatches(save, entry, search, filter))
                        entries.Add(new KeyValuePair<ResidentRecord, ResidentEntry>(resident, entry));
            return entries;
        }

        static int CompareEntries(KeyValuePair<ResidentRecord, ResidentEntry> a,
            KeyValuePair<ResidentRecord, ResidentEntry> b)
        {
            int turn = a.Value.Turn.CompareTo(b.Value.Turn);
            if (turn != 0) return turn;
            int person = a.Key.Identity.CompareTo(b.Key.Identity);
            if (person != 0) return person;
            return a.Value.Sequence.CompareTo(b.Value.Sequence);
        }

        static string EntryLine(RecordsSave save, ResidentRecord resident, ResidentEntry entry)
        {
            string line = new WorldTime(entry.Turn) + " | " + resident.Name + " | " + save.Display[entry];
            string context = save.Context[entry];
            if (context != null) line += " | " + context;
            return line;
        }

        static void AddWrappedLine(List<string> lines, string line)
        {
            const string indent = "    ";
            bool continuation = false;
            while (line.Length > 120)
            {
                int floor = continuation ? indent.Length : 0;
                int split = line.LastIndexOf(' ', 119, 120 - floor);
                // A break inside the continuation indent would consume no content.
                if (split <= floor) split = 120;
                lines.Add(line.Substring(0, split));
                line = indent + line.Substring(split).TrimStart();
                continuation = true;
            }
            lines.Add(line);
        }

        static bool SelfEncounter(ResidentRecord resident, ResidentEntry entry)
        {
            return !entry.Direct && entry.SubjectId == resident.Identity &&
                (entry.Kind == "unique_arrival" || entry.Kind == "met_unique" ||
                    entry.Kind.StartsWith("met_", StringComparison.Ordinal));
        }

        internal static string DisplayText(ResidentEntry entry)
        {
            if (entry.Text == null) return "";
            string text = WithoutStoryIds(entry.Text);
            string readable = null;
            if (entry.Kind == "goal_plan") readable = OldPlanText(text);
            else if (entry.Kind == "story_stage") readable = OldStoryStageText(text);
            else if (entry.Kind.StartsWith("goal_", StringComparison.Ordinal))
            {
                readable = OldIntentText(text);
                if (readable == null && entry.Kind == "goal_started") readable = RecentGoalStartText(text);
            }
            return readable ?? text;
        }

        static string WithoutStoryIds(string text)
        {
            int start = text.IndexOf(" [story ", StringComparison.Ordinal);
            while (start >= 0)
            {
                int end = text.IndexOf(']', start);
                if (end < 0) break;
                text = text.Remove(start, end - start + 1);
                start = text.IndexOf(" [story ", StringComparison.Ordinal);
            }
            return text.Trim();
        }

        static string OldPlanText(string text)
        {
            if (!text.StartsWith("Plan: ", StringComparison.Ordinal)) return null;
            string[] steps = text.Substring(6).TrimEnd('.').Split(new[] { " → " }, StringSplitOptions.None);
            for (int i = 0; i < steps.Length; i++) steps[i] = OldPlanStep(steps[i]);
            return "Planned route: " + String.Join(", then ", steps) + ".";
        }

        static string OldStoryStageText(string text)
        {
            if (!text.StartsWith("Story ", StringComparison.Ordinal)) return null;
            Match story = Regex.Match(text, @"^Story ([a-z_]+): ([a-z_]+)\.");
            if (!story.Success) return null;
            string subject = "The effort to " + story.Groups[1].Value.Replace('_', ' ');
            switch (story.Groups[2].Value)
            {
                case "completed": return subject + " succeeded.";
                case "failed": return subject + " failed.";
                case "abandoned": return subject + " was abandoned.";
                default: return subject + " continued.";
            }
        }

        static string OldIntentText(string text)
        {
            if (!text.StartsWith("Intent ", StringComparison.Ordinal)) return null;
            Match intent = Regex.Match(text, @"^Intent ([a-z]+): (.*?); target (.*?); (.*)\.$");
            if (!intent.Success) return null;
            string state = intent.Groups[1].Value;
            string goal = intent.Groups[2].Value;
            string reason = intent.Groups[4].Value;
            Match need = Regex.Match(reason, @"^([^:]+): (-?\d+) → (-?\d+); deficit (\d+), importance (\d+), confidence (\d+), utility \d+(?:; (.*))?$");
            if (state == "started" && need.Success)
                return ReadableGoalStart("Decided to " + goal.ToLowerInvariant(), need.Groups[1].Value,
                    need.Groups[4].Value, need.Groups[5].Value, intent.Groups[3].Value,
                    need.Groups[7].Value);

            string detail = goal + ". " + reason + ".";
            if (state == "completed") return "Achieved: " + detail;
            if (state == "started") return "Decided to: " + detail;
            return "Goal " + state + ": " + detail;
        }

        static string RecentGoalStartText(string text)
        {
            if (text.IndexOf(". Need unmet: ", StringComparison.Ordinal) < 0) return null;
            Match recent = Regex.Match(text, @"^(.*?) decided to (.*?)\. Need unmet: (\d+)% \(current -?\d+, desired -?\d+\); importance: (\d+)/200; confidence: \d+%(?:\. The (.*?) trait raised its importance by \d+ points)?\.$");
            if (!recent.Success) return null;
            string goal = recent.Groups[2].Value;
            int forTarget = goal.IndexOf(" for ", StringComparison.Ordinal);
            string target = forTarget < 0 ? null : goal.Substring(forTarget + 5);
            string trait = null;
            if (recent.Groups[5].Success)
                trait = "because trait " + recent.Groups[5].Value + " raised this goal's importance";
            return ReadableGoalStart(recent.Groups[1].Value + " decided to " + goal,
                ValueForGoal(goal), recent.Groups[3].Value, recent.Groups[4].Value, target, trait);
        }

        static string ReadableGoalStart(string action, string value, string deficitText, string importanceText,
            string target, string trait)
        {
            int deficit, importance;
            string result = action;
            if (Int32.TryParse(deficitText, out deficit) && Int32.TryParse(importanceText, out importance))
            {
                string context = ResidentRecords.GoalContext(value, deficit, importance, target);
                if (context != null) result += ". " + context;
            }
            Match influence = Regex.Match(trait ?? "", @"(?:because trait |The )([^;]+?)(?: raised| trait)");
            if (influence.Success) result += ". The " + influence.Groups[1].Value + " trait made this goal more compelling";
            return result + ".";
        }

        static string ValueForGoal(string goal)
        {
            goal = goal.ToLowerInvariant();
            if (goal.StartsWith("have usable food", StringComparison.Ordinal)) return "Nutrition";
            if (goal.StartsWith("keep a reserve of usable food", StringComparison.Ordinal)) return "FoodReserve";
            if (goal.StartsWith("provide needed food", StringComparison.Ordinal)) return "Care";
            if (goal.StartsWith("meet a person's medical need", StringComparison.Ordinal)) return "MedicalCare";
            if (goal.StartsWith("recover health", StringComparison.Ordinal)) return "Recovery";
            if (goal.StartsWith("reach safety", StringComparison.Ordinal)) return "Safety";
            return "";
        }

        static string OldPlanStep(string step)
        {
            switch (step)
            {
                case "Travel": case "travel": return "travel onward";
                case "EnterShelter": case "shelter.enter": return "enter the shelter";
                case "PickupFood": case "food.take": return "collect food";
                case "AskFood": case "food.ask": return "ask for food";
                case "GiveFood": case "food.give": return "give food";
                default: return Regex.Replace(step.Replace('_', ' ').Replace('.', ' '), "([a-z])([A-Z])", "$1 $2").ToLowerInvariant();
            }
        }
    }
}
