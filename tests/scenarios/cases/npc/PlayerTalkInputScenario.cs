using System.Windows.Forms;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class PlayerTalkInputScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/player-talk-input", () => TownScenarioFactory.Arena(4707,
            ".....", ".....", ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = NpcIntentSupport.Player(world, 2, 2);
            Actor requester = NpcIntentSupport.Actor(world, "requester", 3, 2);
            ScenarioUI ui = (ScenarioUI)world.Game.UI;
            NpcEvents.Publish(world.Game, "requested_food", requester, player);
            player.ActionPoints = Rules.BASE_ACTION_COST;
            ui.QueueWaitKey(Keys.Escape);
            Check.Equal(false, Check.Call(world.Game, "HandlePlayerTalk", new[] { typeof(Actor) }, player),
                "Esc postpones a request without answering");
            Check.Equal(true, NpcConversation.Pending(player, requester) != null, "cancel preserves the pending request");
            Check.Equal(Rules.BASE_ACTION_COST, player.ActionPoints, "cancel costs no turn");
            ui.QueueWaitKey(Keys.Y);
            Check.Equal(true, Check.Call(world.Game, "HandlePlayerTalk", new[] { typeof(Actor) }, player),
                "one adjacent person is selected without a direction prompt");
            Check.Equal(0, player.ActionPoints, "Y performs a real talk action");
            Check.Equal(true, NpcIntentSupport.HasEvent(requester, "food_promised"), "Y publishes its consequence");

            Actor second = NpcIntentSupport.Actor(world, "second", 2, 3);
            NpcEvents.Publish(world.Game, "requested_medicine", second, player);
            player.ActionPoints = Rules.BASE_ACTION_COST;
            ui.QueueKey(Keys.NumPad2);
            ui.QueueWaitKey(Keys.N);
            Check.Equal(true, Check.Call(world.Game, "HandlePlayerTalk", new[] { typeof(Actor) }, player),
                "direction selects the intended person when two are adjacent");
            Check.Equal(true, NpcIntentSupport.HasEvent(second, "request_refused"), "N declines the selected request");
            Check.Equal(0, player.ActionPoints, "N costs one action");
        });
    }
}
