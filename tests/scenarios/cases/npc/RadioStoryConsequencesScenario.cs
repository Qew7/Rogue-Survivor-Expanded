using System;
using System.Collections.Generic;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Gameplay.Personality;

static class RadioStoryConsequencesScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/radio-story-consequences", () => TownScenarioFactory.Arena(4646,
            "....#.....", "....#.....", "....#....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Session.Get.Seed = 4646;
            World city = new World(1);
            city[0, 0] = world.Map.District;
            Session.Get.World = city;
            Actor player = NpcIntentSupport.Player(world, 9, 1);
            player.AudioRangeMod = -20;
            Actor ada = NpcIntentSupport.Actor(world, "Ada", 0, 1);
            Actor boris = NpcIntentSupport.Actor(world, "Boris", 1, 1);
            Actor clara = NpcIntentSupport.Actor(world, "Clara", 2, 1);
            Actor dylan = NpcIntentSupport.Actor(world, "Dylan", 3, 1);
            Actor witness = NpcIntentSupport.Actor(world, "witness", 1, 2);
            Actor relay = NpcIntentSupport.Actor(world, "relay", 6, 1);
            Actor listener = NpcIntentSupport.Actor(world, "listener", 8, 1);
            foreach (Actor person in new[] { ada, boris, clara, dylan })
                witness.Personality.Opinion(person.PersonalityIdentity, person.UnmodifiedName);

            SignificantEvent first = NpcEvents.Publish(world.Game, "requested_medicine", ada, boris,
                story: "search");
            world.Map.LocalTime.TurnCounter = 1;
            SignificantEvent second = NpcEvents.Publish(world.Game, "requested_medicine", boris, clara,
                first.Id, "search");
            world.Map.LocalTime.TurnCounter = 2;
            SignificantEvent third = NpcEvents.Publish(world.Game, "requested_medicine", clara, dylan,
                second.Id, "search");
            world.Map.LocalTime.TurnCounter = 3;
            SignificantEvent delivery = NpcEvents.Publish(world.Game, "shared_medicine", dylan, clara,
                third.Id, "search");
            Check.Equal(third.Id, witness.Personality.Knowledge.Facts.First(f => f.EventId == delivery.Id).CauseId,
                "the witness retains the real link to the delivery");

            world.Place(witness, 5, 1);
            NpcConversation.ShareRumor(world.Game, witness, relay,
                witness.Personality.Knowledge.Facts.First(f => f.EventId == delivery.Id), true);
            Check.Equal(4, relay.Personality.Knowledge.Facts.Count(f => f.StoryId == "search"),
                "the relay knows the complete episode");
            Session.Get.WorldTime.TurnCounter = 27 * WorldTime.TURNS_PER_HOUR;
            world.Map.LocalTime.TurnCounter = Session.Get.WorldTime.TurnCounter;
            RadioProgram program = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 2, 27);
            Check.Equal(true, program.Facts != null && program.Facts.Length == 4,
                "the radio retains every known fact for listeners");
            Check.Equal(true, program.Text.Contains("Clara asked a civilian for medicine") &&
                program.Text.Contains("Dylan gave medicine to Clara"),
                "the bulletin reports the need and its known outcome without inventing the request recipient: " + program.Text);
            Check.Equal(false, program.Text.Contains("Ada asked") || program.Text.Contains("Boris asked") ||
                program.Text.Contains("A secondhand report says that"),
                "the bulletin omits the relay chain and duplicate source lead: " + program.Text);
            Check.Equal(true, program.Text.StartsWith("A caller relays: ", StringComparison.Ordinal),
                "the caller's secondhand evidence remains attributed");

            Check.Call(world.Game, "BroadcastRadio",
                new[] { typeof(int), typeof(Map), typeof(System.Drawing.Point), typeof(Actor) },
                2, world.Map, listener.Location.Position, listener);
            NpcKnownPerson known = listener.Personality.Knowledge.Person(clara.PersonalityIdentity);
            Check.Equal(true, known != null && known.MedicalNeed == 0 && known.MedicalTurn == 3,
                "the listener updates the reported need after hearing the later delivery");
            Check.Call(world.Game, "BroadcastRadio",
                new[] { typeof(int), typeof(Map), typeof(System.Drawing.Point), typeof(Actor) },
                2, world.Map, player.Location.Position, player);
            Check.Equal(program.Text, player.Personality.HeardJournal.Last().Text,
                "the player's journal keeps the same edited bulletin");

            Session.Get.WorldTime.TurnCounter = 30 * WorldTime.TURNS_PER_HOUR;
            world.Map.LocalTime.TurnCounter = Session.Get.WorldTime.TurnCounter;
            RadioProgram interlude = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 2, 30);
            Check.Equal(null, interlude.Facts, "the scheduled station interlude remains separate from stories");
            Check.Call(world.Game, "BroadcastRadio",
                new[] { typeof(int), typeof(Map), typeof(System.Drawing.Point), typeof(Actor) },
                2, world.Map, player.Location.Position, player);

            world.Place(witness, 1, 2);
            world.Map.LocalTime.TurnCounter++;
            SignificantEvent unrelatedNews = NpcEvents.Publish(world.Game, "traded", ada, boris,
                story: "other-news");
            Check.Equal(true, witness.Personality.Knowledge.Facts.Any(f => f.EventId == unrelatedNews.Id),
                "the witness also knows a newer unrelated event");
            world.Place(witness, 8, 2);
            player.AudioRangeMod = 0;
            NpcFact followUp = NpcConversation.Rumor(world.Game, witness, player);
            Check.Equal("other-news", followUp.StoryId,
                "the witness no longer retells a story learned over a day ago");
            player.ActionPoints = Rules.BASE_ACTION_COST;
            new ActionPlayerTalk(player, world.Game, witness, null).Perform();
            Check.Equal(0, player.ActionPoints, "asking the witness spends the usual talk action");
            Check.Equal("heard_rumor", player.Personality.HeardJournal.Last().Kind,
                "the witness's fresh account is saved after the radio bulletin");

            NpcFact orphan = witness.Personality.Knowledge.Facts.First(f => f.EventId == first.Id).Retell(
                witness.PersonalityIdentity, 4, 70);
            orphan.CauseId = 999999;
            orphan.SubjectId = Guid.NewGuid();
            orphan.OtherId = Guid.NewGuid();
            List<NpcFact> unrelated = (List<NpcFact>)Check.Call(world.Game, "RadioStoryBeats",
                new List<NpcFact> { orphan, relay.Personality.Knowledge.Facts.First(f => f.EventId == delivery.Id) });
            Check.Equal(1, unrelated.Count,
                "an unrelated report with a missing cause cannot become the delivery's setup");

            SignificantEvent promise = NpcEvents.Publish(world.Game, "medicine_promised", dylan, clara,
                story: "deadline-phrasing");
            NpcFact promiseFact = dylan.Personality.Knowledge.Facts.First(f => f.EventId == promise.Id);
            string promiseReport = NpcConversation.ReportSentence(world.Game, dylan, promiseFact, true);
            Check.Equal(true, promiseReport.Contains("within six hours") &&
                !promiseReport.Contains("180 turns"),
                "story reports express the real deadline in world time");
        });
    }
}
