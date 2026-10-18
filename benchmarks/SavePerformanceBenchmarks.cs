using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using djack.RogueSurvivor.Data;
using djack.RogueSurvivor.Engine;

// Run only against a copied save. All writes go to an independent temporary directory.
static class SavePerformanceBenchmarks
{
    internal sealed class CountingStream : Stream
    {
        public long Bytes, Calls;
        public override bool CanRead { get { return false; } }
        public override bool CanSeek { get { return false; } }
        public override bool CanWrite { get { return true; } }
        public override long Length { get { return Bytes; } }
        public override long Position { get { return Bytes; } set { throw new NotSupportedException(); } }
        public override void Flush() { }
        public override void Write(byte[] buffer, int offset, int count) { Bytes += count; Calls++; }
        public override void WriteByte(byte value) { Bytes++; Calls++; }
        public override int Read(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
    }

    public static void Run(string path, bool coreOnly = false, bool statsOnly = false)
    {
        Console.WriteLine("SAVE PROFILE input bytes={0}; samples=3; input is never modified", new FileInfo(path).Length);
        Stopwatch load = Stopwatch.StartNew();
        Session session = BinarySaveStore.LoadExact<Session>(path);
        Console.WriteLine("SAVE PROFILE load={0:F2} ms; day={1}; managed memory={2} bytes",
            load.Elapsed.TotalMilliseconds, session.WorldTime.Day, GC.GetTotalMemory(false));
        GC.Collect(); GC.WaitForPendingFinalizers();
        Console.WriteLine("SAVE PROFILE retained managed memory={0} bytes", GC.GetTotalMemory(true));
        if (statsOnly)
        {
            long wrongMap = 0, invalidPosition = 0, duplicatePosition = 0, misplaced = 0;
            foreach (District district in Districts(session))
                foreach (Map map in district.Maps)
                {
                    var positions = new HashSet<System.Drawing.Point>();
                    foreach (Actor actor in map.Actors)
                    {
                        bool wrong = actor.Location.Map != map;
                        bool invalid = !map.IsInBounds(actor.Location.Position);
                        bool duplicate = !positions.Add(actor.Location.Position);
                        if (wrong) wrongMap++;
                        if (invalid) invalidPosition++;
                        if (duplicate) duplicatePosition++;
                        if ((wrong || invalid || duplicate) && wrongMap + invalidPosition + duplicatePosition <= 10)
                            Console.WriteLine("SAVE PROFILE actor mismatch: map={0} district={1} size={2}x{3} actor={4} position={5} actual map={6} wrong={7} invalid={8} duplicate={9} dead={10} listed in actual map={11}",
                                map.Name, district.WorldPosition, map.Width, map.Height, actor.Name,
                                actor.Location.Position, actor.Location.Map == null ? "null" : actor.Location.Map.Name,
                                wrong, invalid, duplicate, actor.IsDead,
                                actor.Location.Map != null && actor.Location.Map.HasActor(actor));
                    }
                    foreach (Corpse corpse in map.Corpses)
                        if (corpse.DeadGuy.Location.Map != map || corpse.DeadGuy.Location.Position != corpse.Position)
                        {
                            misplaced++;
                            if (misplaced <= 5)
                                Console.WriteLine("SAVE PROFILE corpse mismatch: map={0} corpse={1} corpse position={2} dead actor position={3}",
                                    map.Name, corpse.DeadGuy.Name, corpse.Position, corpse.DeadGuy.Location.Position);
                        }
                }
            Console.WriteLine("SAVE PROFILE actor wrong map={0}; invalid position={1}; duplicate position={2}; corpse mismatch={3}",
                wrongMap, invalidPosition, duplicatePosition, misplaced);
            Console.WriteLine("SAVE PROFILE background city simulation enabled={0}",
                !session.GamePreset.DisableDistantSimulationDuringRest);
            Console.WriteLine("SAVE PROFILE saved options={0}; district simulation={1}; processors={2}",
                session.GamePreset.HasOptions, GameOptions.Name(session.GamePreset.Options.SimulateDistricts),
                Environment.ProcessorCount);
            foreach (District district in Districts(session))
                foreach (Map map in district.Maps) map.ReconstructAuxiliaryFields();
            Console.WriteLine("SAVE PROFILE map reconstruction succeeded");
            var dueByTurn = new SortedDictionary<int, int[]>();
            for (int x = 0; x < session.World.Size; x++) for (int y = 0; y < session.World.Size; y++)
            {
                District district = session.World[x, y];
                if (district == null || district == session.CurrentMap.District ||
                    district.EntryMap.LocalTime.TurnCounter >= session.WorldTime.TurnCounter) continue;
                int localTurn = district.EntryMap.LocalTime.TurnCounter;
                int[] colors;
                if (!dueByTurn.TryGetValue(localTurn, out colors))
                    dueByTurn[localTurn] = colors = new int[2];
                colors[(x + y) & 1]++;
            }
            int largestColorBatch = 0;
            foreach (int[] colors in dueByTurn.Values)
                largestColorBatch = Math.Max(largestColorBatch, Math.Max(colors[0], colors[1]));
            Console.WriteLine("SAVE PROFILE world turn={0}; city size={1}; due local turns={2}; largest same-time color batch={3}",
                session.WorldTime.TurnCounter, session.World.Size, dueByTurn.Count, largestColorBatch);
            int shownTurns = 0;
            foreach (KeyValuePair<int, int[]> due in dueByTurn)
            {
                if (shownTurns++ == 6) break;
                Console.WriteLine("SAVE PROFILE due turn={0}; color 0={1}; color 1={2}",
                    due.Key, due.Value[0], due.Value[1]);
                for (int color = 0; color < 2; color++)
                {
                    if (due.Value[color] == 0) continue;
                    var batch = new List<District>();
                    for (int x = 0; x < session.World.Size; x++) for (int y = 0; y < session.World.Size; y++)
                    {
                        District district = session.World[x, y];
                        if (district != null && district != session.CurrentMap.District &&
                            district.EntryMap.LocalTime.TurnCounter == due.Key && ((x + y) & 1) == color)
                            batch.Add(district);
                    }
                    var waves = (List<List<int>>)typeof(RogueGame).GetMethod("RestSimulationWaves",
                        BindingFlags.Static | BindingFlags.NonPublic).Invoke(null,
                        new object[] { batch, session.NpcDirector, session.CurrentMap.District });
                    int largestWave = 0;
                    foreach (List<int> wave in waves) largestWave = Math.Max(largestWave, wave.Count);
                    Console.WriteLine("SAVE PROFILE due turn={0}; color={1}; waves={2}; largest parallel wave={3}",
                        due.Key, color, waves.Count, largestWave);
                }
            }
            if (wrongMap + invalidPosition + duplicatePosition > 0) return;
        }
        FieldInfo archiveField = typeof(Session).GetField("m_ResidentRecords", BindingFlags.Instance | BindingFlags.NonPublic);
        ResidentRecords archive = (ResidentRecords)archiveField.GetValue(session);
        long entries = 0, textChars = 0, eventIds = 0;
        Dictionary<string, int> categories = new Dictionary<string, int>();
        if (archive != null)
            foreach (ResidentRecord resident in archive.Residents)
            {
                foreach (ResidentEntry entry in resident.Entries)
                {
                    entries++; textChars += entry.Text.Length;
                    if (entry.EventId != 0) eventIds++;
                    int count; categories.TryGetValue(entry.Kind, out count); categories[entry.Kind] = count + 1;
                }
            }
        long maps = 0, tiles = 0, actors = 0, traits = 0, pending = 0, observed = 0, relations = 0, links = 0;
        long corpses = 0, misplacedCorpses = 0, groundItems = 0, carriedItems = 0, mapObjects = 0, scents = 0, districtLag = 0;
        foreach (District district in Districts(session))
        {
            districtLag += session.WorldTime.TurnCounter - district.EntryMap.LocalTime.TurnCounter;
            foreach (Map map in district.Maps)
            {
                maps++; tiles += (long)map.Width * map.Height;
                map.ReconstructAuxiliaryFields();
                corpses += map.CountCorpses;
                foreach (Corpse corpse in map.Corpses)
                    if (corpse.DeadGuy.Location.Map != map || corpse.DeadGuy.Location.Position != corpse.Position)
                    {
                        misplacedCorpses++;
                        if (misplacedCorpses <= 5)
                            Console.WriteLine("SAVE PROFILE misplaced corpse: {0} at {1} in {2}; dead actor at {3}",
                                corpse.DeadGuy.Name, corpse.Position, map.Name, corpse.DeadGuy.Location);
                    }
                foreach (Inventory inventory in map.GroundInventories) groundItems += inventory.CountItems;
                foreach (MapObject ignored in map.MapObjects) mapObjects++;
                foreach (OdorScent ignored in map.Scents) scents++;
                foreach (Actor actor in map.Actors)
                {
                    actors++;
                    if (actor.Inventory != null) carriedItems += actor.Inventory.CountItems;
                    if (actor.Personality == null) continue;
                    PersonalityState state = actor.Personality;
                    traits += state.Traits.Count; pending += state.Memories.Count; observed += state.Events.Count;
                    // Avoid lazy getters creating empty relationship dictionaries in the measured graph.
                    foreach (string field in new[] { "m_People", "m_Groups", "m_Factions" })
                    {
                        System.Collections.IDictionary records = (System.Collections.IDictionary)typeof(PersonalityState)
                            .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(state);
                        if (records == null) continue;
                        foreach (RelationshipRecord record in records.Values) { relations++; links += record.Memories.Count; }
                    }
                }
            }
        }
        Console.WriteLine("SAVE PROFILE maps={0}; tiles={1}; live-map actors={2}; traits={3}; pending={4}; observations={5}; relationships={6}; memory links={7}",
            maps, tiles, actors, traits, pending, observed, relations, links);
        Console.WriteLine("SAVE PROFILE corpses={0}; ground items={1}; carried items={2}; map objects={3}; scents={4}; total district lag={5}",
            corpses, groundItems, carriedItems, mapObjects, scents, districtLag);
        Console.WriteLine("SAVE PROFILE misplaced corpses={0}; background city simulation enabled={1}",
            misplacedCorpses, !session.GamePreset.DisableDistantSimulationDuringRest);
        Console.WriteLine("SAVE PROFILE residents={0}; entries={1}; text chars={2}; event IDs={3}",
            archive == null ? 0 : archive.Residents.Count, entries, textChars, eventIds);
        foreach (KeyValuePair<string, int> category in categories)
            Console.WriteLine("SAVE PROFILE archive category {0}={1}", category.Key, category.Value);
        if (statsOnly) return;
        string directory = Path.Combine(Path.GetTempPath(), "rogue-save-bench-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            if (!coreOnly) Measure("presave world traversal", () => { session.World.OptimizeBeforeSaving(); return ""; });
            Measure("current atomic file save", () => {
                string output = Path.Combine(directory, "output.dat");
                BinarySaveStore.SaveSnapshot(output, session, session.Mods);
                return "file bytes=" + new FileInfo(output).Length;
            });
            if (coreOnly)
            {
                if (archive != null) Measure("archive alone, buffered gzip", () => Compressed(archive, true, CompressionLevel.Optimal));
                return;
            }
            Measure("graph only, counting sink", () => {
                CountingStream sink = new CountingStream();
                ObjectGraphStore.Write(sink, session);
                return "raw bytes=" + sink.Bytes + "; write calls=" + sink.Calls;
            });
            Measure("graph + direct default gzip", () => Compressed(session, false, CompressionLevel.Optimal));
            Measure("graph + 64KiB buffer + optimal gzip", () => Compressed(session, true, CompressionLevel.Optimal));
            Measure("graph + 64KiB buffer + fastest gzip", () => Compressed(session, true, CompressionLevel.Fastest));
            if (archive != null)
            {
                Measure("archive alone, buffered gzip", () => Compressed(archive, true, CompressionLevel.Optimal));
                archiveField.SetValue(session, null);
                try { Measure("world without archive, direct gzip (diagnostic only)", () => Compressed(session, false, CompressionLevel.Optimal)); }
                finally { archiveField.SetValue(session, archive); }
            }
        }
        finally { Directory.Delete(directory, true); }
    }

    static IEnumerable<District> Districts(Session session)
    {
        for (int x = 0; x < session.World.Size; x++)
            for (int y = 0; y < session.World.Size; y++)
                if (session.World[x, y] != null) yield return session.World[x, y];
    }

    static string Compressed(object value, bool buffered, CompressionLevel level)
    {
        CountingStream sink = new CountingStream();
        using (GZipStream gzip = new GZipStream(sink, level, true))
        {
            if (buffered)
                using (BufferedStream buffer = new BufferedStream(gzip, 65536)) ObjectGraphStore.Write(buffer, value);
            else ObjectGraphStore.Write(gzip, value);
        }
        return "gzip bytes=" + sink.Bytes + "; sink writes=" + sink.Calls;
    }

    static void Measure(string name, Func<string> action)
    {
        double[] samples = new double[3];
        string details = "";
        for (int i = 0; i < samples.Length; i++)
        {
            GC.Collect(); GC.WaitForPendingFinalizers();
            Console.WriteLine("SAVE BENCH starting {0}, sample {1}/3", name, i + 1);
            Stopwatch timer = Stopwatch.StartNew();
            details = action();
            samples[i] = timer.Elapsed.TotalMilliseconds;
            Console.WriteLine("SAVE BENCH sample {0}: {1:F2} ms; {2}", name, samples[i], details);
        }
        Array.Sort(samples);
        Console.WriteLine("SAVE BENCH MEDIAN {0}: {1:F2} ms; {2}", name, samples[1], details);
    }
}
