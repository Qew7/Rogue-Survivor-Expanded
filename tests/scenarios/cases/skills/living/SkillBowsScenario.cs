using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class SkillBowsScenario
{
    public static void Register()
    {
        SkillScenario.Register("bows", Skills.IDs.BOWS, (w, a) => SkillScenario.RangedDamage(w, a, AttackKind.BOW),
            secondEffect: (w, a) => SkillScenario.RangedHit(w, a, AttackKind.BOW));
    }
}
