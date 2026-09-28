using System;
using System.Collections.Generic;
using System.IO;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Engine
{
    sealed class RecordsSave
    {
        public readonly string Path;
        public readonly int Turn;
        public readonly ResidentRecords Records;
        public RecordsSave(string path, Session session)
        { Path = path; Turn = session.WorldTime.TurnCounter; Records = session.ResidentRecords; }
    }

    static class RecordsReader
    {
        public static RecordsSave Load(string path)
        {
            // Deliberately do not restore Session, mods, options, player, or simulation.
            Session saved = BinarySaveStore.LoadExact<Session>(path);
            if (!saved.GamePreset.NpcPersonalitiesEnabled)
                throw new InvalidDataException("NPC traits and memories are disabled in this save.");
            return new RecordsSave(path, saved);
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
        {
            List<KeyValuePair<ResidentRecord, ResidentEntry>> entries =
                new List<KeyValuePair<ResidentRecord, ResidentEntry>>();
            IEnumerable<ResidentRecord> people = selected == null
                ? save.Records.Residents : new[] { selected };
            foreach (ResidentRecord resident in people)
                foreach (ResidentEntry entry in resident.Entries)
                    if (entry.Turn <= save.Turn)
                        entries.Add(new KeyValuePair<ResidentRecord, ResidentEntry>(resident, entry));
            entries.Sort((a, b) => {
                int turn = a.Value.Turn.CompareTo(b.Value.Turn);
                if (turn != 0) return turn;
                int person = a.Key.Identity.CompareTo(b.Key.Identity);
                return person != 0 ? person : a.Value.Sequence.CompareTo(b.Value.Sequence);
            });
            List<string> lines = new List<string>();
            if (save.Records.IsPartial)
                lines.Add("Partial history: older saves retain only the records that were still available.");
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
