using System;
using System.Buffers;
using MessagePack;
using MessagePack.Formatters;

namespace BugfixesAndQoL.AreaConstruction
{
    internal enum QuarryControlKind : byte { Hello, HelloEcho, ProofAck }
    internal enum QuarrySimulationKind : byte { ReadyProof, Build }

    [MessagePackObject, MessagePackFormatter(typeof(QuarryControlFormatter))]
    public sealed class QuarryControlPacket
    {
        [Key(0)] public int Protocol = QuarryNetwork.Protocol;
        [Key(1)] public byte Kind;
        [Key(2)] public string Fingerprint = "";
        [Key(3)] public string Roster = "";
        [Key(4)] public string Nonce = "";
        [Key(5)] public string Token = "";
        [Key(6)] public long Attempt;
        [Key(7)] public int Command;
        [Key(8)] public int Tick;
    }

    [MessagePackObject, MessagePackFormatter(typeof(QuarrySimulationFormatter))]
    public sealed class QuarrySimulationPacket
    {
        [Key(0)] public int Protocol = QuarryNetwork.Protocol;
        [Key(1)] public byte Kind;
        [Key(2)] public string Token = "";
        [Key(3)] public long Sequence;
        [Key(4)] public byte[] Body = Array.Empty<byte>();
    }

    public sealed class QuarryControlFormatter : IMessagePackFormatter<QuarryControlPacket>
    {
        public void Serialize(ref MessagePackWriter writer, QuarryControlPacket value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }
            writer.WriteArrayHeader(9);
            writer.Write(value.Protocol);
            writer.Write(value.Kind);
            writer.Write(value.Fingerprint);
            writer.Write(value.Roster);
            writer.Write(value.Nonce);
            writer.Write(value.Token);
            writer.Write(value.Attempt);
            writer.Write(value.Command);
            writer.Write(value.Tick);
        }

        public QuarryControlPacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;
            if (reader.ReadArrayHeader() != 9)
                throw new InvalidOperationException("Unsupported quarry control packet.");
            return new QuarryControlPacket
            {
                Protocol = reader.ReadInt32(),
                Kind = reader.ReadByte(),
                Fingerprint = ReadText(ref reader),
                Roster = ReadText(ref reader),
                Nonce = ReadText(ref reader),
                Token = ReadText(ref reader),
                Attempt = reader.ReadInt64(),
                Command = reader.ReadInt32(),
                Tick = reader.ReadInt32()
            };
        }

        internal static string ReadText(ref MessagePackReader reader)
        {
            var bytes = reader.ReadStringSequence();
            if (!bytes.HasValue || bytes.Value.Length > 64)
                throw new InvalidOperationException("Invalid quarry protocol text.");
            return System.Text.Encoding.ASCII.GetString(bytes.Value.ToArray());
        }
    }

    public sealed class QuarrySimulationFormatter : IMessagePackFormatter<QuarrySimulationPacket>
    {
        public void Serialize(ref MessagePackWriter writer, QuarrySimulationPacket value, MessagePackSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNil();
                return;
            }
            writer.WriteArrayHeader(5);
            writer.Write(value.Protocol);
            writer.Write(value.Kind);
            writer.Write(value.Token);
            writer.Write(value.Sequence);
            writer.Write(value.Body);
        }

        public QuarrySimulationPacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil())
                return null;
            if (reader.ReadArrayHeader() != 5)
                throw new InvalidOperationException("Unsupported quarry simulation packet.");
            var packet = new QuarrySimulationPacket
            {
                Protocol = reader.ReadInt32(),
                Kind = reader.ReadByte(),
                Token = QuarryControlFormatter.ReadText(ref reader),
                Sequence = reader.ReadInt64()
            };
            var bytes = reader.ReadBytes();
            if (!bytes.HasValue || bytes.Value.Length > QuarryNetwork.MaximumWorldVectorBytes)
                throw new InvalidOperationException("Invalid quarry protocol body.");
            packet.Body = bytes.Value.ToArray();
            return packet;
        }
    }
}
