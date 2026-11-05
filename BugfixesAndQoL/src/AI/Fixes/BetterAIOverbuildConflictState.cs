// Feature: Deterministic repetition guard for failed AI overbuild attempts.
using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    internal sealed class BetterAIOverbuildConflictState
    {
        internal const uint RepeatWindowTicks = 12000;

        private readonly Dictionary<BetterAIOverbuildBuildingIdentity, PendingRemoval>
            pendingRemovals = new Dictionary<BetterAIOverbuildBuildingIdentity, PendingRemoval>();
        private readonly Dictionary<BetterAIOverbuildConflictKey, ConfirmedRemoval>
            firstRemovals = new Dictionary<BetterAIOverbuildConflictKey, ConfirmedRemoval>();
        private readonly Dictionary<BetterAIOverbuildConflictKey, ConflictLock>
            locks = new Dictionary<BetterAIOverbuildConflictKey, ConflictLock>();
        private readonly Dictionary<BetterAIOverbuildBuildingIdentity,
            HashSet<BetterAIOverbuildConflictKey>> lockKeysByBlocker =
                new Dictionary<BetterAIOverbuildBuildingIdentity,
                    HashSet<BetterAIOverbuildConflictKey>>();
        private int pendingTick = int.MinValue;

        internal int ActiveLockCount => locks.Count;

        internal bool ShouldProtect(
            int tick,
            BetterAIOverbuildConflictKey key,
            int blockerId,
            uint blockerGlobalId)
        {
            var identity = new BetterAIOverbuildBuildingIdentity(blockerId, blockerGlobalId);
            if (locks.TryGetValue(key, out ConflictLock activeLock))
            {
                if (activeLock.CurrentBlocker.Equals(identity))
                    return true;

                // A replacement ends only this concrete lock. The new object may become a
                // fresh candidate, but is not proof that the previous cycle continued.
                locks.Remove(key);
                RemoveBlockerLockKey(activeLock.CurrentBlocker, key);
                firstRemovals.Remove(key);
                return false;
            }

            if (!firstRemovals.TryGetValue(key, out ConfirmedRemoval firstRemoval))
                return false;

            if (ElapsedTicks(tick, firstRemoval.Tick) > RepeatWindowTicks)
            {
                firstRemovals.Remove(key);
                return false;
            }

            if (firstRemoval.Blocker.GlobalId == blockerGlobalId)
                return false;

            firstRemovals.Remove(key);
            var conflictLock = new ConflictLock(identity);
            locks[key] = conflictLock;
            if (!lockKeysByBlocker.TryGetValue(
                    identity, out HashSet<BetterAIOverbuildConflictKey> blockerLocks))
            {
                blockerLocks = new HashSet<BetterAIOverbuildConflictKey>();
                lockKeysByBlocker[identity] = blockerLocks;
            }
            blockerLocks.Add(key);
            return true;
        }

        internal void RegisterDelegatedDecision(
            int tick,
            BetterAIOverbuildConflictKey key,
            int blockerId,
            uint blockerGlobalId)
        {
            BeginPendingTick(tick);
            var identity = new BetterAIOverbuildBuildingIdentity(blockerId, blockerGlobalId);
            pendingRemovals[identity] = new PendingRemoval(key);
        }

        internal bool ObserveBulldoze(int tick, int blockerId, uint blockerGlobalId)
        {
            var identity = new BetterAIOverbuildBuildingIdentity(blockerId, blockerGlobalId);
            if (RemoveLocks(identity) != 0)
                return false;

            if (pendingTick != tick)
            {
                pendingRemovals.Clear();
                pendingTick = tick;
                return false;
            }

            if (!pendingRemovals.TryGetValue(identity, out PendingRemoval pending))
                return false;

            pendingRemovals.Remove(identity);
            firstRemovals[pending.Key] = new ConfirmedRemoval(identity, tick);
            return true;
        }

        internal int ObserveRemoval(int blockerId, uint blockerGlobalId)
        {
            var identity = new BetterAIOverbuildBuildingIdentity(blockerId, blockerGlobalId);
            pendingRemovals.Remove(identity);
            return RemoveLocks(identity);
        }

        internal void Reset()
        {
            pendingTick = int.MinValue;
            pendingRemovals.Clear();
            firstRemovals.Clear();
            locks.Clear();
            lockKeysByBlocker.Clear();
        }

        internal static uint ElapsedTicks(int currentTick, int earlierTick) =>
            unchecked((uint)(currentTick - earlierTick));

        private int RemoveLocks(BetterAIOverbuildBuildingIdentity identity)
        {
            if (!lockKeysByBlocker.TryGetValue(
                    identity, out HashSet<BetterAIOverbuildConflictKey> blockerLockKeys))
                return 0;

            int removed = 0;
            foreach (BetterAIOverbuildConflictKey key in blockerLockKeys)
            {
                if (locks.Remove(key))
                    removed++;
                firstRemovals.Remove(key);
            }
            lockKeysByBlocker.Remove(identity);
            return removed;
        }

        private void RemoveBlockerLockKey(
            BetterAIOverbuildBuildingIdentity identity,
            BetterAIOverbuildConflictKey key)
        {
            if (!lockKeysByBlocker.TryGetValue(
                    identity, out HashSet<BetterAIOverbuildConflictKey> blockerLockKeys))
                return;
            blockerLockKeys.Remove(key);
            if (blockerLockKeys.Count == 0)
                lockKeysByBlocker.Remove(identity);
        }

        private void BeginPendingTick(int tick)
        {
            if (pendingTick == tick)
                return;
            pendingRemovals.Clear();
            pendingTick = tick;
        }

        private readonly struct PendingRemoval
        {
            internal PendingRemoval(BetterAIOverbuildConflictKey key)
            {
                Key = key;
            }

            internal BetterAIOverbuildConflictKey Key { get; }
        }

        private readonly struct ConfirmedRemoval
        {
            internal ConfirmedRemoval(BetterAIOverbuildBuildingIdentity blocker, int tick)
            {
                Blocker = blocker;
                Tick = tick;
            }

            internal BetterAIOverbuildBuildingIdentity Blocker { get; }
            internal int Tick { get; }
        }

        private readonly struct ConflictLock
        {
            internal ConflictLock(BetterAIOverbuildBuildingIdentity currentBlocker)
            {
                CurrentBlocker = currentBlocker;
            }

            internal BetterAIOverbuildBuildingIdentity CurrentBlocker { get; }
        }
    }

    internal readonly struct BetterAIOverbuildPlacementKey : IEquatable<BetterAIOverbuildPlacementKey>
    {
        internal BetterAIOverbuildPlacementKey(
            int placingPlayerId,
            int mapper,
            int baseX,
            int baseY,
            int orientation)
        {
            PlacingPlayerId = placingPlayerId;
            Mapper = mapper;
            BaseX = baseX;
            BaseY = baseY;
            Orientation = orientation;
        }

        internal int PlacingPlayerId { get; }
        internal int Mapper { get; }
        internal int BaseX { get; }
        internal int BaseY { get; }
        internal int Orientation { get; }

        internal static BetterAIOverbuildPlacementKey FromNativePass(
            int placingPlayerId,
            int mapper,
            int currentBaseX,
            int currentBaseY,
            int originalOrientation,
            int pass)
        {
            int correction = pass == 1
                ? originalOrientation < 11 ? 2 : originalOrientation == 11 ? 1 : 0
                : 0;
            return new BetterAIOverbuildPlacementKey(
                placingPlayerId,
                mapper,
                unchecked(currentBaseX + correction),
                unchecked(currentBaseY + correction),
                originalOrientation);
        }

        public bool Equals(BetterAIOverbuildPlacementKey other) =>
            PlacingPlayerId == other.PlacingPlayerId && Mapper == other.Mapper &&
            BaseX == other.BaseX && BaseY == other.BaseY && Orientation == other.Orientation;
        public override bool Equals(object obj) =>
            obj is BetterAIOverbuildPlacementKey other && Equals(other);
        public override int GetHashCode()
        {
            int hash = (PlacingPlayerId * 397) ^ Mapper;
            hash = (hash * 397) ^ BaseX;
            hash = (hash * 397) ^ BaseY;
            return (hash * 397) ^ Orientation;
        }
    }

    internal readonly struct BetterAIOverbuildBlockerKey : IEquatable<BetterAIOverbuildBlockerKey>
    {
        internal BetterAIOverbuildBlockerKey(
            int ownerId,
            int structureType,
            int anchorX,
            int anchorY)
        {
            OwnerId = ownerId;
            StructureType = structureType;
            AnchorX = anchorX;
            AnchorY = anchorY;
        }

        internal int OwnerId { get; }
        internal int StructureType { get; }
        internal int AnchorX { get; }
        internal int AnchorY { get; }

        public bool Equals(BetterAIOverbuildBlockerKey other) =>
            OwnerId == other.OwnerId && StructureType == other.StructureType &&
            AnchorX == other.AnchorX && AnchorY == other.AnchorY;
        public override bool Equals(object obj) =>
            obj is BetterAIOverbuildBlockerKey other && Equals(other);
        public override int GetHashCode()
        {
            int hash = (OwnerId * 397) ^ StructureType;
            hash = (hash * 397) ^ AnchorX;
            return (hash * 397) ^ AnchorY;
        }
    }

    internal readonly struct BetterAIOverbuildConflictKey : IEquatable<BetterAIOverbuildConflictKey>
    {
        internal BetterAIOverbuildConflictKey(
            BetterAIOverbuildPlacementKey placement,
            BetterAIOverbuildBlockerKey blocker)
        {
            Placement = placement;
            Blocker = blocker;
        }

        internal BetterAIOverbuildPlacementKey Placement { get; }
        internal BetterAIOverbuildBlockerKey Blocker { get; }

        public bool Equals(BetterAIOverbuildConflictKey other) =>
            Placement.Equals(other.Placement) && Blocker.Equals(other.Blocker);
        public override bool Equals(object obj) =>
            obj is BetterAIOverbuildConflictKey other && Equals(other);
        public override int GetHashCode() =>
            (Placement.GetHashCode() * 397) ^ Blocker.GetHashCode();
    }

    internal readonly struct BetterAIOverbuildBuildingIdentity :
        IEquatable<BetterAIOverbuildBuildingIdentity>
    {
        internal BetterAIOverbuildBuildingIdentity(int buildingId, uint globalId)
        {
            BuildingId = buildingId;
            GlobalId = globalId;
        }

        internal int BuildingId { get; }
        internal uint GlobalId { get; }

        public bool Equals(BetterAIOverbuildBuildingIdentity other) =>
            BuildingId == other.BuildingId && GlobalId == other.GlobalId;
        public override bool Equals(object obj) =>
            obj is BetterAIOverbuildBuildingIdentity other && Equals(other);
        public override int GetHashCode() =>
            (BuildingId * 397) ^ unchecked((int)GlobalId);
    }
}
