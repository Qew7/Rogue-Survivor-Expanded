using System;
using System.IO;
using System.IO.Compression;
using djack.RogueSurvivor.Data;

namespace djack.RogueSurvivor.Engine
{
    static partial class BinarySaveStore
    {
        static ModStamp[] ReadEnvelope(Stream stream)
        { bool ignored; return ReadEnvelope(stream, false, out ignored); }
        static ModStamp[] ReadEnvelope(Stream stream, bool allowSettings, out bool legacy)
        {
            BinaryReader reader = new BinaryReader(stream);
            foreach (byte expected in Magic)
                if (reader.ReadByte() != expected) throw new InvalidDataException("Not a current save. Start a new game.");
            int version = reader.ReadByte();
            legacy = version == 4 && allowSettings;
            if (version != Version && !legacy) throw new InvalidDataException("Unsupported save format " + version + "; start a new game.");
            CheckGameVersion(stream); return ReadMods(stream);
        }
        static void WriteCompressed(Stream stream, object value, bool omitArchive)
        {
            using (GZipStream compressed = new GZipStream(stream, CompressionMode.Compress, true))
            using (BufferedStream buffer = new BufferedStream(compressed, 65536))
                ObjectGraphStore.Write(buffer, value, omitArchive);
        }
        static void WriteRecordsSection(Stream stream, Session session, bool refresh)
        {
            BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(session != null);
            if (session == null) return;
            bool enabled = session.GamePreset.NpcPersonalitiesEnabled;
            writer.Write(enabled); writer.Write(session.WorldTime.TurnCounter);
            long sizePosition = stream.Position; writer.Write(0L);
            long start = stream.Position;
            if (enabled && refresh)
            {
                session.ResidentRecords.Refresh(session);
            }
            // Keep history even if personalities are disabled; the viewer still rejects that preset.
            WriteCompressed(stream, session.ResidentRecords, false);
            long end = stream.Position;
            stream.Position = sizePosition; writer.Write(end - start); stream.Position = end;
        }
        static ResidentRecords ReadRecordsSection(Stream stream, bool load,
            out bool isSession, out bool enabled, out int turn, bool requireEnabled = false)
        {
            BinaryReader reader = new BinaryReader(stream);
            isSession = reader.ReadBoolean(); enabled = false; turn = 0;
            if (!isSession) return null;
            enabled = reader.ReadBoolean(); turn = reader.ReadInt32(); long length = reader.ReadInt64();
            if (turn < 0 || length <= 0 || length > stream.Length - stream.Position) throw new InvalidDataException("Invalid records section.");
            if (requireEnabled && !enabled) throw new InvalidDataException("NPC traits and memories are disabled in this save.");
            if (!load) { stream.Position += length; return null; }
            using (SectionStream section = new SectionStream(stream, length))
            using (GZipStream compressed = new GZipStream(section, CompressionMode.Decompress))
            {
                ResidentRecords records = ObjectGraphStore.Read(compressed) as ResidentRecords;
                if (records == null || section.Remaining != 0) throw new InvalidDataException("Invalid resident archive.");
                return records;
            }
        }
        public static RecordsSave ReadRecords(string path)
        {
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                ReadEnvelope(stream);
                bool isSession, enabled; int turn;
                ResidentRecords records = ReadRecordsSection(stream, true, out isSession, out enabled, out turn, true);
                if (!isSession || !enabled) throw new InvalidDataException("NPC traits and memories are disabled in this save.");
                return new RecordsSave(path, turn, records);
            }
        }
        // Stops GZip read-ahead at the section boundary; never closes the parent file.
        sealed class SectionStream : Stream
        {
            readonly Stream source;
            public long Remaining { get; private set; }
            public SectionStream(Stream source, long length) { this.source = source; Remaining = length; }
            public override bool CanRead { get { return true; } }
            public override bool CanWrite { get { return false; } }
            public override bool CanSeek { get { return false; } }
            public override long Length { get { throw new NotSupportedException(); } }
            public override long Position { get { throw new NotSupportedException(); } set { throw new NotSupportedException(); } }
            public override int Read(byte[] buffer, int offset, int count)
            {
                int read = source.Read(buffer, offset, (int)Math.Min(count, Remaining));
                if (read == 0 && Remaining != 0 && count != 0) throw new EndOfStreamException();
                Remaining -= read; return read;
            }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
            public override void SetLength(long length) { throw new NotSupportedException(); }
            public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        }
    }
}
