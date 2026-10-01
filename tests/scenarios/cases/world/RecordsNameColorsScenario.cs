using System;
using System.Drawing;
using System.Windows.Forms;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class RecordsNameColorsScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/records-name-colors", () => TownScenarioFactory.Arena(4830,
            "......", "......"), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor ada = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "Ada", false, false, 0);
            Actor bo = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheBikers, "Bo", false, false, 0);
            world.Place(ada, 1, 0); world.Place(bo, 2, 0);
            ResidentRecord record = Session.Get.ResidentRecords.Register(ada);
            Session.Get.ResidentRecords.Register(bo);
            record.Add("test", 0, "Ada met Bo at the grocery store.");

            Actor firstLee = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "Lee", false, false, 0);
            Actor secondLee = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheBikers, "Lee", false, false, 0);
            world.Place(firstLee, 3, 0); world.Place(secondLee, 4, 0);
            Session.Get.ResidentRecords.Register(firstLee);
            Session.Get.ResidentRecords.Register(secondLee);

            var names = new RecordsTextColors(Session.Get.ResidentRecords.Residents);
            string line = "Ada met Bo. Adaline and Bored stayed elsewhere.";
            var runs = names.Runs(line, Color.White);
            Check.Equal("Ada", line.Substring(runs[0].Start, runs[0].Length), "first name is recognized");
            Check.Equal(Color.LightSkyBlue, runs[0].Color, "civilian name uses its faction color");
            Check.Equal(true, HasRun(line, runs, "Bo", Color.Orange), "biker name uses another color");
            Check.Equal(false, HasRun(line, runs, "Ada", Color.LightSkyBlue, 12),
                "names embedded in longer words are not colored");
            Check.Equal(Color.LightGray, names.Runs("Lee", Color.White)[0].Color,
                "the same name in different factions gets an unambiguous neutral color");
            string place = "at the apartment building.";
            var placeRuns = names.Runs(place, Color.White);
            int buildingStart = -1;
            foreach (RecordsTextRun run in placeRuns)
                if (place.Substring(run.Start, run.Length) == "apartment building") buildingStart = run.Start;
            Check.Equal("at the ".Length, buildingStart, "the building follows a space");
            Check.Equal(8 * buildingStart,
                RecordsTextColors.PrefixWidth(place, buildingStart, value => value.TrimEnd().Length * 8),
                "a trailing-space-dropping text measurer keeps the gap before the colored building");

            ScenarioUI ui = (ScenarioUI)world.Game.UI;
            foreach (Keys key in new[] { Keys.Enter, Keys.Escape, Keys.Escape }) ui.QueueWaitKey(key);
            Check.Call(world.Game, "BrowseRecords", new[] { typeof(RecordsSave) }, new RecordsSave("test.dat", Session.Get));
            Check.Equal(true, ui.DrawnBold.Exists(draw => draw.Item1 == Color.LightSkyBlue && draw.Item2 == "Ada"),
                "the real Read Records screen draws the civilian name in color");
            Check.Equal(true, ui.DrawnBold.Exists(draw => draw.Item1 == Color.Orange && draw.Item2 == "Bo"),
                "the real Read Records screen draws the biker name in color");
            Check.Equal(true, ui.DrawnBold.Exists(draw => draw.Item1 == Zone.FoodStoreColor &&
                draw.Item2 == "grocery store"), "the real Read Records screen draws the building in color");
        });
    }

    static bool HasRun(string line, System.Collections.Generic.IList<RecordsTextRun> runs,
        string name, Color color, int after = 0)
    {
        foreach (RecordsTextRun run in runs)
            if (run.Start >= after && run.Color == color && line.Substring(run.Start, run.Length) == name) return true;
        return false;
    }
}
