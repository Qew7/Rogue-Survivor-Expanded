using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class SkillZAgileScenario
{
    public static void Register()
    {
        SkillScenario.Register("z-agile", Skills.IDs.Z_AGILE, (w, a) => w.Game.Rules.ActorDefence(a, a.CurrentDefence).Value,
            true, false, (w, a) => SkillScenario.MeleeHit(w, a));
    }
}
