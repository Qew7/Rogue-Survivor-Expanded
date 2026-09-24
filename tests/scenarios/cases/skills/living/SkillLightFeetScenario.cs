using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class SkillLightFeetScenario
{
    public static void Register()
    {
        SkillScenario.Register("light-feet", Skills.IDs.LIGHT_FEET, (w, a) => SkillScenario.TrapChance(w, a), false, true);
    }
}
