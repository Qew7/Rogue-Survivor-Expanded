using System.Drawing;
using System.Windows.Forms;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class GrenadeTargetInputScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("combat/grenade-target-input", () => TownScenarioFactory.Arena(4522,
            ".......", ".......", ".......", ".......", "......."), world =>
        {
            Point origin = new Point(2, 2);
            Rectangle view = new Rectangle(0, 0, 7, 5);
            RogueGame.KeyBindings.ResetToDefaults();
            Direction east = RogueGame.CommandToDirection(
                InputTranslator.KeyToCommand(new KeyEventArgs(Keys.NumPad6)));
            Point? keyboard = RogueGame.GrenadeTargetFromDirection(origin, east,
                world.Map, origin, 3);
            Check.Equal(new Point(3, 2), keyboard.Value, "keyboard aims one tile east");
            Check.Equal(new Point(3, 2), RogueGame.GrenadeTargetFromMouse(
                new Point(3, 2), world.Map, view, origin, 3).Value,
                "mouse aims at the clicked tile");
            Check.Equal(null, RogueGame.GrenadeTargetFromMouse(
                new Point(6, 2), world.Map, view, origin, 3), "mouse rejects distant target");
            Check.Equal(null, RogueGame.GrenadeTargetFromMouse(
                new Point(7, 2), world.Map, view, origin, 6), "mouse rejects tile outside view");
            Check.Equal(null, RogueGame.GrenadeTargetFromDirection(
                new Point(5, 2), east, world.Map, origin, 3), "keyboard rejects distant target");
        });
    }
}
