using System;
using System.Collections.Generic;
using System.IO;
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

    static class RecordsReader
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

        static bool EntryMatches(ResidentEntry entry, string search, RecordsEventFilter filter)
        {
            if (!String.IsNullOrEmpty(search) && entry.Text.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) return false;
            if (filter == RecordsEventFilter.All) return true;
            return NpcRecordDescriptions.Matches(entry, (NpcRecordCategory)(1 << ((int)filter - 1)));
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
            IEnumerable<ResidentRecord> people = selected == null
                ? (IEnumerable<ResidentRecord>)(query == null ? new List<ResidentRecord>(save.Records.Residents) :
                    query.Select(save).ConvertAll(p => p.Resident)) : new[] { selected };
            foreach (ResidentRecord resident in people)
                foreach (ResidentEntry entry in resident.Entries)
                    if (entry.Turn <= save.Turn && EntryMatches(entry, search, filter))
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
                    " [" + entry.Key.Identity.ToString("N").Substring(0, 8) + "] | " + entry.Value.Text;
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
    }
}
