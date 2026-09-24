using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class SkillZLightEaterScenario
{
    public static void Register()
    {
        SkillScenario.Register("z-light-eater", Skills.IDs.Z_LIGHT_EATER, (w, a) => w.Game.Rules.ActorMaxRot(a), true, false);
    }
}
