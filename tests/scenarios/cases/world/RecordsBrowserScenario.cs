using System;
using System.Windows.Forms;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class RecordsBrowserScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/records-browser", () => TownScenarioFactory.Arena(4583, ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor actor = SkillScenario.Actor(world); actor.Name = "Alice"; actor.Personality = new PersonalityState();
            Session.Get.ResidentRecords.Register(actor);
            RecordsSave save = new RecordsSave("test.dat", Session.Get);
            ScenarioUI ui = (ScenarioUI)world.Game.UI;
            // Search, clear, cancelled search, sort, reverse, edit a numeric filter, empty winner, reset, winner, timeline search/category.
            foreach (Keys key in new[] { Keys.S, Keys.Z, Keys.Enter, Keys.I, Keys.Escape, Keys.R,
                Keys.S, Keys.B, Keys.Escape, Keys.O, Keys.Down, Keys.Enter, Keys.V, Keys.F,
                Keys.Down, Keys.Down, Keys.Down, Keys.Down, Keys.Enter, Keys.Back, Keys.D9, Keys.Enter, Keys.Escape,
                Keys.I, Keys.Escape, Keys.R, Keys.I, Keys.S, Keys.X, Keys.Enter, Keys.F, Keys.Down, Keys.Enter,
                Keys.R, Keys.F, Keys.PageDown, Keys.Enter, Keys.End, Keys.Home, Keys.Escape, Keys.Escape }) ui.QueueWaitKey(key);
            Check.Call(world.Game, "BrowseRecords", new[] { typeof(RecordsSave) }, save);
            string drawn = String.Join(" ", ui.DrawnStrings.ToArray());
            Check.Equal(true, drawn.Contains("No matching NPCs"), "empty filtered winner is explained");
            Check.Equal(true, drawn.Contains("Sort: Items (reversed)"), "sort and reverse controls operate");
            Check.Equal(true, drawn.Contains("Minimum items received: 9"), "numeric filter is rendered");
            Check.Equal(true, drawn.Contains("Read Records - Alice"), "most interesting button opens NPC history");
            Check.Equal(true, drawn.Contains("Interest score"), "winner explains its score");
            Check.Equal(true, drawn.Contains("Events: Memories | Text: x"), "timeline search and category combine");
            Check.Equal(true, drawn.Contains("Events: Intentions | Text: "), "new intentions category opens through the real browser");
            Check.Equal(0, Session.Get.WorldTime.TurnCounter, "browser never simulates turns");
        });
    }
}
