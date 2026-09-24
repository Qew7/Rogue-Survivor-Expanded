using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class SkillLightSleeperScenario
{
    public static void Register()
    {
        SkillScenario.Register("light-sleeper", Skills.IDs.LIGHT_SLEEPER, (w, a) => w.Game.Rules.ActorLoudNoiseWakeupChance(a, 1));
    }
}
