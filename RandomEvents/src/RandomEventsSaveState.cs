using MessagePack;
using MessagePack.Formatters;
using System;

namespace RandomEvents
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(RandomEventsSaveStateFormatter))]
    public sealed class RandomEventsSaveState
    {
        public const int CurrentSchemaVersion = 1;
        [Key(0)] public int SchemaVersion = CurrentSchemaVersion;
        [Key(1)] public ulong PrngState0;
        [Key(2)] public ulong PrngState1;
        [Key(3)] public int NextDueAbsoluteMonth;
        [Key(4)] public int StartAbsoluteMonth;
        [Key(5)] public int[] SharedCooldownUntilAbsoluteMonths = Array.Empty<int>();
        [Key(6)] public int[] IndividualCooldownUntilAbsoluteMonths = Array.Empty<int>();
        [Key(7)] public bool BatchPrepared;
        [Key(8)] public int[] PreparedDirectKinds = Array.Empty<int>();
        [Key(9)] public int[] PreparedDirectStrengths = Array.Empty<int>();
        [Key(10)] public int[] PreparedDirectTargetPlayerIds = Array.Empty<int>();
        [Key(11)] public bool SignpostsInitialized;
        [Key(12)] public int[] SignpostBuildingIds = new[] { -1, -1, -1, -1 };
    }

    public sealed class RandomEventsSaveStateFormatter : IMessagePackFormatter<RandomEventsSaveState>
    {
        private const int FieldCount = 13;

        public void Serialize(ref MessagePackWriter writer, RandomEventsSaveState value, MessagePackSerializerOptions options)
        {
            if (value == null) { writer.WriteNil(); return; }
            writer.WriteArrayHeader(FieldCount);
            writer.Write(value.SchemaVersion);
            writer.Write(value.PrngState0);
            writer.Write(value.PrngState1);
            writer.Write(value.NextDueAbsoluteMonth);
            writer.Write(value.StartAbsoluteMonth);
            RandomEventsMessagePack.WriteIntArray(ref writer, value.SharedCooldownUntilAbsoluteMonths);
            RandomEventsMessagePack.WriteIntArray(ref writer, value.IndividualCooldownUntilAbsoluteMonths);
            writer.Write(value.BatchPrepared);
            RandomEventsMessagePack.WriteIntArray(ref writer, value.PreparedDirectKinds);
            RandomEventsMessagePack.WriteIntArray(ref writer, value.PreparedDirectStrengths);
            RandomEventsMessagePack.WriteIntArray(ref writer, value.PreparedDirectTargetPlayerIds);
            writer.Write(value.SignpostsInitialized);
            RandomEventsMessagePack.WriteIntArray(ref writer, value.SignpostBuildingIds);
        }

        public RandomEventsSaveState Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;
            RandomEventsMessagePack.RequireFieldCount(ref reader, FieldCount, "save state");
            return new RandomEventsSaveState
            {
                SchemaVersion = reader.ReadInt32(),
                PrngState0 = reader.ReadUInt64(),
                PrngState1 = reader.ReadUInt64(),
                NextDueAbsoluteMonth = reader.ReadInt32(),
                StartAbsoluteMonth = reader.ReadInt32(),
                SharedCooldownUntilAbsoluteMonths = RandomEventsMessagePack.ReadIntArray(ref reader, RandomEventsCooldownCodec.EventCount),
                IndividualCooldownUntilAbsoluteMonths = RandomEventsMessagePack.ReadIntArray(ref reader, RandomEventsCooldownCodec.FullIndividualLength),
                BatchPrepared = reader.ReadBoolean(),
                PreparedDirectKinds = RandomEventsMessagePack.ReadIntArray(ref reader, RandomEventsBatchValidator.MaximumActionCount),
                PreparedDirectStrengths = RandomEventsMessagePack.ReadIntArray(ref reader, RandomEventsBatchValidator.MaximumActionCount),
                PreparedDirectTargetPlayerIds = RandomEventsMessagePack.ReadIntArray(ref reader, RandomEventsBatchValidator.MaximumActionCount),
                SignpostsInitialized = reader.ReadBoolean(),
                SignpostBuildingIds = RandomEventsMessagePack.ReadIntArray(ref reader, 4)
            };
        }
    }
}
