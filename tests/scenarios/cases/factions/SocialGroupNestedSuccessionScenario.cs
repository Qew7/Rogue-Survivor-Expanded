using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class SocialGroupNestedSuccessionScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("factions/social-group-nested-succession", () => TownScenarioFactory.Arena(4820,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 8, 2);
            Actor founder = NpcIntentSupport.Actor(world, "founder", 1, 1);
            Actor sibling = NpcIntentSupport.Actor(world, "sibling", 2, 1);
            Actor branch = NpcIntentSupport.Actor(world, "branch", 3, 1);
            Actor successor = NpcIntentSupport.Actor(world, "successor", 4, 1, "loyal", "organized");
            Actor member = NpcIntentSupport.Actor(world, "member", 5, 1, "loyal");
            founder.AddFollower(sibling); founder.AddFollower(branch);
            branch.AddFollower(successor); branch.AddFollower(member);
            SocialGroup original = founder.SocialGroup;
            world.Game.KillActor(null, branch, "scenario", false);
            Check.Same(original, founder.SocialGroup, "root keeps its group after nested leader dies");
            Check.Same(original, sibling.SocialGroup, "unrelated branch keeps original identity");
            Check.Equal(founder.PersonalityIdentity, original.LeaderId, "nested succession cannot replace root leader");
            Check.Equal(true, original.Members.Contains(founder.PersonalityIdentity) && original.Members.Contains(sibling.PersonalityIdentity),
                "root and sibling remain in the original membership list");
            Check.Equal(false, original.Members.Contains(branch.PersonalityIdentity) || original.Members.Contains(successor.PersonalityIdentity),
                "detached branch is not left in the old group");
            Check.Same(successor, member.Leader, "eligible child leads the detached branch");
            Check.Same(successor.SocialGroup, member.SocialGroup, "branch members share their successor's group");
            Check.Equal(false, successor.SocialGroup.Identity == original.Identity, "new branch has a distinct identity");
            Check.Equal(successor.PersonalityIdentity, successor.SocialGroup.LeaderId, "branch leader ID is coherent");
        });
    }
}
