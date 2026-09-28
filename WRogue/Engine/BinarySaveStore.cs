using System;
using System.IO;
using System.IO.Compression;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Engine
{
    // V5 envelope: independently readable archive, then the compact world graph.
    static partial class BinarySaveStore
    {
        static readonly byte[] Magic = { (byte)'R', (byte)'S', (byte)'E', (byte)'1' };
        const byte Version = 5;

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
        { SaveCore(path, value, mods, true); }

        // Diagnostic copy: retain the already captured metrics without accessing model catalogs.
        internal static void SaveSnapshot(string path, object value, ModStamp[] mods)
        { SaveCore(path, value, mods, false); }

        static void SaveCore(string path, object value, ModStamp[] mods, bool refreshRecords)
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
                    Session session = value as Session;
                    WriteRecordsSection(stream, session, refreshRecords);
                    WriteCompressed(stream, value, session != null);
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

        // Archive browsing must read the selected file, never silently replace it with a backup.
        public static T LoadExact<T>(string path)
        {
            ModStamp[] ignored;
            return (T)Read(path, delegate(object value)
            {
                if (!(value is T)) throw new InvalidDataException("Save contains an unexpected object type.");
            }, out ignored);
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
                return ReadEnvelope(stream);
            }
        }

        static void CheckGameVersion(Stream stream)
        {
            string savedVersion = new BinaryReader(stream).ReadString();
            if (savedVersion.Length == 0 || savedVersion.Length > 128)
                throw new InvalidDataException("Invalid saved game version.");
            if (!SetupConfig.SupportsGameVersion(savedVersion))
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
                bool legacy;
                mods = ReadEnvelope(stream, true, out legacy);
                if (legacy)
                {
                    object settings;
                    using (GZipStream compressed = new GZipStream(stream, CompressionMode.Decompress, true))
                        settings = ObjectGraphStore.ReadLegacySettings(compressed);
                    if (validate != null) validate(settings); return settings;
                }
                bool isSession, enabled; int turn;
                ResidentRecords records = ReadRecordsSection(stream, true, out isSession, out enabled, out turn);
                object value;
                using (GZipStream compressed = new GZipStream(stream, CompressionMode.Decompress, true))
                    value = ObjectGraphStore.Read(compressed);
                if (isSession)
                {
                    Session session = value as Session;
                    if (session == null || session.WorldTime.TurnCounter != turn ||
                        session.GamePreset.NpcPersonalitiesEnabled != enabled)
                        throw new InvalidDataException("Archive does not match saved session.");
                    session.RestoreResidentRecords(records);
                }
                if (validate != null) validate(value);
                return value;
            }
        }
    }
}
