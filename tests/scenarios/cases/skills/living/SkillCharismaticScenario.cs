using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class SkillCharismaticScenario
{
    public static void Register()
    {
        SkillScenario.Register("charismatic", Skills.IDs.CHARISMATIC, (w, a) => w.Game.Rules.ActorTrustIncrease(a),
            secondEffect: (w, a) => w.Game.Rules.ActorCharismaticTradeChance(a));
    }
}
