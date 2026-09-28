using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class PersonalityIdentityScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-identity", () => TownScenarioFactory.Arena(4536,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Session.Get.UniqueActors.TheSewersThing = new UniqueActor();
            Actor player = SkillScenario.Actor(world);
            world.SetPlayer(player);
            Actor leader = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "leader", false, false, 0);
            leader.Personality = new PersonalityState();
            Actor first = new Actor(world.Game.GameActors.FemaleCivilian,
                world.Game.GameFactions.TheCivilians, "Alex", true, false, 0);
            Actor second = new Actor(world.Game.GameActors.FemaleCivilian,
                world.Game.GameFactions.TheCivilians, "Alex", true, false, 0);
            Actor unrelated = new Actor(world.Game.GameActors.FemaleCivilian,
                world.Game.GameFactions.TheCivilians, "Alex", true, false, 0);
            world.Place(leader, 1, 1);
            world.Place(first, 2, 1);
            world.Place(second, 2, 2);
            world.Place(unrelated, 3, 1);
            leader.AddFollower(first);
            leader.AddFollower(second);

            world.Game.KillActor(null, first, "scenario", false);
            world.Game.KillActor(null, second, "scenario", false);
            Check.Equal(2, leader.Personality.Memories.Count,
                "two followers sharing a name create distinct loss memories");
            Check.Equal(2, leader.Personality.Events.Count,
                "same-turn deaths of namesakes remain separate observations");
            Check.Equal(false, leader.Personality.Memories[0].SubjectId ==
                leader.Personality.Memories[1].SubjectId,
                "loss memories retain distinct actor identities");

            world.Game.KillActor(null, unrelated, "scenario", false);
            Check.Call(world.Game, "Zombify",
                new[] { typeof(Actor), typeof(Actor), typeof(bool) }, null, unrelated, false);
            Check.Equal(2, leader.Personality.Memories.Count,
                "a stranger with a companion's name is not mistaken for a friend");

            Check.Call(world.Game, "Zombify",
                new[] { typeof(Actor), typeof(Actor), typeof(bool) }, null, first, false);
            Check.Equal(3, leader.Personality.Memories.Count,
                "the actual former follower still creates a zombification memory");
            Check.Equal("zombified_friend", leader.Personality.Memories[2].Id,
                "the companion memory is attached to the right actor");
        });
    }
}
