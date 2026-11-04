using System;
using MessagePack;
using MessagePack.Formatters;

namespace AutoSendTest
{
    internal enum AutoSendEdit { Hello, Reserve, Minimum, Maximum, Enabled, Paused, Ready }

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
    }

    // Explicit framing rejects incomplete/oversized packets instead of assigning permissive defaults.
    public sealed class AutoSendPacketFormatter : IMessagePackFormatter<AutoSendPacket>
    {
        public void Serialize(ref MessagePackWriter writer, AutoSendPacket value, MessagePackSerializerOptions options)
        {
            if (value == null) { writer.WriteNil(); return; }
            writer.WriteArrayHeader(8); writer.Write(value.Protocol); writer.Write(value.Epoch);
            writer.Write(value.Revision); writer.Write(value.Operation); writer.Write(value.Recipient);
            writer.Write(value.Good); writer.Write(value.Amount); writer.Write(value.Enabled);
        }
        public AutoSendPacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;
            if (reader.ReadArrayHeader() != 8) throw new InvalidOperationException("Unsupported AutoSend packet shape.");
            return new AutoSendPacket { Protocol = reader.ReadInt32(), Epoch = reader.ReadInt64(),
                Revision = reader.ReadInt64(), Operation = reader.ReadInt32(), Recipient = reader.ReadInt32(),
                Good = reader.ReadInt32(), Amount = reader.ReadInt32(), Enabled = reader.ReadBoolean() };
        }
    }
}
