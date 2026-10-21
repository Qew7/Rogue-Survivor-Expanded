using System;
using System.IO;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.MapObjects;
using djack.RogueSurvivor.Gameplay;
using djack.RogueSurvivor.Gameplay.Generators;

static class CachePresetScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/cache-note-presets", () => TownScenarioFactory.Arena(4734,
            ".....", ".....", "....."), world =>
        {
            GamePreset preset = GamePreset.BuiltIn(GameMode.GM_STANDARD);
            string[] labels = (string[])typeof(RogueGame).GetField("GamePresetLabels",
                BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            int cacheRow = Array.IndexOf(labels, "NPC caches");
            int noteRow = Array.IndexOf(labels, "NPC notes");
            Check.Equal(true, cacheRow > 0 && noteRow > 0, "both switches appear in preset menu");
            Check.Equal(true, preset.CachesEnabled && preset.NotesEnabled, "both features start enabled");
            Check.Call(typeof(RogueGame), "ChangeGamePreset",
                new[] { typeof(GamePreset), typeof(int), typeof(int) }, preset, cacheRow - 1, 1);
            Check.Equal(false, preset.CachesEnabled, "cache switch turns off");
            Check.Equal(true, preset.NotesEnabled, "notes remain independently enabled");
            Session.Get.GamePreset = preset;
            BaseTownGenerator generator = new BaseTownGenerator(world.Game, BaseTownGenerator.DEFAULT_PARAMS);
            MapObject oldDrawer = (MapObject)Check.CallOn(typeof(BaseMapGenerator), generator,
                "MakeObjDrawer", GameImages.OBJ_DRAWER);
            Check.Equal(false, oldDrawer is StorageContainer, "disabled caches use legacy drawer");
            Check.Call(typeof(RogueGame), "ChangeGamePreset",
                new[] { typeof(GamePreset), typeof(int), typeof(int) }, preset, cacheRow - 1, 1);
            Session.Get.GamePreset = preset;
            MapObject cacheDrawer = (MapObject)Check.CallOn(typeof(BaseMapGenerator), generator,
                "MakeObjDrawer", GameImages.OBJ_DRAWER);
            Check.Equal(true, cacheDrawer is StorageContainer, "enabled caches use opening drawer");
            Check.Call(typeof(RogueGame), "ChangeGamePreset",
                new[] { typeof(GamePreset), typeof(int), typeof(int) }, preset, noteRow - 1, 1);
            Check.Equal(false, preset.NotesEnabled, "note switch turns off");
            Check.Equal(true, preset.CachesEnabled, "caches remain independently enabled");
            string path = Path.Combine(Path.GetTempPath(), "cache-preset-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, preset);
                GamePreset loaded = BinarySaveStore.Load<GamePreset>(path);
                Check.Equal(true, loaded.CachesEnabled, "cache switch survives save");
                Check.Equal(false, loaded.NotesEnabled, "note switch survives save");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
