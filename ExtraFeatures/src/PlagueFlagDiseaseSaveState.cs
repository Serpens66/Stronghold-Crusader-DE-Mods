// Feature: Persist exact AI flag disease projectile identities across save/load.
using MessagePack;
using MessagePack.Formatters;
using System;

namespace ExtraFeatures
{
    [MessagePackObject]
    [MessagePackFormatter(typeof(PlagueFlagDiseaseSaveStateFormatter))]
    internal sealed class PlagueFlagDiseaseSaveState
    {
        internal const int CurrentVersion = 1;

        [Key(0)] public int Version = CurrentVersion;
        [Key(1)] public PlagueFlagDiseaseSaveRecord[] Projectiles = Array.Empty<PlagueFlagDiseaseSaveRecord>();
    }

    [MessagePackObject]
    [MessagePackFormatter(typeof(PlagueFlagDiseaseSaveRecordFormatter))]
    internal sealed class PlagueFlagDiseaseSaveRecord
    {
        [Key(0)] public int SlotId;
        [Key(1)] public uint GlobalId;
    }

    internal sealed class PlagueFlagDiseaseSaveStateFormatter : IMessagePackFormatter<PlagueFlagDiseaseSaveState>
    {
        public void Serialize(ref MessagePackWriter writer, PlagueFlagDiseaseSaveState value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(2);
            writer.Write(value.Version);
            PlagueFlagDiseaseSaveRecord[] records = value.Projectiles ?? Array.Empty<PlagueFlagDiseaseSaveRecord>();
            writer.WriteArrayHeader(records.Length);
            IMessagePackFormatter<PlagueFlagDiseaseSaveRecord> formatter =
                options.Resolver.GetFormatterWithVerify<PlagueFlagDiseaseSaveRecord>();
            for (int index = 0; index < records.Length; index++)
                formatter.Serialize(ref writer, records[index], options);
        }

        public PlagueFlagDiseaseSaveState Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            int count = reader.ReadArrayHeader();
            if (count != 2)
                throw new MessagePackSerializationException("AI flag disease state has an invalid field count.");

            var value = new PlagueFlagDiseaseSaveState();
            for (int index = 0; index < count; index++)
            {
                if (index == 0)
                {
                    value.Version = reader.ReadInt32();
                    continue;
                }

                int recordCount = reader.ReadArrayHeader();
                if (recordCount < 0 || recordCount > PlagueFlagDiseaseRegistry.NativeProjectileSlotCount)
                    throw new MessagePackSerializationException("AI flag disease state has an invalid record count.");
                value.Projectiles = new PlagueFlagDiseaseSaveRecord[recordCount];
                IMessagePackFormatter<PlagueFlagDiseaseSaveRecord> formatter =
                    options.Resolver.GetFormatterWithVerify<PlagueFlagDiseaseSaveRecord>();
                for (int recordIndex = 0; recordIndex < recordCount; recordIndex++)
                    value.Projectiles[recordIndex] = formatter.Deserialize(ref reader, options);
            }
            return value;
        }
    }

    internal sealed class PlagueFlagDiseaseSaveRecordFormatter : IMessagePackFormatter<PlagueFlagDiseaseSaveRecord>
    {
        public void Serialize(ref MessagePackWriter writer, PlagueFlagDiseaseSaveRecord value, MessagePackSerializerOptions options)
        {
            writer.WriteArrayHeader(2);
            writer.Write(value.SlotId);
            writer.Write(value.GlobalId);
        }

        public PlagueFlagDiseaseSaveRecord Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            int count = reader.ReadArrayHeader();
            if (count != 2)
                throw new MessagePackSerializationException("AI flag disease record has an invalid field count.");

            var value = new PlagueFlagDiseaseSaveRecord();
            for (int index = 0; index < count; index++)
            {
                if (index == 0)
                    value.SlotId = reader.ReadInt32();
                else
                    value.GlobalId = reader.ReadUInt32();
            }
            return value;
        }
    }
}
