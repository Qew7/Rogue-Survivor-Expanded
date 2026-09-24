using djack.RogueSurvivor.Data;

static class WeatherVisibilityScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/weather-visibility", () => TownScenarioFactory.Arena(4314,
            ".....", ".....", "....."), world =>
        {
            Actor civilian = SkillScenario.Actor(world);
            Actor undead = SkillScenario.Actor(world, true);
            int clear = world.Game.Rules.ActorFOV(civilian, world.Map.LocalTime, Weather.CLEAR);
            int rain = world.Game.Rules.ActorFOV(civilian, world.Map.LocalTime, Weather.HEAVY_RAIN);
            Check.Equal(true, rain < clear, "rain reduces civilian visibility outdoors");
            int undeadClear = world.Game.Rules.ActorFOV(undead, world.Map.LocalTime, Weather.CLEAR);
            int undeadRain = world.Game.Rules.ActorFOV(undead, world.Map.LocalTime, Weather.HEAVY_RAIN);
            Check.Equal(undeadClear, undeadRain, "undead ignore rain visibility penalty");
        });
    }
}
