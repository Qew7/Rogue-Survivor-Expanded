using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class SkillToughScenario
{
    public static void Register()
    {
        SkillScenario.Register("tough", Skills.IDs.TOUGH, (w, a) => w.Game.Rules.ActorMaxHPs(a));
    }
}
