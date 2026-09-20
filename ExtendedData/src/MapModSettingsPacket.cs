using MessagePack;
using MessagePack.Formatters;

namespace ExtendedData
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(MapModSettingsPacketFormatter))]
    public sealed class MapModSettingsPacket
    {
        internal const int CurrentProtocolVersion = 1;

        [Key(0)] public int ProtocolVersion;
        [Key(1)] public bool Apply;
        [Key(2)] public string MapFileName;
        [Key(3)] public uint MapCrc;
        [Key(4)] public string Json;
    }

    public sealed class MapModSettingsPacketFormatter : IMessagePackFormatter<MapModSettingsPacket>
    {
        private const int FieldCount = 5;

        public void Serialize(
            ref MessagePackWriter writer,
            MapModSettingsPacket value,
            MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(FieldCount);
            writer.Write(value.ProtocolVersion);
            writer.Write(value.Apply);
            writer.Write(value.MapFileName);
            writer.Write(value.MapCrc);
            writer.Write(value.Json);
        }

        public MapModSettingsPacket Deserialize(
            ref MessagePackReader reader,
            MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            int fieldCount = reader.ReadArrayHeader();
            var packet = new MapModSettingsPacket();
            for (int index = 0; index < fieldCount; index++)
            {
                switch (index)
                {
                    case 0: packet.ProtocolVersion = reader.ReadInt32(); break;
                    case 1: packet.Apply = reader.ReadBoolean(); break;
                    case 2: packet.MapFileName = reader.ReadString(); break;
                    case 3: packet.MapCrc = reader.ReadUInt32(); break;
                    case 4: packet.Json = reader.ReadString(); break;
                    default: reader.Skip(); break;
                }
            }
            return fieldCount < FieldCount ? null : packet;
        }
    }
}
