using System;

namespace EnemyGatePathfindingTest
{
    internal readonly struct RouteTilePoint
    {
        internal RouteTilePoint(int x, int y) { X = x; Y = y; }
        internal int X { get; }
        internal int Y { get; }
    }

    internal enum CapturedGateFilterDecision
    {
        PreserveVanilla,
        ExcludeForeignCapture,
        FailOpen
    }

    internal readonly struct NativeGateAccessRecord
    {
        internal NativeGateAccessRecord(
            bool valid,
            int ownerPlayerId,
            int capturedByPlayerId,
            ushort unrelatedPlayers)
        {
            Valid = valid;
            OwnerPlayerId = ownerPlayerId;
            CapturedByPlayerId = capturedByPlayerId;
            UnrelatedPlayers = unrelatedPlayers;
        }

        internal bool Valid { get; }
        internal int OwnerPlayerId { get; }
        internal int CapturedByPlayerId { get; }
        internal ushort UnrelatedPlayers { get; }
    }

    // Immutable data prepared outside native callbacks. The inline Capturer hook must
    // never call Script Extender APIs or calculate alliances while Vanilla is running.
    internal sealed class NativeGateAccessSnapshot
    {
        internal static readonly NativeGateAccessSnapshot Empty =
            new NativeGateAccessSnapshot(Array.Empty<NativeGateAccessRecord>(), 0);

        internal NativeGateAccessSnapshot(
            NativeGateAccessRecord[] recordsByBuildingId,
            ulong topologyFingerprint)
        {
            RecordsByBuildingId = recordsByBuildingId ?? Array.Empty<NativeGateAccessRecord>();
            TopologyFingerprint = topologyFingerprint;
        }

        internal NativeGateAccessRecord[] RecordsByBuildingId { get; }
        internal ulong TopologyFingerprint { get; }

        internal CapturedGateFilterDecision Evaluate(
            int queryPlayerId,
            int buildingId,
            int recordOwnerPlayerId,
            bool vanillaSawUncaptured)
        {
            if (queryPlayerId <= 0 || queryPlayerId > 8 || buildingId <= 0 ||
                buildingId >= RecordsByBuildingId.Length)
                return CapturedGateFilterDecision.FailOpen;

            NativeGateAccessRecord record = RecordsByBuildingId[buildingId];
            if (!record.Valid || record.OwnerPlayerId != recordOwnerPlayerId ||
                (record.CapturedByPlayerId == 0) != vanillaSawUncaptured)
                return CapturedGateFilterDecision.FailOpen;

            if (record.CapturedByPlayerId == 0)
                return CapturedGateFilterDecision.PreserveVanilla;

            ushort playerBit = unchecked((ushort)(1 << queryPlayerId));
            return (record.UnrelatedPlayers & playerBit) != 0
                ? CapturedGateFilterDecision.ExcludeForeignCapture
                : CapturedGateFilterDecision.PreserveVanilla;
    }
}
    internal enum TopologyDiagnosticDisposition
    {
        Accepted,
        InvalidBridge,
        InvalidGatehouseId,
        InvalidGateState,
        InvalidGlobalId,
        MissingGatehouseEntry,
        InvalidDoorTiles,
        InvalidFootprint,
        InconsistentReread
    }

    internal static class EnemyGatePathfindingPolicy
    {
        // UPDATE REVIEW (CrusaderDE.dll): the direction-bit order is tied to the
        // eight native neighbor vectors and must be revalidated after a DLL update.
        internal static bool IsBidirectionalEdgeOpen(
            byte sourceDirectionBits,
            byte targetDirectionBits,
            int direction)
        {
            if (direction < 0 || direction > 7)
                return false;
            int opposite = (direction + 4) & 7;
            return (sourceDirectionBits & (1 << direction)) != 0 &&
                (targetDirectionBits & (1 << opposite)) != 0;
        }

        internal static TopologyDiagnosticDisposition ClassifyTopologyCandidate(
            bool bridgeActive,
            bool bridgeGlobalValid,
            bool gatehouseIdValid,
            bool gateActive,
            bool gateGlobalValid,
            bool gatehouseEntryValid,
            bool gatehouseEntryMatches,
            bool doorTilesValid,
            bool rereadConsistent,
            bool footprintValid)
        {
            if (!bridgeActive) return TopologyDiagnosticDisposition.InvalidBridge;
            if (!bridgeGlobalValid || !gateGlobalValid)
                return TopologyDiagnosticDisposition.InvalidGlobalId;
            if (!gatehouseIdValid) return TopologyDiagnosticDisposition.InvalidGatehouseId;
            if (!gateActive) return TopologyDiagnosticDisposition.InvalidGateState;
            if (!gatehouseEntryValid) return TopologyDiagnosticDisposition.MissingGatehouseEntry;
            if (!gatehouseEntryMatches || !rereadConsistent)
                return TopologyDiagnosticDisposition.InconsistentReread;
            if (!doorTilesValid) return TopologyDiagnosticDisposition.InvalidDoorTiles;
            if (!footprintValid) return TopologyDiagnosticDisposition.InvalidFootprint;
            return TopologyDiagnosticDisposition.Accepted;
        }

        internal static CapturedGateFilterDecision EvaluateGateAccess(
            int queryPlayerId,
            int ownerPlayerId,
            int capturedByPlayerId,
            Func<int, bool> isValidPlayer,
            Func<int, int, bool> isAllied)
        {
            if (isValidPlayer == null || isAllied == null ||
                !isValidPlayer(queryPlayerId) || !isValidPlayer(ownerPlayerId))
                return CapturedGateFilterDecision.FailOpen;

            if (isAllied(queryPlayerId, ownerPlayerId))
                return CapturedGateFilterDecision.PreserveVanilla;

            // Vanilla already excludes an uncaptured hostile entry. Keep its flags intact.
            if (capturedByPlayerId == 0)
                return CapturedGateFilterDecision.PreserveVanilla;
            if (!isValidPlayer(capturedByPlayerId))
                return CapturedGateFilterDecision.FailOpen;

            return isAllied(queryPlayerId, capturedByPlayerId)
                ? CapturedGateFilterDecision.PreserveVanilla
                : CapturedGateFilterDecision.ExcludeForeignCapture;
        }

        internal static bool IsUnrelatedGateCombination(
            int queryPlayerId,
            int ownerPlayerId,
            int capturedByPlayerId,
            Func<int, bool> isValidPlayer,
            Func<int, int, bool> isAllied)
        {
            if (isValidPlayer == null || isAllied == null ||
                !isValidPlayer(queryPlayerId) || !isValidPlayer(ownerPlayerId))
                return false;
            if (isAllied(queryPlayerId, ownerPlayerId))
                return false;
            if (capturedByPlayerId == 0)
                return true;
            return isValidPlayer(capturedByPlayerId) &&
                !isAllied(queryPlayerId, capturedByPlayerId);
        }

        internal static int CalculateRectangleDistance(
            int firstBeginX,
            int firstBeginY,
            int firstEndX,
            int firstEndY,
            int secondBeginX,
            int secondBeginY,
            int secondEndX,
            int secondEndY)
        {
            int dx = firstEndX < secondBeginX
                ? secondBeginX - firstEndX
                : secondEndX < firstBeginX ? firstBeginX - secondEndX : 0;
            int dy = firstEndY < secondBeginY
                ? secondBeginY - firstEndY
                : secondEndY < firstBeginY ? firstBeginY - secondEndY : 0;
            return Math.Max(dx, dy);
        }

        internal static bool AreFootprintsCardinallyAdjacent(
            RouteTilePoint[] first,
            RouteTilePoint[] second)
        {
            if (first == null || second == null || first.Length == 0 || second.Length == 0)
                return false;
            for (int firstIndex = 0; firstIndex < first.Length; firstIndex++)
            {
                for (int secondIndex = 0; secondIndex < second.Length; secondIndex++)
                {
                    int dx = Math.Abs(first[firstIndex].X - second[secondIndex].X);
                    int dy = Math.Abs(first[firstIndex].Y - second[secondIndex].Y);
                    if (dx + dy == 1)
                        return true;
                }
            }
            return false;
        }

        internal static int FindUniqueAdjacentCandidate(
            RouteTilePoint[] bridge,
            RouteTilePoint[][] gates,
            bool[] eligible)
        {
            if (bridge == null || gates == null || eligible == null)
                return -1;
            int count = Math.Min(gates.Length, eligible.Length);
            int match = -1;
            for (int index = 0; index < count; index++)
            {
                if (!eligible[index] || !AreFootprintsCardinallyAdjacent(bridge, gates[index]))
                    continue;
                if (match >= 0)
                    return -1;
                match = index;
            }
            return match;
        }

    }
}
