using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class SkillZStrongScenario
{
    public static void Register()
    {
        SkillScenario.Register("z-strong", Skills.IDs.Z_STRONG, (w, a) => SkillScenario.MeleeDamage(w, a), true, false);
    }
}
