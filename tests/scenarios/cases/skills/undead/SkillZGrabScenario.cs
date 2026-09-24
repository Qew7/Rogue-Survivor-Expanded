using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class SkillZGrabScenario
{
    public static void Register()
    {
        SkillScenario.Register("z-grab", Skills.IDs.Z_GRAB, (w, a) => w.Game.Rules.ZGrabChance(a, a), true, false);
    }
}
