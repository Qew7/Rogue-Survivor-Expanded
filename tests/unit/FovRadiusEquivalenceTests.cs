using System.Drawing;
using djack.RogueSurvivor.Engine;

static class FovRadiusEquivalenceTests
{
    public static void Run()
    {
        Rules rules = new Rules(new DiceRoller(7130));
        for (int y = -128; y <= 128; y++)
            for (int x = -128; x <= 128; x++)
            {
                Point delta = new Point(x, y);
                int square = x * x + y * y;
                for (int radius = 0; radius <= 100; radius++)
                {
                    bool original = rules.LOSDistance(Point.Empty, delta) <= radius;
                    bool squared = 0.75f * square <= radius * radius;
                    if (original != squared)
                        Check.Equal(original, squared,
                            "FOV radius equivalence at " + x + "," + y +
                            " range " + radius);
                }
            }
    }
}
