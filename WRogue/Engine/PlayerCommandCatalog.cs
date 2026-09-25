using System;

namespace djack.RogueSurvivor.Engine
{
    // One list for the binding screen, with coverage checked by the unit suite.
    static class PlayerCommandCatalog
    {
        internal sealed class Entry
        {
            public readonly PlayerCommand Command;
            public readonly string Label;
            public Entry(PlayerCommand command, string label)
            {
                Command = command;
                Label = label;
            }
        }

        public static readonly Entry[] Bindable = {
            new Entry(PlayerCommand.MOVE_N, "Move N"),
            new Entry(PlayerCommand.MOVE_NE, "Move NE"),
            new Entry(PlayerCommand.MOVE_E, "Move E"),
            new Entry(PlayerCommand.MOVE_SE, "Move SE"),
            new Entry(PlayerCommand.MOVE_S, "Move S"),
            new Entry(PlayerCommand.MOVE_SW, "Move SW"),
            new Entry(PlayerCommand.MOVE_W, "Move W"),
            new Entry(PlayerCommand.MOVE_NW, "Move NW"),
            new Entry(PlayerCommand.WAIT_OR_SELF, "Wait"),
            new Entry(PlayerCommand.WAIT_LONG, "Wait 1 hour"),
            new Entry(PlayerCommand.ABANDON_GAME, "Abandon Game"),
            new Entry(PlayerCommand.ADVISOR, "Advisor Hint"),
            new Entry(PlayerCommand.BARRICADE_MODE, "Barricade"),
            new Entry(PlayerCommand.BREAK_MODE, "Break"),
            new Entry(PlayerCommand.BUILD_LARGE_FORTIFICATION, "Build Large Fortification"),
            new Entry(PlayerCommand.BUILD_SMALL_FORTIFICATION, "Build Small Fortification"),
            new Entry(PlayerCommand.CITY_INFO, "City Info"),
            new Entry(PlayerCommand.CLOSE_DOOR, "Close"),
            new Entry(PlayerCommand.FIRE_MODE, "Fire"),
            new Entry(PlayerCommand.GIVE_ITEM, "Give"),
            new Entry(PlayerCommand.HELP_MODE, "Help"),
            new Entry(PlayerCommand.HINTS_SCREEN_MODE, "Hints screen"),
            new Entry(PlayerCommand.NEGOCIATE_TRADE, "Negociate Trade"),
            new Entry(PlayerCommand.ITEM_SLOT_0, "Item 1 slot"),
            new Entry(PlayerCommand.ITEM_SLOT_1, "Item 2 slot"),
            new Entry(PlayerCommand.ITEM_SLOT_2, "Item 3 slot"),
            new Entry(PlayerCommand.ITEM_SLOT_3, "Item 4 slot"),
            new Entry(PlayerCommand.ITEM_SLOT_4, "Item 5 slot"),
            new Entry(PlayerCommand.ITEM_SLOT_5, "Item 6 slot"),
            new Entry(PlayerCommand.ITEM_SLOT_6, "Item 7 slot"),
            new Entry(PlayerCommand.ITEM_SLOT_7, "Item 8 slot"),
            new Entry(PlayerCommand.ITEM_SLOT_8, "Item 9 slot"),
            new Entry(PlayerCommand.ITEM_SLOT_9, "Item 10 slot"),
            new Entry(PlayerCommand.LEAD_MODE, "Lead"),
            new Entry(PlayerCommand.LOAD_GAME, "Load Game"),
            new Entry(PlayerCommand.MARK_ENEMIES_MODE, "Mark Enemies"),
            new Entry(PlayerCommand.MESSAGE_LOG, "Messages Log"),
            new Entry(PlayerCommand.OPTIONS_MODE, "Options"),
            new Entry(PlayerCommand.ORDER_MODE, "Order"),
            new Entry(PlayerCommand.PULL_MODE, "Pull"),
            new Entry(PlayerCommand.PUSH_MODE, "Push"),
            new Entry(PlayerCommand.QUIT_GAME, "Quit Game"),
            new Entry(PlayerCommand.KEYBINDING_MODE, "Redefine Keys"),
            new Entry(PlayerCommand.RUN_TOGGLE, "Run"),
            new Entry(PlayerCommand.SAVE_GAME, "Save Game"),
            new Entry(PlayerCommand.SCREENSHOT, "Screenshot"),
            new Entry(PlayerCommand.SHOUT, "Shout"),
            new Entry(PlayerCommand.SLEEP, "Sleep"),
            new Entry(PlayerCommand.SWITCH_PLACE, "Switch Place"),
            new Entry(PlayerCommand.USE_EXIT, "Use Exit"),
            new Entry(PlayerCommand.USE_SPRAY, "Use Spray"),
            new Entry(PlayerCommand.MOUSE_MOVE_MODE, "Mouse movement"),
            new Entry(PlayerCommand.XPD_BASE, "XPD base"),
            new Entry(PlayerCommand.EAT_CORPSE, "Eat corpse"),
            new Entry(PlayerCommand.REVIVE_CORPSE, "Revive corpse"),
        };
    }
}
