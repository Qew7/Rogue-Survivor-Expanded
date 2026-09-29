using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
namespace djack.RogueSurvivor.Gameplay.Personality
{
    sealed partial class KnowledgeModule : INpcContentModule
    {
        public string Id { get { return "knowledge"; } }
        public void Register(NpcCatalogBuilder catalog)
        {
            catalog.Memory(new MemoryDefinition("heard_a_report", "Was told a report about events", 2, 5,
                new[] { new MemoryTrigger("rumor_shared", (a, e) => a == e.Other) },
                new MemoryOutcome(null, "mistrustful", null), new MemoryOutcome(null, null, Skills.IDs.CHARISMATIC))
                .Relate(MemoryRelationRole.Subject, 0), false);
            RegisterEvents(catalog);
        }
        void RegisterEvents(NpcCatalogBuilder catalog)
        {
            catalog.Event(new NpcEventDefinition("rumor_shared", NpcRecordCategory.None, false, e => (e.Subject ?? "Someone") + " told " + (e.Other ?? "someone") + " a report about an earlier event.", null));
            catalog.Event(new NpcEventDefinition("person_location", NpcRecordCategory.None, false, null, null));
        }
    }
}
