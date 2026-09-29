using System;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.Personality;

static class IntentBoundariesScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/intent-boundaries", () => TownScenarioFactory.Arena(4604, "...#...", "...#...", "...#..."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = NpcIntentSupport.Player(world, 2, 1);
            Actor grateful = NpcIntentSupport.Actor(world, "grateful", 1, 1, "generous");
            Actor hidden = NpcIntentSupport.Actor(world, "grateful", 5, 1, "generous");
            NpcIntentSupport.Food(world, grateful, 3);
            SignificantEvent source = new SignificantEvent("helped", grateful, player, world.Map, grateful.Location.Position, 0);
            PersonalitySystem.Report(world.Game, source); PersonalitySystem.Report(world.Game, source);
            Check.Equal(1, grateful.Personality.Intents.Count, "duplicate event creates only one intent");
            Check.Equal(1, grateful.Personality.Reactions.Count, "duplicate event creates only one reaction");
            Check.Equal(0, hidden.Personality.Intents.Count, "hidden namesake receives no private intention");
            NpcIntent intent = grateful.Personality.Intents[0];
            Actor namesake = NpcIntentSupport.Actor(world, player.UnmodifiedName, 1, 0);
            var wrongReaction = new ActionNpcReaction(grateful, world.Game, grateful.Personality.Reactions[0], namesake);
            Check.Equal(false, wrongReaction.IsLegal(), "reaction cannot address another person with the same name");
            wrongReaction.Perform();
            Check.Equal(1, grateful.Personality.Reactions.Count, "invalid reaction remains queued for its actual recipient");
            NpcIntentSystem.Block(grateful, intent, "temporary obstruction");
            var delayedAction = new ActionNpcIntent(grateful, world.Game, intent, player, NpcFoodSupply.SpareFood(world.Game, grateful, player));
            Check.Equal(false, delayedAction.IsLegal(), "selected action cannot bypass retry delay");
            world.Map.LocalTime.TurnCounter = intent.NextAttempt;
            // A stale action is rejected when the recipient can no longer receive food.
            player.Inventory.MaxCapacity = 0;
            var action = new ActionNpcIntent(grateful, world.Game, intent, player, NpcFoodSupply.SpareFood(world.Game, grateful, player));
            int points = grateful.ActionPoints, food = NpcIntentSupport.FoodUnits(grateful);
            Check.Equal(false, action.IsLegal(), "full recipient inventory prevents social transfer");
            action.Perform();
            Check.Equal(points, grateful.ActionPoints, "illegal action spends no AP");
            Check.Equal(food, NpcIntentSupport.FoodUnits(grateful), "illegal action loses no items");
            int before = grateful.Personality.Person(player.PersonalityIdentity).Feeling;
            for (int turn = 1; turn <= 40; turn++)
                PersonalitySystem.Report(world.Game, new SignificantEvent("raid", null, null, world.Map, grateful.Location.Position, turn));
            Check.Equal(32, grateful.Personality.Events.Count, "source leaves the bounded observation journal");
            int due = 0;
            foreach (MemoryInstance memory in grateful.Personality.Memories) due = Math.Max(due, memory.ResolveTurn);
            world.Map.LocalTime.TurnCounter = due;
            PersonalitySystem.ResolveDue(world.Game, world.Map);
            PersonalitySystem.Report(world.Game, source);
            Check.Equal(before, grateful.Personality.Person(player.PersonalityIdentity).Feeling, "replaying resolved source does not award feeling again");
            Check.Equal(0, grateful.Personality.Memories.Count, "resolved source does not recreate its memory");
            GamePreset disabled = GamePreset.BuiltIn(GameMode.GM_STANDARD); disabled.NpcPersonalitiesEnabled = false;
            Session.Get.GamePreset = disabled;
            PersonalitySystem.Report(world.Game, new SignificantEvent("helped", hidden, player, world.Map, hidden.Location.Position, 9000));
            Check.Equal(0, hidden.Personality.Intents.Count, "disabled preset creates no intentions");
            Check.Equal(null, NpcIntentSystem.Select(grateful), "disabled preset does not execute saved goals");
            Check.Equal(false, action.IsLegal(), "disabled preset rejects already selected action");
        });
    }
}
