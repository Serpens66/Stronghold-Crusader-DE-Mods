using System;
using System.Buffers;
using System.IO;
using MessagePack;
using MessagePack.Formatters;

namespace MultiplayerRestartTest
{
    [MessagePackFormatter(typeof(RestartWireFormatter))]
    internal sealed class RestartWire
    {
        internal const int WireVersion = 2;
        internal int Control; // 0=protocol, 1=capability hello, 2=hello acknowledgement.
        internal short PacketId;
        internal RestartPacket Packet;
        internal RestartWire Copy() => new RestartWire { Control = Control, PacketId = PacketId,
            Packet = Packet == null ? null : new RestartPacket { Kind = Packet.Kind, Session = Packet.Session,
                Operation = Packet.Operation, Digest = Packet.Digest, LobbyId = Packet.LobbyId } };
    }
    internal sealed class RestartWireFormatter : IMessagePackFormatter<RestartWire>
    {
        public void Serialize(ref MessagePackWriter writer, RestartWire value, MessagePackSerializerOptions options)
        {
            Validate(value); var p = value.Packet;
            writer.WriteArrayHeader(8); writer.Write(RestartWire.WireVersion); writer.Write(value.Control); writer.Write(value.PacketId);
            writer.Write((int)p.Kind); writer.Write(p.Session); writer.Write(p.Operation); writer.Write(p.Digest); writer.Write(p.LobbyId);
        }
        public RestartWire Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            options.Security.DepthStep(ref reader);
            try
            {
                if (reader.ReadArrayHeader() != 8 || reader.ReadInt32() != RestartWire.WireVersion) throw new MessagePackSerializationException("Restart wire version/schema mismatch.");
                var value = new RestartWire { Control = reader.ReadInt32(), PacketId = reader.ReadInt16(),
                    Packet = new RestartPacket { Kind = (RestartMessageKind)reader.ReadInt32(), Session = Text(ref reader, 160),
                        Operation = Text(ref reader, 32), Digest = Text(ref reader, 64), LobbyId = reader.ReadUInt64() } };
                if (!reader.End) throw new MessagePackSerializationException("Restart wire trailing bytes.");
                Validate(value); return value;
            }
            finally { reader.Depth--; }
        }
        private static string Text(ref MessagePackReader reader, int max)
        {
            var bytes = reader.ReadStringSequence();
            if (!bytes.HasValue || bytes.Value.Length > max) throw new MessagePackSerializationException("Restart wire string exceeds limit.");
            return System.Text.Encoding.UTF8.GetString(bytes.Value.ToArray());
        }
        private static void Validate(RestartWire value)
        {
            var p = value?.Packet;
            if (p == null || value.Control < 0 || value.Control > 2 || value.PacketId <= 0 ||
                p.Session == null || p.Session.Length < 1 || p.Session.Length > 160 || p.Digest == null || p.Digest.Length != 64 ||
                p.Operation == null || p.Operation.Length > 32 || (value.Control == 0 && (!Enum.IsDefined(typeof(RestartMessageKind), p.Kind) ||
                    !Guid.TryParseExact(p.Operation, "N", out _))))
                throw new MessagePackSerializationException("Invalid restart wire payload.");
            if (value.Control == 0 && (p.Kind == RestartMessageKind.Lobby ? p.LobbyId == 0 : p.LobbyId != 0))
                throw new MessagePackSerializationException("Invalid restart lobby framing.");
            if (value.Control != 0 && (p.LobbyId != 0 || p.Operation.Length != 0 || p.Kind != RestartMessageKind.Prepare))
                throw new MessagePackSerializationException("Invalid capability hello.");
        }
    }
}
