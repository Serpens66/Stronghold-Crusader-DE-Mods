// Feature: Persist AI tower/gate rebuild-delay targets that are missing when the game is saved.
using MessagePack;
using MessagePack.Formatters;
using System;

namespace ExtraFeatures
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(AIDefenseRebuildSaveStateFormatter))]
    internal sealed class AIDefenseRebuildSaveState
    {
        internal const int CurrentVersion = 1;
        // Eight players times the AIV frame limit 0x922 (AIDefenseRepairRuntime.MaximumFrameCount);
        // a literal so the payload bound stays independent of runtime types.
        internal const int MaximumRecords = 8 * 0x922;

        [Key(0)] public int Version = CurrentVersion;
        [Key(1)] public AIDefenseRebuildSaveRecord[] Targets = Array.Empty<AIDefenseRebuildSaveRecord>();
    }

    [MessagePackObject]
    [MessagePackFormatter(typeof(AIDefenseRebuildSaveRecordFormatter))]
    internal sealed class AIDefenseRebuildSaveRecord
    {
        internal const byte DestroyedFlag = 1;
        internal const byte DelayFlag = 2;
        internal const byte DelayFromConfirmedDestructionFlag = 4;
        internal const byte KnownFlags = DestroyedFlag | DelayFlag | DelayFromConfirmedDestructionFlag;

        [Key(0)] public int PlayerId;
        [Key(1)] public int TileX;
        [Key(2)] public int TileY;
        [Key(3)] public byte Family;
        [Key(4)] public byte Flags;
        [Key(5)] public int DestroyedTick;
        [Key(6)] public int DelayStartTick;
    }

    internal sealed class AIDefenseRebuildSaveStateFormatter : IMessagePackFormatter<AIDefenseRebuildSaveState>
    {
        public void Serialize(ref MessagePackWriter writer, AIDefenseRebuildSaveState value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(2);
            writer.Write(value.Version);
            AIDefenseRebuildSaveRecord[] records = value.Targets ?? Array.Empty<AIDefenseRebuildSaveRecord>();
            writer.WriteArrayHeader(records.Length);
            IMessagePackFormatter<AIDefenseRebuildSaveRecord> formatter =
                options.Resolver.GetFormatterWithVerify<AIDefenseRebuildSaveRecord>();
            for (int index = 0; index < records.Length; index++)
                formatter.Serialize(ref writer, records[index], options);
        }

        public AIDefenseRebuildSaveState Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.ReadArrayHeader() != 2)
                throw new MessagePackSerializationException("AI defense rebuild state has an invalid field count.");

            var value = new AIDefenseRebuildSaveState { Version = reader.ReadInt32() };
            int recordCount = reader.ReadArrayHeader();
            if (recordCount < 0 || recordCount > AIDefenseRebuildSaveState.MaximumRecords)
                throw new MessagePackSerializationException("AI defense rebuild state has an invalid record count.");
            value.Targets = new AIDefenseRebuildSaveRecord[recordCount];
            IMessagePackFormatter<AIDefenseRebuildSaveRecord> formatter =
                options.Resolver.GetFormatterWithVerify<AIDefenseRebuildSaveRecord>();
            for (int index = 0; index < recordCount; index++)
                value.Targets[index] = formatter.Deserialize(ref reader, options);
            return value;
        }
    }

    internal sealed class AIDefenseRebuildSaveRecordFormatter : IMessagePackFormatter<AIDefenseRebuildSaveRecord>
    {
        private const int FieldCount = 7;

        public void Serialize(ref MessagePackWriter writer, AIDefenseRebuildSaveRecord value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(FieldCount);
            writer.Write(value.PlayerId);
            writer.Write(value.TileX);
            writer.Write(value.TileY);
            writer.Write(value.Family);
            writer.Write(value.Flags);
            writer.Write(value.DestroyedTick);
            writer.Write(value.DelayStartTick);
        }

        public AIDefenseRebuildSaveRecord Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.ReadArrayHeader() != FieldCount)
                throw new MessagePackSerializationException("AI defense rebuild record has an invalid field count.");

            return new AIDefenseRebuildSaveRecord
            {
                PlayerId = reader.ReadInt32(),
                TileX = reader.ReadInt32(),
                TileY = reader.ReadInt32(),
                Family = reader.ReadByte(),
                Flags = reader.ReadByte(),
                DestroyedTick = reader.ReadInt32(),
                DelayStartTick = reader.ReadInt32()
            };
        }
    }
}
