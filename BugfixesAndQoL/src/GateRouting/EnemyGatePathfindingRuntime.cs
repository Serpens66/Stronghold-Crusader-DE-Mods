using APIShared.GateRouting.Native;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using CorrectedPathing = APIShared.ScriptExtenderReplacements.GamePathingManagerAPI;

namespace BugfixesAndQoL.GateRouting
{
    // TEMP_GATE_MIGRATION_DIAGNOSTICS: diagnostics live in EnemyGatePathfindingRuntime.DevDiag.cs
    // and attach through the partial Diag* methods; without SERP_DEVDIAG every call is removed.
    internal sealed unsafe partial class EnemyGatePathfindingRuntime
    {
        private GateRoutingNativeHooks nativeHooks;
        private const int SiteCount = 2;
        private static readonly int DecisionCount =
            Enum.GetValues(typeof(NativeGateSnapshotDecision)).Length;
        // TEMP_GATE_ROUTE_ACCEPTANCE: compact minute windows for the endurance test.
#if SERP_DEVDIAG
        private static readonly long DiagnosticInterval = Stopwatch.Frequency * 60L;
#endif

        private readonly ManualLogSource log;
#if SERP_DEVDIAG
        private readonly bool detailedDiagnostics;
        private readonly bool macemenDiagnostics;
#else
        private const bool detailedDiagnostics = false;
        private const bool macemenDiagnostics = false;
#endif
        // Coverage counters only feed Info checkpoints. Without diagnostics the per-edge
        // callback keeps just the rare integrity categories that can raise warnings.
#if SERP_DEVDIAG
        private readonly bool coverageCounters;
#else
        private const bool coverageCounters = false;
#endif
        private readonly DeferredGateDiagnosticErrors deferredErrors = new DeferredGateDiagnosticErrors();
        private uint reportedIntegrityErrors;
        private readonly IntegrityErrorFlushGate integrityFlush = new IntegrityErrorFlushGate();
        private long integrityRevision;
        internal long SupportIntegrityRevision => Interlocked.Read(ref integrityRevision);
        private long CountIntegrityError(ref long counter)
        {
            long value = Interlocked.Increment(ref counter);
            Interlocked.Increment(ref integrityRevision);
            return value;
        }
        private int CountIntegrityError(ref int counter)
        {
            int value = Interlocked.Increment(ref counter);
            Interlocked.Increment(ref integrityRevision);
            return value;
        }
        private GateTopologySnapshotProvider topologyProvider;
        private SamePclGateRouteRuntime samePclRouteRuntime;
        private volatile NativeGateAccessSnapshot gateAccess = NativeGateAccessSnapshot.Empty;
        private volatile NativeGateAccessSnapshot previousStableGateAccess = NativeGateAccessSnapshot.Empty;
        private ulong libraryBase;
        private int mapActive;
        private int callbackWarnings;
#if SERP_DEVDIAG
        private long nextDiagnosticAt;

        private readonly long[] siteCalls = new long[SiteCount];

        private readonly long[] lastSiteCalls = new long[SiteCount];
#endif

        private readonly long[] decisions = new long[SiteCount * DecisionCount];
#if SERP_DEVDIAG
        private readonly long[] lastDecisions = new long[SiteCount * DecisionCount];

        private readonly long[] queryPlayers = new long[9];

        private readonly long[] preservedOriginalZf = new long[2];

        private readonly long[] foreignOriginalZf = new long[2];

        private readonly long[] accessRuleOriginalZf = new long[2];

        private long nativeRejectionPreserved, permittedComparisonPreserved;

        private long untrackedPolicyClear;

        private long untrackedRemovedRecord;
#endif

        private long untrackedUnexpectedGate;
#if SERP_DEVDIAG
        private long untrackedNonGate;
#endif

        private int pathfindingGlobalsCompared;
#if SERP_DEVDIAG
        private readonly CapturerSample[] samples =
            new CapturerSample[SiteCount * DecisionCount];
#endif

        internal EnemyGatePathfindingRuntime(ManualLogSource log, bool detailedDiagnostics = false, bool macemenDiagnostics = false)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            Shared.DevDiag.Attach(log);
#if SERP_DEVDIAG
            this.detailedDiagnostics = detailedDiagnostics;
            this.macemenDiagnostics = macemenDiagnostics;

            coverageCounters = detailedDiagnostics || macemenDiagnostics ||
                Shared.DebugLogHelper.DiagnosticsEnabled;
#endif
        }

        internal void ObserveBuildingCapture(BuildingCaptureEventArgs args)
        {
            if (args != null && args.BuildingId > 0)
            {
                DiagObserveCapture(args);
                topologyProvider?.RequestCaptureSnapshot(args.Phase == EventHookPhase.Post);
            }
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

            Shared.NativeResolution pclGraphCompareResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                GateRoutingNativeLayout.PclGraphCapturedByComparePattern,
                GateRoutingNativeLayout.PclGraphCapturedByFilterRva -
                    GateRoutingNativeLayout.PclGraphCapturedByCompareOffsetInPattern,
                referenceHashMatches: true,
                "PCL-graph hostile-gate captured-player comparison",
                log);
            Shared.NativeResolution builderPrecheckCompareResolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                GateRoutingNativeLayout.BuilderPrecheckCapturedByComparePattern,
                GateRoutingNativeLayout.BuilderPrecheckCapturedByFilterRva -
                    GateRoutingNativeLayout.BuilderPrecheckCapturedByCompareOffsetInPattern,
                referenceHashMatches: true,
                "builder-precheck hostile-gate captured-player comparison",
                log);
            int pclGraphFilterRva = pclGraphCompareResolution.Rva +
                GateRoutingNativeLayout.PclGraphCapturedByCompareOffsetInPattern;
            int builderPrecheckFilterRva = builderPrecheckCompareResolution.Rva +
                GateRoutingNativeLayout.BuilderPrecheckCapturedByCompareOffsetInPattern;
            if (pclGraphFilterRva != GateRoutingNativeLayout.PclGraphCapturedByFilterRva ||
                builderPrecheckFilterRva !=
                    GateRoutingNativeLayout.BuilderPrecheckCapturedByFilterRva)
                throw new InvalidOperationException("native gate signatures resolved outside their audited RVAs");

            libraryBase = unchecked((ulong)context.ModuleHandle.ToInt64());
            GateRoutingNativeLayout.ValidateComparisonSites(memory);
            ProbeExactHookLength(
                libraryBase,
                pclGraphFilterRva,
                GateRoutingNativeLayout.PclGraphCapturedByFilterHookLength,
                "PCL-graph captured-player filter");
            ProbeExactHookLength(
                libraryBase,
                builderPrecheckFilterRva,
                GateRoutingNativeLayout.BuilderPrecheckCapturedByFilterHookLength,
                "builder-precheck captured-player filter");
            topologyProvider = new GateTopologySnapshotProvider(log, detailedDiagnostics, deferredErrors);
            topologyProvider.SetGateAccessConsumer(UpdateGateAccess);
            DiagCreate();

            if (!GateRoutingNativeHooks.TryRequest(BugfixesAndQoLPlugin.PluginGuid, "gate-routing", context, log, out nativeHooks, out string nativeReason))
                throw new InvalidOperationException(nativeReason);
            nativeHooks.InstallComparisons(FilterUnrelatedCapturedEnemyGatePclGraph,FilterUnrelatedCapturedEnemyGateBuilderPrecheck);

            bool friendlyMoatHookOwnerLoaded =
                Chainloader.PluginInfos.ContainsKey("BugfixesAndQoL_Serp");
            try
            {
                object samePclDiagnostics = null;
                DiagSamePclObserver(ref samePclDiagnostics);
                samePclRouteRuntime = new SamePclGateRouteRuntime(
                    log, memory, context.Region, libraryBase, friendlyMoatHookOwnerLoaded,
                    samePclDiagnostics, nativeHooks, deferredErrors);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Integrated gate routing requires its complete Same-PCL path", ex);
            }
            DiagAttachSamePcl();
            topologyProvider.SetRoutePolicyConsumer(updated =>
                samePclRouteRuntime?.UpdatePolicy(updated));
            try { samePclRouteRuntime.InstallHooksEagerly(); }
            catch (Exception ex)
            {
                // Same fail-open outcome as a failed first publication: no gate masks are applied.
                Shared.DebugLogHelper.LogWarning(log,
                    $"Native gate-mask hooks could not be installed; gate masks remain inactive: {ex.GetType().Name}: {ex.Message}");
            }
            // TEMP_GATE_ROUTE_ACCEPTANCE: publish only after the existing initialization succeeds.
            DiagInstallNative(context);

            if (detailedDiagnostics) {
                                         if (Shared.DebugLogHelper.DiagnosticsEnabled)
                                             Shared.DebugLogHelper.LogDebug(log,
                "Crash-safe enemy-gate hooks installed: " +
                "capturerAdapter=post-cleanup-test, " +
                $"pclGraphCapturerFilter=0x{pclGraphFilterRva:X} " +
                $"({pclGraphCompareResolution.Method}+0x" +
                $"{GateRoutingNativeLayout.PclGraphCapturedByCompareOffsetInPattern:X}), " +
                $"pclGraphDisplaced={GateRoutingNativeLayout.PclGraphCapturedByFilterHookLength}, " +
                $"builderPrecheckCapturerFilter=0x{builderPrecheckFilterRva:X} " +
                $"({builderPrecheckCompareResolution.Method}+0x" +
                $"{GateRoutingNativeLayout.BuilderPrecheckCapturedByCompareOffsetInPattern:X}), " +
                $"builderPrecheckDisplaced={GateRoutingNativeLayout.BuilderPrecheckCapturedByFilterHookLength}, " +
                $"redBird={redBirdVersion}, " +
                $"dllSha256={GateRoutingNativeLayout.ReferenceSha256}. " +
                "Managed cursor searches, the whole PCL detour and every global " +
                "Direction-Grid write were removed.");
                                     }
            if (detailedDiagnostics && samePclRouteRuntime?.Installed != true && !friendlyMoatHookOwnerLoaded)
                {
                    if (Shared.DebugLogHelper.DiagnosticsEnabled)
                        Shared.DebugLogHelper.LogDebug(log,
                    "Same-PCL hook publication is waiting for the first non-empty gate direction mask; " +
                    "all searches remain fail-open until then.");
                }
        }

        internal void BeginMap(bool editor = false)
        {
            if (Volatile.Read(ref mapActive) != 0)
                EndMap("replacement before MissionStart");
            StartMapEpoch("MissionStart", editor);
        }

        private void StartMapEpoch(string reason, bool editor)
        {
            if (Interlocked.CompareExchange(ref mapActive, 1, 0) != 0) return;
            ResetMapCounters();
            samePclRouteRuntime?.ResetCounters();
            samePclRouteRuntime?.SetBuildingMapMode(editor);
            AssassinGatehouseDescentFeature.SetObservers(null, null);
            DiagBeginMap();
            topologyProvider?.BeginExplicitEpoch(reason);
            DiagBeginEpoch();
            if (Shared.DebugLogHelper.DiagnosticsEnabled)
            {
                // Keep captures inside the enabled scope; otherwise the compiler can allocate them before the guard.
                var diagnosticEditor = @editor;
                Shared.DebugLogHelper.LogDebug(log, () =>
                    $"Enemy-gate map started: epoch={topologyProvider?.DiagnosticEpoch}, editor={diagnosticEditor}, " +
                    $"direction masks={(samePclRouteRuntime?.Installed == true ? "active" :
                        samePclRouteRuntime != null ? "pending-policy" : "inactive")}.");
            }
        }

        internal void EndMap(string reason = "MissionEnd")
        {
            bool hadActiveEpoch = Interlocked.CompareExchange(ref mapActive, 0, 1) == 1;
            if (!hadActiveEpoch)
                return;
            AssassinGatehouseDescentFeature.SetObservers(null, null);
            FlushDeferredErrors(final: true);
#if SERP_DEVDIAG
            LogDiagnosticCheckpoint("final", reason);
#endif

            DiagEndMap();
            topologyProvider?.EndEpoch(reason);
            gateAccess = NativeGateAccessSnapshot.Empty;
        }

        internal void ProcessDeferredDiagnostics()
        {
            try
            {
                ComparePathfindingGlobalsOnce();
                DiagDeferred();

                FlushDeferredErrors();
#if SERP_DEVDIAG
                long now = Stopwatch.GetTimestamp();
                if (detailedDiagnostics && Volatile.Read(ref mapActive) != 0 &&
                    now >= Volatile.Read(ref nextDiagnosticAt))
                {

                    Volatile.Write(ref nextDiagnosticAt, now + DiagnosticInterval);

                    LogDiagnosticCheckpoint("periodic", "TEMP_GATE_ROUTE_ACCEPTANCE 60-second interval");

                }
#endif

            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure(ex);
            }
        }

        private void ComparePathfindingGlobalsOnce()
        {
            if (Interlocked.CompareExchange(ref pathfindingGlobalsCompared, 1, 0) != 0)
                return;

            try
            {
                CorrectedPathing pathing = CorrectedPathing.Instance;
                if (!pathing.IsAvailable)
                {
                    Shared.DebugLogHelper.LogWarning(log,
                        "APIShared pathfinding-global startup comparison unavailable: " +
                        pathing.UnavailabilityReason + "; the enemy-gate policy remains active and read-only.");
                    return;
                }
                Span<PathfindingProfile> nativeProfiles =
                    pathing.GetUnitTypePathfindingProfiles();
                Span<int> profiles = MemoryMarshal.Cast<PathfindingProfile, int>(nativeProfiles);
                Span<int> permissions =
                    pathing.GetUnitTypePathfindingConnectionClasses();
                PathfindingGlobalsComparison comparison =
                    PathfindingGlobalsBaseline.Compare(profiles, permissions);
                string counts = FormatPathfindingGlobalCounts(comparison);
                if (comparison.MatchesCanonical)
                {
                    if (detailedDiagnostics) {
                                                 if (Shared.DebugLogHelper.DiagnosticsEnabled)
                                                     Shared.DebugLogHelper.LogDebug(log,
                        "APIShared pathfinding globals match the canonical " +
                        $"FBCB9319 process-start tables: {counts}.");
                                             }
                    return;
                }

                if (comparison.ComparedValuesMatch && !comparison.HasExpectedLengths)
                {
                    Shared.DebugLogHelper.LogWarning(log,
                        "APIShared pathfinding-global coverage is incomplete or has an " +
                        "unexpected API extent; all compared values match the canonical FBCB9319 " +
                        $"tables. This is not evidence of table mutation: {counts}, " +
                        $"missingNativeCoverage={comparison.MissingCoverage}, " +
                        "nativeRowStride=90; policy unchanged.");
                    return;
                }

                Shared.DebugLogHelper.LogWarning(log,
                    "Compared APIShared pathfinding-global values differ from the canonical " +
                    "FBCB9319 process-start tables. Another component may have changed " +
                    "process-wide unit-type behavior; the enemy-gate policy remains " +
                    $"read-only and unchanged: {counts}, " +
                    $"samples=[{FormatPathfindingGlobalSamples(comparison.Samples)}].");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogWarning(log,
                    "APIShared pathfinding-global startup comparison failed; " +
                    $"the enemy-gate policy remains active and read-only: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static string FormatPathfindingGlobalCounts(
            PathfindingGlobalsComparison comparison)
        {
            var builder = new StringBuilder(256);
            builder.Append("profileLength=").Append(comparison.ProfileLength)
                .Append('/').Append(PathfindingGlobalsBaseline.UnitTypeCount)
                .Append(", permissionLength=").Append(comparison.PermissionLength)
                .Append('/').Append(PathfindingGlobalsBaseline.PermissionCount)
                .Append(", profileMismatches=").Append(comparison.ProfileMismatches)
                .Append(", permissionMismatches=").Append(comparison.PermissionMismatches)
                .Append(", invalidValues=").Append(comparison.InvalidValues)
                .Append(", profilesLarge/Default=").Append(comparison.LargeProfiles)
                .Append('/').Append(comparison.DefaultProfiles)
                .Append(", comparedClassAllowed=");
            for (int index = 0; index < comparison.AllowedByClass.Length; index++)
            {
                if (index != 0) builder.Append('/');
                builder.Append(comparison.AllowedByClass[index]);
            }
            builder.Append(", fingerprint=0x")
                .Append(comparison.ActualFingerprint.ToString("X16", CultureInfo.InvariantCulture))
                .Append(", expected=0x")
                .Append(comparison.ExpectedFingerprint.ToString("X16", CultureInfo.InvariantCulture));
            return builder.ToString();
        }

        private static string FormatPathfindingGlobalSamples(
            PathfindingGlobalMismatch[] samples)
        {
            if (samples == null || samples.Length == 0)
                return "none";
            var builder = new StringBuilder(samples.Length * 64);
            for (int index = 0; index < samples.Length; index++)
            {
                if (index != 0) builder.Append("; ");
                PathfindingGlobalMismatch sample = samples[index];
                builder.Append(sample.Kind == PathfindingGlobalTableKind.Profile
                        ? "profile"
                        : "class=" + ((PathConnectionClass)sample.ConnectionClass).ToString())
                    .Append("/unit=").Append(sample.UnitType).Append('/')
                    .Append(((eChimps)sample.UnitType).ToString())
                    .Append("/expected=").Append(sample.Expected)
                    .Append("/actual=").Append(sample.Actual);
            }
            return builder.ToString();
        }

        internal void OnGameTick()
        {
            long measured = 0;
            Shared.DevDiag.Start(ref measured);
            try { topologyProvider?.OnGameTick(); }
            catch { topologyProvider?.RecordSnapshotFailure(); }
            Shared.DevDiag.Stop(ref measured, "gate.tick.freshness");
        }

        // Functional gate-policy refresh. Driven by the simulation tick (the map editor,
        // which has no simulation or peers, drives it from render frames), so every peer
        // republishes access, topology and tribe ownership at the same simulation point.
        internal void ProcessGatePolicies(long now)
        {
            long measured = 0;
            Shared.DevDiag.Start(ref measured);
            try { topologyProvider?.ProcessTick(now); }
            catch { topologyProvider?.RecordSnapshotFailure(); }
            Shared.DevDiag.StopSpike(ref measured, "gate.tick.topology+access", 20000);
            Shared.DevDiag.Start(ref measured);
            try { samePclRouteRuntime?.ProcessTick(now); }
            catch (Exception ex) { TryLogDiagnosticFailure(ex); }
            Shared.DevDiag.StopSpike(ref measured, "gate.tick.tribes+publish", 20000);
        }

        internal void ObserveTargetOrder(TribeIssueOrderWithTargetEventArgs args)
        {
            DiagTargetOrder(args);
        }

        internal void ObserveTribeMove(TribeIssueOrderMoveHereEventArgs args)
        {
            DiagTribeMove(args);
        }

        internal void ObserveUnitMove(UnitMoveHereEventArgs args)
        {
            DiagUnitMove(args);
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
            IntPtr context)
        {
            // UPDATE REVIEW (CrusaderDE.dll): E2610 keeps query player in R14 here.
            MeasureCapturedEnemyGateFilter(context, false);
        }

        private void FilterUnrelatedCapturedEnemyGateBuilderPrecheck(
            IntPtr context)
        {
            // UPDATE REVIEW (CrusaderDE.dll): E2F60 keeps query player in RBP here.
            MeasureCapturedEnemyGateFilter(context, true);
        }

        // DEVDIAG measurement shell; without the symbol it reduces to the direct call.
        private void MeasureCapturedEnemyGateFilter(
            IntPtr context,
            bool builderPrecheck)
        {
            long measured = 0;
            Shared.DevDiag.Start(ref measured);
            FilterUnrelatedCapturedEnemyGate(context, builderPrecheck);
            Shared.DevDiag.Stop(ref measured, builderPrecheck ? "gate.compare.builder" : "gate.compare.pcl");
        }

        private void FilterUnrelatedCapturedEnemyGate(
            IntPtr context,
            bool builderPrecheck)
        {
            if (Volatile.Read(ref mapActive)==0) return;
            X64SmartCPUContext* registers = null;
            bool originalZeroKnown = false;
            bool originalZero = false;
            try
            {
#if SERP_DEVDIAG
                if (coverageCounters)
                    Interlocked.Increment(ref siteCalls[builderPrecheck ? 1 : 0]);
#endif

                registers = (X64SmartCPUContext*)context;
                if (registers == null)
                {
                    RecordDecision(builderPrecheck, NativeGateSnapshotDecision.Exception,
                        0, 0, 0, 0, 0, 0, 0, 0, default, 0, false, false,
                        gateAccess.TopologyFingerprint);
                    return;
                }

                // Native SETNE secured the real CMP result before any wrapper code.
                originalZero = (registers->R11 & 0xFFUL) == 0;
                originalZeroKnown = true;
                byte* captureBase = (byte*)(builderPrecheck ? registers->R13 : registers->RAX);
                ushort nativeCapturedByPlayerId = *(ushort*)(captureBase + registers->RDX +
                    GateRoutingNativeLayout.CapturedByPlayerTableDisplacement);

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
                    GateRoutingNativeLayout.RecordBuildingIdOffset);
                int ownerPlayerId = *(int*)(record +
                    GateRoutingNativeLayout.RecordOwnerPlayerIdOffset);
                uint subjectGlobalId = *(uint*)(record +
                    GateRoutingNativeLayout.RecordSubjectGlobalIdOffset);
                int firstPcl = *(int*)(record +
                    GateRoutingNativeLayout.RecordFirstPclOffset);
                int secondPcl = *(int*)(record +
                    GateRoutingNativeLayout.RecordSecondPclOffset);
                int thirdPcl = *(int*)(record +
                    GateRoutingNativeLayout.RecordThirdPclOffset);
                if (buildingId != recordBuildingId)
                {
                    RecordDecision(builderPrecheck,
                        NativeGateSnapshotDecision.RecordIdMismatch,
                        queryPlayerId, buildingId, recordBuildingId, ownerPlayerId,
                        nativeCapturedByPlayerId, firstPcl, secondPcl, thirdPcl, default,
                        unchecked((ushort)registers->RAX), originalZero,
                        originalZero, gateAccess.TopologyFingerprint, subjectGlobalId);
                    return;
                }

                NativeGateAccessSnapshot current = gateAccess;
                NativeGateAccessRecord snapshotRecord;
                NativeGateSnapshotDecision decision = current.Evaluate(
                    queryPlayerId, buildingId, ownerPlayerId, nativeCapturedByPlayerId,
                    out snapshotRecord);
                if (decision == NativeGateSnapshotDecision.CaptureMismatch)
                {
                    try { topologyProvider?.ObserveCaptureMismatch(current, buildingId,
                        subjectGlobalId, ownerPlayerId, nativeCapturedByPlayerId); }
                    catch { CountIntegrityError(ref callbackWarnings); }
                }
                if ((decision == NativeGateSnapshotDecision.ExcludeForeignCapture || decision == NativeGateSnapshotDecision.ExcludeAccessRule))
                {
                    registers->R11 &= ~0xFFUL;
                }
                RecordDecision(builderPrecheck, decision, queryPlayerId, buildingId,
                    recordBuildingId, ownerPlayerId, nativeCapturedByPlayerId,
                    firstPcl, secondPcl, thirdPcl, snapshotRecord,
                    unchecked((ushort)registers->RAX), originalZero,
                    (decision == NativeGateSnapshotDecision.ExcludeForeignCapture || decision == NativeGateSnapshotDecision.ExcludeAccessRule) || originalZero,
                    current.TopologyFingerprint, subjectGlobalId);
            }
            catch
            {
                // Restore the secured Vanilla Boolean; the emitter creates ZF later.
                if (registers != null && originalZeroKnown)
                    registers->R11 = (registers->R11 & ~0xFFUL) | (originalZero ? 0UL : 1UL);
                CountIntegrityError(ref callbackWarnings);
                try
                {
                    RecordDecision(builderPrecheck, NativeGateSnapshotDecision.Exception,
                        0, 0, 0, 0, 0, 0, 0, 0, default, 0, originalZero,
                        originalZero, gateAccess.TopologyFingerprint);
                }
                catch { /* A diagnostic failure must not escape the native callback. */ }
            }
        }

        private void CountDecision(ref long counter, NativeGateSnapshotDecision decision)
        {
            Interlocked.Increment(ref counter);
            // Publish the revision AFTER the counter, so a reader cannot consume a change too early.
            if (decision == NativeGateSnapshotDecision.RecordIdMismatch || decision == NativeGateSnapshotDecision.OwnerMismatch ||
                decision == NativeGateSnapshotDecision.InvalidQueryPlayer || decision == NativeGateSnapshotDecision.Exception)
                Interlocked.Increment(ref integrityRevision);
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
            ulong fingerprint,
            uint subjectGlobalId = 0)
        {
            if (Volatile.Read(ref mapActive) == 0)
                return;
            int decisionIndex = (int)decision;
            int site = builderPrecheck ? 1 : 0;
            if (!coverageCounters)
            {
                // Functional hot path: Preserve*/Exclude* are the overwhelming majority and
                // only feed Info checkpoints. Integrity categories stay counted for warnings.
                if ((int)decision <= (int)NativeGateSnapshotDecision.ExcludeAccessRule)
                    return;
                CountDecision(ref decisions[(site * DecisionCount) + decisionIndex], decision);
                if (decision == NativeGateSnapshotDecision.UntrackedConnection)
                {
                    NativeGateAccessSnapshot current = gateAccess;
                    if (current.TrackedRecords != 0 &&
                        !HasStableRecord(previousStableGateAccess, buildingId) &&
                        current.MatchesGateIdentity(buildingId, subjectGlobalId))
                        CountIntegrityError(ref untrackedUnexpectedGate);
                }
                return;
            }
#if SERP_DEVDIAG
            CountDecision(ref decisions[(site * DecisionCount) + decisionIndex], decision);
            if (queryPlayerId > 0 && queryPlayerId < queryPlayers.Length)
                Interlocked.Increment(ref queryPlayers[queryPlayerId]);
            if ((int)decision <= (int)NativeGateSnapshotDecision.PreserveCapturerAlly)
                Interlocked.Increment(ref preservedOriginalZf[originalZero ? 1 : 0]);
            if (decision == NativeGateSnapshotDecision.ExcludeForeignCapture)
                Interlocked.Increment(ref foreignOriginalZf[originalZero ? 1 : 0]);
            if (decision == NativeGateSnapshotDecision.ExcludeAccessRule)
                Interlocked.Increment(ref accessRuleOriginalZf[originalZero ? 1 : 0]);
            if ((int)decision <= (int)NativeGateSnapshotDecision.ExcludeAccessRule)
            {
                if (originalZero && finalZero) Interlocked.Increment(ref nativeRejectionPreserved);
                if (GateSearchEvidence.Comparison(originalZero, finalZero, decision) == "permitted-acceptance-preserved")
                    Interlocked.Increment(ref permittedComparisonPreserved);
            }
            if (decision == NativeGateSnapshotDecision.UntrackedConnection)
            {
                NativeGateAccessSnapshot live = gateAccess;
                if (live.TrackedRecords == 0)
                    Interlocked.Increment(ref untrackedPolicyClear);
                else if (HasStableRecord(previousStableGateAccess, buildingId))
                    Interlocked.Increment(ref untrackedRemovedRecord);
                else if (live.MatchesGateIdentity(buildingId, subjectGlobalId))
                    CountIntegrityError(ref untrackedUnexpectedGate);
                else
                    Interlocked.Increment(ref untrackedNonGate);
            }
            DiagGateComparison(builderPrecheck, decision, queryPlayerId, buildingId, nativeOwner,
                nativeCaptured, originalZero, finalZero, subjectGlobalId);

            if (!detailedDiagnostics) return;
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
            sample.SubjectGlobalId = subjectGlobalId;
            Volatile.Write(ref sample.State, 2);
#endif
        }

        private void TryLogDiagnosticFailure(Exception ex)
        {
            CountIntegrityError(ref callbackWarnings);
            deferredErrors.Record(ex.GetType().Name, ex.Message);
        }

        private string DescribeIntegrityErrors(SamePclCoverageSnapshot same, TopologyCoverageSnapshot topology) =>
            $"callback={Volatile.Read(ref callbackWarnings)},snapshot={topology.Errors},exceptions={same.Exceptions}," +
            $"slots={same.SlotConflicts},scope={same.ScopeMismatches},player={same.InvalidPlayers},pool={same.PoolExhaustions}," +
            $"tacticalPlayer={same.AiTacticalInvalidPlayers},tacticalScope={same.AiTacticalScopeConflicts},tacticalExceptions={same.AiTacticalExceptions}," +
            $"identity={DecisionTotal(NativeGateSnapshotDecision.RecordIdMismatch)},owner={DecisionTotal(NativeGateSnapshotDecision.OwnerMismatch)}," +
            $"queryPlayer={DecisionTotal(NativeGateSnapshotDecision.InvalidQueryPlayer)},nativeException={DecisionTotal(NativeGateSnapshotDecision.Exception)}," +
            $"unexpectedGate={Read(ref untrackedUnexpectedGate)},invalidPlayerContext=[{samePclRouteRuntime?.InvalidPlayerContext ?? "unavailable"}]";

        private void FlushDeferredErrors(bool final = false)
        {
            foreach (string error in deferredErrors.DrainNewCauses())
                Shared.DebugLogHelper.LogWarning(log, "Enemy-gate deferred failure: " + error);
            if (detailedDiagnostics || (!final && Volatile.Read(ref mapActive) == 0)) return;
            long ownRevision = SupportIntegrityRevision;
            long sameRevision = samePclRouteRuntime?.SupportIntegrityRevision ?? 0;
            long topologyRevision = topologyProvider?.SupportIntegrityRevision ?? 0;
            if (!final && !integrityFlush.HasChanges(ownRevision, sameRevision, topologyRevision)) return;
            if (!integrityFlush.TryReserve(ownRevision, sameRevision, topologyRevision,
                Stopwatch.GetTimestamp(), Stopwatch.Frequency, final)) return;
            SamePclCoverageSnapshot same = samePclRouteRuntime?.GetCoverageSnapshot() ?? default;
            TopologyCoverageSnapshot topology = topologyProvider?.GetCoverageSnapshot() ?? default;
            // Only a newly observed category emits a warning; repetitions remain in the final totals.
            uint causes = (topology.Errors > 0 ? 1U : 0) |
                (same.Exceptions > 0 ? 2U : 0) | (same.SlotConflicts > 0 ? 4U : 0) |
                (same.ScopeMismatches > 0 ? 8U : 0) | (same.InvalidPlayers > 0 ? 16U : 0) |
                (same.PoolExhaustions > 0 ? 32U : 0) | (same.AiTacticalInvalidPlayers > 0 ? 64U : 0) |
                (same.AiTacticalScopeConflicts > 0 ? 128U : 0) | (same.AiTacticalExceptions > 0 ? 256U : 0) |
                (DecisionTotal(NativeGateSnapshotDecision.RecordIdMismatch) > 0 ? 512U : 0) |
                (DecisionTotal(NativeGateSnapshotDecision.OwnerMismatch) > 0 ? 1024U : 0) |
                (DecisionTotal(NativeGateSnapshotDecision.InvalidQueryPlayer) > 0 ? 2048U : 0) |
                (DecisionTotal(NativeGateSnapshotDecision.Exception) > 0 ? 4096U : 0) |
                (Read(ref untrackedUnexpectedGate) > 0 ? 8192U : 0) |
                (Volatile.Read(ref callbackWarnings) > 0 ? 16384U : 0);
            if ((causes & ~reportedIntegrityErrors) != 0 || (final && causes != 0))
                Shared.DebugLogHelper.LogWarning(log, "Enemy-gate integrity errors (complete totals at map end): " + DescribeIntegrityErrors(same, topology));
            reportedIntegrityErrors |= causes;
        }

        private void ResetMapCounters()
        {
            deferredErrors.Reset();
            reportedIntegrityErrors = 0;
            integrityFlush.Reset(SupportIntegrityRevision,
                samePclRouteRuntime?.SupportIntegrityRevision ?? 0,
                topologyProvider?.SupportIntegrityRevision ?? 0);
            previousStableGateAccess = NativeGateAccessSnapshot.Empty;
#if SERP_DEVDIAG
            Array.Clear(siteCalls, 0, siteCalls.Length);

            Array.Clear(lastSiteCalls, 0, lastSiteCalls.Length);
#endif

            Array.Clear(decisions, 0, decisions.Length);
#if SERP_DEVDIAG
            Array.Clear(lastDecisions, 0, lastDecisions.Length);

            Array.Clear(queryPlayers, 0, queryPlayers.Length);

            Array.Clear(preservedOriginalZf, 0, preservedOriginalZf.Length);

            Array.Clear(foreignOriginalZf, 0, foreignOriginalZf.Length);

            Array.Clear(accessRuleOriginalZf, 0, accessRuleOriginalZf.Length);

            Interlocked.Exchange(ref nativeRejectionPreserved, 0);

            Interlocked.Exchange(ref permittedComparisonPreserved, 0);

            Reset(ref untrackedPolicyClear);

            Reset(ref untrackedRemovedRecord);
#endif

            Reset(ref untrackedUnexpectedGate);
#if SERP_DEVDIAG
            Reset(ref untrackedNonGate);

            Array.Clear(samples, 0, samples.Length);
#endif

            Interlocked.Exchange(ref callbackWarnings, 0);
#if SERP_DEVDIAG
            Volatile.Write(ref nextDiagnosticAt, Stopwatch.GetTimestamp() + DiagnosticInterval);
#endif

        }

        private static long Read(ref long value) => Interlocked.Read(ref value);
#if SERP_DEVDIAG
        private void LogDiagnosticCheckpoint(string kind, string reason)
        {
            FlushDeferredErrors();
            if (!detailedDiagnostics)
            {
                foreach (string pending in topologyProvider?.CaptureDiagnostics.UnresolvedDetails() ?? Array.Empty<string>())
                    Shared.DebugLogHelper.LogWarning(log, "Enemy-gate unresolved capture: " + pending);
                LogAcceptanceVerdict(reason);
                return;
            }
            if (topologyProvider != null)
            {
                foreach (string change in topologyProvider.CaptureDiagnostics.DrainChanges())
                    {
                        if (Shared.DebugLogHelper.DiagnosticsEnabled)
                            Shared.DebugLogHelper.LogDebug(log, "Enemy-gate capture transition: " + change + ".");
                    }
                if (Shared.DebugLogHelper.DiagnosticsEnabled)
                        Shared.DebugLogHelper.LogDebug(log, "Enemy-gate capture recovery: " +
                    topologyProvider.CaptureDiagnostics.Summary + ".");
            }
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
            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                    Shared.DebugLogHelper.LogDebug(log,
                $"Enemy-gate diagnostic checkpoint: kind={kind}, reason={reason}, " +
                $"sites(pclGraph={pcl}(+{pclDelta}),builderPrecheck={builder}(+{builderDelta})), " +
                $"coverageBySite(pclGraph=[{FormatSiteCoverage(0)}]," +
                $"builderPrecheck=[{FormatSiteCoverage(1)}]), players({players}), " +
                $"preservedByOriginalZf(zf0={Read(ref preservedOriginalZf[0])},zf1={Read(ref preservedOriginalZf[1])}), " +
                $"foreignBlocked(changedZf={Read(ref foreignOriginalZf[0])},alreadyExcluded={Read(ref foreignOriginalZf[1])}), " +
                $"untracked(policyClear={Read(ref untrackedPolicyClear)},removedRecord={Read(ref untrackedRemovedRecord)}," +
                $"nonGate={Read(ref untrackedNonGate)},unexpectedGate={Read(ref untrackedUnexpectedGate)}), " +
                $"callbackWarnings={Volatile.Read(ref callbackWarnings)}.");
            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                    Shared.DebugLogHelper.LogDebug(log,
                $"Enemy-gate snapshot checkpoint: {topologyProvider?.DescribeState() ?? "unavailable"}.");
            SamePclCoverageSnapshot same = samePclRouteRuntime?.GetCoverageSnapshot() ?? default;
            double cursorAverageMs = same.CursorNativeRefreshes == 0 ? 0.0 :
                same.CursorValidationTicks * 1000.0 /
                Stopwatch.Frequency / same.CursorNativeRefreshes;
            double cursorMaximumMs = same.CursorValidationMaxTicks * 1000.0 /
                Stopwatch.Frequency;
            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                    Shared.DebugLogHelper.LogDebug(log,
                $"Enemy-gate Same-PCL checkpoint: kind={kind}, installed={same.Installed}," +
                $"sharedHookOwner={same.OwnerConflict},queries={same.Queries}," +
                $"searchWithoutRejectedEdges={same.Preserved},edgeRejected={same.RejectedEdges}," +
                $"successWithRejectedEdges={same.SuccessWithRejectedEdges},negativeWithRejectedEdges={same.NegativeWithRejectedEdges}," +
                $"negativeWithoutRejectedEdges={same.NegativeWithoutRejectedEdges},incompleteSearches={same.IncompleteSearches}," +
                $"humanSuccessWithRejectedEdges={same.HumanSuccessWithRejectedEdges},aiSuccessWithRejectedEdges={same.AiSuccessWithRejectedEdges}," +
                $"attackEdgesFiltered={same.AttackEdges},buildingEdgesFiltered={same.BuildingEdges}," +
                $"candidateEdgesFiltered={same.CandidateEdges},cursorCommandEdgesFiltered={same.CursorCommandEdges}," +
                $"directCursorEdgesFiltered={same.DirectCursorEdges}," +
                $"cursorPclChecks={same.CursorPclChecks}," +
                $"cursorWrapperCalls={same.CursorPclWrapperCalls}," +
                $"cursorSamePclEligible={same.CursorSamePclEligible}," +
                $"cursorDifferentPcl={same.CursorDifferentPcl}," +
                $"cursorDifferentPclEligible={same.CursorDifferentPclEligible}," +
                $"cursorValidationRequests={same.CursorValidationRequests}," +
                $"cursorResultForcedZero={same.CursorResultForcedZero}," +
                $"cursorNativeRefreshes={same.CursorNativeRefreshes}," +
                $"cursorCacheHits={same.CursorCacheHits}," +
                $"cursorExactCacheHits={same.CursorExactCacheHits}," +
                $"cursorReferenceNoRoutes={same.CursorReferenceNoRoutes}," +
                $"cursorProvenPolicyBlocks={same.CursorProvenPolicyBlocks}," +
                $"cursorValidationDeferrals={same.CursorValidationDeferrals}," +
                $"cursorPolicyBlocked={same.CursorPolicyBlocked}," +
                $"cursorReachable={same.CursorReachable}," +
                $"cursorRejectedEdges={same.CursorRejectedEdges}," +
                $"cursorUnitPending={same.CursorUnitPending}," +
                $"cursorValidationMs(avg={cursorAverageMs.ToString("F3", CultureInfo.InvariantCulture)}," +
                $"max={cursorMaximumMs.ToString("F3", CultureInfo.InvariantCulture)})," +
                $"missingContext={same.MissingContexts},invalidPlayer={same.InvalidPlayers}," +
                $"scopeMismatch={same.ScopeMismatches},threadSlotConflict={same.SlotConflicts}," +
                $"snapshotPoolExhaustion={same.PoolExhaustions},exceptions={same.Exceptions}," +
                $"aiQueries={same.AiQueries},aiNegativeWithRejectedEdges={same.AiNegativeWithRejectedEdges}," +
                $"aiNegativeWithoutRejectedEdges={same.AiNegativeWithoutRejectedEdges},counterfactualVanillaRoute=not-measured," +
                $"attackApproachQueries={same.AttackQueries},buildingApproachQueries={same.BuildingApproachQueries}," +
                $"buildingConsumerQueries={same.BuildingConsumerQueries}," +
                $"alternateBuildingConsumerQueries={same.AlternateBuildingConsumerQueries}," +
                $"candidateQueries={same.CandidateQueries},cursorCommandQueries={same.CursorCommandQueries}," +
                $"plannerState0x419Queries={same.AiTacticalTargetQueries}," +
                $"plannerState0x419BuildingEdges={same.AiTacticalBuildingEdges}," +
                $"plannerState0x419UnitEdges={same.AiTacticalUnitEdges}," +
                $"plannerState0x419FallbackEdges={same.AiTacticalFallbackEdges}," +
                $"plannerState0x419InvalidPlayer={same.AiTacticalInvalidPlayers}," +
                $"plannerState0x419ScopeConflict={same.AiTacticalScopeConflicts}," +
                $"plannerState0x419Exceptions={same.AiTacticalExceptions}," +
                $"scopeSamples=[{samePclRouteRuntime?.DescribeScopeSamples() ?? "none"}]," +
                $"aiTacticalEdgeSamples=[{samePclRouteRuntime?.DescribeAiTacticalEdgeSamples() ?? "none"}]," +
                $"cursorDecisionSample=[{samePclRouteRuntime?.DescribeCursorDecisionSample() ?? "none"}]," +
                $"cursorPreviewSample=[{samePclRouteRuntime?.DescribeCursorPreviewSample() ?? "none"}]," +
                "managedCursorSearches=0,managedReplacementSearches=0,directionGridWrites=0.");
            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                    Shared.DebugLogHelper.LogDebug(log,
                $"Enemy-gate AI order checkpoint: kind={kind}, " +
                DescribeOrderCheckpoint() +
                ",plannerState0x419Queries=" + same.AiTacticalTargetQueries +
                ",plannerState0x419Edges=" +
                (same.AiTacticalBuildingEdges + same.AiTacticalUnitEdges +
                    same.AiTacticalFallbackEdges) + ".");
            LogNewCapturerSamples();
            if (string.Equals(kind, "final", StringComparison.Ordinal))
                LogAcceptanceVerdict(reason);
        }

        private void LogAcceptanceVerdict(string reason)
        {
            // Both variants are Info-only; skip building them when Info is suppressed.
            if (!Shared.DebugLogHelper.DiagnosticsEnabled) return;
            TopologyCoverageSnapshot topology = topologyProvider?.GetCoverageSnapshot() ?? default;
            SamePclCoverageSnapshot same = samePclRouteRuntime?.GetCoverageSnapshot() ?? default;
            long uncaptured = DecisionTotal(NativeGateSnapshotDecision.PreserveUncaptured);
            long ownCapturer = DecisionTotal(NativeGateSnapshotDecision.PreserveCapturer);
            long alliedCapturer = DecisionTotal(NativeGateSnapshotDecision.PreserveCapturerAlly);
            long foreignCapture = DecisionTotal(NativeGateSnapshotDecision.ExcludeForeignCapture);
            long policyFailures =
                DecisionTotal(NativeGateSnapshotDecision.InvalidQueryPlayer) +
                DecisionTotal(NativeGateSnapshotDecision.RecordIdMismatch) +
                DecisionTotal(NativeGateSnapshotDecision.OwnerMismatch) +
                Math.Max(0, DecisionTotal(NativeGateSnapshotDecision.CaptureMismatch) -
                    (topologyProvider?.CaptureDiagnostics.Recovered ?? 0)) +
                DecisionTotal(NativeGateSnapshotDecision.Exception);
            bool hookActivity = Read(ref siteCalls[0]) > 0 && Read(ref siteCalls[1]) > 0;
            bool runtimeFailed = Volatile.Read(ref callbackWarnings) != 0 ||
                topology.Errors != 0 ||
                same.Exceptions != 0 || same.SlotConflicts != 0 || same.ScopeMismatches != 0 ||
                same.InvalidPlayers != 0 || same.PoolExhaustions != 0 ||
                same.AiTacticalInvalidPlayers != 0 ||
                same.AiTacticalScopeConflicts != 0 || same.AiTacticalExceptions != 0 ||
                Read(ref untrackedUnexpectedGate) != 0 || policyFailures != 0;
            if (!detailedDiagnostics)
            {
                if (Shared.DebugLogHelper.DiagnosticsEnabled)
                        Shared.DebugLogHelper.LogDebug(log,
                    $"Enemy-gate map completed: epoch={topologyProvider?.DiagnosticEpoch}, reason={reason}, " +
                    $"queries={same.Queries}, aiQueries={same.AiQueries}, aiNegativeWithRejectedEdges={same.AiNegativeWithRejectedEdges}, " +
                    $"aiNegativeWithoutRejectedEdges={same.AiNegativeWithoutRejectedEdges}, incompleteSearches={same.IncompleteSearches}, " +
                    $"nativeRejectionPreserved={Read(ref nativeRejectionPreserved)}, foreignCaptureAcceptanceRejected={Read(ref foreignOriginalZf[0])},accessRuleAcceptanceRejected={Read(ref accessRuleOriginalZf[0])}, " +
                    $"permittedComparisonPreserved={Read(ref permittedComparisonPreserved)}, " +
                    $"edgeRejected={same.RejectedEdges}, cursorBlocks={same.CursorResultForcedZero}, " +
                    $"capture=[{topologyProvider?.CaptureDiagnostics.Summary ?? "unavailable"}], " +
                    $"errors=[{(runtimeFailed ? DescribeIntegrityErrors(same, topology) : "none")}], " +
                    (string.IsNullOrEmpty(deferredErrors.Summary) ? "" : $"causes=[{deferredErrors.Summary}], ") +
                    $"runtimeIntegrity={EnemyGatePathfindingPolicy.IntegrityVerdict(hookActivity || same.Queries > 0, runtimeFailed)}, " +
                    "Raid/Assassin routes=NOT_INSPECTED (DetailedDiagnostics=false).");
                return;
            }
            DiagnosticVerdict sameHookVerdict = same.OwnerConflict
                ? DiagnosticVerdict.NOT_APPLICABLE
                : !same.Installed ? DiagnosticVerdict.FAIL
                : EnemyGatePathfindingPolicy.IntegrityVerdict(same.Queries > 0, false);

            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                    Shared.DebugLogHelper.LogDebug(log,
                "Enemy-gate acceptance verdict: " +
                $"capturerComparisonExecution={EnemyGatePathfindingPolicy.ObservationVerdict(hookActivity ? 1 : 0)}," +
                $"runtimeIntegrity={EnemyGatePathfindingPolicy.IntegrityVerdict(hookActivity || same.Queries > 0, runtimeFailed)}," +
                $"uncapturedGateObserved={EnemyGatePathfindingPolicy.ObservationVerdict(uncaptured)}," +
                $"nativeRejectionPreserved={Read(ref nativeRejectionPreserved)}," +
                $"foreignCaptureAcceptanceRejected={Read(ref foreignOriginalZf[0])},accessRuleAcceptanceRejected={Read(ref accessRuleOriginalZf[0])},permittedComparisonPreserved={Read(ref permittedComparisonPreserved)}," +
                "counterfactualVanillaRoute=not-measured,observedIsNotFunctionalPass=true," +
                $"capturedGate={EnemyGatePathfindingPolicy.ObservationVerdict(topology.PeakCaptured)}," +
                $"ownerAtHook={DiagnosticVerdict.NOT_APPLICABLE}," +
                $"ownerAllyAtHook={DiagnosticVerdict.NOT_APPLICABLE}," +
                $"ownCapturer={EnemyGatePathfindingPolicy.ObservationVerdict(ownCapturer)}," +
                $"alliedCapturer={EnemyGatePathfindingPolicy.ObservationVerdict(alliedCapturer)}," +
                $"foreignCapturer={EnemyGatePathfindingPolicy.ObservationVerdict(foreignCapture)}," +
                $"foreignCaptureBlock={EnemyGatePathfindingPolicy.ObservationVerdict(Read(ref foreignOriginalZf[0]))}," +
                $"nativeCursorScope={EnemyGatePathfindingPolicy.ObservationVerdict(same.DirectCursorQueries)}," +
                $"nativeCursorEdgesFiltered={EnemyGatePathfindingPolicy.ObservationVerdict(same.DirectCursorEdges)}," +
                $"cursorPreviewExecution={EnemyGatePathfindingPolicy.ObservationVerdict(same.CursorPclChecks)}," +
                $"cursorWrapperExecution={EnemyGatePathfindingPolicy.ObservationVerdict(same.CursorPclWrapperCalls)}," +
                $"cursorDifferentPclExecution={EnemyGatePathfindingPolicy.ObservationVerdict(same.CursorDifferentPclEligible)}," +
                $"cursorReferenceNoRoute={EnemyGatePathfindingPolicy.ObservationVerdict(same.CursorReferenceNoRoutes)}," +
                $"cursorProvenPolicyBlock={EnemyGatePathfindingPolicy.ObservationVerdict(same.CursorProvenPolicyBlocks)}," +
                $"cursorResultForcedZero={EnemyGatePathfindingPolicy.ObservationVerdict(same.CursorResultForcedZero)}," +
                $"cursorPreviewRefresh={EnemyGatePathfindingPolicy.ObservationVerdict(same.CursorNativeRefreshes)}," +
                $"cursorPreviewPolicyBlocked={EnemyGatePathfindingPolicy.ObservationVerdict(same.CursorPolicyBlocked)}," +
                $"cursorPreviewReachable={EnemyGatePathfindingPolicy.ObservationVerdict(same.CursorReachable)}," +
                $"samePclHookExecution={sameHookVerdict}," +
                $"searchWithoutRejectedEdges={EnemyGatePathfindingPolicy.ObservationVerdict(same.Preserved)}," +
                $"edgeRejected={EnemyGatePathfindingPolicy.ObservationVerdict(same.RejectedEdges)}," +
                $"successWithRejectedEdges={EnemyGatePathfindingPolicy.ObservationVerdict(same.SuccessWithRejectedEdges)}," +
                $"negativeWithRejectedEdges={EnemyGatePathfindingPolicy.ObservationVerdict(same.NegativeWithRejectedEdges)}," +
                $"humanSuccessWithRejectedEdges={EnemyGatePathfindingPolicy.ObservationVerdict(same.HumanSuccessWithRejectedEdges)}," +
                $"aiSuccessWithRejectedEdges={EnemyGatePathfindingPolicy.ObservationVerdict(same.AiSuccessWithRejectedEdges)}," +
                $"aiQuery={EnemyGatePathfindingPolicy.ObservationVerdict(same.AiQueries)}," +
                $"aiNegativeWithRejectedEdges={EnemyGatePathfindingPolicy.ObservationVerdict(same.AiNegativeWithRejectedEdges)}," +
                $"plannerState0x419Execution={EnemyGatePathfindingPolicy.ObservationVerdict(same.AiTacticalTargetQueries)}," +
                $"plannerState0x419InteriorEdges={EnemyGatePathfindingPolicy.ObservationVerdict(same.AiTacticalBuildingEdges + same.AiTacticalUnitEdges)}," +
                $"plannerState0x419FallbackEdges={EnemyGatePathfindingPolicy.ObservationVerdict(same.AiTacticalFallbackEdges)}," +
                $"attackEdgesFiltered={EnemyGatePathfindingPolicy.ObservationVerdict(same.AttackEdges)}," +
                $"buildingEdgesFiltered={EnemyGatePathfindingPolicy.ObservationVerdict(same.BuildingEdges)}," +
                $"candidateEdgesFiltered={EnemyGatePathfindingPolicy.ObservationVerdict(same.CandidateEdges)}," +
                $"cursorCommandEdgesFiltered={EnemyGatePathfindingPolicy.ObservationVerdict(same.CursorCommandEdges)}," +
                $"threadSlotIntegrity={EnemyGatePathfindingPolicy.IntegrityVerdict(same.Queries > 0, same.SlotConflicts > 0)}," +
                $"drawbridgeAssociation={EnemyGatePathfindingPolicy.ObservationVerdict(topology.DrawbridgeObserved ? 1 : 0)}," +
                $"lifecycle={DiagnosticVerdict.PASS}," +
                $"captureTransitions={topology.CaptureTransitions},recaptureTransitions={topology.RecaptureTransitions}," +
                $"untrackedTransitionCalls={DecisionTotal(NativeGateSnapshotDecision.UntrackedConnection)}," +
                $"untrackedPolicyClear={Read(ref untrackedPolicyClear)}," +
                $"untrackedRemovedRecord={Read(ref untrackedRemovedRecord)}," +
                $"untrackedNonGate={Read(ref untrackedNonGate)}," +
                $"untrackedUnexpectedGate={Read(ref untrackedUnexpectedGate)}," +
                $"causes=[{deferredErrors.Summary}],reason={reason}.");
        }
#endif

        private long DecisionTotal(NativeGateSnapshotDecision decision) =>
            Read(ref decisions[(int)decision]) +
            Read(ref decisions[DecisionCount + (int)decision]);

        private static bool HasStableRecord(NativeGateAccessSnapshot snapshot, int buildingId) =>
            snapshot != null && buildingId > 0 &&
            buildingId < snapshot.RecordsByBuildingId.Length &&
            snapshot.RecordsByBuildingId[buildingId].Valid;

        private static void Reset(ref long value) => Interlocked.Exchange(ref value, 0);
#if SERP_DEVDIAG
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
                    decision <= (int)NativeGateSnapshotDecision.ExcludeAccessRule)
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
                if (Shared.DebugLogHelper.DiagnosticsEnabled)
                        Shared.DebugLogHelper.LogDebug(log,
                    $"Enemy-gate first capturer sample: site={(sample.Site == 0 ? "pclGraph" : "builderPrecheck")}, " +
                    $"decision={(NativeGateSnapshotDecision)sample.Decision}, queryPlayer={sample.QueryPlayer}, " +
                    $"building={sample.BuildingId}, recordBuilding={sample.RecordBuildingId}, " +
                    $"subjectGlobal={sample.SubjectGlobalId}, " +
                    $"nativeOwner={sample.NativeOwner}, snapshotOwner={sample.SnapshotOwner}, " +
                    $"nativeCaptured={sample.NativeCaptured}, snapshotCaptured={sample.SnapshotCaptured}, " +
                    $"portalPcls={sample.FirstPcl}/{sample.SecondPcl}/{sample.ThirdPcl}, " +
                    $"compareValue={sample.CompareValue}, nativeCompareEqual={sample.OriginalZero}, branchReject={sample.FinalZero}, adapter=post-cleanup-test, " +
                    $"accessFingerprint=0x{sample.Fingerprint:X16}.");
            }
        }
#endif

        // TEMP_GATE_MIGRATION_DIAGNOSTICS: implemented only in EnemyGatePathfindingRuntime.DevDiag.cs.
        // Without SERP_DEVDIAG the compiler removes every call including its arguments.
        partial void DiagObserveCapture(BuildingCaptureEventArgs args);
        partial void DiagCreate();
        partial void DiagSamePclObserver(ref object observer);
        partial void DiagAttachSamePcl();
        partial void DiagInstallNative(CrusaderLibraryLoadContext context);
        partial void DiagBeginMap();
        partial void DiagBeginEpoch();
        partial void DiagEndMap();
        partial void DiagDeferred();
        partial void DiagTargetOrder(TribeIssueOrderWithTargetEventArgs args);
        partial void DiagTribeMove(TribeIssueOrderMoveHereEventArgs args);
        partial void DiagUnitMove(UnitMoveHereEventArgs args);
        partial void DiagGateComparison(bool builderPrecheck, NativeGateSnapshotDecision decision,
            int queryPlayerId, int buildingId, int nativeOwner, int nativeCaptured,
            bool originalZero, bool finalZero, uint subjectGlobalId);
        partial void DiagDescribeOrderCheckpoint(ref string text);
#if SERP_DEVDIAG
        private string DescribeOrderCheckpoint()
        {
            string text = "unavailable";
            DiagDescribeOrderCheckpoint(ref text);
            return text;
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
            internal uint SubjectGlobalId;
        }
#endif

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
