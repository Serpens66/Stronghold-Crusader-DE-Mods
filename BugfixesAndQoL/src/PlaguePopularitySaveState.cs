using MessagePack;
using MessagePack.Formatters;
using System;

namespace BugfixesAndQoL
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(PlaguePopularitySaveStateFormatter))]
    public sealed class PlaguePopularitySaveState
    {
        public const int CurrentVersion = 1;

        [Key(0)] public int Version = CurrentVersion;
        [Key(1)] public int[] ManagedPlayerIds = Array.Empty<int>();
        [Key(2)] public PlagueHerdSaveRecord[] Herds = Array.Empty<PlagueHerdSaveRecord>();
    }

    [MessagePackObject]
    [MessagePackFormatter(typeof(PlagueHerdSaveRecordFormatter))]
    public sealed class PlagueHerdSaveRecord
    {
        [Key(0)] public int PlayerId;
        [Key(1)] public int[] ProjectileSlotIds = Array.Empty<int>();
        [Key(2)] public uint[] ProjectileGlobalIds = Array.Empty<uint>();
    }

    public sealed class PlaguePopularitySaveStateFormatter : IMessagePackFormatter<PlaguePopularitySaveState>
    {
        private const int FieldCount = 3;
        private static readonly PlagueHerdSaveRecordFormatter HerdFormatter =
            new PlagueHerdSaveRecordFormatter();

        public void Serialize(ref MessagePackWriter writer, PlaguePopularitySaveState value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(FieldCount);
            writer.Write(value.Version);
            WriteIntArray(ref writer, value.ManagedPlayerIds);
            PlagueHerdSaveRecord[] herds = value.Herds ?? Array.Empty<PlagueHerdSaveRecord>();
            writer.WriteArrayHeader(herds.Length);
            for (int index = 0; index < herds.Length; index++)
                HerdFormatter.Serialize(ref writer, herds[index], options);
        }

        public PlaguePopularitySaveState Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            int count = reader.ReadArrayHeader();
            PlaguePopularitySaveState value = new PlaguePopularitySaveState();
            for (int index = 0; index < count; index++)
            {
                switch (index)
                {
                    case 0:
                        value.Version = reader.ReadInt32();
                        break;
                    case 1:
                        value.ManagedPlayerIds = ReadIntArray(ref reader);
                        break;
                    case 2:
                        value.Herds = ReadHerdArray(ref reader, options);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }
            return value;
        }

        private static void WriteIntArray(ref MessagePackWriter writer, int[] values)
        {
            values = values ?? Array.Empty<int>();
            writer.WriteArrayHeader(values.Length);
            for (int index = 0; index < values.Length; index++)
                writer.Write(values[index]);
        }

        private static int[] ReadIntArray(ref MessagePackReader reader)
        {
            if (reader.TryReadNil())
                return Array.Empty<int>();
            int length = reader.ReadArrayHeader();
            int[] values = new int[length];
            for (int index = 0; index < length; index++)
                values[index] = reader.ReadInt32();
            return values;
        }

        private static PlagueHerdSaveRecord[] ReadHerdArray(
            ref MessagePackReader reader,
            MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return Array.Empty<PlagueHerdSaveRecord>();
            int length = reader.ReadArrayHeader();
            PlagueHerdSaveRecord[] values = new PlagueHerdSaveRecord[length];
            for (int index = 0; index < length; index++)
                values[index] = HerdFormatter.Deserialize(ref reader, options);
            return values;
        }
    }

    public sealed class PlagueHerdSaveRecordFormatter : IMessagePackFormatter<PlagueHerdSaveRecord>
    {
        private const int FieldCount = 3;

        public void Serialize(ref MessagePackWriter writer, PlagueHerdSaveRecord value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(FieldCount);
            writer.Write(value.PlayerId);
            WriteIntArray(ref writer, value.ProjectileSlotIds);
            WriteUIntArray(ref writer, value.ProjectileGlobalIds);
        }

        public PlagueHerdSaveRecord Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            int count = reader.ReadArrayHeader();
            PlagueHerdSaveRecord value = new PlagueHerdSaveRecord();
            for (int index = 0; index < count; index++)
            {
                switch (index)
                {
                    case 0:
                        value.PlayerId = reader.ReadInt32();
                        break;
                    case 1:
                        value.ProjectileSlotIds = ReadIntArray(ref reader);
                        break;
                    case 2:
                        value.ProjectileGlobalIds = ReadUIntArray(ref reader);
                        break;
                    default:
                        reader.Skip();
                        break;
                }
            }
            return value;
        }

        private static void WriteIntArray(ref MessagePackWriter writer, int[] values)
        {
            values = values ?? Array.Empty<int>();
            writer.WriteArrayHeader(values.Length);
            for (int index = 0; index < values.Length; index++)
                writer.Write(values[index]);
        }

        private static int[] ReadIntArray(ref MessagePackReader reader)
        {
            if (reader.TryReadNil())
                return Array.Empty<int>();
            int length = reader.ReadArrayHeader();
            int[] values = new int[length];
            for (int index = 0; index < length; index++)
                values[index] = reader.ReadInt32();
            return values;
        }

        private static void WriteUIntArray(ref MessagePackWriter writer, uint[] values)
        {
            values = values ?? Array.Empty<uint>();
            writer.WriteArrayHeader(values.Length);
            for (int index = 0; index < values.Length; index++)
                writer.Write(values[index]);
        }

        private static uint[] ReadUIntArray(ref MessagePackReader reader)
        {
            if (reader.TryReadNil())
                return Array.Empty<uint>();
            int length = reader.ReadArrayHeader();
            uint[] values = new uint[length];
            for (int index = 0; index < length; index++)
                values[index] = reader.ReadUInt32();
            return values;
        }
    }
}
