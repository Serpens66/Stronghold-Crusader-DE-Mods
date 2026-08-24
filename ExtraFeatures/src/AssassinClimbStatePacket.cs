using MessagePack;
using MessagePack.Formatters;

namespace ExtraFeatures
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(AssassinClimbStatePacketFormatter))]
    public sealed class AssassinClimbStatePacket
    {
        [Key(0)] public int ProtocolVersion;
        [Key(1)] public int PlayerId;
        [Key(2)] public int OperationId;
        [Key(3)] public bool AllowClimbing;
    }

    public sealed class AssassinClimbStatePacketFormatter : IMessagePackFormatter<AssassinClimbStatePacket>
    {
        private const int FieldCount = 4;

        public void Serialize(ref MessagePackWriter writer, AssassinClimbStatePacket value, MessagePackSerializerOptions options)
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
            writer.Write(value.AllowClimbing);
        }

        public AssassinClimbStatePacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            int fieldCount = reader.ReadArrayHeader();
            var packet = new AssassinClimbStatePacket();
            for (int index = 0; index < fieldCount; index++)
            {
                switch (index)
                {
                    case 0: packet.ProtocolVersion = reader.ReadInt32(); break;
                    case 1: packet.PlayerId = reader.ReadInt32(); break;
                    case 2: packet.OperationId = reader.ReadInt32(); break;
                    case 3: packet.AllowClimbing = reader.ReadBoolean(); break;
                    default: reader.Skip(); break;
                }
            }

            return packet;
        }
    }
}
