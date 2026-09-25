using System;
using System.IO;
using System.Reflection;
using djack.RogueSurvivor.Engine;

static class ModProfileScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("mods/profile-roundtrip",
            () => TownScenarioFactory.Create(4410, false), world =>
            {
                ModInfo[] available = ModCatalog.Discover("mods");
                Check.Equal(true, available.Length > 0, "sample mod available");
                Check.Equal("1.0.0", available[0].Version, "mod version loaded");
                Check.Equal("0.1.0", available[0].GameVersion,
                    "supported game version loaded");
                Check.Equal(true, available[0].SupportsCurrentGame,
                    "bundled mod supports this game version");
                string root = Path.Combine(Path.GetTempPath(),
                    "rogue-mod-profile-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(root);
                try
                {
                    string profile = Path.Combine(root, "profile.json");
                    ModProfileStore.Save(profile, new ModInfo[] { available[0] });
                    Check.Equal(available[0].Name,
                        ModProfileStore.Load(profile, available)[0].Name,
                        "menu profile retains selection");
                    string save = Path.Combine(root, "save.dat");
                    ModStamp[] required = ModCatalog.Stamps(new ModInfo[] { available[0] });
                    BinarySaveStore.Save(save, "world", required);
                    ModStamp[] stored;
                    Check.Equal("world", BinarySaveStore.Load(save, null, out stored),
                        "versioned save loads");
                    Check.Equal(required[0].Version, stored[0].Version,
                        "save retains required mod version");
                    Session.Get.Mods = required;
                    Session.Save(Session.Get, save, Session.SaveFormat.FORMAT_BIN);
                    Check.Equal(true, Session.Load(save, Session.SaveFormat.FORMAT_BIN),
                        "game session loads with its mod profile");
                    Check.Equal(required[0].Name, Session.Get.Mods[0].Name,
                        "loaded session retains mod name");
                    Check.Equal(required[0].Version, Session.Get.Mods[0].Version,
                        "loaded session retains mod version");
                    ModSaveValidator.Validate(Session.Get);
                    Session.Get.Mods = ModCatalog.Stamps(new ModInfo[0]);
                    Session.Save(Session.Get, save, Session.SaveFormat.FORMAT_BIN);
                    Check.Equal(0, BinarySaveStore.ReadMods(save).Length,
                        "resaving after fallback removes the absent mod");
                    ScenarioWorld floor = new ScenarioWorld(4410, "....", "....", "....");
                    Session.Get.World[0, 0].EntryMap = floor.Map;
                    Session.Get.CurrentMap = floor.Map;
                    var actor = floor.Place("unknown model", 1, 1);
                    typeof(djack.RogueSurvivor.Data.Actor).GetField("m_ModelID",
                        BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(actor, Int32.MaxValue);
                    bool invalidRejected = false;
                    try { ModSaveValidator.Validate(Session.Get); }
                    catch (InvalidDataException) { invalidRejected = true; }
                    Check.Equal(true, invalidRejected,
                        "save with no default model requires its mod");
                    ModStamp[] missing;
                    Check.Equal(1, ModCatalog.Match(stored, available, out missing).Length,
                        "matching mod is applied");
                    Check.Equal(0, missing.Length, "installed mod is available");
                    Check.Equal(0, ModCatalog.Match(stored, new ModInfo[0], out missing).Length,
                        "missing mod is omitted");
                    Check.Equal(required[0].Name, missing[0].Name,
                        "missing mod can be named in error");
                    Check.Equal(required[0].Version, missing[0].Version,
                        "missing mod required version is known");
                    ModInfo changed = new ModInfo { Name = available[0].Name,
                        Version = "2.0.0", DirectoryPath = available[0].DirectoryPath };
                    Check.Equal(0, ModCatalog.Match(stored, new ModInfo[] { changed },
                        out missing).Length, "different installed version falls back");
                    ModInfo incompatible = new ModInfo { Name = available[0].Name,
                        Version = available[0].Version, GameVersion = "alpha 99",
                        DirectoryPath = available[0].DirectoryPath };
                    Check.Equal(false, incompatible.SupportsCurrentGame,
                        "incompatible game version is detected");
                    ModLoadOrder order = new ModLoadOrder(new ModInfo[] { incompatible });
                    order.Toggle(0);
                    Check.Equal(0, order.EnabledCount,
                        "incompatible mod cannot be enabled from the menu");
                    Check.Equal(0, ModCatalog.Match(stored, new ModInfo[] { incompatible },
                        out missing).Length, "incompatible mod falls back");
                    ModProfileStore.Save(profile, new ModInfo[0]);
                    Check.Equal(0, ModProfileStore.Load(profile, available).Length,
                        "empty menu profile persists");
                }
                finally { Directory.Delete(root, true); }
            });
    }
}
