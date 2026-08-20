using MessagePack;
using MessagePack.Formatters;

namespace CustomCustomTrail
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(CoopCustomizePacketFormatter))]
    public sealed class CoopCustomizePacket
    {
        [Key(0)] public int ProtocolVersion;
        [Key(1)] public int TrailId;
        [Key(2)] public int MissionId;
        [Key(3)] public bool Launch;
    }

    public sealed class CoopCustomizePacketFormatter : IMessagePackFormatter<CoopCustomizePacket>
    {
        private const int FieldCount = 4;

        public void Serialize(
            ref MessagePackWriter writer,
            CoopCustomizePacket value,
            MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(FieldCount);
            writer.Write(value.ProtocolVersion);
            writer.Write(value.TrailId);
            writer.Write(value.MissionId);
            writer.Write(value.Launch);
        }

        public CoopCustomizePacket Deserialize(
            ref MessagePackReader reader,
            MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            int fieldCount = reader.ReadArrayHeader();
            var packet = new CoopCustomizePacket();
            for (int index = 0; index < fieldCount; index++)
            {
                switch (index)
                {
                    case 0: packet.ProtocolVersion = reader.ReadInt32(); break;
                    case 1: packet.TrailId = reader.ReadInt32(); break;
                    case 2: packet.MissionId = reader.ReadInt32(); break;
                    case 3: packet.Launch = reader.ReadBoolean(); break;
                    default: reader.Skip(); break;
                }
            }
            return packet;
        }
    }
}
