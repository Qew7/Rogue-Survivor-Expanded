using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class AidTradeRumorScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/aid-trade-rumor", () => TownScenarioFactory.Arena(4905,
            "...#.........", "...#.........", "...#........."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor witness = NpcIntentSupport.Actor(world, "witness", 0, 1);
            Actor donor = NpcIntentSupport.Actor(world, "Morgan", 1, 1);
            Actor recipient = NpcIntentSupport.Actor(world, "Petrov", 2, 1);
            Actor buyer = NpcIntentSupport.Actor(world, "buyer", 0, 0);
            Actor seller = NpcIntentSupport.Actor(world, "seller", 1, 0);
            donor.Faction = world.Game.GameFactions.TheBikers;
            recipient.Faction = world.Game.GameFactions.ThePolice;
            buyer.Faction = world.Game.GameFactions.TheSurvivors;
            seller.Faction = world.Game.GameFactions.TheGangstas;
            Actor listener = NpcIntentSupport.Actor(world, "listener", 5, 1);
            Actor player = NpcIntentSupport.Player(world, 6, 1);
            witness.Personality.Opinion(donor.PersonalityIdentity, donor.UnmodifiedName);
            witness.Personality.Opinion(seller.PersonalityIdentity, seller.UnmodifiedName);

            ItemFood gift = NpcIntentSupport.Food(world, donor, 1);
            world.Game.DoGiveItemTo(donor, recipient, gift);
            NpcFact aid = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "shared_food");
            Check.Equal(true, aid != null, "successful gift becomes a retained fact");
            Check.Equal("Morgan", aid.ReportSubject, "known donor is named");
            Check.Equal("a police officer", aid.ReportOther, "unknown recipient is described by faction");
            Check.Equal(true, NpcRecordDescriptions.Report(world.Game.NpcContent, aid).Contains("Morgan shared food with a police officer"),
                "aid rumor includes both participants");

            ItemFood food = NpcIntentSupport.Food(world, buyer, 1);
            var medicine = new ItemMedicine(world.Game.GameItems.BANDAGE);
            Check.Equal(true, seller.Inventory.AddAll(medicine), "seller carries a trade item");
            Check.Call(world.Game, "SwapActorItems", new[] { typeof(Actor), typeof(Item), typeof(Actor), typeof(Item) },
                buyer, food, seller, medicine);
            Check.Equal(true, buyer.Inventory.Contains(medicine) && seller.Inventory.Contains(food), "real exchange completed");
            NpcFact trade = witness.Personality.Knowledge.Facts.Find(f => f.Kind == "traded");
            Check.Equal(true, trade != null, "completed trade becomes a retained fact");
            Check.Equal("a survivor", trade.ReportSubject, "unknown trader is described by faction");
            Check.Equal("seller", trade.ReportOther, "known partner is named");
            Check.Equal(true, NpcRecordDescriptions.Report(world.Game.NpcContent, trade).Contains("a survivor traded with seller"),
                "trade rumor includes both participants");
            Check.Equal(false, listener.Personality.Knowledge.Facts.Exists(f => f.EventId == aid.EventId || f.EventId == trade.EventId),
                "wall blocks both original events");

            world.Place(witness, 4, 0); world.Place(listener, 5, 0); world.Place(player, 6, 0);
            var tellAid = new ActionNpcTell(witness, world.Game, listener, aid);
            Check.Equal(true, tellAid.IsLegal(), "witness can report the gift"); tellAid.Perform();
            witness.ActionPoints = Rules.BASE_ACTION_COST;
            var tellTrade = new ActionNpcTell(witness, world.Game, listener, trade);
            Check.Equal(true, tellTrade.IsLegal(), "witness can report the trade"); tellTrade.Perform();
            Check.Equal("a police officer", listener.Personality.Knowledge.Facts.Find(f => f.EventId == aid.EventId).ReportOther,
                "aid hearsay preserves the original recipient description");
            Check.Equal("a survivor", listener.Personality.Knowledge.Facts.Find(f => f.EventId == trade.EventId).ReportSubject,
                "trade hearsay preserves the original trader description");
            Check.Equal(2, player.Personality.HeardJournal.Count, "player hears both reports");
        });
    }
}
