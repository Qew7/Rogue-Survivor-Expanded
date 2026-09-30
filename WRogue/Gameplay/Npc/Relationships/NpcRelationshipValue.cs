using System;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcRelationshipValue
    {
        // Callers choose observed membership or remembered membership explicitly.
        public static int Calculate(Actor owner, Guid personId, Guid groupId, int factionId, bool social, PersonalityRegistry registry = null,
            string excludeTrait = null)
        {
            if (owner == null || owner.Personality == null) return 0;
            registry = registry ?? PersonalitySystem.Registry;
            RelationshipRecord person = owner.Personality.Person(personId), group = owner.Personality.Group(groupId), faction = owner.Personality.Faction(factionId);
            int feeling = person == null ? 0 : person.Feeling;
            if (social && person != null) feeling += (person.Trust - person.Fear - person.Grievance) / 4;
            feeling += (group == null ? 0 : group.Feeling) + (faction == null ? 0 : faction.Feeling);
            if (factionId >= 0) foreach (TraitInstance trait in owner.Personality.Traits)
            {
                if (trait.Id == excludeTrait) continue;
                TraitDefinition definition = registry.Trait(trait.Id);
                if (definition != null && definition.RelationFactionId == factionId) feeling += definition.RelationBias;
            }
            return Math.Max(-100, Math.Min(100, feeling));
        }
    }
}
