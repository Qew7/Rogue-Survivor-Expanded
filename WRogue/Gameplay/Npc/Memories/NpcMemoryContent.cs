using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    static class NpcMemoryContent
    {
        public static void Received(NpcCatalogBuilder catalog, string id, string name, string kind, int feeling, string trait)
        {
            catalog.Memory(new MemoryDefinition(id, name, 2, 5,
                new[] { new MemoryTrigger(kind, (a, e) => a == e.Other) },
                new MemoryOutcome(null, trait, trait == null ? (Skills.IDs?)Skills.IDs.CHARISMATIC : null),
                new MemoryOutcome(null, null, Skills.IDs.STRONG_PSYCHE))
                .Relate(MemoryRelationRole.Subject, feeling, MemoryRelationRole.Subject));
        }
    }
}
