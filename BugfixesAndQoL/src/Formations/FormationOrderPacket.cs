using MessagePack;
using MessagePack.Formatters;

namespace BugfixesAndQoL.UnitCommands
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(FormationOrderPacketFormatter))]
    internal sealed class FormationOrderPacket
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
        [Key(12)] public ushort UnitCount;
        // Keys 11 and 13 are reserved: dimensions and preview hashes are not command intent.
        [Key(14)] public ushort RequestedRows;
        [Key(15)] public ulong SelectionHash;
    }

    internal sealed class FormationOrderPacketFormatter : IMessagePackFormatter<FormationOrderPacket>
    {
        private const int FieldCount = 16;

        // MessagePack's attribute resolver instantiates this formatter by reflection.
        public FormationOrderPacketFormatter() { }

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
            writer.WriteNil();
            writer.Write(value.UnitCount);
            writer.WriteNil();
            writer.Write(value.RequestedRows);
            writer.Write(value.SelectionHash);
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
                    case 11: reader.Skip(); break;
                    case 12: packet.UnitCount = reader.ReadUInt16(); break;
                    case 13: reader.Skip(); break;
                    case 14: packet.RequestedRows = reader.ReadUInt16(); break;
                    case 15: packet.SelectionHash = reader.ReadUInt64(); break;
                    default: reader.Skip(); break;
                }
            }
            return packet;
        }
    }
}
