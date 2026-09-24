using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class SkillAgileScenario
{
    public static void Register()
    {
        SkillScenario.Register("agile", Skills.IDs.AGILE, (w, a) => w.Game.Rules.ActorDefence(a, a.CurrentDefence).Value,
            secondEffect: (w, a) => SkillScenario.MeleeHit(w, a));
    }
}
