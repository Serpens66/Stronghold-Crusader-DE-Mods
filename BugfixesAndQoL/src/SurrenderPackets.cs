using MessagePack;
using MessagePack.Formatters;

namespace BugfixesAndQoL
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(SurrenderRequestPacketFormatter))]
    internal sealed class SurrenderRequestPacket
    {
        [Key(0)] public int ProtocolVersion;
        [Key(1)] public int RequestId;
    }

    internal sealed class SurrenderRequestPacketFormatter : IMessagePackFormatter<SurrenderRequestPacket>
    {
        private const int FieldCount = 2;

        public void Serialize(ref MessagePackWriter writer, SurrenderRequestPacket value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(FieldCount);
            writer.Write(value.ProtocolVersion);
            writer.Write(value.RequestId);
        }

        public SurrenderRequestPacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            int fieldCount = reader.ReadArrayHeader();
            var packet = new SurrenderRequestPacket();
            for (int index = 0; index < fieldCount; index++)
            {
                switch (index)
                {
                    case 0: packet.ProtocolVersion = reader.ReadInt32(); break;
                    case 1: packet.RequestId = reader.ReadInt32(); break;
                    default: reader.Skip(); break;
                }
            }

            return packet;
        }
    }

    [MessagePackObject]
    [MessagePackFormatter(typeof(SurrenderExecutionPacketFormatter))]
    internal sealed class SurrenderExecutionPacket
    {
        [Key(0)] public int PlayerId;
    }

    internal sealed class SurrenderExecutionPacketFormatter : IMessagePackFormatter<SurrenderExecutionPacket>
    {
        public void Serialize(ref MessagePackWriter writer, SurrenderExecutionPacket value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            // A scalar keeps the Chore body to one byte for player slots 1-8.
            writer.Write(value.PlayerId);
        }

        public SurrenderExecutionPacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            return new SurrenderExecutionPacket
            {
                PlayerId = reader.ReadInt32()
            };
        }
    }

    [MessagePackObject]
    [MessagePackFormatter(typeof(EliminatedPlayerSpectatorPacketFormatter))]
    internal sealed class EliminatedPlayerSpectatorPacket
    {
        [Key(0)] public int PlayerId;
    }

    internal sealed class EliminatedPlayerSpectatorPacketFormatter : IMessagePackFormatter<EliminatedPlayerSpectatorPacket>
    {
        public void Serialize(ref MessagePackWriter writer, EliminatedPlayerSpectatorPacket value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            // Keep the Chore body to one byte for player slots 1-8. Session validity is
            // checked against the process-local APIShared lord-death latch on every peer.
            writer.Write(value.PlayerId);
        }

        public EliminatedPlayerSpectatorPacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            return new EliminatedPlayerSpectatorPacket
            {
                PlayerId = reader.ReadInt32()
            };
        }
    }
}
