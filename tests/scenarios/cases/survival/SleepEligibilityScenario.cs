using djack.RogueSurvivor.Data;

static class SleepEligibilityScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("survival/sleep-eligibility", () => TownScenarioFactory.Arena(4312,
            ".....", ".....", "....."), world =>
        {
            Actor actor = SkillScenario.Actor(world);
            Check.Equal(false, world.Game.Rules.CanActorSleep(actor), "rested actor need not sleep");
            actor.SleepPoints = 0;
            Check.Equal(true, world.Game.Rules.CanActorSleep(actor), "tired actor may sleep");
            actor.FoodPoints = 0;
            Check.Equal(false, world.Game.Rules.CanActorSleep(actor), "hungry actor cannot sleep");
        });
    }
}
