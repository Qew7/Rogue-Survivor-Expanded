using System;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class RadioStoryContinuationScenario
{
    static NpcFact Fact(Actor source, long id, string kind, string story, string name, int turn = -1)
    {
        int when = turn < 0 ? (int)id : turn;
        return new NpcFact { EventId = id, Kind = kind, StoryId = story, EventTurn = when, LearnedTurn = when,
            Place = source.Location, Source = NpcKnowledgeSource.Told, Confidence = 80, Hops = 1,
            SubjectName = name, OtherName = "Bo" };
    }

    public static void Register()
    {
        ScenarioRunner.Add("npc/radio-story-continuation", () => TownScenarioFactory.Arena(4644,
            "........", "........", "........"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Session.Get.Seed = 4644;
            World city = new World(1);
            city[0, 0] = world.Map.District;
            Session.Get.World = city;
            Actor player = NpcIntentSupport.Player(world, 2, 1);
            Actor source = NpcIntentSupport.Actor(world, "source", 6, 1);
            NpcFact old = Fact(source, 10, "shared_food", "familiar", "Ada");
            NpcFact news = Fact(source, 11, "shared_food", "familiar", "Ben");
            source.Personality.Knowledge.Facts.Add(old);
            source.Personality.Knowledge.Facts.Add(news);
            player.Personality.Knowledge.Facts.Add(old.Retell(source.PersonalityIdentity, 11, 60));

            Session.Get.WorldTime.TurnCounter = 26 * WorldTime.TURNS_PER_HOUR;
            RadioProgram update = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 0, 26);
            Check.Equal(2, update.Facts.Length, "broadcast retains the complete episode");
            Check.Equal(true, update.Text.StartsWith("Follow-up from survivors: ", StringComparison.Ordinal) &&
                update.Text.Contains("Earlier reports said that Ada") && update.Text.Contains("Ben"),
                "familiar story gives one factual reminder before the new development");
            world.Map.LocalTime.TurnCounter = Session.Get.WorldTime.TurnCounter;
            Check.Call(world.Game, "BroadcastRadio",
                new[] { typeof(int), typeof(Map), typeof(System.Drawing.Point), typeof(Actor) },
                0, world.Map, player.Location.Position, player);
            Check.Equal(true, player.Personality.Knowledge.Facts.Any(f => f.EventId == news.EventId),
                "radio still transmits the new fact");
            Check.Equal(update.Text, player.Personality.HeardJournal.Last().Text,
                "journal keeps the concise continuation");

            Session.Get.WorldTime.TurnCounter = 27 * WorldTime.TURNS_PER_HOUR;
            RadioProgram repeated = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 0, 27);
            Check.Equal(true, repeated.Text.Contains("No new details on that story.") &&
                !repeated.Text.Contains("Ada") && !repeated.Text.Contains("Ben"),
                "later broadcast does not replay an unchanged story");
            Check.Equal(2, repeated.Facts.Length, "unchanged broadcast retains its underlying facts");

            source.Personality.Knowledge.Facts.Clear();
            source.Personality.Knowledge.Facts.Add(Fact(source, 12, "army_supplies", "dispatch", "Cy", 732));
            source.Personality.Knowledge.Facts.Add(Fact(source, 13, "requested_medicine", "call", "Di", 733));
            source.Personality.Knowledge.Facts.Add(Fact(source, 14, "attack", "street", "Eli", 734));
            string[] leads = { null, "Situation report: ", "A caller says: ", "Word on the street: " };
            for (int station = 1; station < 4; station++)
            {
                RadioProgram program = null;
                for (int slot = 52; slot < 58; slot++)
                {
                    Session.Get.WorldTime.TurnCounter = slot * WorldTime.TURNS_PER_HOUR;
                    program = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", station, slot);
                    if (program.Facts != null) break;
                }
                Check.Equal(true, program.Facts != null &&
                    program.Text.StartsWith(leads[station], StringComparison.Ordinal),
                    "station " + station + " uses its own report voice");
            }
        });
    }
}
