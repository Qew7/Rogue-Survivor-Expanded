using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class SkillZEaterScenario
{
    public static void Register()
    {
        SkillScenario.Register("z-eater", Skills.IDs.Z_EATER, (w, a) => w.Game.Rules.ActorBiteHpRegen(a, 100), true, false);
    }
}
