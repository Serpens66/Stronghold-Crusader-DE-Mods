using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace VirtualUnitsPrototype
{
    internal sealed class SaveRecord
    {
        public byte Kind; public int GameId; public uint GlobalId; public string TypeId;
        public int DefinitionVersion; public int OriginalMaxHealth; public int OriginalSpeed;
    }

    internal sealed class ControlGroupSaveRecord
    {
        public int Group; public int GameId; public uint GlobalId; public string TypeId;
    }

    internal sealed class SavePayload
    {
        public List<SaveRecord> Records = new List<SaveRecord>();
        public List<ControlGroupSaveRecord> ControlGroups = new List<ControlGroupSaveRecord>();
    }

    internal static class SaveCodec
    {
        private const int LegacyFormatVersion = 1;
        private const int FormatVersion = 2;
        public static byte[] Encode(IEnumerable<SaveRecord> records) => Encode(records, Array.Empty<ControlGroupSaveRecord>());
        public static byte[] Encode(IEnumerable<SaveRecord> records, IEnumerable<ControlGroupSaveRecord> controlGroups)
        {
            var list = new List<SaveRecord>(records);
            var groups = new List<ControlGroupSaveRecord>(controlGroups);
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, new UTF8Encoding(false), true))
            {
                writer.Write(FormatVersion); writer.Write(list.Count);
                foreach (SaveRecord record in list)
                {
                    writer.Write(record.Kind); writer.Write(record.GameId); writer.Write(record.GlobalId);
                    writer.Write(record.TypeId ?? string.Empty); writer.Write(record.DefinitionVersion);
                    writer.Write(record.OriginalMaxHealth); writer.Write(record.OriginalSpeed);
                }
                writer.Write(groups.Count);
                foreach (ControlGroupSaveRecord record in groups)
                {
                    writer.Write(record.Group); writer.Write(record.GameId); writer.Write(record.GlobalId);
                    writer.Write(record.TypeId ?? string.Empty);
                }
                writer.Flush(); return stream.ToArray();
            }
        }
        public static List<SaveRecord> Decode(byte[] data) => DecodePayload(data).Records;
        public static SavePayload DecodePayload(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            using (var stream = new MemoryStream(data, false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                int version = reader.ReadInt32();
                if (version != LegacyFormatVersion && version != FormatVersion) throw new InvalidDataException("Unsupported save format.");
                int count = reader.ReadInt32();
                if (count < 0 || count > 100000) throw new InvalidDataException("Invalid save record count.");
                var result = new SavePayload();
                for (int index = 0; index < count; index++)
                    result.Records.Add(new SaveRecord { Kind = reader.ReadByte(), GameId = reader.ReadInt32(), GlobalId = reader.ReadUInt32(), TypeId = reader.ReadString(), DefinitionVersion = reader.ReadInt32(), OriginalMaxHealth = reader.ReadInt32(), OriginalSpeed = reader.ReadInt32() });
                if (version == FormatVersion)
                {
                    int groupCount = reader.ReadInt32();
                    if (groupCount < 0 || groupCount > 100000) throw new InvalidDataException("Invalid control-group record count.");
                    for (int index = 0; index < groupCount; index++) result.ControlGroups.Add(new ControlGroupSaveRecord
                    { Group = reader.ReadInt32(), GameId = reader.ReadInt32(), GlobalId = reader.ReadUInt32(), TypeId = reader.ReadString() });
                }
                if (stream.Position != stream.Length) throw new InvalidDataException("Trailing save data.");
                return result;
            }
        }
    }
}
