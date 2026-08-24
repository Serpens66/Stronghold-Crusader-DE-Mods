// Feature: Explicit synchronized payload for fair siege-ammunition restocking.
using MessagePack;
using MessagePack.Formatters;
using System;

namespace BugfixesAndQoL
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(SiegeAmmoRestockPacketFormatter))]
    internal sealed class SiegeAmmoRestockPacket
    {
        [Key(0)] public int ProtocolVersion;
        [Key(1)] public int PlayerId;
        [Key(2)] public int OperationId;
        [Key(3)] public int Modifier;
        [Key(4)] public int BaseStoneCost;
        [Key(5)] public int BaseAmmunitionAmount;
        [Key(6)] public int[] GlobalUnitIds = Array.Empty<int>();
    }

    internal sealed class SiegeAmmoRestockPacketFormatter : IMessagePackFormatter<SiegeAmmoRestockPacket>
    {
        private const int FieldCount = 7;

        public void Serialize(ref MessagePackWriter writer, SiegeAmmoRestockPacket value, MessagePackSerializerOptions options)
        {
            if (value == null) { writer.WriteNil(); return; }
            writer.WriteArrayHeader(FieldCount);
            writer.Write(value.ProtocolVersion);
            writer.Write(value.PlayerId);
            writer.Write(value.OperationId);
            writer.Write(value.Modifier);
            writer.Write(value.BaseStoneCost);
            writer.Write(value.BaseAmmunitionAmount);
            int[] ids = value.GlobalUnitIds ?? Array.Empty<int>();
            writer.WriteArrayHeader(ids.Length);
            for (int index = 0; index < ids.Length; index++) writer.Write(ids[index]);
        }

        public SiegeAmmoRestockPacket Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            if (reader.TryReadNil()) return null;
            int fields = reader.ReadArrayHeader();
            var packet = new SiegeAmmoRestockPacket();
            for (int index = 0; index < fields; index++)
            {
                switch (index)
                {
                    case 0: packet.ProtocolVersion = reader.ReadInt32(); break;
                    case 1: packet.PlayerId = reader.ReadInt32(); break;
                    case 2: packet.OperationId = reader.ReadInt32(); break;
                    case 3: packet.Modifier = reader.ReadInt32(); break;
                    case 4: packet.BaseStoneCost = reader.ReadInt32(); break;
                    case 5: packet.BaseAmmunitionAmount = reader.ReadInt32(); break;
                    case 6:
                        int count = reader.ReadArrayHeader();
                        if (count < 0 || count > SiegeAmmoRestockPolicy.MaximumTargetCount)
                            throw new MessagePackSerializationException("Siege-ammunition unit list exceeds the protocol limit.");
                        packet.GlobalUnitIds = new int[count];
                        for (int idIndex = 0; idIndex < count; idIndex++) packet.GlobalUnitIds[idIndex] = reader.ReadInt32();
                        break;
                    default: reader.Skip(); break;
                }
            }
            return packet;
        }
    }
}
