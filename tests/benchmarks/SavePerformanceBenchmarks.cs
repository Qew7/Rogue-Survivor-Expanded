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

    public static void Run(string path)
    {
        Console.WriteLine("SAVE PROFILE input bytes={0}; samples=3; input is never modified", new FileInfo(path).Length);
        Stopwatch load = Stopwatch.StartNew();
        Session session = BinarySaveStore.LoadExact<Session>(path);
        Console.WriteLine("SAVE PROFILE load={0:F2} ms; day={1}; managed memory={2} bytes",
            load.Elapsed.TotalMilliseconds, session.WorldTime.Day, GC.GetTotalMemory(false));
        FieldInfo archiveField = typeof(Session).GetField("m_ResidentRecords", BindingFlags.Instance | BindingFlags.NonPublic);
        ResidentRecords archive = (ResidentRecords)archiveField.GetValue(session);
        long entries = 0, textChars = 0, keys = 0, keyChars = 0;
        Dictionary<string, int> categories = new Dictionary<string, int>();
        if (archive != null)
            foreach (ResidentRecord resident in archive.Residents)
            {
                foreach (ResidentEntry entry in resident.Entries) { entries++; textChars += entry.Text.Length; }
                Dictionary<string, bool> dedup = (Dictionary<string, bool>)typeof(ResidentRecord)
                    .GetField("m_Keys", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(resident);
                foreach (string key in dedup.Keys)
                {
                    keys++; keyChars += key.Length;
                    string category = key.StartsWith("event:") ? key.Split(':')[1] : key.Split(':')[0];
                    int count; categories.TryGetValue(category, out count); categories[category] = count + 1;
                }
            }
        long maps = 0, tiles = 0, actors = 0, traits = 0, pending = 0, observed = 0, relations = 0, links = 0;
        foreach (District district in Districts(session))
            foreach (Map map in district.Maps)
            {
                maps++; tiles += (long)map.Width * map.Height;
                map.ReconstructAuxiliaryFields();
                foreach (Actor actor in map.Actors)
                {
                    actors++;
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
        Console.WriteLine("SAVE PROFILE maps={0}; tiles={1}; live-map actors={2}; traits={3}; pending={4}; observations={5}; relationships={6}; memory links={7}",
            maps, tiles, actors, traits, pending, observed, relations, links);
        Console.WriteLine("SAVE PROFILE residents={0}; entries={1}; text chars={2}; dedup keys={3}; dedup chars={4}",
            archive == null ? 0 : archive.Residents.Count, entries, textChars, keys, keyChars);
        foreach (KeyValuePair<string, int> category in categories)
            Console.WriteLine("SAVE PROFILE archive category {0}={1}", category.Key, category.Value);
        string directory = Path.Combine(Path.GetTempPath(), "rogue-save-bench-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            Measure("presave world traversal", () => { session.World.OptimizeBeforeSaving(); return ""; });
            Measure("current atomic file save", () => {
                string output = Path.Combine(directory, "output.dat");
                BinarySaveStore.SaveSnapshot(output, session, session.Mods);
                return "file bytes=" + new FileInfo(output).Length;
            });
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
