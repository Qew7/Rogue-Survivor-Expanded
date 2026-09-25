using System;
using System.Drawing;
using System.Reflection;
using djack.RogueSurvivor.Gameplay;

static class GameImagesGrayLevelTests
{
    public static void Run()
    {
        MethodInfo makeGray = typeof(GameImages).GetMethod("MakeGrayLevel",
            BindingFlags.Static | BindingFlags.NonPublic);
        using (Bitmap source = new Bitmap(3, 1))
        {
            source.SetPixel(0, 0, Color.FromArgb(128, 255, 0, 0));
            source.SetPixel(1, 0, Color.FromArgb(255, 0, 255, 0));
            source.SetPixel(2, 0, Color.FromArgb(0, 20, 30, 40));
            using (Bitmap expected = new Bitmap(source))
            using (Bitmap gray = (Bitmap)makeGray.Invoke(null, new object[] { source }))
            {
                for (int x = 0; x < 3; x++)
                {
                    Color original = source.GetPixel(x, 0);
                    int value = (int)(255 * 0.55f * original.GetBrightness());
                    expected.SetPixel(x, 0, Color.FromArgb(original.A, value, value, value));
                    Check.Equal(expected.GetPixel(x, 0).ToArgb(),
                        gray.GetPixel(x, 0).ToArgb(), "grayscale pixel " + x);
                }
            }
        }

        using (Bitmap source = new Bitmap(32, 32))
        {
            for (int y = 0; y < source.Height; y++)
                for (int x = 0; x < source.Width; x++)
                    source.SetPixel(x, y, Color.FromArgb((x * 17 + y * 3) % 256,
                        (x * 31) % 256, (y * 47) % 256, (x * y * 7) % 256));
            using (Bitmap expected = new Bitmap(source))
            using (Bitmap gray = (Bitmap)makeGray.Invoke(null, new object[] { source }))
            {
                for (int y = 0; y < source.Height; y++)
                    for (int x = 0; x < source.Width; x++)
                    {
                        Color color = source.GetPixel(x, y);
                        int value = (int)(255 * 0.55f * color.GetBrightness());
                        expected.SetPixel(x, y, Color.FromArgb(color.A, value, value, value));
                        Check.Equal(expected.GetPixel(x, y).ToArgb(),
                            gray.GetPixel(x, y).ToArgb(), "grayscale varied pixel " + x + "," + y);
                    }
            }
        }
    }
}
