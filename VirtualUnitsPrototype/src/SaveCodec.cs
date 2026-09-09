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

    internal static class SaveCodec
    {
        private const int FormatVersion = 1;
        public static byte[] Encode(IEnumerable<SaveRecord> records)
        {
            var list = new List<SaveRecord>(records);
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
                writer.Flush(); return stream.ToArray();
            }
        }
        public static List<SaveRecord> Decode(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            using (var stream = new MemoryStream(data, false))
            using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
            {
                if (reader.ReadInt32() != FormatVersion) throw new InvalidDataException("Unsupported save format.");
                int count = reader.ReadInt32();
                if (count < 0 || count > 100000) throw new InvalidDataException("Invalid save record count.");
                var result = new List<SaveRecord>(count);
                for (int index = 0; index < count; index++)
                    result.Add(new SaveRecord { Kind = reader.ReadByte(), GameId = reader.ReadInt32(), GlobalId = reader.ReadUInt32(), TypeId = reader.ReadString(), DefinitionVersion = reader.ReadInt32(), OriginalMaxHealth = reader.ReadInt32(), OriginalSpeed = reader.ReadInt32() });
                if (stream.Position != stream.Length) throw new InvalidDataException("Trailing save data.");
                return result;
            }
        }
    }
}
