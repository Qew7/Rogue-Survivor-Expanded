using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed class PersonalityRegistry
    {
        public NpcContentCatalog Content { get; internal set; }
        readonly Dictionary<string, TraitDefinition> m_Traits = new Dictionary<string, TraitDefinition>();
        readonly Dictionary<string, MemoryDefinition> m_Memories = new Dictionary<string, MemoryDefinition>();
        readonly List<TraitDefinition> m_StartingTraits = new List<TraitDefinition>();
        readonly List<MemoryDefinition> m_StartingMemories = new List<MemoryDefinition>();
        readonly Dictionary<string, List<MemoryDefinition>> m_ByEvent = new Dictionary<string, List<MemoryDefinition>>();

        public IEnumerable<TraitDefinition> StartingTraits { get { return m_StartingTraits; } }
        public IEnumerable<MemoryDefinition> StartingMemories { get { return m_StartingMemories; } }
        public IEnumerable<TraitDefinition> AllTraits { get { return m_Traits.Values; } }
        public IEnumerable<MemoryDefinition> AllMemories { get { return m_Memories.Values; } }
        public int TraitCount { get { return m_Traits.Count; } }
        public int AdvancedTraitCount { get { int n = 0; foreach (TraitDefinition t in m_Traits.Values) if (t.Advanced) n++; return n; } }

        public void Register(TraitDefinition definition)
        {
            if (definition == null || String.IsNullOrEmpty(definition.Id) || m_Traits.ContainsKey(definition.Id))
                throw new ArgumentException("Duplicate or empty trait ID.");
            m_Traits.Add(definition.Id, definition);
            if (!definition.Advanced) m_StartingTraits.Add(definition);
        }

        public void Register(MemoryDefinition definition, bool starting)
        {
            if (definition == null || String.IsNullOrEmpty(definition.Id) || m_Memories.ContainsKey(definition.Id) ||
                definition.MinDays < 1 || definition.MaxDays < definition.MinDays ||
                definition.EvidenceKinds == null)
                throw new ArgumentException("Invalid memory definition.");
            foreach (string kind in definition.EvidenceKinds)
                if (String.IsNullOrEmpty(kind))
                    throw new ArgumentException("Invalid evidence kind.");
            m_Memories.Add(definition.Id, definition);
            if (starting) m_StartingMemories.Add(definition);
            foreach (MemoryTrigger trigger in definition.Triggers)
            {
                List<MemoryDefinition> list;
                if (!m_ByEvent.TryGetValue(trigger.EventKind, out list))
                {
                    list = new List<MemoryDefinition>();
                    m_ByEvent.Add(trigger.EventKind, list);
                }
                if (!list.Contains(definition)) list.Add(definition);
            }
        }

        public TraitDefinition Trait(string id)
        {
            TraitDefinition definition;
            return id != null && m_Traits.TryGetValue(id, out definition) ? definition : null;
        }

        public void Conflict(string left, string right)
        {
            TraitDefinition a = Trait(left);
            TraitDefinition b = Trait(right);
            if (a == null || b == null) throw new ArgumentException("Unknown conflicting trait.");
            a.AddConflict(right);
            b.AddConflict(left);
        }

        public MemoryDefinition Memory(string id)
        {
            MemoryDefinition definition;
            return id != null && m_Memories.TryGetValue(id, out definition) ? definition : null;
        }

        public IEnumerable<MemoryDefinition> ForEvent(string kind)
        {
            List<MemoryDefinition> definitions;
            if (kind != null && m_ByEvent.TryGetValue(kind, out definitions))
                return definitions;
            return new MemoryDefinition[0];
        }
    }
}
