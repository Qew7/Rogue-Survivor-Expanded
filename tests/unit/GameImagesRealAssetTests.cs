using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using djack.RogueSurvivor.Gameplay;

static class GameImagesRealAssetTests
{
    public static void Run()
    {
        string root = Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "..", "WRogue"));
        string images = Path.Combine(root, "Resources", "Images");
        if (!Directory.Exists(images))
            throw new DirectoryNotFoundException("Game image resources: " + images);

        MethodInfo makeGray = typeof(GameImages).GetMethod("MakeGrayLevel",
            BindingFlags.Static | BindingFlags.NonPublic);
        string[] files = Directory.GetFiles(images, "*.png", SearchOption.AllDirectories);
        Check.Equal(true, files.Length > 0, "game PNG resources are present");
        foreach (string file in files)
        {
            using (Bitmap source = new Bitmap(file))
            using (Bitmap expected = new Bitmap(source))
            using (Bitmap actual = (Bitmap)makeGray.Invoke(null, new object[] { source }))
            {
                for (int y = 0; y < source.Height; y++)
                    for (int x = 0; x < source.Width; x++)
                    {
                        Color original = source.GetPixel(x, y);
                        int value = (int)(255 * 0.55f * original.GetBrightness());
                        expected.SetPixel(x, y,
                            Color.FromArgb(original.A, value, value, value));
                        Check.Equal(expected.GetPixel(x, y).ToArgb(),
                            actual.GetPixel(x, y).ToArgb(),
                            Path.GetFileName(file) + " grayscale at " + x + "," + y);
                    }
            }
        }
    }
}
