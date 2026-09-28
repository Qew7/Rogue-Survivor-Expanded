using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class IntentDepartureScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/intent-departure", () => TownScenarioFactory.Arena(4603, ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Session.Get.UniqueActors.TheSewersThing = new UniqueActor();
            NpcIntentSupport.Player(world, 6, 2);
            Actor leader = NpcIntentSupport.Actor(world, "leader", 2, 1, "protective");
            Actor independent = NpcIntentSupport.Actor(world, "independent", 1, 1, "independent", "solitary");
            Actor loyal = NpcIntentSupport.Actor(world, "loyal", 3, 1, "loyal");
            Actor victim = NpcIntentSupport.Actor(world, "victim", 2, 0);
            leader.AddFollower(independent); leader.AddFollower(loyal);
            world.Game.KillActor(leader, victim, "scenario", false);
            NpcIntent departure = NpcIntentSupport.Intent(independent, "leave_unsafe_group");
            Check.Equal(true, departure != null, "witnessed leader violence creates a trait-based departure goal");
            Check.Equal(null, NpcIntentSupport.Intent(loyal, "leave_unsafe_group"), "loyal follower reacts differently");
            NpcIntentSupport.Turn(world, independent);
            Check.Equal(null, independent.Leader, "actual AI action ends membership");
            Check.Equal(1, leader.CountFollowers, "other follower stays in the group");
            Check.Equal(NpcIntentStatus.Completed, departure.Status, "departure completes after membership changes");
            Check.Equal(true, NpcIntentSupport.HasEvent(leader, "left_group"), "leader experiences the voluntary departure");
            MemoryInstance memory = null;
            foreach (MemoryInstance m in independent.Personality.Memories) if (m.Id == "left_unsafe_group") memory = m;
            Check.Equal(true, memory != null, "leaving has a distinct personal memory");
            world.Map.LocalTime.TurnCounter = memory.ResolveTurn;
            djack.RogueSurvivor.Gameplay.Personality.PersonalitySystem.ResolveDue(world.Game, world.Map);
            Check.Equal(true, independent.Personality.HasTrait("hermit"), "solitary leaver develops the eligible memory outcome");
            Check.Equal(true, independent.Personality.Person(leader.PersonalityIdentity).Memories.Contains(memory), "resolved departure stays in personal history");
        });
    }
}
