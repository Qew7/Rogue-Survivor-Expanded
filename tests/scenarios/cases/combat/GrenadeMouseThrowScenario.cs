using System.Drawing;
using System.Windows.Forms;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;

static class GrenadeMouseThrowScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("combat/grenade-mouse-throw", () => TownScenarioFactory.Arena(4525,
            "............", "............", "............", "............", "............"), world =>
        {
            Actor player = SkillScenario.Actor(world);
            world.Place(player, 3, 2);
            world.SetPlayer(player);
            world.Game.ComputeViewRect(player.Location.Position);
            ItemGrenade grenade = new ItemGrenade(world.Game.GameItems.GRENADE,
                world.Game.GameItems.GRENADE_PRIMED);
            Check.Equal(true, player.Inventory.AddAll(grenade), "grenade fits inventory");
            Check.Equal(true, world.Try(new ActionEquipItem(player, world.Game, grenade)),
                "grenade can be equipped");
            Point target = new Point(7, 2);
            Point screen = (Point)Check.Call(world.Game, "MapToScreen", target);
            screen.Offset(1, 1);
            ScenarioUI ui = (ScenarioUI)world.Game.UI;
            ui.MousePosition = screen;
            ui.QueueClick(MouseButtons.Left);
            Check.Equal(true, Check.Call(world.Game, "HandlePlayerThrowGrenade", player),
                "mouse click throws grenade");
            Check.Equal(false, player.Inventory.Contains(grenade), "thrown grenade is consumed");
            Check.Equal(true, world.Map.GetItemsAt(target).TopItem is ItemGrenadePrimed,
                "primed grenade lands on clicked tile");
        });
    }
}
