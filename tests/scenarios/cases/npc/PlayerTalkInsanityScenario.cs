using System.Reflection;
using System.Windows.Forms;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class PlayerTalkInsanityScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/player-talk-insanity", () => TownScenarioFactory.Arena(4822,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = NpcIntentSupport.Player(world, 1, 1);
            Actor requester = NpcIntentSupport.Actor(world, "requester", 2, 1);
            NpcEvents.Publish(world.Game, "requested_food", requester, player);
            NpcReaction pending = NpcConversation.Pending(player, requester);
            Check.Equal(true, pending != null, "fixture offers a request before the player acts");
            player.Sanity = 0;
            player.ActionPoints = Rules.BASE_ACTION_COST;
            typeof(RogueGame).GetField("m_Rules", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(world.Game, new Rules(new DiceRoller(85)));
            ScenarioUI ui = (ScenarioUI)world.Game.UI;
            ui.QueueKey(Keys.V);
            ui.QueueWaitKey(Keys.Enter);
            Check.Call(world.Game, "HandlePlayerActor", new[] { typeof(Actor) }, player);
            Check.Equal(true, player.Personality.Reactions.Contains(pending),
                "an involuntary action interrupts talk before the request is answered");
            Check.Equal(false, NpcIntentSupport.HasEvent(requester, "food_promised"),
                "insanity cannot silently accept the request");
            Check.Equal(0, player.ActionPoints, "the involuntary shout consumes the player's turn");
        });
    }
}
