using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

static class MouseMoveTests
{
    static readonly Type Path = Check.Type("Engine.MouseMovePath");

    static List<Point> Find(Point start, Point goal, Rectangle bounds, Func<Point, bool> passable)
    {
        return (List<Point>)Check.Call(Path, "Find", new Type[] {
            typeof(Point), typeof(Point), typeof(Rectangle), typeof(Func<Point, bool>)
        }, start, goal, bounds, passable);
    }

    public static void Run()
    {
        Rectangle board = new Rectangle(0, 0, 5, 5);
        Func<Point, bool> clear = point => true;
        List<Point> diagonal = Find(new Point(0, 0), new Point(3, 3), board, clear);
        Check.Equal(3, diagonal.Count, "diagonal steps");
        Check.Equal(new Point(3, 3), diagonal[2], "diagonal destination");

        HashSet<Point> wall = new HashSet<Point> { new Point(1, 0), new Point(1, 1), new Point(1, 2), new Point(1, 3) };
        List<Point> detour = Find(new Point(0, 0), new Point(2, 0), board, point => !wall.Contains(point));
        Check.Equal(8, detour.Count, "detour around wall");
        foreach (Point step in detour)
            Check.Equal(false, wall.Contains(step), "never enters wall");

        Check.Equal(null, Find(new Point(0, 0), new Point(1, 1), board,
            point => point != new Point(1, 1)), "blocked goal");
        Check.Equal(null, Find(new Point(0, 0), new Point(5, 0), board, clear), "outside view");
        Check.Equal(0, Find(new Point(1, 1), new Point(1, 1), board, clear).Count, "same tile");
        Rectangle shifted = new Rectangle(-4, -4, 9, 9);
        List<Point> shiftedRoute = Find(new Point(-3, -3), new Point(3, 2), shifted, clear);
        Check.Equal(new Point(3, 2), shiftedRoute[shiftedRoute.Count - 1],
            "path indexing respects offset view bounds");

        // A fully enclosed target must remain unreachable even with diagonal movement.
        Check.Equal(null, Find(new Point(0, 0), new Point(4, 4), board,
            point => point.X < 3 || point == new Point(4, 4)), "enclosed target");

        Type bindings = Check.Type("Engine.Keybindings");
        object defaults = Activator.CreateInstance(bindings, true);
        Type commands = Check.Type("Engine.PlayerCommand");
        object mouseMode = Enum.Parse(commands, "MOUSE_MOVE_MODE");
        Check.Equal(Keys.M, Check.Call(defaults, "Get", new Type[] { commands }, mouseMode), "mouse key");

        Type gameType = Check.Type("Engine.RogueGame");
        object game = Check.Empty("Engine.RogueGame");
        object player = Check.Empty("Data.Actor");
        object other = Check.Empty("Data.Actor");
        BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo playerField = gameType.GetField("m_Player", fields);
        FieldInfo stepsField = gameType.GetField("m_MouseMoveSteps", fields);
        playerField.SetValue(game, player);
        stepsField.SetValue(game, new List<Point> { new Point(1, 0) });
        Type actorType = Check.Type("Data.Actor");
        Type[] damageArgs = { actorType, typeof(int) };
        Check.Equal(false, Check.Call(game, "CancelMouseMoveOnDamage", damageArgs, other, 2), "other actor damage");
        Check.Equal(false, Check.Call(game, "CancelMouseMoveOnDamage", damageArgs, player, 0), "zero damage");
        Check.Equal(1, ((List<Point>)stepsField.GetValue(game)).Count, "route still active");
        Check.Equal(true, Check.Call(game, "CancelMouseMoveOnDamage", damageArgs, player, 2), "player damage");
        Check.Equal(null, stepsField.GetValue(game), "damage cancels route");
    }
}
