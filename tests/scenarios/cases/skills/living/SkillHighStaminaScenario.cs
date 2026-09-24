using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class SkillHighStaminaScenario
{
    public static void Register()
    {
        SkillScenario.Register("high-stamina", Skills.IDs.HIGH_STAMINA, (w, a) => w.Game.Rules.ActorMaxSTA(a));
    }
}
