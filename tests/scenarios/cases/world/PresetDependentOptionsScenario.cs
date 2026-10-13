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
            int rest = Array.IndexOf(labels, "Simulate distant districts while resting");
            int finish = Array.IndexOf(labels, "Finish distant simulation after resting");
            int parallel = Array.IndexOf(labels, "Parallel distant simulation while resting");
            Check.Equal(true, rest >= 0 && finish == rest + 1 && parallel == finish + 1,
                "dependent rest controls appear together");
            Check.Equal(false, preset.FinishDistantSimulationAfterRest, "finishing rest debt defaults off");
            Check.Equal(true, preset.ParallelDistantSimulationDuringRest,
                "new games default to parallel distant rest simulation");
            Check.Equal("N/A", Value(preset, rest), "rest simulation is inactive when district simulation is off");
            Check.Equal("N/A", Value(preset, finish), "finish option is inactive when simulation is off");
            Check.Equal("N/A", Value(preset, parallel), "parallel option is inactive when simulation is off");
            Check.Equal("district simulation", OptionRequirement(options, GameOptions.IDs.GAME_SIM_THREAD, preset),
                "simulation thread is inactive without district simulation");
            Check.Equal("district simulation", OptionRequirement(options, GameOptions.IDs.GAME_SIMULATE_SLEEP, preset),
                "legacy sleep simulation is inactive without district simulation");
            Toggle(preset, finish);
            Toggle(preset, parallel);
            Toggle(preset, rest);
            Check.Equal(false, preset.FinishDistantSimulationAfterRest,
                "inactive finish option cannot be switched on");
            Check.Equal(false, preset.DisableDistantSimulationDuringRest,
                "inactive rest simulation option cannot be changed");
            Check.Equal(true, preset.ParallelDistantSimulationDuringRest,
                "inactive parallel option cannot be changed");

            options.SimulateDistricts = GameOptions.SimRatio.FULL;
            preset.Options = options;
            Check.Equal("distant rest simulation disabled in the preset",
                OptionRequirement(options, GameOptions.IDs.GAME_SIM_THREAD, preset),
                "legacy simulation thread is inactive while preset rest simulation owns the work");
            Toggle(preset, finish);
            Toggle(preset, parallel);
            Check.Equal(false, preset.ParallelDistantSimulationDuringRest,
                "parallel simulation can be disabled in the preset");
            Toggle(preset, parallel);
            Check.Equal(true, preset.FinishDistantSimulationAfterRest,
                "finish option can be enabled after district simulation");
            Check.Equal(true, preset.ParallelDistantSimulationDuringRest,
                "parallel option can be enabled after district simulation");
            Toggle(preset, rest);
            Check.Equal("N/A", Value(preset, finish), "finish option is inactive when rest simulation is off");
            Check.Equal("N/A", Value(preset, parallel), "parallel option is inactive when rest simulation is off");
            Check.Equal(null, OptionRequirement(options, GameOptions.IDs.GAME_SIM_THREAD, preset),
                "legacy simulation thread becomes available when rest simulation is off");
            Check.Equal("simulation thread disabled", OptionRequirement(options, GameOptions.IDs.GAME_SIMULATE_SLEEP, preset),
                "legacy sleep simulation depends on the thread being off");
            Toggle(preset, finish);
            Check.Equal(true, preset.FinishDistantSimulationAfterRest,
                "inactive child keeps its chosen value for later");
            Toggle(preset, rest);
            Check.Equal("ON", Value(preset, finish), "re-enabled rest simulation restores the child value");
            Check.Equal("ON", Value(preset, parallel), "re-enabled rest simulation restores parallel choice");

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
                Check.Equal(true, loaded.FinishDistantSimulationAfterRest,
                    "finish choice survives preset save and load");
                Check.Equal("ON", Value(loaded, finish), "loaded dependent choice is visible");
                Check.Equal("ON", Value(loaded, parallel), "loaded parallel choice is visible");
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
