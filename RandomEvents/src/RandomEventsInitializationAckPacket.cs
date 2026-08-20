using MessagePack;
using MessagePack.Formatters;
using System;

namespace RandomEvents
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(RandomEventsInitializationAckPacketFormatter))]
    public sealed class RandomEventsInitializationAckPacket
    {
        [Key(0)] public int ProtocolVersion;
        [Key(1)] public int OperationId;
        [Key(2)] public int PlayerId;
        [Key(3)] public string StateDigest = string.Empty;
    }

    public sealed class RandomEventsInitializationAckPacketFormatter : IMessagePackFormatter<RandomEventsInitializationAckPacket>
    {
        private const int FieldCount = 4;

        public void Serialize(
            ref MessagePackWriter writer,
            RandomEventsInitializationAckPacket value,
            MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(FieldCount);
            writer.Write(value.ProtocolVersion);
            writer.Write(value.OperationId);
            writer.Write(value.PlayerId);
            writer.Write(value.StateDigest ?? string.Empty);
        }

        public RandomEventsInitializationAckPacket Deserialize(
            ref MessagePackReader reader,
            MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            int count = reader.ReadArrayHeader();
            var value = new RandomEventsInitializationAckPacket();
            for (int index = 0; index < count; index++)
            {
                switch (index)
                {
                    case 0: value.ProtocolVersion = reader.ReadInt32(); break;
                    case 1: value.OperationId = reader.ReadInt32(); break;
                    case 2: value.PlayerId = reader.ReadInt32(); break;
                    case 3: value.StateDigest = reader.ReadString() ?? string.Empty; break;
                    default: reader.Skip(); break;
                }
            }
            return value;
        }
    }
}
