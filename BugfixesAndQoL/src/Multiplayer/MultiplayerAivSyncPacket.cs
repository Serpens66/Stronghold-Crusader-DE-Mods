using MessagePack;
using MessagePack.Formatters;

namespace BugfixesAndQoL
{
    internal enum MultiplayerAivSyncPacketKind
    {
        Begin = 1,
        Chunk = 2,
        Ack = 3,
        Reject = 4
    }

    [MessagePackObject]
    [MessagePackFormatter(typeof(MultiplayerAivSyncPacketFormatter))]
    internal sealed class MultiplayerAivSyncPacket
    {
        [Key(0)] public int ProtocolVersion;
        [Key(1)] public int Kind;
        [Key(2)] public ulong LobbyId;
        [Key(3)] public int Generation;
        [Key(4)] public string VanillaChecksum;
        [Key(5)] public string ManifestHash;
        [Key(6)] public int UncompressedLength;
        [Key(7)] public int CompressedLength;
        [Key(8)] public int ChunkIndex;
        [Key(9)] public int ChunkCount;
        [Key(10)] public string DataBase64;
        [Key(11)] public string Message;
    }

    internal sealed class MultiplayerAivSyncPacketFormatter : IMessagePackFormatter<MultiplayerAivSyncPacket>
    {
        private const int FieldCount = 12;

        public void Serialize(ref MessagePackWriter writer, MultiplayerAivSyncPacket value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }
            writer.WriteArrayHeader(FieldCount);
            writer.Write(value.ProtocolVersion);
            writer.Write(value.Kind);
            writer.Write(value.LobbyId);
            writer.Write(value.Generation);
            writer.Write(value.VanillaChecksum);
            writer.Write(value.ManifestHash);
            writer.Write(value.UncompressedLength);
            writer.Write(value.CompressedLength);
            writer.Write(value.ChunkIndex);
            writer.Write(value.ChunkCount);
            writer.Write(value.DataBase64);
            writer.Write(value.Message);
        }

        public MultiplayerAivSyncPacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;
            int count = reader.ReadArrayHeader();
            if (count > FieldCount)
                throw new MessagePackSerializationException("AIV sync packet has too many fields.");
            var value = new MultiplayerAivSyncPacket();
            for (int index = 0; index < count; index++)
            {
                switch (index)
                {
                    case 0: value.ProtocolVersion = reader.ReadInt32(); break;
                    case 1: value.Kind = reader.ReadInt32(); break;
                    case 2: value.LobbyId = reader.ReadUInt64(); break;
                    case 3: value.Generation = reader.ReadInt32(); break;
                    case 4: value.VanillaChecksum = reader.ReadString(); break;
                    case 5: value.ManifestHash = reader.ReadString(); break;
                    case 6: value.UncompressedLength = reader.ReadInt32(); break;
                    case 7: value.CompressedLength = reader.ReadInt32(); break;
                    case 8: value.ChunkIndex = reader.ReadInt32(); break;
                    case 9: value.ChunkCount = reader.ReadInt32(); break;
                    case 10: value.DataBase64 = reader.ReadString(); break;
                    case 11: value.Message = reader.ReadString(); break;
                    default: reader.Skip(); break;
                }
            }
            return value;
        }
    }
}
