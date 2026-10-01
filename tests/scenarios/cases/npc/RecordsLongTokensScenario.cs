using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class RecordsLongTokensScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("npc/records-long-tokens", () => TownScenarioFactory.Arena(4579,
            "...", "...", "..."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor actor = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, new string('N', 260), true, false, 0);
            actor.Personality = new PersonalityState();
            world.Place(actor, 1, 1);
            ResidentRecord record = Session.Get.ResidentRecords.Register(actor);
            string token = new string('X', 500);
            record.Add("long", 0, "before " + token + " after");
            record.Add("boundary", 0, "before " + new string('Y', 116) + " tail");
            record.Add("short", 0, "Short readable entry.");
            string path = Path.Combine(Path.GetTempPath(), "records-long-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                RecordsSave save = RecordsReader.Load(path);
                ResidentRecord saved = RecordsReader.Residents(save)[0];
                foreach (ResidentRecord selected in new[] { saved, null })
                {
                    IList<string> lines = RecordsReader.Lines(save, selected);
                    Check.Equal(true, lines.Count < 40, "long tokens wrap in finite space");
                    string compact = String.Join("", new List<string>(lines).ToArray()).Replace(" ", "");
                    Check.Equal(true, compact.Contains("before" + token + "after"),
                        "hard wrapping preserves every character of a long saved token");
                    Check.Equal(true, compact.Contains("before" + new string('Y', 116) + "tail"),
                        "token at continuation width boundary remains complete");
                    Check.Equal(true, compact.Contains(actor.UnmodifiedName), "long resident name remains complete");
                    Check.Equal(true, compact.Contains("Shortreadableentry."), "ordinary short entry remains readable");
                    foreach (string line in lines)
                    {
                        Check.Equal(true, line.Length <= 120, "every wrapped display line fits");
                        Check.Equal(true, line.Trim().Length > 0, "no indent-only continuation is emitted");
                    }
                    ScenarioUI ui = (ScenarioUI)world.Game.UI;
                    ui.QueueWaitKey(Keys.End);
                    ui.QueueWaitKey(Keys.Escape);
                    Check.Call(world.Game, "ShowRecordLines", new[] { typeof(string), typeof(IList<string>) },
                        "long saved records", lines);
                    Check.Equal(true, ui.DrawnStrings.Contains(lines[lines.Count - 1]),
                        "timeline renders through the final saved entry");
                }
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
