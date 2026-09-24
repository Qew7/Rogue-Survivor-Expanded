using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class SkillCarpentryScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("skill/carpentry", () => TownScenarioFactory.Create(4201, false), world =>
        {
            Actor actor = SkillScenario.Actor(world);
            int points = world.Game.Rules.ActorBarricadingPoints(actor, 100);
            int material = world.Game.Rules.ActorBarricadingMaterialNeedForFortification(actor, true);
            world.Game.SkillUpgrade(actor, Skills.IDs.CARPENTRY);
            Check.Equal(true, world.Game.Rules.ActorBarricadingPoints(actor, 100) > points,
                "first level strengthens barricade");
            world.Game.SkillUpgrade(actor, Skills.IDs.CARPENTRY);
            world.Game.SkillUpgrade(actor, Skills.IDs.CARPENTRY);
            Check.Equal(true, world.Game.Rules.ActorBarricadingMaterialNeedForFortification(actor, true) < material,
                "third level saves construction material");
        });
    }
}
