using MessagePack;
using MessagePack.Formatters;

namespace ExtraFeatures
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(KnightTransformationPacketFormatter))]
    public sealed class KnightTransformationPacket
    {
        [Key(0)] public int ProtocolVersion;
        [Key(1)] public int PlayerId;
        [Key(2)] public int OperationId;
        [Key(3)] public int Action;
        [Key(4)] public int[] UnitGlobalIds;
    }

    public sealed class KnightTransformationPacketFormatter : IMessagePackFormatter<KnightTransformationPacket>
    {
        private const int FieldCount = 5;

        public void Serialize(ref MessagePackWriter writer, KnightTransformationPacket value, MessagePackSerializerOptions options)
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
            writer.Write(value.Action);
            int[] ids = value.UnitGlobalIds;
            if (ids == null)
            {
                writer.WriteNil();
                return;
            }

            writer.WriteArrayHeader(ids.Length);
            for (int index = 0; index < ids.Length; index++)
                writer.Write(ids[index]);
        }

        public KnightTransformationPacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;

            int fieldCount = reader.ReadArrayHeader();
            var packet = new KnightTransformationPacket();
            for (int index = 0; index < fieldCount; index++)
            {
                switch (index)
                {
                    case 0: packet.ProtocolVersion = reader.ReadInt32(); break;
                    case 1: packet.PlayerId = reader.ReadInt32(); break;
                    case 2: packet.OperationId = reader.ReadInt32(); break;
                    case 3: packet.Action = reader.ReadInt32(); break;
                    case 4:
                        if (reader.TryReadNil())
                        {
                            packet.UnitGlobalIds = null;
                            break;
                        }

                        int count = reader.ReadArrayHeader();
                        packet.UnitGlobalIds = new int[count];
                        for (int idIndex = 0; idIndex < count; idIndex++)
                            packet.UnitGlobalIds[idIndex] = reader.ReadInt32();
                        break;
                    default: reader.Skip(); break;
                }
            }

            return packet;
        }
    }
}
