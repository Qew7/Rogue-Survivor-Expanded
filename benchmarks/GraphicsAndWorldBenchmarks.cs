using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;
using djack.RogueSurvivor.Gameplay;

static class GraphicsAndWorldBenchmarks
{
    public static void Run(ScenarioWorld world, Actor player)
    {
        using (Bitmap source = new Bitmap(128, 128))
        {
            for (int x = 0; x < source.Width; x++)
                for (int y = 0; y < source.Height; y++)
                    source.SetPixel(x, y, Color.FromArgb(255, x, y, (x + y) / 2));
            MethodInfo makeGray = typeof(GameImages).GetMethod("MakeGrayLevel",
                BindingFlags.Static | BindingFlags.NonPublic);
            PerformanceBenchmarks.Measure("grayscale image 128x128", 50, () =>
            {
                using (Bitmap result = (Bitmap)makeGray.Invoke(null, new object[] { source })) { }
            });
        }

        OverlayCollection overlays = new OverlayCollection();
        ScenarioUI overlayUI = new ScenarioUI();
        for (int i = 0; i < 20; i++)
            overlays.Add(new OverlayRect(Color.White, new Rectangle(i, i, 1, 1)));
        PerformanceBenchmarks.Measure("draw 20 overlays", 100000,
            () => overlays.Draw(overlayUI));
        PerformanceBenchmarks.Measure("exit lookup 40x40", 10000,
            () => world.Map.HasAnExitIn(new Rectangle(0, 0, 40, 40)));
        PerformanceBenchmarks.Measure("field of view 40x40", 1000,
            () => LOS.ComputeFOVFor(world.Game.Rules, player,
                world.Map.LocalTime, Weather.CLEAR));

        Corpse searchedCorpse = null;
        for (int i = 0; i < 300; i++)
        {
            Corpse corpse = new Corpse(SkillScenario.Actor(world), 5, 5, 0, 0, 1);
            world.Map.AddCorpseAt(corpse, new Point(0, 0));
            searchedCorpse = corpse;
        }
        PerformanceBenchmarks.Measure("corpse membership 300 corpses", 100000,
            () => world.Map.HasCorpse(searchedCorpse));

        for (int i = 0; i < 100; i++)
            world.Map.SetExitAt(new Point(i % 40, i / 40),
                new Exit(world.Map, Point.Empty));
        PerformanceBenchmarks.Measure("exit lookup 1x1, 100 exits", 100000,
            () => world.Map.HasAnExitIn(new Rectangle(39, 39, 1, 1)));

        CSVParser csv = new CSVParser();
        int csvRows = 0;
        foreach (string file in Directory.GetFiles("Resources/Data", "*.csv"))
            csvRows += File.ReadAllLines(file).Length;
        string[] sampleRows = new string[csvRows];
        for (int i = 0; i < sampleRows.Length; i++)
            sampleRows[i] = "one,two,\"three,four\",five,six";
        PerformanceBenchmarks.Measure("CSV parse 131 rows", 100,
            () => csv.Parse(sampleRows));

        ModInfo[] available = new ModInfo[100];
        ModStamp[] stamps = new ModStamp[100];
        for (int i = 0; i < available.Length; i++)
        {
            available[i] = new ModInfo { Name = "mod" + i, Version = "1" };
            stamps[i] = new ModStamp { Name = "mod" + i, Version = "1" };
        }
        PerformanceBenchmarks.Measure("match 100 mod stamps", 1000, () =>
        {
            ModStamp[] missing;
            ModCatalog.Match(stamps, available, out missing);
        });

        string modRoot = Path.Combine(Path.GetTempPath(), "rogue-bench-mods-" +
            Guid.NewGuid().ToString("N"));
        ModInfo[] previousMods = ModCatalog.Selected;
        try
        {
            ModInfo[] mods = new ModInfo[5];
            for (int i = 0; i < mods.Length; i++)
            {
                string directory = Path.Combine(modRoot, "mod" + i);
                Directory.CreateDirectory(directory);
                mods[i] = new ModInfo { Name = "mod" + i, DirectoryPath = directory };
            }
            ModCatalog.Select(mods);
            PerformanceBenchmarks.Measure("resolve missing asset, 5 mods", 10000,
                () => ModCatalog.Resolve("Images", "absent.png"));
        }
        finally
        {
            ModCatalog.Select(previousMods);
            if (Directory.Exists(modRoot)) Directory.Delete(modRoot, true);
        }
    }
}
