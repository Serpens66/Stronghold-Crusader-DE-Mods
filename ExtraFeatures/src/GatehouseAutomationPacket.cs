// Feature: Tick-synchronized per-gate automatic-control changes.
using MessagePack;
using MessagePack.Formatters;

namespace ExtraFeatures
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(GatehouseAutomationPacketFormatter))]
    public sealed class GatehouseAutomationPacket
    {
        [Key(0)] public int ProtocolVersion;
        [Key(1)] public int PlayerId;
        [Key(2)] public int OperationId;
        [Key(3)] public int BuildingGlobalId;
        [Key(4)] public bool AutomaticEnabled;
    }

    public sealed class GatehouseAutomationPacketFormatter : IMessagePackFormatter<GatehouseAutomationPacket>
    {
        private const int FieldCount = 5;

        public void Serialize(ref MessagePackWriter writer, GatehouseAutomationPacket value, MessagePackSerializerOptions options)
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
            writer.Write(value.BuildingGlobalId);
            writer.Write(value.AutomaticEnabled);
        }

        public GatehouseAutomationPacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            int fieldCount = reader.ReadArrayHeader();
            var packet = new GatehouseAutomationPacket();
            for (int index = 0; index < fieldCount; index++)
            {
                switch (index)
                {
                    case 0: packet.ProtocolVersion = reader.ReadInt32(); break;
                    case 1: packet.PlayerId = reader.ReadInt32(); break;
                    case 2: packet.OperationId = reader.ReadInt32(); break;
                    case 3: packet.BuildingGlobalId = reader.ReadInt32(); break;
                    case 4: packet.AutomaticEnabled = reader.ReadBoolean(); break;
                    default: reader.Skip(); break;
                }
            }

            return packet;
        }
    }
}
