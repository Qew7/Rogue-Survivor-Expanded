using System;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class StoryEditorialBeatsScenario
{
    sealed class SignalStory : INpcContentModule
    {
        public string Id { get { return "scenario.signal-story"; } }

        public void Register(NpcCatalogBuilder catalog)
        {
            Add(catalog, "signal_seen", "noticed a light near the road");
            Add(catalog, "signal_walked", "walked toward the road");
            Add(catalog, "signal_waited", "waited beside the road");
            Add(catalog, "signal_detour", "changed their route");
            Add(catalog, "signal_failed", "found that the signal had gone dark", priority: 8);
            Add(catalog, "signal_help", "asked for help");
            Add(catalog, "signal_returned", "returned with help", conclusion: true);
            Add(catalog, "unrelated_signal", "heard a different signal");
        }

        static void Add(NpcCatalogBuilder catalog, string kind, string action, int priority = 0,
            bool conclusion = false)
        {
            catalog.Event(new NpcEventDefinition(kind, NpcRecordCategory.World, true,
                e => (e.Subject ?? "Someone") + " " + action + ".",
                f => (f.ReportSubject ?? "Someone") + " " + action)
            { ReportPriority = priority, ReportConclusion = conclusion });
        }
    }

    static int Count(string text, string pattern)
    {
        int count = 0, at = 0, found;
        while ((found = text.IndexOf(pattern, at, StringComparison.Ordinal)) >= 0)
        { count++; at = found + pattern.Length; }
        return count;
    }

    public static void Register()
    {
        ScenarioRunner.Add("npc/story-editorial-beats", () => TownScenarioFactory.Arena(4636,
            "....#.....", "....#.....", "....#....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            world.Game.NpcContent = PersonalityContent.Create(new SignalStory()).Content;
            Actor mara = NpcIntentSupport.Actor(world, "Mara", 2, 1);
            Actor witness = NpcIntentSupport.Actor(world, "witness", 1, 1);
            witness.Personality.Opinion(mara.PersonalityIdentity, mara.UnmodifiedName);
            Actor relay = NpcIntentSupport.Actor(world, "relay", 9, 1);
            Actor player = NpcIntentSupport.Player(world, 8, 1);
            player.AudioRangeMod = -20;
            const string story = "signal-story";
            string[] kinds = { "signal_seen", "signal_walked", "signal_waited", "signal_detour",
                "signal_failed", "signal_help", "signal_returned" };
            SignificantEvent[] events = new SignificantEvent[kinds.Length];
            for (int i = 0; i < kinds.Length; i++)
            {
                world.Map.LocalTime.TurnCounter = i;
                long cause = i == 5 ? events[4].Id : i == 6 ? events[5].Id : 0;
                events[i] = NpcEvents.Publish(world.Game, kinds[i], mara, null, cause, story);
            }
            NpcEvents.Publish(world.Game, "unrelated_signal", mara, null, story: "other-story");
            NpcFact first = witness.Personality.Knowledge.Facts.First(f => f.EventId == events[0].Id);
            Check.Equal(0, player.Personality.Knowledge.Facts.Count,
                "wall keeps the player from learning the events directly");
            world.Place(relay, 0, 1);
            NpcConversation.ShareRumor(world.Game, witness, relay, first, true);
            Check.Equal(7, relay.Personality.Knowledge.Facts.Count(f => f.StoryId == story),
                "relay receives every fact even when the text will be shorter");

            Session.Get.WorldTime.TurnCounter = WorldTime.TURNS_PER_DAY;
            RadioProgram program = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 0, 24);
            Check.Equal(7, program.Facts.Length, "radio retains every fact for its listeners");
            Check.Equal(true, program.Text.Contains("noticed a light") &&
                program.Text.Contains("signal had gone dark") &&
                program.Text.Contains("asked for help") && program.Text.Contains("returned with help"),
                "registered future events form a setup, reversal, response and outcome: " + program.Text);
            Check.Equal(false, program.Text.Contains("waited beside") || program.Text.Contains("changed their route"),
                "routine beats do not crowd the bulletin");
            Check.Equal(true, program.Text.Contains("A secondhand report says that") &&
                !program.Text.Contains("I saw that"), "radio uses attributed broadcaster wording");
            Check.Equal(true, program.Text.Contains("they found that the signal had gone dark"),
                "known subject is named once, then referred to without repetition: " + program.Text);
            Check.Equal(4, Count(program.Text, "."),
                "short chapter includes at most four visible event sentences");

            player.AudioRangeMod = 0;
            world.Place(witness, 7, 1);
            NpcConversation.ShareRumor(world.Game, witness, player, first, true);
            string spoken = player.Personality.HeardJournal.Last().Text;
            Check.Equal(true, spoken.Contains("I saw that") && spoken.Contains("signal had gone dark") &&
                spoken.Contains("returned with help"), "direct speech keeps the witness's own voice");
            Check.Equal(false, spoken.Contains("waited beside") || spoken.Contains("changed their route"),
                "the same editor removes routine beats from conversation");
            Check.Equal(7, player.Personality.Knowledge.Facts.Count(f => f.StoryId == story),
                "the listener still learns all seven underlying events");
            Check.Equal(false, player.Personality.Knowledge.Facts.Any(f => f.StoryId == "other-story"),
                "an unrelated event cannot enter the chapter");
        });
    }
}
