using System;
using System.Drawing;
using System.Reflection;

static class GenerationTests
{
    public static void Run()
    {
        Type generator = Check.Type("Gameplay.Generators.BaseTownGenerator");
        Type parameters = generator.GetNestedType("Parameters", BindingFlags.Public);
        object config = Activator.CreateInstance(parameters);
        PropertyInfo width = parameters.GetProperty("MapWidth");
        width.SetValue(config, 40, null);
        Check.Equal(40, width.GetValue(config, null), "accepted map width");
        Check.Throws<ArgumentOutOfRangeException>(() => width.SetValue(config, 0, null), "zero map width");
        Check.Throws<ArgumentOutOfRangeException>(() => width.SetValue(config, 101, null), "oversized map width");

        Type blockType = generator.GetNestedType("Block", BindingFlags.Public);
        object block = Activator.CreateInstance(blockType, new object[] { new Rectangle(10, 20, 12, 14) });
        Check.Equal(new Rectangle(11, 21, 10, 12), blockType.GetProperty("BuildingRect").GetValue(block, null), "walkway ring");
        Check.Equal(new Rectangle(12, 22, 8, 10), blockType.GetProperty("InsideRect").GetValue(block, null), "wall ring");
        Check.Call(block, "ResetRectangle", new Rectangle(0, 0, 6, 8));
        Check.Equal(new Rectangle(2, 2, 2, 4), blockType.GetProperty("InsideRect").GetValue(block, null), "reset block geometry");
    }
}
