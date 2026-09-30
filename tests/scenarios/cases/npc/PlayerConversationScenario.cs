using System;
using System.IO;
using System.Windows.Forms;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.Personality;

static class PlayerConversationScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/player-conversation", () => TownScenarioFactory.Arena(4703,
            ".........", ".........", "........."), world =>
        {
            Session original = Session.Get;
            original.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = NpcIntentSupport.Player(world, 1, 1);
            Actor requester = NpcIntentSupport.Actor(world, "requester", 2, 1);
            Actor stranger = NpcIntentSupport.Actor(world, "stranger", 7, 1);
            RogueGame.KeyBindings.ResetToDefaults();
            Check.Equal(PlayerCommand.TALK, InputTranslator.KeyToCommand(new KeyEventArgs(Keys.V)),
                "V opens the talk command without a browser shortcut");
            player.ActionPoints = Rules.BASE_ACTION_COST;
            var illegal = new ActionPlayerTalk(player, world.Game, stranger, null);
            Check.Equal(false, illegal.IsLegal(), "talking through a distance is illegal");
            illegal.Perform();
            Check.Equal(Rules.BASE_ACTION_COST, player.ActionPoints, "invalid talk spends no AP");

            SignificantEvent request = NpcEvents.Publish(world.Game, "requested_food", requester, player);
            NpcReaction pending = NpcConversation.Pending(player, requester);
            Check.Equal(request.Id, pending.CauseId, "player receives the actual request, with its event id");
            var unanswered = new ActionPlayerTalk(player, world.Game, requester, null);
            Check.Equal(false, unanswered.IsLegal(), "request cannot be silently accepted without Y/N");
            var accept = new ActionPlayerTalk(player, world.Game, requester, true);
            Check.Equal(true, accept.IsLegal(), "adjacent player can answer request");
            accept.Perform();
            Check.Equal(0, player.ActionPoints, "answer spends one action");
            Check.Equal(null, NpcConversation.Pending(player, requester), "answered request leaves queue");
            Check.Equal(true, NpcIntentSupport.HasEvent(requester, "food_promised"), "yes creates an observable promise");
            Check.Equal(true, player.Personality.Commitments.Exists(c => c.CauseId == request.Id), "promise is tracked until delivery or deadline");
            var firstGift = NpcIntentSupport.Food(world, player, 1);
            world.Game.DoGiveItemTo(player, requester, firstGift);
            Check.Equal(true, NpcIntentSupport.HasEvent(requester, "promise_kept"),
                "actually giving food fulfils the player's promise and affects the requester");

            world.Map.LocalTime.TurnCounter = 1;
            player.ActionPoints = Rules.BASE_ACTION_COST;
            NpcEvents.Publish(world.Game, "requested_medicine", requester, player);
            new ActionPlayerTalk(player, world.Game, requester, false).Perform();
            Check.Equal(0, player.ActionPoints, "no also spends one action");
            Check.Equal(true, NpcIntentSupport.HasEvent(requester, "request_refused"), "no changes the requester's lived history");

            world.Map.LocalTime.TurnCounter = 2;
            player.ActionPoints = Rules.BASE_ACTION_COST;
            var loss = new NpcAttachment { Kind = "place", Person = player.PersonalityIdentity,
                Resource = "food", MissingUnits = 1, Place = requester.Location };
            requester.Personality.Attach(loss);
            SignificantEvent restitution = NpcEvents.Publish(world.Game, "restitution_requested", requester, player);
            new ActionPlayerTalk(player, world.Game, requester, true).Perform();
            Check.Equal(true, player.Personality.Commitments.Exists(c => c.CauseId == restitution.Id && c.Resource == "food"),
                "agreeing to restitution creates a deliverable food promise");
            var replacement = NpcIntentSupport.Food(world, player, 1);
            world.Game.DoGiveItemTo(player, requester, replacement);
            Check.Equal(0, loss.MissingUnits, "actual replacement clears the requester's missing supplies");

            world.Map.LocalTime.TurnCounter = 3;
            SignificantEvent laterRequest = NpcEvents.Publish(world.Game, "requested_medicine", requester, player);
            Check.Equal(laterRequest.Id, NpcConversation.Pending(player, requester).CauseId,
                "new requests remain available for a later answer");

            string path = Path.Combine(Path.GetTempPath(), "player-talk-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                Session loaded = BinarySaveStore.Load<Session>(path);
                Actor savedPlayer = NpcIntentSupport.Find(loaded.World[0, 0].EntryMap, player.PersonalityIdentity);
                Check.Equal(true, savedPlayer.Personality.Commitments.Exists(c => c.CauseId == request.Id),
                    "player's accepted request persists");
                Check.Equal(true, savedPlayer.Personality.Reactions.Exists(r => r.CauseId == laterRequest.Id),
                    "unanswered request persists through save and load");
            }
            finally
            {
                Session.Restore(original);
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
            world.Map.LocalTime.TurnCounter = 34;
            Check.Equal(null, NpcConversation.Pending(player, requester), "old unanswered request expires");
            GamePreset disabled = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            disabled.NpcPersonalitiesEnabled = false;
            Session.Get.GamePreset = disabled;
            NpcEvents.Publish(world.Game, "requested_food", requester, player);
            Check.Equal(null, NpcConversation.Pending(player, requester), "disabled personality system offers no social request");
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Place(stranger, 3, 1);
            world.Map.LocalTime.TurnCounter = 35;
            SignificantEvent overheard = NpcEvents.Publish(world.Game, "requested_food", requester, stranger);
            Check.Equal(true, NpcConversation.Pending(player, requester).Overheard,
                "player hears the NPC ask another person");
            player.ActionPoints = Rules.BASE_ACTION_COST;
            new ActionPlayerTalk(player, world.Game, requester, true).Perform();
            Check.Equal(0, player.ActionPoints, "answer to an overheard request spends a turn");
            Check.Equal(true, player.Personality.Commitments.Exists(c => c.CauseId == overheard.Id),
                "volunteering to help a third party creates a real linked promise");
            world.Map.LocalTime.TurnCounter = 36;
            SignificantEvent overheardMedicine = NpcEvents.Publish(world.Game, "requested_medicine", requester, stranger);
            player.ActionPoints = Rules.BASE_ACTION_COST;
            new ActionPlayerTalk(player, world.Game, requester, false).Perform();
            bool declinedOverheard = false;
            foreach (ObservedEvent entry in requester.Personality.Events)
                if (entry.Kind == "request_refused" && entry.CauseId == overheardMedicine.Id)
                    declinedOverheard = true;
            Check.Equal(true, declinedOverheard, "declining an overheard request is a causal event for its requester");
        });
    }
}
