using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class SkillZInfectorScenario
{
    public static void Register()
    {
        SkillScenario.Register("z-infector", Skills.IDs.Z_INFECTOR, (w, a) => Rules.InfectionForDamage(a, 100), true, false);
    }
}
