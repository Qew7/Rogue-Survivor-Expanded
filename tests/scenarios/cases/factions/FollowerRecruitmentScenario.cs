using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay;

static class FollowerRecruitmentScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("factions/recruitment", () => TownScenarioFactory.Arena(4313,
            ".....", ".....", "....."), world =>
        {
            Actor leader = SkillScenario.Actor(world);
            world.Map.PlaceActorAt(leader, new Point(1, 1));
            world.SetPlayer(leader);
            Actor recruit = SkillScenario.Actor(world);
            world.Map.PlaceActorAt(recruit, new Point(2, 1));
            Check.Equal(false, world.Game.Rules.CanActorTakeLead(leader, recruit),
                "no leadership capacity before upgrade");
            world.Game.SkillUpgrade(leader, Skills.IDs.LEADERSHIP);
            Check.Equal(true, world.Try(new ActionTakeLead(leader, world.Game, recruit)),
                "leader can recruit friendly civilian");
            Check.Same(leader, recruit.Leader, "recruit follows leader");
            Check.Equal(1, leader.CountFollowers, "leader has one follower");
        });
    }
}
