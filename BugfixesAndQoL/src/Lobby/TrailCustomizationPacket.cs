using MessagePack;
using MessagePack.Formatters;

namespace BugfixesAndQoL
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(TrailCustomizationPacketFormatter))]
    public sealed class TrailCustomizationPacket
    {
        public const int CurrentProtocolVersion = 1;

        [Key(0)] public int ProtocolVersion { get; set; }
        [Key(1)] public int TrailId { get; set; }
        [Key(2)] public int MissionId { get; set; }
        [Key(3)] public bool Launch { get; set; }
    }

    public sealed class TrailCustomizationPacketFormatter : IMessagePackFormatter<TrailCustomizationPacket>
    {
        public void Serialize(ref MessagePackWriter writer, TrailCustomizationPacket value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }
            writer.WriteArrayHeader(4);
            writer.Write(value.ProtocolVersion);
            writer.Write(value.TrailId);
            writer.Write(value.MissionId);
            writer.Write(value.Launch);
        }

        public TrailCustomizationPacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;
            int count = reader.ReadArrayHeader();
            var value = new TrailCustomizationPacket();
            for (int index = 0; index < count; index++)
            {
                switch (index)
                {
                    case 0: value.ProtocolVersion = reader.ReadInt32(); break;
                    case 1: value.TrailId = reader.ReadInt32(); break;
                    case 2: value.MissionId = reader.ReadInt32(); break;
                    case 3: value.Launch = reader.ReadBoolean(); break;
                    default: reader.Skip(); break;
                }
            }
            return value;
        }
    }
}
