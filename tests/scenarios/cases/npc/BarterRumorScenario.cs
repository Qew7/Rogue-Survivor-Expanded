using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class BarterRumorScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/barter-rumor", () => TownScenarioFactory.Arena(4906,
            ".......", ".......", "......."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            NpcIntentSupport.Player(world, 6, 2);
            Actor witness = NpcIntentSupport.Actor(world, "witness", 4, 1);
            Actor buyer = NpcIntentSupport.Actor(world, "buyer", 1, 1, "sociable", "honest");
            Actor seller = NpcIntentSupport.Actor(world, "seller", 2, 1, "sociable", "honest", "humble");
            witness.Personality.Opinion(seller.PersonalityIdentity, seller.UnmodifiedName);
            var payment = new ItemMedicine(world.Game.GameItems.MEDIKIT);
            Check.Equal(true, buyer.Inventory.AddAll(payment), "buyer owns real payment");
            NpcIntentSupport.Food(world, seller, 3);
            buyer.FoodPoints = Session.Get.GamePreset.HungerPoints - 1;
            NpcIntentSupport.Turn(world, buyer);
            NpcIntentSupport.Turn(world, seller);
            world.Map.LocalTime.TurnCounter = 8;
            NpcIntentSupport.Turn(world, buyer);
            Check.Equal(true, seller.Inventory.Contains(payment) && NpcIntentSupport.FoodUnits(buyer) == 2,
                "the barter transfers real items");
            NpcFact fact = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "bartered_food");
            Check.Equal(true, fact != null, "the completed barter becomes a reportable fact");
            Check.Equal("a civilian", fact.ReportSubject, "unknown buyer is described by faction");
            Check.Equal("seller", fact.ReportOther, "known seller is named");
            Check.Equal(true, NpcRecordDescriptions.Report(world.Game.NpcContent, fact)
                .Contains("a civilian obtained food by exchanging supplies with seller"),
                "barter rumor preserves both actor descriptions");
        });
    }
}
