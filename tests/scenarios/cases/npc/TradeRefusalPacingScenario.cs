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
            Actor player = NpcIntentSupport.Player(world, 7, 1);
            Actor trader = NpcIntentSupport.Actor(world, "trader", 1, 1);
            Actor refuser = NpcIntentSupport.Actor(world, "refuser", 2, 1);
            Actor other = NpcIntentSupport.Actor(world, "other", 0, 1);
            trader.Inventory.AddAll(new ItemFood(world.Game.GameItems.CANNED_FOOD));
            refuser.Inventory.AddAll(new ItemFood(world.Game.GameItems.GROCERIES));
            other.Inventory.AddAll(new ItemFood(world.Game.GameItems.GROCERIES));
            player.Inventory.AddAll(new ItemFood(world.Game.GameItems.CANNED_FOOD));
            ((CivilianAI)refuser.Controller).Directives.CanTrade = false;
            ((CivilianAI)other.Controller).Directives.CanTrade = false;

            world.Game.DoTrade(trader, refuser);
            Check.Equal(1, Refusals(trader), "a real refusal becomes an observed event");
            Check.Equal(12, trader.Personality.Frustration(0), "refusal affects the trader's patience");
            Check.Equal(WorldTime.TURNS_PER_HOUR / 2, trader.Personality.NextTradeAttemptTurn,
                "a refused trader waits half an hour before seeking another partner");
            Check.Equal(6 * WorldTime.TURNS_PER_HOUR,
                trader.Personality.Person(refuser.PersonalityIdentity).NextTradeAttemptTurn,
                "the trader waits six hours before asking the same person again");
            Check.Equal(false, world.Game.Rules.CanActorInitiateTradeWith(trader, refuser),
                "the refusing partner is temporarily unavailable");
            Check.Equal(true, world.Game.Rules.CanActorInitiateTradeWith(refuser, trader),
                "the refuser can propose a different trade immediately");
            Check.Equal(false, world.Game.Rules.CanActorInitiateTradeWith(trader, other),
                "another partner waits for the general pause");

            world.Map.LocalTime.TurnCounter = 1;
            world.Game.DoChat(trader, refuser);
            Check.Equal(1, Refusals(trader), "ordinary conversation does not retry the refused trade");
            world.Map.LocalTime.TurnCounter = WorldTime.TURNS_PER_HOUR / 2 - 1;
            Check.Equal(false, world.Game.Rules.CanActorInitiateTradeWith(trader, other),
                "another partner is blocked just before the half-hour boundary");
            world.Map.LocalTime.TurnCounter++;
            Check.Equal(true, world.Game.Rules.CanActorInitiateTradeWith(trader, other),
                "another partner becomes eligible at the half-hour boundary");
            Check.Equal(false, world.Game.Rules.CanActorInitiateTradeWith(trader, refuser),
                "the refusing partner still has a separate six-hour pause");
            NpcIntentSupport.Turn(world, trader);
            Check.Equal(2, Refusals(trader), "the production AI can try a different partner");
            Check.Equal(WorldTime.TURNS_PER_HOUR, trader.Personality.NextTradeAttemptTurn,
                "the second refusal restarts the general half-hour pause");
            Check.Equal(6 * WorldTime.TURNS_PER_HOUR + WorldTime.TURNS_PER_HOUR / 2,
                trader.Personality.Person(other.PersonalityIdentity).NextTradeAttemptTurn,
                "the second refusal starts that partner's six-hour pause");

            world.Game.DoTrade(player, refuser);
            Check.Equal(true, world.Game.Rules.CanActorInitiateTradeWith(player, refuser),
                "a player-initiated refusal does not block another player offer");

            Guid traderId = trader.PersonalityIdentity;
            Guid refuserId = refuser.PersonalityIdentity;
            Guid otherId = other.PersonalityIdentity;
            string path = Path.Combine(Path.GetTempPath(), "trade-pacing-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, original);
                Session loaded = BinarySaveStore.Load<Session>(path);
                Map map = loaded.World[0, 0].EntryMap; map.ReconstructAuxiliaryFields();
                Actor restored = NpcIntentSupport.Find(map, traderId);
                Actor savedRefuser = NpcIntentSupport.Find(map, refuserId);
                Actor savedOther = NpcIntentSupport.Find(map, otherId);
                Check.Equal(WorldTime.TURNS_PER_HOUR, restored.Personality.NextTradeAttemptTurn,
                    "the general pause survives save and load");
                Check.Equal(6 * WorldTime.TURNS_PER_HOUR,
                    restored.Personality.Person(refuserId).NextTradeAttemptTurn,
                    "the first partner's pause survives save and load");
                Check.Equal(6 * WorldTime.TURNS_PER_HOUR + WorldTime.TURNS_PER_HOUR / 2,
                    restored.Personality.Person(otherId).NextTradeAttemptTurn,
                    "the second partner's pause survives save and load");
                map.LocalTime.TurnCounter = WorldTime.TURNS_PER_HOUR - 1;
                Check.Equal(false, world.Game.Rules.CanActorInitiateTradeWith(restored, savedOther),
                    "the general pause remains active after loading");
                map.LocalTime.TurnCounter = 6 * WorldTime.TURNS_PER_HOUR - 1;
                Check.Equal(false, world.Game.Rules.CanActorInitiateTradeWith(restored, savedRefuser),
                    "the first partner is blocked before six hours pass");
                Check.Equal(true, world.Game.Rules.CanActorInitiateTradeWith(savedRefuser, restored),
                    "the refuser remains free to propose a trade after save and load");
                map.LocalTime.TurnCounter = 6 * WorldTime.TURNS_PER_HOUR;
                Check.Equal(true, world.Game.Rules.CanActorInitiateTradeWith(restored, savedRefuser),
                    "the first partner becomes eligible at the boundary");
                Check.Equal(false, world.Game.Rules.CanActorInitiateTradeWith(restored, savedOther),
                    "the second partner keeps their own later deadline");
                map.LocalTime.TurnCounter += WorldTime.TURNS_PER_HOUR / 2;
                Check.Equal(true, world.Game.Rules.CanActorInitiateTradeWith(restored, savedOther),
                    "the second partner becomes eligible at their boundary");
            }
            finally
            {
                Session.Restore(original);
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }

    static int Refusals(Actor actor)
    {
        int count = 0;
        foreach (ObservedEvent observed in actor.Personality.Events)
            if (observed.Kind == "trade_refused") count++;
        return count;
    }
}
