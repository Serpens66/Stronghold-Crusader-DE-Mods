using MessagePack;
using MessagePack.Formatters;
using System;
using System.Buffers;

namespace RandomEvents
{
    internal enum RandomEventsCooldownEncoding { None, SharedDense, IndividualSparse, IndividualDense }

    [MessagePackObject, MessagePackFormatter(typeof(RandomEventsInitializationChorePacketFormatter))]
    public sealed class RandomEventsInitializationChorePacket
    {
        [Key(0)] public int ProtocolVersion;
        [Key(1)] public int OperationId;
        [Key(2)] public byte[] ConfigurationDigest = Array.Empty<byte>();
        [Key(3)] public ulong PrngState0;
        [Key(4)] public ulong PrngState1;
        [Key(5)] public int NextDueAbsoluteMonth;
        [Key(6)] public int StartAbsoluteMonth;
        [Key(7)] public int CooldownEncoding;
        [Key(8)] public int[] CooldownData = Array.Empty<int>();
    }

    [MessagePackObject, MessagePackFormatter(typeof(RandomEventsBatchChorePacketFormatter))]
    public sealed class RandomEventsBatchChorePacket
    {
        [Key(0)] public int ProtocolVersion;
        [Key(1)] public int OperationId;
        [Key(2)] public ulong PrngState0;
        [Key(3)] public ulong PrngState1;
        [Key(4)] public int DueAbsoluteMonth;
        [Key(5)] public int[] EventKinds = Array.Empty<int>();
        [Key(6)] public int[] EventStrengths = Array.Empty<int>();
        [Key(7)] public int[] TargetPlayerIds = Array.Empty<int>();
    }

    [MessagePackObject, MessagePackFormatter(typeof(RandomEventsSignpostChorePacketFormatter))]
    public sealed class RandomEventsSignpostChorePacket
    {
        [Key(0)] public int ProtocolVersion;
        [Key(1)] public int OperationId;
    }

    public sealed class RandomEventsInitializationChorePacketFormatter : IMessagePackFormatter<RandomEventsInitializationChorePacket>
    {
        private const int FieldCount = 9;
        public void Serialize(ref MessagePackWriter writer, RandomEventsInitializationChorePacket value, MessagePackSerializerOptions options)
        {
            if (value == null) { writer.WriteNil(); return; }
            writer.WriteArrayHeader(FieldCount);
            writer.Write(value.ProtocolVersion); writer.Write(value.OperationId);
            RandomEventsMessagePack.WriteByteArray(ref writer, value.ConfigurationDigest);
            writer.Write(value.PrngState0); writer.Write(value.PrngState1);
            writer.Write(value.NextDueAbsoluteMonth); writer.Write(value.StartAbsoluteMonth);
            writer.Write(value.CooldownEncoding);
            RandomEventsMessagePack.WriteIntArray(ref writer, value.CooldownData);
        }

        public RandomEventsInitializationChorePacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;
            RandomEventsMessagePack.RequireFieldCount(ref reader, FieldCount, "initialization Chore");
            return new RandomEventsInitializationChorePacket
            {
                ProtocolVersion = reader.ReadInt32(), OperationId = reader.ReadInt32(),
                ConfigurationDigest = RandomEventsMessagePack.ReadByteArray(ref reader, 32),
                PrngState0 = reader.ReadUInt64(), PrngState1 = reader.ReadUInt64(),
                NextDueAbsoluteMonth = reader.ReadInt32(), StartAbsoluteMonth = reader.ReadInt32(),
                CooldownEncoding = reader.ReadInt32(), CooldownData = RandomEventsMessagePack.ReadIntArray(ref reader, 240)
            };
        }
    }

    public sealed class RandomEventsBatchChorePacketFormatter : IMessagePackFormatter<RandomEventsBatchChorePacket>
    {
        private const int FieldCount = 8;
        public void Serialize(ref MessagePackWriter writer, RandomEventsBatchChorePacket value, MessagePackSerializerOptions options)
        {
            if (value == null) { writer.WriteNil(); return; }
            writer.WriteArrayHeader(FieldCount);
            writer.Write(value.ProtocolVersion); writer.Write(value.OperationId);
            writer.Write(value.PrngState0); writer.Write(value.PrngState1); writer.Write(value.DueAbsoluteMonth);
            RandomEventsMessagePack.WriteIntArray(ref writer, value.EventKinds);
            RandomEventsMessagePack.WriteIntArray(ref writer, value.EventStrengths);
            RandomEventsMessagePack.WriteIntArray(ref writer, value.TargetPlayerIds);
        }

        public RandomEventsBatchChorePacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;
            RandomEventsMessagePack.RequireFieldCount(ref reader, FieldCount, "batch Chore");
            return new RandomEventsBatchChorePacket
            {
                ProtocolVersion = reader.ReadInt32(), OperationId = reader.ReadInt32(),
                PrngState0 = reader.ReadUInt64(), PrngState1 = reader.ReadUInt64(), DueAbsoluteMonth = reader.ReadInt32(),
                EventKinds = RandomEventsMessagePack.ReadIntArray(ref reader, 135),
                EventStrengths = RandomEventsMessagePack.ReadIntArray(ref reader, 135),
                TargetPlayerIds = RandomEventsMessagePack.ReadIntArray(ref reader, 135)
            };
        }
    }

    public sealed class RandomEventsSignpostChorePacketFormatter : IMessagePackFormatter<RandomEventsSignpostChorePacket>
    {
        public void Serialize(ref MessagePackWriter writer, RandomEventsSignpostChorePacket value, MessagePackSerializerOptions options)
        {
            if (value == null) { writer.WriteNil(); return; }
            writer.WriteArrayHeader(2); writer.Write(value.ProtocolVersion); writer.Write(value.OperationId);
        }

        public RandomEventsSignpostChorePacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;
            RandomEventsMessagePack.RequireFieldCount(ref reader, 2, "signpost Chore");
            return new RandomEventsSignpostChorePacket { ProtocolVersion = reader.ReadInt32(), OperationId = reader.ReadInt32() };
        }
    }

    internal static class RandomEventsMessagePack
    {
        public static void RequireFieldCount(ref MessagePackReader reader, int expected, string label)
        {
            int count = reader.ReadArrayHeader();
            if (count != expected)
                throw new MessagePackSerializationException($"RandomEvents {label} has {count} fields; expected exactly {expected}.");
        }

        public static void WriteIntArray(ref MessagePackWriter writer, int[] values)
        {
            if (values == null) { writer.WriteNil(); return; }
            writer.WriteArrayHeader(values.Length);
            for (int index = 0; index < values.Length; index++) writer.Write(values[index]);
        }

        public static int[] ReadIntArray(ref MessagePackReader reader, int maximumLength)
        {
            if (reader.TryReadNil()) return Array.Empty<int>();
            int length = reader.ReadArrayHeader();
            if (length < 0 || length > maximumLength)
                throw new MessagePackSerializationException($"RandomEvents integer-array length {length} exceeds {maximumLength}.");
            int[] values = new int[length];
            for (int index = 0; index < length; index++) values[index] = reader.ReadInt32();
            return values;
        }

        public static void WriteByteArray(ref MessagePackWriter writer, byte[] values) => writer.Write(values ?? Array.Empty<byte>());
        public static byte[] ReadByteArray(ref MessagePackReader reader, int expectedLength)
        {
            byte[] values = reader.ReadBytes()?.ToArray() ?? Array.Empty<byte>();
            if (values.Length != expectedLength)
                throw new MessagePackSerializationException($"RandomEvents byte-array length {values.Length}; expected {expectedLength}.");
            return values;
        }
    }
}
