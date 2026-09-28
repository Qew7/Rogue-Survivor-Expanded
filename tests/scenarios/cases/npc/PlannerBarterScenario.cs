using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;
using djack.RogueSurvivor.Gameplay;

static class PlannerBarterScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/planner-barter", () => TownScenarioFactory.Arena(4644, ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 6, 2);
            Actor buyer = NpcIntentSupport.Actor(world, "buyer", 1, 1, "sociable", "honest");
            Actor seller = NpcIntentSupport.Actor(world, "seller", 2, 1, "sociable", "honest", "humble");
            var payment = new ItemMedicine(world.Game.GameItems.MEDIKIT); buyer.Inventory.AddAll(payment); NpcIntentSupport.Food(world, seller, 3);
            buyer.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            NpcIntentSupport.Turn(world, buyer);
            NpcIntent goal = NpcIntentSupport.Intent(buyer, "request_food");
            Check.Equal(0, NpcIntentSupport.FoodUnits(buyer), "request predicts help but creates no food");
            NpcIntentSupport.Turn(world, seller);
            Check.Equal(true, NpcIntentSupport.HasEvent(buyer, "food_offered"), "trade preference produces a real counteroffer");
            Check.Equal(0, NpcIntentSupport.FoodUnits(buyer), "a spoken counteroffer is still not a transaction");
            world.Map.LocalTime.TurnCounter = 8; int sellerAP = seller.ActionPoints;
            NpcIntentSupport.Turn(world, buyer);
            Check.Equal(NpcIntentStatus.Completed, goal.Status, "new knowledge changes the plan to an actual exchange");
            Check.Equal(2, NpcIntentSupport.FoodUnits(buyer), "buyer acquires agreed food units");
            Check.Equal(1, NpcIntentSupport.FoodUnits(seller), "seller retains its food reserve");
            Check.Equal(false, buyer.Inventory.Contains(payment), "buyer actually parts with payment");
            Check.Equal(true, seller.Inventory.Contains(payment), "seller receives the real item");
            Check.Equal(sellerAP, seller.ActionPoints, "passive seller is not charged a second turn");
            MemoryInstance memory = null; foreach (MemoryInstance pending in buyer.Personality.Memories) if (pending.Id == "traded_for_food") memory = pending;
            Check.Equal(true, memory != null, "completed exchange creates a personal experience");
            int charisma = buyer.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.CHARISMATIC);
            world.Map.LocalTime.TurnCounter = memory.ResolveTurn; PersonalitySystem.ResolveDue(world.Game, world.Map);
            Check.Equal(charisma + 1, buyer.Sheet.SkillTable.GetSkillLevel((int)Skills.IDs.CHARISMATIC), "negotiation memory resolves to a real skill outcome");
        });
    }
}
