using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class StoryGroupShelterScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/story-group-shelter", () => TownScenarioFactory.Arena(4626, "......", "......", "......"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 5, 2);
            Actor leader = NpcIntentSupport.Actor(world, "leader", 2, 2, "loyal");
            Actor follower = NpcIntentSupport.Actor(world, "follower", 0, 2, "loyal");
            for (int x = 2; x <= 3; x++) for (int y = 1; y <= 2; y++) world.Map.GetTileAt(x, y).IsInside = true;
            NpcIntentSupport.Turn(world, leader); world.Place(leader, 0, 1); leader.AddFollower(follower);
            Actor victim = NpcIntentSupport.Actor(world, "victim", 3, 0);
            Actor attacker = NpcIntentSupport.Actor(world, "attacker", 3, 1);
            PersonalitySystem.Report(world.Game, new SignificantEvent("attack", victim, attacker, world.Map, victim.Location.Position, 0));
            NpcIntentSupport.Turn(world, leader);
            NpcGroupPlan plan = leader.SocialGroup.Plan;
            Check.Equal("group_shelter", plan.Kind, "known danger motivates a shelter plan");
            NpcIntent own = NpcIntentSupport.Intent(leader, "seek_group_shelter"), other = NpcIntentSupport.Intent(follower, "seek_group_shelter");
            Check.Equal(true, own != null && other != null, "participants independently accept the proposal");
            for (int i = 0; i < 10 && (!own.Finished || !other.Finished); i++)
            {
                world.Map.LocalTime.TurnCounter = i + 1;
                if (!own.Finished) NpcIntentSupport.Turn(world, leader);
                if (!other.Finished) NpcIntentSupport.Turn(world, follower);
            }
            Check.Equal(NpcIntentStatus.Completed, own.Status, "leader reaches actual indoor shelter");
            Check.Equal(NpcIntentStatus.Completed, other.Status, "follower can occupy another indoor tile");
            Check.Equal(true, world.Map.GetTileAt(leader.Location.Position).IsInside && world.Map.GetTileAt(follower.Location.Position).IsInside, "shelter outcome reflects map state");
            Check.Equal("completed", Session.Get.NpcDirector.Find(plan.StoryId).Stage, "story completes when accepted roles arrive");
        });
    }
}
