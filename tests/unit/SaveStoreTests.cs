using System;
using System.IO;
using System.IO.Compression;


static class SaveStoreTests
{
    static readonly Type Store = Check.Type("Engine.BinarySaveStore");

    static void Save(string path, object value)
    {
        Check.Call(Store, "Save", new Type[] { typeof(string), typeof(object) }, path, value);
    }

    static object Load(string path)
    {
        return Check.Call(Store, "Load", new Type[] { typeof(string), typeof(Action<object>) }, path, null);
    }

    public static void Run()
    {
        string path = Path.Combine(Path.GetTempPath(), "rogue-save-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            Save(path, "first new save");
            Check.Equal("first new save", Load(path), "versioned save reads");
            Check.Equal((byte)5, File.ReadAllBytes(path)[4], "compact format version");
            byte[] current = File.ReadAllBytes(path);
            foreach (byte version in new byte[] { 0, 1, 2, 3, 6 })
            {
                byte[] old = (byte[])current.Clone(); old[4] = version; File.WriteAllBytes(path, old);
                Check.Throws<InvalidDataException>(() => djack.RogueSurvivor.Engine.BinarySaveStore.LoadExact<string>(path),
                    "unsupported format is rejected");
            }
            using (FileStream file = File.Create(path))
            {
                BinaryWriter header = new BinaryWriter(file);
                header.Write(new byte[] { (byte)'R', (byte)'S', (byte)'E', (byte)'1', 4 });
                header.Write(djack.RogueSurvivor.SetupConfig.GAME_VERSION); header.Write(0);
                using (GZipStream gzip = new GZipStream(file, CompressionMode.Compress))
                {
                    BinaryWriter old = new BinaryWriter(gzip); old.Write(1); old.Write(1);
                    old.Write(typeof(djack.RogueSurvivor.Engine.Session).AssemblyQualifiedName);
                    old.Write((byte)0); old.Write(0); old.Write(0);
                }
            }
            Check.Throws<InvalidDataException>(() => djack.RogueSurvivor.Engine.BinarySaveStore.LoadExact<djack.RogueSurvivor.Engine.Session>(path),
                "version-4 world is rejected before building its graph");
            File.WriteAllBytes(path, current);
            Save(path, "second new save");
            Check.Equal("second new save", Load(path), "replacement reads");
            byte[] damaged = File.ReadAllBytes(path);
            damaged[damaged.Length - 1] ^= 0x7f;
            File.WriteAllBytes(path, damaged);
            Check.Equal("first new save", Load(path), "backup recovers damaged checksum");
            File.WriteAllText(path, "broken");
            Check.Equal("first new save", Load(path), "backup recovers corrupt save");

            Save(path, "expected string");
            Save(path, 42);
            Check.Equal("expected string", djack.RogueSurvivor.Engine.BinarySaveStore.Load<string>(path),
                "backup recovers wrong object type");

            using (MemoryStream unknown = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(unknown);
                writer.Write(1);
                writer.Write(1);
                writer.Write(-1);
                writer.Write(typeof(FileInfo).AssemblyQualifiedName);
                writer.Write((byte)0);
                writer.Write(0);
                writer.Write(0);
                unknown.Position = 0;
                bool rejected = false;
                try { djack.RogueSurvivor.Engine.ObjectGraphStore.Read(unknown); }
                catch (InvalidDataException) { rejected = true; }
                Check.Equal(true, rejected, "save reader rejects framework object types");
            }
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
        }
    }
}
