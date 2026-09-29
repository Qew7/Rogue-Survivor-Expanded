using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    // Keep legacy bits stable. Local bindings never share the catalog's fact range.
    sealed class NpcFactLayout
    {
        public const int MaximumPlaces = 32, MaximumQuestions = 8;
        public const ulong Locations = 0x0000FFFFFFFF0000UL;
        public const ulong Away = 1UL << 11;
        readonly Dictionary<string, NpcPlanningState> flags = new Dictionary<string, NpcPlanningState>();
        public NpcFactLayout(IEnumerable<string> additional)
        {
            foreach (NpcPlanFact fact in Enum.GetValues(typeof(NpcPlanFact))) if (fact != NpcPlanFact.None) flags.Add(fact.ToString(), (ulong)fact);
            flags.Add("Away", Away);
            var keys = new List<string>(additional); keys.Sort(StringComparer.Ordinal);
            if (keys.Count > 200) throw new ArgumentException("NPC plan supports 200 additional catalog facts.");
            for (int i = 0; i < keys.Count; i++)
            { if (flags.ContainsKey(keys[i])) throw new ArgumentException("Reserved fact ID: " + keys[i]); flags.Add(keys[i], NpcPlanningState.Bit(56 + i)); }
        }
        public NpcPlanningState Mask(string id)
        { NpcPlanningState flag; if (!flags.TryGetValue(id, out flag)) throw new ArgumentException("Unknown plan fact: " + id); return flag; }
        public ulong this[string id]
        { get { NpcPlanningState flag = Mask(id); if (flag.Extended) throw new ArgumentException("Use the symbolic mask API for extended facts."); return flag.Low; } }
        public static ulong Location(int index)
        { if (index < 0 || index >= MaximumPlaces) throw new ArgumentOutOfRangeException("index"); return 1UL << (16 + index); }
        public static ulong Question(int index)
        { if (index < 0 || index >= MaximumQuestions) throw new ArgumentOutOfRangeException("index"); return 1UL << (48 + index); }
    }
}
