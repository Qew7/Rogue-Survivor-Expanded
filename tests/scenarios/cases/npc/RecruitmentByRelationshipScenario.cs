using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;
using djack.RogueSurvivor.Gameplay.Personality;

static class RecruitmentByRelationshipScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/recruitment-by-relationship", () => TownScenarioFactory.Arena(4822,
            "........", "........", "........"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 7, 2);
            Actor leader = NpcIntentSupport.Actor(world, "leader", 1, 1);
            Actor disliked = NpcIntentSupport.Actor(world, "disliked", 2, 1);
            Actor neutral = NpcIntentSupport.Actor(world, "neutral", 1, 2);
            Actor liked = NpcIntentSupport.Actor(world, "liked", 2, 2);
            world.Game.SkillUpgrade(leader, Skills.IDs.LEADERSHIP);
            leader.Personality.Opinion(disliked.PersonalityIdentity, disliked.UnmodifiedName).Feeling = -40;
            leader.Personality.Opinion(liked.PersonalityIdentity, liked.UnmodifiedName).Feeling = 40;
            Check.Equal(false, world.Game.Rules.CanActorTakeLead(leader, disliked), "disliked neighbor is ineligible");
            Check.Equal(true, world.Game.Rules.CanActorTakeLead(leader, neutral), "neutral neighbor is eligible");
            Check.Equal(true, world.Game.Rules.CanActorTakeLead(leader, liked), "liked neighbor is eligible");

            NpcIntentSupport.Turn(world, leader);
            Check.Same(leader, liked.Leader, "real controller invites the best-liked eligible visible actor");
            Check.Equal(null, neutral.Leader, "nearer neutral actor was not selected first");
            Check.Equal(null, disliked.Leader, "nearest ineligible actor did not block recruitment");
        });
    }
}
