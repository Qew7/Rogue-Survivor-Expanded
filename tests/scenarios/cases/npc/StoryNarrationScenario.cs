using System;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class StoryNarrationScenario
{
    static int Count(string text, string phrase)
    {
        int count = 0, from = 0, at;
        while ((at = text.IndexOf(phrase, from, StringComparison.Ordinal)) >= 0)
        { count++; from = at + phrase.Length; }
        return count;
    }

    static void CheckStory(string text, string place)
    {
        Check.Equal(1, Count(text, "I was told that"), "repeated hearsay introduction is omitted");
        Check.Equal(1, Count(text, "I saw that"), "changed evidence source remains explicit");
        Check.Equal(1, Count(text, place), "shared location is stated once");
        Check.Equal(true, text.Contains(" After that, "), "first later event has a transition");
        Check.Equal(true, text.Contains(" Later, "), "later events use varied transitions");
        Check.Equal(true, text.Contains(" Around the same time, "), "same-turn event has a neutral transition");
        Check.Equal(false, text.Contains(" Then I "), "chapter does not repeat the old formula");
    }

    public static void Register()
    {
        ScenarioRunner.Add("npc/story-narration", () => TownScenarioFactory.Arena(4637,
            "........", "........", "........"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Session.Get.Seed = 4637;
            World city = new World(1);
            city[0, 0] = world.Map.District;
            Session.Get.World = city;
            Actor player = NpcIntentSupport.Player(world, 7, 1);
            Actor speaker = NpcIntentSupport.Actor(world, "speaker", 1, 1);
            Actor listener = NpcIntentSupport.Actor(world, "listener", 2, 1);
            for (int i = 0; i < 4; i++)
                speaker.Personality.Knowledge.Facts.Add(new NpcFact {
                    EventId = 200 + i, Kind = "attack", StoryId = "narration",
                    EventTurn = i == 3 ? 2 : i, Place = speaker.Location,
                    Source = i == 3 ? NpcKnowledgeSource.Witness : NpcKnowledgeSource.Told,
                    Confidence = 80, Hops = 1, SubjectName = "Person" + i, OtherName = "Bo"
                });
            string place = "in district " + World.CoordToString(world.Map.District.WorldPosition.X,
                world.Map.District.WorldPosition.Y);
            Session.Get.WorldTime.TurnCounter = WorldTime.TURNS_PER_DAY;
            RadioProgram radio = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 3, 24);
            Check.Equal(4, radio.Facts.Length, "radio retains every event in the chapter");
            CheckStory(radio.Text, place);

            NpcConversation.ShareRumor(world.Game, speaker, listener,
                speaker.Personality.Knowledge.Facts[0], true);
            CheckStory(player.Personality.HeardJournal.Last().Text, place);
            Check.Equal(4, listener.Personality.Knowledge.Facts.Count(f => f.StoryId == "narration"),
                "listener learns every event despite shorter narration");
        });
    }
}
