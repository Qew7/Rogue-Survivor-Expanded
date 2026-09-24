using System.Drawing;
using System.Windows.Forms;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

static class DoorBarricadeKeyboardInputScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("doors/barricade-keyboard-input", () => TownScenarioFactory.Arena(4524,
            ".....", ".....", "....."), world =>
        {
            Actor player = SkillScenario.Actor(world);
            world.Place(player, 1, 1);
            world.SetPlayer(player);
            world.Game.ComputeViewRect(player.Location.Position);
            DoorWindow door = new DoorWindow("door", "closed", "open", "broken", 40);
            world.Map.PlaceMapObjectAt(door, new Point(2, 1));
            player.Inventory.AddAll(new ItemBarricadeMaterial(world.Game.GameItems.WOODENPLANK));
            RogueGame.KeyBindings.ResetToDefaults();
            ((ScenarioUI)world.Game.UI).QueueKey(Keys.NumPad6);
            Check.Equal(true, Check.Call(world.Game, "HandlePlayerBarricade", player),
                "numpad executes barricade command");
            Check.Equal(true, door.BarricadePoints > 0, "keyboard barricade changes door");
        });
    }
}
