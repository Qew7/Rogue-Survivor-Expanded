using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

static class PlannerRefusalReplanScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/planner-refusal-replan", () => TownScenarioFactory.Arena(4648, ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 6, 2);
            Actor hungry = NpcIntentSupport.Actor(world, "hungry", 1, 1, "sociable");
            Actor selfish = NpcIntentSupport.Actor(world, "selfish", 2, 1, "selfish");
            NpcIntentSupport.Food(world, selfish, 3); hungry.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            NpcIntentSupport.Turn(world, hungry); NpcIntent goal = NpcIntentSupport.Intent(hungry, "request_food");
            NpcIntentSupport.Turn(world, selfish);
            Check.Equal(false, goal.Finished, "refused method does not terminate the desired food state");
            Check.Equal(0, NpcIntentSupport.FoodUnits(hungry), "refusal cannot satisfy the food goal");
            world.Map.DropItemAt(new ItemFood(world.Game.GameItems.CANNED_FOOD), new Point(1, 2));
            world.Map.LocalTime.TurnCounter = 1; NpcIntentSupport.Turn(world, hungry);
            Check.Equal(NpcIntentStatus.Completed, goal.Status, "the same goal replans to a newly perceived pickup");
            Check.Equal(1, NpcIntentSupport.FoodUnits(hungry), "only the real ground item satisfies the goal");
            Check.Equal(3, NpcIntentSupport.FoodUnits(selfish), "the refusing character retains all possessions");
            Check.Equal(null, world.Map.GetItemsAt(1, 2), "the alternative action removes its actual item");
            Check.Equal(true, NpcIntentSupport.HasEvent(hungry, "request_refused"), "the unsuccessful interaction remains in personal history");
            Check.Equal((ulong)NpcPlanFact.Food, goal.Plan.Desired, "the desired result remains stable across methods");
            int plans = 0;
            foreach (ResidentEntry entry in Session.Get.ResidentRecords.Register(hungry).Entries) if (entry.Kind == "goal_plan") plans++;
            Check.Equal(2, plans, "records preserve the original plan and its actual replacement");
        });
    }
}
