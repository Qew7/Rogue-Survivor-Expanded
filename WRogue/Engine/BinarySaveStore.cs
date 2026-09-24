using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization.Formatters.Binary;

namespace djack.RogueSurvivor.Engine
{
    // Versioned envelope around the legacy object graph. Old unmarked saves remain readable.
    static class BinarySaveStore
    {
        static readonly byte[] Magic = { (byte)'R', (byte)'S', (byte)'E', (byte)'1' };
        const byte Version = 3;

        public static void Save(string path, object value)
        {
            Save(path, value, new ModStamp[0]);
        }

        public static void Save(string path, object value, ModStamp[] mods)
        {
            if (path == null) throw new ArgumentNullException("path");
            if (value == null) throw new ArgumentNullException("value");
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(Magic, 0, Magic.Length);
                    stream.WriteByte(Version);
                    WriteMods(stream, mods ?? new ModStamp[0]);
                    using (GZipStream compressed = new GZipStream(stream, CompressionMode.Compress, true))
                        ObjectGraphStore.Write(compressed, value);
                    stream.Flush();
                }
                if (File.Exists(path))
                    File.Replace(temporary, path, path + ".bak");
                else
                    File.Move(temporary, path);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }

        public static object Load(string path, Action<object> validate)
        {
            ModStamp[] ignored;
            return Load(path, validate, out ignored);
        }

        public static object Load(string path, Action<object> validate, out ModStamp[] mods)
        {
            if (path == null) throw new ArgumentNullException("path");
            try
            {
                return Read(path, validate, out mods);
            }
            catch (Exception)
            {
                if (!File.Exists(path + ".bak")) throw;
                return Read(path + ".bak", validate, out mods);
            }
        }

        public static T Load<T>(string path)
        {
            return (T)Load(path, delegate(object value)
            {
                if (!(value is T))
                    throw new InvalidDataException("Save contains an unexpected object type.");
            });
        }

        public static ModStamp[] ReadMods(string path)
        {
            try { return ReadModsFile(path); }
            catch (Exception)
            {
                if (!File.Exists(path + ".bak")) throw;
                return ReadModsFile(path + ".bak");
            }
        }

        static ModStamp[] ReadModsFile(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] header = new byte[Magic.Length];
                if (stream.Read(header, 0, header.Length) != header.Length)
                    throw new InvalidDataException("Save is truncated.");
                for (int i = 0; i < header.Length; i++)
                    if (header[i] != Magic[i]) return new ModStamp[0];
                int version = stream.ReadByte();
                if (version == 1 || version == 2) return new ModStamp[0];
                if (version != Version) throw new InvalidDataException("Unsupported save version: " + version);
                return ReadMods(stream);
            }
        }

        static void WriteMods(Stream stream, ModStamp[] mods)
        {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(mods.Length);
            foreach (ModStamp mod in mods)
            {
                writer.Write(mod.Name ?? String.Empty);
                writer.Write(mod.Version ?? String.Empty);
            }
        }

        static ModStamp[] ReadMods(Stream stream)
        {
            BinaryReader reader = new BinaryReader(stream);
            int count = reader.ReadInt32();
            if (count < 0 || count > 1000) throw new InvalidDataException("Invalid mod count.");
            ModStamp[] mods = new ModStamp[count];
            for (int i = 0; i < count; i++)
            {
                string name = reader.ReadString();
                string version = reader.ReadString();
                if (name.Length == 0 || name.Length > 256 || version.Length > 128)
                    throw new InvalidDataException("Invalid mod identity.");
                mods[i] = new ModStamp { Name = name, Version = version };
            }
            return mods;
        }

        static object Read(string path, Action<object> validate, out ModStamp[] mods)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                byte[] header = new byte[Magic.Length];
                if (stream.Read(header, 0, header.Length) != header.Length)
                    throw new InvalidDataException("Save is truncated.");
                bool marked = true;
                for (int i = 0; i < header.Length; i++)
                    if (header[i] != Magic[i]) marked = false;
                int version = 0;
                if (marked)
                {
                    version = stream.ReadByte();
                    if (version != 1 && version != 2 && version != Version)
                        throw new InvalidDataException("Unsupported save version: " + version);
                }
                else
                    stream.Position = 0;
                mods = version == Version ? ReadMods(stream) : new ModStamp[0];
                object value;
                if (version == 2 || version == Version)
                {
                    using (GZipStream compressed = new GZipStream(stream, CompressionMode.Decompress, true))
                        value = ObjectGraphStore.Read(compressed);
                }
                else
                    value = new BinaryFormatter().Deserialize(stream);
                if (validate != null) validate(value);
                return value;
            }
        }
    }
}
