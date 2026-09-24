using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class SkillZToughScenario
{
    public static void Register()
    {
        SkillScenario.Register("z-tough", Skills.IDs.Z_TOUGH, (w, a) => w.Game.Rules.ActorMaxHPs(a), true, false);
    }
}
