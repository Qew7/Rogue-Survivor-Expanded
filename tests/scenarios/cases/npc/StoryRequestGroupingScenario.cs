using System;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class StoryRequestGroupingScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/story-request-grouping", () => TownScenarioFactory.Arena(4642,
            "................", "................", "................"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Session.Get.Seed = 4642;
            World city = new World(1);
            city[0, 0] = world.Map.District;
            Session.Get.World = city;
            Actor player = NpcIntentSupport.Player(world, 15, 1);
            Actor teller = NpcIntentSupport.Actor(world, "teller", 0, 1);
            Actor mara = NpcIntentSupport.Actor(world, "Mara", 1, 1);
            Actor[] recipients = {
                NpcIntentSupport.Actor(world, "Juan", 2, 1),
                NpcIntentSupport.Actor(world, "Harry", 3, 1),
                NpcIntentSupport.Actor(world, "Gilbert", 4, 1) };
            Actor nina = NpcIntentSupport.Actor(world, "Nina", 5, 1);
            teller.Personality.Opinion(mara.PersonalityIdentity, mara.UnmodifiedName);
            teller.Personality.Opinion(nina.PersonalityIdentity, nina.UnmodifiedName);
            foreach (Actor recipient in recipients)
                teller.Personality.Opinion(recipient.PersonalityIdentity, recipient.UnmodifiedName);

            for (int i = 0; i < recipients.Length; i++)
            {
                world.Map.LocalTime.TurnCounter = i;
                NpcEvents.Publish(world.Game, "requested_medicine", mara, recipients[i], story: "medicine-search");
            }
            world.Map.LocalTime.TurnCounter = 3;
            NpcEvents.Publish(world.Game, "requested_medicine", nina, recipients[0], story: "medicine-search");
            world.Map.LocalTime.TurnCounter = 4;
            SignificantEvent promise = NpcEvents.Publish(world.Game, "medicine_promised", mara, recipients[0],
                story: "medicine-search");
            world.Map.LocalTime.TurnCounter = 5;
            NpcEvents.Publish(world.Game, "promise_kept", mara, recipients[0], promise.Id, "medicine-search");
            NpcFact first = teller.Personality.Knowledge.Facts.First(f => f.Kind == "requested_medicine" &&
                f.SubjectId == mara.PersonalityIdentity);
            Actor relay = NpcIntentSupport.Actor(world, "relay", 0, 2);
            NpcConversation.ShareRumor(world.Game, teller, relay, first, true);
            Check.Equal(6, relay.Personality.Knowledge.Facts.Count(f => f.StoryId == "medicine-search"),
                "nearby relay learns the full chapter");
            foreach (Actor actor in world.Map.Actors)
                if (actor != relay && actor != teller)
                    actor.Personality.Knowledge.Facts.RemoveAll(f => f.Source == NpcKnowledgeSource.Told);
            NpcFact relayed = relay.Personality.Knowledge.Facts.First(f => f.Kind == "requested_medicine");
            Check.Equal(true, relayed.Source == NpcKnowledgeSource.Told && relayed.Confidence >= 40 &&
                relayed.Hops < 3 && NpcConversation.CanTell(world.Game, relay, relayed),
                "relay has an eligible radio source");
            Session.Get.WorldTime.TurnCounter = WorldTime.TURNS_PER_HOUR + 5;
            RadioProgram radio = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 2, 1);
            Check.Equal(true, radio.Facts != null, "local calls selects a news chapter: " + radio.Text);
            Check.Equal(6, radio.Facts.Length, "radio retains all requests and their outcome");
            Check.Equal(true, radio.Text.Contains("Mara asked Juan, Harry, and Gilbert for medicine"),
                "radio groups one person's distinct requests");
            Check.Equal(true, radio.Text.Contains("Nina asked"), "another requester remains separate");
            Check.Equal(true, radio.Text.Contains(" In the end, "), "known outcome closes the episode");

            Actor listener = NpcIntentSupport.Actor(world, "listener", 14, 1);
            world.Place(teller, 13, 1);
            NpcConversation.ShareRumor(world.Game, teller, listener, first, true);
            string speech = player.Personality.HeardJournal.Last().Text;
            Check.Equal(true, speech.Contains("Mara asked Juan, Harry, and Gilbert for medicine"),
                "spoken story uses the same grouping: " + speech);
            Check.Equal(true, speech.Contains("Nina asked") && speech.Contains(" In the end, "),
                "different actor and outcome remain visible");
            Check.Equal(6, listener.Personality.Knowledge.Facts.Count(f => f.StoryId == "medicine-search" &&
                f.Kind != "rumor_shared"), "listener learns every underlying event");
        });
    }
}
