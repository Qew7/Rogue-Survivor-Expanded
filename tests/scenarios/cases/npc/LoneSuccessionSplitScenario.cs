using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class LoneSuccessionSplitScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/lone-succession-split", () => TownScenarioFactory.Arena(4863,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor leader = NpcIntentSupport.Actor(world, "leader", 1, 1, "loyal");
            Actor successor = NpcIntentSupport.Actor(world, "successor", 2, 1, "loyal", "organized");
            Actor supporter = NpcIntentSupport.Actor(world, "supporter", 3, 1, "loyal");
            Actor rival = NpcIntentSupport.Actor(world, "rival", 1, 2, "solitary");
            Actor bystander = NpcIntentSupport.Actor(world, "bystander", 3, 2);
            leader.AddFollower(successor); leader.AddFollower(supporter); leader.AddFollower(rival);
            SocialGroup original = leader.SocialGroup;
            rival.SetTrustIn(successor, -100);
            world.Game.KillActor(null, leader, "scenario", false);

            Check.Equal(true, rival.SocialGroup != null && rival.SocialGroup != original &&
                rival.SocialGroup.Members.Contains(rival.PersonalityIdentity), "lone dissenter forms its own group");
            Check.Equal(true, rival.Personality.Memories.Any(m => m.Id == "left_after_succession"),
                "lone dissenter remembers leaving");
            Check.Equal(false, bystander.Personality.Memories.Any(m => m.Id == "saw_group_split"),
                "groupless bystander is not mistaken for a member of the rival group");
        });
    }
}
