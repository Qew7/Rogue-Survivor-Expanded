using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.MapObjects;

static class PowerGeneratorScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("objects/power-generator", () => TownScenarioFactory.Arena(4315,
            ".....", ".....", "....."), world =>
        {
            Actor actor = SkillScenario.Actor(world);
            world.Map.PlaceActorAt(actor, new Point(1, 1));
            PowerGenerator generator = new PowerGenerator("generator", "off", "on");
            world.Map.PlaceMapObjectAt(generator, new Point(2, 1));
            Check.Equal(false, generator.IsOn, "generator starts off");
            Check.Equal(true, world.Try(new ActionSwitchPowerGenerator(actor, world.Game, generator)),
                "switch action legal");
            Check.Equal(true, generator.IsOn, "generator turns on");
            Check.Equal(true, world.Try(new ActionSwitchPowerGenerator(actor, world.Game, generator)),
                "switch action legal again");
            Check.Equal(false, generator.IsOn, "generator turns off");
        });
    }
}
