using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class PoliceEscortEmptyClockScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/police-escort-empty-clock", () => TownScenarioFactory.Arena(9157,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor officer = NpcIntentSupport.Actor(world, "officer", 1, 1);
            officer.Faction = world.Game.GameFactions.ThePolice;
            Actor formerFollower = NpcIntentSupport.Actor(world, "former follower", 2, 1);
            officer.AddFollower(formerFollower);
            officer.RemoveFollower(formerFollower);
            Check.Equal(null, officer.Followers, "last follower leaving clears the collection");

            NpcIntentSystem.AdvanceClock(world.Game, world.Map);
            Check.Equal(null, formerFollower.Leader, "map clock leaves the former follower independent");
            Check.Equal(0, officer.CountFollowers, "map clock accepts an officer with no followers");
        });
    }
}
