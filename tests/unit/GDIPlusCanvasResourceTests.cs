using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Runtime.Serialization;
using djack.RogueSurvivor.UI;

static class GDIPlusCanvasResourceTests
{
    sealed class DisposableGraphic : GDIPlusGameCanvas.IGfx, IDisposable
    {
        public bool Disposed;
        public void Draw(Graphics graphics) { }
        public void Dispose() { Disposed = true; }
    }

    public static void Run()
    {
        GDIPlusGameCanvas canvas = (GDIPlusGameCanvas)FormatterServices.GetUninitializedObject(
            typeof(GDIPlusGameCanvas));
        List<GDIPlusGameCanvas.IGfx> commands = new List<GDIPlusGameCanvas.IGfx>();
        typeof(GDIPlusGameCanvas).GetField("m_Gfxs", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(canvas, commands);
        typeof(GDIPlusGameCanvas).GetField("m_BrushesCache", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(canvas, new Dictionary<Color, Brush>());
        DisposableGraphic old = new DisposableGraphic();
        commands.Add(old);
        canvas.Clear(Color.Black);
        Check.Equal(true, old.Disposed, "clearing a frame disposes owned graphics");
        Check.Equal(0, commands.Count, "clearing a frame drops commands");

        canvas.AddString(SystemFonts.DefaultFont, Color.White, "one", 0, 0);
        canvas.AddString(SystemFonts.DefaultFont, Color.White, "two", 0, 10);
        FieldInfo brush = commands[0].GetType().GetField("m_Brush",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Check.Equal(true, Object.ReferenceEquals(brush.GetValue(commands[0]),
            brush.GetValue(commands[1])), "text commands share cached brush");

        using (Bitmap source = new Bitmap(2, 2))
        using (Bitmap target = new Bitmap(10, 10))
        using (Graphics graphics = Graphics.FromImage(target))
        {
            Type type = typeof(GDIPlusGameCanvas).GetNestedType("GfxImageTransform",
                BindingFlags.NonPublic);
            GDIPlusGameCanvas.IGfx transformed = (GDIPlusGameCanvas.IGfx)
                Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic,
                    null, new object[] { source, 45f, 1f, 1, 1 }, null);
            transformed.Draw(graphics);
            ((IDisposable)transformed).Dispose();
        }
        canvas.Clear(Color.Black);
    }
}
