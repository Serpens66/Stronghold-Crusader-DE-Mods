using MessagePack;
using MessagePack.Formatters;

namespace ExtraFeatures
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(QuarryPileRelocationPacketFormatter))]
    public sealed class QuarryPileRelocationPacket
    {
        [Key(0)] public int ProtocolVersion;
        [Key(1)] public int PlayerId;
        [Key(2)] public int OperationId;
        [Key(3)] public int QuarryGlobalId;
        [Key(4)] public int OldPileGlobalId;
        [Key(5)] public int TargetTileX;
        [Key(6)] public int TargetTileY;
    }

    public sealed class QuarryPileRelocationPacketFormatter : IMessagePackFormatter<QuarryPileRelocationPacket>
    {
        private const int FieldCount = 7;

        public void Serialize(
            ref MessagePackWriter writer,
            QuarryPileRelocationPacket value,
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
            writer.Write(value.QuarryGlobalId);
            writer.Write(value.OldPileGlobalId);
            writer.Write(value.TargetTileX);
            writer.Write(value.TargetTileY);
        }

        public QuarryPileRelocationPacket Deserialize(
            ref MessagePackReader reader,
            MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            int fieldCount = reader.ReadArrayHeader();
            var packet = new QuarryPileRelocationPacket();
            for (int index = 0; index < fieldCount; index++)
            {
                switch (index)
                {
                    case 0: packet.ProtocolVersion = reader.ReadInt32(); break;
                    case 1: packet.PlayerId = reader.ReadInt32(); break;
                    case 2: packet.OperationId = reader.ReadInt32(); break;
                    case 3: packet.QuarryGlobalId = reader.ReadInt32(); break;
                    case 4: packet.OldPileGlobalId = reader.ReadInt32(); break;
                    case 5: packet.TargetTileX = reader.ReadInt32(); break;
                    case 6: packet.TargetTileY = reader.ReadInt32(); break;
                    default: reader.Skip(); break;
                }
            }

            return packet;
        }
    }
}
