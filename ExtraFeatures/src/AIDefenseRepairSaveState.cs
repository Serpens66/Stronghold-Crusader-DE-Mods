// Feature: Persist minimal deterministic AI tower/gatehouse rebuild state.
using MessagePack;
using MessagePack.Formatters;
using System;

namespace ExtraFeatures
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(AIDefenseRepairSaveStateFormatter))]
    internal sealed class AIDefenseRepairSaveState
    {
        internal const int CurrentVersion = 2;

        [Key(0)] public int Version = CurrentVersion;
        [Key(1)] public AIDefenseRebuildSaveRecord[] Targets = Array.Empty<AIDefenseRebuildSaveRecord>();
    }

    [MessagePackObject]
    [MessagePackFormatter(typeof(AIDefenseRebuildSaveRecordFormatter))]
    internal sealed class AIDefenseRebuildSaveRecord
    {
        [Key(0)] public int PlayerId;
        [Key(1)] public int ActiveLayout;
        [Key(2)] public int FrameIndex;
        [Key(3)] public short Mapper;
        [Key(4)] public int TargetTileId;
        [Key(5)] public int MissingElapsedTicks = -1;
    }

    internal sealed class AIDefenseRepairSaveStateFormatter : IMessagePackFormatter<AIDefenseRepairSaveState>
    {
        public void Serialize(ref MessagePackWriter writer, AIDefenseRepairSaveState value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(2);
            writer.Write(value.Version);
            WriteRecords(ref writer, value.Targets, options);
        }

        public AIDefenseRepairSaveState Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            int count = reader.ReadArrayHeader();
            if (count > 3)
                throw new MessagePackSerializationException("AI defense rebuild state has too many fields.");

            var value = new AIDefenseRepairSaveState();
            if (count == 0)
                return value;

            value.Version = reader.ReadInt32();
            for (int index = 1; index < count; index++)
            {
                // Version 1 stored unrelated detailed damage/rebuild records. Skip them safely.
                if (value.Version != AIDefenseRepairSaveState.CurrentVersion || index != 1)
                    reader.Skip();
                else
                    value.Targets = ReadRecords(ref reader, options);
            }
            return value;
        }

        private static void WriteRecords(
            ref MessagePackWriter writer,
            AIDefenseRebuildSaveRecord[] records,
            MessagePackSerializerOptions options)
        {
            records = records ?? Array.Empty<AIDefenseRebuildSaveRecord>();
            writer.WriteArrayHeader(records.Length);
            IMessagePackFormatter<AIDefenseRebuildSaveRecord> formatter =
                options.Resolver.GetFormatterWithVerify<AIDefenseRebuildSaveRecord>();
            for (int index = 0; index < records.Length; index++)
                formatter.Serialize(ref writer, records[index], options);
        }

        private static AIDefenseRebuildSaveRecord[] ReadRecords(
            ref MessagePackReader reader,
            MessagePackSerializerOptions options)
        {
            int count = reader.ReadArrayHeader();
            if (count < 0 || count > 20000)
                throw new MessagePackSerializationException("AI defense rebuild state has an invalid target count.");
            var records = new AIDefenseRebuildSaveRecord[count];
            IMessagePackFormatter<AIDefenseRebuildSaveRecord> formatter =
                options.Resolver.GetFormatterWithVerify<AIDefenseRebuildSaveRecord>();
            for (int index = 0; index < count; index++)
                records[index] = formatter.Deserialize(ref reader, options);
            return records;
        }
    }

    internal sealed class AIDefenseRebuildSaveRecordFormatter : IMessagePackFormatter<AIDefenseRebuildSaveRecord>
    {
        public void Serialize(ref MessagePackWriter writer, AIDefenseRebuildSaveRecord value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(6);
            writer.Write(value.PlayerId);
            writer.Write(value.ActiveLayout);
            writer.Write(value.FrameIndex);
            writer.Write(value.Mapper);
            writer.Write(value.TargetTileId);
            writer.Write(value.MissingElapsedTicks);
        }

        public AIDefenseRebuildSaveRecord Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            int count = reader.ReadArrayHeader();
            if (count > 6)
                throw new MessagePackSerializationException("AI defense rebuild target has too many fields.");
            var value = new AIDefenseRebuildSaveRecord();
            for (int index = 0; index < count; index++)
            {
                switch (index)
                {
                    case 0: value.PlayerId = reader.ReadInt32(); break;
                    case 1: value.ActiveLayout = reader.ReadInt32(); break;
                    case 2: value.FrameIndex = reader.ReadInt32(); break;
                    case 3: value.Mapper = reader.ReadInt16(); break;
                    case 4: value.TargetTileId = reader.ReadInt32(); break;
                    case 5: value.MissingElapsedTicks = reader.ReadInt32(); break;
                    default: reader.Skip(); break;
                }
            }
            return value;
        }
    }
}
