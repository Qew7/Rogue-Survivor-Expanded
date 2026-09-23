using System;
using System.Drawing;

static class RulesTests
{
    public static void Run()
    {
        object rules = Check.Empty("Engine.Rules");
        Check.Equal(4, Check.Call(rules, "GridDistance", new Type[] { typeof(Point), typeof(Point) }, new Point(1, 2), new Point(5, 5)), "diagonal grid distance");
        Check.Equal(0, Check.Call(rules, "GridDistance", new Type[] { typeof(Point), typeof(Point) }, new Point(2, 3), new Point(2, 3)), "same tile");
        Check.Equal(5f, Check.Call(rules, "StdDistance", new Type[] { typeof(Point), typeof(Point) }, new Point(1, 2), new Point(4, 6)), "Euclidean distance");
        Check.Equal(true, Check.Call(rules, "IsAdjacent", new Type[] { typeof(Point), typeof(Point) }, new Point(1, 1), new Point(2, 2)), "diagonal adjacent");
        Check.Equal(false, Check.Call(rules, "IsAdjacent", new Type[] { typeof(Point), typeof(Point) }, new Point(1, 1), new Point(3, 1)), "two tiles away");
    }
}
