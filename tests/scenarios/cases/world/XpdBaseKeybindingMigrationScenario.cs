using System;
using System.IO;
using System.Windows.Forms;
using djack.RogueSurvivor.Engine;

static class XpdBaseKeybindingMigrationScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("xpd/base-keybinding-migration", () => TownScenarioFactory.Arena(4429,
            "...", "...", "..."), world =>
        {
            string path = Path.Combine(Path.GetTempPath(), "base-keys-" + Guid.NewGuid().ToString("N"));
            try
            {
                Keybindings old = new Keybindings();
                old.Set(PlayerCommand.XPD_BASE, Keys.None);
                Keybindings.Save(old, path);
                Keybindings migrated = Keybindings.Load(path);
                Check.Equal(Keys.B | Keys.Control, migrated.Get(PlayerCommand.XPD_BASE),
                    "old bindings gain Ctrl+B");

                old.Set(PlayerCommand.BARRICADE_MODE, Keys.B | Keys.Control);
                Keybindings.Save(old, path);
                migrated = Keybindings.Load(path);
                Check.Equal(Keys.None, migrated.Get(PlayerCommand.XPD_BASE),
                    "migration respects a user's Ctrl+B assignment");
                Check.Equal(PlayerCommand.BARRICADE_MODE, migrated.Get(Keys.B | Keys.Control),
                    "existing Ctrl+B assignment remains intact");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
