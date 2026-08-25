// TEMPORARY INGAME DIAGNOSTICS - SAFE TO REMOVE.
// Removal: delete this file and BetterAIOverbuildDiagnostics.cs, then remove their Compile
// entries and BETTER_AI_OVERBUILD_DIAGNOSTICS from BugfixesAndQoL.csproj. The marked #if
// call sites then compile away and may be deleted separately.
using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    internal sealed class BetterAIOverbuildDiagnosticState
    {
        private readonly HashSet<PromotionKey> promotionsThisTick = new HashSet<PromotionKey>();
        private readonly HashSet<DecisionKey> decisionsThisTick = new HashSet<DecisionKey>();
        private readonly Dictionary<BuildingKey, PendingRemoval> pendingRemovals =
            new Dictionary<BuildingKey, PendingRemoval>();
        private readonly Dictionary<int, int> promotionCounts = new Dictionary<int, int>();
        private int currentTick = int.MinValue;

        internal int ProtectedCount { get; private set; }
        internal int DelegatedCount { get; private set; }
        internal int ConfirmedRemovalCount { get; private set; }
        internal int UncorrelatedDelegationCount { get; private set; }
        internal int DuplicateCount { get; private set; }

        internal bool RecordPromotion(int tick, int playerId, int mapper, int targetX, int targetY)
        {
            BeginTick(tick);
            if (!promotionsThisTick.Add(new PromotionKey(playerId, mapper, targetX, targetY)))
            {
                DuplicateCount++;
                return false;
            }

            promotionCounts.TryGetValue(mapper, out int count);
            promotionCounts[mapper] = count + 1;
            return true;
        }

        internal bool RecordDecision(
            int tick,
            int placingPlayerId,
            int mapper,
            int targetX,
            int targetY,
            int pass,
            int blockerId,
            uint blockerGlobalId,
            bool isProtected,
            out PendingRemoval pending)
        {
            BeginTick(tick);
            var decision = new DecisionKey(
                placingPlayerId, mapper, targetX, targetY, pass, blockerGlobalId);
            if (!decisionsThisTick.Add(decision))
            {
                DuplicateCount++;
                pending = default;
                return false;
            }

            if (isProtected)
            {
                ProtectedCount++;
                pending = default;
                return true;
            }

            DelegatedCount++;
            pending = new PendingRemoval(
                tick, placingPlayerId, mapper, targetX, targetY, pass, blockerId, blockerGlobalId);
            pendingRemovals[new BuildingKey(blockerId, blockerGlobalId)] = pending;
            return true;
        }

        internal bool ConfirmRemoval(
            int tick,
            int blockerId,
            uint blockerGlobalId,
            out PendingRemoval pending)
        {
            BeginTick(tick);
            var key = new BuildingKey(blockerId, blockerGlobalId);
            if (!pendingRemovals.TryGetValue(key, out pending) || pending.Tick != tick)
                return false;

            pendingRemovals.Remove(key);
            ConfirmedRemovalCount++;
            return true;
        }

        internal BetterAIOverbuildDiagnosticSummary SnapshotAndReset()
        {
            FlushPendingRemovals();
            var counts = new Dictionary<int, int>(promotionCounts);
            var summary = new BetterAIOverbuildDiagnosticSummary(
                counts,
                ProtectedCount,
                DelegatedCount,
                ConfirmedRemovalCount,
                UncorrelatedDelegationCount,
                DuplicateCount);
            Reset();
            return summary;
        }

        internal void Reset()
        {
            currentTick = int.MinValue;
            promotionsThisTick.Clear();
            decisionsThisTick.Clear();
            pendingRemovals.Clear();
            promotionCounts.Clear();
            ProtectedCount = 0;
            DelegatedCount = 0;
            ConfirmedRemovalCount = 0;
            UncorrelatedDelegationCount = 0;
            DuplicateCount = 0;
        }

        private void BeginTick(int tick)
        {
            if (tick == currentTick)
                return;

            FlushPendingRemovals();
            currentTick = tick;
            promotionsThisTick.Clear();
            decisionsThisTick.Clear();
        }

        private void FlushPendingRemovals()
        {
            UncorrelatedDelegationCount += pendingRemovals.Count;
            pendingRemovals.Clear();
        }

        private readonly struct PromotionKey : IEquatable<PromotionKey>
        {
            private readonly int playerId;
            private readonly int mapper;
            private readonly int targetX;
            private readonly int targetY;

            internal PromotionKey(int playerId, int mapper, int targetX, int targetY)
            {
                this.playerId = playerId;
                this.mapper = mapper;
                this.targetX = targetX;
                this.targetY = targetY;
            }

            public bool Equals(PromotionKey other) =>
                playerId == other.playerId && mapper == other.mapper &&
                targetX == other.targetX && targetY == other.targetY;

            public override bool Equals(object obj) => obj is PromotionKey other && Equals(other);
            public override int GetHashCode() =>
                (((playerId * 397) ^ mapper) * 397 ^ targetX) * 397 ^ targetY;
        }

        private readonly struct DecisionKey : IEquatable<DecisionKey>
        {
            private readonly int playerId;
            private readonly int mapper;
            private readonly int targetX;
            private readonly int targetY;
            private readonly int pass;
            private readonly uint blockerGlobalId;

            internal DecisionKey(
                int playerId, int mapper, int targetX, int targetY, int pass, uint blockerGlobalId)
            {
                this.playerId = playerId;
                this.mapper = mapper;
                this.targetX = targetX;
                this.targetY = targetY;
                this.pass = pass;
                this.blockerGlobalId = blockerGlobalId;
            }

            public bool Equals(DecisionKey other) =>
                playerId == other.playerId && mapper == other.mapper &&
                targetX == other.targetX && targetY == other.targetY &&
                pass == other.pass && blockerGlobalId == other.blockerGlobalId;

            public override bool Equals(object obj) => obj is DecisionKey other && Equals(other);
            public override int GetHashCode()
            {
                int hash = (playerId * 397) ^ mapper;
                hash = (hash * 397) ^ targetX;
                hash = (hash * 397) ^ targetY;
                hash = (hash * 397) ^ pass;
                return (hash * 397) ^ unchecked((int)blockerGlobalId);
            }
        }

        private readonly struct BuildingKey : IEquatable<BuildingKey>
        {
            private readonly int buildingId;
            private readonly uint globalId;

            internal BuildingKey(int buildingId, uint globalId)
            {
                this.buildingId = buildingId;
                this.globalId = globalId;
            }

            public bool Equals(BuildingKey other) =>
                buildingId == other.buildingId && globalId == other.globalId;
            public override bool Equals(object obj) => obj is BuildingKey other && Equals(other);
            public override int GetHashCode() => (buildingId * 397) ^ unchecked((int)globalId);
        }
    }

    internal readonly struct PendingRemoval
    {
        internal PendingRemoval(
            int tick, int placingPlayerId, int mapper, int targetX, int targetY,
            int pass, int blockerId, uint blockerGlobalId)
        {
            Tick = tick;
            PlacingPlayerId = placingPlayerId;
            Mapper = mapper;
            TargetX = targetX;
            TargetY = targetY;
            Pass = pass;
            BlockerId = blockerId;
            BlockerGlobalId = blockerGlobalId;
        }

        internal int Tick { get; }
        internal int PlacingPlayerId { get; }
        internal int Mapper { get; }
        internal int TargetX { get; }
        internal int TargetY { get; }
        internal int Pass { get; }
        internal int BlockerId { get; }
        internal uint BlockerGlobalId { get; }
    }

    internal readonly struct BetterAIOverbuildDiagnosticSummary
    {
        internal BetterAIOverbuildDiagnosticSummary(
            IReadOnlyDictionary<int, int> promotionCounts,
            int protectedCount,
            int delegatedCount,
            int confirmedRemovalCount,
            int uncorrelatedDelegationCount,
            int duplicateCount)
        {
            PromotionCounts = promotionCounts;
            ProtectedCount = protectedCount;
            DelegatedCount = delegatedCount;
            ConfirmedRemovalCount = confirmedRemovalCount;
            UncorrelatedDelegationCount = uncorrelatedDelegationCount;
            DuplicateCount = duplicateCount;
        }

        internal IReadOnlyDictionary<int, int> PromotionCounts { get; }
        internal int ProtectedCount { get; }
        internal int DelegatedCount { get; }
        internal int ConfirmedRemovalCount { get; }
        internal int UncorrelatedDelegationCount { get; }
        internal int DuplicateCount { get; }
    }
}
