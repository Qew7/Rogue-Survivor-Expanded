using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class StalePlayerReplyScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/stale-player-reply", () => TownScenarioFactory.Arena(4821,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = NpcIntentSupport.Player(world, 1, 1);
            Actor target = NpcIntentSupport.Actor(world, "target", 2, 1);
            var missing = new NpcReaction(target, "old request", 1, 0, "removed_event");
            player.Personality.Reactions.Add(missing);
            NpcConversation.Reply(world.Game, player, target, missing, true);
            Check.Equal(false, player.Personality.Reactions.Contains(missing), "removed event clears a saved reply without crashing");

            var noReply = new NpcReaction(target, "old request", 2, 0, "rumor_shared");
            player.Personality.Reactions.Add(noReply);
            Check.Equal(true, world.Game.NpcContent.Event(noReply.Kind) != null, "boundary event exists in the catalog");
            Check.Equal(null, world.Game.NpcContent.Event(noReply.Kind).PlayerReply, "boundary event has no reply definition");
            NpcConversation.Reply(world.Game, player, target, noReply, false);
            Check.Equal(false, player.Personality.Reactions.Contains(noReply), "event without a reply clears its saved prompt");

            var staleAtInput = new NpcReaction(target, "old request", 3, 0, "removed_event");
            player.Personality.Reactions.Add(staleAtInput);
            player.ActionPoints = Rules.BASE_ACTION_COST;
            Check.Equal(true, Check.Call(world.Game, "HandlePlayerTalk", new[] { typeof(Actor) }, player),
                "talking after content changes skips the obsolete Y/N prompt");
            Check.Equal(false, player.Personality.Reactions.Contains(staleAtInput), "talk input discards obsolete request");
        });
    }
}
