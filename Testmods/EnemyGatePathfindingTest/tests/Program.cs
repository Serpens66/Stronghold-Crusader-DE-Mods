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
                ImmutableGateSnapshotIsAllianceAwareAndFailOpen();
                NativeContractIncludesDrawbridgePclAndExactFilterSite();
                CapturerHooksCoverBothNativeSitesAtomically();
                SamePclCandidatePolicyIsFailOpenAndAllianceAware();
                RectangleDistanceSupportsSpatialBridgeDiagnosis();
                NativeHookByteContractsRejectMutation();
                TopologyRejectionClassificationIsDeterministic();
                FootprintAdjacencyIgnoresBrokenEditorBounds();
                UniqueSpatialGateAssociationFailsOpenWhenAmbiguous();
                DirectionEdgesRequireBothNativeDirections();
                TileRouteNativeContractIsPinned();
                NativeRouteHotPathsRemainPrimitiveOnly();
                UnsafeGlobalMutationAndWholePclDetourAreAbsent();
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

        private static void ImmutableGateSnapshotIsAllianceAwareAndFailOpen()
        {
            ushort unrelated = unchecked((ushort)((1 << 3) | (1 << 4)));
            var records = new NativeGateAccessRecord[8];
            records[5] = new NativeGateAccessRecord(true, 7, 2, unrelated);
            var snapshot = new NativeGateAccessSnapshot(records, 0x1234);
            Assert(snapshot.Evaluate(3, 5, 7, false) ==
                    CapturedGateFilterDecision.ExcludeForeignCapture,
                "snapshot excludes an unrelated foreign capturer");
            Assert(snapshot.Evaluate(1, 5, 7, false) ==
                    CapturedGateFilterDecision.PreserveVanilla,
                "snapshot allows a player allied to the capturer");
            Assert(snapshot.Evaluate(3, 5, 7, true) ==
                    CapturedGateFilterDecision.FailOpen,
                "capture-state mismatch invalidates a stale snapshot");
            Assert(snapshot.Evaluate(3, 5, 6, false) ==
                    CapturedGateFilterDecision.FailOpen,
                "owner mismatch fails open");
            Assert(NativeGateAccessSnapshot.Empty.Evaluate(3, 5, 7, false) ==
                    CapturedGateFilterDecision.FailOpen,
                "empty snapshot fails open");

            records[5] = new NativeGateAccessRecord(true, 7, 0, unrelated);
            var recaptured = new NativeGateAccessSnapshot(records, 0x1235);
            Assert(recaptured.Evaluate(3, 5, 7, true) ==
                    CapturedGateFilterDecision.PreserveVanilla,
                "next snapshot preserves Vanilla for an uncaptured enemy gate");
        }

        private static void NativeContractIncludesDrawbridgePclAndExactFilterSite()
        {
            Assert(EnemyGatePathfindingNativeDefinition.NativeRecordStride == 0x204, "record stride");
            Assert(EnemyGatePathfindingNativeDefinition.RecordFirstPclOffset == -0x1E8, "first PCL");
            Assert(EnemyGatePathfindingNativeDefinition.RecordSecondPclOffset == -0x1E4, "second PCL");
            Assert(EnemyGatePathfindingNativeDefinition.RecordThirdPclOffset == -0x34, "drawbridge PCL");
            Assert(EnemyGatePathfindingNativeDefinition.PclGraphCapturedByFilterRva == 0xE2705,
                "PCL-graph filter RVA");
            Assert(EnemyGatePathfindingNativeDefinition.PclGraphCapturedByFilterHookLength == 20,
                "PCL-graph filter span");
            Assert(EnemyGatePathfindingNativeDefinition.PclGraphCapturedByFilterEndRva == 0xE2719,
                "PCL-graph exclusive end");
            Assert(EnemyGatePathfindingNativeDefinition.PclGraphAllowedRecordTargetRva == 0xE271B,
                "PCL-graph branch target is outside the span");
            Assert(EnemyGatePathfindingNativeDefinition.BuilderPrecheckCapturedByFilterRva == 0xE3024,
                "builder-precheck filter RVA");
            Assert(EnemyGatePathfindingNativeDefinition.BuilderPrecheckCapturedByFilterHookLength == 20,
                "builder-precheck filter span");
            Assert(EnemyGatePathfindingNativeDefinition.BuilderPrecheckCapturedByFilterEndRva == 0xE3038,
                "builder-precheck exclusive end");
            Assert(EnemyGatePathfindingNativeDefinition.BuilderPrecheckAllowedRecordTargetRva == 0xE303A,
                "builder-precheck branch target is outside the span");
        }

        private static void NativeHookByteContractsRejectMutation()
        {
            var memory = new byte[EnemyGatePathfindingNativeDefinition.BuilderPrecheckAllowedRecordTargetRva + 2];
            WriteBytes(memory, EnemyGatePathfindingNativeDefinition.CursorPclDecisionRva,
                "85 C0 48 8D 3D E3 FB FC 03 B8 01 00 00 00");
            WriteBytes(memory, EnemyGatePathfindingNativeDefinition.PclGraphPredecessorJumpRva,
                "74 16 49 63 49 F4 48 69 D1 2C 03 00 00 66 83 BC 02 D2 CE 4C 06 00 74 11 FF C3");
            WriteBytes(memory, EnemyGatePathfindingNativeDefinition.BuilderPrecheckPredecessorJumpRva,
                "74 16 49 63 49 F4 48 69 D1 2C 03 00 00 66 42 39 84 2A D2 CE 4C 06 74 0D FF C3");

            EnemyGatePathfindingNativeDefinition.ValidateNativeHookContracts(memory);
            int pclMutation = EnemyGatePathfindingNativeDefinition.PclGraphCapturedByFilterRva + 3;
            memory[pclMutation] ^= 1;
            AssertNativeContractRejected(memory, "mutated PCL-graph block fails closed");
            memory[pclMutation] ^= 1;

            int builderMutation = EnemyGatePathfindingNativeDefinition.BuilderPrecheckCapturedByFilterRva + 15;
            memory[builderMutation] ^= 1;
            AssertNativeContractRejected(memory, "mutated builder-precheck block fails closed");
        }

        private static void AssertNativeContractRejected(byte[] memory, string message)
        {
            bool rejected = false;
            try
            {
                EnemyGatePathfindingNativeDefinition.ValidateNativeHookContracts(memory);
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }
            Assert(rejected, message);
        }

        private static void CapturerHooksCoverBothNativeSitesAtomically()
        {
            string runtimeSource = File.ReadAllText(Path.Combine("src", "EnemyGatePathfindingRuntime.cs"));
            string pclGraphBody = ExtractMethodBody(
                runtimeSource, "FilterUnrelatedCapturedEnemyGatePclGraph");
            string builderBody = ExtractMethodBody(
                runtimeSource, "FilterUnrelatedCapturedEnemyGateBuilderPrecheck");
            string sharedBody = ExtractMethodBody(runtimeSource, "FilterUnrelatedCapturedEnemyGate");

            Assert(pclGraphBody.IndexOf("context, false", StringComparison.Ordinal) >= 0,
                "PCL-graph hook selects the R14 query context");
            Assert(builderBody.IndexOf("context, true", StringComparison.Ordinal) >= 0,
                "builder-precheck hook selects the RBP query context");
            Assert(sharedBody.IndexOf("registers->R14", StringComparison.Ordinal) >= 0,
                "PCL-graph query player is read from R14");
            Assert(sharedBody.IndexOf("registers->RBP", StringComparison.Ordinal) >= 0,
                "builder-precheck query player is read from RBP");
            Assert(sharedBody.IndexOf("registers->RCX", StringComparison.Ordinal) >= 0,
                "both filters read the building id from RCX");
            Assert(sharedBody.IndexOf("registers->R9", StringComparison.Ordinal) >= 0,
                "both filters read the gate record from R9");
            Assert(runtimeSource.IndexOf("RollbackAndThrow", StringComparison.Ordinal) >= 0,
                "both filters use the atomic rollback transaction");
            Assert(runtimeSource.IndexOf("pclGraphCapturedByFilterHook,", StringComparison.Ordinal) >= 0,
                "PCL-graph filter is registered");
            Assert(runtimeSource.IndexOf("builderPrecheckCapturedByFilterHook,", StringComparison.Ordinal) >= 0,
                "builder-precheck filter is registered");
            Assert(runtimeSource.IndexOf("OwnsHooks = false", StringComparison.Ordinal) >= 0,
                "process-lifetime hook ownership is explicit");
            Assert(runtimeSource.IndexOf("commitResult.IsCompleteSuccess", StringComparison.Ordinal) >= 0,
                "transaction commit result is checked");
            Assert(runtimeSource.IndexOf("new ContextHookOptions", StringComparison.Ordinal) >= 0,
                "context hook options are explicit");
            Assert(runtimeSource.IndexOf("Placement = OverwrittenInstructionPlacement.BeforeCallback",
                    StringComparison.Ordinal) >= 0,
                "displaced comparisons execute before callbacks");
            Assert(runtimeSource.IndexOf("ProbeExactHookLength", StringComparison.Ordinal) >= 0,
                "RedBird spans are probed before publication");
            Assert(runtimeSource.IndexOf("DisplacedByteCount", StringComparison.Ordinal) >= 0,
                "committed RedBird spans are checked");
            Assert(runtimeSource.IndexOf("transaction.DisableAll()", StringComparison.Ordinal) >= 0,
                "unexpected committed spans roll back before publication");
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
        }

        private static void TileRouteNativeContractIsPinned()
        {
            Assert(EnemyGatePathfindingNativeDefinition.CursorPclDecisionRva == 0x8F1C4,
                "ordinary cursor PCL decision RVA");
            Assert(EnemyGatePathfindingNativeDefinition.CursorPclDecisionHookLength == 14,
                "ordinary cursor PCL decision span");
            Assert(EnemyGatePathfindingNativeDefinition.PathDirectionGridRva == 0x51890D0,
                "native direction grid RVA");
            Assert(EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive == 320800,
                "native tile-grid capacity");
            Assert(EnemyGatePathfindingNativeDefinition.MapGridWidth == 800,
                "native tile-grid width");
        }

        private static void NativeRouteHotPathsRemainPrimitiveOnly()
        {
            string tileSource = File.ReadAllText(Path.Combine("src", "CursorGateRouteFilter.cs"));
            string runtimeSource = File.ReadAllText(Path.Combine("src", "EnemyGatePathfindingRuntime.cs"));
            string[] forbidden =
            {
                "GamePlayerManagerAPI", "GameUnitManagerAPI", "DebugLogHelper",
                "Monitor.", "lock (", "StringBuilder", "Console.",
                "new List", "new Dictionary", "new int[", "new byte[", "new string"
            };
            foreach (string method in new[] { "FilterPositiveCursorPcl", "SearchWithoutBlocked" })
            {
                string body = ExtractMethodBody(tileSource, method);
                foreach (string token in forbidden)
                    Assert(body.IndexOf(token, StringComparison.Ordinal) < 0,
                        method + " hot path excludes " + token);
            }
            foreach (string method in new[]
            {
                "FilterUnrelatedCapturedEnemyGatePclGraph",
                "FilterUnrelatedCapturedEnemyGateBuilderPrecheck",
                "FilterUnrelatedCapturedEnemyGate"
            })
            {
                string capturedBody = ExtractMethodBody(runtimeSource, method);
                foreach (string token in forbidden)
                    Assert(capturedBody.IndexOf(token, StringComparison.Ordinal) < 0,
                        method + " hot path excludes " + token);
            }
        }

        private static void UnsafeGlobalMutationAndWholePclDetourAreAbsent()
        {
            string tileSource = File.ReadAllText(Path.Combine("src", "CursorGateRouteFilter.cs"));
            string runtimeSource = File.ReadAllText(Path.Combine("src", "EnemyGatePathfindingRuntime.cs"));
            Assert(tileSource.IndexOf("ApplyOverlay", StringComparison.Ordinal) < 0,
                "global Direction-Grid overlay is absent");
            Assert(tileSource.IndexOf("RestoreOverlay", StringComparison.Ordinal) < 0,
                "global Direction-Grid restoration path is absent");
            Assert(tileSource.IndexOf("NativeDetour", StringComparison.Ordinal) < 0,
                "builder and planner detours are absent");
            Assert(tileSource.IndexOf("originalBuilder", StringComparison.Ordinal) < 0,
                "second builder run is absent without a local edge filter");
            Assert(runtimeSource.IndexOf("GetNextReachablePclDelegate", StringComparison.Ordinal) < 0,
                "whole PCL function detour delegate is absent");
            Assert(runtimeSource.IndexOf("AddDetour", StringComparison.Ordinal) < 0,
                "whole PCL function detour installation is absent");
        }

        private static void WriteBytes(byte[] destination, int offset, string hexadecimal)
        {
            string[] bytes = hexadecimal.Split(' ');
            for (int index = 0; index < bytes.Length; index++)
                destination[offset + index] = Convert.ToByte(bytes[index], 16);
        }

        private static string ExtractMethodBody(string source, string methodName)
        {
            string returnType = methodName == "SearchWithoutBlocked" ? "private int " : "private void ";
            int name = source.IndexOf(returnType + methodName + "(", StringComparison.Ordinal);
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
