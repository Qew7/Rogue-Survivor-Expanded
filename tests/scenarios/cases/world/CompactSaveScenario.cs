using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class CompactSaveScenario
{
    public static void Register()
    {
        ScenarioRunner.Add("storage/compact-save", () => TownScenarioFactory.Arena(4580, "....", "...."), world =>
        {
            Tile tile = world.Map.GetTileAt(0, 0); tile.IsInside = tile.IsInView = tile.IsVisited = true;
            tile.AddDecoration("first"); tile.AddDecoration("second");
            Tile other = world.Map.GetTileAt(1, 0);
            FieldInfo decorations = typeof(Tile).GetField("m_Decorations", BindingFlags.Instance | BindingFlags.NonPublic);
            decorations.SetValue(other, decorations.GetValue(tile));
            decorations.SetValue(world.Map.GetTileAt(2, 0), new List<string>());
            world.Map.SetExitAt(new Point(3, 0), new Exit(world.Map, new Point(0, 1)));
            List<object> root = new List<object> { world.Map, tile }; root.Add(root);
            using (MemoryStream stream = new MemoryStream())
            {
                ObjectGraphStore.Write(stream, root); stream.Position = 0;
                List<object> loaded = (List<object>)ObjectGraphStore.Read(stream);
                Map map = (Map)loaded[0]; Tile saved = map.GetTileAt(0, 0);
                Check.Same(loaded, loaded[2], "cycle survives");
                Check.Same(map, map.GetExitAt(new Point(3, 0)).ToMap, "exit shares same map");
                Check.Same(saved, loaded[1], "external tile reference survives");
                Check.Equal(true, saved.IsInside && saved.IsInView && saved.IsVisited, "all flags survive");
                Check.Equal("first,second", String.Join(",", new List<string>(saved.Decorations).ToArray()), "decoration order survives");
                Check.Same(decorations.GetValue(saved), decorations.GetValue(map.GetTileAt(1, 0)), "shared decoration list survives");
                Check.Equal(true, map.GetTileAt(2, 0).HasDecorations, "empty decoration list remains non-null");
                Check.Equal(false, map.GetTileAt(3, 0).HasDecorations, "null decoration list remains null");
                map.GetTileAt(1, 0).IsVisited = true; saved.IsVisited = false;
                Check.Equal(true, map.GetTileAt(1, 0).IsVisited, "changing one tile does not share flags");
                Check.Equal(false, Object.ReferenceEquals(saved, map.GetTileAt(1, 0)), "equal cells remain distinct mutable tiles");
            }
            string path = Path.Combine(Path.GetTempPath(), "compact-" + Guid.NewGuid().ToString("N"));
            try
            {
                BinarySaveStore.Save(path, Session.Get);
                Session loaded = BinarySaveStore.LoadExact<Session>(path);
                for (int i = 0; i < 30; i++) Check.Equal(Session.Get.GameDiceRoller.Roll(0, 10000),
                    loaded.GameDiceRoller.Roll(0, 10000), "RNG state and deterministic continuation survive");
                Check.Equal(Session.Get.WorldTime.TurnCounter, loaded.WorldTime.TurnCounter, "clock survives");
                Check.Equal((byte)5, File.ReadAllBytes(path)[4], "new envelope used");
                using (MemoryStream bad = new MemoryStream())
                {
                    BinaryWriter writer = new BinaryWriter(bad); writer.Write(1); writer.Write(1); writer.Write(99);
                    bad.Position = 0; Check.Throws<InvalidDataException>(() => ObjectGraphStore.Read(bad), "unknown type reference rejected");
                }
            }
            finally { if (File.Exists(path)) File.Delete(path); }
        });
    }
}
