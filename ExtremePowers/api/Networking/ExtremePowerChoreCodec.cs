using System;
using System.IO;
using MessagePack;

namespace ExtremePowers.API
{
    [MessagePackObject, MessagePackFormatter(typeof(ExtremePowerChoreFormatter))]
    public sealed class ExtremePowerChore
    {
        public ExtremePowerChore(byte protocol, ExtremePowerId power, int playerId, ExtremePowerTarget target, ulong operationId)
        { Protocol = protocol; Power = power; PlayerId = playerId; Target = target; OperationId = operationId; }
        public byte Protocol { get; } public ExtremePowerId Power { get; } public int PlayerId { get; } public ExtremePowerTarget Target { get; } public ulong OperationId { get; }
    }
    // Stable explicit wire format. The transport prepends its separately allocated two-byte packet id.
    public static class ExtremePowerChoreCodec
    {
        public const byte CurrentProtocol = 1;
        public static byte[] Serialize(ExtremePowerChore value)
        {
            if (value == null || value.Protocol != CurrentProtocol || (uint)value.Power > 7 || value.PlayerId < 1 || value.PlayerId > 8 || value.OperationId == 0 || !ExtremePowerTargetValidator.IsValid(value.Target)) throw new ArgumentException("Invalid packet.", nameof(value));
            using (var stream = new MemoryStream(24)) using (var writer = new BinaryWriter(stream)) { writer.Write(value.Protocol); writer.Write((byte)value.Power); writer.Write(value.PlayerId); writer.Write((byte)value.Target.Kind); writer.Write(value.Target.TileIndex); writer.Write(value.Target.UnitId); writer.Write(value.OperationId); return stream.ToArray(); }
        }
        public static bool TryDeserialize(byte[] payload, out ExtremePowerChore value)
        {
            value = null; if (payload == null || payload.Length != 23) return false;
            try { using (var reader = new BinaryReader(new MemoryStream(payload, false))) { byte protocol = reader.ReadByte(); int power = reader.ReadByte(); int player = reader.ReadInt32(); var kind = (ExtremePowerTargetKind)reader.ReadByte(); int tile = reader.ReadInt32(); int unit = reader.ReadInt32(); ulong op = reader.ReadUInt64(); if (protocol != CurrentProtocol || power < 0 || power > 7 || player < 1 || player > 8 || op == 0) return false; var target = kind == ExtremePowerTargetKind.None ? ExtremePowerTarget.None : kind == ExtremePowerTargetKind.MapPoint ? ExtremePowerTarget.MapPoint(tile) : kind == ExtremePowerTargetKind.Unit ? ExtremePowerTarget.Unit(unit) : default; if (!ExtremePowerTargetValidator.IsValid(target)) return false; value = new ExtremePowerChore(protocol, (ExtremePowerId)power, player, target, op); return true; } }
            catch { return false; }
        }
    }
}
