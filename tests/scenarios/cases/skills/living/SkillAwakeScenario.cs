using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class SkillAwakeScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("skill/awake", () => TownScenarioFactory.Create(4201, false), world =>
        {
            Actor actor = SkillScenario.Actor(world);
            int maxSleep = world.Game.Rules.ActorMaxSleep(actor);
            int regen = world.Game.Rules.ActorSleepRegen(actor, false);
            world.Game.SkillUpgrade(actor, Skills.IDs.AWAKE);
            Check.Equal(true, world.Game.Rules.ActorMaxSleep(actor) > maxSleep,
                "first level increases maximum sleep");
            world.Game.SkillUpgrade(actor, Skills.IDs.AWAKE);
            Check.Equal(true, world.Game.Rules.ActorSleepRegen(actor, false) > regen,
                "second level improves sleep regeneration after integer rounding");
        });
    }
}
