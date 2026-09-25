using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Generators;

static class ActorPlacementScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("generation/actor-placement", () => TownScenarioFactory.Arena(7121,
            "...", ".#.", "..."), world =>
        {
            BaseTownGenerator.Parameters parameters = BaseTownGenerator.DEFAULT_PARAMS;
            parameters.District = new District(new Point(0, 0), DistrictKind.RESIDENTIAL);
            BaseTownGenerator generator = new BaseTownGenerator(world.Game, parameters);
            Actor actor = SkillScenario.Actor(world);
            Check.Equal(false, generator.ActorPlace(new DiceRoller(7121), 100,
                world.Map, actor, point => point == new Point(1, 1)),
                "actor cannot be placed on a wall");
            Check.Equal(true, generator.ActorPlace(new DiceRoller(7121), 100,
                world.Map, actor, point => point == new Point(2, 2)),
                "actor finds the only eligible tile");
            Check.Equal(new Point(2, 2), actor.Location.Position,
                "actor is placed at the eligible tile");
            Check.Equal(false, generator.ActorPlace(new DiceRoller(7121), 100,
                world.Map, SkillScenario.Actor(world), point => point == new Point(2, 2)),
                "occupied tile cannot be reused");
        });
    }
}
