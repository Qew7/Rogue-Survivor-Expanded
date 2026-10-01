using System;
using System.IO;
using System.Windows.Forms;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay.Personality;

static class RecordsScreenScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/records-screen", () => TownScenarioFactory.Arena(4562,
            ".....", ".....", "....."), world =>
        {
            Session.Get.GamePreset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            Actor actor = new Actor(world.Game.GameActors.MaleCivilian,
                world.Game.GameFactions.TheCivilians, "recorded NPC", true, false, 0);
            actor.Personality = new PersonalityState();
            world.Place(actor, 1, 1);
            PersonalitySystem.Report(world.Game, new SignificantEvent("starvation", actor, null,
                world.Map, actor.Location.Position, 1));
            Session.Get.WorldTime.TurnCounter = 1;
            ScenarioUI ui = (ScenarioUI)world.Game.UI;
            string directory = Path.Combine(Path.GetTempPath(), "records-ui-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                ui.QueueWaitKey(Keys.Escape);
                Check.Call(world.Game, "ReadRecordsFrom", new[] { typeof(string) }, directory);
                Check.Equal(true, ui.DrawnStrings.Contains("No readable saves with NPC traits and memories enabled."),
                    "empty save folder is explained");
                BinarySaveStore.Save(Path.Combine(directory, "world.dat"), Session.Get);
                foreach (Keys key in new[] { Keys.Enter, Keys.Enter, Keys.Escape }) ui.QueueWaitKey(key);
                int residentIndex = RecordsReader.Residents(new RecordsSave("test", Session.Get))
                    .FindIndex(r => r.Identity == actor.PersonalityIdentity) + 2;
                for (int i = 0; i < residentIndex; i++) ui.QueueWaitKey(Keys.Down);
                foreach (Keys key in new[] { Keys.Enter, Keys.Escape, Keys.Escape, Keys.Escape })
                    ui.QueueWaitKey(key);
                Check.Call(world.Game, "ReadRecordsFrom", new[] { typeof(string) }, directory);
                Check.Equal(true, ui.DrawnStrings.Contains("Read Records - All residents"),
                    "all-resident timeline opens from save selection");
                Check.Equal(true, String.Concat(ui.DrawnStrings.ToArray()).Contains("Read Records - recorded NPC"),
                    "individual resident timeline opens from resident selection");
                Check.Equal(true, String.Join(" ", ui.DrawnStrings.ToArray()).Contains("faced starvation"),
                    "event text is rendered");
                Check.Equal(1, Session.Get.WorldTime.TurnCounter, "reading records does not simulate turns");
                string[] choices = new string[85];
                for (int i = 0; i < choices.Length; i++) choices[i] = "choice " + i;
                foreach (Keys key in new[] { Keys.PageDown, Keys.PageDown, Keys.PageDown, Keys.Enter })
                    ui.QueueWaitKey(key);
                Check.Equal(84, (int)Check.Call(world.Game, "ChooseRecord",
                    new[] { typeof(string), typeof(string[]), typeof(string) }, "choose", choices, "notice"),
                    "long resident selector reaches and clamps its last page");
                string[] lines = new string[100];
                for (int i = 0; i < lines.Length; i++) lines[i] = "line " + i;
                foreach (Keys key in new[] { Keys.End, Keys.PageDown, Keys.Home, Keys.PageUp, Keys.Escape })
                    ui.QueueWaitKey(key);
                Check.Call(world.Game, "ShowRecordLines",
                    new[] { typeof(string), typeof(System.Collections.Generic.IList<string>) }, "timeline", lines);
                Check.Equal(true, ui.DrawnStrings.Contains("line 99"), "long timeline reaches its final event");
            }
            finally { Directory.Delete(directory, true); }
        });
    }
}
