using MessagePack;
using MessagePack.Formatters;
using System;
using System.Buffers;

namespace ChoreTestMod
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(ChoreTestPacketFormatter))]
    internal sealed class ChoreTestPacket
    {
        [Key(0)] public int ProtocolVersion;
        [Key(1)] public int PlayerId;
        [Key(2)] public int OperationId;
        [Key(3)] public int LordGlobalId;

        // This is populated only while deserializing so the receiver can report the
        // exact bytes delivered by Chore 106 before any values are normalized.
        [IgnoreMember] public byte[] ReceivedBody;
    }

    internal sealed class ChoreTestPacketFormatter : IMessagePackFormatter<ChoreTestPacket>
    {
        private const int FieldCount = 4;

        public void Serialize(
            ref MessagePackWriter writer,
            ChoreTestPacket value,
            MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(FieldCount);
            writer.Write(value.ProtocolVersion);
            writer.Write(value.PlayerId);
            writer.Write(value.OperationId);
            writer.Write(value.LordGlobalId);
        }

        public ChoreTestPacket Deserialize(
            ref MessagePackReader reader,
            MessagePackSerializerOptions options)
        {
            byte[] receivedBody = reader.Sequence.Slice(reader.Position).ToArray();

            try
            {
                if (reader.TryReadNil())
                    return null;

                int fieldCount = reader.ReadArrayHeader();
                if (fieldCount != FieldCount)
                    throw new MessagePackSerializationException($"Expected {FieldCount} fields, received {fieldCount}.");

                return new ChoreTestPacket
                {
                    ProtocolVersion = reader.ReadInt32(),
                    PlayerId = reader.ReadInt32(),
                    OperationId = reader.ReadInt32(),
                    LordGlobalId = reader.ReadInt32(),
                    ReceivedBody = receivedBody
                };
            }
            catch (Exception exception)
            {
                ChoreTestModPlugin.ReportDecodeFailure(receivedBody, exception);
                throw;
            }
        }
    }
}
