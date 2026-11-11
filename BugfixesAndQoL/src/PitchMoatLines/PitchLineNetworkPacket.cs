using MessagePack;
using MessagePack.Formatters;

namespace BugfixesAndQoL.PitchMoatLines
{
    internal enum PitchLineOperation { Pitch = 1 }

    [MessagePackObject]
    [MessagePackFormatter(typeof(PitchLineNetworkPacketFormatter))]
    internal sealed class PitchLineNetworkPacket
    {
        [Key(0)] public int Version { get; }
        [Key(1)] public PitchLineOperation Operation { get; }
        [Key(2)] public int X { get; }
        [Key(3)] public int Y { get; }
        [Key(4)] public int RequestId { get; }

        internal PitchLineNetworkPacket(int version, PitchLineOperation operation, int x, int y, int requestId)
        { Version = version; Operation = operation; X = x; Y = y; RequestId = requestId; }
    }

    internal sealed class PitchLineNetworkPacketFormatter : IMessagePackFormatter<PitchLineNetworkPacket>
    {
        public void Serialize(ref MessagePackWriter writer, PitchLineNetworkPacket value, MessagePackSerializerOptions options)
        {
            if (value == null) { writer.WriteNil(); return; }
            writer.WriteArrayHeader(5);
            writer.Write(value.Version); writer.Write((int)value.Operation);
            writer.Write(value.X); writer.Write(value.Y); writer.Write(value.RequestId);
        }

        public PitchLineNetworkPacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;
            // This protocol is deliberately closed: no partially defaulted requests,
            // unbounded extension arrays, foreign owner, or local slot on the wire.
            if (reader.ReadArrayHeader() != 5)
                throw new MessagePackSerializationException("Invalid pitch-line field count.");
            return new PitchLineNetworkPacket(reader.ReadInt32(), (PitchLineOperation)reader.ReadInt32(),
                reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
        }
    }
}
