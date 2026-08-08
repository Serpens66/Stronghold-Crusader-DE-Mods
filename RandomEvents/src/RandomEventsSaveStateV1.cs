using MessagePack;
using MessagePack.Formatters;
using System;

namespace RandomEvents
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(RandomEventsSaveStateV1Formatter))]
    public sealed class RandomEventsSaveStateV1
    {
        public const int CurrentVersion = 1;

        [Key(0)] public int Version = CurrentVersion;
        [Key(1)] public bool EffectiveEnabled;
        [Key(2)] public int IntervalMonths;
        [Key(3)] public int MultiplayerMode;
        [Key(4)] public int[] Chances = Array.Empty<int>();
        [Key(5)] public int[] StrengthMinimums = Array.Empty<int>();
        [Key(6)] public int[] StrengthMaximums = Array.Empty<int>();
        [Key(7)] public ulong PrngState0;
        [Key(8)] public ulong PrngState1;
        [Key(9)] public int NextDueAbsoluteMonth;
        [Key(10)] public int[] PreparedDirectKinds = Array.Empty<int>();
        [Key(11)] public int[] PreparedDirectStrengths = Array.Empty<int>();
        [Key(12)] public int[] PreparedTimelineKinds = Array.Empty<int>();
        [Key(13)] public int[] TimelineEntryIds = new[] { -1, -1, -1, -1, -1 };
        [Key(14)] public bool SignpostsInitialized;
        [Key(15)] public int[] SignpostBuildingIds = new[] { -1, -1, -1, -1 };
        [Key(16)] public bool BatchPrepared;
        [Key(17)] public int[] PreparedTimelineStrengths = Array.Empty<int>();
    }

    public sealed class RandomEventsSaveStateV1Formatter : IMessagePackFormatter<RandomEventsSaveStateV1>
    {
        private const int FieldCount = 18;

        public void Serialize(ref MessagePackWriter writer, RandomEventsSaveStateV1 value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(FieldCount);
            writer.Write(value.Version);
            writer.Write(value.EffectiveEnabled);
            writer.Write(value.IntervalMonths);
            writer.Write(value.MultiplayerMode);
            WriteIntArray(ref writer, value.Chances);
            WriteIntArray(ref writer, value.StrengthMinimums);
            WriteIntArray(ref writer, value.StrengthMaximums);
            writer.Write(value.PrngState0);
            writer.Write(value.PrngState1);
            writer.Write(value.NextDueAbsoluteMonth);
            WriteIntArray(ref writer, value.PreparedDirectKinds);
            WriteIntArray(ref writer, value.PreparedDirectStrengths);
            WriteIntArray(ref writer, value.PreparedTimelineKinds);
            WriteIntArray(ref writer, value.TimelineEntryIds);
            writer.Write(value.SignpostsInitialized);
            WriteIntArray(ref writer, value.SignpostBuildingIds);
            writer.Write(value.BatchPrepared);
            WriteIntArray(ref writer, value.PreparedTimelineStrengths);
        }

        public RandomEventsSaveStateV1 Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            int count = reader.ReadArrayHeader();
            RandomEventsSaveStateV1 value = new RandomEventsSaveStateV1();
            for (int index = 0; index < count; index++)
            {
                switch (index)
                {
                    case 0: value.Version = reader.ReadInt32(); break;
                    case 1: value.EffectiveEnabled = reader.ReadBoolean(); break;
                    case 2: value.IntervalMonths = reader.ReadInt32(); break;
                    case 3: value.MultiplayerMode = reader.ReadInt32(); break;
                    case 4: value.Chances = ReadIntArray(ref reader); break;
                    case 5: value.StrengthMinimums = ReadIntArray(ref reader); break;
                    case 6: value.StrengthMaximums = ReadIntArray(ref reader); break;
                    case 7: value.PrngState0 = reader.ReadUInt64(); break;
                    case 8: value.PrngState1 = reader.ReadUInt64(); break;
                    case 9: value.NextDueAbsoluteMonth = reader.ReadInt32(); break;
                    case 10: value.PreparedDirectKinds = ReadIntArray(ref reader); break;
                    case 11: value.PreparedDirectStrengths = ReadIntArray(ref reader); break;
                    case 12: value.PreparedTimelineKinds = ReadIntArray(ref reader); break;
                    case 13: value.TimelineEntryIds = ReadIntArray(ref reader); break;
                    case 14: value.SignpostsInitialized = reader.ReadBoolean(); break;
                    case 15: value.SignpostBuildingIds = ReadIntArray(ref reader); break;
                    case 16: value.BatchPrepared = reader.ReadBoolean(); break;
                    case 17: value.PreparedTimelineStrengths = ReadIntArray(ref reader); break;
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

    internal struct SavedPrng
    {
        public SavedPrng(ulong state0, ulong state1)
        {
            State0 = state0;
            State1 = state1;
            if ((State0 | State1) == 0)
                State1 = 0x9E3779B97F4A7C15UL;
        }

        public ulong State0;
        public ulong State1;

        public ulong NextUInt64()
        {
            // xoroshiro128+ is tiny, deterministic and its complete state fits in the save data.
            ulong first = State0;
            ulong second = State1;
            ulong result = first + second;
            second ^= first;
            State0 = RotateLeft(first, 55) ^ second ^ (second << 14);
            State1 = RotateLeft(second, 36);
            return result;
        }

        public int Next(int exclusiveMaximum)
        {
            if (exclusiveMaximum <= 0)
                throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
            ulong bound = (uint)exclusiveMaximum;
            ulong threshold = unchecked(0UL - bound) % bound;
            ulong value;
            do
            {
                value = NextUInt64();
            }
            while (value < threshold);
            return (int)(value % bound);
        }

        public int NextInclusive(int minimum, int maximum)
        {
            if (maximum < minimum)
                throw new ArgumentOutOfRangeException(nameof(maximum));
            return minimum + Next(checked(maximum - minimum + 1));
        }

        private static ulong RotateLeft(ulong value, int count) =>
            (value << count) | (value >> (64 - count));
    }
}
