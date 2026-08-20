using MessagePack;
using MessagePack.Formatters;

namespace ExtraFeatures
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(SingleBuildingPausePacketFormatter))]
    public sealed class SingleBuildingPausePacket
    {
        [Key(0)] public int ProtocolVersion;
        [Key(1)] public int PlayerId;
        [Key(2)] public int OperationId;
        [Key(3)] public int BuildingGlobalId;
        [Key(4)] public bool TargetSleeping;
        [Key(5)] public int Action;
        [Key(6)] public bool SynchronizeAfterReset;
    }

    public sealed class SingleBuildingPausePacketFormatter : IMessagePackFormatter<SingleBuildingPausePacket>
    {
        private const int FieldCount = 7;

        public void Serialize(ref MessagePackWriter writer, SingleBuildingPausePacket value, MessagePackSerializerOptions options)
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
            writer.Write(value.TargetSleeping);
            writer.Write(value.Action);
            writer.Write(value.SynchronizeAfterReset);
        }

        public SingleBuildingPausePacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            int fieldCount = reader.ReadArrayHeader();
            var packet = new SingleBuildingPausePacket();
            for (int index = 0; index < fieldCount; index++)
            {
                switch (index)
                {
                    case 0: packet.ProtocolVersion = reader.ReadInt32(); break;
                    case 1: packet.PlayerId = reader.ReadInt32(); break;
                    case 2: packet.OperationId = reader.ReadInt32(); break;
                    case 3: packet.BuildingGlobalId = reader.ReadInt32(); break;
                    case 4: packet.TargetSleeping = reader.ReadBoolean(); break;
                    case 5: packet.Action = reader.ReadInt32(); break;
                    case 6: packet.SynchronizeAfterReset = reader.ReadBoolean(); break;
                    default: reader.Skip(); break;
                }
            }

            return packet;
        }
    }
}
