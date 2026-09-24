using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Actions;

static class RestScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("survival/rest", () => TownScenarioFactory.Arena(4309,
            ".....", ".....", "....."), world =>
        {
            Actor actor = SkillScenario.Actor(world);
            actor.StaminaPoints = 5;
            int before = actor.StaminaPoints;
            int ap = actor.ActionPoints;
            Check.Equal(true, world.Try(new ActionWait(actor, world.Game)), "wait action legal");
            Check.Equal(true, actor.StaminaPoints > before, "waiting restores stamina");
            Check.Equal(true, actor.ActionPoints < ap, "waiting costs action points");
        });
    }
}
