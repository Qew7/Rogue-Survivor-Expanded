using System;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.AI;

static class TradeRefusalPacingScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/trade-refusal-pacing", () => TownScenarioFactory.Arena(8203,
            ".........", ".........", "........."), world =>
        {
            Session original = Session.Get;
            original.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = NpcIntentSupport.Player(world, 3, 1);
            Actor trader = NpcIntentSupport.Actor(world, "trader", 1, 1);
            Actor refuser = NpcIntentSupport.Actor(world, "refuser", 2, 1);
            ((CivilianAI)refuser.Controller).Directives.CanTrade = false;
            world.Game.DoTrade(trader, refuser);
            Check.Equal(true, NpcIntentSupport.HasEvent(trader, "trade_refused"),
                "a real refusal still becomes an event for memories and rumors");
            Check.Equal(12, trader.Personality.Frustration(0),
                "the real refusal still affects the trader's patience");
            Check.Equal(WorldTime.TURNS_PER_HOUR, trader.Personality.NextTradeAttemptTurn,
                "a refused trader waits one hour before seeking another deal");
            world.Game.DoTrade(player, refuser);
            Check.Equal(0, player.Personality.NextTradeAttemptTurn,
                "a player-initiated refused trade does not restrict the player's future trades");
            world.Map.RemoveActor(refuser);

            Actor other = NpcIntentSupport.Actor(world, "other", 0, 1);
            trader.Inventory.AddAll(new ItemFood(world.Game.GameItems.CANNED_FOOD));
            other.Inventory.AddAll(new ItemFood(world.Game.GameItems.GROCERIES));
            GamePreset neutral = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            neutral.NpcPersonalitiesEnabled = false;
            original.GamePreset = neutral;
            Check.Equal(false, Check.Call(typeof(CivilianAI), "CanSeekTrade", new[] { typeof(Actor), typeof(int) },
                trader, WorldTime.TURNS_PER_HOUR - 1),
                "a different partner cannot bypass the global pause");
            Check.Equal(true, Check.Call(typeof(CivilianAI), "CanSeekTrade", new[] { typeof(Actor), typeof(int) },
                trader, WorldTime.TURNS_PER_HOUR),
                "trade selection resumes when the pause ends");
            trader.Controller = new CivilianAI();
            world.Map.LocalTime.TurnCounter = WorldTime.TURNS_PER_HOUR - 1;
            NpcIntentSupport.Turn(world, trader);
            Check.Equal(false, Check.CallOn(typeof(BaseAI), trader.Controller, "IsActorTabooTrade", other),
                "a new partner cannot bypass the trader's global pause");

            Guid traderId = trader.PersonalityIdentity;
            string path = Path.Combine(Path.GetTempPath(), "trade-pacing-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, original);
                Session loaded = BinarySaveStore.Load<Session>(path);
                Map map = loaded.World[0, 0].EntryMap; map.ReconstructAuxiliaryFields();
                Actor restored = NpcIntentSupport.Find(map, traderId);
                Check.Equal(WorldTime.TURNS_PER_HOUR, restored.Personality.NextTradeAttemptTurn,
                    "the trade pause survives save and load");
            }
            finally
            {
                Session.Restore(original);
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
