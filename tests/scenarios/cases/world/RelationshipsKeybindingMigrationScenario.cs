using System;
using System.IO;
using System.Windows.Forms;
using djack.RogueSurvivor.Engine;

static class RelationshipsKeybindingMigrationScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/relationships-keybinding-migration", () => TownScenarioFactory.Arena(4557,
            "...", "...", "..."), world =>
        {
            string path = Path.Combine(Path.GetTempPath(), "relation-keys-" + Guid.NewGuid().ToString("N"));
            try
            {
                Keybindings current = new Keybindings();
                Check.Equal(PlayerCommand.RELATIONSHIPS, current.Get(Keys.I | Keys.Shift),
                    "new bindings open relationships with Shift+I");
                current.Set(PlayerCommand.RELATIONSHIPS, Keys.None);
                Keybindings.Save(current, path);
                Keybindings migrated = Keybindings.Load(path);
                Check.Equal(Keys.I | Keys.Shift, migrated.Get(PlayerCommand.RELATIONSHIPS),
                    "old bindings receive the new command");

                current.Set(PlayerCommand.CITY_INFO, Keys.I | Keys.Shift);
                Keybindings.Save(current, path);
                migrated = Keybindings.Load(path);
                Check.Equal(Keys.None, migrated.Get(PlayerCommand.RELATIONSHIPS),
                    "migration respects an existing Shift+I assignment");
                Check.Equal(PlayerCommand.CITY_INFO, migrated.Get(Keys.I | Keys.Shift),
                    "existing assignment remains usable");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
