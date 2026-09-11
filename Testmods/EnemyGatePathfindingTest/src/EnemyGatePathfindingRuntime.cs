using BepInEx.Bootstrap;
using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API.LowLevel;
using System;
using System.Threading;

namespace EnemyGatePathfindingTest
{
    internal sealed unsafe class EnemyGatePathfindingRuntime
    {
        private const ulong ZeroFlagMask = 1UL << 6;
        private const int MaximumCallbackWarningsPerMap = 8;

        private readonly ManualLogSource log;
        private GateTopologySnapshotProvider topologyProvider;
        private CursorGateRouteFilter cursorRouteFilter;
        private HookTransaction transaction;
        private readonly HookHandle<X64InlineHook> pclGraphCapturedByFilterHook = new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> builderPrecheckCapturedByFilterHook = new HookHandle<X64InlineHook>();
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
            CrusaderLibraryLoadContext context,
            bool referenceHashMatches)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            ReadOnlySpan<byte> memory = context.Memory;
            if (!referenceHashMatches)
                throw new InvalidOperationException("fixed native layout hash does not match the supported CrusaderDE.dll");
            if (context.ModuleHandle == IntPtr.Zero)
                throw new InvalidOperationException("native library handle is null");
            Version redBirdVersion = typeof(X64InlineHook).Assembly.GetName().Version;
            if (redBirdVersion != new Version(1, 1, 0, 0))
                throw new InvalidOperationException(
                    $"RedBird.X64 {redBirdVersion} is not the audited 1.1.0 implementation");

            Shared.NativeResolution pclGraphCompareResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                EnemyGatePathfindingNativeDefinition.PclGraphCapturedByComparePattern,
                EnemyGatePathfindingNativeDefinition.PclGraphCapturedByFilterRva -
                    EnemyGatePathfindingNativeDefinition.PclGraphCapturedByCompareOffsetInPattern,
                referenceHashMatches: true,
                "PCL-graph hostile-gate captured-player comparison",
                log);
            Shared.NativeResolution builderPrecheckCompareResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                EnemyGatePathfindingNativeDefinition.BuilderPrecheckCapturedByComparePattern,
                EnemyGatePathfindingNativeDefinition.BuilderPrecheckCapturedByFilterRva -
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
            int pclGraphFilterRva = pclGraphCompareResolution.Rva +
                EnemyGatePathfindingNativeDefinition.PclGraphCapturedByCompareOffsetInPattern;
            int builderPrecheckFilterRva = builderPrecheckCompareResolution.Rva +
                EnemyGatePathfindingNativeDefinition.BuilderPrecheckCapturedByCompareOffsetInPattern;
            int cursorXRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                cursorResolution.Rva + EnemyGatePathfindingNativeDefinition.CursorTargetXDisplacementOffset,
                cursorResolution.Rva + EnemyGatePathfindingNativeDefinition.CursorTargetXNextInstructionOffset);
            int cursorYRva = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                cursorResolution.Rva + EnemyGatePathfindingNativeDefinition.CursorTargetYDisplacementOffset,
                cursorResolution.Rva + EnemyGatePathfindingNativeDefinition.CursorTargetYNextInstructionOffset);
            if (pclGraphFilterRva != EnemyGatePathfindingNativeDefinition.PclGraphCapturedByFilterRva ||
                builderPrecheckFilterRva !=
                    EnemyGatePathfindingNativeDefinition.BuilderPrecheckCapturedByFilterRva ||
                cursorResolution.Rva != EnemyGatePathfindingNativeDefinition.CursorTargetSignatureRva ||
                cursorXRva != EnemyGatePathfindingNativeDefinition.CursorTargetXRva ||
                cursorYRva != EnemyGatePathfindingNativeDefinition.CursorTargetYRva)
                throw new InvalidOperationException("native gate/cursor signatures resolved outside their audited RVAs");

            libraryBase = unchecked((ulong)context.ModuleHandle.ToInt64());
            EnemyGatePathfindingNativeDefinition.ValidateNativeHookContracts(memory);
            ProbeExactHookLength(
                libraryBase,
                pclGraphFilterRva,
                EnemyGatePathfindingNativeDefinition.PclGraphCapturedByFilterHookLength,
                "PCL-graph captured-player filter");
            ProbeExactHookLength(
                libraryBase,
                builderPrecheckFilterRva,
                EnemyGatePathfindingNativeDefinition.BuilderPrecheckCapturedByFilterHookLength,
                "builder-precheck captured-player filter");
            topologyProvider = new GateTopologySnapshotProvider(log);
            topologyProvider.SetGateAccessConsumer(UpdateGateAccess);

            // The displaced integer-only blocks define RCX/RDX and ZF before the callback.
            // No XMM value is live across either audited block. The callback may only read
            // primitive snapshots and adjust RFLAGS; no game API access is permitted.
            transaction = new HookTransaction(
                context.Region,
                SHCDESE.BepInEx.Bootstrap.Plugin.Instance.LoggerFactory,
                new HookTransactionOptions
                {
                    FailureMode = TransactionFailureMode.RollbackAndThrow,
                    OwnsHooks = false
                });
            transaction.AddContextHook(
                pclGraphCapturedByFilterHook,
                HookTarget.FromAddress(libraryBase + unchecked((ulong)pclGraphFilterRva)),
                FilterUnrelatedCapturedEnemyGatePclGraph,
                new ContextHookOptions
                {
                    Registers = X64SmartCPUContextRegs.All,
                    HookSize = EnemyGatePathfindingNativeDefinition.PclGraphCapturedByFilterHookLength,
                    ErrorMode = CallbackErrorMode.LogAndContinue,
                    Placement = OverwrittenInstructionPlacement.BeforeCallback
                });
            transaction.AddContextHook(
                builderPrecheckCapturedByFilterHook,
                HookTarget.FromAddress(libraryBase + unchecked((ulong)builderPrecheckFilterRva)),
                FilterUnrelatedCapturedEnemyGateBuilderPrecheck,
                new ContextHookOptions
                {
                    Registers = X64SmartCPUContextRegs.All,
                    HookSize = EnemyGatePathfindingNativeDefinition.BuilderPrecheckCapturedByFilterHookLength,
                    ErrorMode = CallbackErrorMode.LogAndContinue,
                    Placement = OverwrittenInstructionPlacement.BeforeCallback
                });
            CommitResult commitResult = transaction.Commit();
            if (!commitResult.IsCompleteSuccess ||
                !pclGraphCapturedByFilterHook.Success ||
                !builderPrecheckCapturedByFilterHook.Success)
                throw new InvalidOperationException(
                    $"both snapshot-based captured-player filters were not installed atomically: {commitResult}");
            if (pclGraphCapturedByFilterHook.Hook.DisplacedByteCount !=
                    EnemyGatePathfindingNativeDefinition.PclGraphCapturedByFilterHookLength ||
                builderPrecheckCapturedByFilterHook.Hook.DisplacedByteCount !=
                    EnemyGatePathfindingNativeDefinition.BuilderPrecheckCapturedByFilterHookLength)
            {
                // This object has not been published yet, so rollback is safe and mandatory.
                transaction.DisableAll();
                throw new InvalidOperationException(
                    "RedBird committed an unexpected captured-player hook span; both hooks were rolled back.");
            }

            bool friendlyMoatHookOwnerLoaded =
                Chainloader.PluginInfos.ContainsKey("BugfixesAndQoL_Serp");
            try
            {
                cursorRouteFilter = new CursorGateRouteFilter(
                    log,
                    memory,
                    context.Region,
                    libraryBase,
                    (int*)(libraryBase + unchecked((ulong)cursorXRva)),
                    (int*)(libraryBase + unchecked((ulong)cursorYRva)),
                    installNativeHooks: !friendlyMoatHookOwnerLoaded);
                topologyProvider.SetRoutePolicyConsumer(cursorRouteFilter.UpdatePolicy);
                cursorRouteFilter.SetTopologyEpochStarter(
                    () => topologyProvider.BeginExplicitEpoch("first cursor query"));
            }
            catch (Exception ex)
            {
                cursorRouteFilter = null;
                Shared.DebugLogHelper.LogWarning(log,
                    "Crash-safe cursor correction could not be installed; the snapshot-based " +
                    $"Different-PCL filter remains active: {ex.GetType().Name}: {ex.Message}");
            }

            Shared.DebugLogHelper.LogInfo(log,
                "Crash-safe enemy-gate hooks installed: " +
                $"pclGraphCapturerFilter=0x{pclGraphFilterRva:X} " +
                $"({pclGraphCompareResolution.Method}+0x" +
                $"{EnemyGatePathfindingNativeDefinition.PclGraphCapturedByCompareOffsetInPattern:X}), " +
                $"pclGraphDisplaced={pclGraphCapturedByFilterHook.Hook.DisplacedByteCount}, " +
                $"builderPrecheckCapturerFilter=0x{builderPrecheckFilterRva:X} " +
                $"({builderPrecheckCompareResolution.Method}+0x" +
                $"{EnemyGatePathfindingNativeDefinition.BuilderPrecheckCapturedByCompareOffsetInPattern:X}), " +
                $"builderPrecheckDisplaced={builderPrecheckCapturedByFilterHook.Hook.DisplacedByteCount}, " +
                $"cursorTarget=0x{cursorResolution.Rva:X}, " +
                $"redBird={redBirdVersion}, " +
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
            topologyProvider?.BeginExplicitEpoch("OnStartMap(Post)");
            cursorRouteFilter?.BeginEpoch("OnStartMap(Post)");
            Shared.DebugLogHelper.LogInfo(log,
                "Enemy-gate map started: snapshot Different-PCL filter and read-only cursor policy active; " +
                "Same-PCL native builder correction disabled pending a complete local-edge proof.");
        }

        internal void EndMap()
        {
            bool hadActiveEpoch = Interlocked.CompareExchange(ref mapActive, 0, 1) == 1;
            topologyProvider?.EndEpoch("OnUnloadMap(Post)");
            cursorRouteFilter?.EndEpoch("OnUnloadMap(Post)");
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
                topologyProvider?.ProcessDeferred();
                cursorRouteFilter?.ProcessDeferred();
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure(ex);
            }
        }

        internal void OnGameTick()
        {
            try { topologyProvider?.OnGameTick(); }
            catch { topologyProvider?.RecordSnapshotFailure(); }
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

        private static void ProbeExactHookLength(
            ulong imageBase,
            int rva,
            int expectedLength,
            string name)
        {
            using (var probe = new X64InlineHook(
                imageBase + unchecked((ulong)rva), expectedLength))
            {
                if (probe.DisplacedByteCount != expectedLength)
                {
                    throw new InvalidOperationException(
                        $"Unexpected RedBird span for {name}: expected {expectedLength}, " +
                        $"decoded {probe.DisplacedByteCount}.");
                }
            }
        }
    }
}
