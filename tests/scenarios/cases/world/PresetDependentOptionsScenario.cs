using System;
using System.IO;
using System.Reflection;
using djack.RogueSurvivor.Engine;

static class PresetDependentOptionsScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/preset-dependent-options", () => TownScenarioFactory.Arena(5001,
            ".....", ".....", "....."), world =>
        {
            GamePreset preset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            GameOptions options = RogueGame.Options;
            options.SimulateDistricts = GameOptions.SimRatio.OFF;
            preset.Options = options;
            preset.HasOptions = true;
            string[] labels = (string[])typeof(RogueGame).GetField("GamePresetLabels",
                BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            int background = Array.IndexOf(labels, "Simulate whole city in background");
            Check.Equal(true, background >= 0 && Array.FindAll(labels,
                label => label == "Simulate whole city in background").Length == 1 &&
                Array.IndexOf(labels, "Other gameplay options") > background,
                "only one background city control appears in the preset");
            Check.Equal(-1, Array.IndexOf(labels, "Finish distant simulation after resting"),
                "blocking finish control is removed");
            Check.Equal(-1, Array.IndexOf(labels, "Parallel background simulation"),
                "parallel worker control is removed");
            Check.Equal("N/A", Value(preset, background),
                "background simulation requires district simulation");
            Toggle(preset, background);
            Check.Equal(false, preset.DisableDistantSimulationDuringRest,
                "inactive background control cannot change");

            options.SimulateDistricts = GameOptions.SimRatio.FULL;
            preset.Options = options;
            Check.Equal("ON", Value(preset, background), "background simulation defaults on");
            Toggle(preset, background);
            Check.Equal("OFF", Value(preset, background), "background simulation can be disabled");
            Toggle(preset, background);
            Check.Equal("ON", Value(preset, background), "background simulation can be restored");

            int corpseDecay = Array.IndexOf(labels, "Corpse decay speed");
            int infectionRate = Array.IndexOf(labels, "Infection rate");
            int shamblerWeight = Array.IndexOf(labels, "Shambler spawn weight");
            Check.Equal("N/A", Value(preset, corpseDecay), "corpse setting requires corpses");
            int decay = preset.CorpseDecayPercent;
            Toggle(preset, corpseDecay);
            Check.Equal(decay, preset.CorpseDecayPercent, "inactive corpse setting cannot change");
            Toggle(preset, Array.IndexOf(labels, "Corpses"));
            Toggle(preset, corpseDecay);
            Check.Equal(decay + 10, preset.CorpseDecayPercent,
                "corpse setting works after enabling corpses");
            Check.Equal("N/A", Value(preset, infectionRate), "infection rate requires infection");
            Toggle(preset, Array.IndexOf(labels, "Infection"));
            Check.Equal(true, Value(preset, infectionRate) != "N/A", "infection rate becomes editable");
            Toggle(preset, Array.IndexOf(labels, "Shamblers"));
            Check.Equal("N/A", Value(preset, shamblerWeight), "spawn weight requires its zombie type");
            Check.Equal("shamblers", OptionRequirement(options, GameOptions.IDs.GAME_SHAMBLERS_UPGRADE, preset),
                "undead branch upgrades also require the zombie type");
            options.PlayMusic = false;
            Check.Equal("music", OptionRequirement(options, GameOptions.IDs.UI_MUSIC_VOLUME, preset),
                "music volume is inactive without music");
            options.IsMinimapOn = false;
            Check.Equal("the minimap", OptionRequirement(options, GameOptions.IDs.UI_SHOW_PLAYER_TAG_ON_MINIMAP, preset),
                "minimap tags are inactive without a minimap");
            preset.Options = options;

            preset.Name = "REST OPTIONS";
            string path = Path.Combine(Path.GetTempPath(), "rest-preset-" + Guid.NewGuid().ToString("N"));
            try
            {
                GamePresetCollection collection = new GamePresetCollection();
                collection.AddOrReplace(preset);
                collection.Save(path);
                GamePreset loaded = GamePresetCollection.Load(path).Presets[0];
                Check.Equal("ON", Value(loaded, background),
                    "single background choice survives preset save and load");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }

    static string Value(GamePreset preset, int row)
    {
        return ((string[])Check.Call(typeof(RogueGame), "GamePresetValues",
            new[] { typeof(GamePreset) }, preset))[row];
    }

    static void Toggle(GamePreset preset, int row)
    {
        Check.Call(typeof(RogueGame), "ChangeGamePreset",
            new[] { typeof(GamePreset), typeof(int), typeof(int) }, preset, row - 1, 1);
    }

    static string OptionRequirement(GameOptions options, GameOptions.IDs option, GamePreset preset)
    {
        return (string)Check.Call(typeof(RogueGame), "GameOptionsRequirement",
            new[] { typeof(GameOptions), typeof(GameOptions.IDs), typeof(GamePreset) }, options, option, preset);
    }

}
