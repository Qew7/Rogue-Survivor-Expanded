using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class SkillHaulerScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("skill/hauler", () => TownScenarioFactory.Create(4201, false), world =>
        {
            Actor actor = SkillScenario.Actor(world);
            int before = actor.Inventory.MaxCapacity;
            world.Game.SkillUpgrade(actor, Skills.IDs.HAULER);
            Check.Equal(true, actor.Inventory.MaxCapacity > before,
                "upgrade expands actual inventory");
            Check.Equal(world.Game.Rules.ActorMaxInv(actor), actor.Inventory.MaxCapacity,
                "inventory capacity stays in sync with rule");
        });
    }
}
