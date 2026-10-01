using System;
using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Engine
{
    struct RecordsTextRun
    {
        public readonly int Start, Length;
        public readonly Color Color;
        public RecordsTextRun(int start, int length, Color color)
        { Start = start; Length = length; Color = color; }
    }

    // The archive already stores names and factions. Rendering needs no world graph.
    sealed class RecordsNameColors
    {
        sealed class NameColor
        {
            public string Name;
            public Color Color;
        }

        static readonly Dictionary<string, Color> Factions = new Dictionary<string, Color>(StringComparer.Ordinal) {
            { "Civilians", Color.LightSkyBlue }, { "Survivors", Color.LightGreen },
            { "Police", Color.CornflowerBlue }, { "Army", Color.Khaki },
            { "Bikers", Color.Orange }, { "Gangstas", Color.Orchid },
            { "CHAR Corp.", Color.Cyan }, { "BlackOps", Color.Silver },
            { "Psychopaths", Color.OrangeRed }, { "Undeads", Color.PaleGreen },
            { "Ferals", Color.Tan }
        };
        readonly Dictionary<char, List<NameColor>> names = new Dictionary<char, List<NameColor>>();

        public RecordsNameColors(IEnumerable<ResidentRecord> residents)
        {
            var unique = new Dictionary<string, NameColor>(StringComparer.Ordinal);
            foreach (ResidentRecord resident in residents)
            {
                if (String.IsNullOrEmpty(resident.Name)) continue;
                Color color;
                if (!Factions.TryGetValue(resident.FactionName ?? "", out color)) color = Color.LightGray;
                NameColor current;
                if (unique.TryGetValue(resident.Name, out current))
                {
                    if (current.Color != color) current.Color = Color.LightGray;
                }
                else unique.Add(resident.Name, new NameColor { Name = resident.Name, Color = color });
            }
            foreach (NameColor current in unique.Values)
            {
                List<NameColor> initial;
                if (!names.TryGetValue(current.Name[0], out initial))
                    names.Add(current.Name[0], initial = new List<NameColor>());
                initial.Add(current);
            }
            foreach (List<NameColor> initial in names.Values)
                initial.Sort((a, b) => b.Name.Length.CompareTo(a.Name.Length));
        }

        public IList<RecordsTextRun> Runs(string text, Color plain)
        {
            var runs = new List<RecordsTextRun>();
            int start = 0;
            for (int index = 0; index < text.Length; index++)
            {
                List<NameColor> candidates;
                if (!names.TryGetValue(text[index], out candidates)) continue;
                foreach (NameColor candidate in candidates)
                {
                    int length = candidate.Name.Length;
                    if (index + length > text.Length ||
                        String.CompareOrdinal(text, index, candidate.Name, 0, length) != 0 ||
                        index > 0 && Char.IsLetterOrDigit(text[index - 1]) ||
                        index + length < text.Length && Char.IsLetterOrDigit(text[index + length])) continue;
                    if (index > start) runs.Add(new RecordsTextRun(start, index - start, plain));
                    runs.Add(new RecordsTextRun(index, length, candidate.Color));
                    index += length - 1;
                    start = index + 1;
                    break;
                }
            }
            if (start < text.Length) runs.Add(new RecordsTextRun(start, text.Length - start, plain));
            return runs;
        }
    }
}
