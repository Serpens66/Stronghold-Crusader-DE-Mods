// Feature: Persist deterministic AI defense repair and rebuild cooldowns.
using MessagePack;
using MessagePack.Formatters;
using System;

namespace ExtraFeatures
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(AIDefenseRepairSaveStateFormatter))]
    internal sealed class AIDefenseRepairSaveState
    {
        internal const int CurrentVersion = 1;

        [Key(0)] public int Version = CurrentVersion;
        [Key(1)] public AIDefenseRepairSaveRecord[] Damaged = Array.Empty<AIDefenseRepairSaveRecord>();
        [Key(2)] public AIDefenseRepairSaveRecord[] Destroyed = Array.Empty<AIDefenseRepairSaveRecord>();
    }

    [MessagePackObject]
    [MessagePackFormatter(typeof(AIDefenseRepairSaveRecordFormatter))]
    internal sealed class AIDefenseRepairSaveRecord
    {
        [Key(0)] public int PlayerId;
        [Key(1)] public int Kind;
        [Key(2)] public int BuildingType;
        [Key(3)] public int GlobalId;
        [Key(4)] public int TileId;
        [Key(5)] public int TileXBegin;
        [Key(6)] public int TileYBegin;
        [Key(7)] public int TileXEnd;
        [Key(8)] public int TileYEnd;
        [Key(9)] public int ElapsedTicks;
    }

    internal sealed class AIDefenseRepairSaveStateFormatter : IMessagePackFormatter<AIDefenseRepairSaveState>
    {
        public void Serialize(ref MessagePackWriter writer, AIDefenseRepairSaveState value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(3);
            writer.Write(value.Version);
            WriteRecords(ref writer, value.Damaged, options);
            WriteRecords(ref writer, value.Destroyed, options);
        }

        public AIDefenseRepairSaveState Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            int count = reader.ReadArrayHeader();
            if (count > 3)
                throw new MessagePackSerializationException("AI defense repair state has too many fields.");

            var value = new AIDefenseRepairSaveState();
            for (int index = 0; index < count; index++)
            {
                switch (index)
                {
                    case 0: value.Version = reader.ReadInt32(); break;
                    case 1: value.Damaged = ReadRecords(ref reader, options); break;
                    case 2: value.Destroyed = ReadRecords(ref reader, options); break;
                    default: reader.Skip(); break;
                }
            }
            return value;
        }

        private static void WriteRecords(ref MessagePackWriter writer, AIDefenseRepairSaveRecord[] records, MessagePackSerializerOptions options)
        {
            records = records ?? Array.Empty<AIDefenseRepairSaveRecord>();
            writer.WriteArrayHeader(records.Length);
            IMessagePackFormatter<AIDefenseRepairSaveRecord> formatter =
                options.Resolver.GetFormatterWithVerify<AIDefenseRepairSaveRecord>();
            for (int index = 0; index < records.Length; index++)
                formatter.Serialize(ref writer, records[index], options);
        }

        private static AIDefenseRepairSaveRecord[] ReadRecords(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            int count = reader.ReadArrayHeader();
            if (count < 0 || count > 20000)
                throw new MessagePackSerializationException("AI defense repair state has an invalid record count.");
            var records = new AIDefenseRepairSaveRecord[count];
            IMessagePackFormatter<AIDefenseRepairSaveRecord> formatter =
                options.Resolver.GetFormatterWithVerify<AIDefenseRepairSaveRecord>();
            for (int index = 0; index < count; index++)
                records[index] = formatter.Deserialize(ref reader, options);
            return records;
        }
    }

    internal sealed class AIDefenseRepairSaveRecordFormatter : IMessagePackFormatter<AIDefenseRepairSaveRecord>
    {
        public void Serialize(ref MessagePackWriter writer, AIDefenseRepairSaveRecord value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(10);
            writer.Write(value.PlayerId);
            writer.Write(value.Kind);
            writer.Write(value.BuildingType);
            writer.Write(value.GlobalId);
            writer.Write(value.TileId);
            writer.Write(value.TileXBegin);
            writer.Write(value.TileYBegin);
            writer.Write(value.TileXEnd);
            writer.Write(value.TileYEnd);
            writer.Write(value.ElapsedTicks);
        }

        public AIDefenseRepairSaveRecord Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            int count = reader.ReadArrayHeader();
            if (count > 10)
                throw new MessagePackSerializationException("AI defense repair record has too many fields.");
            var value = new AIDefenseRepairSaveRecord();
            for (int index = 0; index < count; index++)
            {
                switch (index)
                {
                    case 0: value.PlayerId = reader.ReadInt32(); break;
                    case 1: value.Kind = reader.ReadInt32(); break;
                    case 2: value.BuildingType = reader.ReadInt32(); break;
                    case 3: value.GlobalId = reader.ReadInt32(); break;
                    case 4: value.TileId = reader.ReadInt32(); break;
                    case 5: value.TileXBegin = reader.ReadInt32(); break;
                    case 6: value.TileYBegin = reader.ReadInt32(); break;
                    case 7: value.TileXEnd = reader.ReadInt32(); break;
                    case 8: value.TileYEnd = reader.ReadInt32(); break;
                    case 9: value.ElapsedTicks = reader.ReadInt32(); break;
                    default: reader.Skip(); break;
                }
            }
            return value;
        }
    }
}
