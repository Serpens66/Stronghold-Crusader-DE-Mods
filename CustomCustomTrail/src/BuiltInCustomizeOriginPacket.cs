using MessagePack;
using MessagePack.Formatters;

namespace CustomCustomTrail
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(BuiltInCustomizeOriginPacketFormatter))]
    public sealed class BuiltInCustomizeOriginPacket
    {
        internal const int CurrentProtocolVersion = 1;

        [Key(0)] public int ProtocolVersion;
        [Key(1)] public int TrailType;
        [Key(2)] public int MissionId;

        internal static bool IsValid(BuiltInCustomizeOriginPacket packet)
        {
            if (packet == null || packet.ProtocolVersion != CurrentProtocolVersion || packet.MissionId < 0)
                return false;
            return (packet.TrailType >= CustomCustomTrailLaunchOriginApi.FirstVanillaTrailType &&
                    packet.TrailType <= CustomCustomTrailLaunchOriginApi.LastVanillaTrailType) ||
                (packet.TrailType >= CustomCustomTrailLaunchOriginApi.FirstSandsOfTimeTrailType &&
                 packet.TrailType <= CustomCustomTrailLaunchOriginApi.LastSandsOfTimeTrailType);
        }
    }

    public sealed class BuiltInCustomizeOriginPacketFormatter : IMessagePackFormatter<BuiltInCustomizeOriginPacket>
    {
        private const int FieldCount = 3;

        public void Serialize(
            ref MessagePackWriter writer,
            BuiltInCustomizeOriginPacket value,
            MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(FieldCount);
            writer.Write(value.ProtocolVersion);
            writer.Write(value.TrailType);
            writer.Write(value.MissionId);
        }

        public BuiltInCustomizeOriginPacket Deserialize(
            ref MessagePackReader reader,
            MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            int fieldCount = reader.ReadArrayHeader();
            var packet = new BuiltInCustomizeOriginPacket();
            for (int index = 0; index < fieldCount; index++)
            {
                switch (index)
                {
                    case 0: packet.ProtocolVersion = reader.ReadInt32(); break;
                    case 1: packet.TrailType = reader.ReadInt32(); break;
                    case 2: packet.MissionId = reader.ReadInt32(); break;
                    default: reader.Skip(); break;
                }
            }
            return fieldCount < FieldCount ? null : packet;
        }
    }
}
