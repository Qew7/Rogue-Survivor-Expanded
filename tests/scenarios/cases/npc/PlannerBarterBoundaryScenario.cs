using System.Collections.Generic;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class PlannerBarterBoundaryScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/planner-barter-boundary", () => TownScenarioFactory.Arena(4648, ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 6, 2);
            Actor buyer = NpcIntentSupport.Actor(world, "buyer", 1, 1, "sociable", "honest");
            Actor seller = NpcIntentSupport.Actor(world, "seller", 2, 1, "sociable", "honest", "humble");
            var payment = new ItemMedicine(world.Game.GameItems.BANDAGE) { Quantity = 2 };
            var existing = new ItemMedicine(world.Game.GameItems.BANDAGE) { Quantity = world.Game.GameItems.BANDAGE.StackingLimit - 1 };
            buyer.Inventory.AddAll(payment); seller.Inventory.AddAll(existing); NpcIntentSupport.Food(world, seller, 3);
            seller.Inventory.MaxCapacity = seller.Inventory.CountItems;
            buyer.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            NpcIntentSupport.Turn(world, buyer); NpcIntentSupport.Turn(world, seller); world.Map.LocalTime.TurnCounter = 8;
            NpcIntent goal = NpcIntentSupport.Intent(buyer, "request_food");
            var domain = new NpcPlanDomain(world.Game, buyer, goal, new List<Actor> { seller });
            int expanded; var steps = NpcGoalPlanner.Search(domain.Initial, goal.Plan.Desired, domain.Actions, out expanded);
            goal.Plan.Invalidate(); goal.Plan.Steps.AddRange(steps);
            var action = new ActionNpcBarter(buyer, world.Game, goal, goal.Plan.Current, seller);
            Check.Equal(true, seller.Inventory.CanAddAtLeastOne(payment), "seller has partial stack space");
            Check.Equal(false, action.IsLegal(), "whole-payment exchange requires complete capacity");
            int ap = buyer.ActionPoints; long received = buyer.Inventory.TotalReceived; action.Perform();
            Check.Equal(ap, buyer.ActionPoints, "invalid exchange costs no turn");
            Check.Equal(received, buyer.Inventory.TotalReceived, "invalid exchange does not increment lifetime acquisitions");
            Check.Equal(0, NpcIntentSupport.FoodUnits(buyer), "partial capacity cannot fabricate food");
            Check.Equal(3, NpcIntentSupport.FoodUnits(seller), "seller's food remains intact");
            Check.Equal(true, buyer.Inventory.Contains(payment), "buyer keeps the complete payment");
        });
    }
}
