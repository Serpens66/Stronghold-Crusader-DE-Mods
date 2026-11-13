using System;
using MessagePack;
using MessagePack.Formatters;

namespace AutoSendTest
{
    [MessagePackObject, MessagePackFormatter(typeof(AutoSendPacketFormatter))]
    public sealed class AutoSendPacket
    {
        [Key(0)] public int Protocol;
        [Key(1)] public long Epoch;
        [Key(2)] public long Revision;
        [Key(3)] public int Operation;
        [Key(4)] public int Recipient;
        [Key(5)] public int Good;
        [Key(6)] public int Amount;
        [Key(7)] public bool Enabled;
        [Key(8)] public int Minimum;
        [Key(9)] public int Maximum;
    }

    // Explicit framing rejects incomplete/oversized packets instead of assigning permissive defaults.
    public sealed class AutoSendPacketFormatter : IMessagePackFormatter<AutoSendPacket>
    {
        public void Serialize(ref MessagePackWriter writer, AutoSendPacket value, MessagePackSerializerOptions options)
        {
            if (value == null) { writer.WriteNil(); return; }
            writer.WriteArrayHeader(10); writer.Write(value.Protocol); writer.Write(value.Epoch);
            writer.Write(value.Revision); writer.Write(value.Operation); writer.Write(value.Recipient);
            writer.Write(value.Good); writer.Write(value.Amount); writer.Write(value.Enabled);
            writer.Write(value.Minimum); writer.Write(value.Maximum);
        }
        public AutoSendPacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;
            if (reader.ReadArrayHeader() != 10) throw new InvalidOperationException("Unsupported AutoSend packet shape.");
            return new AutoSendPacket { Protocol = reader.ReadInt32(), Epoch = reader.ReadInt64(),
                Revision = reader.ReadInt64(), Operation = reader.ReadInt32(), Recipient = reader.ReadInt32(),
                Good = reader.ReadInt32(), Amount = reader.ReadInt32(), Enabled = reader.ReadBoolean(),
                Minimum = reader.ReadInt32(), Maximum = reader.ReadInt32() };
        }
    }
}
