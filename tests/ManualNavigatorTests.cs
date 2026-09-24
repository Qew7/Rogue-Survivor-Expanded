using System;
using System.Windows.Forms;
using djack.RogueSurvivor.Engine;

static class ManualNavigatorTests
{
    public static void Run()
    {
        string[] lines = { "a", "b", "<SECTION>", "c", "d", "e",
            "f", "<SECTION>", "g", "h", "i", "j" };
        ManualNavigator manual = new ManualNavigator(lines);
        manual.Move(Keys.Up, -1, 3);
        Check.Equal(0, manual.Line, "manual does not scroll before start");
        manual.Move(Keys.PageDown, -1, 3);
        Check.Equal(3, manual.Line, "manual pages down");
        manual.Move(Keys.None, 2, 3);
        Check.Equal(8, manual.Line, "manual jumps to second section");
        manual.Move(Keys.None, 9, 3);
        Check.Equal(8, manual.Line, "missing section keeps position");
        manual.Move(Keys.PageDown, -1, 3);
        Check.Equal(9, manual.Line, "manual clamps final page");
        manual.Move(Keys.None, 0, 3);
        Check.Equal(0, manual.Line, "section zero returns to start");
        Check.Equal(0, new ManualNavigator(new string[0]).Line, "empty manual starts safely");
    }
}
