using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    enum DecisionKind { Item, Courage, Group, Law, Trade, Explore, Compassion, Supplies }

    // Definitions and effects are registered once; instances keep only IDs and parameters.
    sealed class TraitEffect
    {
        public readonly DecisionKind Decision;
        public readonly int Amount;
        public readonly Func<Actor, Item, bool> Applies;

        public TraitEffect(DecisionKind decision, int amount, Func<Actor, Item, bool> applies = null)
        {
            Decision = decision;
            Amount = amount;
            Applies = applies;
        }
    }

    sealed class TraitDefinition
    {
        public readonly string Id;
        public readonly string Name;
        public readonly bool Advanced;
        public readonly bool ItemParameter;
        public readonly string RequiresTrait;
        public readonly TraitEffect[] Effects;
        public int RelationFactionId { get; private set; } = -1;
        public int RelationBias { get; private set; }
        readonly List<string> m_Conflicts = new List<string>();

        public TraitDefinition(string id, string name, bool advanced, bool itemParameter,
            string requiresTrait, params TraitEffect[] effects)
        {
            Id = id;
            Name = name;
            Advanced = advanced;
            ItemParameter = itemParameter;
            RequiresTrait = requiresTrait;
            Effects = effects;
        }

        public bool Eligible(Actor actor)
        {
            if (actor.Personality == null || actor.Personality.HasTrait(Id) ||
                (RequiresTrait != null && !actor.Personality.HasTrait(RequiresTrait))) return false;
            foreach (string conflict in m_Conflicts)
                if (actor.Personality.HasTrait(conflict)) return false;
            return true;
        }

        public void AddConflict(string id) { m_Conflicts.Add(id); }
        public TraitDefinition TowardFaction(int id, int amount)
        { RelationFactionId = id; RelationBias = amount; return this; }
    }

    sealed class SignificantEvent
    {
        public readonly string Kind;
        public readonly Actor Subject;
        public readonly bool SubjectIsDirect;
        public readonly Actor Other;
        public readonly bool OtherIsDirect;
        public readonly Map Map;
        public readonly System.Drawing.Point Position;
        public readonly int Turn;

        public SignificantEvent(string kind, Actor subject, Actor other, Map map,
            System.Drawing.Point position, int turn, bool otherIsDirect = true,
            bool subjectIsDirect = true)
        {
            Kind = kind;
            Subject = subject;
            SubjectIsDirect = subjectIsDirect;
            Other = other;
            OtherIsDirect = otherIsDirect;
            Map = map;
            Position = position;
            Turn = turn;
        }
    }

    sealed class MemoryTrigger
    {
        public readonly string EventKind;
        public readonly Func<Actor, SignificantEvent, bool> Applies;

        public MemoryTrigger(string eventKind, Func<Actor, SignificantEvent, bool> applies)
        {
            EventKind = eventKind;
            Applies = applies;
        }
    }

    sealed class MemoryOutcome
    {
        public readonly Func<Actor, MemoryInstance, bool> Applies;
        public readonly string TraitId;
        public readonly Skills.IDs? SkillId;

        public MemoryOutcome(Func<Actor, MemoryInstance, bool> applies, string traitId, Skills.IDs? skillId)
        {
            Applies = applies;
            TraitId = traitId;
            SkillId = skillId;
        }
    }

    enum MemoryRelationRole { None, Subject, Other, OtherOrSubject }

    sealed class MemoryDefinition
    {
        public readonly string Id;
        public readonly string Name;
        public readonly int MinDays;
        public readonly int MaxDays;
        public readonly MemoryTrigger[] Triggers;
        public readonly string[] EvidenceKinds;
        public readonly MemoryOutcome[] Outcomes;
        public MemoryRelationRole PersonRole { get; private set; }
        public MemoryRelationRole GroupRole { get; private set; }
        public int FeelingChange { get; private set; }
        public int FallbackFeelingChange { get; private set; }
        public int RelationFactionId { get; private set; } = -1;
        public int FactionFeelingChange { get; private set; }
        public bool OncePerPerson { get; private set; }

        public MemoryDefinition(string id, string name, int minDays, int maxDays,
            MemoryTrigger[] triggers, params MemoryOutcome[] outcomes)
            : this(id, name, minDays, maxDays, triggers, new string[0], outcomes)
        {
        }

        public MemoryDefinition(string id, string name, int minDays, int maxDays,
            MemoryTrigger[] triggers, string[] evidenceKinds, params MemoryOutcome[] outcomes)
        {
            Id = id;
            Name = name;
            MinDays = minDays;
            MaxDays = maxDays;
            Triggers = triggers;
            EvidenceKinds = evidenceKinds;
            Outcomes = outcomes;
        }

        public MemoryDefinition Relate(MemoryRelationRole person, int feeling,
            MemoryRelationRole group = MemoryRelationRole.None, int? fallbackFeeling = null)
        {
            PersonRole = person;
            GroupRole = group;
            FeelingChange = feeling;
            FallbackFeelingChange = fallbackFeeling ?? feeling;
            return this;
        }

        public bool TracksEvidence(string kind)
        {
            foreach (string tracked in EvidenceKinds)
                if (tracked == kind) return true;
            return false;
        }
        public MemoryDefinition TowardFaction(int id, int amount)
        { RelationFactionId = id; FactionFeelingChange = amount; return this; }
        public MemoryDefinition FirstEncounter()
        { OncePerPerson = true; return this; }
    }

    sealed class PersonalityRegistry
    {
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
