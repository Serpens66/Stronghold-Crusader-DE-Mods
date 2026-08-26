using MessagePack;
using MessagePack.Formatters;
using System.Collections.Generic;

namespace ExtraFeatures
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(KnightTransformationPacketFormatter))]
    public sealed class KnightTransformationPacket
    {
        public const int ChorePayloadByteLimit = 1200;
        public const int PacketIdPrefixByteCount = sizeof(short);
        public const int MaximumPacketBodyBytes = ChorePayloadByteLimit - PacketIdPrefixByteCount;

        // Every MessagePack array element consumes at least one byte, so the packet budget
        // is also an absolute allocation ceiling without imposing a gameplay selection cap.
        public const int MaximumEncodedTargetCount = MaximumPacketBodyBytes;

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

            int[] ids = value.UnitGlobalIds;
            if (ids == null || ids.Length < 1 || ids.Length > KnightTransformationPacket.MaximumEncodedTargetCount)
                throw new MessagePackSerializationException("Knight transformation target count is outside the protocol limit.");

            writer.WriteArrayHeader(FieldCount);
            writer.Write(value.ProtocolVersion);
            writer.Write(value.PlayerId);
            writer.Write(value.OperationId);
            writer.Write(value.Action);
            writer.WriteArrayHeader(ids.Length);
            for (int index = 0; index < ids.Length; index++)
                writer.Write(ids[index]);
        }

        public KnightTransformationPacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            long packetBytes = reader.Sequence.Length - reader.Consumed;
            if (packetBytes > KnightTransformationPacket.MaximumPacketBodyBytes)
                throw new MessagePackSerializationException("Knight transformation packet exceeds the Chore payload limit.");

            if (reader.TryReadNil())
                return null;

            int fieldCount = reader.ReadArrayHeader();
            if (fieldCount < FieldCount)
                throw new MessagePackSerializationException($"Knight transformation packet has {fieldCount} fields; expected at least {FieldCount}.");

            var packet = new KnightTransformationPacket
            {
                ProtocolVersion = reader.ReadInt32(),
                PlayerId = reader.ReadInt32(),
                OperationId = reader.ReadInt32(),
                Action = reader.ReadInt32()
            };

            int count = reader.ReadArrayHeader();
            if (count < 1 || count > KnightTransformationPacket.MaximumEncodedTargetCount)
                throw new MessagePackSerializationException("Knight transformation target count is outside the protocol limit.");

            packet.UnitGlobalIds = new int[count];
            for (int idIndex = 0; idIndex < count; idIndex++)
                packet.UnitGlobalIds[idIndex] = reader.ReadInt32();

            // Additive protocol revisions remain readable by older peers.
            for (int index = FieldCount; index < fieldCount; index++)
                reader.Skip();

            return packet;
        }
    }

    internal static class KnightTransformationPacketValidation
    {
        public static bool HasValidMetadataAndTargets(KnightTransformationPacket packet, int maximumPlayers)
        {
            if (packet == null || maximumPlayers < 1 || packet.PlayerId < 1 || packet.PlayerId > maximumPlayers ||
                packet.OperationId <= 0 || packet.UnitGlobalIds == null || packet.UnitGlobalIds.Length < 1 ||
                packet.UnitGlobalIds.Length > KnightTransformationPacket.MaximumEncodedTargetCount)
                return false;

            var seen = new HashSet<int>();
            for (int index = 0; index < packet.UnitGlobalIds.Length; index++)
            {
                int globalId = packet.UnitGlobalIds[index];
                if (globalId <= 0 || !seen.Add(globalId))
                    return false;
            }

            return true;
        }

        public static bool DoesSerializedBodyFitChore(int bodyLength)
        {
            return bodyLength > 0 && bodyLength <= KnightTransformationPacket.MaximumPacketBodyBytes;
        }
    }
}
