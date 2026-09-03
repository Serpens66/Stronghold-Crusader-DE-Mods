using System;
using System.Collections.Generic;

namespace EngineerSiegeFix
{
    internal static class EngineerCrewHandoffPolicy
    {
        internal const ushort EngineerType = 0x1E;
        internal const ushort CatapultType = 0x27;
        internal const ushort TrebuchetType = 0x28;
        internal const ushort BuildSiegeEngineCommand = 0x10;
        internal const ushort AiSiegeEngineerRole = 0x16;

        public static bool TrySelect(
            DeviceSnapshot device,
            IReadOnlyList<EngineerSnapshot> units,
            out EngineerSnapshot[] selected)
        {
            selected = null;
            int required = RequiredCrew(device.TargetType);
            if (!device.IsValid || required == 0 || units == null)
                return false;

            var matches = new List<EngineerSnapshot>(required + 1);
            var unitIds = new HashSet<int>();
            var globalIds = new HashSet<uint>();
            for (int index = 0; index < units.Count; index++)
            {
                EngineerSnapshot candidate = units[index];
                if (!IsCandidate(device, candidate))
                    continue;

                // Duplicate identities indicate a reused or inconsistent slot. Abort the
                // complete handoff instead of partially consuming an ambiguous crew.
                if (!unitIds.Add(candidate.UnitId) || !globalIds.Add(candidate.GlobalId))
                    return false;

                matches.Add(candidate);
                if (matches.Count > required)
                    return false;
            }

            if (matches.Count != required)
                return false;

            selected = matches.ToArray();
            return true;
        }

        public static int RequiredCrew(ushort targetType)
        {
            if (targetType == CatapultType)
                return 2;
            if (targetType == TrebuchetType)
                return 3;
            return 0;
        }

        public static bool IsScheduledCrewSearch(uint phaseSeed, uint gameTick) =>
            ((phaseSeed ^ gameTick ^ 0xFFFFFFF8U) & 0xFU) == 0;

        private static bool IsCandidate(DeviceSnapshot device, EngineerSnapshot candidate)
        {
            if (!candidate.IsAlive || candidate.UnitType != EngineerType ||
                candidate.OwnerId != device.OwnerId || candidate.GlobalId == 0 ||
                candidate.InternalAssignment != 0)
            {
                return false;
            }

            bool explicitlyAssigned =
                candidate.Command == BuildSiegeEngineCommand &&
                candidate.TargetUnitId == device.UnitId;
            bool aiAssigned =
                device.IsAiControlled &&
                device.TargetType == CatapultType &&
                candidate.AiRole == AiSiegeEngineerRole;
            if (!explicitlyAssigned && !aiAssigned)
                return false;

            int verticalDistance = Math.Abs(
                candidate.Height + candidate.HeightFine - device.Height - device.HeightFine);
            if (verticalDistance >= 0x11)
                return false;

            int worldDistance = Math.Max(
                Math.Abs(candidate.WorldX - device.WorldX),
                Math.Abs(candidate.WorldY - device.WorldY));
            return worldDistance < 0x1E;
        }
    }

    internal readonly struct DeviceSnapshot
    {
        public DeviceSnapshot(
            int unitId,
            uint globalId,
            byte ownerId,
            ushort targetType,
            bool isAiControlled,
            int worldX,
            int worldY,
            int height,
            int heightFine,
            bool isValid = true)
        {
            UnitId = unitId;
            GlobalId = globalId;
            OwnerId = ownerId;
            TargetType = targetType;
            IsAiControlled = isAiControlled;
            WorldX = worldX;
            WorldY = worldY;
            Height = height;
            HeightFine = heightFine;
            IsValid = isValid && unitId > 0 && globalId != 0 && ownerId >= 1 && ownerId <= 8;
        }

        public int UnitId { get; }
        public uint GlobalId { get; }
        public byte OwnerId { get; }
        public ushort TargetType { get; }
        public bool IsAiControlled { get; }
        public int WorldX { get; }
        public int WorldY { get; }
        public int Height { get; }
        public int HeightFine { get; }
        public bool IsValid { get; }
    }

    internal readonly struct EngineerSnapshot
    {
        public EngineerSnapshot(
            int unitId,
            uint globalId,
            byte ownerId,
            ushort unitType,
            bool isAlive,
            int internalAssignment,
            ushort command,
            ushort targetUnitId,
            ushort aiRole,
            ushort tribeLeaderUnitId,
            int worldX,
            int worldY,
            int height,
            int heightFine)
        {
            UnitId = unitId;
            GlobalId = globalId;
            OwnerId = ownerId;
            UnitType = unitType;
            IsAlive = isAlive;
            InternalAssignment = internalAssignment;
            Command = command;
            TargetUnitId = targetUnitId;
            AiRole = aiRole;
            TribeLeaderUnitId = tribeLeaderUnitId;
            WorldX = worldX;
            WorldY = worldY;
            Height = height;
            HeightFine = heightFine;
        }

        public int UnitId { get; }
        public uint GlobalId { get; }
        public byte OwnerId { get; }
        public ushort UnitType { get; }
        public bool IsAlive { get; }
        public int InternalAssignment { get; }
        public ushort Command { get; }
        public ushort TargetUnitId { get; }
        public ushort AiRole { get; }
        public ushort TribeLeaderUnitId { get; }
        public int WorldX { get; }
        public int WorldY { get; }
        public int Height { get; }
        public int HeightFine { get; }
    }
}
