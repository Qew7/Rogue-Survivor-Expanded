using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class SkillHardyScenario
{
    public static void Register()
    {
        SkillScenario.Register("hardy", Skills.IDs.HARDY, (w, a) => w.Game.Rules.ActorHealChanceBonus(a));
    }
}
