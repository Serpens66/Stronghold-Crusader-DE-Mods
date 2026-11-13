using System;
using System.Collections.Generic;
using MessagePack;
using MessagePack.Formatters;

namespace BugfixesAndQoL.PitchMoatLines
{
    internal enum PitchLineOperation { Pitch = 1, PlannedMoat = 2, CompletedMoat = 3 }

    [MessagePackObject]
    [MessagePackFormatter(typeof(PitchLineNetworkPacketFormatter))]
    internal sealed class PitchLineNetworkPacket
    {
        [Key(0)] public int Version { get; }
        [Key(1)] public PitchLineOperation Operation { get; }
        [Key(2)] public int Actor { get; }
        [Key(3)] public int RequestId { get; }
        [Key(4)] public IReadOnlyList<int> PackedTiles { get; }

        internal PitchLineNetworkPacket(int version, PitchLineOperation operation, int actor, int requestId, int[] packedTiles)
        {
            Version = version; Operation = operation; Actor = actor; RequestId = requestId;
            // Neither the input buffer nor an observer can mutate a queued line.
            PackedTiles = Array.AsReadOnly((int[])packedTiles.Clone());
        }

        internal LineTile GetTile(int index)
        {
            int packed = PackedTiles[index];
            return new LineTile(packed & 0xffff, (int)((uint)packed >> 16));
        }
    }

    internal sealed class PitchLineNetworkPacketFormatter : IMessagePackFormatter<PitchLineNetworkPacket>
    {
        public void Serialize(ref MessagePackWriter writer, PitchLineNetworkPacket value, MessagePackSerializerOptions options)
        {
            if (value == null) { writer.WriteNil(); return; }
            writer.WriteArrayHeader(5);
            writer.Write(value.Version); writer.Write((int)value.Operation);
            writer.Write(value.Actor); writer.Write(value.RequestId);
            writer.WriteArrayHeader(value.PackedTiles.Count);
            for (int i = 0; i < value.PackedTiles.Count; i++) writer.Write(value.PackedTiles[i]);
        }

        public PitchLineNetworkPacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;
            if (reader.ReadArrayHeader() != 5)
                throw new MessagePackSerializationException("Invalid pitch/moat batch field count.");
            int version = reader.ReadInt32();
            var operation = (PitchLineOperation)reader.ReadInt32();
            int actor = reader.ReadInt32(), request = reader.ReadInt32();
            int count = reader.ReadArrayHeader();
            if (count < 1 || count > LinePolicy.MaxTiles)
                throw new MessagePackSerializationException("Invalid pitch/moat batch tile count.");
            var tiles = new int[count];
            for (int i = 0; i < count; i++) tiles[i] = reader.ReadInt32();
            return new PitchLineNetworkPacket(version, operation, actor, request, tiles);
        }
    }
}
