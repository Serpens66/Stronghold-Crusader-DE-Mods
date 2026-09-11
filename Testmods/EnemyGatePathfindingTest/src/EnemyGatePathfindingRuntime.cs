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
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;

namespace EnemyGatePathfindingTest
{
    internal sealed unsafe class EnemyGatePathfindingRuntime
    {
        private const int MaximumCallbackWarningsPerMap = 8;
        private const int SiteCount = 2;
        private static readonly int DecisionCount =
            Enum.GetValues(typeof(NativeGateSnapshotDecision)).Length;
        private static readonly long DiagnosticInterval = Stopwatch.Frequency * 10L;

        private readonly ManualLogSource log;
        private GateTopologySnapshotProvider topologyProvider;
        private CursorGateRouteFilter cursorRouteFilter;
        private SamePclGateRouteRuntime samePclRouteRuntime;
        private HookTransaction transaction;
        private readonly HookHandle<X64InlineHook> pclGraphCapturedByFilterHook = new HookHandle<X64InlineHook>();
        private readonly HookHandle<X64InlineHook> builderPrecheckCapturedByFilterHook = new HookHandle<X64InlineHook>();
        private volatile NativeGateAccessSnapshot gateAccess = NativeGateAccessSnapshot.Empty;
        private volatile NativeGateAccessSnapshot previousStableGateAccess = NativeGateAccessSnapshot.Empty;
        private ulong libraryBase;
        private int mapActive;
        private int callbackWarnings;
        private long nextDiagnosticAt;
        private readonly long[] siteCalls = new long[SiteCount];
        private readonly long[] lastSiteCalls = new long[SiteCount];
        private readonly long[] decisions = new long[SiteCount * DecisionCount];
        private readonly long[] lastDecisions = new long[SiteCount * DecisionCount];
        private readonly long[] queryPlayers = new long[9];
        private readonly long[] preservedOriginalZf = new long[2];
        private readonly long[] foreignOriginalZf = new long[2];
        private long untrackedPolicyClear;
        private long untrackedRemovedRecord;
        private long untrackedUnexpected;
        private readonly CapturerSample[] samples =
            new CapturerSample[SiteCount * DecisionCount];

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

            // The displaced integer-only blocks define RCX/RDX before the callback.
            // RedBird's BeforeCallback stub changes flags while saving context, so the
            // callback reconstructs CMP from its exact operands and restores ZF itself.
            // No XMM value is live across either audited block.
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
                samePclRouteRuntime = new SamePclGateRouteRuntime(
                    log, memory, context.Region, libraryBase, friendlyMoatHookOwnerLoaded);
            }
            catch (Exception ex)
            {
                samePclRouteRuntime = null;
                Shared.DebugLogHelper.LogWarning(log,
                    "Active Same-PCL builder correction could not be installed; " +
                    $"Vanilla remains active for that path: {ex.GetType().Name}: {ex.Message}");
            }
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
            topologyProvider.SetRoutePolicyConsumer(updated =>
            {
                cursorRouteFilter?.UpdatePolicy(updated);
                samePclRouteRuntime?.UpdatePolicy(updated);
            });

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
            if (samePclRouteRuntime?.Installed != true && !friendlyMoatHookOwnerLoaded)
                Shared.DebugLogHelper.LogWarning(log,
                    "Same-PCL builder rerouting is unavailable and remains fail-open. " +
                    "Cursor causality and Different-PCL filtering remain active.");
        }

        internal void BeginMap()
        {
            if (Volatile.Read(ref mapActive) != 0)
                EndMap("implicit restart before OnStartMap(Post)");
            if (Interlocked.CompareExchange(ref mapActive, 1, 0) != 0)
                return;
            ResetMapCounters();
            samePclRouteRuntime?.ResetCounters();
            topologyProvider?.BeginExplicitEpoch("OnStartMap(Post)");
            cursorRouteFilter?.BeginEpoch("OnStartMap(Post)");
            Shared.DebugLogHelper.LogInfo(log,
                "Enemy-gate map started: Different-PCL filter, causal cursor policy and " +
                $"Same-PCL builder correction={(samePclRouteRuntime?.Installed == true ? "active" : "inactive")}.");
        }

        internal void EndMap(string reason = "OnUnloadMap(Pre)")
        {
            bool hadActiveEpoch = Interlocked.CompareExchange(ref mapActive, 0, 1) == 1;
            if (!hadActiveEpoch)
                return;
            LogDiagnosticCheckpoint("final", reason);
            topologyProvider?.EndEpoch(reason);
            cursorRouteFilter?.EndEpoch(reason);
            gateAccess = NativeGateAccessSnapshot.Empty;
        }

        internal void ProcessDeferredDiagnostics()
        {
            try
            {
                topologyProvider?.ProcessDeferred();
                cursorRouteFilter?.ProcessDeferred();
                samePclRouteRuntime?.ProcessDeferred();
                long now = Stopwatch.GetTimestamp();
                if (Volatile.Read(ref mapActive) != 0 &&
                    now >= Volatile.Read(ref nextDiagnosticAt))
                {
                    Volatile.Write(ref nextDiagnosticAt, now + DiagnosticInterval);
                    LogDiagnosticCheckpoint("periodic", "10-second interval");
                }
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

        private void UpdateGateAccess(NativeGateAccessSnapshot updated)
        {
            NativeGateAccessSnapshot next = updated ?? NativeGateAccessSnapshot.Empty;
            NativeGateAccessSnapshot current = gateAccess;
            if (next.TrackedRecords != 0 && current.TrackedRecords != 0 &&
                !ReferenceEquals(next, current))
                previousStableGateAccess = current;
            gateAccess = next;
        }

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
            X64SmartCPUContext* registers = null;
            bool originalZeroKnown = false;
            bool originalZero = false;
            try
            {
                if (Volatile.Read(ref mapActive) != 0)
                {
                    Interlocked.Increment(ref siteCalls[builderPrecheck ? 1 : 0]);
                }

                registers = context.Pointer;
                if (registers == null)
                {
                    RecordDecision(builderPrecheck, NativeGateSnapshotDecision.Exception,
                        0, 0, 0, 0, 0, 0, 0, 0, default, 0, false, false,
                        gateAccess.TopologyFingerprint);
                    return;
                }

                // The original CMP executed immediately before this callback, therefore
                // rereading the same word uses the already validated native memory contract.
                byte* captureBase = (byte*)(builderPrecheck ? registers->R13 : registers->RAX);
                ushort nativeCapturedByPlayerId = *(ushort*)(captureBase + registers->RDX +
                    EnemyGatePathfindingNativeDefinition.CapturedByPlayerTableDisplacement);
                originalZero = builderPrecheck
                    ? EnemyGatePathfindingNativeDefinition.BuilderPrecheckCaptureCompareIsEqual(
                        nativeCapturedByPlayerId, unchecked((ushort)registers->RAX))
                    : EnemyGatePathfindingNativeDefinition.PclGraphCaptureCompareIsEqual(
                        nativeCapturedByPlayerId);
                originalZeroKnown = true;
                registers->Rflags = EnemyGatePathfindingPolicy.SetZeroFlag(
                    registers->Rflags, originalZero);

                int queryPlayerId = builderPrecheck
                    ? unchecked((int)(uint)registers->RBP)
                    : unchecked((int)(uint)registers->R14);
                int buildingId = unchecked((int)(uint)registers->RCX);
                byte* record = (byte*)registers->R9;
                if (record == null)
                {
                    RecordDecision(builderPrecheck,
                        NativeGateSnapshotDecision.UntrackedConnection,
                        queryPlayerId, buildingId, 0, 0, nativeCapturedByPlayerId,
                        0, 0, 0, default, unchecked((ushort)registers->RAX), originalZero,
                        originalZero, gateAccess.TopologyFingerprint);
                    return;
                }

                int recordBuildingId = *(int*)(record +
                    EnemyGatePathfindingNativeDefinition.RecordBuildingIdOffset);
                int ownerPlayerId = *(int*)(record +
                    EnemyGatePathfindingNativeDefinition.RecordOwnerPlayerIdOffset);
                int firstPcl = *(int*)(record +
                    EnemyGatePathfindingNativeDefinition.RecordFirstPclOffset);
                int secondPcl = *(int*)(record +
                    EnemyGatePathfindingNativeDefinition.RecordSecondPclOffset);
                int thirdPcl = *(int*)(record +
                    EnemyGatePathfindingNativeDefinition.RecordThirdPclOffset);
                if (buildingId != recordBuildingId)
                {
                    RecordDecision(builderPrecheck,
                        NativeGateSnapshotDecision.RecordIdMismatch,
                        queryPlayerId, buildingId, recordBuildingId, ownerPlayerId,
                        nativeCapturedByPlayerId, firstPcl, secondPcl, thirdPcl, default,
                        unchecked((ushort)registers->RAX), originalZero,
                        originalZero, gateAccess.TopologyFingerprint);
                    return;
                }

                NativeGateAccessSnapshot current = gateAccess;
                NativeGateAccessRecord snapshotRecord;
                NativeGateSnapshotDecision decision = current.Evaluate(
                    queryPlayerId, buildingId, ownerPlayerId, nativeCapturedByPlayerId,
                    out snapshotRecord);
                if (decision == NativeGateSnapshotDecision.ExcludeForeignCapture)
                {
                    registers->Rflags = EnemyGatePathfindingPolicy.SetZeroFlag(
                        registers->Rflags, true);
                }
                RecordDecision(builderPrecheck, decision, queryPlayerId, buildingId,
                    recordBuildingId, ownerPlayerId, nativeCapturedByPlayerId,
                    firstPcl, secondPcl, thirdPcl, snapshotRecord,
                    unchecked((ushort)registers->RAX), originalZero,
                    decision == NativeGateSnapshotDecision.ExcludeForeignCapture || originalZero,
                    current.TopologyFingerprint);
            }
            catch
            {
                // Any policy failure retains the exact result of Vanilla's displaced CMP.
                if (registers != null && originalZeroKnown)
                    registers->Rflags = EnemyGatePathfindingPolicy.SetZeroFlag(
                        registers->Rflags, originalZero);
                RecordDecision(builderPrecheck, NativeGateSnapshotDecision.Exception,
                    0, 0, 0, 0, 0, 0, 0, 0, default, 0, originalZero,
                    originalZero, gateAccess.TopologyFingerprint);
                Interlocked.Increment(ref callbackWarnings);
            }
        }

        private void RecordDecision(
            bool builderPrecheck,
            NativeGateSnapshotDecision decision,
            int queryPlayerId,
            int buildingId,
            int recordBuildingId,
            int nativeOwner,
            int nativeCaptured,
            int firstPcl,
            int secondPcl,
            int thirdPcl,
            NativeGateAccessRecord snapshotRecord,
            ushort compareValue,
            bool originalZero,
            bool finalZero,
            ulong fingerprint)
        {
            if (Volatile.Read(ref mapActive) == 0)
                return;
            int decisionIndex = (int)decision;
            int site = builderPrecheck ? 1 : 0;
            Interlocked.Increment(ref decisions[(site * DecisionCount) + decisionIndex]);
            if (queryPlayerId > 0 && queryPlayerId < queryPlayers.Length)
                Interlocked.Increment(ref queryPlayers[queryPlayerId]);
            if ((int)decision <= (int)NativeGateSnapshotDecision.PreserveCapturerAlly)
                Interlocked.Increment(ref preservedOriginalZf[originalZero ? 1 : 0]);
            if (decision == NativeGateSnapshotDecision.ExcludeForeignCapture)
                Interlocked.Increment(ref foreignOriginalZf[originalZero ? 1 : 0]);
            if (decision == NativeGateSnapshotDecision.UntrackedConnection)
            {
                NativeGateAccessSnapshot live = gateAccess;
                if (live.TrackedRecords == 0)
                    Interlocked.Increment(ref untrackedPolicyClear);
                else if (HasStableRecord(previousStableGateAccess, buildingId))
                    Interlocked.Increment(ref untrackedRemovedRecord);
                else
                    Interlocked.Increment(ref untrackedUnexpected);
            }

            ref CapturerSample sample = ref samples[(site * DecisionCount) + decisionIndex];
            if (Interlocked.CompareExchange(ref sample.State, 1, 0) != 0)
                return;
            sample.Site = site;
            sample.Decision = decisionIndex;
            sample.QueryPlayer = queryPlayerId;
            sample.BuildingId = buildingId;
            sample.RecordBuildingId = recordBuildingId;
            sample.NativeOwner = nativeOwner;
            sample.NativeCaptured = nativeCaptured;
            sample.FirstPcl = firstPcl;
            sample.SecondPcl = secondPcl;
            sample.ThirdPcl = thirdPcl;
            sample.SnapshotOwner = snapshotRecord.OwnerPlayerId;
            sample.SnapshotCaptured = snapshotRecord.CapturedByPlayerId;
            sample.CompareValue = compareValue;
            sample.OriginalZero = originalZero ? 1 : 0;
            sample.FinalZero = finalZero ? 1 : 0;
            sample.Fingerprint = fingerprint;
            Volatile.Write(ref sample.State, 2);
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
            previousStableGateAccess = NativeGateAccessSnapshot.Empty;
            Array.Clear(siteCalls, 0, siteCalls.Length);
            Array.Clear(lastSiteCalls, 0, lastSiteCalls.Length);
            Array.Clear(decisions, 0, decisions.Length);
            Array.Clear(lastDecisions, 0, lastDecisions.Length);
            Array.Clear(queryPlayers, 0, queryPlayers.Length);
            Array.Clear(preservedOriginalZf, 0, preservedOriginalZf.Length);
            Array.Clear(foreignOriginalZf, 0, foreignOriginalZf.Length);
            Reset(ref untrackedPolicyClear);
            Reset(ref untrackedRemovedRecord);
            Reset(ref untrackedUnexpected);
            Array.Clear(samples, 0, samples.Length);
            Interlocked.Exchange(ref callbackWarnings, 0);
            Volatile.Write(ref nextDiagnosticAt, Stopwatch.GetTimestamp() + DiagnosticInterval);
        }

        private static long Read(ref long value) => Interlocked.Read(ref value);

        private void LogDiagnosticCheckpoint(string kind, string reason)
        {
            long pcl = Read(ref siteCalls[0]);
            long builder = Read(ref siteCalls[1]);
            long pclDelta = pcl - lastSiteCalls[0];
            long builderDelta = builder - lastSiteCalls[1];
            lastSiteCalls[0] = pcl;
            lastSiteCalls[1] = builder;
            var players = new StringBuilder();
            for (int player = 1; player <= 8; player++)
            {
                long count = Read(ref queryPlayers[player]);
                if (count == 0) continue;
                if (players.Length > 0) players.Append(',');
                players.Append('p').Append(player).Append('=').Append(count);
            }
            if (players.Length == 0) players.Append("none:NOT_OBSERVED");
            Shared.DebugLogHelper.LogInfo(log,
                $"Enemy-gate diagnostic checkpoint: kind={kind}, reason={reason}, " +
                $"sites(pclGraph={pcl}(+{pclDelta}),builderPrecheck={builder}(+{builderDelta})), " +
                $"coverageBySite(pclGraph=[{FormatSiteCoverage(0)}]," +
                $"builderPrecheck=[{FormatSiteCoverage(1)}]), players({players}), " +
                $"preservedByOriginalZf(zf0={Read(ref preservedOriginalZf[0])},zf1={Read(ref preservedOriginalZf[1])}), " +
                $"foreignBlocked(changedZf={Read(ref foreignOriginalZf[0])},alreadyExcluded={Read(ref foreignOriginalZf[1])}), " +
                $"untracked(policyClear={Read(ref untrackedPolicyClear)},removedRecord={Read(ref untrackedRemovedRecord)}," +
                $"unexpected={Read(ref untrackedUnexpected)}), " +
                $"callbackWarnings={Volatile.Read(ref callbackWarnings)}.");
            Shared.DebugLogHelper.LogInfo(log,
                $"Enemy-gate snapshot checkpoint: {topologyProvider?.DescribeState() ?? "unavailable"}.");
            cursorRouteFilter?.LogCheckpoint(kind, reason);
            SamePclCoverageSnapshot same = samePclRouteRuntime?.GetCoverageSnapshot() ?? default;
            Shared.DebugLogHelper.LogInfo(log,
                $"Enemy-gate Same-PCL checkpoint: kind={kind}, installed={same.Installed}," +
                $"hookOwnerConflict={same.OwnerConflict},queries={same.Queries}," +
                $"nativeRoutePreserved={same.Preserved},edgeRejected={same.RejectedEdges}," +
                $"vanillaDetours={same.Detours},policyNoRoute={same.NoRoutes}," +
                $"humanBuilderDetour={same.HumanDetours},aiDetour={same.AiDetours}," +
                $"attackDetour={same.AttackDetours},buildingApproachDetour={same.BuildingDetours}," +
                $"cursorDetour={same.CursorDetours},missingContext={same.MissingContexts}," +
                $"threadSlotConflict={same.SlotConflicts},exceptions={same.Exceptions}," +
                $"elapsedMs={(same.ElapsedTicks * 1000.0 / Stopwatch.Frequency).ToString("F3", CultureInfo.InvariantCulture)}," +
                $"managedReplacementSearches=0,directionGridWrites=0.");
            LogNewCapturerSamples();
            if (string.Equals(kind, "final", StringComparison.Ordinal))
                LogAcceptanceVerdict(reason);
        }

        private void LogAcceptanceVerdict(string reason)
        {
            TopologyCoverageSnapshot topology = topologyProvider?.GetCoverageSnapshot() ?? default;
            CursorCoverageSnapshot cursor = cursorRouteFilter?.GetCoverageSnapshot() ?? default;
            SamePclCoverageSnapshot same = samePclRouteRuntime?.GetCoverageSnapshot() ?? default;
            long uncaptured = DecisionTotal(NativeGateSnapshotDecision.PreserveUncaptured);
            long ownCapturer = DecisionTotal(NativeGateSnapshotDecision.PreserveCapturer);
            long alliedCapturer = DecisionTotal(NativeGateSnapshotDecision.PreserveCapturerAlly);
            long foreignCapture = DecisionTotal(NativeGateSnapshotDecision.ExcludeForeignCapture);
            long policyFailures =
                DecisionTotal(NativeGateSnapshotDecision.InvalidQueryPlayer) +
                DecisionTotal(NativeGateSnapshotDecision.RecordIdMismatch) +
                DecisionTotal(NativeGateSnapshotDecision.OwnerMismatch) +
                DecisionTotal(NativeGateSnapshotDecision.CaptureMismatch) +
                DecisionTotal(NativeGateSnapshotDecision.Exception);
            bool hookActivity = Read(ref siteCalls[0]) > 0 && Read(ref siteCalls[1]) > 0;
            bool runtimeFailed = Volatile.Read(ref callbackWarnings) != 0 ||
                topology.Errors != 0 || cursor.Errors != 0 || cursor.Failures != 0 ||
                same.Exceptions != 0 || same.SlotConflicts != 0 ||
                Read(ref untrackedUnexpected) != 0 || policyFailures != 0;
            DiagnosticVerdict sameHookVerdict = same.OwnerConflict
                ? DiagnosticVerdict.NOT_APPLICABLE
                : !same.Installed ? DiagnosticVerdict.FAIL
                : EnemyGatePathfindingPolicy.IntegrityVerdict(same.Queries > 0, false);

            Shared.DebugLogHelper.LogInfo(log,
                "Enemy-gate acceptance verdict: " +
                $"hookExecution={EnemyGatePathfindingPolicy.IntegrityVerdict(hookActivity, false)}," +
                $"runtimeIntegrity={EnemyGatePathfindingPolicy.IntegrityVerdict(hookActivity || cursor.CheckedRoutes > 0, runtimeFailed)}," +
                $"uncapturedGate={EnemyGatePathfindingPolicy.ObservationVerdict(uncaptured)}," +
                $"capturedGate={EnemyGatePathfindingPolicy.ObservationVerdict(topology.PeakCaptured)}," +
                $"ownerAtHook={DiagnosticVerdict.NOT_APPLICABLE}," +
                $"ownerAllyAtHook={DiagnosticVerdict.NOT_APPLICABLE}," +
                $"ownCapturer={EnemyGatePathfindingPolicy.ObservationVerdict(ownCapturer)}," +
                $"alliedCapturer={EnemyGatePathfindingPolicy.ObservationVerdict(alliedCapturer)}," +
                $"foreignCapturer={EnemyGatePathfindingPolicy.ObservationVerdict(foreignCapture)}," +
                $"foreignCaptureBlock={EnemyGatePathfindingPolicy.ObservationVerdict(Read(ref foreignOriginalZf[0]))}," +
                $"cursorReachable={EnemyGatePathfindingPolicy.ObservationVerdict(cursor.Reachable)}," +
                $"cursorVanillaNoRoute={EnemyGatePathfindingPolicy.ObservationVerdict(cursor.VanillaNoRoute)}," +
                $"cursorPolicyBlocked={EnemyGatePathfindingPolicy.ObservationVerdict(cursor.PolicyBlocked)}," +
                $"cursorTargetBlocked={EnemyGatePathfindingPolicy.ObservationVerdict(cursor.TargetBlocked)}," +
                $"cursorForcedDetour={EnemyGatePathfindingPolicy.ObservationVerdict(cursor.ForcedDetour)}," +
                $"samePclHookExecution={sameHookVerdict}," +
                $"nativeRoutePreserved={EnemyGatePathfindingPolicy.ObservationVerdict(same.Preserved)}," +
                $"edgeRejected={EnemyGatePathfindingPolicy.ObservationVerdict(same.RejectedEdges)}," +
                $"vanillaDetour={EnemyGatePathfindingPolicy.ObservationVerdict(same.Detours)}," +
                $"policyNoRoute={EnemyGatePathfindingPolicy.ObservationVerdict(same.NoRoutes)}," +
                $"humanBuilderDetour={EnemyGatePathfindingPolicy.ObservationVerdict(same.HumanDetours)}," +
                $"aiDetour={EnemyGatePathfindingPolicy.ObservationVerdict(same.AiDetours)}," +
                $"attackDetour={EnemyGatePathfindingPolicy.ObservationVerdict(same.AttackDetours)}," +
                $"buildingApproachDetour={EnemyGatePathfindingPolicy.ObservationVerdict(same.BuildingDetours)}," +
                $"cursorDetour={EnemyGatePathfindingPolicy.ObservationVerdict(same.CursorDetours)}," +
                $"threadSlotIntegrity={EnemyGatePathfindingPolicy.IntegrityVerdict(same.Queries > 0, same.SlotConflicts > 0)}," +
                $"drawbridgeTopology={(topology.DrawbridgeObserved ? DiagnosticVerdict.PASS : DiagnosticVerdict.NOT_OBSERVED)}," +
                $"lifecycle={DiagnosticVerdict.PASS}," +
                $"captureTransitions={topology.CaptureTransitions},recaptureTransitions={topology.RecaptureTransitions}," +
                $"untrackedTransitionCalls={DecisionTotal(NativeGateSnapshotDecision.UntrackedConnection)}," +
                $"untrackedPolicyClear={Read(ref untrackedPolicyClear)}," +
                $"untrackedRemovedRecord={Read(ref untrackedRemovedRecord)}," +
                $"untrackedUnexpected={Read(ref untrackedUnexpected)}," +
                $"reason={reason}.");
        }

        private long DecisionTotal(NativeGateSnapshotDecision decision) =>
            Read(ref decisions[(int)decision]) +
            Read(ref decisions[DecisionCount + (int)decision]);

        private static bool HasStableRecord(NativeGateAccessSnapshot snapshot, int buildingId) =>
            snapshot != null && buildingId > 0 &&
            buildingId < snapshot.RecordsByBuildingId.Length &&
            snapshot.RecordsByBuildingId[buildingId].Valid;

        private static void Reset(ref long value) => Interlocked.Exchange(ref value, 0);

        private string FormatSiteCoverage(int site)
        {
            var coverage = new StringBuilder();
            for (int decision = 0; decision < DecisionCount; decision++)
            {
                if (coverage.Length > 0) coverage.Append(',');
                int index = (site * DecisionCount) + decision;
                long total = Read(ref decisions[index]);
                long delta = total - lastDecisions[index];
                lastDecisions[index] = total;
                coverage.Append((NativeGateSnapshotDecision)decision).Append('=')
                    .Append(total).Append("(+").Append(delta).Append(')');
                if (decision == (int)NativeGateSnapshotDecision.PreserveOwner ||
                    decision == (int)NativeGateSnapshotDecision.PreserveOwnerAlly)
                    coverage.Append(":NOT_APPLICABLE");
                else if (total == 0 &&
                    decision <= (int)NativeGateSnapshotDecision.ExcludeForeignCapture)
                    coverage.Append(":NOT_OBSERVED");
            }
            return coverage.ToString();
        }

        private void LogNewCapturerSamples()
        {
            for (int index = 0; index < samples.Length; index++)
            {
                ref CapturerSample sample = ref samples[index];
                if (Volatile.Read(ref sample.State) != 2 ||
                    Interlocked.CompareExchange(ref sample.Reported, 1, 0) != 0)
                    continue;
                Shared.DebugLogHelper.LogInfo(log,
                    $"Enemy-gate first capturer sample: site={(sample.Site == 0 ? "pclGraph" : "builderPrecheck")}, " +
                    $"decision={(NativeGateSnapshotDecision)sample.Decision}, queryPlayer={sample.QueryPlayer}, " +
                    $"building={sample.BuildingId}, recordBuilding={sample.RecordBuildingId}, " +
                    $"nativeOwner={sample.NativeOwner}, snapshotOwner={sample.SnapshotOwner}, " +
                    $"nativeCaptured={sample.NativeCaptured}, snapshotCaptured={sample.SnapshotCaptured}, " +
                    $"portalPcls={sample.FirstPcl}/{sample.SecondPcl}/{sample.ThirdPcl}, " +
                    $"compareValue={sample.CompareValue}, originalZF={sample.OriginalZero}, finalZF={sample.FinalZero}, " +
                    $"accessFingerprint=0x{sample.Fingerprint:X16}.");
            }
        }

        private struct CapturerSample
        {
            internal int State;
            internal int Reported;
            internal int Site;
            internal int Decision;
            internal int QueryPlayer;
            internal int BuildingId;
            internal int RecordBuildingId;
            internal int NativeOwner;
            internal int NativeCaptured;
            internal int FirstPcl;
            internal int SecondPcl;
            internal int ThirdPcl;
            internal int SnapshotOwner;
            internal int SnapshotCaptured;
            internal ushort CompareValue;
            internal int OriginalZero;
            internal int FinalZero;
            internal ulong Fingerprint;
        }

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
