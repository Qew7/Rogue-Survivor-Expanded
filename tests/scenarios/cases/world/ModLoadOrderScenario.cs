using System;
using System.IO;
using djack.RogueSurvivor.Engine;

static class ModLoadOrderScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("mods/load-order",
            () => TownScenarioFactory.Create(4409, false),
            world =>
            {
                string root = Path.Combine(Path.GetTempPath(), "rogue-mod-order-" +
                    Guid.NewGuid().ToString("N"));
                try
                {
                    string alpha = Path.Combine(root, "Alpha");
                    string beta = Path.Combine(root, "Beta");
                    string gamma = Path.Combine(root, "Gamma");
                    Directory.CreateDirectory(Path.Combine(alpha, "Data"));
                    Directory.CreateDirectory(Path.Combine(beta, "Data"));
                    Directory.CreateDirectory(Path.Combine(beta, "Images"));
                    Directory.CreateDirectory(gamma);
                    File.WriteAllText(Path.Combine(alpha, "Data", "Actors.csv"), "alpha");
                    File.WriteAllText(Path.Combine(beta, "Data", "Actors.csv"), "beta");
                    File.WriteAllText(Path.Combine(beta, "Images", "tile.png"), "beta tile");

                    ModLoadOrder order = new ModLoadOrder(ModCatalog.Discover(root));
                    Check.Equal(3, order.Count, "all folders appear in the mod list");
                    Check.Equal(0, order.EnabledCount, "original game is the default");
                    int alphaIndex = order.Toggle(0);
                    int betaIndex = order.Toggle(1);
                    Check.Equal(0, alphaIndex, "first selected mod is highest priority");
                    Check.Equal(1, betaIndex, "second selected mod follows it");
                    ModCatalog.Select(order.Selected());
                    Check.Equal(Path.Combine(alpha, "Data", "Actors.csv"),
                        ModCatalog.Resolve("Data", "Actors.csv"),
                        "highest priority mod wins a data conflict");
                    Check.Equal(Path.Combine(beta, "Images", "tile.png"),
                        ModCatalog.Resolve("Images", "tile.png"),
                        "missing file falls through to lower priority mod");
                    Check.Equal(Path.Combine("Resources", "Data", "Skills.csv"),
                        ModCatalog.Resolve("Data", "Skills.csv"),
                        "missing file falls through to original resources");

                    betaIndex = order.Move(betaIndex, -1);
                    ModCatalog.Select(order.Selected());
                    Check.Equal(0, betaIndex, "enabled mod moves up in priority");
                    Check.Equal("Beta", ModCatalog.Selected[0].Name,
                        "selected order reflects the menu");
                    Check.Equal(Path.Combine(beta, "Data", "Actors.csv"),
                        ModCatalog.Resolve("Data", "Actors.csv"),
                        "new highest priority mod wins the same conflict");
                    ModLoadOrder reopened = new ModLoadOrder(ModCatalog.Discover(root),
                        ModCatalog.Selected);
                    Check.Equal("Beta", reopened[0].Name,
                        "reopening the menu preserves the highest priority mod");
                    Check.Equal(2, reopened.EnabledCount,
                        "reopening the menu preserves all enabled mods");
                    Check.Equal(0, order.Move(0, -1), "top mod cannot move higher");
                    Check.Equal(2, order.Move(2, -1), "disabled mod cannot change priority");

                    order.Toggle(0);
                    ModCatalog.Select(order.Selected());
                    Check.Equal(1, ModCatalog.Selected.Length, "disabled mod is removed");
                    Check.Equal(Path.Combine(alpha, "Data", "Actors.csv"),
                        ModCatalog.Resolve("Data", "Actors.csv"),
                        "disabling top mod reveals the next mod");
                    order.Toggle(0);
                    ModCatalog.Select(order.Selected());
                    Check.Equal(0, ModCatalog.Selected.Length, "all mods can be disabled");
                    Check.Equal(Path.Combine("Resources", "Data", "Actors.csv"),
                        ModCatalog.Resolve("Data", "Actors.csv"),
                        "no selected mods uses original resources");
                }
                finally
                {
                    ModCatalog.Select();
                    if (Directory.Exists(root)) Directory.Delete(root, true);
                }
            });
    }
}
