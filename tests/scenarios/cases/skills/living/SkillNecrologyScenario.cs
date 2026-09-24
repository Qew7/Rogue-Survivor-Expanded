using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class SkillNecrologyScenario
{
    public static void Register()
    {
        SkillScenario.Register("necrology", Skills.IDs.NECROLOGY, (w, a) => w.Game.Rules.ActorDamageBonusVsUndeads(a));
    }
}
