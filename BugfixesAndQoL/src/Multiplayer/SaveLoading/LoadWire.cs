using System;
using System.Buffers;
using MessagePack;
using MessagePack.Formatters;

namespace BugfixesAndQoL.Multiplayer.SaveLoading
{
    [MessagePackFormatter(typeof(LoadWireFormatter))]
    internal sealed class LoadWire
    {
        internal const int WireVersion = 1;
        internal const int MaximumBytes = 512;
        internal int Control; // 0=transition, 1=capability hello, 2=hello reply.
        internal short PacketId;
        internal LoadPacket Packet;
        internal LoadWire Copy() => new LoadWire { Control = Control, PacketId = PacketId,
            Packet = Packet == null ? null : new LoadPacket { Kind = Packet.Kind, Session = Packet.Session,
                Operation = Packet.Operation, LobbyId = Packet.LobbyId } };
    }
    internal sealed class LoadWireFormatter : IMessagePackFormatter<LoadWire>
    {
        public void Serialize(ref MessagePackWriter writer, LoadWire value, MessagePackSerializerOptions options)
        {
            Validate(value);
            writer.WriteArrayHeader(7); writer.Write(LoadWire.WireVersion); writer.Write(value.Control); writer.Write(value.PacketId);
            writer.Write((int)value.Packet.Kind); writer.Write(value.Packet.Session);
            writer.Write(value.Packet.Operation); writer.Write(value.Packet.LobbyId);
        }
        public LoadWire Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            options.Security.DepthStep(ref reader);
            try
            {
                if (reader.ReadArrayHeader() != 7 || reader.ReadInt32() != LoadWire.WireVersion)
                    throw new MessagePackSerializationException("Multiplayer load protocol version/schema mismatch.");
                var value = new LoadWire { Control = reader.ReadInt32(), PacketId = reader.ReadInt16(),
                    Packet = new LoadPacket { Kind = (LoadMessageKind)reader.ReadInt32(), Session = reader.ReadUInt64(),
                        Operation = Text(ref reader), LobbyId = reader.ReadUInt64() } };
                if (!reader.End) throw new MessagePackSerializationException("Multiplayer load protocol has trailing bytes.");
                Validate(value); return value;
            }
            finally { reader.Depth--; }
        }
        private static string Text(ref MessagePackReader reader)
        {
            var bytes = reader.ReadStringSequence();
            if (!bytes.HasValue || bytes.Value.Length > 32)
                throw new MessagePackSerializationException("Multiplayer load operation is missing or exceeds its limit.");
            return System.Text.Encoding.UTF8.GetString(bytes.Value.ToArray());
        }
        private static void Validate(LoadWire value)
        {
            LoadPacket packet = value?.Packet;
            if (packet == null || value.Control < 0 || value.Control > 2 || value.PacketId <= 0 || packet.Session == 0 ||
                !Enum.IsDefined(typeof(LoadMessageKind), packet.Kind) || packet.Operation == null)
                throw new MessagePackSerializationException("Invalid multiplayer load envelope.");
            if (value.Control != 0)
            {
                if (packet.Kind != LoadMessageKind.Prepare || packet.Operation.Length != 0 || packet.LobbyId != 0)
                    throw new MessagePackSerializationException("Invalid multiplayer load capability hello.");
            }
            else if (packet.Operation.Length != 32 || !Guid.TryParseExact(packet.Operation, "N", out _) ||
                (packet.Kind == LoadMessageKind.Lobby ? packet.LobbyId == 0 : packet.LobbyId != 0))
                throw new MessagePackSerializationException("Invalid multiplayer load transition payload.");
        }
    }
}
