using System;
using System.Collections.Generic;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed class NpcSituationDefinition
    {
        public readonly string Id;
        public readonly int Confidence, MaxAge;
        public readonly NpcIntentDefinition[] Methods;
        public NpcSituationDefinition(string id, int confidence, int age, params NpcIntentDefinition[] methods)
        { Id = id; Confidence = confidence; MaxAge = age; Methods = methods; }
    }
    static class NpcStoryContent
    {
        static readonly Dictionary<string, NpcSituationDefinition> byFact = new Dictionary<string, NpcSituationDefinition>
        {
            { "attack", new NpcSituationDefinition("reported_violence", 40, 180, NpcIntentContent.Avoid, NpcIntentContent.Confront) },
            { "murder", new NpcSituationDefinition("reported_murder", 40, 180, NpcIntentContent.Avoid, NpcIntentContent.Confront) }
        };
        public static NpcSituationDefinition ForFact(string kind)
        { NpcSituationDefinition definition; return byFact.TryGetValue(kind, out definition) ? definition : null; }
        public static void RegisterMemories(PersonalityRegistry registry)
        {
            registry.Register(new MemoryDefinition("heard_a_report", "Was told a report about events", 2, 5,
                new[] { new MemoryTrigger("rumor_shared", (a, e) => a == e.Other) },
                new MemoryOutcome(null, "mistrustful", null), new MemoryOutcome(null, null, Skills.IDs.CHARISMATIC))
                .Relate(MemoryRelationRole.Subject, 0), false);
            registry.Register(new MemoryDefinition("found_a_companion", "Found a missing companion", 2, 5,
                new[] { new MemoryTrigger("reunited", (a, e) => a == e.Subject) },
                new MemoryOutcome(null, "protector", null), new MemoryOutcome(null, null, Skills.IDs.LEADERSHIP))
                .Relate(MemoryRelationRole.Other, 5, MemoryRelationRole.Other), false);
            registry.Register(new MemoryDefinition("completed_group_delivery", "Completed a group supply mission", 2, 5,
                new[] { new MemoryTrigger("supplies_delivered", (a, e) => a == e.Subject) },
                new MemoryOutcome(null, "selfless", null), new MemoryOutcome(null, null, Skills.IDs.LEADERSHIP))
                .Relate(MemoryRelationRole.Other, 5, MemoryRelationRole.Other), false);
            registry.Register(new MemoryDefinition("reached_group_shelter", "Reached the group's shelter", 2, 5,
                new[] { new MemoryTrigger("shelter_reached", (a, e) => a == e.Subject) },
                new MemoryOutcome(null, "protector", null), new MemoryOutcome(null, null, Skills.IDs.STRONG_PSYCHE))
                .Relate(MemoryRelationRole.None, 0, MemoryRelationRole.Subject), false);
            registry.Register(new MemoryDefinition("witnessed_group_succession", "Witnessed a new group leader", 2, 5,
                new[] { new MemoryTrigger("group_succession", (a, e) => a.SocialGroup != null && e.Subject != null && a.SocialGroup == e.Subject.SocialGroup) },
                new MemoryOutcome(null, "protector", null), new MemoryOutcome(null, null, Skills.IDs.LEADERSHIP))
                .Relate(MemoryRelationRole.Subject, 0, MemoryRelationRole.Subject), false);
        }
    }
}
