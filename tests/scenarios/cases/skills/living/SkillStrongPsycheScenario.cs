using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class SkillStrongPsycheScenario
{
    public static void Register()
    {
        SkillScenario.Register("strong-psyche", Skills.IDs.STRONG_PSYCHE, (w, a) => w.Game.Rules.ActorDisturbedLevel(a), false, true);
    }
}
