using System.Drawing;
using System.Windows.Forms;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Items;
using djack.RogueSurvivor.Engine.MapObjects;

static class DoorBarricadeMouseInputScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("doors/barricade-mouse-input", () => TownScenarioFactory.Arena(4523,
            ".....", ".....", "....."), world =>
        {
            Actor player = SkillScenario.Actor(world);
            world.Place(player, 1, 1);
            world.SetPlayer(player);
            world.Game.ComputeViewRect(player.Location.Position);
            DoorWindow door = new DoorWindow("door", "closed", "open", "broken", 40);
            world.Map.PlaceMapObjectAt(door, new Point(2, 1));
            player.Inventory.AddAll(new ItemBarricadeMaterial(world.Game.GameItems.WOODENPLANK));
            ScenarioUI ui = (ScenarioUI)world.Game.UI;
            Point screen = (Point)Check.Call(world.Game, "MapToScreen", new Point(2, 1));
            screen.Offset(1, 1);
            ui.MousePosition = screen;
            ui.QueueClick(MouseButtons.Left);
            Check.Equal(true, Check.Call(world.Game, "HandlePlayerBarricade", player),
                "mouse click executes barricade command");
            Check.Equal(true, door.BarricadePoints > 0, "mouse barricade changes door");
        });
    }
}
