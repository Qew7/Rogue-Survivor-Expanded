using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class SkillFirearmsScenario
{
    public static void Register()
    {
        SkillScenario.Register("firearms", Skills.IDs.FIREARMS, (w, a) => SkillScenario.RangedDamage(w, a, AttackKind.FIREARM),
            secondEffect: (w, a) => SkillScenario.RangedHit(w, a, AttackKind.FIREARM));
    }
}
