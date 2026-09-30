using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;
static class StockpileInterestScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/stockpile-interest", () => TownScenarioFactory.Arena(4802,
            ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 8, 2);
            Actor planner = NpcIntentSupport.Actor(world, "planner", 1, 1, "organized", "disciplined");
            world.Map.DropItemAt(new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 2 }, new Point(2, 1));
            world.Map.DropItemAt(new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 2 }, new Point(4, 1));
            for (int turn = 0; turn <= 24 && NpcIntentSupport.FoodUnits(planner) < 3; turn++)
            { world.Map.LocalTime.TurnCounter = turn; NpcIntentSupport.Turn(world, planner); }
            Check.Equal(true, NpcIntentSupport.FoodUnits(planner) >= 3, "a lasting interest accumulates a real reserve from multiple places");
            Check.Equal(true, planner.Personality.Interest("food_reserve", planner.PersonalityIdentity) != null,
                "interest persists independently from one completed plan");
            var stale = new ItemFood(world.Game.GameItems.CANNED_FOOD); world.Map.DropItemAt(stale, new Point(6, 1));
            world.Map.LocalTime.TurnCounter = 25; NpcIntentSupport.Turn(world, planner);
            var stalePlace = new Location(world.Map, new Point(6, 1));
            Check.Equal(true, planner.Personality.Knowledge.Places.Exists(p => p.Kind == "food" && p.Place == stalePlace && p.Units > 0),
                "planner actually observes the cache before it disappears");
            int interestsBeforeRemoval = planner.Personality.Interests.Count;
            world.Map.RemoveItemAt(stale, new Point(6, 1));
            world.Map.LocalTime.TurnCounter = 26; NpcIntentSupport.Turn(world, planner);
            Check.Equal(interestsBeforeRemoval, planner.Personality.Interests.Count,
                "removing observed food does not create another lasting interest");
            Check.Equal(true, planner.Personality.Interests.Count <= 16, "saved interests stay bounded");
        });
    }
}
