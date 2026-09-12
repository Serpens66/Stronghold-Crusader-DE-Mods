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

    internal enum NativeGateSnapshotDecision
    {
        PreserveUncaptured,
        PreserveOwner,
        PreserveOwnerAlly,
        PreserveCapturer,
        PreserveCapturerAlly,
        ExcludeForeignCapture,
        InvalidQueryPlayer,
        UntrackedConnection,
        RecordIdMismatch,
        OwnerMismatch,
        CaptureMismatch,
        Exception
    }

    internal enum DiagnosticVerdict
    {
        PASS,
        FAIL,
        NOT_OBSERVED,
        NOT_APPLICABLE
    }

    internal enum CaptureTransitionKind
    {
        None,
        Captured,
        Recaptured
    }

    internal readonly struct NativeGateAccessRecord
    {
        internal NativeGateAccessRecord(
            bool valid,
            int ownerPlayerId,
            int capturedByPlayerId,
            ushort ownerRelatedPlayers,
            ushort capturerRelatedPlayers,
            ushort unrelatedPlayers)
        {
            Valid = valid;
            OwnerPlayerId = ownerPlayerId;
            CapturedByPlayerId = capturedByPlayerId;
            OwnerRelatedPlayers = ownerRelatedPlayers;
            CapturerRelatedPlayers = capturerRelatedPlayers;
            UnrelatedPlayers = unrelatedPlayers;
        }

        internal bool Valid { get; }
        internal int OwnerPlayerId { get; }
        internal int CapturedByPlayerId { get; }
        internal ushort OwnerRelatedPlayers { get; }
        internal ushort CapturerRelatedPlayers { get; }
        internal ushort UnrelatedPlayers { get; }

        internal bool PolicyEquals(NativeGateAccessRecord other) =>
            Valid == other.Valid &&
            OwnerPlayerId == other.OwnerPlayerId &&
            CapturedByPlayerId == other.CapturedByPlayerId &&
            OwnerRelatedPlayers == other.OwnerRelatedPlayers &&
            CapturerRelatedPlayers == other.CapturerRelatedPlayers &&
            UnrelatedPlayers == other.UnrelatedPlayers;
    }

    // Immutable data prepared outside native callbacks. The inline Capturer hook must
    // never call Script Extender APIs or calculate alliances while Vanilla is running.
    internal sealed class NativeGateAccessSnapshot
    {
        internal static readonly NativeGateAccessSnapshot Empty =
            new NativeGateAccessSnapshot(Array.Empty<NativeGateAccessRecord>(), 0,
                Array.Empty<uint>());

        internal NativeGateAccessSnapshot(
            NativeGateAccessRecord[] recordsByBuildingId,
            ulong rawFingerprint,
            uint[] gateGlobalsByBuildingId = null)
        {
            RecordsByBuildingId = recordsByBuildingId ?? Array.Empty<NativeGateAccessRecord>();
            RawFingerprint = rawFingerprint;
            GateGlobalsByBuildingId = gateGlobalsByBuildingId ?? Array.Empty<uint>();
            int tracked = 0;
            int captured = 0;
            int blockedPairs = 0;
            ulong policyFingerprint = 1469598103934665603UL;
            for (int index = 1; index < RecordsByBuildingId.Length; index++)
            {
                NativeGateAccessRecord record = RecordsByBuildingId[index];
                if (!record.Valid)
                    continue;
                tracked++;
                if (record.CapturedByPlayerId != 0)
                    captured++;
                for (int player = 1; player <= 8; player++)
                    if ((record.UnrelatedPlayers & (1 << player)) != 0)
                        blockedPairs++;
                unchecked
                {
                    policyFingerprint = (policyFingerprint ^ (uint)index) * 1099511628211UL;
                    policyFingerprint = (policyFingerprint ^ (uint)record.OwnerPlayerId) * 1099511628211UL;
                    policyFingerprint = (policyFingerprint ^ (uint)record.CapturedByPlayerId) * 1099511628211UL;
                    policyFingerprint = (policyFingerprint ^ record.OwnerRelatedPlayers) * 1099511628211UL;
                    policyFingerprint = (policyFingerprint ^ record.CapturerRelatedPlayers) * 1099511628211UL;
                    policyFingerprint = (policyFingerprint ^ record.UnrelatedPlayers) * 1099511628211UL;
                }
            }
            TrackedRecords = tracked;
            CapturedRecords = captured;
            BlockedPlayerGatePairs = blockedPairs;
            TopologyFingerprint = tracked == 0 ? 0 : policyFingerprint;
        }

        internal NativeGateAccessRecord[] RecordsByBuildingId { get; }
        internal ulong RawFingerprint { get; }
        internal uint[] GateGlobalsByBuildingId { get; }
        internal ulong TopologyFingerprint { get; }
        internal int TrackedRecords { get; }
        internal int CapturedRecords { get; }
        internal int UncapturedRecords => TrackedRecords - CapturedRecords;
        internal int BlockedPlayerGatePairs { get; }
        internal bool MatchesGateIdentity(int buildingId, uint subjectGlobalId) =>
            buildingId > 0 && buildingId < GateGlobalsByBuildingId.Length &&
            subjectGlobalId != 0 && GateGlobalsByBuildingId[buildingId] == subjectGlobalId;

        internal bool PolicyEquals(NativeGateAccessSnapshot other)
        {
            if (other == null || TrackedRecords != other.TrackedRecords)
                return false;
            int length = Math.Max(RecordsByBuildingId.Length, other.RecordsByBuildingId.Length);
            for (int buildingId = 1; buildingId < length; buildingId++)
            {
                NativeGateAccessRecord left = buildingId < RecordsByBuildingId.Length
                    ? RecordsByBuildingId[buildingId] : default;
                NativeGateAccessRecord right = buildingId < other.RecordsByBuildingId.Length
                    ? other.RecordsByBuildingId[buildingId] : default;
                if (!left.PolicyEquals(right))
                    return false;
            }
            return true;
        }

        internal NativeGateSnapshotDecision Evaluate(
            int queryPlayerId,
            int buildingId,
            int recordOwnerPlayerId,
            int nativeCapturedByPlayerId)
        {
            return Evaluate(queryPlayerId, buildingId, recordOwnerPlayerId,
                nativeCapturedByPlayerId, out _);
        }

        internal NativeGateSnapshotDecision Evaluate(
            int queryPlayerId,
            int buildingId,
            int recordOwnerPlayerId,
            int nativeCapturedByPlayerId,
            out NativeGateAccessRecord record)
        {
            record = default;
            if (queryPlayerId <= 0 || queryPlayerId > 8)
                return NativeGateSnapshotDecision.InvalidQueryPlayer;
            if (buildingId <= 0 || buildingId >= RecordsByBuildingId.Length)
                return NativeGateSnapshotDecision.UntrackedConnection;

            record = RecordsByBuildingId[buildingId];
            if (!record.Valid)
                return NativeGateSnapshotDecision.UntrackedConnection;
            if (record.OwnerPlayerId != recordOwnerPlayerId)
                return NativeGateSnapshotDecision.OwnerMismatch;
            if (record.CapturedByPlayerId != nativeCapturedByPlayerId)
                return NativeGateSnapshotDecision.CaptureMismatch;

            ushort playerBit = unchecked((ushort)(1 << queryPlayerId));
            if (queryPlayerId == record.OwnerPlayerId)
                return NativeGateSnapshotDecision.PreserveOwner;
            if ((record.OwnerRelatedPlayers & playerBit) != 0)
                return NativeGateSnapshotDecision.PreserveOwnerAlly;
            if (record.CapturedByPlayerId == 0)
                return NativeGateSnapshotDecision.PreserveUncaptured;
            if (queryPlayerId == record.CapturedByPlayerId)
                return NativeGateSnapshotDecision.PreserveCapturer;
            if ((record.CapturerRelatedPlayers & playerBit) != 0)
                return NativeGateSnapshotDecision.PreserveCapturerAlly;
            return (record.UnrelatedPlayers & playerBit) != 0
                ? NativeGateSnapshotDecision.ExcludeForeignCapture
                : NativeGateSnapshotDecision.UntrackedConnection;
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

    internal enum PassageAxisSource
    {
        None,
        EntryExitCoordinates,
        LinkedDrawbridge,
        ElongatedFootprint
    }

    internal static class PassageAxisResolver
    {
        internal static bool TryResolve(
            bool coordinatesAvailable,
            int entryX, int entryY, int exitX, int exitY,
            int gateMinX, int gateMinY, int gateMaxX, int gateMaxY,
            bool linkedBridgeAvailable,
            int bridgeMinX, int bridgeMinY, int bridgeMaxX, int bridgeMaxY,
            out bool horizontal,
            out PassageAxisSource source)
        {
            horizontal = false;
            source = PassageAxisSource.None;
            if (coordinatesAvailable && IsMapCoordinate(entryX, entryY) &&
                IsMapCoordinate(exitX, exitY))
            {
                int dx = Math.Abs(exitX - entryX);
                int dy = Math.Abs(exitY - entryY);
                if (dx != dy)
                {
                    horizontal = dx > dy;
                    source = PassageAxisSource.EntryExitCoordinates;
                    return true;
                }
            }

            if (linkedBridgeAvailable)
            {
                bool separatedX = bridgeMaxX < gateMinX || bridgeMinX > gateMaxX;
                bool separatedY = bridgeMaxY < gateMinY || bridgeMinY > gateMaxY;
                bool overlapsX = bridgeMinX <= gateMaxX && bridgeMaxX >= gateMinX;
                bool overlapsY = bridgeMinY <= gateMaxY && bridgeMaxY >= gateMinY;
                if (separatedX && overlapsY && !separatedY)
                {
                    horizontal = true;
                    source = PassageAxisSource.LinkedDrawbridge;
                    return true;
                }
                if (separatedY && overlapsX && !separatedX)
                {
                    horizontal = false;
                    source = PassageAxisSource.LinkedDrawbridge;
                    return true;
                }
            }

            int width = gateMaxX - gateMinX;
            int height = gateMaxY - gateMinY;
            if (gateMinX <= gateMaxX && gateMinY <= gateMaxY && width != height)
            {
                // The passage is perpendicular to the long wall/footprint axis.
                horizontal = height > width;
                source = PassageAxisSource.ElongatedFootprint;
                return true;
            }
            return false;
        }

        private static bool IsMapCoordinate(int x, int y) =>
            x >= 0 && x < EnemyGatePathfindingNativeDefinition.MapGridWidth &&
            y >= 0 && y < EnemyGatePathfindingNativeDefinition.MapGridWidth;
    }

    internal static class EnemyGatePathfindingPolicy
    {
        internal const ulong ZeroFlagMask = 1UL << 6;

        internal static ulong SetZeroFlag(ulong flags, bool isEqual) => isEqual
            ? flags | ZeroFlagMask
            : flags & ~ZeroFlagMask;

        internal static DiagnosticVerdict ObservationVerdict(long count) => count > 0
            ? DiagnosticVerdict.PASS
            : DiagnosticVerdict.NOT_OBSERVED;

        internal static DiagnosticVerdict IntegrityVerdict(bool observed, bool failed) => failed
            ? DiagnosticVerdict.FAIL
            : observed ? DiagnosticVerdict.PASS : DiagnosticVerdict.NOT_OBSERVED;

        internal static bool CursorPreviewCacheMatches(
            bool valid,
            int cachedPlayer,
            int cachedUnitId,
            int cachedTargetX,
            int cachedTargetY,
            int cachedTargetPcl,
            int cachedSourcePcl,
            ulong cachedFingerprint,
            int player,
            int unitId,
            int targetX,
            int targetY,
            int targetPcl,
            int sourcePcl,
            ulong fingerprint) =>
            valid && cachedPlayer == player && cachedUnitId == unitId &&
            cachedTargetX == targetX && cachedTargetY == targetY &&
            cachedTargetPcl == targetPcl && cachedSourcePcl == sourcePcl &&
            cachedFingerprint == fingerprint;

        internal static bool CursorPreviewStickyBlockMatches(
            bool valid,
            bool cachedAllowed,
            int cachedPlayer,
            int cachedUnitId,
            int cachedTargetPcl,
            int cachedSourcePcl,
            ulong cachedFingerprint,
            int player,
            int unitId,
            int targetPcl,
            int sourcePcl,
            ulong fingerprint) =>
            valid && !cachedAllowed && cachedPlayer == player &&
            cachedUnitId == unitId && cachedTargetPcl == targetPcl &&
            cachedSourcePcl == sourcePcl && cachedFingerprint == fingerprint;

        internal static int ApplyCursorPreviewResult(int vanillaResult,
            bool cacheValid, bool cacheAllowed) =>
            vanillaResult > 0 && cacheValid && !cacheAllowed
                ? 0 : vanillaResult;

        // A failed Vanilla search is attributed to the gate policy only when the
        // native direction adapters actually rejected at least one masked edge.
        internal static bool ShouldBlockCursorPreview(int nativeResult, long rejectedEdges) =>
            nativeResult == 0 && rejectedEdges > 0;

        internal static CaptureTransitionKind ClassifyCaptureTransition(
            bool previousValid,
            int previousCapturer,
            bool currentValid,
            int currentCapturer)
        {
            if (!previousValid || !currentValid || previousCapturer == currentCapturer)
                return CaptureTransitionKind.None;
            return previousCapturer == 0 && currentCapturer != 0
                ? CaptureTransitionKind.Captured
                : previousCapturer != 0 ? CaptureTransitionKind.Recaptured : CaptureTransitionKind.None;
        }

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
