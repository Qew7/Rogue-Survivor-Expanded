using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class SkillStrongScenario
{
    public static void Register()
    {
        SkillScenario.Register("strong", Skills.IDs.STRONG, (w, a) => SkillScenario.MeleeDamage(w, a),
            secondEffect: (w, a) => w.Game.Rules.ActorMaxThrowRange(a, 5));
    }
}
