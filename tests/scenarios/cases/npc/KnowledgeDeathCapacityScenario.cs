using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class KnowledgeDeathCapacityScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/knowledge-death-capacity", () => TownScenarioFactory.Arena(4822,
            "......", "......", "......"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Session.Get.UniqueActors.TheSewersThing = new UniqueActor();
            Actor player = NpcIntentSupport.Player(world, 0, 1);
            Actor victim = NpcIntentSupport.Actor(world, "victim", 1, 1);
            Actor witness = NpcIntentSupport.Actor(world, "witness", 3, 1);
            Actor killer = new Actor(world.Game.GameActors.Zombie,
                world.Game.GameFactions.TheUndeads, "killer", false, false, 0);
            world.Place(killer, 2, 1);

            NpcKnowledge knowledge = player.Personality.Knowledge;
            knowledge.People.Add(new NpcKnownPerson { Id = victim.PersonalityIdentity });
            for (int i = 1; i < 32; i++)
                knowledge.People.Add(new NpcKnownPerson { Id = new Guid(i, 0, 0, new byte[8]) });
            Check.Equal(32, knowledge.People.Count, "the victim is the oldest known person at capacity");

            world.Game.KillActor(killer, victim, "scenario", false);
            NpcKnownPerson knownVictim = knowledge.Person(victim.PersonalityIdentity);
            NpcKnownPerson knownKiller = knowledge.Person(killer.PersonalityIdentity);
            Check.Equal(true, knownVictim != null && knownVictim.Dead,
                "the observed death keeps the victim and marks them dead");
            Check.Equal(true, knownKiller != null && knownKiller.Hostile,
                "the newly seen killer remains known as hostile");
            Check.Equal(32, knowledge.People.Count, "observations keep the knowledge capacity bound");
            Check.Equal(true, witness.Personality.Knowledge.Person(victim.PersonalityIdentity).Dead,
                "a nearby NPC also learns the death");

            Actor accident = NpcIntentSupport.Actor(world, "accident victim", 1, 2);
            world.Game.KillActor(null, accident, "scenario", false);
            Check.Equal(true, knowledge.Person(accident.PersonalityIdentity).Dead,
                "a death without a killer remains observable");
        });
    }
}
