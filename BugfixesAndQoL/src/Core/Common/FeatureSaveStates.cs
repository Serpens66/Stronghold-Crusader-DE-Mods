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
        // Matches SynchronizedMovementCadencePatch.MaximumTrackedTribeId, the size of the
        // native cadence table that a restored synchronization must fit into.
        public const int MaximumTribeId = 4500;
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

    [MessagePackObject]
    [MessagePackFormatter(typeof(AssassinClimbSaveStateFormatter))]
    public sealed class AssassinClimbSaveState
    {
        public const int CurrentVersion = 1;

        [Key(0)] public int Version = CurrentVersion;
        // Only players deviating from the default "climbing allowed" are stored.
        [Key(1)] public int[] ForbiddenPlayerIds = Array.Empty<int>();
    }

    [MessagePackObject]
    [MessagePackFormatter(typeof(TroopMovementSyncSaveStateFormatter))]
    public sealed class TroopMovementSyncSaveState
    {
        public const int CurrentVersion = 1;

        [Key(0)] public int Version = CurrentVersion;
        [Key(1)] public TroopMovementSyncSaveEntry[] Entries = Array.Empty<TroopMovementSyncSaveEntry>();
    }

    public struct TroopMovementSyncSaveEntry
    {
        public int TribeId;
        public uint TribeGlobalId;
        public bool Running;
        public ushort RunningSpeedBonus;
        public ushort PreviousMovementSpeed;
        public ushort AppliedMovementSpeed;
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

    public sealed class AssassinClimbSaveStateFormatter : IMessagePackFormatter<AssassinClimbSaveState>
    {
        private const int FieldCount = 2;

        public void Serialize(ref MessagePackWriter writer, AssassinClimbSaveState value, MessagePackSerializerOptions options)
        {
            Validate(value);
            writer.WriteArrayHeader(FieldCount);
            writer.Write(value.Version);
            writer.WriteArrayHeader(value.ForbiddenPlayerIds.Length);
            foreach (int playerId in value.ForbiddenPlayerIds)
                writer.Write(playerId);
        }

        public AssassinClimbSaveState Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            FeatureSaveCodec.RequireHeader(ref reader, FieldCount, AssassinClimbSaveState.CurrentVersion, "Assassin climb record");
            int length = FeatureSaveCodec.ReadLength(ref reader, FeatureSaveLimits.MaximumPlayerId, "Assassin climb player array");
            var playerIds = new int[length];
            for (int index = 0; index < length; index++)
                playerIds[index] = reader.ReadInt32();

            var value = new AssassinClimbSaveState { ForbiddenPlayerIds = playerIds };
            Validate(value);
            return value;
        }

        internal static void Validate(AssassinClimbSaveState value)
        {
            FeatureSaveCodec.Require(value != null && value.Version == AssassinClimbSaveState.CurrentVersion &&
                value.ForbiddenPlayerIds != null && value.ForbiddenPlayerIds.Length <= FeatureSaveLimits.MaximumPlayerId,
                "Assassin climb record header is invalid.");
            var players = new HashSet<int>();
            foreach (int playerId in value.ForbiddenPlayerIds)
            {
                FeatureSaveCodec.Require(playerId > 0 && playerId <= FeatureSaveLimits.MaximumPlayerId && players.Add(playerId),
                    "Assassin climb record contains an invalid or duplicate player ID.");
            }
        }
    }

    public sealed class TroopMovementSyncSaveStateFormatter : IMessagePackFormatter<TroopMovementSyncSaveState>
    {
        private const int FieldCount = 2;
        private const int EntryFieldCount = 6;

        public void Serialize(ref MessagePackWriter writer, TroopMovementSyncSaveState value, MessagePackSerializerOptions options)
        {
            Validate(value);
            writer.WriteArrayHeader(FieldCount);
            writer.Write(value.Version);
            writer.WriteArrayHeader(value.Entries.Length);
            foreach (TroopMovementSyncSaveEntry entry in value.Entries)
            {
                writer.WriteArrayHeader(EntryFieldCount);
                writer.Write(entry.TribeId);
                writer.Write(entry.TribeGlobalId);
                writer.Write(entry.Running);
                writer.Write(entry.RunningSpeedBonus);
                writer.Write(entry.PreviousMovementSpeed);
                writer.Write(entry.AppliedMovementSpeed);
            }
        }

        public TroopMovementSyncSaveState Deserialize(ref MessagePackReader reader, MessagePackSerializerOptions options)
        {
            FeatureSaveCodec.RequireHeader(ref reader, FieldCount, TroopMovementSyncSaveState.CurrentVersion, "Troop movement record");
            int length = FeatureSaveCodec.ReadLength(ref reader, FeatureSaveLimits.MaximumTribeId, "Troop movement entry array");
            var entries = new TroopMovementSyncSaveEntry[length];
            for (int index = 0; index < length; index++)
            {
                FeatureSaveCodec.Require(reader.ReadArrayHeader() == EntryFieldCount, "Troop movement entry has an invalid field count.");
                entries[index] = new TroopMovementSyncSaveEntry
                {
                    TribeId = reader.ReadInt32(),
                    TribeGlobalId = reader.ReadUInt32(),
                    Running = reader.ReadBoolean(),
                    RunningSpeedBonus = reader.ReadUInt16(),
                    PreviousMovementSpeed = reader.ReadUInt16(),
                    AppliedMovementSpeed = reader.ReadUInt16()
                };
            }

            var value = new TroopMovementSyncSaveState { Entries = entries };
            Validate(value);
            return value;
        }

        internal static void Validate(TroopMovementSyncSaveState value)
        {
            FeatureSaveCodec.Require(value != null && value.Version == TroopMovementSyncSaveState.CurrentVersion &&
                value.Entries != null && value.Entries.Length <= FeatureSaveLimits.MaximumTribeId,
                "Troop movement record header is invalid.");
            var tribeIds = new HashSet<int>();
            foreach (TroopMovementSyncSaveEntry entry in value.Entries)
            {
                FeatureSaveCodec.Require(entry.TribeId > 0 && entry.TribeId <= FeatureSaveLimits.MaximumTribeId &&
                    entry.TribeGlobalId != 0 && (entry.Running || entry.RunningSpeedBonus == 0) &&
                    tribeIds.Add(entry.TribeId),
                    "Troop movement record contains an invalid or duplicate tribe.");
            }
        }
    }
}
