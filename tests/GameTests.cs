using System;
using System.Drawing;

static class GameTests
{
    public static void Run()
    {
        object game = Check.Empty("Engine.RogueGame");
        Check.Equal("", Check.Call(game, "Capitalize", new Type[] { typeof(string) }, new object[] { null }), "null message");
        Check.Equal("A", Check.Call(game, "Capitalize", "a"), "single letter");
        Check.Equal("Hello WORLD", Check.Call(game, "Capitalize", "hello WORLD"), "message capitalization");
        Check.Equal("an apple", Check.Call(game, "AorAn", "apple"), "vowel article");
        Check.Equal("a book", Check.Call(game, "AorAn", "book"), "consonant article");
        Check.Equal("abc", Check.Call(game, "TruncateString", "abcdef", 3), "truncate");
        Check.Equal("abc", Check.Call(game, "TruncateString", "abc", 3), "no truncation");
        Check.Call(game, "ComputeViewRect", new Point(15, 15));
        Check.Equal(true, Check.Call(game, "IsInViewRect", new Point(5, 5)), "view top left");
        Check.Equal(true, Check.Call(game, "IsInViewRect", new Point(25, 25)), "view bottom right");
        Check.Equal(false, Check.Call(game, "IsInViewRect", new Point(4, 5)), "outside view left");
        Check.Equal(false, Check.Call(game, "IsInViewRect", new Point(25, 26)), "outside view bottom");
        Check.Equal(new Point(0, 0), Check.Call(game, "MapToScreen", new Type[] { typeof(Point) }, new Point(5, 5)), "view origin screen");
        Check.Equal(new Point(320, 320), Check.Call(game, "MapToScreen", new Type[] { typeof(Point) }, new Point(15, 15)), "view center screen");
        Check.Equal(new Point(15, 15), Check.Call(game, "ScreenToMap", new Type[] { typeof(Point) }, new Point(320, 320)), "screen map roundtrip");
        Check.Equal(4, Check.Call(game, "FindLongestLine", new Type[] { typeof(string[]) }, (object)new string[] { "a", null, "four" }), "longest line");
    }
}
