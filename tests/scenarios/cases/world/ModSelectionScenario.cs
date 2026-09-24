using System;
using System.IO;
using djack.RogueSurvivor.Engine;

static class ModSelectionScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("mods/deonapocalypse",
            () =>
            {
                ModInfo[] mods = ModCatalog.Discover("mods");
                Check.Equal(1, mods.Length, "sample mod is discoverable");
                ModCatalog.Select(mods[0]);
                return TownScenarioFactory.Create(4408, false);
            },
            world =>
            {
                try
                {
                    Check.Equal("Deonapocalypse", ModCatalog.Active.Name, "selected mod name");
                    Check.Equal("Deon", ModCatalog.Active.Author, "author metadata loaded");
                    Check.Equal("Deon", ModCatalog.Active.GetAuthors()[0],
                        "single author metadata remains supported");
                    Check.Equal(true, ModCatalog.Active.Website.StartsWith("https://"),
                        "download site metadata loaded");
                    Check.Equal("rat", world.Game.GameActors.Skeleton.Name,
                        "selected mod changes actor data");
                    Check.Equal(true, ModCatalog.Resolve("Images", "Tiles/wall_brick.png")
                        .StartsWith(ModCatalog.Active.DirectoryPath),
                        "selected tileset overrides base image");
                    Check.Equal(true, File.Exists(ModCatalog.Resolve("Images", "rot1_1.png")),
                        "missing mod image falls back to base resource");
                }
                finally { ModCatalog.Select(null); }
                Check.Equal(Path.Combine("Resources", "Data", "Actors.csv"),
                    ModCatalog.Resolve("Data", "Actors.csv"),
                    "original game uses base data after mod is cleared");
                Check.Equal(0, ModCatalog.Discover("missing-mod-directory").Length,
                    "missing mods directory leaves original game available");
                string root = Path.Combine(Path.GetTempPath(), "rogue-mods-" + Guid.NewGuid().ToString("N"));
                try
                {
                    string folder = Path.Combine(root, "BrokenMetadata");
                    Directory.CreateDirectory(folder);
                    Check.Equal(1, ModCatalog.Discover(root).Length,
                        "each immediate folder is available as a mod");
                    ModInfo withoutMetadata = ModCatalog.Discover(root)[0];
                    Check.Equal(0, withoutMetadata.GetAuthors().Length,
                        "author may be omitted");
                    Check.Equal(0, withoutMetadata.GetWebsites().Length,
                        "website may be omitted");
                    File.WriteAllText(Path.Combine(folder, "authors.json"),
                        "{\"authors\":[\"Alice\",\"Bob\"]," +
                        "\"websites\":[\"https://example.com\",\"https://example.org\"]," +
                        "\"description\":\"A test mod\"}");
                    ModInfo multiple = ModCatalog.Discover(root)[0];
                    Check.Equal(2, multiple.GetAuthors().Length, "multiple authors are loaded");
                    Check.Equal("Bob", multiple.GetAuthors()[1], "second author is loaded");
                    Check.Equal(2, multiple.GetWebsites().Length, "multiple sites are loaded");
                    Check.Equal("https://example.org", multiple.GetWebsites()[1],
                        "second site is loaded");
                    File.WriteAllText(Path.Combine(folder, "authors.json"),
                        "{\"description\":\"No attribution\"}");
                    ModInfo withoutAttribution = ModCatalog.Discover(root)[0];
                    Check.Equal(0, withoutAttribution.GetAuthors().Length,
                        "author may be absent from metadata");
                    Check.Equal(0, withoutAttribution.GetWebsites().Length,
                        "website may be absent from metadata");
                    File.WriteAllText(Path.Combine(folder, "authors.json"), "not json");
                    Check.Equal(true, ModCatalog.Discover(root)[0].Description.Contains("Cannot read"),
                        "invalid optional metadata does not hide the mod");
                }
                finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
            });
    }
}
