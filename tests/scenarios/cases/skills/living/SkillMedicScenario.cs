using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class SkillMedicScenario
{
    public static void Register()
    {
        SkillScenario.Register("medic", Skills.IDs.MEDIC, (w, a) => w.Game.Rules.ActorMedicineEffect(a, 100));
    }
}
