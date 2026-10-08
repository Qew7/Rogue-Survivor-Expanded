using System;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class StoryReportCollapseScenario
{
    static int Count(string text, string phrase)
    {
        int count = 0, from = 0, at;
        while ((at = text.IndexOf(phrase, from, StringComparison.Ordinal)) >= 0)
        { count++; from = at + phrase.Length; }
        return count;
    }

    public static void Register()
    {
        ScenarioRunner.Add("npc/story-report-collapse", () => TownScenarioFactory.Arena(4636,
            "........", "........", "........"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Session.Get.Seed = 4636;
            World city = new World(1);
            city[0, 0] = world.Map.District;
            Session.Get.World = city;
            Actor player = NpcIntentSupport.Player(world, 7, 1);
            Actor speaker = NpcIntentSupport.Actor(world, "speaker", 1, 1);
            Actor listener = NpcIntentSupport.Actor(world, "listener", 2, 1);
            for (int i = 0; i < 3; i++)
                speaker.Personality.Knowledge.Facts.Add(new NpcFact {
                    EventId = 100 + i, Kind = "attack", StoryId = "chapter",
                    EventTurn = 0, Place = speaker.Location, Source = NpcKnowledgeSource.Told,
                    Confidence = 80, Hops = 1, SubjectName = i == 2 ? "Cora" : "Ada",
                    OtherName = "Bo"
                });
            NpcFact repeated = speaker.Personality.Knowledge.Facts[0];
            NpcFact distinct = speaker.Personality.Knowledge.Facts[2];
            string repeatedLine = NpcConversation.ReportSentence(world.Game, speaker, repeated);
            string distinctLine = NpcConversation.ReportSentence(world.Game, speaker, distinct);
            string distinctReport = NpcRecordDescriptions.Report(world.Game.NpcContent, distinct);
            Check.Equal(false, repeatedLine == distinctLine, "different reports remain distinct");

            RadioProgram radio = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 3, 0);
            Check.Equal(3, radio.Facts.Length, "radio retains all underlying events");
            Check.Equal(1, Count(radio.Text, repeatedLine), "radio says repeated report once");
            Check.Equal(1, Count(radio.Text, distinctReport), "radio keeps different report");

            NpcConversation.ShareRumor(world.Game, speaker, listener, repeated, true);
            string heard = player.Personality.HeardJournal.Last().Text;
            Check.Equal(1, Count(heard, repeatedLine), "spoken chapter says repeated report once");
            Check.Equal(1, Count(heard, distinctReport), "spoken chapter keeps different report");
            Check.Equal(3, listener.Personality.Knowledge.Facts.Count(f => f.StoryId == "chapter"),
                "listener still learns all distinct events");
        });
    }
}
