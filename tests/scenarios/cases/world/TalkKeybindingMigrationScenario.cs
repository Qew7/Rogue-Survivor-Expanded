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
                Check.Equal(Keys.T | Keys.Control, migrated.Get(PlayerCommand.TALK),
                    "older key bindings gain Ctrl+T for talk");
                old.Set(PlayerCommand.NEGOCIATE_TRADE, Keys.T | Keys.Control);
                Keybindings.Save(old, path);
                migrated = Keybindings.Load(path);
                Check.Equal(Keys.None, migrated.Get(PlayerCommand.TALK),
                    "migration keeps the player's existing Ctrl+T assignment");
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            }
        });
    }
}
