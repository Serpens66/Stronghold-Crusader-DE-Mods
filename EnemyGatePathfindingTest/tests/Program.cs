using System;

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
                DiagnosticBuildingStatesAreEditorSafe();
                TopologyRejectionClassificationIsDeterministic();
                MoveCorrelationUsesNearestMatchingPredecessor();
                MoveCorrelationRejectsMismatchesAndExpiredQueries();
                MoveRoleAccountingAlwaysBalances();
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
            Assert(EnemyGatePathfindingPolicy.ShouldQueueDeferredDiagnostic(7, 7, 0),
                "Same-PCL query is deferred");
            Assert(EnemyGatePathfindingPolicy.ShouldQueueDeferredDiagnostic(7, 8, 1),
                "capturer-filter query is deferred");
            Assert(!EnemyGatePathfindingPolicy.ShouldQueueDeferredDiagnostic(7, 8, 0),
                "ordinary different-PCL query stays counter-only");
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
