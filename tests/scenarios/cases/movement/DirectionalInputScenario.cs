using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class DirectionalInputScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("movement/directional-input", () => TownScenarioFactory.Arena(4521,
            ".....", ".....", ".....", ".....", "....."), world =>
        {
            Actor player = SkillScenario.Actor(world);
            world.Place(player, 2, 2);
            world.SetPlayer(player);
            typeof(RogueGame).GetField("m_MapViewRect", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(world.Game, new Rectangle(0, 0, 5, 5));
            ScenarioUI ui = (ScenarioUI)world.Game.UI;
            RogueGame.KeyBindings.ResetToDefaults();

            Direction[] directions = { Direction.N, Direction.NE, Direction.E,
                Direction.SE, Direction.S, Direction.SW, Direction.W, Direction.NW };
            Keys[] keys = { Keys.NumPad8, Keys.NumPad9, Keys.NumPad6, Keys.NumPad3,
                Keys.NumPad2, Keys.NumPad1, Keys.NumPad4, Keys.NumPad7 };
            for (int i = 0; i < directions.Length; i++)
            {
                ui.QueueKey(keys[i]);
                Check.Same(directions[i], Check.Call(world.Game, "WaitDirectionOrCancel"),
                    "keyboard direction " + directions[i]);
                Point target = player.Location.Position + directions[i];
                ui.MousePosition = Screen(target);
                ui.QueueClick(MouseButtons.Left);
                Check.Same(directions[i], Check.Call(world.Game, "WaitDirectionOrCancel"),
                    "mouse direction " + directions[i]);
            }
            ui.QueueKey(Keys.NumPad5);
            Check.Same(Direction.NEUTRAL, Check.Call(world.Game, "WaitDirectionOrCancel"),
                "keyboard self target");
            ui.MousePosition = Screen(player.Location.Position);
            ui.QueueClick(MouseButtons.Left);
            Check.Same(Direction.NEUTRAL, Check.Call(world.Game, "WaitDirectionOrCancel"),
                "mouse self target");
            ui.QueueKey(Keys.Escape);
            Check.Equal(null, Check.Call(world.Game, "WaitDirectionOrCancel"), "keyboard cancel");
            ui.QueueClick(MouseButtons.Right);
            Check.Equal(null, Check.Call(world.Game, "WaitDirectionOrCancel"), "mouse cancel");

            Rectangle view = new Rectangle(0, 0, 5, 5);
            Check.Equal(null, RogueGame.DirectionFromMouseTarget(player.Location.Position,
                new Point(4, 2), view, MouseButtons.Left), "distant click cannot choose direction");
            Check.Equal(null, RogueGame.DirectionFromMouseTarget(player.Location.Position,
                new Point(3, 2), view, MouseButtons.Right), "right click cannot choose direction");
            Check.Equal(null, RogueGame.DirectionFromMouseTarget(player.Location.Position,
                new Point(-1, 2), view, MouseButtons.Left), "offscreen click cannot choose direction");
        });
    }

    static Point Screen(Point tile)
    {
        return new Point(tile.X * RogueGame.TILE_SIZE + 1, tile.Y * RogueGame.TILE_SIZE + 1);
    }
}
