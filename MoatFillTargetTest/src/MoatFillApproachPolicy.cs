using System;

namespace MoatFillTargetTest
{
    internal static class MoatFillApproachPolicy
    {
        internal const byte ReservationStep = 20;
        internal const byte TemporarilyExcludedReservation = 100;
        internal const int FillRelationshipMode = 2;
        internal const int PublishMoatTileMode = 1;
        internal const int ResolveFillApproachMode = 2;
        internal const uint MovementBlockedLowTileFlagMask = 0x00000030;
        internal const uint MovementBlockedStructureTileFlagMask = 0x10000100;
        internal const uint CompletedMoatTileFlag = 0x40000000;

        public static bool ShouldInspectSelection(
            int relationshipMode,
            bool supportedUnitType = true) =>
            relationshipMode == FillRelationshipMode && supportedUnitType;

        public static bool ShouldReplaceResolverResult(int mode, bool correlated) =>
            mode == ResolveFillApproachMode && correlated;

        public static bool IsSameNativeRegion(short sourceRegion, short candidateRegion) =>
            sourceRegion == candidateRegion;

        public static bool HasDownstreamMovementBlockingFlags(uint flags) =>
            (flags & MovementBlockedLowTileFlagMask) != 0 ||
            (flags & MovementBlockedStructureTileFlagMask) != 0;

        public static bool IsCompletedMoat(uint flags) =>
            (flags & CompletedMoatTileFlag) != 0;

        public static bool TryChoose(
            ApproachCandidate[] candidates,
            int sourceX,
            int sourceY,
            out ApproachCandidate selected,
            out ApproachDecisionSummary summary)
        {
            if (candidates == null)
                throw new ArgumentNullException(nameof(candidates));

            selected = default;
            summary = default;
            bool found = false;
            long bestDistance = long.MaxValue;
            for (int index = 0; index < candidates.Length; index++)
            {
                ApproachCandidate candidate = candidates[index];
                summary.Checked++;
                if (!candidate.Valid)
                {
                    summary.Invalid++;
                    continue;
                }
                if (candidate.CompletedMoat)
                {
                    summary.CompletedMoatRejected++;
                    continue;
                }
                if (!candidate.HeightAllowed || !candidate.SameRegion)
                {
                    summary.NativeGeometryRejected++;
                    continue;
                }
                if (!candidate.Walkable)
                {
                    summary.BlockedTerrain++;
                    continue;
                }
                if (candidate.Occupied)
                {
                    summary.Occupied++;
                    continue;
                }

                long dx = sourceX - candidate.X;
                long dy = sourceY - candidate.Y;
                long distance = dx * dx + dy * dy;
                // Strict comparison preserves Vanilla's N, NE, E, SE, S, SW, W, NW tie order.
                if (!found || distance < bestDistance)
                {
                    found = true;
                    bestDistance = distance;
                    selected = candidate;
                }
            }

            summary.Free = found ? 1 : 0;
            return found;
        }

        public static bool TryUndoVanillaReservation(
            byte reservationAfterSelection,
            out byte reservationBeforeSelection)
        {
            if (reservationAfterSelection < ReservationStep)
            {
                reservationBeforeSelection = reservationAfterSelection;
                return false;
            }

            reservationBeforeSelection = (byte)(reservationAfterSelection - ReservationStep);
            return true;
        }
    }

    internal readonly struct ApproachCandidate
    {
        public ApproachCandidate(
            int order,
            int x,
            int y,
            int tileId,
            bool valid,
            bool heightAllowed,
            bool sameRegion,
            bool completedMoat,
            bool walkable,
            bool occupied,
            int occupantUnitId)
        {
            Order = order;
            X = x;
            Y = y;
            TileId = tileId;
            Valid = valid;
            HeightAllowed = heightAllowed;
            SameRegion = sameRegion;
            CompletedMoat = completedMoat;
            Walkable = walkable;
            Occupied = occupied;
            OccupantUnitId = occupantUnitId;
        }

        public int Order { get; }
        public int X { get; }
        public int Y { get; }
        public int TileId { get; }
        public bool Valid { get; }
        public bool HeightAllowed { get; }
        public bool SameRegion { get; }
        public bool CompletedMoat { get; }
        public bool Walkable { get; }
        public bool Occupied { get; }
        public int OccupantUnitId { get; }
    }

    internal struct ApproachDecisionSummary
    {
        public int Checked;
        public int Invalid;
        public int NativeGeometryRejected;
        public int CompletedMoatRejected;
        public int BlockedTerrain;
        public int Occupied;
        public int Free;
    }
}
