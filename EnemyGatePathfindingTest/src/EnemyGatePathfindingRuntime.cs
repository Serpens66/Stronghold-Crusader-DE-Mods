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
        private HookRef<X64InlineHook> capturedByFilterHook = new HookRef<X64InlineHook>();
        private volatile NativeGateAccessSnapshot gateAccess = NativeGateAccessSnapshot.Empty;
        private ulong libraryBase;
        private int mapActive;
        private int callbackWarnings;
        private long uncapturedEnemyRecordsExcludedByVanilla;
        private long alliedCapturedEnemyRecordsAllowed;
        private long foreignCapturedEnemyRecordsRejected;
        private long filterFailOpenRecords;

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

            Shared.NativeResolution compareSequenceResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                EnemyGatePathfindingNativeDefinition.CapturedByComparePattern,
                EnemyGatePathfindingNativeDefinition.CapturedByCompareRva -
                    EnemyGatePathfindingNativeDefinition.CapturedByCompareOffsetInPattern,
                referenceHashMatches: true,
                "hostile-gate captured-player comparison",
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

            int compareRva = compareSequenceResolution.Rva +
                EnemyGatePathfindingNativeDefinition.CapturedByCompareOffsetInPattern;
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
            if (compareRva != EnemyGatePathfindingNativeDefinition.CapturedByCompareRva ||
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
                ref capturedByFilterHook,
                libraryBase + unchecked((ulong)compareRva),
                FilterUnrelatedCapturedEnemyGate,
                regs: X64SmartCPUContextRegs.All,
                hookSize: EnemyGatePathfindingNativeDefinition.CapturedByCompareHookLength,
                errorMode: CallbackErrorMode.LogAndContinue,
                placement: OverwrittenInstructionPlacement.BeforeCallback);
            transaction.Commit();
            if (!capturedByFilterHook.Success)
                throw new InvalidOperationException("snapshot-based captured-player filter was not installed");

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
                $"capturerFilter=0x{compareRva:X} ({compareSequenceResolution.Method}+0x" +
                $"{EnemyGatePathfindingNativeDefinition.CapturedByCompareOffsetInPattern:X}), " +
                $"cursorTarget=0x{cursorResolution.Rva:X}, commandAudit=0x{commandDecisionRva:X}, " +
                $"dllSha256={EnemyGatePathfindingNativeDefinition.ReferenceSha256}. " +
                "The whole PCL detour and every global Direction-Grid write were removed.");
            Shared.DebugLogHelper.LogWarning(log,
                "Same-PCL AI tile rerouting is disabled fail-open: F4930 dispatches to six " +
                "native search variants and no common local edge-acceptance hook is fully validated. " +
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

        private void FilterUnrelatedCapturedEnemyGate(NativePointer<X64SmartCPUContext> context)
        {
            try
            {
                X64SmartCPUContext* registers = context.Pointer;
                if (registers == null)
                {
                    RecordFilterDecision(CapturedGateFilterDecision.FailOpen, false);
                    return;
                }

                int queryPlayerId = unchecked((int)(uint)registers->R14);
                int buildingId = unchecked((int)(uint)registers->RCX);
                byte* record = (byte*)registers->R9;
                if (record == null)
                {
                    RecordFilterDecision(CapturedGateFilterDecision.FailOpen, false);
                    return;
                }

                int recordBuildingId = *(int*)(record +
                    EnemyGatePathfindingNativeDefinition.RecordBuildingIdOffset);
                int ownerPlayerId = *(int*)(record +
                    EnemyGatePathfindingNativeDefinition.RecordOwnerPlayerIdOffset);
                bool vanillaSawUncaptured = (registers->Rflags & ZeroFlagMask) != 0;
                if (buildingId != recordBuildingId)
                {
                    RecordFilterDecision(CapturedGateFilterDecision.FailOpen, vanillaSawUncaptured);
                    return;
                }

                NativeGateAccessSnapshot current = gateAccess;
                CapturedGateFilterDecision decision = current.Evaluate(
                    queryPlayerId, buildingId, ownerPlayerId, vanillaSawUncaptured);
                if (decision == CapturedGateFilterDecision.ExcludeForeignCapture)
                    registers->Rflags |= ZeroFlagMask;
                RecordFilterDecision(decision, vanillaSawUncaptured);
            }
            catch
            {
                RecordFilterDecision(CapturedGateFilterDecision.FailOpen, false);
                Interlocked.Increment(ref callbackWarnings);
            }
        }

        private void RecordFilterDecision(CapturedGateFilterDecision decision, bool vanillaUncaptured)
        {
            if (Volatile.Read(ref mapActive) == 0)
                return;
            if (decision == CapturedGateFilterDecision.ExcludeForeignCapture)
                Interlocked.Increment(ref foreignCapturedEnemyRecordsRejected);
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
            Interlocked.Exchange(ref callbackWarnings, 0);
        }

        private static long Read(ref long value) => Interlocked.Read(ref value);
    }
}
