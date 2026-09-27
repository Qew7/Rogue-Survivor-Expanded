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

        public ObservedEvent(string kind, int turn, string subject, string other, bool direct)
        {
            Kind = kind;
            Turn = turn;
            Subject = subject;
            Other = other;
            Direct = direct;
        }
    }

    [Serializable]
    sealed class MemoryInstance
    {
        public readonly string Id;
        public readonly int StartTurn;
        public readonly int ResolveTurn;
        public readonly string Subject;

        public MemoryInstance(string id, int startTurn, int resolveTurn, string subject)
        {
            Id = id;
            StartTurn = startTurn;
            ResolveTurn = resolveTurn;
            Subject = subject;
        }
    }

    // This is the only new per-actor object in the saved world graph.
    [Serializable]
    sealed class PersonalityState
    {
        readonly List<TraitInstance> m_Traits = new List<TraitInstance>(4);
        readonly List<MemoryInstance> m_Memories = new List<MemoryInstance>(2);
        readonly List<ObservedEvent> m_Events = new List<ObservedEvent>(8);

        public IList<TraitInstance> Traits { get { return m_Traits.AsReadOnly(); } }
        public IList<MemoryInstance> Memories { get { return m_Memories.AsReadOnly(); } }
        public IList<ObservedEvent> Events { get { return m_Events.AsReadOnly(); } }

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
                if (existing.Id == memory.Id && existing.Subject == memory.Subject)
                    return false;
            m_Memories.Add(memory);
            return true;
        }

        public void RemoveMemory(MemoryInstance memory) { m_Memories.Remove(memory); }

        public void Remember(ObservedEvent lifeEvent)
        {
            if (lifeEvent == null) return;
            foreach (ObservedEvent old in m_Events)
                if (old.Kind == lifeEvent.Kind && old.Turn == lifeEvent.Turn &&
                    old.Subject == lifeEvent.Subject && old.Other == lifeEvent.Other) return;
            m_Events.Add(lifeEvent);
            // Keep the journal bounded even when a district sees many deaths or raids.
            if (m_Events.Count > 32) m_Events.RemoveAt(0);
        }
    }
}
