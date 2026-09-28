using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class PersonalityAnimalKillScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-animal-kill", () => TownScenarioFactory.Arena(4535,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Session.Get.UniqueActors.TheSewersThing = new UniqueActor();
            Actor player = SkillScenario.Actor(world);
            world.SetPlayer(player);
            Actor killer = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "killer", false, false, 0);
            killer.Personality = new PersonalityState();
            Actor dog = new Actor(world.Game.GameActors.FeralDog,
                world.Game.GameFactions.TheFerals, "dog", false, false, 0);
            Actor person = new Actor(world.Game.GameActors.FemaleCivilian,
                world.Game.GameFactions.TheCivilians, "person", false, false, 0);
            world.Place(killer, 1, 1);
            world.Place(dog, 2, 1);
            world.Place(person, 3, 1);

            world.Game.KillActor(killer, dog, "scenario", false);
            Check.Equal(0, killer.Personality.Memories.Count,
                "killing a living animal does not create a killed-person memory");
            world.Game.KillActor(killer, person, "scenario", false);
            Check.Equal(1, killer.Personality.Memories.Count,
                "killing a living person creates the memory");
            Check.Equal("killed_person", killer.Personality.Memories[0].Id,
                "the new memory identifies a human killing");
        });
    }
}
