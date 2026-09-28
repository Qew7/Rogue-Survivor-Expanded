using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class SaveStructureAuditTests
{
    public static void Run()
    {
        Map map = new Map(1, "audit", 2, 2);
        Tile tile = map.GetTileAt(0, 0);
        tile.AddDecoration("é:one"); tile.AddDecoration("two");
        tile.IsVisited = true;
        District district = new District(Point.Empty, DistrictKind.RESIDENTIAL);
        district.EntryMap = map; district.AddUniqueMap(map);
        List<object> root = new List<object> { district, map, tile, tile, "same", "same", null };
        root.Add(root);
        root.Add(new Dictionary<string, object> { { "same", map }, { "empty", new byte[0] } });
        root.Add(new object[,] { { new Point(1, 2), Guid.Empty }, { 3.5m, new byte[] { 1, 2 } } });
        root.Add(new Queue<string>(new[] { "same" })); root.Add(new HashSet<int> { 4, 5 });
        root.Add(new object[] { true, (byte)2, (sbyte)-2, (short)-3, (ushort)3, 4, 4U, 5L, 5UL,
            0.5f, 0.5d, 'é', DateTime.MinValue, TimeSpan.FromTicks(7), Lighting.DARKNESS, new string('é', 128) });
        SaveGraphAudit audit = new SaveGraphAudit(root);
        using (MemoryStream output = new MemoryStream())
        {
            ObjectGraphStore.Write(output, root);
            Check.Equal(output.Length, audit.Bytes, "audit exactly accounts for graph bytes");
            output.Position = 0;
            List<object> copy = (List<object>)ObjectGraphStore.Read(output);
            Check.Same(copy, copy[7], "cycle survives graph roundtrip");
            Map loadedMap = (Map)copy[1];
            Check.Same(loadedMap, ((District)copy[0]).EntryMap, "map shared between district and root");
            Check.Same(loadedMap.GetTileAt(0, 0), copy[2], "tile shared with external reference");
            Check.Same(copy[2], copy[3], "repeated tile is stored once");
        }
        Check.Equal(1L, audit.Types[typeof(Map)].Nodes, "audit counts shared map once");
        Check.Equal(4L, audit.Types[typeof(Tile)].Nodes, "equal mutable tiles stay distinct");
        Check.Equal(1, district.CountMaps, "same map is not added twice");
        SaveTileAudit tiles = new SaveTileAudit();
        tiles.Add(map); tiles.Add(map); tiles.Finish();
        Check.Equal(2, tiles.Patterns, "tile flags and decorations distinguish value patterns");
        Check.Equal(4L, tiles.Runs, "runs restart at each map");
        Check.Equal(1, tiles.EqualTerrainGroups, "equal terrain is counted");
        // Null and empty decoration lists differ; decoration order is preserved.
        typeof(Tile).GetField("m_Decorations", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(map.GetTileAt(0, 1), new List<string>());
        map.GetTileAt(1, 0).AddDecoration("two"); map.GetTileAt(1, 0).AddDecoration("é:one");
        map.GetTileAt(1, 0).IsVisited = true;
        SaveTileAudit boundaries = new SaveTileAudit(); boundaries.Add(map);
        Check.Equal(4, boundaries.Patterns, "null, empty and differently ordered decorations retained");
        Check.Equal(3, boundaries.DecorationPatterns, "decoration patterns retain list contents");
    }
}
