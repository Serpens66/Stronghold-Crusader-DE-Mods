using MessagePack;
using MessagePack.Formatters;

namespace CastlePlanner
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(CastleSpawnSyncRequestPacketFormatter))]
    public sealed class CastleSpawnSyncRequestPacket
    {
        [Key(0)] public int ProtocolVersion;
        [Key(1)] public ulong LobbyId;
    }

    public sealed class CastleSpawnSyncRequestPacketFormatter :
        IMessagePackFormatter<CastleSpawnSyncRequestPacket>
    {
        private const int FieldCount = 2;

        public void Serialize(
            ref MessagePackWriter writer,
            CastleSpawnSyncRequestPacket value,
            MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(FieldCount);
            writer.Write(value.ProtocolVersion);
            writer.Write(value.LobbyId);
        }

        public CastleSpawnSyncRequestPacket Deserialize(
            ref MessagePackReader reader,
            MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            int fieldCount = reader.ReadArrayHeader();
            var packet = new CastleSpawnSyncRequestPacket();
            for (int index = 0; index < fieldCount; index++)
            {
                switch (index)
                {
                    case 0: packet.ProtocolVersion = reader.ReadInt32(); break;
                    case 1: packet.LobbyId = reader.ReadUInt64(); break;
                    default: reader.Skip(); break;
                }
            }
            return packet;
        }
    }
}
