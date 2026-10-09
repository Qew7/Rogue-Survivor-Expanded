using System;
using System.Drawing;
using System.IO;
using System.Linq;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class RadioLocalTimeScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/radio-local-time", () => TownScenarioFactory.Arena(4978,
            "...", "...", "..."), world =>
        {
            Actor player = NpcIntentSupport.Player(world, 1, 1);
            int hour = WorldTime.TURNS_PER_HOUR;
            Session.Get.WorldTime.TurnCounter = 25 * hour;
            RadioProgram future = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 2, 25);
            world.Map.LocalTime.TurnCounter = 24 * hour;
            Check.Call(world.Game, "BroadcastRadio",
                new[] { typeof(int), typeof(Map), typeof(Point), typeof(Actor) },
                2, world.Map, player.Location.Position, null);
            HeardJournalEntry heard = player.Personality.HeardJournal.FirstOrDefault(entry =>
                entry.Kind == "radio" && entry.CauseId == 24);
            Check.Equal(true, heard != null, "lagging district hears its own hour's broadcast");
            RadioProgram past = (RadioProgram)Check.Call(world.Game, "GetRadioProgram", 2, 24);
            Check.Equal(past.Text, heard.Text, "heard text matches the recorded historical program");
            Check.Same(future, Session.Get.RadioPrograms[2], "historical airing leaves the current slot intact");

            string path = Path.Combine(Path.GetTempPath(), "radio-local-time-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                Session loaded = BinarySaveStore.Load<Session>(path);
                Check.Equal(past.Text, loaded.RadioProgramHistory[(24L << 2) | 2].Text,
                    "historical program survives save and load");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
