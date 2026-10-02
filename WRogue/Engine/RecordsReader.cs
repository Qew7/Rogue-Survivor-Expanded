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
        public readonly int Turn;
        public readonly ResidentRecords Records;
        List<RecordsProfile> m_Profiles;
        public IList<RecordsProfile> Profiles { get {
            if (m_Profiles == null) { m_Profiles = new List<RecordsProfile>();
                foreach (ResidentRecord resident in Records.Residents) m_Profiles.Add(new RecordsProfile(resident, Turn)); }
            return m_Profiles; } }
        public RecordsSave(string path, Session session)
        { Path = path; Turn = session.WorldTime.TurnCounter; Records = session.ResidentRecords; }
        public RecordsSave(string path, int turn, ResidentRecords records)
        { Path = path; Turn = turn; Records = records; }
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
                string candidate = file.EndsWith(".bak", StringComparison.OrdinalIgnoreCase)
                    ? file.Substring(0, file.Length - 4) : file;
                string extension = System.IO.Path.GetExtension(candidate);
                if (!extension.Equals(".dat", StringComparison.OrdinalIgnoreCase) &&
                    !extension.Equals(".sav", StringComparison.OrdinalIgnoreCase)) continue;
                try { saves.Add(Load(file)); }
                catch (Exception) { skipped++; }
            }
            return saves;
        }

        static bool EntryMatches(ResidentEntry entry, string search, RecordsEventFilter filter,
            Dictionary<long, ResidentEntry> causes)
        {
            if (filter != RecordsEventFilter.All &&
                !NpcRecordDescriptions.Matches(entry, (NpcRecordCategory)(1 << ((int)filter - 1)))) return false;
            if (String.IsNullOrEmpty(search) || entry.Text.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                (!String.IsNullOrEmpty(entry.StoryId) && entry.StoryId.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                DisplayText(entry).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            string context = CauseContext(entry, causes);
            return context != null && context.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static List<ResidentRecord> Residents(RecordsSave save)
        {
            List<ResidentRecord> people = new List<ResidentRecord>(save.Records.Residents);
            people.Sort((a, b) => {
                int name = String.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                return name != 0 ? name : a.Identity.CompareTo(b.Identity);
            });
            return people;
        }

        public static IList<string> Lines(RecordsSave save, ResidentRecord selected)
        { return Lines(save, selected, null, "", RecordsEventFilter.All); }

        public static IList<string> Lines(RecordsSave save, ResidentRecord selected, RecordsQuery query,
            string search, RecordsEventFilter filter)
        {
            List<KeyValuePair<ResidentRecord, ResidentEntry>> entries =
                new List<KeyValuePair<ResidentRecord, ResidentEntry>>();
            Dictionary<long, ResidentEntry> causes = CauseIndex(save);
            IEnumerable<ResidentRecord> people = selected == null
                ? (IEnumerable<ResidentRecord>)(query == null ? new List<ResidentRecord>(save.Records.Residents) :
                    query.Select(save).ConvertAll(p => p.Resident)) : new[] { selected };
            foreach (ResidentRecord resident in people)
                foreach (ResidentEntry entry in resident.Entries)
                    if (entry.Turn <= save.Turn && !SelfEncounter(resident, entry) &&
                        EntryMatches(entry, search, filter, causes))
                        entries.Add(new KeyValuePair<ResidentRecord, ResidentEntry>(resident, entry));
            entries.Sort((a, b) => {
                int turn = a.Value.Turn.CompareTo(b.Value.Turn);
                if (turn != 0) return turn;
                int person = a.Key.Identity.CompareTo(b.Key.Identity);
                return person != 0 ? person : a.Value.Sequence.CompareTo(b.Value.Sequence);
            });
            List<string> lines = new List<string>();
            if (save.Records.IsPartial)
                lines.Add("Partial history: only recoverable records were available when the archive was rebuilt.");
            foreach (KeyValuePair<ResidentRecord, ResidentEntry> entry in entries)
            {
                string line = new WorldTime(entry.Value.Turn) + " | " + entry.Key.Name +
                    " | " + DisplayText(entry.Value);
                string context = CauseContext(entry.Value, causes);
                if (context != null) line += " | " + context;
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
            if (entries.Count == 0) lines.Add("No recorded events before this save.");
            return lines;
        }

        static bool SelfEncounter(ResidentRecord resident, ResidentEntry entry)
        {
            return !entry.Direct && entry.SubjectId == resident.Identity &&
                (entry.Kind == "unique_arrival" || entry.Kind == "met_unique" ||
                    entry.Kind.StartsWith("met_", StringComparison.Ordinal));
        }

        static string DisplayText(ResidentEntry entry)
        {
            string text = entry.Text;
            if (text == null) return "";
            int start = text.IndexOf(" [story ", StringComparison.Ordinal);
            while (start >= 0)
            {
                int end = text.IndexOf(']', start);
                if (end < 0) break;
                text = text.Remove(start, end - start + 1);
                start = text.IndexOf(" [story ", StringComparison.Ordinal);
            }
            text = text.Trim();
            if (entry.Kind == "goal_plan" && text.StartsWith("Plan: ", StringComparison.Ordinal))
            {
                string[] steps = text.Substring(6).TrimEnd('.').Split(new[] { " → " }, StringSplitOptions.None);
                for (int i = 0; i < steps.Length; i++) steps[i] = OldPlanStep(steps[i]);
                return "Planned route: " + String.Join(", then ", steps) + ".";
            }
            if (entry.Kind == "story_stage" && text.StartsWith("Story ", StringComparison.Ordinal))
            {
                Match story = Regex.Match(text, @"^Story ([a-z_]+): ([a-z_]+)\.");
                if (story.Success)
                {
                    string goal = story.Groups[1].Value.Replace('_', ' ');
                    string stage = story.Groups[2].Value;
                    return "The effort to " + goal + (stage == "completed" ? " succeeded." :
                        stage == "failed" ? " failed." : stage == "abandoned" ? " was abandoned." : " continued.");
                }
            }
            if (entry.Kind.StartsWith("goal_", StringComparison.Ordinal) && text.StartsWith("Intent ", StringComparison.Ordinal))
            {
                Match intent = Regex.Match(text, @"^Intent ([a-z]+): (.*?); target (.*?); (.*)\.$");
                if (intent.Success)
                {
                    string state = intent.Groups[1].Value, goal = intent.Groups[2].Value;
                    string reason = intent.Groups[4].Value;
                    Match need = Regex.Match(reason, @"^[^:]+: (-?\d+) → (-?\d+); deficit (\d+), importance (\d+), confidence (\d+), utility \d+(?:; (.*))?$");
                    if (state == "started" && need.Success)
                        return "Decided to " + goal.ToLowerInvariant() + ". Need unmet: " + need.Groups[3].Value +
                            "% (current " + need.Groups[1].Value + ", desired " + need.Groups[2].Value +
                            "); importance: " + need.Groups[4].Value + "/200; confidence: " + need.Groups[5].Value +
                            "%" + (need.Groups[6].Success ? ". " + need.Groups[6].Value.Replace("because trait ", "Trait ") : "") + ".";
                    return (state == "completed" ? "Achieved: " : state == "started" ? "Decided to: " : "Goal " + state + ": ") +
                        goal + ". " + reason + ".";
                }
            }
            return text;
        }

        static string OldPlanStep(string step)
        {
            switch (step)
            {
                case "Travel": case "travel": return "travel to the destination";
                case "EnterShelter": case "shelter.enter": return "enter the shelter";
                case "PickupFood": case "food.take": return "collect food";
                case "AskFood": case "food.ask": return "ask for food";
                case "GiveFood": case "food.give": return "give food";
                default: return Regex.Replace(step.Replace('_', ' ').Replace('.', ' '), "([a-z])([A-Z])", "$1 $2").ToLowerInvariant();
            }
        }
    }
}
