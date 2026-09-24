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
        const byte Version = 2;

        public static void Save(string path, object value)
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
            if (path == null) throw new ArgumentNullException("path");
            try
            {
                return Read(path, validate);
            }
            catch (Exception)
            {
                if (!File.Exists(path + ".bak")) throw;
                return Read(path + ".bak", validate);
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

        static object Read(string path, Action<object> validate)
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
                    if (version != 1 && version != Version)
                        throw new InvalidDataException("Unsupported save version: " + version);
                }
                else
                    stream.Position = 0;
                object value;
                if (version == Version)
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
