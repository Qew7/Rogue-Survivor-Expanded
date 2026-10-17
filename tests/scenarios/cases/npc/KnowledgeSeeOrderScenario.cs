using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class KnowledgeSeeOrderScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/knowledge-see-order", () => TownScenarioFactory.Arena(8149,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor observer = NpcIntentSupport.Actor(world, "observer", 0, 1);
            Actor first = NpcIntentSupport.Actor(world, "first", 1, 1);
            Actor second = NpcIntentSupport.Actor(world, "second", 2, 1);
            Actor newcomer = NpcIntentSupport.Actor(world, "newcomer", 3, 1);
            NpcKnowledge knowledge = observer.Personality.Knowledge;
            NpcKnownPerson firstRecord = knowledge.See(first, 1);
            NpcKnownPerson secondRecord = knowledge.See(second, 1);
            int revision = knowledge.Revision;

            Check.Same(firstRecord, knowledge.See(first, 2), "seeing a familiar person retains their record");
            Check.Equal(revision, knowledge.Revision, "unchanged observation does not revise knowledge");
            Check.Same(secondRecord, knowledge.People[0], "other person becomes the oldest record");
            Check.Same(firstRecord, knowledge.People[1], "refreshed person moves to the end");

            for (int i = 1; i <= 30; i++)
                knowledge.People.Add(new NpcKnownPerson { Id = new Guid(i, 0, 0, new byte[8]) });
            Check.Same(secondRecord, knowledge.See(second, 3), "oldest record can be refreshed at capacity");
            knowledge.See(newcomer, 4);
            Check.Equal(32, knowledge.People.Count, "capacity stays bounded");
            Check.Equal(null, knowledge.Person(first.PersonalityIdentity), "oldest unrefreshed person is evicted");
            Check.Same(secondRecord, knowledge.Person(second.PersonalityIdentity), "refreshed record survives eviction");
        });
    }
}
