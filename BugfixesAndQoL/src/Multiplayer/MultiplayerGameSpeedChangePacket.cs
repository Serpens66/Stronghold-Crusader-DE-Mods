using MessagePack;
using MessagePack.Formatters;

namespace BugfixesAndQoL
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(MultiplayerGameSpeedChangePacketFormatter))]
    public sealed class MultiplayerGameSpeedChangePacket
    {
        [Key(0)] public int ProtocolVersion;
        [Key(1)] public int Action;
        [Key(2)] public int TargetSpeed;
        [Key(3)] public int PauseState;
    }

    public sealed class MultiplayerGameSpeedChangePacketFormatter : IMessagePackFormatter<MultiplayerGameSpeedChangePacket>
    {
        private const int FieldCount = 4;

        public void Serialize(
            ref MessagePackWriter writer,
            MultiplayerGameSpeedChangePacket value,
            MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(FieldCount);
            writer.Write(value.ProtocolVersion);
            writer.Write(value.Action);
            writer.Write(value.TargetSpeed);
            writer.Write(value.PauseState);
        }

        public MultiplayerGameSpeedChangePacket Deserialize(
            ref MessagePackReader reader,
            MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            int fieldCount = reader.ReadArrayHeader();
            var packet = new MultiplayerGameSpeedChangePacket();
            for (int index = 0; index < fieldCount; index++)
            {
                switch (index)
                {
                    case 0: packet.ProtocolVersion = reader.ReadInt32(); break;
                    case 1: packet.Action = reader.ReadInt32(); break;
                    case 2: packet.TargetSpeed = reader.ReadInt32(); break;
                    case 3: packet.PauseState = reader.ReadInt32(); break;
                    default: reader.Skip(); break;
                }
            }

            return packet;
        }
    }
}
