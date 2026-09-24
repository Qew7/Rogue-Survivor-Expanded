using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using djack.RogueSurvivor;
using djack.RogueSurvivor.Engine;

static class HintsSaveTests
{
    public static void Run()
    {
        Logger.CreateFile();
        string path = Path.Combine(Path.GetTempPath(), "rogue-hints-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            GameHintsStatus hints = new GameHintsStatus();
            hints.SetAdvisorHintAsGiven(AdvisorHint._FIRST);
            GameHintsStatus.Save(hints, path);
            Check.Equal(true, GameHintsStatus.Load(path).IsAdvisorHintGiven(AdvisorHint._FIRST),
                "new hint save restores status");

            using (FileStream legacy = File.Create(path))
                new BinaryFormatter().Serialize(legacy, hints);
            Check.Equal(true, GameHintsStatus.Load(path).IsAdvisorHintGiven(AdvisorHint._FIRST),
                "legacy hint save restores status");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
        }
    }
}
