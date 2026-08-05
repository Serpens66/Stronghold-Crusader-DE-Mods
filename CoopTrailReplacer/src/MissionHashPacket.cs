using MessagePack;
using MessagePack.Formatters;

namespace CoopTrailReplacer
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(MissionHashPacketFormatter))]
    public sealed class MissionHashPacket
    {
        [Key(0)] public int SchemaVersion { get; set; }
        [Key(1)] public int TrailId { get; set; }
        [Key(2)] public int MissionId { get; set; }
        [Key(3)] public string Hash { get; set; }
        [Key(4)] public bool RequestReply { get; set; }
    }

    public sealed class MissionHashPacketFormatter : IMessagePackFormatter<MissionHashPacket>
    {
        public void Serialize(ref MessagePackWriter writer, MissionHashPacket value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(5);
            writer.Write(value.SchemaVersion);
            writer.Write(value.TrailId);
            writer.Write(value.MissionId);
            writer.Write(value.Hash);
            writer.Write(value.RequestReply);
        }

        public MissionHashPacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            int count = reader.ReadArrayHeader();
            var packet = new MissionHashPacket();
            for (int index = 0; index < count; index++)
            {
                switch (index)
                {
                    case 0: packet.SchemaVersion = reader.ReadInt32(); break;
                    case 1: packet.TrailId = reader.ReadInt32(); break;
                    case 2: packet.MissionId = reader.ReadInt32(); break;
                    case 3: packet.Hash = reader.ReadString(); break;
                    case 4: packet.RequestReply = reader.ReadBoolean(); break;
                    default: reader.Skip(); break;
                }
            }
            return packet;
        }
    }
}
