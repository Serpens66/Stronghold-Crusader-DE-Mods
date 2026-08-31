using System;

namespace EnemyGatePathfindingTest
{
    internal readonly struct RouteTilePoint
    {
        internal RouteTilePoint(int x, int y) { X = x; Y = y; }
        internal int X { get; }
        internal int Y { get; }
    }

    internal readonly struct QueryCorrelationCandidate
    {
        internal QueryCorrelationCandidate(
            long timestamp,
            int playerId,
            int cursorX,
            int cursorY,
            int sourcePcl,
            int targetPcl,
            long result)
        {
            Timestamp = timestamp;
            PlayerId = playerId;
            CursorX = cursorX;
            CursorY = cursorY;
            SourcePcl = sourcePcl;
            TargetPcl = targetPcl;
            Result = result;
        }

        internal long Timestamp { get; }
        internal int PlayerId { get; }
        internal int CursorX { get; }
        internal int CursorY { get; }
        internal int SourcePcl { get; }
        internal int TargetPcl { get; }
        internal long Result { get; }
    }

    internal enum CapturedGateFilterDecision
    {
        PreserveVanilla,
        ExcludeForeignCapture,
        FailOpen
    }

    internal enum NativeQueryOrigin
    {
        Unavailable,
        HumanCursorOrCommandValidation,
        CommonUnitPathBuilder,
        OtherNativeCaller
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
        private static readonly int[] DirectionX = { 0, 1, 1, 1, 0, -1, -1, -1 };
        private static readonly int[] DirectionY = { -1, -1, 0, 1, 1, 1, 0, -1 };
        internal const int NeedsInitAliveState = 1;
        internal const int IsAliveAliveState = 2;

        internal static bool IsDiagnosticBuildingActive(int aliveState) =>
            aliveState == NeedsInitAliveState || aliveState == IsAliveAliveState;

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

        internal static NativeQueryOrigin ClassifyCallerRva(ulong callerRva)
        {
            if (callerRva == 0)
                return NativeQueryOrigin.Unavailable;
            if (callerRva >= EnemyGatePathfindingNativeDefinition.HumanCursorCommandStartRva &&
                callerRva < EnemyGatePathfindingNativeDefinition.HumanCursorCommandEndRva)
            {
                return NativeQueryOrigin.HumanCursorOrCommandValidation;
            }
            if (callerRva >= EnemyGatePathfindingNativeDefinition.CommonPathBuilderStartRva &&
                callerRva < EnemyGatePathfindingNativeDefinition.CommonPathBuilderEndRva)
            {
                return NativeQueryOrigin.CommonUnitPathBuilder;
            }
            return NativeQueryOrigin.OtherNativeCaller;
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

        internal static bool ShouldQueueDeferredDiagnostic(
            int sourcePcl, int targetPcl, long result, int filterRecordCount) =>
            sourcePcl == targetPcl || result == 0 || filterRecordCount > 0;

        internal static bool IsTopologyRelevantToQuery(
            int sourcePcl,
            int targetPcl,
            int entryPcl,
            int exitPcl,
            int[] footprintAndBorderPcls)
        {
            if (sourcePcl == targetPcl)
            {
                return ContainsPcl(footprintAndBorderPcls, sourcePcl);
            }

            // Editor NeedsInit gates can precede their authoritative gatehouse-array
            // entry. Their footprint is useful for diagnosis, but never for a fix.
            if (entryPcl < 0 || exitPcl < 0)
                return ContainsPcl(footprintAndBorderPcls, sourcePcl) ||
                    ContainsPcl(footprintAndBorderPcls, targetPcl);

            return (entryPcl == sourcePcl && exitPcl == targetPcl) ||
                (entryPcl == targetPcl && exitPcl == sourcePcl);
        }

        private static bool ContainsPcl(int[] values, int value)
        {
            if (values == null)
                return false;
            for (int index = 0; index < values.Length; index++)
            {
                if (values[index] == value)
                    return true;
            }
            return false;
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

        internal static int FindNearestPrecedingCorrelation(
            QueryCorrelationCandidate[] candidates,
            int count,
            long commandTimestamp,
            long maximumAge,
            int playerId,
            int targetX,
            int targetY,
            int targetPcl)
        {
            if (candidates == null || count <= 0 || maximumAge < 0)
                return -1;

            int boundedCount = Math.Min(count, candidates.Length);
            int bestIndex = -1;
            long bestAge = long.MaxValue;
            for (int index = 0; index < boundedCount; index++)
            {
                QueryCorrelationCandidate candidate = candidates[index];
                long age = commandTimestamp - candidate.Timestamp;
                if (age < 0 || age > maximumAge || age >= bestAge ||
                    candidate.PlayerId != playerId ||
                    candidate.CursorX != targetX || candidate.CursorY != targetY ||
                    candidate.SourcePcl != candidate.TargetPcl || candidate.Result == 0 ||
                    candidate.TargetPcl != targetPcl)
                {
                    continue;
                }

                bestIndex = index;
                bestAge = age;
            }
            return bestIndex;
        }

        internal static long CalculateUnknownRoleCount(long total, long human, long ai) =>
            Math.Max(0, total - Math.Max(0, human) - Math.Max(0, ai));

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

        internal static bool TrySelectPackedRouteDecoding(
            byte[] packedDirections,
            int pathLength,
            int startX,
            int startY,
            int targetX,
            int targetY,
            out bool beginAtTarget,
            out bool invertDirections)
        {
            beginAtTarget = false;
            invertDirections = false;
            if (packedDirections == null || pathLength <= 0 ||
                pathLength > EnemyGatePathfindingNativeDefinition.MaximumDecodedPathLength ||
                packedDirections.Length < (pathLength + 1) / 2)
                return false;

            for (int variant = 0; variant < 4; variant++)
            {
                bool fromTarget = (variant & 2) != 0;
                bool invert = (variant & 1) != 0;
                int x = fromTarget ? targetX : startX;
                int y = fromTarget ? targetY : startY;
                for (int step = 0; step < pathLength; step++)
                {
                    int direction = (packedDirections[step >> 1] >> ((step & 1) * 4)) & 0x0F;
                    if (direction > 7)
                        return false;
                    int sign = invert ? -1 : 1;
                    x += DirectionX[direction] * sign;
                    y += DirectionY[direction] * sign;
                    if (x < 0 || x >= EnemyGatePathfindingNativeDefinition.MapGridWidth ||
                        y < 0 || y >= EnemyGatePathfindingNativeDefinition.MapGridWidth)
                        break;
                }
                int expectedX = fromTarget ? startX : targetX;
                int expectedY = fromTarget ? startY : targetY;
                if (x == expectedX && y == expectedY)
                {
                    beginAtTarget = fromTarget;
                    invertDirections = invert;
                    return true;
                }
            }
            return false;
        }
    }
}
