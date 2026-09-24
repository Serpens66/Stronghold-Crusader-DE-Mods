using MessagePack;
using MessagePack.Formatters;

namespace WaterboyTargetReservationTest
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(WaterboyModePacketFormatter))]
    public sealed class WaterboyModePacket
    {
        [Key(0)] public int ProtocolVersion;
        [Key(1)] public int PlayerId;
        [Key(2)] public int OperationId;
        [Key(3)] public int SourceBuildingId;
        [Key(4)] public uint SourceBuildingGlobalId;
        [Key(5)] public bool Enabled;
    }

    public sealed class WaterboyModePacketFormatter : IMessagePackFormatter<WaterboyModePacket>
    {
        private const int FieldCount = 6;

        public void Serialize(ref MessagePackWriter writer, WaterboyModePacket value,
            MessagePackSerializerOptions options)
        {
            if (value == null) { writer.WriteNil(); return; }
            writer.WriteArrayHeader(FieldCount);
            writer.Write(value.ProtocolVersion);
            writer.Write(value.PlayerId);
            writer.Write(value.OperationId);
            writer.Write(value.SourceBuildingId);
            writer.Write(value.SourceBuildingGlobalId);
            writer.Write(value.Enabled);
        }

        public WaterboyModePacket Deserialize(ref MessagePackReader reader,
            MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;
            int count = reader.ReadArrayHeader();
            var packet = new WaterboyModePacket();
            for (int index = 0; index < count; index++)
            {
                switch (index)
                {
                    case 0: packet.ProtocolVersion = reader.ReadInt32(); break;
                    case 1: packet.PlayerId = reader.ReadInt32(); break;
                    case 2: packet.OperationId = reader.ReadInt32(); break;
                    case 3: packet.SourceBuildingId = reader.ReadInt32(); break;
                    case 4: packet.SourceBuildingGlobalId = reader.ReadUInt32(); break;
                    case 5: packet.Enabled = reader.ReadBoolean(); break;
                    default: reader.Skip(); break;
                }
            }
            return packet;
        }
    }
}
