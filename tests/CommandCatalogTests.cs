using System;
using System.Collections;
using System.Reflection;
using System.Windows.Forms;

static class CommandCatalogTests
{
    public static void Run()
    {
        Type commandType = Check.Type("Engine.PlayerCommand");
        Type catalogType = Check.Type("Engine.PlayerCommandCatalog");
        Array entries = (Array)catalogType.GetField("Bindable").GetValue(null);
        Type bindingsType = Check.Type("Engine.Keybindings");
        object defaults = Activator.CreateInstance(bindingsType, true);
        Hashtable seen = new Hashtable();

        foreach (object entry in entries)
        {
            object command = entry.GetType().GetField("Command").GetValue(entry);
            string label = (string)entry.GetType().GetField("Label").GetValue(entry);
            Check.Equal(false, seen.ContainsKey(command), "duplicate command " + command);
            Check.Equal(true, !String.IsNullOrEmpty(label), "empty label " + command);
            Check.Equal(true, Check.Call(defaults, "Get", new Type[] { commandType }, command).Equals(Keys.None) == false,
                "missing default key " + command);
            seen[command] = true;
        }
        foreach (object command in Enum.GetValues(commandType))
            if (command.ToString() != "NONE")
                Check.Equal(true, seen.ContainsKey(command), "command missing from binding screen " + command);
    }
}
