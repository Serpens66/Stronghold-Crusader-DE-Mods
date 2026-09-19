using MessagePack;
using MessagePack.Formatters;

namespace FormationTest
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(FormationOrderPacketFormatter))]
    public sealed class FormationOrderPacket
    {
        [Key(0)] public int ProtocolVersion;
        [Key(1)] public int OperationId;
        [Key(2)] public int TribeId;
        [Key(3)] public int TargetX;
        [Key(4)] public int TargetY;
        [Key(5)] public int IsNewOrder;
        [Key(6)] public int MoveType;
        [Key(7)] public byte Formation;
        [Key(8)] public byte Density;
        [Key(9)] public byte PlacementMode;
        [Key(10)] public byte DirectionSector;
        [Key(11)] public ushort Width;
        [Key(12)] public ushort UnitCount;
        [Key(13)] public ulong PlanHash;
    }

    public sealed class FormationOrderPacketFormatter : IMessagePackFormatter<FormationOrderPacket>
    {
        private const int FieldCount = 14;

        public void Serialize(
            ref MessagePackWriter writer,
            FormationOrderPacket value,
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
            writer.Write(value.TribeId);
            writer.Write(value.TargetX);
            writer.Write(value.TargetY);
            writer.Write(value.IsNewOrder);
            writer.Write(value.MoveType);
            writer.Write(value.Formation);
            writer.Write(value.Density);
            writer.Write(value.PlacementMode);
            writer.Write(value.DirectionSector);
            writer.Write(value.Width);
            writer.Write(value.UnitCount);
            writer.Write(value.PlanHash);
        }

        public FormationOrderPacket Deserialize(
            ref MessagePackReader reader,
            MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            int fieldCount = reader.ReadArrayHeader();
            var packet = new FormationOrderPacket();
            for (int index = 0; index < fieldCount; index++)
            {
                switch (index)
                {
                    case 0: packet.ProtocolVersion = reader.ReadInt32(); break;
                    case 1: packet.OperationId = reader.ReadInt32(); break;
                    case 2: packet.TribeId = reader.ReadInt32(); break;
                    case 3: packet.TargetX = reader.ReadInt32(); break;
                    case 4: packet.TargetY = reader.ReadInt32(); break;
                    case 5: packet.IsNewOrder = reader.ReadInt32(); break;
                    case 6: packet.MoveType = reader.ReadInt32(); break;
                    case 7: packet.Formation = reader.ReadByte(); break;
                    case 8: packet.Density = reader.ReadByte(); break;
                    case 9: packet.PlacementMode = reader.ReadByte(); break;
                    case 10: packet.DirectionSector = reader.ReadByte(); break;
                    case 11: packet.Width = reader.ReadUInt16(); break;
                    case 12: packet.UnitCount = reader.ReadUInt16(); break;
                    case 13: packet.PlanHash = reader.ReadUInt64(); break;
                    default: reader.Skip(); break;
                }
            }
            return packet;
        }
    }
}
