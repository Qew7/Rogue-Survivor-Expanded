using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class RecordsDistrictColorsScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/records-district-colors", () => TownScenarioFactory.Arena(4945,
            ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor player = NpcIntentSupport.Player(world, 4, 1);
            Actor resident = NpcIntentSupport.Actor(world, "resident", 1, 1);
            var kinds = new Dictionary<string, DistrictKind> {
                { "A0", DistrictKind.BUSINESS }, { "B0", DistrictKind.RESIDENTIAL },
                { "C0", DistrictKind.SHOPPING }, { "D0", DistrictKind.GREEN },
                { "E0", DistrictKind.GENERAL } };
            var palette = new RecordsTextColors(Session.Get.ResidentRecords.Residents, kinds);
            Check.Equal(true, HasColor(palette, "business district A0", Color.Red), "business district keeps map color");
            Check.Equal(true, HasColor(palette, "residential district B0", Color.Orange), "residential district keeps map color");
            Check.Equal(true, HasColor(palette, "shopping district C0", Color.White), "shopping district keeps map color");
            Check.Equal(true, HasColor(palette, "green district D0", Color.Green), "green district keeps map color");
            Check.Equal(true, HasColor(palette, "district E0", Color.Gray), "general district keeps map color");
            Check.Equal(true, HasColor(palette, "district A0", Color.Red), "old coordinate-only rumor is colored");
            Check.Equal(false, HasColor(new RecordsTextColors(Session.Get.ResidentRecords.Residents),
                "district A0", Color.Red), "unknown old archive does not guess a district kind");

            string coordinate = World.CoordToString(world.Map.District.WorldPosition.X,
                world.Map.District.WorldPosition.Y);
            Session.Get.ResidentRecords.Register(resident).Add("district-note", 0,
                "Resident heard a report from district " + coordinate + ".");
            string path = Path.Combine(Path.GetTempPath(), "records-districts-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                RecordsSave archive = RecordsReader.Load(path);
                Check.Equal(world.Map.District.Kind, archive.Records.DistrictKinds[coordinate],
                    "archive-only load preserves the district kind");
                var savedColors = new RecordsTextColors(archive.Records.Residents, archive.Records.DistrictKinds);
                Check.Equal(true, HasColor(savedColors, "district " + coordinate,
                    District.DisplayColor(world.Map.District.Kind)), "saved rumor uses the world-map color");
                ScenarioUI ui = (ScenarioUI)world.Game.UI;
                foreach (Keys key in new[] { Keys.Enter, Keys.Escape, Keys.Escape }) ui.QueueWaitKey(key);
                Check.Call(world.Game, "BrowseRecords", new[] { typeof(RecordsSave) }, archive);
                Check.Equal(true, ui.DrawnBold.Exists(draw => draw.Item1 == District.DisplayColor(world.Map.District.Kind) &&
                    draw.Item2 == "district " + coordinate), "Read Records draws the saved district in color");

                player.Personality.HearSpeech(new HeardJournalEntry(0, "heard_rumor", "resident",
                    "Something happened in district " + coordinate + ".", 0));
                ui.DrawnBold.Clear();
                ui.QueueWaitKey(Keys.Escape);
                Check.Call(world.Game, "HandleHeardJournal", Type.EmptyTypes);
                Check.Equal(true, ui.DrawnBold.Exists(draw => draw.Item1 == District.DisplayColor(world.Map.District.Kind) &&
                    draw.Item2 == "district " + coordinate), "the heard journal colors the same district");
            }
            finally { if (File.Exists(path)) File.Delete(path); if (File.Exists(path + ".bak")) File.Delete(path + ".bak"); }
        });
    }

    static bool HasColor(RecordsTextColors colors, string phrase, Color expected)
    {
        foreach (RecordsTextRun run in colors.Runs(phrase, Color.MediumPurple))
            if (run.Color == expected && phrase.Substring(run.Start, run.Length) == phrase) return true;
        return false;
    }
}
