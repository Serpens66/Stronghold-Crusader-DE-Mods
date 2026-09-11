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
                SnapshotDecisionDiagnosticsCoverEveryAccessClass();
                SnapshotMetadataCountsTrackedPolicy();
                AccessPolicyEqualityIgnoresRawScanOnlyChanges();
                CaptureTransitionCoverageIsDeterministic();
                CapturerComparisonAndFlagRestorationAreExact();
                NativeContractIncludesDrawbridgePclAndExactFilterSite();
                CapturerHooksCoverBothNativeSitesAtomically();
                SnapshotRefreshPathsAreSeparatedAndBounded();
                RoutePolicyFingerprintIgnoresDynamicTileState();
                DiagnosticLifecycleAndSamplesAreBounded();
                AcceptanceVerdictsAreMachineReadable();
                CausalCursorClassificationIsStrict();
                StableDiagnosticBaselinesSurviveFailOpenClears();
                CompactTopologyAndInvariantTimingAreEnforced();
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
            ushort ownerRelated = unchecked((ushort)(1 << 7));
            ushort capturerRelated = unchecked((ushort)((1 << 1) | (1 << 2)));
            ushort unrelated = unchecked((ushort)((1 << 3) | (1 << 4)));
            var records = new NativeGateAccessRecord[8];
            records[5] = new NativeGateAccessRecord(
                true, 7, 2, ownerRelated, capturerRelated, unrelated);
            var snapshot = new NativeGateAccessSnapshot(records, 0x1234);
            Assert(snapshot.Evaluate(3, 5, 7, 2) ==
                    NativeGateSnapshotDecision.ExcludeForeignCapture,
                "snapshot excludes an unrelated foreign capturer");
            Assert(snapshot.Evaluate(1, 5, 7, 2) ==
                    NativeGateSnapshotDecision.PreserveCapturerAlly,
                "snapshot allows a player allied to the capturer");
            Assert(snapshot.Evaluate(3, 5, 7, 0) ==
                    NativeGateSnapshotDecision.CaptureMismatch,
                "capture-state mismatch invalidates a stale snapshot");
            Assert(snapshot.Evaluate(3, 5, 6, 2) ==
                    NativeGateSnapshotDecision.OwnerMismatch,
                "owner mismatch fails open");
            Assert(snapshot.Evaluate(0, 5, 7, 2) ==
                    NativeGateSnapshotDecision.InvalidQueryPlayer,
                "invalid query player is classified separately");
            Assert(NativeGateAccessSnapshot.Empty.Evaluate(3, 5, 7, 2) ==
                    NativeGateSnapshotDecision.UntrackedConnection,
                "empty snapshot fails open");

            records[5] = new NativeGateAccessRecord(
                true, 7, 0, ownerRelated, 0, unrelated);
            var recaptured = new NativeGateAccessSnapshot(records, 0x1235);
            Assert(recaptured.Evaluate(3, 5, 7, 0) ==
                    NativeGateSnapshotDecision.PreserveUncaptured,
                "next snapshot preserves Vanilla for an uncaptured enemy gate");
        }

        private static void SnapshotDecisionDiagnosticsCoverEveryAccessClass()
        {
            ushort ownerRelated = unchecked((ushort)((1 << 1) | (1 << 2)));
            ushort capturerRelated = unchecked((ushort)((1 << 3) | (1 << 4)));
            ushort unrelated = unchecked((ushort)((1 << 5) | (1 << 6)));
            var records = new NativeGateAccessRecord[3];
            records[2] = new NativeGateAccessRecord(
                true, 1, 3, ownerRelated, capturerRelated, unrelated);
            var snapshot = new NativeGateAccessSnapshot(records, 7);
            Assert(snapshot.Evaluate(1, 2, 1, 3) == NativeGateSnapshotDecision.PreserveOwner,
                "owner is classified separately");
            Assert(snapshot.Evaluate(2, 2, 1, 3) == NativeGateSnapshotDecision.PreserveOwnerAlly,
                "owner ally is classified separately");
            Assert(snapshot.Evaluate(3, 2, 1, 3) == NativeGateSnapshotDecision.PreserveCapturer,
                "capturer is classified separately");
            Assert(snapshot.Evaluate(4, 2, 1, 3) == NativeGateSnapshotDecision.PreserveCapturerAlly,
                "capturer ally is classified separately");
            Assert(snapshot.Evaluate(5, 2, 1, 3) == NativeGateSnapshotDecision.ExcludeForeignCapture,
                "foreign capturer is classified separately");
        }

        private static void SnapshotMetadataCountsTrackedPolicy()
        {
            var records = new NativeGateAccessRecord[4];
            records[1] = new NativeGateAccessRecord(true, 1, 0, 0x0006, 0, 0x0018);
            records[3] = new NativeGateAccessRecord(true, 3, 4, 0x0008, 0x0010, 0x0060);
            var snapshot = new NativeGateAccessSnapshot(records, 9);
            Assert(snapshot.TrackedRecords == 2, "snapshot counts tracked records");
            Assert(snapshot.CapturedRecords == 1, "snapshot counts captured records");
            Assert(snapshot.UncapturedRecords == 1, "snapshot counts uncaptured records");
            Assert(snapshot.BlockedPlayerGatePairs == 4,
                "snapshot counts blocked player/gate pairs");
        }

        private static void AccessPolicyEqualityIgnoresRawScanOnlyChanges()
        {
            var firstRecords = new NativeGateAccessRecord[4];
            firstRecords[2] = new NativeGateAccessRecord(true, 3, 0, 0x0008, 0, 0x0006);
            var sameRecordsWithDifferentCapacity = new NativeGateAccessRecord[8];
            sameRecordsWithDifferentCapacity[2] = firstRecords[2];
            var first = new NativeGateAccessSnapshot(firstRecords, 0x1000);
            var rawOnlyChange = new NativeGateAccessSnapshot(
                sameRecordsWithDifferentCapacity, 0x2000);
            Assert(first.RawFingerprint != rawOnlyChange.RawFingerprint,
                "raw scan fingerprints retain diagnostic changes");
            Assert(first.PolicyEquals(rawOnlyChange),
                "raw fingerprint and trailing capacity do not change access policy");
            Assert(first.TopologyFingerprint == rawOnlyChange.TopologyFingerprint,
                "semantic access fingerprint remains stable across raw-only changes");

            sameRecordsWithDifferentCapacity[2] = new NativeGateAccessRecord(
                true, 3, 4, 0x0008, 0x0010, 0x0006);
            var captured = new NativeGateAccessSnapshot(sameRecordsWithDifferentCapacity, 0x3000);
            Assert(!first.PolicyEquals(captured), "capture changes access policy");
            Assert(first.TopologyFingerprint != captured.TopologyFingerprint,
                "capture changes semantic access fingerprint");

            var ownerChangedRecords = (NativeGateAccessRecord[])firstRecords.Clone();
            ownerChangedRecords[2] = new NativeGateAccessRecord(true, 4, 0, 0x0010, 0, 0x0006);
            Assert(!first.PolicyEquals(new NativeGateAccessSnapshot(ownerChangedRecords, 0x4000)),
                "owner and owner-alliance mask changes access policy");
            var maskChangedRecords = (NativeGateAccessRecord[])firstRecords.Clone();
            maskChangedRecords[2] = new NativeGateAccessRecord(true, 3, 0, 0x0008, 0, 0x0020);
            Assert(!first.PolicyEquals(new NativeGateAccessSnapshot(maskChangedRecords, 0x5000)),
                "blocking-mask changes access policy");
            var removedRecords = (NativeGateAccessRecord[])firstRecords.Clone();
            removedRecords[2] = default;
            Assert(!first.PolicyEquals(new NativeGateAccessSnapshot(removedRecords, 0x6000)),
                "record validity changes access policy");
        }

        private static void CaptureTransitionCoverageIsDeterministic()
        {
            Assert(EnemyGatePathfindingPolicy.ClassifyCaptureTransition(true, 0, true, 3) ==
                    CaptureTransitionKind.Captured,
                "uncaptured to captured is a capture transition");
            Assert(EnemyGatePathfindingPolicy.ClassifyCaptureTransition(true, 3, true, 4) ==
                    CaptureTransitionKind.Recaptured,
                "capturer replacement is a recapture transition");
            Assert(EnemyGatePathfindingPolicy.ClassifyCaptureTransition(true, 3, true, 0) ==
                    CaptureTransitionKind.Recaptured,
                "capture removal is retained as a recapture transition");
            Assert(EnemyGatePathfindingPolicy.ClassifyCaptureTransition(false, 0, true, 3) ==
                    CaptureTransitionKind.None,
                "initial snapshot population is not a runtime capture transition");
            Assert(EnemyGatePathfindingPolicy.ClassifyCaptureTransition(true, 3, false, 0) ==
                    CaptureTransitionKind.None,
                "record removal is not misclassified as recapture");
        }

        private static void CapturerComparisonAndFlagRestorationAreExact()
        {
            Assert(EnemyGatePathfindingNativeDefinition.PclGraphCaptureCompareIsEqual(0),
                "PCL-graph CMP is equal only for an uncaptured record");
            Assert(!EnemyGatePathfindingNativeDefinition.PclGraphCaptureCompareIsEqual(3),
                "PCL-graph CMP is unequal for a captured record");
            Assert(EnemyGatePathfindingNativeDefinition.BuilderPrecheckCaptureCompareIsEqual(3, 3),
                "builder CMP uses the actual AX operand");
            Assert(!EnemyGatePathfindingNativeDefinition.BuilderPrecheckCaptureCompareIsEqual(3, 0),
                "builder CMP detects unequal capture and AX values");

            ulong flagsWithoutZero = 0x202UL & ~EnemyGatePathfindingPolicy.ZeroFlagMask;
            ulong flagsWithZero = flagsWithoutZero | EnemyGatePathfindingPolicy.ZeroFlagMask;
            Assert((EnemyGatePathfindingPolicy.SetZeroFlag(flagsWithoutZero, true) &
                    EnemyGatePathfindingPolicy.ZeroFlagMask) != 0,
                "reconstructed equality restores ZF");
            Assert((EnemyGatePathfindingPolicy.SetZeroFlag(flagsWithZero, false) &
                    EnemyGatePathfindingPolicy.ZeroFlagMask) == 0,
                "reconstructed inequality clears stale callback ZF");
            Assert((EnemyGatePathfindingPolicy.SetZeroFlag(flagsWithZero, false) &
                    ~EnemyGatePathfindingPolicy.ZeroFlagMask) ==
                    (flagsWithZero & ~EnemyGatePathfindingPolicy.ZeroFlagMask),
                "ZF restoration preserves every unrelated flag bit");

            string runtimeSource = File.ReadAllText(Path.Combine("src", "EnemyGatePathfindingRuntime.cs"));
            string body = ExtractMethodBody(runtimeSource, "FilterUnrelatedCapturedEnemyGate");
            Assert(body.IndexOf("CapturedByPlayerTableDisplacement", StringComparison.Ordinal) >= 0,
                "callback rereads the exact native capture-table operand");
            Assert(body.IndexOf("(registers->Rflags &", StringComparison.Ordinal) < 0,
                "callback never treats RedBird's saved flags as the displaced CMP result");
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
            var memory = new byte[EnemyGatePathfindingNativeDefinition.PathBuilderRva + 64];
            WriteBytes(memory, EnemyGatePathfindingNativeDefinition.CursorPclDecisionRva,
                "85 C0 48 8D 3D E3 FB FC 03 B8 01 00 00 00");
            WriteBytes(memory, EnemyGatePathfindingNativeDefinition.PclGraphPredecessorJumpRva,
                "74 16 49 63 49 F4 48 69 D1 2C 03 00 00 66 83 BC 02 D2 CE 4C 06 00 74 11 FF C3");
            WriteBytes(memory, EnemyGatePathfindingNativeDefinition.BuilderPrecheckPredecessorJumpRva,
                "74 16 49 63 49 F4 48 69 D1 2C 03 00 00 66 42 39 84 2A D2 CE 4C 06 74 0D FF C3");
            WriteBytes(memory, EnemyGatePathfindingNativeDefinition.PathBuilderRva,
                "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 48 83 EC 40 48 63 41 0C 48 8B D9 41 8B F0 44 8B D2");

            EnemyGatePathfindingNativeDefinition.ValidateNativeHookContracts(memory);
            EnemyGatePathfindingNativeDefinition.ValidateSamePclBuilderContract(memory);
            int pclMutation = EnemyGatePathfindingNativeDefinition.PclGraphCapturedByFilterRva + 3;
            memory[pclMutation] ^= 1;
            AssertNativeContractRejected(memory, "mutated PCL-graph block fails closed");
            memory[pclMutation] ^= 1;

            int builderMutation = EnemyGatePathfindingNativeDefinition.BuilderPrecheckCapturedByFilterRva + 15;
            memory[builderMutation] ^= 1;
            AssertNativeContractRejected(memory, "mutated builder-precheck block fails closed");
            memory[builderMutation] ^= 1;
            int pathBuilderMutation = EnemyGatePathfindingNativeDefinition.PathBuilderRva + 24;
            memory[pathBuilderMutation] ^= 1;
            bool builderRejected = false;
            try
            {
                EnemyGatePathfindingNativeDefinition.ValidateSamePclBuilderContract(memory);
            }
            catch (InvalidOperationException)
            {
                builderRejected = true;
            }
            Assert(builderRejected, "mutated F4930 function entry fails closed");
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
            Assert(sharedBody.IndexOf("originalZeroKnown", StringComparison.Ordinal) >= 0 &&
                    sharedBody.IndexOf("SetZeroFlag", StringComparison.Ordinal) >= 0,
                "callback restores reconstructed Vanilla ZF on policy failures");
            Assert(runtimeSource.IndexOf("NativeGateSnapshotDecision.RecordIdMismatch",
                    StringComparison.Ordinal) >= 0,
                "record-ID mismatch has a dedicated diagnostic outcome");
            Assert(runtimeSource.IndexOf("CapturerSample[]", StringComparison.Ordinal) >= 0 &&
                    runtimeSource.IndexOf("CompareExchange(ref sample.State", StringComparison.Ordinal) >= 0,
                "capturer samples are preallocated and atomically published");
            Assert(sharedBody.IndexOf("RecordFirstPclOffset", StringComparison.Ordinal) >= 0 &&
                    sharedBody.IndexOf("RecordSecondPclOffset", StringComparison.Ordinal) >= 0 &&
                    sharedBody.IndexOf("RecordThirdPclOffset", StringComparison.Ordinal) >= 0 &&
                    runtimeSource.IndexOf("portalPcls=", StringComparison.Ordinal) >= 0,
                "first samples identify all native portal components");
        }

        private static void RoutePolicyFingerprintIgnoresDynamicTileState()
        {
            string source = File.ReadAllText(
                Path.Combine("src", "GateTopologySnapshotProvider.cs"));
            string body = ExtractMethodBody(source, "MixRoutePolicy");
            foreach (string dynamicField in new[]
            {
                "IsOpen", "EntryPcl", "ExitPcl", "GatePath", "Flags", "Walkable"
            })
                Assert(body.IndexOf(dynamicField, StringComparison.Ordinal) < 0,
                    "route policy fingerprint ignores dynamic field " + dynamicField);
            foreach (string policyField in new[]
            {
                "GateId", "GateGlobal", "BridgeId", "BridgeGlobal",
                "UnrelatedByPlayer", "tile.TileId", "tile.Footprint"
            })
                Assert(body.IndexOf(policyField, StringComparison.Ordinal) >= 0,
                    "route policy fingerprint includes " + policyField);
        }

        private static void DiagnosticLifecycleAndSamplesAreBounded()
        {
            string plugin = File.ReadAllText(
                Path.Combine("src", "EnemyGatePathfindingTestPlugin.cs"));
            string runtime = File.ReadAllText(
                Path.Combine("src", "EnemyGatePathfindingRuntime.cs"));
            string cursor = File.ReadAllText(
                Path.Combine("src", "CursorGateRouteFilter.cs"));
            Assert(plugin.IndexOf("args.Phase == EventHookPhase.Pre", StringComparison.Ordinal) >= 0,
                "map summary uses reliable unload Pre phase");
            Assert(runtime.IndexOf("implicit restart before OnStartMap(Post)",
                    StringComparison.Ordinal) >= 0,
                "new map defensively finalizes a missed unload");
            Assert(runtime.IndexOf("DiagnosticInterval = Stopwatch.Frequency * 10L",
                    StringComparison.Ordinal) >= 0,
                "one central ten-second diagnostic cadence is used");
            Assert(cursor.IndexOf("CursorSample[]", StringComparison.Ordinal) >= 0 &&
                    cursor.IndexOf("CompareExchange(ref sample.State", StringComparison.Ordinal) >= 0,
                "cursor samples are bounded and atomically published");
            Assert(runtime.IndexOf(":NOT_OBSERVED", StringComparison.Ordinal) >= 0,
                "uncovered capturer cases are explicit");
            Assert(cursor.IndexOf("players.Append(\"none:NOT_OBSERVED\")",
                    StringComparison.Ordinal) >= 0,
                "uncovered cursor player activity is explicit");
        }

        private static void AcceptanceVerdictsAreMachineReadable()
        {
            Assert(EnemyGatePathfindingPolicy.ObservationVerdict(1) == DiagnosticVerdict.PASS,
                "observed acceptance case passes");
            Assert(EnemyGatePathfindingPolicy.ObservationVerdict(0) ==
                    DiagnosticVerdict.NOT_OBSERVED,
                "missing acceptance case is explicit");
            Assert(EnemyGatePathfindingPolicy.IntegrityVerdict(true, false) ==
                    DiagnosticVerdict.PASS,
                "observed error-free runtime passes");
            Assert(EnemyGatePathfindingPolicy.IntegrityVerdict(true, true) ==
                    DiagnosticVerdict.FAIL,
                "runtime error dominates observed activity");
            Assert(EnemyGatePathfindingPolicy.IntegrityVerdict(false, false) ==
                    DiagnosticVerdict.NOT_OBSERVED,
                "unexercised integrity remains unobserved");

            string runtime = File.ReadAllText(
                Path.Combine("src", "EnemyGatePathfindingRuntime.cs"));
            Assert(runtime.IndexOf("Enemy-gate acceptance verdict:", StringComparison.Ordinal) >= 0,
                "final machine-readable verdict is logged");
            Assert(runtime.IndexOf("ownerAtHook={DiagnosticVerdict.NOT_APPLICABLE}",
                    StringComparison.Ordinal) >= 0,
                "upstream owner short-circuit is not reported as missing coverage");
            Assert(runtime.IndexOf("untrackedTransitionCalls=", StringComparison.Ordinal) >= 0,
                "transient untracked calls remain separately visible");
            Assert(runtime.IndexOf("cursorPolicyBlocked=", StringComparison.Ordinal) >= 0 &&
                    runtime.IndexOf("cursorForcedDetour=", StringComparison.Ordinal) >= 0 &&
                    runtime.IndexOf("samePclHookExecution=", StringComparison.Ordinal) >= 0,
                "causal cursor and active Same-PCL verdicts are explicit");
        }

        private static void CausalCursorClassificationIsStrict()
        {
            Assert(EnemyGatePathfindingPolicy.ClassifyCausalRoute(
                    false, -1, false, -1, false, 0) == CausalRouteDecision.VanillaNoRoute,
                "an unreproduced positive Vanilla result fails open");
            Assert(EnemyGatePathfindingPolicy.ClassifyCausalRoute(
                    true, 5, false, -1, false, 0) == CausalRouteDecision.VanillaNoRoute,
                "filtered no-route without a policy encounter fails open");
            Assert(EnemyGatePathfindingPolicy.ClassifyCausalRoute(
                    true, 5, false, -1, false, 1) == CausalRouteDecision.PolicyBlocked,
                "a reachable baseline cut by policy is causally blocked");
            Assert(EnemyGatePathfindingPolicy.ClassifyCausalRoute(
                    true, 5, false, -1, true, 1) == CausalRouteDecision.TargetBlocked,
                "a blocked target is classified separately");
            Assert(EnemyGatePathfindingPolicy.ClassifyCausalRoute(
                    true, 5, true, 8, false, 2) == CausalRouteDecision.ForcedDetour,
                "a longer filtered shortest path proves an actual detour");
            Assert(EnemyGatePathfindingPolicy.ClassifyCausalRoute(
                    true, 5, true, 5, false, 2) == CausalRouteDecision.Reachable,
                "an equally short filtered path is not overstated as a forced detour");
        }

        private static void StableDiagnosticBaselinesSurviveFailOpenClears()
        {
            string source = File.ReadAllText(
                Path.Combine("src", "GateTopologySnapshotProvider.cs"));
            string tick = ExtractMethodBody(source, "OnGameTick");
            Assert(source.IndexOf("lastStableAccessSnapshot", StringComparison.Ordinal) >= 0 &&
                    source.IndexOf("lastStableTopologySnapshot", StringComparison.Ordinal) >= 0,
                "diagnostic baselines are separate from live fail-open snapshots");
            Assert(tick.IndexOf("lastStableAccessSnapshot = NativeGateAccessSnapshot.Empty",
                    StringComparison.Ordinal) < 0 &&
                    tick.IndexOf("lastStableTopologySnapshot = TopologySnapshot.Empty",
                    StringComparison.Ordinal) < 0,
                "tick-side fail-open clear preserves stable diagnostic baselines");
            string access = ExtractMethodBody(source, "RefreshGateAccess");
            Assert(access.IndexOf("previous.PolicyEquals(rebuilt)", StringComparison.Ordinal) >= 0,
                "access publication uses semantic snapshot equality");
            Assert(access.IndexOf("accessRepublishes", StringComparison.Ordinal) >= 0,
                "equivalent policy is republished after a fail-open window");
            Assert(access.IndexOf("suppressedRawAccessChanges", StringComparison.Ordinal) >= 0,
                "raw-only changes are counted without policy churn");
        }

        private static void CompactTopologyAndInvariantTimingAreEnforced()
        {
            string topology = File.ReadAllText(
                Path.Combine("src", "GateTopologySnapshotProvider.cs"));
            string cursor = File.ReadAllText(
                Path.Combine("src", "CursorGateRouteFilter.cs"));
            Assert(topology.IndexOf("footprintTiles=[", StringComparison.Ordinal) < 0,
                "topology logs no longer dump every footprint tile");
            Assert(topology.IndexOf("/tileRange=", StringComparison.Ordinal) >= 0 &&
                    topology.IndexOf("/hash=0x", StringComparison.Ordinal) >= 0,
                "compact footprint range and hash are logged");
            Assert(topology.IndexOf("AppendTopologyDetail(detail, gateInfo.Format())",
                    StringComparison.Ordinal) < 0,
                "initial accepted gate details are not duplicated");
            Assert(cursor.IndexOf("CultureInfo.InvariantCulture", StringComparison.Ordinal) >= 0,
                "cursor timing uses invariant decimal formatting");
            Assert(cursor.IndexOf("forcedDetour", StringComparison.Ordinal) >= 0 &&
                    cursor.IndexOf("vanillaNoRoute", StringComparison.Ordinal) >= 0,
                "causal detours are distinguished from baseline-model misses");
        }

        private static void SnapshotRefreshPathsAreSeparatedAndBounded()
        {
            string source = File.ReadAllText(
                Path.Combine("src", "GateTopologySnapshotProvider.cs"));
            int accessCall = source.IndexOf("RefreshGateAccess();", StringComparison.Ordinal);
            int topologyCall = source.IndexOf("RefreshTopologyIfDue(now);", StringComparison.Ordinal);
            Assert(accessCall >= 0,
                "per-frame deferred work refreshes the cheap access fingerprint");
            Assert(topologyCall > accessCall,
                "deferred work retains separately throttled topology rebuilding");
            Assert(source.IndexOf("TopologySafetyInterval = Math.Max(1, Stopwatch.Frequency)",
                    StringComparison.Ordinal) >= 0,
                "expensive topology safety rebuild is capped at one per second");
            string access = ExtractMethodBody(source, "RefreshGateAccess");
            Assert(access.IndexOf("ComputeGateAccessFingerprint", StringComparison.Ordinal) >= 0,
                "access refresh probes a fingerprint before publishing");
            Assert(access.IndexOf("fingerprint == lastAccessFingerprint", StringComparison.Ordinal) >= 0,
                "unchanged access state avoids snapshot allocation");
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
            Assert(EnemyGatePathfindingNativeDefinition.PathBuilderRva == 0xF4930,
                "central tile builder function entry RVA");
            Assert(EnemyGatePathfindingNativeDefinition.MaximumRouteEdges == 2000,
                "two native direction nibbles fit each unit-buffer byte");
            Assert(EnemyGatePathfindingNativeDefinition.CapturedByPlayerTableDisplacement == 0x64CCED2,
                "native capture-table displacement");
        }

        private static void NativeRouteHotPathsRemainPrimitiveOnly()
        {
            string tileSource = File.ReadAllText(Path.Combine("src", "CursorGateRouteFilter.cs"));
            string runtimeSource = File.ReadAllText(Path.Combine("src", "EnemyGatePathfindingRuntime.cs"));
            string samePclSource = File.ReadAllText(Path.Combine("src", "SamePclGateRouteRuntime.cs"));
            string[] forbidden =
            {
                "GamePlayerManagerAPI", "GameUnitManagerAPI", "DebugLogHelper",
                "Monitor.", "lock (", "StringBuilder", "Console.",
                "new List", "new Dictionary", "new int[", "new byte[", "new string"
            };
            foreach (string method in new[] { "FilterPositiveCursorPcl", "SearchCausally", "SearchCore" })
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
            string builderBody = ExtractMethodBody(
                samePclSource, "BuildPathWithEnemyGatePolicy");
            foreach (string token in new[]
            {
                "GameUnitManagerAPI", "GamePlayerManagerAPI", "DebugLogHelper",
                "Monitor.", "lock (", "StringBuilder", "Console."
            })
                Assert(builderBody.IndexOf(token, StringComparison.Ordinal) < 0,
                    "active builder callback excludes " + token);
        }

        private static void UnsafeGlobalMutationAndWholePclDetourAreAbsent()
        {
            string tileSource = File.ReadAllText(Path.Combine("src", "CursorGateRouteFilter.cs"));
            string runtimeSource = File.ReadAllText(Path.Combine("src", "EnemyGatePathfindingRuntime.cs"));
            string samePclSource = File.ReadAllText(Path.Combine("src", "SamePclGateRouteRuntime.cs"));
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
            Assert(samePclSource.IndexOf("PathBuilderRva =", StringComparison.Ordinal) < 0 &&
                    samePclSource.IndexOf("transaction.AddDetour", StringComparison.Ordinal) >= 0,
                "Same-PCL uses the separately validated F4930 function detour");
            Assert(samePclSource.IndexOf("directionGrid[from]", StringComparison.Ordinal) >= 0 &&
                    samePclSource.IndexOf("directionGrid[from] =", StringComparison.Ordinal) < 0 &&
                    samePclSource.IndexOf("directionGrid[to] =", StringComparison.Ordinal) < 0,
                "Same-PCL reads the native direction grid without an overlay writer");
            Assert(samePclSource.IndexOf("existingHookOwner", StringComparison.Ordinal) >= 0 &&
                    samePclSource.IndexOf("BugfixesAndQoL_Serp", StringComparison.Ordinal) >= 0,
                "overlapping builder ownership is explicitly suppressed");
        }

        private static void WriteBytes(byte[] destination, int offset, string hexadecimal)
        {
            string[] bytes = hexadecimal.Split(' ');
            for (int index = 0; index < bytes.Length; index++)
                destination[offset + index] = Convert.ToByte(bytes[index], 16);
        }

        private static string ExtractMethodBody(string source, string methodName)
        {
            int name = source.LastIndexOf(methodName + "(", StringComparison.Ordinal);
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
