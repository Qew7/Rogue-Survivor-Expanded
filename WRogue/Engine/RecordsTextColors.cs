using System;
using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Gameplay;

namespace djack.RogueSurvivor.Engine
{
    struct RecordsTextRun
    {
        public readonly int Start, Length;
        public readonly Color Color;
        public RecordsTextRun(int start, int length, Color color)
        { Start = start; Length = length; Color = color; }
    }

    // Only saved resident metadata and stable place labels are needed for rendering.
    sealed class RecordsTextColors
    {
        sealed class ColoredTerm
        {
            public string Name;
            public Color Color;
        }

        readonly Dictionary<char, List<ColoredTerm>> terms = new Dictionary<char, List<ColoredTerm>>();

        public RecordsTextColors(IEnumerable<ResidentRecord> residents)
        {
            var residentsByName = new Dictionary<string, ColoredTerm>(StringComparer.Ordinal);
            foreach (ResidentRecord resident in residents)
            {
                if (String.IsNullOrEmpty(resident.Name)) continue;
                Color color = GameFactions.RecordColor(resident.FactionName);
                ColoredTerm current;
                if (residentsByName.TryGetValue(resident.Name, out current))
                {
                    if (current.Color != color) current.Color = GameFactions.UnknownRecordColor;
                }
                else residentsByName.Add(resident.Name, new ColoredTerm { Name = resident.Name, Color = color });
            }
            foreach (ColoredTerm current in residentsByName.Values)
                Add(current);
            foreach (BuildingKind kind in Enum.GetValues(typeof(BuildingKind)))
            {
                string label = Zone.BuildingLabel(kind);
                if (label != null && !residentsByName.ContainsKey(label))
                    Add(new ColoredTerm { Name = label, Color = Zone.BuildingColor(kind) });
            }
            foreach (List<ColoredTerm> initial in terms.Values)
                initial.Sort((a, b) => b.Name.Length.CompareTo(a.Name.Length));
        }

        void Add(ColoredTerm term)
        {
            List<ColoredTerm> initial;
            if (!terms.TryGetValue(term.Name[0], out initial))
                terms.Add(term.Name[0], initial = new List<ColoredTerm>());
            initial.Add(term);
        }

        public IList<RecordsTextRun> Runs(string text, Color plain)
        {
            var runs = new List<RecordsTextRun>();
            int start = 0;
            for (int index = 0; index < text.Length; index++)
            {
                List<ColoredTerm> candidates;
                if (!terms.TryGetValue(text[index], out candidates)) continue;
                foreach (ColoredTerm candidate in candidates)
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
