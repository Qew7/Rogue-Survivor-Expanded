using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class SkillUnsuspiciousScenario
{
    public static void Register()
    {
        SkillScenario.Register("unsuspicious", Skills.IDs.UNSUSPICIOUS, (w, a) => w.Game.Rules.ActorUnsuspicousChance(a, a));
    }
}
