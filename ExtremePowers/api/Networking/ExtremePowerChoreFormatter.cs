using MessagePack;
using MessagePack.Formatters;

namespace ExtremePowers.API
{
    public sealed class ExtremePowerChoreFormatter : IMessagePackFormatter<ExtremePowerChore>
    {
        private const int FieldCount = 7;
        public void Serialize(ref MessagePackWriter writer, ExtremePowerChore value, MessagePackSerializerOptions options)
        {
            if (value == null) { writer.WriteNil(); return; }
            if (value.Protocol != ExtremePowerChoreCodec.CurrentProtocol || (uint)value.Power > 7 || value.PlayerId < 1 || value.PlayerId > 8 || value.OperationId == 0 || !ExtremePowerTargetValidator.IsValid(value.Target)) throw new MessagePackSerializationException("Invalid Extreme Power packet.");
            writer.WriteArrayHeader(FieldCount); writer.Write(value.Protocol); writer.Write((int)value.Power); writer.Write(value.PlayerId); writer.Write((int)value.Target.Kind); writer.Write(value.Target.TileIndex); writer.Write(value.Target.UnitId); writer.Write(value.OperationId);
        }
        public ExtremePowerChore Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;
            int count = reader.ReadArrayHeader(); if (count != FieldCount) throw new MessagePackSerializationException("Extreme Power packet must contain exactly seven fields.");
            byte protocol = reader.ReadByte(); int power = reader.ReadInt32(); int player = reader.ReadInt32(); int kind = reader.ReadInt32(); int tile = reader.ReadInt32(); int unit = reader.ReadInt32(); ulong operation = reader.ReadUInt64();
            if (protocol != ExtremePowerChoreCodec.CurrentProtocol || power < 0 || power > 7 || player < 1 || player > 8 || operation == 0) throw new MessagePackSerializationException("Extreme Power packet metadata is invalid.");
            ExtremePowerTarget target = kind == 0 ? ExtremePowerTarget.None : kind == 1 ? ExtremePowerTarget.MapPoint(tile) : kind == 2 ? ExtremePowerTarget.Unit(unit) : default;
            if (!ExtremePowerTargetValidator.IsValid(target)) throw new MessagePackSerializationException("Extreme Power packet target is invalid.");
            return new ExtremePowerChore(protocol, (ExtremePowerId)power, player, target, operation);
        }
    }
}
