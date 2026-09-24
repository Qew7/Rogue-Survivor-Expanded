using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class SkillLeadershipScenario
{
    public static void Register()
    {
        SkillScenario.Register("leadership", Skills.IDs.LEADERSHIP, (w, a) => w.Game.Rules.ActorMaxFollowers(a));
    }
}
