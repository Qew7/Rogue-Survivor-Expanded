using System;
using System.Collections.Generic;

namespace djack.RogueSurvivor.Data
{
    [Serializable]
    sealed class TraitInstance
    {
        public readonly string Id;
        // -1 means that the trait is not tied to an item model.
        public readonly int ItemModelId;

        public TraitInstance(string id, int itemModelId = -1)
        {
            Id = id;
            ItemModelId = itemModelId;
        }
    }

    [Serializable]
    sealed class ObservedEvent
    {
        public readonly string Kind;
        public readonly int Turn;
        public readonly string Subject;
        public readonly string Other;
        public readonly bool Direct;
        public readonly bool RelatedToSubject;
        public readonly Guid SubjectId;
        public readonly Guid OtherId;
        public readonly long EventId, CauseId;
        public readonly string StoryId;

        public ObservedEvent(string kind, int turn, string subject, string other, bool direct,
            bool relatedToSubject = false, Guid subjectId = default(Guid),
            Guid otherId = default(Guid), long eventId = 0, long causeId = 0, string storyId = null)
        {
            EventId = eventId; CauseId = causeId; StoryId = storyId;
            Kind = kind;
            Turn = turn;
            Subject = subject;
            Other = other;
            Direct = direct;
            RelatedToSubject = relatedToSubject;
            SubjectId = subjectId;
            OtherId = otherId;
        }
    }

    [Serializable]
    sealed class MemoryInstance
    {
        public readonly string Id;
        public readonly int StartTurn;
        public readonly int ResolveTurn;
        public readonly string Subject;
        public readonly bool RelatedToSubject;
        public readonly Guid SubjectId;
        public readonly Guid RelatedPersonId;
        public int ResolvedTurn;
        public string OutcomeId;
        readonly Dictionary<string, int> m_EvidenceTurns = new Dictionary<string, int>();

        public MemoryInstance(string id, int startTurn, int resolveTurn, string subject,
            bool relatedToSubject = false, Guid subjectId = default(Guid),
            Guid relatedPersonId = default(Guid))
        {
            Id = id;
            StartTurn = startTurn;
            ResolveTurn = resolveTurn;
            Subject = subject;
            RelatedToSubject = relatedToSubject;
            SubjectId = subjectId;
            RelatedPersonId = relatedPersonId;
        }

        public void RememberEvidence(string kind, int turn)
        {
            int previous;
            if (!m_EvidenceTurns.TryGetValue(kind, out previous) || turn > previous)
                m_EvidenceTurns[kind] = turn;
        }

        public bool HasEvidenceSince(string kind, int turn)
        {
            int observedTurn;
            return m_EvidenceTurns.TryGetValue(kind, out observedTurn) && observedTurn >= turn;
        }
    }

    // A relationship retains its memories after they leave the pending queue. IDs rather
    // than Actor references keep dead people and former groups in the saved history.
    [Serializable]
    sealed class RelationshipRecord
    {
        public readonly Guid Identity;
        public readonly int FactionId;
        public readonly string Name;
        public int Feeling;
        readonly List<MemoryInstance> m_Memories = new List<MemoryInstance>();

        public IList<MemoryInstance> Memories { get { return m_Memories.AsReadOnly(); } }

        public RelationshipRecord(Guid identity, int factionId, string name)
        {
            Identity = identity;
            FactionId = factionId;
            Name = name;
        }

        public void Remember(MemoryInstance memory, int change)
        {
            if (m_Memories.Contains(memory)) return;
            m_Memories.Add(memory);
            Feeling = Math.Max(-100, Math.Min(100, Feeling + change));
        }
    }

    // This is the only new per-actor object in the saved world graph.
    [Serializable]
    sealed partial class PersonalityState
    {
        readonly List<TraitInstance> m_Traits = new List<TraitInstance>(4);
        readonly List<MemoryInstance> m_Memories = new List<MemoryInstance>(2);
        readonly List<ObservedEvent> m_Events = new List<ObservedEvent>(8);
        // Lazily initialized so personalities from older saves also have an empty tree.
        Dictionary<Guid, RelationshipRecord> m_People;
        Dictionary<Guid, RelationshipRecord> m_Groups;
        Dictionary<int, RelationshipRecord> m_Factions;

        Dictionary<Guid, RelationshipRecord> PersonRecords
        {
            get { return m_People ?? (m_People = new Dictionary<Guid, RelationshipRecord>()); }
        }
        Dictionary<Guid, RelationshipRecord> GroupRecords
        {
            get { return m_Groups ?? (m_Groups = new Dictionary<Guid, RelationshipRecord>()); }
        }
        Dictionary<int, RelationshipRecord> FactionRecords
        {
            get { return m_Factions ?? (m_Factions = new Dictionary<int, RelationshipRecord>()); }
        }

        public IList<TraitInstance> Traits { get { return m_Traits.AsReadOnly(); } }
        public IList<MemoryInstance> Memories { get { return m_Memories.AsReadOnly(); } }
        public IList<ObservedEvent> Events { get { return m_Events.AsReadOnly(); } }
        public ICollection<RelationshipRecord> People { get { return PersonRecords.Values; } }
        public ICollection<RelationshipRecord> Groups { get { return GroupRecords.Values; } }
        public ICollection<RelationshipRecord> Factions { get { return FactionRecords.Values; } }

        public RelationshipRecord Person(Guid id)
        {
            RelationshipRecord record;
            return PersonRecords.TryGetValue(id, out record) ? record : null;
        }

        public RelationshipRecord Group(Guid id)
        {
            RelationshipRecord record;
            return GroupRecords.TryGetValue(id, out record) ? record : null;
        }

        public RelationshipRecord Faction(int id)
        {
            RelationshipRecord record;
            return FactionRecords.TryGetValue(id, out record) ? record : null;
        }

        public void RememberPerson(Guid id, string name, MemoryInstance memory, int change)
        {
            if (id == Guid.Empty || memory == null) return;
            RelationshipRecord record = Person(id);
            if (record == null) PersonRecords.Add(id, record = new RelationshipRecord(id, -1, name));
            record.Remember(memory, change);
        }

        public void RememberGroup(Guid id, string name, MemoryInstance memory, int change)
        {
            if (id == Guid.Empty || memory == null) return;
            RelationshipRecord record = Group(id);
            if (record == null) GroupRecords.Add(id, record = new RelationshipRecord(id, -1, name));
            record.Remember(memory, change);
        }

        public void RememberFaction(int id, string name, MemoryInstance memory, int change)
        {
            if (id < 0 || memory == null) return;
            RelationshipRecord record = Faction(id);
            if (record == null) FactionRecords.Add(id, record = new RelationshipRecord(Guid.Empty, id, name));
            record.Remember(memory, change);
        }

        public bool HasTrait(string id)
        {
            foreach (TraitInstance trait in m_Traits)
                if (trait.Id == id) return true;
            return false;
        }

        public bool AddTrait(TraitInstance trait)
        {
            if (trait == null || HasTrait(trait.Id)) return false;
            m_Traits.Add(trait);
            return true;
        }

        public bool AddMemory(MemoryInstance memory)
        {
            if (memory == null) return false;
            foreach (MemoryInstance existing in m_Memories)
                if (existing.Id == memory.Id &&
                    (existing.RelatedPersonId != Guid.Empty || memory.RelatedPersonId != Guid.Empty
                        ? existing.RelatedPersonId == memory.RelatedPersonId :
                    (existing.SubjectId != Guid.Empty || memory.SubjectId != Guid.Empty
                        ? existing.SubjectId == memory.SubjectId
                        : existing.Subject == memory.Subject)))
                    return false;
            m_Memories.Add(memory);
            return true;
        }

        public void RemoveMemory(MemoryInstance memory) { m_Memories.Remove(memory); }

        long m_LastObservedEventId;
        public bool Remember(ObservedEvent lifeEvent)
        {
            if (lifeEvent == null || (lifeEvent.EventId > 0 && lifeEvent.EventId <= m_LastObservedEventId)) return false;
            foreach (ObservedEvent old in m_Events)
                if (old.EventId != 0 && lifeEvent.EventId != 0 ? old.EventId == lifeEvent.EventId :
                    old.Kind == lifeEvent.Kind && old.Turn == lifeEvent.Turn &&
                    (old.SubjectId != Guid.Empty || lifeEvent.SubjectId != Guid.Empty
                        ? old.SubjectId == lifeEvent.SubjectId
                        : old.Subject == lifeEvent.Subject) &&
                    (old.OtherId != Guid.Empty || lifeEvent.OtherId != Guid.Empty
                        ? old.OtherId == lifeEvent.OtherId
                        : old.Other == lifeEvent.Other)) return false;
            m_Events.Add(lifeEvent);
            if (lifeEvent.EventId > 0) m_LastObservedEventId = lifeEvent.EventId;
            // Keep the journal bounded even when a district sees many deaths or raids.
            if (m_Events.Count > 32) m_Events.RemoveAt(0);
            return true;
        }
    }
}
