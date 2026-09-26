using System;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class CorruptGamePresetScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/corrupt-game-presets", () => TownScenarioFactory.Arena(4461,
            "...", "...", "..."), world =>
        {
            string path = Path.Combine(Path.GetTempPath(), "corrupt-presets-" + Guid.NewGuid().ToString("N"));
            try
            {
                File.WriteAllText(path, "broken preset file");
                Check.Equal(0, GamePresetCollection.Load(path).Presets.Count,
                    "unreadable presets do not block built-in game setup");

                GamePreset valid = GamePreset.BuiltIn(GameMode.GM_STANDARD);
                valid.Name = "VALID CUSTOM";
                GamePreset invalid = valid.Copy();
                invalid.Name = "BROKEN CUSTOM";
                invalid.HungerThreshold = 101;
                GamePresetCollection mixed = new GamePresetCollection();
                mixed.Presets.Add(null);
                mixed.Presets.Add(invalid);
                mixed.Presets.Add(valid);
                BinarySaveStore.Save(path, mixed);
                GamePresetCollection loaded = GamePresetCollection.Load(path);
                Check.Equal(1, loaded.Presets.Count, "invalid preset entries are skipped");
                Check.Equal("VALID CUSTOM", loaded.Presets[0].Name, "valid preset survives mixed file");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
