using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;

static class EatingScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("survival/eating", () => TownScenarioFactory.Arena(4317,
            ".....", ".....", "....."), world =>
        {
            Actor actor = SkillScenario.Actor(world);
            actor.FoodPoints = 0;
            ItemFood groceries = new ItemFood(world.Game.GameItems.GROCERIES, 1000);
            actor.Inventory.AddAll(groceries);
            Check.Equal(true, world.Try(new ActionUseItem(actor, world.Game, groceries)),
                "food use legal");
            Check.Equal(true, actor.FoodPoints > 0, "eating restores food points");
            Check.Equal(false, actor.Inventory.Contains(groceries), "food consumed");
        });
    }
}
