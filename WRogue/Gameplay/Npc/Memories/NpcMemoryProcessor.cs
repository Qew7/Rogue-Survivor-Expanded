using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed class NpcMemoryRelations
    {
        public Guid Person, Group;
        public string PersonName, GroupName, FactionName;
        public int Faction = -1;
        public static NpcMemoryRelations Observed(Actor person, Actor group)
        {
            var result = new NpcMemoryRelations();
            if (person != null)
            {
                result.Person = person.PersonalityIdentity; result.PersonName = person.UnmodifiedName;
                if (person.Faction != null) { result.Faction = person.Faction.ID; result.FactionName = person.Faction.Name; }
                if (group == null) group = person.HasLeader ? person.Leader : person.CountFollowers > 0 ? person : null;
            }
            if (group != null)
            { result.Group = group.SocialGroup == null ? group.PersonalityIdentity : group.SocialGroup.Identity;
                result.GroupName = group.SocialGroup == null ? group.UnmodifiedName : group.SocialGroup.LeaderName; }
            return result;
        }
    }

    static class NpcMemoryProcessor
    {
        public static void Add(Actor actor, MemoryDefinition definition, int turn, string subject, DiceRoller dice,
            bool relatedToSubject = false, Guid subjectId = default(Guid), NpcMemoryRelations relations = null, int impact = 0)
        {
            int days = dice == null ? definition.MinDays : dice.Roll(definition.MinDays, definition.MaxDays + 1);
            MemoryInstance memory = new MemoryInstance(definition.Id, turn, turn + days * WorldTime.TURNS_PER_DAY,
                subject, relatedToSubject, subjectId, relations == null ? Guid.Empty : relations.Person);
            if (!actor.Personality.AddMemory(memory)) return;
            if (relations != null)
            {
                if (relations.Person != Guid.Empty) actor.Personality.RememberPerson(relations.Person, relations.PersonName, memory, impact);
                if (relations.Group != Guid.Empty) actor.Personality.RememberGroup(relations.Group, relations.GroupName, memory, impact / 3);
                if (definition.RelationFactionId < 0 && relations.Faction >= 0 && relations.Faction != (int)GameFactions.IDs.TheCivilians)
                    actor.Personality.RememberFaction(relations.Faction, relations.FactionName, memory, impact / 4);
            }
            if (definition.RelationFactionId >= 0) actor.Personality.RememberFaction(definition.RelationFactionId,
                Models.Factions[definition.RelationFactionId].Name, memory, definition.FactionFeelingChange);
            if (actor.Location.Map != null) Session.Get.ResidentRecords.MemoryStarted(actor, memory, definition);
        }
        public static void Evidence(Actor owner, PersonalityRegistry registry, string kind, int turn)
        {
            foreach (MemoryInstance memory in owner.Personality.Memories)
            {
                MemoryDefinition definition = registry.Memory(memory.Id);
                if (definition != null && turn >= memory.StartTurn && definition.TracksEvidence(kind)) memory.RememberEvidence(kind, turn);
            }
        }
    }
}
