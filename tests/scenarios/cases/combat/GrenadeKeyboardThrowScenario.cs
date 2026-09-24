using System.Drawing;
using System.Windows.Forms;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Engine.Actions;
using djack.RogueSurvivor.Engine.Items;

static class GrenadeKeyboardThrowScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("combat/grenade-keyboard-throw", () => TownScenarioFactory.Arena(4526,
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
            RogueGame.KeyBindings.ResetToDefaults();
            ScenarioUI ui = (ScenarioUI)world.Game.UI;
            for (int i = 0; i < 4; i++) ui.QueueKey(Keys.NumPad6);
            ui.QueueKey(Keys.F);
            Check.Equal(true, Check.Call(world.Game, "HandlePlayerThrowGrenade", player),
                "keyboard aim and F throw grenade");
            Check.Equal(false, player.Inventory.Contains(grenade), "thrown grenade is consumed");
            Check.Equal(true, world.Map.GetItemsAt(new Point(7, 2)).TopItem is ItemGrenadePrimed,
                "primed grenade lands on keyboard target");
        });
    }
}
