using BepInEx.Bootstrap;
using BepInEx.Logging;
using System;
using System.Threading;
using Zhuqiaomon.Assembly;
using Zhuqiaomon.Hooks;
using Zhuqiaomon.Hooks.Transaction;
using Zhuqiaomon.Memory;

namespace EnemyGatePathfindingTest
{
    internal sealed unsafe class EnemyGatePathfindingRuntime
    {
        private const ulong ZeroFlagMask = 1UL << 6;
        private const int MaximumCallbackWarningsPerMap = 8;

        private readonly ManualLogSource log;
        private SamePclBridgeDiagnostics samePclDiagnostics;
        private TileRouteDiagnostics tileRouteDiagnostics;
        private HookTransaction transaction;
        private HookRef<X64InlineHook> pclGraphCapturedByFilterHook = new HookRef<X64InlineHook>();
        private HookRef<X64InlineHook> builderPrecheckCapturedByFilterHook = new HookRef<X64InlineHook>();
        private volatile NativeGateAccessSnapshot gateAccess = NativeGateAccessSnapshot.Empty;
        private ulong libraryBase;
        private int mapActive;
        private int callbackWarnings;
        private long uncapturedEnemyRecordsExcludedByVanilla;
        private long alliedCapturedEnemyRecordsAllowed;
        private long foreignCapturedEnemyRecordsRejected;
        private long filterFailOpenRecords;
        private long pclGraphFilterRecords;
        private long builderPrecheckFilterRecords;
        private long pclGraphForeignCapturedRejected;
        private long builderPrecheckForeignCapturedRejected;

        internal EnemyGatePathfindingRuntime(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        internal void InitializeNative(
            IntPtr libraryHandle,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            if (!referenceHashMatches)
                throw new InvalidOperationException("fixed native layout hash does not match the supported CrusaderDE.dll");
            if (libraryHandle == IntPtr.Zero)
                throw new InvalidOperationException("native library handle is null");

            Shared.NativeResolution pclGraphCompareResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                EnemyGatePathfindingNativeDefinition.PclGraphCapturedByComparePattern,
                EnemyGatePathfindingNativeDefinition.PclGraphCapturedByCompareRva -
                    EnemyGatePathfindingNativeDefinition.PclGraphCapturedByCompareOffsetInPattern,
                referenceHashMatches: true,
                "PCL-graph hostile-gate captured-player comparison",
                log);
            Shared.NativeResolution builderPrecheckCompareResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                EnemyGatePathfindingNativeDefinition.BuilderPrecheckCapturedByComparePattern,
                EnemyGatePathfindingNativeDefinition.BuilderPrecheckCapturedByCompareRva -
                    EnemyGatePathfindingNativeDefinition.BuilderPrecheckCapturedByCompareOffsetInPattern,
                referenceHashMatches: true,
                "builder-precheck hostile-gate captured-player comparison",
                log);
            Shared.NativeResolution cursorResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                EnemyGatePathfindingNativeDefinition.CursorTargetPattern,
                EnemyGatePathfindingNativeDefinition.CursorTargetSignatureRva,
                referenceHashMatches: true,
                "human cursor target coordinate loads",
                log);
            Shared.NativeResolution commandDecisionResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                EnemyGatePathfindingNativeDefinition.CommandPclDecisionPattern,
                EnemyGatePathfindingNativeDefinition.CommandPclDecisionRva -
                    EnemyGatePathfindingNativeDefinition.CommandPclDecisionOffsetInPattern,
                referenceHashMatches: true,
                "shared command PCL decision (audit only)",
                log);

            int pclGraphCompareRva = pclGraphCompareResolution.Rva +
                EnemyGatePathfindingNativeDefinition.PclGraphCapturedByCompareOffsetInPattern;
            int builderPrecheckCompareRva = builderPrecheckCompareResolution.Rva +
                EnemyGatePathfindingNativeDefinition.BuilderPrecheckCapturedByCompareOffsetInPattern;
            int cursorXRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                cursorResolution.Rva + EnemyGatePathfindingNativeDefinition.CursorTargetXDisplacementOffset,
                cursorResolution.Rva + EnemyGatePathfindingNativeDefinition.CursorTargetXNextInstructionOffset);
            int cursorYRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                cursorResolution.Rva + EnemyGatePathfindingNativeDefinition.CursorTargetYDisplacementOffset,
                cursorResolution.Rva + EnemyGatePathfindingNativeDefinition.CursorTargetYNextInstructionOffset);
            int commandDecisionRva = commandDecisionResolution.Rva +
                EnemyGatePathfindingNativeDefinition.CommandPclDecisionOffsetInPattern;
            if (pclGraphCompareRva != EnemyGatePathfindingNativeDefinition.PclGraphCapturedByCompareRva ||
                builderPrecheckCompareRva !=
                    EnemyGatePathfindingNativeDefinition.BuilderPrecheckCapturedByCompareRva ||
                cursorResolution.Rva != EnemyGatePathfindingNativeDefinition.CursorTargetSignatureRva ||
                commandDecisionRva != EnemyGatePathfindingNativeDefinition.CommandPclDecisionRva ||
                cursorXRva != EnemyGatePathfindingNativeDefinition.CursorTargetXRva ||
                cursorYRva != EnemyGatePathfindingNativeDefinition.CursorTargetYRva)
                throw new InvalidOperationException("native gate/cursor signatures resolved outside their audited RVAs");

            libraryBase = unchecked((ulong)libraryHandle.ToInt64());
            samePclDiagnostics = new SamePclBridgeDiagnostics(
                log,
                (int*)(libraryBase + unchecked((ulong)cursorXRva)),
                (int*)(libraryBase + unchecked((ulong)cursorYRva)));
            samePclDiagnostics.SetGateAccessConsumer(UpdateGateAccess);

            // UPDATE REVIEW (Script Extender 1.42.0): only primitive snapshot reads and
            // RFLAGS changes are allowed in this callback. No API access is permitted.
            transaction = new HookTransaction(
                memory,
                libraryBase,
                loggerFactory: null,
                failureMode: TransactionFailureMode.RollbackAndThrow);
            transaction.AddContextHook(
                ref pclGraphCapturedByFilterHook,
                libraryBase + unchecked((ulong)pclGraphCompareRva),
                FilterUnrelatedCapturedEnemyGatePclGraph,
                regs: X64SmartCPUContextRegs.All,
                hookSize: EnemyGatePathfindingNativeDefinition.PclGraphCapturedByCompareHookLength,
                errorMode: CallbackErrorMode.LogAndContinue,
                placement: OverwrittenInstructionPlacement.BeforeCallback);
            transaction.AddContextHook(
                ref builderPrecheckCapturedByFilterHook,
                libraryBase + unchecked((ulong)builderPrecheckCompareRva),
                FilterUnrelatedCapturedEnemyGateBuilderPrecheck,
                regs: X64SmartCPUContextRegs.All,
                hookSize: EnemyGatePathfindingNativeDefinition.BuilderPrecheckCapturedByCompareHookLength,
                errorMode: CallbackErrorMode.LogAndContinue,
                placement: OverwrittenInstructionPlacement.BeforeCallback);
            transaction.Commit();
            if (!pclGraphCapturedByFilterHook.Success || !builderPrecheckCapturedByFilterHook.Success)
                throw new InvalidOperationException(
                    "both snapshot-based captured-player filters were not installed atomically");

            bool moveMoatLoaded = Chainloader.PluginInfos.ContainsKey("MoveMoatTest_Serp");
            try
            {
                tileRouteDiagnostics = new TileRouteDiagnostics(
                    log,
                    memory,
                    libraryBase,
                    (int*)(libraryBase + unchecked((ulong)cursorXRva)),
                    (int*)(libraryBase + unchecked((ulong)cursorYRva)),
                    installNativeHooks: !moveMoatLoaded);
                samePclDiagnostics.SetRoutePolicyConsumer(tileRouteDiagnostics.UpdatePolicy);
                tileRouteDiagnostics.SetTopologyEpochStarter(
                    () => samePclDiagnostics.BeginExplicitEpoch("first cursor query"));
            }
            catch (Exception ex)
            {
                tileRouteDiagnostics = null;
                Shared.DebugLogHelper.LogWarning(log,
                    "Crash-safe cursor correction could not be installed; the snapshot-based " +
                    $"Different-PCL filter remains active: {ex.GetType().Name}: {ex.Message}");
            }

            Shared.DebugLogHelper.LogInfo(log,
                "Crash-safe enemy-gate hooks installed: " +
                $"pclGraphCapturerFilter=0x{pclGraphCompareRva:X} " +
                $"({pclGraphCompareResolution.Method}+0x" +
                $"{EnemyGatePathfindingNativeDefinition.PclGraphCapturedByCompareOffsetInPattern:X}), " +
                $"builderPrecheckCapturerFilter=0x{builderPrecheckCompareRva:X} " +
                $"({builderPrecheckCompareResolution.Method}+0x" +
                $"{EnemyGatePathfindingNativeDefinition.BuilderPrecheckCapturedByCompareOffsetInPattern:X}), " +
                $"cursorTarget=0x{cursorResolution.Rva:X}, commandAudit=0x{commandDecisionRva:X}, " +
                $"dllSha256={EnemyGatePathfindingNativeDefinition.ReferenceSha256}. " +
                "The whole PCL detour and every global Direction-Grid write were removed.");
            Shared.DebugLogHelper.LogWarning(log,
                "Same-PCL AI tile rerouting is disabled fail-open: F4930 dispatches to six primary " +
                "searches plus a conditional DB650 post-search, and no complete local edge filter " +
                "is validated. 79C0 is distance-only. " +
                "Cursor reachability remains read-only; no builder/planner hook is installed.");
        }

        internal void BeginMap()
        {
            if (Interlocked.CompareExchange(ref mapActive, 1, 0) != 0)
                return;
            ResetMapCounters();
            samePclDiagnostics?.BeginExplicitEpoch("OnStartMap(Post)");
            tileRouteDiagnostics?.BeginEpoch("OnStartMap(Post)");
            Shared.DebugLogHelper.LogInfo(log,
                "Enemy-gate map started: snapshot Different-PCL filter and read-only cursor policy active; " +
                "Same-PCL native builder correction disabled pending a complete local-edge proof.");
        }

        internal void EndMap()
        {
            bool hadActiveEpoch = Interlocked.CompareExchange(ref mapActive, 0, 1) == 1;
            samePclDiagnostics?.EndEpoch("OnUnloadMap(Post)");
            tileRouteDiagnostics?.EndEpoch("OnUnloadMap(Post)");
            gateAccess = NativeGateAccessSnapshot.Empty;
            if (!hadActiveEpoch)
                return;
            Shared.DebugLogHelper.LogInfo(log,
                "Enemy-gate map summary: " +
                $"capturerSites(pclGraph={Read(ref pclGraphFilterRecords)}, " +
                $"builderPrecheck={Read(ref builderPrecheckFilterRecords)}), " +
                $"foreignRejectedBySite(pclGraph={Read(ref pclGraphForeignCapturedRejected)}, " +
                $"builderPrecheck={Read(ref builderPrecheckForeignCapturedRejected)}), " +
                $"uncapturedExcludedByVanilla={Read(ref uncapturedEnemyRecordsExcludedByVanilla)}, " +
                $"alliedCapturedAllowed={Read(ref alliedCapturedEnemyRecordsAllowed)}, " +
                $"foreignCapturedRejected={Read(ref foreignCapturedEnemyRecordsRejected)}, " +
                $"filterFailOpen={Read(ref filterFailOpenRecords)}, " +
                $"callbackWarnings={Volatile.Read(ref callbackWarnings)}.");
        }

        internal void ProcessDeferredDiagnostics()
        {
            try
            {
                samePclDiagnostics?.ProcessDeferred();
                tileRouteDiagnostics?.ProcessDeferred();
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure(ex);
            }
        }

        internal void OnGameTick()
        {
            try { samePclDiagnostics?.OnGameTick(); }
            catch { samePclDiagnostics?.RecordHotPathFailure(); }
        }

        private void UpdateGateAccess(NativeGateAccessSnapshot updated) =>
            gateAccess = updated ?? NativeGateAccessSnapshot.Empty;

        private void FilterUnrelatedCapturedEnemyGatePclGraph(
            NativePointer<X64SmartCPUContext> context)
        {
            // UPDATE REVIEW (CrusaderDE.dll): E2610 keeps query player in R14 here.
            FilterUnrelatedCapturedEnemyGate(context, false);
        }

        private void FilterUnrelatedCapturedEnemyGateBuilderPrecheck(
            NativePointer<X64SmartCPUContext> context)
        {
            // UPDATE REVIEW (CrusaderDE.dll): E2F60 keeps query player in RBP here.
            FilterUnrelatedCapturedEnemyGate(context, true);
        }

        private void FilterUnrelatedCapturedEnemyGate(
            NativePointer<X64SmartCPUContext> context,
            bool builderPrecheck)
        {
            try
            {
                if (Volatile.Read(ref mapActive) != 0)
                {
                    if (builderPrecheck)
                        Interlocked.Increment(ref builderPrecheckFilterRecords);
                    else
                        Interlocked.Increment(ref pclGraphFilterRecords);
                }

                X64SmartCPUContext* registers = context.Pointer;
                if (registers == null)
                {
                    RecordFilterDecision(
                        CapturedGateFilterDecision.FailOpen, false, builderPrecheck);
                    return;
                }

                int queryPlayerId = builderPrecheck
                    ? unchecked((int)(uint)registers->RBP)
                    : unchecked((int)(uint)registers->R14);
                int buildingId = unchecked((int)(uint)registers->RCX);
                byte* record = (byte*)registers->R9;
                if (record == null)
                {
                    RecordFilterDecision(
                        CapturedGateFilterDecision.FailOpen, false, builderPrecheck);
                    return;
                }

                int recordBuildingId = *(int*)(record +
                    EnemyGatePathfindingNativeDefinition.RecordBuildingIdOffset);
                int ownerPlayerId = *(int*)(record +
                    EnemyGatePathfindingNativeDefinition.RecordOwnerPlayerIdOffset);
                bool vanillaSawUncaptured = (registers->Rflags & ZeroFlagMask) != 0;
                if (buildingId != recordBuildingId)
                {
                    RecordFilterDecision(
                        CapturedGateFilterDecision.FailOpen, vanillaSawUncaptured, builderPrecheck);
                    return;
                }

                NativeGateAccessSnapshot current = gateAccess;
                CapturedGateFilterDecision decision = current.Evaluate(
                    queryPlayerId, buildingId, ownerPlayerId, vanillaSawUncaptured);
                if (decision == CapturedGateFilterDecision.ExcludeForeignCapture)
                    registers->Rflags |= ZeroFlagMask;
                RecordFilterDecision(decision, vanillaSawUncaptured, builderPrecheck);
            }
            catch
            {
                RecordFilterDecision(
                    CapturedGateFilterDecision.FailOpen, false, builderPrecheck);
                Interlocked.Increment(ref callbackWarnings);
            }
        }

        private void RecordFilterDecision(
            CapturedGateFilterDecision decision,
            bool vanillaUncaptured,
            bool builderPrecheck)
        {
            if (Volatile.Read(ref mapActive) == 0)
                return;
            if (decision == CapturedGateFilterDecision.ExcludeForeignCapture)
            {
                Interlocked.Increment(ref foreignCapturedEnemyRecordsRejected);
                if (builderPrecheck)
                    Interlocked.Increment(ref builderPrecheckForeignCapturedRejected);
                else
                    Interlocked.Increment(ref pclGraphForeignCapturedRejected);
            }
            else if (decision == CapturedGateFilterDecision.FailOpen)
                Interlocked.Increment(ref filterFailOpenRecords);
            else if (vanillaUncaptured)
                Interlocked.Increment(ref uncapturedEnemyRecordsExcludedByVanilla);
            else
                Interlocked.Increment(ref alliedCapturedEnemyRecordsAllowed);
        }

        private void TryLogDiagnosticFailure(Exception ex)
        {
            if (Interlocked.Increment(ref callbackWarnings) <= MaximumCallbackWarningsPerMap)
                Shared.DebugLogHelper.LogWarning(log,
                    "Enemy-gate deferred diagnostics failed without changing native behavior: " +
                    $"{ex.GetType().Name}: {ex.Message}");
        }

        private void ResetMapCounters()
        {
            Interlocked.Exchange(ref uncapturedEnemyRecordsExcludedByVanilla, 0);
            Interlocked.Exchange(ref alliedCapturedEnemyRecordsAllowed, 0);
            Interlocked.Exchange(ref foreignCapturedEnemyRecordsRejected, 0);
            Interlocked.Exchange(ref filterFailOpenRecords, 0);
            Interlocked.Exchange(ref pclGraphFilterRecords, 0);
            Interlocked.Exchange(ref builderPrecheckFilterRecords, 0);
            Interlocked.Exchange(ref pclGraphForeignCapturedRejected, 0);
            Interlocked.Exchange(ref builderPrecheckForeignCapturedRejected, 0);
            Interlocked.Exchange(ref callbackWarnings, 0);
        }

        private static long Read(ref long value) => Interlocked.Read(ref value);
    }
}
