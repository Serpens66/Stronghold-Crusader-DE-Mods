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
                var packet = new ChoreTestPacket
                {
                    ReceivedBody = receivedBody
                };

                for (int index = 0; index < fieldCount; index++)
                {
                    switch (index)
                    {
                        case 0: packet.ProtocolVersion = reader.ReadInt32(); break;
                        case 1: packet.PlayerId = reader.ReadInt32(); break;
                        case 2: packet.OperationId = reader.ReadInt32(); break;
                        case 3: packet.LordGlobalId = reader.ReadInt32(); break;
                        default: reader.Skip(); break;
                    }
                }

                return packet;
            }
            catch (Exception exception)
            {
                ChoreTestModPlugin.ReportDecodeFailure(receivedBody, exception);
                throw;
            }
        }
    }
}
