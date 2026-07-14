using MessagePack;
using MessagePack.Formatters;

namespace MPTest
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(WoodcutterSwordsmanSpawnPacketFormatter))]
    public sealed class WoodcutterSwordsmanSpawnPacket
    {
        [Key(0)] public int SourcePlayerId;
        [Key(1)] public int RequestId;
        [Key(2)] public int WoodcutterGlobalId;
        [Key(3)] public int TargetTileX;
        [Key(4)] public int TargetTileY;
    }

    public sealed class WoodcutterSwordsmanSpawnPacketFormatter : IMessagePackFormatter<WoodcutterSwordsmanSpawnPacket>
    {
        public void Serialize(
            ref MessagePackWriter writer,
            WoodcutterSwordsmanSpawnPacket value,
            MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(5);
            writer.Write(value.SourcePlayerId);
            writer.Write(value.RequestId);
            writer.Write(value.WoodcutterGlobalId);
            writer.Write(value.TargetTileX);
            writer.Write(value.TargetTileY);
        }

        public WoodcutterSwordsmanSpawnPacket Deserialize(
            ref MessagePackReader reader,
            MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            int fieldCount = reader.ReadArrayHeader();
            WoodcutterSwordsmanSpawnPacket packet = new WoodcutterSwordsmanSpawnPacket();
            for (int index = 0; index < fieldCount; index++)
            {
                switch (index)
                {
                    case 0: packet.SourcePlayerId = reader.ReadInt32(); break;
                    case 1: packet.RequestId = reader.ReadInt32(); break;
                    case 2: packet.WoodcutterGlobalId = reader.ReadInt32(); break;
                    case 3: packet.TargetTileX = reader.ReadInt32(); break;
                    case 4: packet.TargetTileY = reader.ReadInt32(); break;
                    default: reader.Skip(); break;
                }
            }

            return packet;
        }
    }
}
