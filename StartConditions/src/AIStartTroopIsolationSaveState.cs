using MessagePack;
using MessagePack.Formatters;
using System;
using System.Collections.Generic;

namespace StartConditions
{
    internal readonly struct AIStartTroopIsolationSaveRecord
    {
        internal AIStartTroopIsolationSaveRecord(
            uint unitGlobalId,
            int ownerPlayerId,
            uint privateTribeGlobalId)
        {
            UnitGlobalId = unitGlobalId;
            OwnerPlayerId = ownerPlayerId;
            PrivateTribeGlobalId = privateTribeGlobalId;
        }

        internal uint UnitGlobalId { get; }
        internal int OwnerPlayerId { get; }
        internal uint PrivateTribeGlobalId { get; }
    }

    [MessagePackObject]
    [MessagePackFormatter(typeof(AIStartTroopIsolationSaveStateFormatter))]
    internal sealed class AIStartTroopIsolationSaveState
    {
        internal const int SchemaVersionCurrent = 1;
        internal const int MaximumRecords = 10000;
        internal const int MaximumPayloadBytes = 262144;

        [IgnoreMember]
        internal int SchemaVersion;

        [IgnoreMember]
        internal AIStartTroopIsolationSaveRecord[] Records;

        internal static byte[] Encode(AIStartTroopIsolationSaveRecord[] records)
        {
            byte[] bytes = MessagePackSerializer.Serialize(new AIStartTroopIsolationSaveState
            {
                SchemaVersion = SchemaVersionCurrent,
                Records = records,
            });
            if (bytes.Length > MaximumPayloadBytes)
                throw new InvalidOperationException("AI start-troop payload exceeds its maximum length.");
            return bytes;
        }

        internal static AIStartTroopIsolationSaveState Decode(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > MaximumPayloadBytes)
                throw new InvalidOperationException("AI start-troop payload has an invalid length.");

            var reader = new MessagePackReader(bytes);
            AIStartTroopIsolationSaveState state = new AIStartTroopIsolationSaveStateFormatter()
                .Deserialize(ref reader, MessagePackSerializerOptions.Standard);
            if (!reader.End)
                throw new InvalidOperationException("Trailing AI start-troop save data.");
            return state;
        }
    }

    internal sealed class AIStartTroopIsolationSaveStateFormatter :
        IMessagePackFormatter<AIStartTroopIsolationSaveState>
    {
        public void Serialize(
            ref MessagePackWriter writer,
            AIStartTroopIsolationSaveState value,
            MessagePackSerializerOptions options)
        {
            AIStartTroopIsolationSaveRecord[] records = value?.Records;
            if (value == null ||
                value.SchemaVersion != AIStartTroopIsolationSaveState.SchemaVersionCurrent ||
                records == null ||
                records.Length > AIStartTroopIsolationSaveState.MaximumRecords)
            {
                throw new MessagePackSerializationException("Invalid AI start-troop save state.");
            }

            var unitGlobalIds = new HashSet<uint>();
            var tribeGlobalIds = new HashSet<uint>();
            writer.WriteArrayHeader(1 + records.Length * 3);
            writer.Write(value.SchemaVersion);
            foreach (AIStartTroopIsolationSaveRecord record in records)
            {
                Validate(record, unitGlobalIds, tribeGlobalIds);
                writer.Write(record.UnitGlobalId);
                writer.Write(record.OwnerPlayerId);
                writer.Write(record.PrivateTribeGlobalId);
            }
        }

        public AIStartTroopIsolationSaveState Deserialize(
            ref MessagePackReader reader,
            MessagePackSerializerOptions options)
        {
            int count = reader.ReadArrayHeader();
            if (count < 1 ||
                count > 1 + AIStartTroopIsolationSaveState.MaximumRecords * 3 ||
                (count - 1) % 3 != 0)
            {
                throw new MessagePackSerializationException("Invalid AI start-troop save-data field count.");
            }

            int schemaVersion = reader.ReadInt32();
            if (schemaVersion != AIStartTroopIsolationSaveState.SchemaVersionCurrent)
                throw new MessagePackSerializationException("Unsupported AI start-troop save-data schema.");

            var records = new AIStartTroopIsolationSaveRecord[(count - 1) / 3];
            var unitGlobalIds = new HashSet<uint>();
            var tribeGlobalIds = new HashSet<uint>();
            for (int index = 0; index < records.Length; index++)
            {
                var record = new AIStartTroopIsolationSaveRecord(
                    reader.ReadUInt32(),
                    reader.ReadInt32(),
                    reader.ReadUInt32());
                Validate(record, unitGlobalIds, tribeGlobalIds);
                records[index] = record;
            }

            return new AIStartTroopIsolationSaveState
            {
                SchemaVersion = schemaVersion,
                Records = records,
            };
        }

        private static void Validate(
            AIStartTroopIsolationSaveRecord record,
            HashSet<uint> unitGlobalIds,
            HashSet<uint> tribeGlobalIds)
        {
            bool duplicateTribeGlobalId = record.PrivateTribeGlobalId != 0 &&
                !tribeGlobalIds.Add(record.PrivateTribeGlobalId);
            if (record.UnitGlobalId == 0 ||
                record.UnitGlobalId > int.MaxValue ||
                record.OwnerPlayerId < 1 ||
                record.OwnerPlayerId > 8 ||
                record.PrivateTribeGlobalId > int.MaxValue ||
                !unitGlobalIds.Add(record.UnitGlobalId) ||
                duplicateTribeGlobalId)
            {
                throw new MessagePackSerializationException("Invalid or duplicate AI start-troop identity.");
            }
        }
    }
}
