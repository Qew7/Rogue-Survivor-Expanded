using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay;
using djack.RogueSurvivor.Gameplay.AI;

static class TrackerTradeValuationScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/tracker-trade-valuation", () => TownScenarioFactory.Arena(4584,
            "....", "...."), world =>
        {
            Actor trader = NpcIntentSupport.Actor(world, "trader", 1, 1, "sociable");
            Actor partner = NpcIntentSupport.Actor(world, "partner", 2, 1, "sociable");
            ItemTracker owned = new ItemTracker(world.Game.GameItems.CELL_PHONE);
            ItemTracker offered = new ItemTracker(world.Game.GameItems.CELL_PHONE);
            owned.Batteries = 10;
            offered.Batteries = 20;
            trader.Inventory.AddAll(owned);
            BaseAI ai = (BaseAI)trader.Controller;
            Check.Equal(BaseAI.TradeRating.ACCEPT, ai.RateTradeOffer(world.Game, partner, offered, owned),
                "NPC can evaluate a better tracker without crashing");
            offered.Batteries = 5;
            Check.Equal(BaseAI.TradeRating.REFUSE, ai.RateTradeOffer(world.Game, partner, offered, owned),
                "NPC rejects a tracker with fewer batteries");
            offered.Batteries = 10;
            Check.Equal(BaseAI.TradeRating.MAYBE, ai.RateTradeOffer(world.Game, partner, offered, owned),
                "equal trackers remain negotiable");
            ItemRadio radio = new ItemRadio(world.Game.GameItems[GameItems.IDs.RADIO_LOCAL] as ItemTrackerModel);
            ItemRadio otherRadio = new ItemRadio(world.Game.GameItems[GameItems.IDs.RADIO_LOCAL] as ItemTrackerModel);
            radio.Batteries = 10;
            otherRadio.Batteries = 20;
            Check.Equal(BaseAI.TradeRating.ACCEPT, ai.RateTradeOffer(world.Game, partner, otherRadio, radio),
                "portable radios use the same safe tracker comparison");
        });
    }
}
