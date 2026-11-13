using System;
using System.Collections.Generic;
using MessagePack;
using PlayerControlRelayTest;

internal static class WireProgram
{
    private static int assertions;
    private static void Check(bool value, string reason) { assertions++; if (!value) throw new Exception(reason); }
    private static void Reject(Action action)
    {
        assertions++;
        try { action(); } catch (MessagePackSerializationException) { return; }
        throw new Exception("Expected malformed wire rejection.");
    }
    private static RelayEnvelope CommandEnvelope()
    {
        var slots = new int[64];
        for (int i = 0; i < slots.Length; i++) slots[i] = i + 1;
        return new RelayEnvelope { Kind = RelayEnvelope.Request, Epoch = "epoch", Player = 2,
            Command = new RelayCommand("epoch", 2, ulong.MaxValue, long.MaxValue, RelayAction.Move, 10, 20, uint.MaxValue, slots) };
    }
    private static byte[] Encode(RelayEnvelope value) => MessagePackSerializer.Serialize(value);
    private static RelayEnvelope Decode(byte[] bytes) => MessagePackSerializer.Deserialize<RelayEnvelope>(bytes);
    // A fixed-array header alone is sufficient: the formatter must reject its count
    // before attempting to read or allocate the absent fields.
    private static byte[] ArrayOf(int count) => new[] { (byte)(0x90 | count) };
    private static void Append<T>(List<byte> bytes, T value) => bytes.AddRange(MessagePackSerializer.Serialize(value));
    private static byte[] HostileCommand(byte[] slots)
    {
        // Intentionally malformed declared-schema fixture, never a second encoder.
        var bytes = new List<byte> { 0x99 };
        Append(bytes, "epoch"); Append(bytes, 2); Append(bytes, 123UL); Append(bytes, 1L);
        Append(bytes, 1); Append(bytes, 10); Append(bytes, 20); Append(bytes, 500U);
        Append(bytes, slots);
        return bytes.ToArray();
    }
    private static byte[] HostileEnvelope(byte[] command, string epoch = "epoch")
    {
        var bytes = new List<byte> { 0x99 };
        Append(bytes, RelayEnvelope.Request); Append(bytes, epoch); Append(bytes, ""); Append(bytes, 2);
        bytes.AddRange(command); Append(bytes, 0); Append(bytes, ""); Append(bytes, 0); Append(bytes, 0);
        return bytes.ToArray();
    }
    private static void Main()
    {
        var source = CommandEnvelope(); var encoded = Encode(source); var decoded = Decode(encoded);
        Check(decoded.Command.Slots.Count == 64 && decoded.Command.Requester == ulong.MaxValue && decoded.Command.RequestId == long.MaxValue, "Command identity did not round trip.");
        Check(decoded.Command.MaxGlobalId == uint.MaxValue, "Identity limit did not round trip.");
        Check(Convert.ToBase64String(decoded.Command.ComputeDigest()) == Convert.ToBase64String(source.Command.ComputeDigest()), "Wire changed command digest.");
        var snapshot = source.Clone(); source.Epoch = "changed";
        Check(snapshot.Epoch == "epoch" && !ReferenceEquals(snapshot.Command, source.Command), "Clone retained mutable envelope/command.");
        // The Chore type is a separate packet identity with identical bytes.
        byte[] choreBytes = MessagePackSerializer.Serialize(new RelayChoreEnvelope(CommandEnvelope()));
        Check(Convert.ToBase64String(choreBytes) == Convert.ToBase64String(Encode(CommandEnvelope())), "Chore envelope changed the wire bytes.");
        Check(MessagePackSerializer.Deserialize<RelayChoreEnvelope>(choreBytes).Inner.Command.Slots.Count == 64, "Chore envelope did not round trip.");
        Reject(() => MessagePackSerializer.Serialize(new RelayChoreEnvelope(null)));
        Reject(() => MessagePackSerializer.Deserialize<RelayChoreEnvelope>(new byte[] { 0xc0 }));
        // A maximal command must fit the native Chore limit (1200 bytes including the packet ID).
        var worst = new List<int>();
        for (int slot = 130; worst.Count < RelaySlotCodec.MaximumEncodedBytes / 2 && slot <= RelaySlotCodec.MaximumSlot; slot += 130) worst.Add(slot);
        var dense = new int[RelaySlotCodec.MaximumEncodedBytes];
        for (int i = 0; i < dense.Length; i++) dense[i] = i + 1;
        string epoch32 = new string('e', 32);
        foreach (int[] slots in new[] { worst.ToArray(), dense })
        {
            var maximal = new RelayEnvelope { Kind = RelayEnvelope.Execute, Epoch = epoch32, Player = 2, Text = "relay-v4",
                MessagePacketId = short.MaxValue, ChorePacketId = short.MaxValue,
                Command = new RelayCommand(epoch32, 2, ulong.MaxValue, long.MaxValue, RelayAction.Move, 799, 799, uint.MaxValue, slots) };
            Check(Encode(maximal).Length + sizeof(short) <= 1200, "Largest command exceeds the Chore limit.");
        }
        for (int kind = RelayEnvelope.Hello; kind <= RelayEnvelope.Ready; kind++)
        {
            var handshake = new RelayEnvelope { Kind = kind, Epoch = "epoch", Nonce = "nonce", MessagePacketId = 100, ChorePacketId = 101 };
            Check(Decode(Encode(handshake)).Kind == kind, "Handshake did not round trip.");
        }
        Reject(() => Decode(ArrayOf(8))); Reject(() => Decode(ArrayOf(10))); Reject(() => Decode(new byte[] { 0xc0 }));
        var trailing = new byte[encoded.Length + 1]; Array.Copy(encoded, trailing, encoded.Length);
        Reject(() => Decode(trailing));
        for (int length = 0; length < encoded.Length; length += Math.Max(1, encoded.Length / 15))
        { var truncated = new byte[length]; Array.Copy(encoded, truncated, length); Reject(() => Decode(truncated)); }
        var bad = CommandEnvelope(); bad.Kind = RelayEnvelope.Hello; Reject(() => Encode(bad));
        bad = CommandEnvelope(); bad.Command = null; Reject(() => Encode(bad));
        bad = CommandEnvelope(); bad.Player = 3; Reject(() => Encode(bad));
        bad = CommandEnvelope(); bad.Outcome = 99; Reject(() => Encode(bad));
        bad = CommandEnvelope(); bad.Epoch = new string('x', 65); Reject(() => Encode(bad));
        bad = CommandEnvelope(); bad.Text = new string('x', 513); Reject(() => Encode(bad));
        bad = CommandEnvelope(); bad.Nonce = new string('x', 65); Reject(() => Encode(bad));
        bad = CommandEnvelope(); bad.MessagePacketId = short.MaxValue + 1; Reject(() => Encode(bad));
        Check(Decode(HostileEnvelope(HostileCommand(new byte[] { 1, 1 }))).Command.Slots.Count == 2, "Valid fixture rejected.");
        Reject(() => Decode(HostileEnvelope(HostileCommand(new byte[0]))));
        Reject(() => Decode(HostileEnvelope(HostileCommand(new byte[] { 0 }))));
        Reject(() => Decode(HostileEnvelope(HostileCommand(new byte[] { 0x80 }))));
        Reject(() => Decode(HostileEnvelope(HostileCommand(new byte[RelaySlotCodec.MaximumEncodedBytes + 1]))));
        Reject(() => Decode(HostileEnvelope(ArrayOf(7))));
        Reject(() => Decode(HostileEnvelope(new byte[] { 0xc0 }, new string('x', 65))));
        Console.WriteLine("PASS: " + assertions + " installed-MessagePack wire assertions.");
    }
}
