using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class PersonalityEvidenceOverflowScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/personality-evidence-overflow", () => TownScenarioFactory.Arena(4537,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Session.Get.UniqueActors.TheSewersThing = new UniqueActor();
            Actor player = SkillScenario.Actor(world);
            world.SetPlayer(player);
            Actor leader = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "leader", false, false, 0);
            Actor follower = new Actor(world.Game.GameActors.FemaleCivilian,
                world.Game.GameFactions.TheCivilians, "follower", false, false, 0);
            follower.Personality = new PersonalityState();
            follower.Personality.AddTrait(new TraitInstance("vindictive"));
            Actor killer = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "killer", false, false, 0);
            world.Place(leader, 1, 1);
            world.Place(follower, 2, 1);
            world.Place(killer, 1, 2);
            leader.AddFollower(follower);

            world.Game.KillActor(killer, leader, "scenario", false);
            Check.Equal("leader_loss", follower.Personality.Memories[0].Id,
                "murder of a leader creates a loss memory");
            for (int turn = 1; turn <= 40; turn++)
                PersonalitySystem.Report(world.Game, new SignificantEvent("raid", null, null,
                    world.Map, follower.Location.Position, turn));
            Check.Equal(32, follower.Personality.Events.Count,
                "the murder has left the bounded event journal");
            Check.Equal("raid", follower.Personality.Events[0].Kind,
                "all retained journal entries are later events");

            world.Map.LocalTime.TurnCounter = follower.Personality.Memories[0].ResolveTurn;
            PersonalitySystem.ResolveDue(world.Game, world.Map);
            Check.Equal(true, follower.Personality.HasTrait("vengeful"),
                "the pending memory retains evidence needed for its outcome");
        });
    }
}
