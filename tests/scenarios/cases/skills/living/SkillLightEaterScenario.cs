using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class SkillLightEaterScenario
{
    public static void Register()
    {
        SkillScenario.Register("light-eater", Skills.IDs.LIGHT_EATER, (w, a) => w.Game.Rules.ActorMaxFood(a),
            secondEffect: (w, a) => w.Game.Rules.ActorItemNutritionValue(a, 100));
    }
}
