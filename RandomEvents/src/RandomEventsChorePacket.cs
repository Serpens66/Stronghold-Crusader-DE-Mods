using MessagePack;
using MessagePack.Formatters;
using System;

namespace RandomEvents
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(RandomEventsChorePacketFormatter))]
    public sealed class RandomEventsChorePacket
    {
        [Key(0)] public int ProtocolVersion;
        [Key(1)] public int CommandType;
        [Key(2)] public int OperationId;
        [Key(3)] public bool EffectiveEnabled;
        [Key(4)] public int IntervalMonths;
        [Key(5)] public int CooldownMonths;
        [Key(6)] public int MultiplayerMode;
        [Key(7)] public int[] Chances = Array.Empty<int>();
        [Key(8)] public int[] StrengthMinimums = Array.Empty<int>();
        [Key(9)] public int[] StrengthMaximums = Array.Empty<int>();
        [Key(10)] public ulong PrngState0;
        [Key(11)] public ulong PrngState1;
        [Key(12)] public int NextDueAbsoluteMonth;
        [Key(13)] public int StartAbsoluteMonth;
        [Key(14)] public int[] SharedCooldownUntilAbsoluteMonths = Array.Empty<int>();
        [Key(15)] public int[] IndividualCooldownUntilAbsoluteMonths = Array.Empty<int>();
        [Key(16)] public bool BatchPrepared;
        [Key(17)] public int[] EventKinds = Array.Empty<int>();
        [Key(18)] public int[] EventStrengths = Array.Empty<int>();
        [Key(19)] public int[] TargetPlayerIds = Array.Empty<int>();
        [Key(20)] public bool SignpostsInitialized;
        [Key(21)] public int[] SignpostBuildingIds = Array.Empty<int>();
    }

    public sealed class RandomEventsChorePacketFormatter : IMessagePackFormatter<RandomEventsChorePacket>
    {
        private const int FieldCount = 22;

        public void Serialize(ref MessagePackWriter writer, RandomEventsChorePacket value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(FieldCount);
            writer.Write(value.ProtocolVersion);
            writer.Write(value.CommandType);
            writer.Write(value.OperationId);
            writer.Write(value.EffectiveEnabled);
            writer.Write(value.IntervalMonths);
            writer.Write(value.CooldownMonths);
            writer.Write(value.MultiplayerMode);
            WriteIntArray(ref writer, value.Chances);
            WriteIntArray(ref writer, value.StrengthMinimums);
            WriteIntArray(ref writer, value.StrengthMaximums);
            writer.Write(value.PrngState0);
            writer.Write(value.PrngState1);
            writer.Write(value.NextDueAbsoluteMonth);
            writer.Write(value.StartAbsoluteMonth);
            WriteIntArray(ref writer, value.SharedCooldownUntilAbsoluteMonths);
            WriteIntArray(ref writer, value.IndividualCooldownUntilAbsoluteMonths);
            writer.Write(value.BatchPrepared);
            WriteIntArray(ref writer, value.EventKinds);
            WriteIntArray(ref writer, value.EventStrengths);
            WriteIntArray(ref writer, value.TargetPlayerIds);
            writer.Write(value.SignpostsInitialized);
            WriteIntArray(ref writer, value.SignpostBuildingIds);
        }

        public RandomEventsChorePacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            int count = reader.ReadArrayHeader();
            var value = new RandomEventsChorePacket();
            for (int index = 0; index < count; index++)
            {
                switch (index)
                {
                    case 0: value.ProtocolVersion = reader.ReadInt32(); break;
                    case 1: value.CommandType = reader.ReadInt32(); break;
                    case 2: value.OperationId = reader.ReadInt32(); break;
                    case 3: value.EffectiveEnabled = reader.ReadBoolean(); break;
                    case 4: value.IntervalMonths = reader.ReadInt32(); break;
                    case 5: value.CooldownMonths = reader.ReadInt32(); break;
                    case 6: value.MultiplayerMode = reader.ReadInt32(); break;
                    case 7: value.Chances = ReadIntArray(ref reader); break;
                    case 8: value.StrengthMinimums = ReadIntArray(ref reader); break;
                    case 9: value.StrengthMaximums = ReadIntArray(ref reader); break;
                    case 10: value.PrngState0 = reader.ReadUInt64(); break;
                    case 11: value.PrngState1 = reader.ReadUInt64(); break;
                    case 12: value.NextDueAbsoluteMonth = reader.ReadInt32(); break;
                    case 13: value.StartAbsoluteMonth = reader.ReadInt32(); break;
                    case 14: value.SharedCooldownUntilAbsoluteMonths = ReadIntArray(ref reader); break;
                    case 15: value.IndividualCooldownUntilAbsoluteMonths = ReadIntArray(ref reader); break;
                    case 16: value.BatchPrepared = reader.ReadBoolean(); break;
                    case 17: value.EventKinds = ReadIntArray(ref reader); break;
                    case 18: value.EventStrengths = ReadIntArray(ref reader); break;
                    case 19: value.TargetPlayerIds = ReadIntArray(ref reader); break;
                    case 20: value.SignpostsInitialized = reader.ReadBoolean(); break;
                    case 21: value.SignpostBuildingIds = ReadIntArray(ref reader); break;
                    default: reader.Skip(); break;
                }
            }
            return value;
        }

        private static void WriteIntArray(ref MessagePackWriter writer, int[] values)
        {
            if (values == null)
            {
                writer.WriteNil();
                return;
            }
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
    }
}
