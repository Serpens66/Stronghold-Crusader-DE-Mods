using System;

namespace BugfixesAndQoL
{
    internal static class ImprovedMoatFillingPolicy
    {
        internal const byte ReservationStep = 20;
        internal const byte TemporarilyExcludedReservation = 100;
        internal const int FillRelationshipMode = 2;
        internal const int PublishMoatTileMode = 1;
        internal const int ResolveFillApproachMode = 2;
        internal const uint MovementBlockedLowTileFlagMask = 0x00000030;
        internal const uint MovementBlockedStructureTileFlagMask = 0x10000100;
        internal const uint CompletedMoatTileFlag = 0x40000000;

        internal static bool ShouldInspectSelection(
            bool featureEnabled,
            int relationshipMode,
            bool supportedUnitType = true) =>
            featureEnabled && relationshipMode == FillRelationshipMode && supportedUnitType;

        internal static bool ShouldReplaceResolverResult(int mode, bool correlated) =>
            mode == ResolveFillApproachMode && correlated;

        internal static bool IsSameNativeRegion(short sourceRegion, short candidateRegion) =>
            sourceRegion == candidateRegion;

        internal static bool HasDownstreamMovementBlockingFlags(uint flags) =>
            (flags & MovementBlockedLowTileFlagMask) != 0 ||
            (flags & MovementBlockedStructureTileFlagMask) != 0;

        internal static bool IsCompletedMoat(uint flags) =>
            (flags & CompletedMoatTileFlag) != 0;

        internal static bool TryUndoVanillaReservation(
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

        internal static bool TryChoose(
            MoatApproachCandidate[] candidates,
            int sourceX,
            int sourceY,
            out MoatApproachCandidate selected)
        {
            if (candidates == null)
                throw new ArgumentNullException(nameof(candidates));

            selected = default;
            bool found = false;
            long bestDistance = long.MaxValue;
            for (int index = 0; index < candidates.Length; index++)
            {
                MoatApproachCandidate candidate = candidates[index];
                if (!candidate.Eligible)
                    continue;

                long dx = sourceX - candidate.X;
                long dy = sourceY - candidate.Y;
                long distance = dx * dx + dy * dy;
                // Strict comparison preserves Vanilla's N, NE, E, SE, S, SW, W, NW ties.
                if (!found || distance < bestDistance)
                {
                    found = true;
                    bestDistance = distance;
                    selected = candidate;
                }
            }
            return found;
        }
    }

    internal readonly struct MoatApproachCandidate
    {
        internal MoatApproachCandidate(int order, int x, int y, int tileId, bool eligible)
        {
            Order = order;
            X = x;
            Y = y;
            TileId = tileId;
            Eligible = eligible;
        }

        internal int Order { get; }
        internal int X { get; }
        internal int Y { get; }
        internal int TileId { get; }
        internal bool Eligible { get; }
    }
}
