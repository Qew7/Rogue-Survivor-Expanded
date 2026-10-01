using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;
static class GroupExpulsionScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/group-expulsion", () => TownScenarioFactory.Arena(4805,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 6, 2);
            Actor leader = NpcIntentSupport.Actor(world, "leader", 1, 1, "loyal", "lawful", "brave");
            Actor follower = NpcIntentSupport.Actor(world, "follower", 2, 1);
            leader.AddFollower(follower); SocialGroup group = leader.SocialGroup;
            leader.Personality.Knowledge.See(follower, 0);
            NpcKnownPerson known = leader.Personality.Knowledge.Person(follower.PersonalityIdentity);
            known.Violation = known.ViolationConfidence = 100; known.ViolationCause = 21;
            NpcIntentSupport.Turn(world, leader);
            Check.Equal(null, follower.Leader, "expulsion changes actual group membership");
            Check.Equal(false, group.Members.Contains(follower.PersonalityIdentity), "former member leaves the stable group identity");
            Check.Equal(true, NpcIntentSupport.HasEvent(follower, "member_expelled"), "both parties can remember the actual exclusion");
            NpcIntent goal = NpcIntentSupport.Intent(leader, "expel_member");
            Check.Equal(NpcIntentStatus.Completed, goal.Status, "completed goal follows actual membership change");
            Actor outsider = NpcIntentSupport.Actor(world, "outsider", 3, 1);
            var target = new NpcKnownPerson { Id = outsider.PersonalityIdentity, Name = outsider.UnmodifiedName, Place = outsider.Location };
            NpcIntent invalid = NpcStorySystem.StartKnown(world.Game.NpcContent, leader, target, world.Game.NpcContent.Capability("expel_member"));
            Check.Equal(true, invalid == null ||
                !new djack.RogueSurvivor.Engine.Actions.ActionNpcIntent(leader, world.Game, invalid, outsider).IsLegal(),
                "an unrelated person cannot be expelled from a group");
        });
    }
}
