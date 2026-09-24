using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class SkillZLightFeetScenario
{
    public static void Register()
    {
        SkillScenario.Register("z-light-feet", Skills.IDs.Z_LIGHT_FEET, (w, a) => SkillScenario.TrapChance(w, a), true, true);
    }
}
