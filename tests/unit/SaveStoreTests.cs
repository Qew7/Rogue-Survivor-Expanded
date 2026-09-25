using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

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
            using (FileStream legacy = File.Create(path))
                new BinaryFormatter().Serialize(legacy, "old save");
            Check.Equal("old save", Load(path), "legacy save reads");

            Save(path, "first new save");
            Check.Equal("first new save", Load(path), "versioned save reads");
            Check.Equal((byte)4, File.ReadAllBytes(path)[4], "new payload has game version and mods");
            byte[] versionFour = File.ReadAllBytes(path);
            byte[] versionThree = new byte[versionFour.Length - 6];
            Array.Copy(versionFour, versionThree, 5);
            versionThree[4] = 3;
            Array.Copy(versionFour, 11, versionThree, 5, versionFour.Length - 11);
            File.WriteAllBytes(path, versionThree);
            Check.Equal("first new save", Load(path), "previous mod manifest version still loads");
            byte[] versionTwo = new byte[versionThree.Length - 4];
            Array.Copy(versionThree, versionTwo, 5);
            versionTwo[4] = 2;
            Array.Copy(versionThree, 9, versionTwo, 5, versionThree.Length - 9);
            File.WriteAllBytes(path, versionTwo);
            Check.Equal("first new save", Load(path), "previous graph version still loads");
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

            using (FileStream versionOne = File.Create(path))
            {
                versionOne.Write(new byte[] { (byte)'R', (byte)'S', (byte)'E', (byte)'1', 1 }, 0, 5);
                new BinaryFormatter().Serialize(versionOne, "version one save");
            }
            Check.Equal("version one save", Load(path), "previous versioned saves still load");

            using (MemoryStream unknown = new MemoryStream())
            {
                BinaryWriter writer = new BinaryWriter(unknown);
                writer.Write(1);
                writer.Write(1);
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
