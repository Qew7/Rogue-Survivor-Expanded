using djack.RogueSurvivor.Engine.Items;

static class FoodSpoilageScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("survival/food-spoilage", () => TownScenarioFactory.Create(4305, false), world =>
        {
            ItemFood food = new ItemFood(world.Game.GameItems.GROCERIES, 100);
            Check.Equal(true, world.Game.Rules.IsFoodStillFresh(food, 99), "food fresh before deadline");
            Check.Equal(true, world.Game.Rules.IsFoodExpired(food, 100), "food expires at deadline");
            Check.Equal(true, world.Game.Rules.IsFoodSpoiled(food, 200), "food spoils later");
            int fresh = world.Game.Rules.FoodItemNutrition(food, 99);
            int expired = world.Game.Rules.FoodItemNutrition(food, 100);
            int spoiled = world.Game.Rules.FoodItemNutrition(food, 200);
            Check.Equal(true, fresh > expired && expired > spoiled,
                "nutrition falls as food ages");
        });
    }
}
