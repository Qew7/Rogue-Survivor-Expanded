using System;
using System.Drawing;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Gameplay.Personality;

static class StoryDisputedAccountsScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/story-disputed-accounts", () => TownScenarioFactory.Arena(4643,
            "..................", "..................", ".................."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Session.Get.Seed = 4643;
            World city = new World(1);
            city[0, 0] = world.Map.District;
            Session.Get.World = city;
            Actor player = NpcIntentSupport.Player(world, 17, 2);
            Actor thief = NpcIntentSupport.Actor(world, "thief", 1, 1, "deceptive", "sociable");
            Actor owner = NpcIntentSupport.Actor(world, "owner", 3, 1, "lawful");
            Actor witness = NpcIntentSupport.Actor(world, "witness", 1, 2, "suspicious");
            Point at = new Point(2, 2);
            world.Map.AddXpdBase(new XpdBase(owner, new[] { at }));
            var food = new ItemFood(world.Game.GameItems.CANNED_FOOD);
            world.Map.DropItemAt(food, at);
            world.Game.DoTakeItem(thief, at, food, storyId: "disputed-theft");
            NpcFact theft = witness.Personality.Knowledge.Facts.First(f => f.Kind == "base_theft");
            NpcFact claim = thief.Personality.Knowledge.Facts.First(f => f.Kind == "claimed_permission");
            Check.Equal(theft.StoryId, claim.StoryId, "permission claim stays in the theft story");
            Actor newcomer = NpcIntentSupport.Actor(world, "newcomer", 4, 1);
            world.Map.LocalTime.TurnCounter = 1;
            Check.Equal(true, world.Try(new ActionNpcTell(thief, world.Game, newcomer, claim)),
                "thief makes the conflicting claim aloud");
            NpcFact refutation = witness.Personality.Knowledge.Facts.First(f => f.Kind == "false_testimony_exposed");
            Check.Equal(theft.StoryId, refutation.StoryId, "eyewitness refutation joins the same story");
            Check.Equal(false, NpcConversation.CanTell(world.Game, witness,
                witness.Personality.Knowledge.Facts.First(f => f.Kind == "claimed_permission")),
                "eyewitness cannot pass the refuted claim as truth");

            Actor relay = NpcIntentSupport.Actor(world, "relay", 1, 0);
            NpcConversation.ShareRumor(world.Game, witness, relay, theft, true);
            Check.Equal(true, relay.Personality.Knowledge.Facts.Any(f => f.Kind == "base_theft") &&
                relay.Personality.Knowledge.Facts.Any(f => f.Kind == "false_testimony_exposed"),
                "relay receives the real theft and its refutation");
            foreach (Actor actor in world.Map.Actors)
                if (actor != relay && actor != witness)
                    actor.Personality.Knowledge.Facts.RemoveAll(f => f.Source == NpcKnowledgeSource.Told);
            player.Personality.Knowledge.Facts.Clear();
            RadioProgram radio = null;
            for (int slot = 24; slot < 30; slot++)
            {
                Session.Get.WorldTime.TurnCounter = slot * WorldTime.TURNS_PER_HOUR + 1;
                radio = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 0, slot);
                if (radio.Facts != null) break;
            }
            Check.Equal(true, radio.Facts != null && radio.Text.Contains(" However, ") &&
                radio.Text.Contains("contradicted by an eyewitness"),
                "radio distinguishes the disputed account from a proven theft: " + radio.Text);

            Actor listener = NpcIntentSupport.Actor(world, "listener", 16, 2);
            world.Place(witness, 15, 2);
            NpcConversation.ShareRumor(world.Game, witness, listener, theft, true);
            string speech = player.Personality.HeardJournal.Last().Text;
            Check.Equal(true, speech.Contains(" However, ") &&
                speech.Contains("contradicted by an eyewitness"),
                "spoken version also identifies the real contradiction");
            Check.Equal(false, listener.Personality.Knowledge.Facts.Any(f => f.Kind == "claimed_permission"),
                "honest retelling does not transmit the refuted claim as a fact");
        });
    }
}
