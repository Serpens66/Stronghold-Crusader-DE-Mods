using MessagePack;
using MessagePack.Formatters;
using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    // Map-scoped fix state that the native save does not contain. Every record is a fixed-order
    // array with a leading version; readers reject unknown versions and invalid content as a
    // whole, so a damaged or newer record restores nothing instead of a partial state.
    public static class FeatureSaveLimits
    {
        public const int MaximumPlayerId = 8;
        public const int MaximumEntries = 10000;
    }

    [MessagePackObject]
    [MessagePackFormatter(typeof(SingleBuildingPauseSaveStateFormatter))]
    public sealed class SingleBuildingPauseSaveState
    {
        public const int CurrentVersion = 1;

        [Key(0)] public int Version = CurrentVersion;
        [Key(1)] public SingleBuildingPauseSaveEntry[] Entries = Array.Empty<SingleBuildingPauseSaveEntry>();
    }

    public struct SingleBuildingPauseSaveEntry
    {
        public int BuildingId;
        public int GlobalId;
        public int Owner;
        public int BuildingType;
        public bool IsSleeping;
    }

    internal static class FeatureSaveCodec
    {
        internal static void RequireHeader(ref MessagePackReader reader, int fieldCount, int currentVersion, string label)
        {
            int count = reader.ReadArrayHeader();
            if (count != fieldCount)
                throw new MessagePackSerializationException($"{label} has {count} fields; expected {fieldCount}.");
            int version = reader.ReadInt32();
            if (version != currentVersion)
                throw new MessagePackSerializationException($"{label} version {version} is not supported.");
        }

        internal static int ReadLength(ref MessagePackReader reader, int maximum, string label)
        {
            int length = reader.ReadArrayHeader();
            if (length < 0 || length > maximum)
                throw new MessagePackSerializationException($"{label} length {length} exceeds {maximum}.");
            return length;
        }

        internal static void Require(bool valid, string message)
        {
            if (!valid)
                throw new MessagePackSerializationException(message);
        }
    }

    public sealed class SingleBuildingPauseSaveStateFormatter : IMessagePackFormatter<SingleBuildingPauseSaveState>
    {
        private const int FieldCount = 2;
        private const int EntryFieldCount = 5;

        public void Serialize(ref MessagePackWriter writer, SingleBuildingPauseSaveState value, MessagePackSerializerOptions options)
        {
            Validate(value);
            writer.WriteArrayHeader(FieldCount);
            writer.Write(value.Version);
            writer.WriteArrayHeader(value.Entries.Length);
            foreach (SingleBuildingPauseSaveEntry entry in value.Entries)
            {
                writer.WriteArrayHeader(EntryFieldCount);
                writer.Write(entry.BuildingId);
                writer.Write(entry.GlobalId);
                writer.Write(entry.Owner);
                writer.Write(entry.BuildingType);
                writer.Write(entry.IsSleeping);
            }
        }

        public SingleBuildingPauseSaveState Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            FeatureSaveCodec.RequireHeader(ref reader, FieldCount, SingleBuildingPauseSaveState.CurrentVersion, "Single-building pause record");
            int length = FeatureSaveCodec.ReadLength(ref reader, FeatureSaveLimits.MaximumEntries, "Single-building pause entry array");
            var entries = new SingleBuildingPauseSaveEntry[length];
            for (int index = 0; index < length; index++)
            {
                FeatureSaveCodec.Require(reader.ReadArrayHeader() == EntryFieldCount, "Single-building pause entry has an invalid field count.");
                entries[index] = new SingleBuildingPauseSaveEntry
                {
                    BuildingId = reader.ReadInt32(),
                    GlobalId = reader.ReadInt32(),
                    Owner = reader.ReadInt32(),
                    BuildingType = reader.ReadInt32(),
                    IsSleeping = reader.ReadBoolean()
                };
            }

            var value = new SingleBuildingPauseSaveState { Entries = entries };
            Validate(value);
            return value;
        }

        internal static void Validate(SingleBuildingPauseSaveState value)
        {
            FeatureSaveCodec.Require(value != null && value.Version == SingleBuildingPauseSaveState.CurrentVersion &&
                value.Entries != null && value.Entries.Length <= FeatureSaveLimits.MaximumEntries,
                "Single-building pause record header is invalid.");
            var buildingIds = new HashSet<int>();
            foreach (SingleBuildingPauseSaveEntry entry in value.Entries)
            {
                FeatureSaveCodec.Require(entry.BuildingId > 0 && entry.GlobalId > 0 &&
                    entry.Owner > 0 && entry.Owner <= FeatureSaveLimits.MaximumPlayerId &&
                    entry.BuildingType >= 0 && buildingIds.Add(entry.BuildingId),
                    "Single-building pause record contains an invalid or duplicate building.");
            }
        }
    }

}
