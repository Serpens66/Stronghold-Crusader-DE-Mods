using System;
using System.IO;
using System.Runtime.InteropServices;
using Iced.Intel;
using RedBird.X64.Hooks;

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
                SnapshotGateIdentityRequiresBothIds();
                AccessPolicyEqualityIgnoresRawScanOnlyChanges();
                CaptureTransitionCoverageIsDeterministic();
                CapturerComparisonAndFlagRestorationAreExact();
                NativeContractIncludesDrawbridgePclAndExactFilterSite();
                CapturerHooksCoverBothNativeSitesAtomically();
                SnapshotRefreshPathsAreSeparatedAndBounded();
                RoutePolicyFingerprintIgnoresDynamicTileState();
                DiagnosticLifecycleAndSamplesAreBounded();
                AcceptanceVerdictsAreMachineReadable();
                StableDiagnosticBaselinesSurviveFailOpenClears();
                CompactTopologyAndInvariantTimingAreEnforced();
                SamePclCandidatePolicyIsFailOpenAndAllianceAware();
                RectangleDistanceSupportsSpatialBridgeDiagnosis();
                NativeHookByteContractsRejectMutation();
                VanillaDirectionFilterContractsAreAtomic();
                DirectionAdapterTileRegistersMatchNativeDataFlow();
                CrashDumpRegisterRegressionsFailOpen();
                DirectionAdaptersActuallyAssembleAndDecode();
                BaselinePlayerScopesUseNativeArguments();
                DirectCursorCallsiteContractIsExact();
                NormalCursorPreviewContractIsExact();
                CursorPreviewDecisionIsCausalAndStable();
                NativeSnapshotPoolAcquisitionIsSynchronized();
                PassageAxisEvidenceIsDeterministic();
                TopologyRejectionClassificationIsDeterministic();
                FootprintAdjacencyIgnoresBrokenEditorBounds();
                UniqueSpatialGateAssociationFailsOpenWhenAmbiguous();
                DirectionEdgesRequireBothNativeDirections();
                DirectionMaskBlocksOnlyTheGatePassage();
                GatehouseUsesBothOuterBoundaries();
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
            Assert(plugin.IndexOf("args.Phase == EventHookPhase.Pre", StringComparison.Ordinal) >= 0,
                "map summary uses reliable unload Pre phase");
            Assert(runtime.IndexOf("implicit restart before OnStartMap(Post)",
                    StringComparison.Ordinal) >= 0,
                "new map defensively finalizes a missed unload");
            Assert(runtime.IndexOf("DiagnosticInterval = Stopwatch.Frequency * 10L",
                    StringComparison.Ordinal) >= 0,
                "one central ten-second diagnostic cadence is used");
            Assert(runtime.IndexOf(":NOT_OBSERVED", StringComparison.Ordinal) >= 0,
                "uncovered capturer cases are explicit");
            Assert(runtime.IndexOf("implicit editor map-size probe", StringComparison.Ordinal) >= 0,
                "editor maps start the central diagnostic epoch without a cursor callback");
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
            Assert(runtime.IndexOf("nativeCursorScope=", StringComparison.Ordinal) >= 0 &&
                    runtime.IndexOf("nativeCursorEdgesFiltered=", StringComparison.Ordinal) >= 0 &&
                    runtime.IndexOf("samePclHookExecution=", StringComparison.Ordinal) >= 0,
                "native cursor scope and active Same-PCL verdicts are explicit");
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
            Assert(topology.IndexOf("footprintTiles=[", StringComparison.Ordinal) < 0,
                "topology logs no longer dump every footprint tile");
            Assert(topology.IndexOf("/tileRange=", StringComparison.Ordinal) >= 0 &&
                    topology.IndexOf("/hash=0x", StringComparison.Ordinal) >= 0,
                "compact footprint range and hash are logged");
            Assert(topology.IndexOf("AppendTopologyDetail(detail, gateInfo.Format())",
                    StringComparison.Ordinal) < 0,
                "initial accepted gate details are not duplicated");
            Assert(!File.Exists(Path.Combine("src", "CursorGateRouteFilter.cs")),
                "the managed cursor BFS implementation is removed");
        }

        private static void SnapshotRefreshPathsAreSeparatedAndBounded()
        {
            string source = File.ReadAllText(
                Path.Combine("src", "GateTopologySnapshotProvider.cs"));
            int accessCall = source.IndexOf("RefreshGateAccessIfDue(now);", StringComparison.Ordinal);
            int topologyCall = source.IndexOf("RefreshTopologyIfDue(now);", StringComparison.Ordinal);
            Assert(accessCall >= 0,
                "deferred work uses the bounded access refresh gate");
            Assert(topologyCall > accessCall,
                "deferred work retains separately throttled topology rebuilding");
            Assert(source.IndexOf("TopologySafetyInterval = Math.Max(1, Stopwatch.Frequency)",
                    StringComparison.Ordinal) >= 0,
                "expensive topology safety rebuild is capped at one per second");
            Assert(source.IndexOf("AccessSafetyInterval = Math.Max(1, Stopwatch.Frequency)",
                    StringComparison.Ordinal) >= 0,
                "access safety scans are capped at one per second");
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
            Assert(EnemyGatePathfindingNativeDefinition.DirectCursorSearchBlockRva == 0x8F251 &&
                    EnemyGatePathfindingNativeDefinition.DirectCursorSearchBlockLength == 29,
                "direct cursor DB650 call block and span");
            Assert(EnemyGatePathfindingNativeDefinition.DirectCursorSearchCallRva == 0x8F269 &&
                    EnemyGatePathfindingNativeDefinition.DirectCursorSearchReturnRva == 0x8F26E &&
                    EnemyGatePathfindingNativeDefinition.DirectTileSearchRva == 0xDB650,
                "direct cursor call, target and return RVAs");
            Assert(EnemyGatePathfindingNativeDefinition.CursorPclDecisionRva == 0x8F1BF &&
                    EnemyGatePathfindingNativeDefinition.CursorPclDecisionLength == 14 &&
                    EnemyGatePathfindingNativeDefinition.CursorPclDecisionReturnRva == 0x8F1CD &&
                    EnemyGatePathfindingNativeDefinition.CursorPclDecisionConsumerRva == 0x8F1D2,
                "normal cursor PCL decision span and consumer RVAs");
            Assert(EnemyGatePathfindingNativeDefinition.CursorManagerRva == 0x3A11DC0 &&
                    EnemyGatePathfindingNativeDefinition.CursorMouseTileXRva == 0x3A11E2C &&
                    EnemyGatePathfindingNativeDefinition.CursorMouseTileYRva == 0x3A11E30 &&
                    EnemyGatePathfindingNativeDefinition.CursorMouseTileIdRva == 0x3A11E38 &&
                    EnemyGatePathfindingNativeDefinition.TileRowStartRva == 0x402FF2C,
                "cursor coordinates and row-start table RVAs");
            Assert(EnemyGatePathfindingNativeDefinition.NativePathManagerRva == 0x60AD660 &&
                    EnemyGatePathfindingNativeDefinition.DirectCursorSearchNodeLimit == 400000,
                "Vanilla DB650 manager and node limit");
            Assert(EnemyGatePathfindingNativeDefinition.PathDirectionGridRva == 0x51890D0,
                "native direction grid RVA");
            Assert(EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive == 320800,
                "native tile-grid capacity");
            Assert(EnemyGatePathfindingNativeDefinition.MapGridWidth == 800,
                "native tile-grid width");
            Assert(EnemyGatePathfindingNativeDefinition.PathBuilderRva == 0xF4930,
                "central tile builder function entry RVA");
            Assert(EnemyGatePathfindingNativeDefinition.CapturedByPlayerTableDisplacement == 0x64CCED2,
                "native capture-table displacement");
        }

        private static void NativeRouteHotPathsRemainPrimitiveOnly()
        {
            string runtimeSource = File.ReadAllText(Path.Combine("src", "EnemyGatePathfindingRuntime.cs"));
            string samePclSource = File.ReadAllText(Path.Combine("src", "SamePclGateRouteRuntime.cs"));
            string emitterSource = File.ReadAllText(Path.Combine("src", "DirectionFilterAdapterEmitter.cs"));
            string[] forbidden =
            {
                "GamePlayerManagerAPI", "GameUnitManagerAPI", "DebugLogHelper",
                "Monitor.", "lock (", "StringBuilder", "Console.",
                "new List", "new Dictionary", "new int[", "new byte[", "new string"
            };
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
            string builderBody = ExtractMethodBody(emitterSource, "Emit");
            foreach (string token in new[]
            {
                "GameUnitManagerAPI", "GamePlayerManagerAPI", "DebugLogHelper",
                "Monitor.", "lock (", "StringBuilder", "Console."
            })
                Assert(builderBody.IndexOf(token, StringComparison.Ordinal) < 0,
                    "native direction adapter excludes " + token);
            Assert(samePclSource.IndexOf("managedReplacementSearches=0", StringComparison.Ordinal) >= 0 &&
                    samePclSource.IndexOf("managedCursorSearches=0", StringComparison.Ordinal) >= 0 &&
                    samePclSource.IndexOf("GateGridRouteSearch", StringComparison.Ordinal) < 0 &&
                    !File.Exists(Path.Combine("src", "CursorGateRouteFilter.cs")),
                "all managed whole-map and cursor searches are absent");
            foreach (string wrapper in new[]
            {
                "FilterBuilder", "FilterAttack", "FilterBuilding", "FilterConsumer",
                "FilterAlternateConsumer", "FilterCandidateSearch", "FilterCursor",
                "FilterDirectCursorSearch", "FilterCursorPclDecision"
            })
            {
                string body = ExtractMethodBody(samePclSource, wrapper);
                Assert(body.IndexOf("=>", StringComparison.Ordinal) < 0 &&
                        body.IndexOf("new ", StringComparison.Ordinal) < 0,
                    wrapper + " creates no closure or per-query object");
            }
        }

        private static void UnsafeGlobalMutationAndWholePclDetourAreAbsent()
        {
            string runtimeSource = File.ReadAllText(Path.Combine("src", "EnemyGatePathfindingRuntime.cs"));
            string samePclSource = File.ReadAllText(Path.Combine("src", "SamePclGateRouteRuntime.cs"));
            Assert(!File.Exists(Path.Combine("src", "CursorGateRouteFilter.cs")),
                "managed cursor filter and its overlay/search fallbacks are absent");
            Assert(runtimeSource.IndexOf("GetNextReachablePclDelegate", StringComparison.Ordinal) < 0,
                "whole PCL function detour delegate is absent");
            Assert(runtimeSource.IndexOf("AddDetour", StringComparison.Ordinal) < 0,
                "whole PCL function detour installation is absent");
            Assert(samePclSource.IndexOf("PathBuilderRva =", StringComparison.Ordinal) < 0 &&
                    samePclSource.IndexOf("transaction.AddDetour", StringComparison.Ordinal) >= 0,
                "Same-PCL uses the separately validated F4930 function detour");
            Assert(samePclSource.IndexOf("globalDirectionGridWrites=0", StringComparison.Ordinal) >= 0 &&
                    samePclSource.IndexOf("PathDirectionGridRva", StringComparison.Ordinal) < 0,
                "Same-PCL filters loaded bytes without addressing the global grid for writes");
            Assert(samePclSource.IndexOf("existingHookOwner", StringComparison.Ordinal) >= 0 &&
                    samePclSource.IndexOf("BugfixesAndQoL_Serp", StringComparison.Ordinal) >= 0,
                "overlapping builder ownership is explicitly suppressed");
        }

        private static void DirectionMaskBlocksOnlyTheGatePassage()
        {
            string topology = File.ReadAllText(Path.Combine("src", "GateTopologySnapshotProvider.cs"));
            string boundary = ExtractMethodBody(topology, "ClearBoundary");
            Assert(boundary.IndexOf("1 << direction", StringComparison.Ordinal) >= 0 &&
                    boundary.IndexOf("(direction + 4) & 7", StringComparison.Ordinal) >= 0,
                "gate boundary clears both directions of the crossing edge");
            Assert(boundary.IndexOf("direction - 1", StringComparison.Ordinal) >= 0 &&
                    boundary.IndexOf("direction + 1", StringComparison.Ordinal) >= 0,
                "gate boundary also rejects diagonal corner cuts");
            string axis = ExtractMethodBody(topology, "TryResolvePassageAxis");
            Assert(axis.IndexOf("PassageAxisResolver.TryResolve", StringComparison.Ordinal) >= 0,
                "topology delegates axis selection to the tested evidence resolver");
        }

        private static void VanillaDirectionFilterContractsAreAtomic()
        {
            int[] expectedRvas =
            {
                0xD9EA6, 0xDA783, 0xDACB2, 0xDB242, 0xF31A8, 0xF33F5,
                0xDB857, 0xDB947, 0xDBA36, 0xDBB26, 0xDC536
            };
            int[] expectedLengths = { 14, 18, 18, 17, 15, 14, 17, 17, 17, 17, 16 };
            Assert(EnemyGatePathfindingNativeDefinition.DirectionFilterRvas.Length == 11,
                "all eleven player-aware native direction loads are represented");
            for (int index = 0; index < expectedRvas.Length; index++)
            {
                Assert(EnemyGatePathfindingNativeDefinition.DirectionFilterRvas[index] == expectedRvas[index],
                    "direction-filter RVA " + index);
                Assert(EnemyGatePathfindingNativeDefinition.DirectionFilterLengths[index] == expectedLengths[index],
                    "direction-filter displaced length " + index);
            }
            string runtime = File.ReadAllText(Path.Combine("src", "SamePclGateRouteRuntime.cs"));
            string source = File.ReadAllText(Path.Combine("src", "DirectionFilterAdapterEmitter.cs"));
            Assert(runtime.IndexOf("for (int index = 0; index < edgeHooks.Length; index++)",
                    StringComparison.Ordinal) >= 0 &&
                    runtime.IndexOf("transaction.AddInline(directCursorHook",
                        StringComparison.Ordinal) >= 0 &&
                    runtime.IndexOf("transaction.AddInline(cursorPclDecisionHook",
                        StringComparison.Ordinal) >= 0 &&
                    runtime.IndexOf("cursorPclDecisionHook",
                        StringComparison.Ordinal) >= 0 &&
                    runtime.IndexOf("transaction.Commit()", StringComparison.Ordinal) >= 0,
                "both cursor sites and all edge adapters share one atomic transaction");
            Assert(source.IndexOf("__dword_ptr.gs[0x48]", StringComparison.Ordinal) >= 0 &&
                    source.IndexOf("ThreadSlotCount", StringComparison.Ordinal) >= 0,
                "native adapters validate the fixed TEB thread slot");
            Assert(source.IndexOf("assembler.and(al", StringComparison.Ordinal) >= 0 &&
                    source.IndexOf("assembler.and(r11b", StringComparison.Ordinal) >= 0 &&
                    source.IndexOf("assembler.and(cl", StringComparison.Ordinal) >= 0,
                "all native direction-load destination registers are masked");
            foreach (string method in new[]
            {
                "EmitRaxDestination", "EmitR11Destination", "EmitRcxDestination",
                "EmitRcxR12Destination"
            })
            {
                string body = ExtractMethodBody(source, method);
                Assert(CountOccurrences(body, "AddInstruction(original[0])") == 1 &&
                        body.IndexOf("for (int index = 0; index < original.Length", StringComparison.Ordinal) < 0,
                    method + " emits its original load once and has no duplicate full replay");
            }
        }

        private static void SnapshotGateIdentityRequiresBothIds()
        {
            var records = new NativeGateAccessRecord[5];
            records[4] = new NativeGateAccessRecord(true, 1, 0, 0x0002, 0, 0x001C);
            var gateGlobals = new uint[5];
            gateGlobals[4] = 0x12345678;
            var snapshot = new NativeGateAccessSnapshot(records, 9, gateGlobals);
            Assert(snapshot.MatchesGateIdentity(4, 0x12345678),
                "matching building and global IDs identify a tracked gate");
            Assert(!snapshot.MatchesGateIdentity(4, 0x12345679),
                "a stale global ID does not identify the current gate");
            Assert(!snapshot.MatchesGateIdentity(3, 0x12345678),
                "a neighboring building ID is not inferred to be the gate");
            Assert(!snapshot.MatchesGateIdentity(4, 0),
                "a missing subject global ID remains harmless and unclassified");
        }

        private static void DirectionAdapterTileRegistersMatchNativeDataFlow()
        {
            Register[] expectedTiles =
            {
                Register.R8, Register.RDI, Register.RDI, Register.R10, Register.RAX,
                Register.R8, Register.RDI, Register.RDI, Register.RDI, Register.RDI,
                Register.R12
            };
            Register[] moduleBases =
            {
                Register.RDX, Register.RDX, Register.RDX, Register.RBX, Register.RDX,
                Register.RCX, Register.R13, Register.R13, Register.R13, Register.R13,
                Register.R9
            };
            Register[] zeroExtendedIndices =
            {
                Register.RAX, Register.R11, Register.R11, Register.RAX, Register.R9,
                Register.R9, Register.RAX, Register.RAX, Register.RAX, Register.RAX,
                Register.RAX
            };
            for (int site = 0; site < expectedTiles.Length; site++)
            {
                Register actual = DirectionFilterAdapterEmitter.GetTileRegister(site);
                Assert(actual == expectedTiles[site],
                    "direction adapter " + site + " uses its audited native tile register");
                Assert(actual != moduleBases[site],
                    "direction adapter " + site + " never indexes policy by its module base");
                Register index = DirectionFilterAdapterEmitter.GetZeroExtendedIndexRegister(site);
                Assert(index == zeroExtendedIndices[site] && index != moduleBases[site],
                    "direction adapter " + site +
                    " zero-extends through a scratch register that is not the native module base");

                byte[] original = EnemyGatePathfindingNativeDefinition.GetDirectionFilterBytes(site);
                ulong ip = 0x180000000UL + unchecked((ulong)
                    EnemyGatePathfindingNativeDefinition.DirectionFilterRvas[site]);
                Instruction directionRead = DecodeAt(original, ip,
                    site >= 6 && site <= 9 ? 1 : 0);
                bool containsTileAndBase =
                    (directionRead.MemoryBase == actual && directionRead.MemoryIndex == moduleBases[site]) ||
                    (directionRead.MemoryIndex == actual && directionRead.MemoryBase == moduleBases[site]);
                Assert(containsTileAndBase,
                    "direction adapter " + site +
                    " mapping matches the native Direction-Grid memory operand");
            }
        }

        private static void CrashDumpRegisterRegressionsFailOpen()
        {
            const long dumpModuleBase = 0x00007FFA80260000;
            Assert(DirectionFilterAdapterEmitter.GetTileRegister(5) == Register.R8,
                "21:59 crash regression: F33F5 uses R8 rather than RCX");
            Assert(DirectionFilterAdapterEmitter.GetTileRegister(0) == Register.R8,
                "D9EA6 uses R8 rather than RDX");
            Assert(DirectionFilterAdapterEmitter.GetTileRegister(3) == Register.R10,
                "DB242 uses R10 rather than RBX");
            Assert(!DirectionFilterAdapterEmitter.IsTileIndexInRange(dumpModuleBase),
                "the DLL base observed in the crash cannot index a policy mask");
            Assert(!DirectionFilterAdapterEmitter.IsTileIndexInRange(-1),
                "negative native tile indices fail open");
            Assert(DirectionFilterAdapterEmitter.IsTileIndexInRange(0) &&
                    DirectionFilterAdapterEmitter.IsTileIndexInRange(
                        EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive - 1),
                "both valid tile-grid boundaries are accepted");
            Assert(!DirectionFilterAdapterEmitter.IsTileIndexInRange(
                    EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive),
                "the first tile beyond the native grid fails open");
        }

        private static void DirectionAdaptersActuallyAssembleAndDecode()
        {
            const ulong library = 0x180000000UL;
            const ulong slots = 0x123456789ABC0000UL;
            for (int site = 0; site < EnemyGatePathfindingNativeDefinition.DirectionFilterRvas.Length; site++)
            {
                byte[] originalBytes = EnemyGatePathfindingNativeDefinition.GetDirectionFilterBytes(site);
                ulong originalIp = library + unchecked((ulong)
                    EnemyGatePathfindingNativeDefinition.DirectionFilterRvas[site]);
                ulong stubIp = library + 0x02000000UL + unchecked((ulong)(site * 0x1000));
                byte[] emitted = DirectionFilterAdapterEmitter.AssembleAndValidate(
                    originalBytes, originalIp, site, slots, stubIp);
                Assert(emitted.Length > originalBytes.Length,
                    "direction adapter " + site + " assembles to a validated native stub");

                Instruction originalFirst = DecodeFirst(originalBytes, originalIp);
                var decoder = Decoder.Create(64, new ByteArrayCodeReader(emitted));
                decoder.IP = stubIp;
                int matchingOriginalLoads = 0;
                int matchingTileAdds = 0;
                int matchingTileCopies = 0;
                int matchingRangeGuards = 0;
                Register expectedTile = DirectionFilterAdapterEmitter.GetTileRegister(site);
                Register expectedMask = site == 1 || site == 2
                    ? Register.RAX : site == 4 || site == 5 || site == 10
                    ? Register.R10 : Register.R9;
                Register expectedIndex = site == 1 || site == 2
                    ? Register.R11 : site == 4 || site == 5 ? Register.R9 : Register.RAX;
                while (decoder.IP < stubIp + (ulong)emitted.Length)
                {
                    decoder.Decode(out Instruction instruction);
                    Assert(instruction.Code != Code.INVALID,
                        "direction adapter " + site + " disassembles without invalid opcodes");
                    if (SameMemoryInstruction(instruction, originalFirst)) matchingOriginalLoads++;
                    if (instruction.Mnemonic == Mnemonic.Add &&
                        instruction.Op0Kind == OpKind.Register &&
                        instruction.Op0Register == expectedMask &&
                        instruction.Op1Kind == OpKind.Register)
                    {
                        Assert(instruction.Op1Register == expectedIndex,
                            "direction adapter " + site + " adds only its zero-extended tile index");
                        matchingTileAdds++;
                    }
                    if (instruction.Mnemonic == Mnemonic.Mov &&
                        instruction.Op0Kind == OpKind.Register &&
                        instruction.Op0Register == ToRegister32(expectedIndex) &&
                        instruction.Op1Kind == OpKind.Register &&
                        instruction.Op1Register == ToRegister32(expectedTile))
                        matchingTileCopies++;
                    if (instruction.Mnemonic == Mnemonic.Cmp &&
                        instruction.Op0Kind == OpKind.Register &&
                        instruction.Op0Register == ToRegister32(expectedTile) &&
                        instruction.Op1Kind == OpKind.Immediate32 &&
                        instruction.Immediate32 ==
                            EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive)
                        matchingRangeGuards++;
                }
                Assert(matchingOriginalLoads == 1,
                    "direction adapter " + site + " contains exactly one original producer load");
                Assert(matchingTileAdds == 1 && matchingTileCopies == 1 &&
                        matchingRangeGuards == 1,
                    "direction adapter " + site +
                    " has one zero-extended tile add and one bounds guard");

                byte[] probeBytes = new byte[64];
                for (int index = 0; index < probeBytes.Length; index++) probeBytes[index] = 0x90;
                Array.Copy(originalBytes, probeBytes, originalBytes.Length);
                IntPtr probeMemory = Marshal.AllocHGlobal(probeBytes.Length);
                try
                {
                    Marshal.Copy(probeBytes, 0, probeMemory, probeBytes.Length);
                    using (var probe = new X64InlineHook(
                        unchecked((ulong)probeMemory.ToInt64()), 14))
                    {
                        Assert(probe.DisplacedByteCount ==
                                EnemyGatePathfindingNativeDefinition.DirectionFilterLengths[site],
                            "installed RedBird decodes the exact direction span " + site);
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(probeMemory);
                }
            }

            byte[] duplicateBytes = EnemyGatePathfindingNativeDefinition.GetDirectionFilterBytes(0);
            Instruction duplicate = DecodeFirst(duplicateBytes,
                library + unchecked((ulong)EnemyGatePathfindingNativeDefinition.DirectionFilterRvas[0]));
            var invalid = new Assembler(64);
            invalid.AddInstruction(duplicate);
            invalid.AddInstruction(duplicate);
            bool rejected = false;
            try
            {
                using (var stream = new MemoryStream())
                    invalid.Assemble(new StreamCodeWriter(stream), library + 0x03000000UL);
            }
            catch (ArgumentException) { rejected = true; }
            Assert(rejected, "Iced regression proves duplicate instruction IPs are rejected");
        }

        private static void BaselinePlayerScopesUseNativeArguments()
        {
            string source = File.ReadAllText(Path.Combine("src", "SamePclGateRouteRuntime.cs"));
            string attack = ExtractMethodBody(source, "FilterAttack");
            Assert(attack.IndexOf("QueryScope scope = Enter(player)", StringComparison.Ordinal) >= 0,
                "DBC60 binds its explicit eighth player argument");
            Assert(source.IndexOf("int tribePlayer = ResolveTribePlayer(tribe)",
                        StringComparison.Ordinal) >= 0 &&
                    source.IndexOf("ValidateExplicitPlayer(player, tribePlayer)",
                        StringComparison.Ordinal) >= 0,
                "DA020 validates its explicit sixth player argument against its tribe");
            Assert(source.IndexOf("ActivePlayerIdRva", StringComparison.Ordinal) >= 0 &&
                    source.IndexOf("GetLocalPlayerId", StringComparison.Ordinal) < 0,
                "195E30 follows Vanilla's active native player rather than the editor local-player API");
            Assert(source.IndexOf("FilterAlternateConsumer", StringComparison.Ordinal) >= 0 &&
                    source.IndexOf("FilterCandidateSearch", StringComparison.Ordinal) >= 0,
                "1232E0 and DC3C0 have explicit query scopes");
            Assert(EnemyGatePathfindingNativeDefinition.ActivePlayerIdRva == 0x88E3D70 &&
                    EnemyGatePathfindingNativeDefinition.AlternateBuildingConsumerRva == 0x1232E0 &&
                    EnemyGatePathfindingNativeDefinition.PlayerAwareCandidateSearchRva == 0xDC3C0,
                "baseline-bound player globals and function entries are pinned");
        }

        private static void DirectCursorCallsiteContractIsExact()
        {
            const ulong library = 0x180000000UL;
            const ulong wrapper = 0x7FFF12345678UL;
            byte[] original = EnemyGatePathfindingNativeDefinition.GetDirectCursorSearchBlockBytes();
            byte[] emitted = DirectCursorCallAdapterEmitter.AssembleAndValidate(
                original,
                library + (ulong)EnemyGatePathfindingNativeDefinition.DirectCursorSearchBlockRva,
                wrapper,
                library + 0x02200000UL);
            Assert(emitted.Length > 0, "direct cursor call adapter assembles and disassembles");

            byte[] probeBytes = new byte[64];
            for (int index = 0; index < probeBytes.Length; index++) probeBytes[index] = 0x90;
            Array.Copy(original, probeBytes, original.Length);
            IntPtr probeMemory = Marshal.AllocHGlobal(probeBytes.Length);
            try
            {
                Marshal.Copy(probeBytes, 0, probeMemory, probeBytes.Length);
                using (var probe = new X64InlineHook(
                    unchecked((ulong)probeMemory.ToInt64()),
                    EnemyGatePathfindingNativeDefinition.DirectCursorSearchBlockLength))
                    Assert(probe.DisplacedByteCount ==
                            EnemyGatePathfindingNativeDefinition.DirectCursorSearchBlockLength,
                        "installed RedBird decodes the exact 29-byte direct cursor block");
            }
            finally { Marshal.FreeHGlobal(probeMemory); }

            string runtime = File.ReadAllText(Path.Combine("src", "SamePclGateRouteRuntime.cs"));
            string wrapperBody = ExtractMethodBody(runtime, "FilterDirectCursorSearch");
            Assert(wrapperBody.IndexOf("originalDirectTileSearch", StringComparison.Ordinal) >= 0 &&
                    wrapperBody.IndexOf("Enter(player)", StringComparison.Ordinal) >= 0 &&
                    wrapperBody.IndexOf("Complete(scope", StringComparison.Ordinal) >= 0,
                "direct cursor wrapper scopes exactly the original DB650 call");
            Assert(wrapperBody.IndexOf("new ", StringComparison.Ordinal) < 0 &&
                    wrapperBody.IndexOf("GetPathComponentGrid", StringComparison.Ordinal) < 0,
                "direct cursor wrapper performs no allocation or PCL work");
        }

        private static void NormalCursorPreviewContractIsExact()
        {
            const ulong imageBase = 0x180000000UL;
            byte[] original = EnemyGatePathfindingNativeDefinition.GetCursorPclDecisionBytes();
            Assert(original.Length == EnemyGatePathfindingNativeDefinition.CursorPclDecisionLength,
                "normal cursor decision has the exact 14-byte baseline block");

            var decoder = Decoder.Create(64, new ByteArrayCodeReader(original));
            decoder.IP = imageBase +
                (ulong)EnemyGatePathfindingNativeDefinition.CursorPclDecisionRva;
            Instruction call = decoder.Decode();
            Instruction test = decoder.Decode();
            Instruction lea = decoder.Decode();
            Assert(call.Mnemonic == Mnemonic.Call &&
                    call.NearBranchTarget == imageBase + 0xE2610UL,
                "cursor decision calls the confirmed E2610 PCL reachability function");
            Assert(test.Mnemonic == Mnemonic.Test && test.Op0Register == Register.EAX &&
                    test.Op1Register == Register.EAX,
                "cursor decision tests E2610's EAX result");
            Assert(lea.Mnemonic == Mnemonic.Lea &&
                    decoder.IP == imageBase +
                        (ulong)EnemyGatePathfindingNativeDefinition.CursorPclDecisionReturnRva,
                "cursor decision displacement ends exactly at 8F1CD");

            byte[] probeBytes = new byte[64];
            for (int index = 0; index < probeBytes.Length; index++) probeBytes[index] = 0x90;
            Array.Copy(original, probeBytes, original.Length);
            IntPtr probeMemory = Marshal.AllocHGlobal(probeBytes.Length);
            try
            {
                Marshal.Copy(probeBytes, 0, probeMemory, probeBytes.Length);
                using (var probe = new X64InlineHook(
                    unchecked((ulong)probeMemory.ToInt64()),
                    EnemyGatePathfindingNativeDefinition.CursorPclDecisionLength))
                    Assert(probe.DisplacedByteCount ==
                            EnemyGatePathfindingNativeDefinition.CursorPclDecisionLength,
                        "installed RedBird decodes exactly the 14-byte cursor decision block");
            }
            finally { Marshal.FreeHGlobal(probeMemory); }

            const ulong wrapper = 0x7FFF76543210UL;
            byte[] emitted = CursorPclCallAdapterEmitter.AssembleAndValidate(
                original, imageBase +
                    (ulong)EnemyGatePathfindingNativeDefinition.CursorPclDecisionRva,
                wrapper, imageBase + 0x02300000UL);
            Assert(emitted.Length > original.Length,
                "cursor PCL call adapter assembles and disassembles");

            var emittedDecoder = Decoder.Create(64, new ByteArrayCodeReader(emitted));
            emittedDecoder.IP = imageBase + 0x02300000UL;
            int calls = 0, tests = 0, leas = 0, stackDelta = 0;
            int modeCopies = 0, unitCopies = 0;
            while (emittedDecoder.IP < imageBase + 0x02300000UL + (ulong)emitted.Length)
            {
                Instruction instruction = emittedDecoder.Decode();
                Assert(instruction.Code != Code.INVALID,
                    "cursor PCL adapter disassembles without invalid opcodes");
                if (instruction.Mnemonic == Mnemonic.Call) calls++;
                if (instruction.Mnemonic == Mnemonic.Test &&
                    instruction.Op0Register == Register.EAX) tests++;
                if (instruction.Mnemonic == Mnemonic.Lea &&
                    instruction.Op0Register == Register.RDI) leas++;
                if (instruction.Mnemonic == Mnemonic.Sub &&
                    instruction.Op0Register == Register.RSP)
                    stackDelta += unchecked((int)instruction.Immediate32);
                if (instruction.Mnemonic == Mnemonic.Add &&
                    instruction.Op0Register == Register.RSP)
                    stackDelta -= unchecked((int)instruction.Immediate32);
                if (instruction.Mnemonic == Mnemonic.Mov &&
                    instruction.Op0Kind == OpKind.Memory &&
                    instruction.MemoryBase == Register.RSP &&
                    instruction.MemoryDisplacement64 == 0x20 &&
                    instruction.Op1Register == Register.R10D) modeCopies++;
                if (instruction.Mnemonic == Mnemonic.Mov &&
                    instruction.Op0Kind == OpKind.Memory &&
                    instruction.MemoryBase == Register.RSP &&
                    instruction.MemoryDisplacement64 == 0x28 &&
                    instruction.Op1Register == Register.R14D) unitCopies++;
            }
            Assert(calls == 1 && tests == 1 && leas == 1 && stackDelta == 0 &&
                    modeCopies == 1 && unitCopies == 1,
                "cursor PCL adapter has one wrapper call, balanced stack, both stack arguments, and one TEST/LEA replay");
            Assert(lea.IPRelativeMemoryAddress == imageBase +
                    (ulong)EnemyGatePathfindingNativeDefinition.NativeDirectionGridRva &&
                    lea.IPRelativeMemoryAddress != 1UL,
                "RedBird post-displacement RDI is the global grid base, not a small source PCL");

            string runtime = File.ReadAllText(Path.Combine("src", "SamePclGateRouteRuntime.cs"));
            string callback = ExtractMethodBody(runtime, "FilterCursorPclDecision");
            string deferred = ExtractMethodBody(runtime, "ProcessCursorPreview");
            Assert(runtime.IndexOf("transaction.AddInline(cursorPclDecisionHook",
                        StringComparison.Ordinal) >= 0 &&
                    runtime.IndexOf("FilterNormalCursorPclDecision",
                        StringComparison.Ordinal) < 0,
                "cursor decision uses the ABI adapter and removes the stale post-LEA context callback");
            Assert(runtime.IndexOf("!cursorPclDecisionHook.Success", StringComparison.Ordinal) >= 0 &&
                    runtime.IndexOf("cursorPclDecisionHook.Hook.DisplacedByteCount",
                        StringComparison.Ordinal) >= 0,
                "cursor decision hook participates in the atomic committed-span contract");
            Assert(callback.IndexOf("originalPclReachability", StringComparison.Ordinal) >= 0 &&
                    callback.IndexOf("targetPcl != sourcePcl", StringComparison.Ordinal) >= 0 &&
                    callback.IndexOf("ApplyCursorPreviewResult", StringComparison.Ordinal) >= 0,
                "wrapper uses the real ABI PCL arguments and preserves Vanilla unless a cache blocks it");
            foreach (string forbidden in new[]
            {
                "GameUnitManagerAPI", "originalDirectTileSearch", "lock (", "new ",
                "DebugLogHelper", "GetSelectedChimps", "registers->"
            })
                Assert(callback.IndexOf(forbidden, StringComparison.Ordinal) < 0,
                    "cursor callback hot path excludes " + forbidden);
            Assert(CountOccurrences(deferred, "TryGetUnitById") == 1 &&
                    CountOccurrences(deferred, "originalDirectTileSearch") == 1 &&
                    deferred.IndexOf("GetSelectedChimps", StringComparison.Ordinal) < 0 &&
                    deferred.IndexOf("for (", StringComparison.Ordinal) < 0 &&
                    deferred.IndexOf("foreach", StringComparison.Ordinal) < 0,
                "deferred preview validates exactly one unit with one Vanilla DB650 search");
            Assert(runtime.IndexOf("Stopwatch.Frequency / 5L", StringComparison.Ordinal) >= 0 &&
                    runtime.IndexOf("cursorRefreshMs=200", StringComparison.Ordinal) >= 0,
                "cursor preview is globally throttled to five native validations per second");
        }

        private static void CursorPreviewDecisionIsCausalAndStable()
        {
            Assert(!EnemyGatePathfindingPolicy.ShouldBlockCursorPreview(0, 0),
                "ordinary Vanilla no-route without a rejected gate edge stays fail-open");
            Assert(EnemyGatePathfindingPolicy.ShouldBlockCursorPreview(0, 1),
                "Vanilla no-route caused by a rejected gate edge blocks the cursor");
            Assert(!EnemyGatePathfindingPolicy.ShouldBlockCursorPreview(1, 12),
                "a successful Vanilla detour remains green despite rejected direct edges");

            Assert(EnemyGatePathfindingPolicy.CursorPreviewCacheMatches(
                    true, 2, 41, 300, 301, 7, 7, 0x1234UL,
                    2, 41, 300, 301, 7, 7, 0x1234UL),
                "an identical cursor key reuses its stable decision");
            Assert(!EnemyGatePathfindingPolicy.CursorPreviewCacheMatches(
                    true, 2, 41, 300, 301, 7, 7, 0x1234UL,
                    2, 41, 301, 301, 7, 7, 0x1234UL),
                "a changed target invalidates the cursor decision immediately");
            Assert(!EnemyGatePathfindingPolicy.CursorPreviewCacheMatches(
                    true, 2, 41, 300, 301, 7, 7, 0x1234UL,
                    2, 41, 300, 301, 7, 7, 0x1235UL),
                "a changed gate policy invalidates the cursor decision immediately");
            Assert(!EnemyGatePathfindingPolicy.CursorPreviewCacheMatches(
                    false, 2, 41, 300, 301, 7, 7, 0x1234UL,
                    2, 41, 300, 301, 7, 7, 0x1234UL),
                "an unpublished cache never changes Vanilla's cursor decision");
            Assert(!EnemyGatePathfindingPolicy.CursorPreviewCacheMatches(
                    true, 2, 41, 300, 301, 7, 7, 0x1234UL,
                    2, 41, 300, 301, 8, 8, 0x1234UL),
                "changed source and target PCLs invalidate a cached cursor decision");

            Assert(EnemyGatePathfindingPolicy.ApplyCursorPreviewResult(
                    7, false, true, false) == 7,
                "Different-PCL preserves Vanilla even with a blocking cache");
            Assert(EnemyGatePathfindingPolicy.ApplyCursorPreviewResult(
                    7, true, false, false) == 7,
                "Same-PCL without a published cache preserves Vanilla");
            Assert(EnemyGatePathfindingPolicy.ApplyCursorPreviewResult(
                    7, true, true, true) == 7,
                "an allowing Same-PCL cache preserves Vanilla");
            Assert(EnemyGatePathfindingPolicy.ApplyCursorPreviewResult(
                    7, true, true, false) == 0,
                "a blocking Same-PCL cache changes a positive result to zero");
            Assert(EnemyGatePathfindingPolicy.ApplyCursorPreviewResult(
                    0, true, true, false) == 0,
                "a Vanilla failure remains byte-equivalent zero");
        }

        private static void GatehouseUsesBothOuterBoundaries()
        {
            string topology = File.ReadAllText(Path.Combine("src", "GateTopologySnapshotProvider.cs"));
            string gate = ExtractMethodBody(topology, "ClearGatehouseOuterDirections");
            Assert(gate.IndexOf("minY - 1, x, minY, 4", StringComparison.Ordinal) >= 0 &&
                    gate.IndexOf("maxY, x, maxY + 1, 4", StringComparison.Ordinal) >= 0,
                "vertical gatehouse blocks both outer entry/exit boundaries");
            Assert(gate.IndexOf("minX - 1, y, minX, y, 2", StringComparison.Ordinal) >= 0 &&
                    gate.IndexOf("maxX, y, maxX + 1, y, 2", StringComparison.Ordinal) >= 0,
                "horizontal gatehouse blocks both outer entry/exit boundaries");
            Assert(gate.IndexOf("Math.Min(info.EntryY, info.ExitY) != minY - 1",
                        StringComparison.Ordinal) >= 0 &&
                    gate.IndexOf("Math.Max(info.EntryY, info.ExitY) != maxY + 1",
                        StringComparison.Ordinal) >= 0,
                "logged 401/372 to 401/366 gate coordinates validate against 367..371 bounds");
            string bridge = ExtractMethodBody(topology, "ClearDrawbridgePassageDirections");
            Assert(bridge.IndexOf("(minY + maxY) >> 1", StringComparison.Ordinal) >= 0 &&
                    bridge.IndexOf("(minX + maxX) >> 1", StringComparison.Ordinal) >= 0,
                "drawbridge keeps its proven middle seam");
            Assert(topology.IndexOf("entry-exit-outer", StringComparison.Ordinal) >= 0,
                "snapshot diagnostics identify the gatehouse barrier contract");
        }

        private static void NativeSnapshotPoolAcquisitionIsSynchronized()
        {
            string source = File.ReadAllText(Path.Combine("src", "SamePclGateRouteRuntime.cs"));
            string enter = ExtractMethodBody(source, "Enter");
            string complete = ExtractMethodBody(source, "Complete");
            Assert(source.IndexOf("NativeSnapshotPoolSize = 4", StringComparison.Ordinal) >= 0 &&
                    source.IndexOf("Marshal.FreeHGlobal", StringComparison.Ordinal) < 0,
                "four static mask slots are never freed while the runtime is published");
            Assert(enter.IndexOf("lock (maskGate)", StringComparison.Ordinal) >= 0 &&
                    complete.IndexOf("lock (maskGate)", StringComparison.Ordinal) >= 0,
                "snapshot acquisition and release share one synchronization gate");
            Assert(source.IndexOf("SlotDepthOffset", StringComparison.Ordinal) >= 0,
                "nested zero-mask scopes use explicit depth rather than pointer nullness");
            Assert(source.IndexOf("CompareExchange(ref samplePublished[index], 1, 0)",
                        StringComparison.Ordinal) >= 0 &&
                    source.IndexOf("Volatile.Write(ref samplePublished[index], 2)",
                        StringComparison.Ordinal) >= 0,
                "scope samples publish only after all primitive fields are written");
        }

        private static void PassageAxisEvidenceIsDeterministic()
        {
            AssertAxis(true, 10, 10, 14, 10, 8, 8, 12, 12,
                false, 0, 0, 0, 0, true, PassageAxisSource.EntryExitCoordinates,
                "entry/exit coordinates resolve a horizontal passage even without an exit tile id");
            AssertAxis(false, 0, 0, 0, 0, 20, 20, 24, 24,
                true, 20, 25, 24, 29, false, PassageAxisSource.LinkedDrawbridge,
                "south drawbridge resolves a vertical gate passage");
            AssertAxis(false, 0, 0, 0, 0, 20, 20, 24, 24,
                true, 20, 15, 24, 19, false, PassageAxisSource.LinkedDrawbridge,
                "north drawbridge resolves a vertical gate passage");
            AssertAxis(false, 0, 0, 0, 0, 20, 20, 24, 24,
                true, 25, 20, 29, 24, true, PassageAxisSource.LinkedDrawbridge,
                "east drawbridge resolves a horizontal gate passage");
            AssertAxis(false, 0, 0, 0, 0, 20, 20, 24, 24,
                true, 15, 20, 19, 24, true, PassageAxisSource.LinkedDrawbridge,
                "west drawbridge resolves a horizontal gate passage");
            AssertAxis(false, 0, 0, 0, 0, 30, 30, 32, 36,
                false, 0, 0, 0, 0, true, PassageAxisSource.ElongatedFootprint,
                "elongated fallback resolves perpendicular passage axis");
            bool resolved = PassageAxisResolver.TryResolve(false, 0, 0, 0, 0,
                325, 483, 329, 487, false, 0, 0, 0, 0,
                out _, out PassageAxisSource ambiguous);
            Assert(!resolved && ambiguous == PassageAxisSource.None,
                "square gate without coordinate or bridge evidence remains fail-open");
        }

        private static void AssertAxis(bool coordinatesAvailable,
            int entryX, int entryY, int exitX, int exitY,
            int gateMinX, int gateMinY, int gateMaxX, int gateMaxY,
            bool bridgeAvailable, int bridgeMinX, int bridgeMinY, int bridgeMaxX, int bridgeMaxY,
            bool expectedHorizontal, PassageAxisSource expectedSource, string message)
        {
            bool resolved = PassageAxisResolver.TryResolve(coordinatesAvailable,
                entryX, entryY, exitX, exitY, gateMinX, gateMinY, gateMaxX, gateMaxY,
                bridgeAvailable, bridgeMinX, bridgeMinY, bridgeMaxX, bridgeMaxY,
                out bool horizontal, out PassageAxisSource source);
            Assert(resolved && horizontal == expectedHorizontal && source == expectedSource, message);
        }

        private static Instruction DecodeFirst(byte[] bytes, ulong ip)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = ip;
            decoder.Decode(out Instruction instruction);
            return instruction;
        }

        private static Instruction DecodeAt(byte[] bytes, ulong ip, int requestedIndex)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = ip;
            Instruction instruction = default;
            for (int index = 0; index <= requestedIndex; index++)
                decoder.Decode(out instruction);
            return instruction;
        }

        private static Register ToRegister32(Register register)
        {
            switch (register)
            {
                case Register.RAX: return Register.EAX;
                case Register.R8: return Register.R8D;
                case Register.R9: return Register.R9D;
                case Register.R10: return Register.R10D;
                case Register.R11: return Register.R11D;
                case Register.R12: return Register.R12D;
                case Register.RDI: return Register.EDI;
                default: throw new ArgumentOutOfRangeException(nameof(register));
            }
        }

        private static bool SameMemoryInstruction(Instruction left, Instruction right) =>
            left.Code == right.Code && left.MemoryBase == right.MemoryBase &&
            left.MemoryIndex == right.MemoryIndex && left.MemoryIndexScale == right.MemoryIndexScale &&
            left.MemoryDisplacement64 == right.MemoryDisplacement64;

        private static int CountOccurrences(string value, string token)
        {
            int count = 0;
            int offset = 0;
            while ((offset = value.IndexOf(token, offset, StringComparison.Ordinal)) >= 0)
            {
                count++;
                offset += token.Length;
            }
            return count;
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
