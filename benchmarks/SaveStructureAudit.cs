using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

static class SaveStructureAudit
{
    public static void Run(string path)
    {
        Console.WriteLine("SAVE AUDIT input bytes={0}; read-only; no active Session or mods restored", new FileInfo(path).Length);
        Stopwatch timer = Stopwatch.StartNew();
        Session session = BinarySaveStore.LoadExact<Session>(path);
        GC.Collect(); GC.WaitForPendingFinalizers();
        Console.WriteLine("SAVE AUDIT loaded day={0}; load={1:F2}s; retained managed bytes={2}",
            session.WorldTime.Day, timer.Elapsed.TotalSeconds, GC.GetTotalMemory(true));
        HashSet<object> listed = new HashSet<object>(new SaveGraphAudit.References());
        Dictionary<string, int> roles = new Dictionary<string, int>();
        Dictionary<string, int> sizes = new Dictionary<string, int>();
        Dictionary<string, int> names = new Dictionary<string, int>();
        long districts = 0, occurrences = 0, wrongDistrict = 0, missingRole = 0;
        for (int x = 0; x < session.World.Size; x++)
            for (int y = 0; y < session.World.Size; y++)
            {
                District district = session.World[x, y]; if (district == null) continue;
                districts++;
                foreach (Map map in district.Maps)
                {
                    occurrences++;
                    if (!Object.ReferenceEquals(map.District, district)) wrongDistrict++;
                    if (!listed.Add(map)) continue;
                    string role = Object.ReferenceEquals(map, district.EntryMap) ? "surface" :
                        Object.ReferenceEquals(map, district.SewersMap) ? "sewers" :
                        Object.ReferenceEquals(map, district.SubwayMap) ? "subway" :
                        map.Name.StartsWith("basement", StringComparison.OrdinalIgnoreCase) ? "basement" : "special";
                    Increment(roles, role); Increment(sizes, map.Width + "x" + map.Height); Increment(names, map.Name);
                }
                foreach (Map role in new[] { district.EntryMap, district.SewersMap, district.SubwayMap })
                {
                    if (role == null) continue;
                    bool found = false;
                    foreach (Map map in district.Maps) if (Object.ReferenceEquals(map, role)) found = true;
                    if (!found) missingRole++;
                }
            }
        Console.WriteLine("SAVE AUDIT city={0}x{0}; districts={1}; map list entries={2}; unique listed maps={3}; duplicate entries={4}; wrong district={5}; role missing from list={6}",
            session.World.Size, districts, occurrences, listed.Count, occurrences - listed.Count, wrongDistrict, missingRole);
        foreach (KeyValuePair<string, int> pair in roles) Console.WriteLine("SAVE AUDIT map role {0}={1}", pair.Key, pair.Value);
        foreach (KeyValuePair<string, int> pair in sizes) Console.WriteLine("SAVE AUDIT map size {0}={1}", pair.Key, pair.Value);
        foreach (KeyValuePair<string, int> pair in names)
            if (pair.Value > 1) Console.WriteLine("SAVE AUDIT repeated map name {0}={1}", pair.Key, pair.Value);
        Console.WriteLine("SAVE AUDIT scanning serialized graph"); timer.Restart();
        SaveGraphAudit graph = new SaveGraphAudit(session);
        List<KeyValuePair<Type, SaveGraphAudit.TypeCount>> types = new List<KeyValuePair<Type, SaveGraphAudit.TypeCount>>(graph.Types);
        types.Sort((a, b) => b.Value.Bytes.CompareTo(a.Value.Bytes));
        Console.WriteLine("SAVE AUDIT graph nodes={0}; reference occurrences={1}; legacy raw estimate={2}; scan={3:F2}s", graph.Nodes.Count, graph.ReferencesWritten, graph.Bytes, timer.Elapsed.TotalSeconds);
        Console.WriteLine("SAVE AUDIT type occurrences={0}; unique types={1}; type name bytes={2}; unique type name bytes={3}", graph.TypeOccurrences, graph.UniqueTypes, graph.TypeBytes, graph.UniqueTypeBytes);
        Console.WriteLine("SAVE AUDIT field groups={0}; field schema bytes={1}; unique field schema bytes={2}", graph.FieldGroups, graph.FieldBytes, graph.UniqueFieldBytes);
        Console.WriteLine("SAVE AUDIT string occurrences={0}; unique values={1}; string payload bytes={2}; unique payload bytes={3}", graph.StringOccurrences, graph.UniqueStrings, graph.StringBytes, graph.UniqueStringBytes);
        foreach (KeyValuePair<Type, SaveGraphAudit.TypeCount> pair in types.GetRange(0, Math.Min(15, types.Count)))
            Console.WriteLine("SAVE AUDIT node type {0}: count={1}; bytes={2}", pair.Key.FullName, pair.Value.Nodes, pair.Value.Bytes);
        SavePerformanceBenchmarks.CountingStream sink = new SavePerformanceBenchmarks.CountingStream();
        ObjectGraphStore.Write(sink, session);
        Console.WriteLine("SAVE AUDIT current compact graph raw bytes={0}; writes={1}", sink.Bytes, sink.Calls);
        long maps = 0, outside = 0;
        SaveTileAudit tiles = new SaveTileAudit();
        foreach (object node in graph.Nodes)
        {
            Map map = node as Map; if (map == null) continue;
            maps++; if (!listed.Contains(map)) outside++;
            tiles.Add(map);
        }
        tiles.Finish();
        SaveGraphAudit.TypeCount tileNodes; graph.Types.TryGetValue(typeof(Tile), out tileNodes);
        Console.WriteLine("SAVE AUDIT graph maps={0}; maps outside district lists={1}; cells={2}; distinct tile objects={3}", maps, outside, tiles.Cells, tileNodes == null ? 0 : tileNodes.Nodes);
        Console.WriteLine("SAVE AUDIT tile value patterns={0}; decoration value patterns={1}; decorated cells={2}; runs={3}", tiles.Patterns, tiles.DecorationPatterns, tiles.DecoratedCells, tiles.Runs);
        Console.WriteLine("SAVE AUDIT tile palette payload estimate={0}; palette+RLE payload estimate={1}; excludes object IDs, alias identity, map framing and compression", tiles.PaletteBytes + 4 * tiles.Cells, tiles.PaletteBytes + 8 * tiles.Runs);
        Console.WriteLine("SAVE AUDIT equal terrain groups={0}; maps in those groups={1}; includes all tile flags/decorations; excludes actors, objects, exits, seeds and other map state", tiles.EqualTerrainGroups, tiles.MapsInEqualTerrainGroups);
    }
    static void Increment(Dictionary<string, int> counts, string key)
    {
        int count; counts.TryGetValue(key, out count); counts[key] = count + 1;
    }
}
