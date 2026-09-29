using System.Collections.Generic;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.Actions;

static class PlannerBoundariesScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/planner-boundaries", () => TownScenarioFactory.Arena(4646, ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 6, 2);
            Actor owner = NpcIntentSupport.Actor(world, "owner", 1, 1, "solitary", "scavenger");
            var food = new ItemFood(world.Game.GameItems.CANNED_FOOD) { Quantity = 3 }; world.Map.DropItemAt(food, new Point(2, 1));
            owner.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            ActorAction action = owner.Controller.GetAction(world.Game);
            NpcIntent goal = NpcIntentSupport.Intent(owner, "obtain_food");
            Check.Equal(true, action != null && action.IsLegal() && goal.Plan.Current.Action == NpcPlanAction.PickupFood, "real controller selects a currently legal pickup primitive");
            world.Map.RemoveItemAt(food, new Point(2, 1)); int ap = owner.ActionPoints;
            action.Perform();
            Check.Equal(ap, owner.ActionPoints, "stale pickup spends no action points");
            Check.Equal(0, NpcIntentSupport.FoodUnits(owner), "predicted effects are never committed as food");
            Check.Equal(false, goal.Finished, "missing prerequisite cannot complete the goal");
            var disabled = GamePreset.BuiltIn(GameMode.GM_STANDARD); disabled.NpcPersonalitiesEnabled = false; Session.Get.GamePreset = disabled;
            Check.Equal(false, action.IsLegal(), "disabled personalities reject an already selected action");
            var operators = new List<NpcPlanStep>();
            for (int i = 0; i < 24; i++) operators.Add(new NpcPlanStep { Action = NpcPlanAction.Travel, Adds = 1UL << i, Cost = 1 });
            int expanded; var result = djack.RogueSurvivor.Gameplay.Personality.NpcGoalPlanner.Search(0, (1UL << 24) - 1, operators, out expanded);
            Check.Equal(null, result, "combinatorial search stops without inventing an outcome");
            Check.Equal(true, expanded <= 128, "search has a strict expansion budget");
        });
    }
}
