using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;

static class MedicineExchangeScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/medicine-exchange", () => TownScenarioFactory.Arena(4677, ".........", ".........", "........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD); NpcIntentSupport.Player(world, 8, 2);
            Actor buyer = NpcIntentSupport.Actor(world, "buyer", 1, 1, "sociable", "generous");
            Actor seller = NpcIntentSupport.Actor(world, "seller", 2, 1, "sociable", "honest", "humble");
            buyer.HitPoints = 1;
            NpcIntentSupport.Food(world, buyer, 3);
            var medicine = new ItemMedicine(world.Game.GameItems.MEDIKIT); seller.Inventory.AddAll(medicine);
            NpcIntentSupport.Turn(world, buyer);
            Check.Equal(true, NpcIntentSupport.HasEvent(seller, "requested_medicine"), "wounded actor asks without inspecting another inventory");
            NpcIntentSupport.Turn(world, seller);
            Check.Equal(true, NpcIntentSupport.HasEvent(buyer, "medicine_offered"), "trader actually announces the exchange");
            world.Map.LocalTime.TurnCounter = 1; NpcIntentSupport.Turn(world, buyer);
            Check.Equal(true, NpcIntentSupport.HasEvent(buyer, "bartered_medicine"), "socially learned offer becomes a real alternative method");
            Check.Equal(1, buyer.HitPoints, "exchange alone does not predict healed wounds");
            Check.Equal(0, NpcIntentSupport.FoodUnits(buyer), "actual agreed payment leaves buyer");
            Check.Equal(3, NpcIntentSupport.FoodUnits(seller), "payment is received once");
            Check.Equal(false, seller.Inventory.Contains(medicine), "seller relinquishes the traded medicine");
            world.Map.LocalTime.TurnCounter = 2; NpcIntentSupport.Turn(world, buyer);
            Check.Equal(true, buyer.HitPoints > 1, "acquired medicine subsequently restores real health");
        });
    }
}
