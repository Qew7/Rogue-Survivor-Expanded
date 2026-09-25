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
        const byte Version = 4;

        sealed class IncompatibleGameVersionException : IOException
        {
            public IncompatibleGameVersionException(string version) : base(
                "Save requires Rogue Survivor Expanded " + version +
                "; current game is " + SetupConfig.GAME_VERSION + ".") { }
        }

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
                    new BinaryWriter(stream).Write(SetupConfig.GAME_VERSION);
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
            catch (Exception error)
            {
                if (error is IncompatibleGameVersionException) throw;
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
            catch (Exception error)
            {
                if (error is IncompatibleGameVersionException) throw;
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
                if (version != 3 && version != Version)
                    throw new InvalidDataException("Unsupported save version: " + version);
                if (version == Version) CheckGameVersion(stream);
                return ReadMods(stream);
            }
        }

        static void CheckGameVersion(Stream stream)
        {
            string savedVersion = new BinaryReader(stream).ReadString();
            if (savedVersion.Length == 0 || savedVersion.Length > 128)
                throw new InvalidDataException("Invalid saved game version.");
            if (!String.Equals(savedVersion, SetupConfig.GAME_VERSION,
                StringComparison.Ordinal))
                throw new IncompatibleGameVersionException(savedVersion);
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
                    if (version != 1 && version != 2 && version != 3 && version != Version)
                        throw new InvalidDataException("Unsupported save version: " + version);
                    if (version == Version) CheckGameVersion(stream);
                }
                else
                    stream.Position = 0;
                mods = version >= 3 ? ReadMods(stream) : new ModStamp[0];
                object value;
                if (version >= 2)
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
