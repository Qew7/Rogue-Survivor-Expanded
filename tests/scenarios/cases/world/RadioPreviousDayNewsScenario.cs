using System;
using System.Drawing;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class RadioPreviousDayNewsScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/radio-previous-day-news", () => TownScenarioFactory.Arena(4636,
            "........", "........", "........"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Session.Get.Seed = 4636;
            Actor source = NpcIntentSupport.Actor(world, "source", 2, 1);
            source.Personality.Knowledge.Learn(new NpcFact {
                EventId = 901, Kind = "shared_food", StoryId = "daily-chapter",
                EventTurn = 0, LearnedTurn = 0, Place = source.Location,
                Source = NpcKnowledgeSource.Told, Confidence = 80, Hops = 1,
                SubjectName = "Ada"
            });

            RadioProgram firstDay = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 0, 0);
            Check.Equal(null, firstDay.Facts, "there is no previous-day report on the first day");

            Session.Get.WorldTime.TurnCounter = WorldTime.TURNS_PER_DAY;
            RadioProgram yesterday = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 0, 24);
            Check.Equal(1, yesterday.Facts.Length, "yesterday's known event enters the next day's bulletin");
            Check.Equal(901L, yesterday.EventId, "the bulletin names the actual earlier event");

            int newTurn = WorldTime.TURNS_PER_DAY + WorldTime.TURNS_PER_HOUR;
            source.Personality.Knowledge.Learn(new NpcFact {
                EventId = 902, Kind = "shared_food", StoryId = "daily-chapter",
                EventTurn = newTurn, LearnedTurn = newTurn, Place = source.Location,
                Source = NpcKnowledgeSource.Told, Confidence = 80, Hops = 1,
                SubjectName = "Ben"
            });
            Session.Get.WorldTime.TurnCounter = 30 * WorldTime.TURNS_PER_HOUR;
            RadioProgram stillYesterday = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 0, 30);
            Check.Equal(1, stillYesterday.Facts.Length,
                "a new rumor cannot leak into a later chapter on the same day");
            Check.Equal(901L, stillYesterday.Facts[0].EventId, "the old story remains available");

            Session.Get.RadioDropDistrict = new Point(0, 0);
            Session.Get.RadioDropTurn = 3 * WorldTime.TURNS_PER_DAY;
            Session.Get.WorldTime.TurnCounter = 32 * WorldTime.TURNS_PER_HOUR;
            RadioProgram forecast = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 1, 32);
            Check.Equal(3 * WorldTime.TURNS_PER_DAY, forecast.ForecastTurn,
                "military flight announcement uses the current schedule");
            Session.Get.RadioDropTurn = 2000;
            Session.Get.WorldTime.TurnCounter = 38 * WorldTime.TURNS_PER_HOUR;
            RadioProgram revisedForecast = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 1, 38);
            Check.Equal(2000, revisedForecast.ForecastTurn,
                "a revised flight schedule reaches the next hourly dispatch without waiting for midnight");

            Session.Get.WorldTime.TurnCounter = 2 * WorldTime.TURNS_PER_DAY;
            RadioProgram continuation = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 0, 48);
            Check.Equal(2, continuation.Facts.Length,
                "the new chapter joins the following day's bulletin with its earlier context");
            Check.Equal(true, Array.Exists(continuation.Facts, f => f.EventId == 902),
                "the new report appears after midnight");
            source.IsDead = true;
            Session.Get.WorldTime.TurnCounter = 54 * WorldTime.TURNS_PER_HOUR;
            RadioProgram withoutSource = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 0, 54);
            Check.Equal(null, withoutSource.Facts, "a dead source cannot voice an archived chapter");
        });
    }
}
