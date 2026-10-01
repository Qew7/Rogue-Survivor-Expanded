using System;
using System.IO;
using System.Windows.Forms;
using djack.RogueSurvivor.Engine;

static class TalkKeybindingMigrationScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("world/talk-keybinding-migration", () => TownScenarioFactory.Arena(4706,
            "...", "...", "..."), world =>
        {
            string path = Path.Combine(Path.GetTempPath(), "talk-keys-" + Guid.NewGuid().ToString("N"));
            try
            {
                Keybindings old = new Keybindings();
                old.Set(PlayerCommand.TALK, Keys.None);
                Keybindings.Save(old, path);
                Keybindings migrated = Keybindings.Load(path);
                Check.Equal(Keys.V, migrated.Get(PlayerCommand.TALK),
                    "older key bindings gain V for talk");
                old.Set(PlayerCommand.TALK, Keys.T | Keys.Control);
                Keybindings.Save(old, path);
                migrated = Keybindings.Load(path);
                Check.Equal(Keys.V, migrated.Get(PlayerCommand.TALK),
                    "old Ctrl+T talk binding migrates away from browser new-tab shortcut");
                old.Set(PlayerCommand.NEGOCIATE_TRADE, Keys.T | Keys.Control);
                Keybindings.Save(old, path);
                migrated = Keybindings.Load(path);
                Check.Equal(Keys.V, migrated.Get(PlayerCommand.TALK),
                    "migration preserves another command's Ctrl+T assignment");
                old.Set(PlayerCommand.TALK, Keys.T | Keys.Control);
                old.Set(PlayerCommand.NEGOCIATE_TRADE, Keys.V);
                Keybindings.Save(old, path);
                migrated = Keybindings.Load(path);
                Check.Equal(Keys.V | Keys.Shift, migrated.Get(PlayerCommand.TALK),
                    "occupied V uses a browser-safe fallback");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
