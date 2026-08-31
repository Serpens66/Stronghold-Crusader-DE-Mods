using System;
using System.IO;

namespace EnemyGatePathfindingTest
{
    internal static class Program
    {
        private static int assertions;

        private static int Main()
        {
            try
            {
                UncapturedEnemyPreservesVanillaExclusion();
                OwnAndAlliedOwnersRemainEligible();
                OwnAndAlliedCaptureRemainEligible();
                UnrelatedThirdPlayerCaptureIsExcluded();
                CaptureAndRecaptureApplyImmediately();
                InvalidStateFailsOpen();
                CallerRangesCoverHumanCursorAndCommonPathBuilder();
                NativeContractIncludesDrawbridgePclAndExactFilterSite();
                SamePclCandidatePolicyIsFailOpenAndAllianceAware();
                DiagnosticNativeContractIsPinned();
                DeferredDiagnosticQueuePolicyIsSelective();
                TopologyQueryMatchingDistinguishesGateAndBridgeCases();
                RectangleDistanceSupportsSpatialBridgeDiagnosis();
                DiagnosticBuildingStatesAreEditorSafe();
                TopologyRejectionClassificationIsDeterministic();
                MoveCorrelationUsesNearestMatchingPredecessor();
                MoveCorrelationRejectsMismatchesAndExpiredQueries();
                MoveRoleAccountingAlwaysBalances();
                FootprintAdjacencyIgnoresBrokenEditorBounds();
                UniqueSpatialGateAssociationFailsOpenWhenAmbiguous();
                PackedRouteDecodingValidatesEndpointAndLimits();
                DirectionEdgesRequireBothNativeDirections();
                TileRouteNativeContractIsPinned();
                NativeRouteHotPathsRemainPrimitiveOnly();
                Console.WriteLine("EnemyGatePathfindingPolicy: {0} assertions passed.", assertions);
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return 1;
            }
        }

        private static void UncapturedEnemyPreservesVanillaExclusion()
        {
            AssertDecision(CapturedGateFilterDecision.PreserveVanilla, 1, 0);
        }

        private static void OwnAndAlliedOwnersRemainEligible()
        {
            AssertDecision(CapturedGateFilterDecision.PreserveVanilla, 1, 0, ownerPlayer: 1);
            AssertDecision(CapturedGateFilterDecision.PreserveVanilla, 1, 0, ownerPlayer: 2);
        }

        private static void OwnAndAlliedCaptureRemainEligible()
        {
            AssertDecision(CapturedGateFilterDecision.PreserveVanilla, 1, 1);
            AssertDecision(CapturedGateFilterDecision.PreserveVanilla, 1, 2);
        }

        private static void UnrelatedThirdPlayerCaptureIsExcluded()
        {
            AssertDecision(CapturedGateFilterDecision.ExcludeForeignCapture, 1, 3);
            AssertDecision(CapturedGateFilterDecision.ExcludeForeignCapture, 1, 4);
        }

        private static void CaptureAndRecaptureApplyImmediately()
        {
            AssertDecision(CapturedGateFilterDecision.ExcludeForeignCapture, 1, 4);
            AssertDecision(CapturedGateFilterDecision.PreserveVanilla, 1, 2);
            AssertDecision(CapturedGateFilterDecision.ExcludeForeignCapture, 1, 4);
        }

        private static void InvalidStateFailsOpen()
        {
            AssertDecision(CapturedGateFilterDecision.FailOpen, 0, 3);
            AssertDecision(CapturedGateFilterDecision.FailOpen, 1, 99);
            AssertDecision(CapturedGateFilterDecision.FailOpen, 1, 0, ownerPlayer: 99);
            Assert(
                EnemyGatePathfindingPolicy.EvaluateGateAccess(1, 3, 3, null, Allied) ==
                    CapturedGateFilterDecision.FailOpen,
                "missing validity callback fails open");
            Assert(
                EnemyGatePathfindingPolicy.EvaluateGateAccess(1, 3, 3, ValidPlayer, null) ==
                    CapturedGateFilterDecision.FailOpen,
                "missing alliance callback fails open");
        }

        private static void CallerRangesCoverHumanCursorAndCommonPathBuilder()
        {
            Assert(
                EnemyGatePathfindingPolicy.ClassifyCallerRva(0x8D632) ==
                    NativeQueryOrigin.HumanCursorOrCommandValidation,
                "known human cursor return address classified");
            Assert(
                EnemyGatePathfindingPolicy.ClassifyCallerRva(0x196528) ==
                    NativeQueryOrigin.CommonUnitPathBuilder,
                "known common path-builder return address classified");
            Assert(
                EnemyGatePathfindingPolicy.ClassifyCallerRva(0x123456) ==
                    NativeQueryOrigin.OtherNativeCaller,
                "unclassified native caller retained");
            Assert(
                EnemyGatePathfindingPolicy.ClassifyCallerRva(0) == NativeQueryOrigin.Unavailable,
                "missing stack attribution classified");
        }

        private static void NativeContractIncludesDrawbridgePclAndExactFilterSite()
        {
            Assert(EnemyGatePathfindingNativeDefinition.NativeRecordStride == 0x204, "record stride");
            Assert(EnemyGatePathfindingNativeDefinition.RecordFirstPclOffset == -0x1E8, "first PCL");
            Assert(EnemyGatePathfindingNativeDefinition.RecordSecondPclOffset == -0x1E4, "second PCL");
            Assert(EnemyGatePathfindingNativeDefinition.RecordThirdPclOffset == -0x34, "drawbridge PCL");
            Assert(EnemyGatePathfindingNativeDefinition.CapturedByCompareRva == 0xE2710, "filter RVA");
            Assert(EnemyGatePathfindingNativeDefinition.CapturedByCompareHookLength == 9, "filter span");
            Assert(EnemyGatePathfindingNativeDefinition.AuditedDirectCallerCount == 84, "caller inventory");
        }

        private static void SamePclCandidatePolicyIsFailOpenAndAllianceAware()
        {
            Assert(EnemyGatePathfindingPolicy.IsUnrelatedGateCombination(1, 3, 0, ValidPlayer, Allied),
                "uncaptured hostile bridge is a candidate");
            Assert(EnemyGatePathfindingPolicy.IsUnrelatedGateCombination(1, 3, 4, ValidPlayer, Allied),
                "foreign third-party capture remains a candidate");
            Assert(!EnemyGatePathfindingPolicy.IsUnrelatedGateCombination(1, 1, 0, ValidPlayer, Allied),
                "own bridge is not a candidate");
            Assert(!EnemyGatePathfindingPolicy.IsUnrelatedGateCombination(1, 2, 0, ValidPlayer, Allied),
                "allied bridge is not a candidate");
            Assert(!EnemyGatePathfindingPolicy.IsUnrelatedGateCombination(1, 3, 2, ValidPlayer, Allied),
                "allied capture is not a candidate");
            Assert(!EnemyGatePathfindingPolicy.IsUnrelatedGateCombination(0, 3, 0, ValidPlayer, Allied),
                "invalid query fails open");
            Assert(!EnemyGatePathfindingPolicy.IsUnrelatedGateCombination(1, 99, 0, ValidPlayer, Allied),
                "invalid owner fails open");
        }

        private static void DiagnosticNativeContractIsPinned()
        {
            Assert(EnemyGatePathfindingNativeDefinition.MoveHereRva == 0x196280, "MoveHere RVA");
            Assert(EnemyGatePathfindingNativeDefinition.CursorTargetSignatureRva == 0x8F3A8,
                "cursor signature RVA");
            Assert(EnemyGatePathfindingNativeDefinition.CursorTargetXRva == 0x3A11E2C,
                "cursor X RVA");
            Assert(EnemyGatePathfindingNativeDefinition.CursorTargetYRva == 0x3A11E30,
                "cursor Y RVA");
        }

        private static void DeferredDiagnosticQueuePolicyIsSelective()
        {
            Assert(EnemyGatePathfindingPolicy.ShouldQueueDeferredDiagnostic(7, 7, 1, 0),
                "Same-PCL query is deferred");
            Assert(EnemyGatePathfindingPolicy.ShouldQueueDeferredDiagnostic(7, 8, 1, 1),
                "capturer-filter query is deferred");
            Assert(EnemyGatePathfindingPolicy.ShouldQueueDeferredDiagnostic(7, 8, 0, 0),
                "negative different-PCL query is deferred for blocked-cursor diagnosis");
            Assert(!EnemyGatePathfindingPolicy.ShouldQueueDeferredDiagnostic(7, 8, 1, 0),
                "ordinary different-PCL query stays counter-only");
        }

        private static void TopologyQueryMatchingDistinguishesGateAndBridgeCases()
        {
            int[] footprintPcls = { 11, 12, 13 };
            Assert(EnemyGatePathfindingPolicy.IsTopologyRelevantToQuery(
                12, 12, 20, 21, footprintPcls),
                "Same-PCL bridge or gate footprint is relevant");
            Assert(!EnemyGatePathfindingPolicy.IsTopologyRelevantToQuery(
                14, 14, 20, 21, footprintPcls),
                "unrelated Same-PCL query is ignored");
            Assert(EnemyGatePathfindingPolicy.IsTopologyRelevantToQuery(
                20, 21, 20, 21, footprintPcls),
                "forward gate entry/exit query is relevant");
            Assert(EnemyGatePathfindingPolicy.IsTopologyRelevantToQuery(
                21, 20, 20, 21, footprintPcls),
                "reverse gate entry/exit query is relevant");
            Assert(!EnemyGatePathfindingPolicy.IsTopologyRelevantToQuery(
                20, 22, 20, 21, footprintPcls),
                "different unrelated PCL pair is ignored");
            Assert(EnemyGatePathfindingPolicy.IsTopologyRelevantToQuery(
                12, 22, -1, -1, footprintPcls),
                "NeedsInit gate fallback associates a touching source PCL");
            Assert(!EnemyGatePathfindingPolicy.IsTopologyRelevantToQuery(
                14, 22, -1, -1, footprintPcls),
                "NeedsInit gate fallback ignores an unrelated PCL pair");
            Assert(!EnemyGatePathfindingPolicy.IsTopologyRelevantToQuery(
                12, 12, 20, 21, null),
                "missing footprint data fails open");
        }

        private static void RectangleDistanceSupportsSpatialBridgeDiagnosis()
        {
            Assert(EnemyGatePathfindingPolicy.CalculateRectangleDistance(
                1, 1, 3, 3, 2, 2, 4, 4) == 0,
                "overlapping rectangles have zero distance");
            Assert(EnemyGatePathfindingPolicy.CalculateRectangleDistance(
                1, 1, 3, 3, 4, 1, 6, 3) == 1,
                "touching tile columns have distance one");
            Assert(EnemyGatePathfindingPolicy.CalculateRectangleDistance(
                1, 1, 3, 3, 8, 9, 10, 11) == 6,
                "Chebyshev rectangle distance uses the farther axis");
        }

        private static void DiagnosticBuildingStatesAreEditorSafe()
        {
            Assert(EnemyGatePathfindingPolicy.IsDiagnosticBuildingActive(1),
                "NeedsInit is active for editor diagnostics");
            Assert(EnemyGatePathfindingPolicy.IsDiagnosticBuildingActive(2),
                "IsAlive is active for diagnostics");
            Assert(!EnemyGatePathfindingPolicy.IsDiagnosticBuildingActive(0),
                "empty building state is inactive");
            Assert(!EnemyGatePathfindingPolicy.IsDiagnosticBuildingActive(3),
                "marked-for-deletion building is inactive");
            Assert(!EnemyGatePathfindingPolicy.IsDiagnosticBuildingActive(6),
                "paused or unknown building state is inactive");
        }

        private static void TopologyRejectionClassificationIsDeterministic()
        {
            AssertTopology(TopologyDiagnosticDisposition.InvalidBridge,
                false, true, true, true, true, true, true, true, true, true);
            AssertTopology(TopologyDiagnosticDisposition.InvalidGlobalId,
                true, false, true, true, true, true, true, true, true, true);
            AssertTopology(TopologyDiagnosticDisposition.InvalidGatehouseId,
                true, true, false, true, true, true, true, true, true, true);
            AssertTopology(TopologyDiagnosticDisposition.InvalidGateState,
                true, true, true, false, true, true, true, true, true, true);
            AssertTopology(TopologyDiagnosticDisposition.MissingGatehouseEntry,
                true, true, true, true, true, false, true, true, true, true);
            AssertTopology(TopologyDiagnosticDisposition.InconsistentReread,
                true, true, true, true, true, true, false, true, true, true);
            AssertTopology(TopologyDiagnosticDisposition.InvalidDoorTiles,
                true, true, true, true, true, true, true, false, true, true);
            AssertTopology(TopologyDiagnosticDisposition.InconsistentReread,
                true, true, true, true, true, true, true, true, false, true);
            AssertTopology(TopologyDiagnosticDisposition.InvalidFootprint,
                true, true, true, true, true, true, true, true, true, false);
            AssertTopology(TopologyDiagnosticDisposition.Accepted,
                true, true, true, true, true, true, true, true, true, true);
        }

        private static void MoveCorrelationUsesNearestMatchingPredecessor()
        {
            var candidates = new[]
            {
                new QueryCorrelationCandidate(100, 2, 400, 401, 7, 7, 1),
                new QueryCorrelationCandidate(180, 2, 400, 401, 7, 7, 1),
                new QueryCorrelationCandidate(190, 2, 400, 401, 8, 8, 1)
            };
            Assert(EnemyGatePathfindingPolicy.FindNearestPrecedingCorrelation(
                    candidates, candidates.Length, 200, 150, 2, 400, 401, 7) == 1,
                "nearest matching preceding query is selected");
        }

        private static void MoveCorrelationRejectsMismatchesAndExpiredQueries()
        {
            AssertNoCorrelation(new QueryCorrelationCandidate(10, 2, 400, 401, 7, 7, 1),
                200, 100, 2, 400, 401, 7, "expired query");
            AssertNoCorrelation(new QueryCorrelationCandidate(100, 3, 400, 401, 7, 7, 1),
                200, 150, 2, 400, 401, 7, "different player");
            AssertNoCorrelation(new QueryCorrelationCandidate(100, 2, 402, 401, 7, 7, 1),
                200, 150, 2, 400, 401, 7, "different coordinates");
            AssertNoCorrelation(new QueryCorrelationCandidate(100, 2, 400, 401, 7, 8, 1),
                200, 150, 2, 400, 401, 8, "different source and target PCL");
            AssertNoCorrelation(new QueryCorrelationCandidate(100, 2, 400, 401, 7, 7, 0),
                200, 150, 2, 400, 401, 7, "negative PCL result");
            AssertNoCorrelation(new QueryCorrelationCandidate(100, 2, 400, 401, 7, 7, 1),
                90, 150, 2, 400, 401, 7, "future query");
        }

        private static void MoveRoleAccountingAlwaysBalances()
        {
            Assert(EnemyGatePathfindingPolicy.CalculateUnknownRoleCount(13, 8, 3) == 2,
                "unclassified MoveHere calls remain unknown");
            Assert(EnemyGatePathfindingPolicy.CalculateUnknownRoleCount(2, 4, 1) == 0,
                "role accounting never becomes negative");
        }

        private static void FootprintAdjacencyIgnoresBrokenEditorBounds()
        {
            var bridge = new[] { new RouteTilePoint(385, 416), new RouteTilePoint(386, 416) };
            var gate = new[] { new RouteTilePoint(385, 417), new RouteTilePoint(386, 417) };
            var distant = new[] { new RouteTilePoint(390, 420) };
            Assert(EnemyGatePathfindingPolicy.AreFootprintsCardinallyAdjacent(bridge, gate),
                "occupied bridge and gate footprints are directly adjacent");
            Assert(!EnemyGatePathfindingPolicy.AreFootprintsCardinallyAdjacent(bridge, distant),
                "distant footprints are not associated");
            Assert(!EnemyGatePathfindingPolicy.AreFootprintsCardinallyAdjacent(null, gate),
                "missing footprint fails open");
        }

        private static void UniqueSpatialGateAssociationFailsOpenWhenAmbiguous()
        {
            var bridge = new[] { new RouteTilePoint(10, 10) };
            var gates = new[]
            {
                new[] { new RouteTilePoint(10, 11) },
                new[] { new RouteTilePoint(11, 10) },
                new[] { new RouteTilePoint(30, 30) }
            };
            Assert(EnemyGatePathfindingPolicy.FindUniqueAdjacentCandidate(
                    bridge, gates, new[] { true, false, true }) == 0,
                "same-owner eligibility selects one adjacent gate");
            Assert(EnemyGatePathfindingPolicy.FindUniqueAdjacentCandidate(
                    bridge, gates, new[] { true, true, true }) == -1,
                "two adjacent candidates fail open");
            Assert(EnemyGatePathfindingPolicy.FindUniqueAdjacentCandidate(
                    bridge, gates, new[] { false, false, true }) == -1,
                "no eligible adjacent candidate fails open");
        }

        private static void PackedRouteDecodingValidatesEndpointAndLimits()
        {
            Assert(EnemyGatePathfindingPolicy.TrySelectPackedRouteDecoding(
                    new byte[] { 0x22 }, 2, 10, 10, 12, 10,
                    out bool fromTarget, out bool invert) && !fromTarget && !invert,
                "low nibble then high nibble decode an eastward route");
            Assert(!EnemyGatePathfindingPolicy.TrySelectPackedRouteDecoding(
                    new byte[] { 0x08 }, 1, 10, 10, 11, 10, out _, out _),
                "invalid direction nibble is rejected");
            Assert(!EnemyGatePathfindingPolicy.TrySelectPackedRouteDecoding(
                    new byte[] { 0x22 }, 3, 10, 10, 13, 10, out _, out _),
                "short packed buffer is rejected");
            Assert(!EnemyGatePathfindingPolicy.TrySelectPackedRouteDecoding(
                    new byte[1001], 2001, 10, 10, 10, 10, out _, out _),
                "path length above native limit is rejected");
        }

        private static void DirectionEdgesRequireBothNativeDirections()
        {
            Assert(EnemyGatePathfindingPolicy.IsBidirectionalEdgeOpen(0x04, 0x40, 2),
                "east edge and west opposite edge form a native connection");
            Assert(!EnemyGatePathfindingPolicy.IsBidirectionalEdgeOpen(0x04, 0x00, 2),
                "missing opposite edge is closed");
            Assert(!EnemyGatePathfindingPolicy.IsBidirectionalEdgeOpen(0x00, 0x40, 2),
                "missing source edge is closed");
            Assert(!EnemyGatePathfindingPolicy.IsBidirectionalEdgeOpen(0xFF, 0xFF, -1),
                "invalid negative direction fails closed inside the managed search");
            Assert(!EnemyGatePathfindingPolicy.IsBidirectionalEdgeOpen(0xFF, 0xFF, 8),
                "direction above native range fails closed inside the managed search");
            Assert(EnemyGatePathfindingPolicy.CloseNeighborEdge(0xFF, 2) == 0xBF,
                "blocking east clears only the western neighbor edge");
            Assert(EnemyGatePathfindingPolicy.CloseNeighborEdge(0x55, -1) == 0x55,
                "invalid edge closure leaves the native byte unchanged");
        }

        private static void TileRouteNativeContractIsPinned()
        {
            Assert(EnemyGatePathfindingNativeDefinition.CentralMovementPlanRva == 0x18E1E0,
                "central planner RVA");
            Assert(EnemyGatePathfindingNativeDefinition.MainPathBuilderRva == 0xF4930,
                "main builder RVA");
            Assert(EnemyGatePathfindingNativeDefinition.AlternatePathBuilderRva == 0xE32B0,
                "alternate builder RVA");
            Assert(EnemyGatePathfindingNativeDefinition.CursorReachabilityRva == 0xE9FF0,
                "cursor reachability RVA");
            Assert(EnemyGatePathfindingNativeDefinition.CursorPclDecisionRva == 0x8F1C4,
                "ordinary cursor PCL decision RVA");
            Assert(EnemyGatePathfindingNativeDefinition.CursorPclDecisionHookLength == 14,
                "ordinary cursor PCL decision span");
            Assert(EnemyGatePathfindingNativeDefinition.PathDirectionGridRva == 0x51890D0,
                "native direction grid RVA");
            Assert(EnemyGatePathfindingNativeDefinition.CommandPclDecisionRva == 0x11B75A,
                "shared command PCL decision RVA");
            Assert(EnemyGatePathfindingNativeDefinition.CommandPclDecisionHookLength == 14,
                "shared command PCL decision audited span");
            Assert(EnemyGatePathfindingNativeDefinition.PathDirectionBufferOffset == 0x155F60,
                "path direction buffer offset");
            Assert(EnemyGatePathfindingNativeDefinition.PathLengthOffset == 0x155F68,
                "path length offset");
            Assert(EnemyGatePathfindingNativeDefinition.MaximumDecodedPathLength == 2000,
                "native path length limit");
        }

        private static void NativeRouteHotPathsRemainPrimitiveOnly()
        {
            string source = File.ReadAllText(Path.Combine("src", "TileRouteDiagnostics.cs"));
            string[] methods =
            {
                "OnMoveHere", "ObservePlan", "BuildPlayerAwareRoute",
                "FilterPositiveCursorPcl", "SearchWithoutBlocked", "ApplyOverlay",
                "RestoreOverlay", "QueueSample"
            };
            string[] forbidden =
            {
                "GamePlayerManagerAPI", "GameUnitManagerAPI", "DebugLogHelper",
                "Monitor.", "lock (", "StringBuilder", "Console.",
                "new List", "new Dictionary", "new int[", "new byte[", "new string"
            };
            foreach (string method in methods)
            {
                string body = ExtractMethodBody(source, method);
                foreach (string token in forbidden)
                    Assert(body.IndexOf(token, StringComparison.Ordinal) < 0,
                        method + " hot path excludes " + token);
            }
        }

        private static string ExtractMethodBody(string source, string methodName)
        {
            bool returnsInt = methodName == "ObservePlan" ||
                methodName == "BuildPlayerAwareRoute" || methodName == "SearchWithoutBlocked";
            string signature = methodName == "OnMoveHere"
                ? "internal void " + methodName + "("
                : (returnsInt ? "private int " : "private void ") + methodName + "(";
            int name = source.IndexOf(signature, StringComparison.Ordinal);
            if (name < 0)
                throw new InvalidOperationException("Method not found for hot-path audit: " + methodName);
            int open = source.IndexOf('{', name);
            if (open < 0)
                throw new InvalidOperationException("Method body not found for hot-path audit: " + methodName);
            int depth = 0;
            for (int index = open; index < source.Length; index++)
            {
                if (source[index] == '{') depth++;
                else if (source[index] == '}' && --depth == 0)
                    return source.Substring(open, index - open + 1);
            }
            throw new InvalidOperationException("Unterminated method body: " + methodName);
        }

        private static void AssertTopology(TopologyDiagnosticDisposition expected,
            bool bridgeActive, bool bridgeGlobal, bool gateId, bool gateActive,
            bool gateGlobal, bool entry, bool entryMatches, bool doors,
            bool reread, bool footprint)
        {
            Assert(EnemyGatePathfindingPolicy.ClassifyTopologyCandidate(
                    bridgeActive, bridgeGlobal, gateId, gateActive, gateGlobal,
                    entry, entryMatches, doors, reread, footprint) == expected,
                "topology disposition " + expected);
        }

        private static void AssertNoCorrelation(QueryCorrelationCandidate candidate,
            long commandTimestamp, long maximumAge, int player, int x, int y,
            int pcl, string message)
        {
            Assert(EnemyGatePathfindingPolicy.FindNearestPrecedingCorrelation(
                    new[] { candidate }, 1, commandTimestamp, maximumAge,
                    player, x, y, pcl) == -1,
                message + " does not correlate");
        }

        private static void AssertDecision(
            CapturedGateFilterDecision expected,
            int queryPlayer,
            int capturedByPlayer,
            int ownerPlayer = 3)
        {
            Assert(
                EnemyGatePathfindingPolicy.EvaluateGateAccess(
                    queryPlayer,
                    ownerPlayer,
                    capturedByPlayer,
                    ValidPlayer,
                    Allied) == expected,
                expected + " for query=" + queryPlayer + ", owner=" + ownerPlayer +
                ", captured=" + capturedByPlayer);
        }

        private static bool ValidPlayer(int player) => player >= 1 && player <= 8;

        private static bool Allied(int first, int second) =>
            first == second || (first <= 2 && second <= 2);

        private static void Assert(bool condition, string message)
        {
            assertions++;
            if (!condition)
                throw new InvalidOperationException("Assertion failed: " + message);
        }
    }
}
