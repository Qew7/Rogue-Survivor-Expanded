using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

namespace djack.RogueSurvivor.Gameplay.Personality
{
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
}
