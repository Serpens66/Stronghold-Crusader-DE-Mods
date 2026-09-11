using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace EnemyGatePathfindingTest
{
    internal readonly struct TopologyCoverageSnapshot
    {
        internal TopologyCoverageSnapshot(int errors, long accessScans, long accessChanges,
            long accessRepublishes, long suppressedRawChanges, int peakTracked,
            int peakCaptured, int peakBlockedPairs, long captureTransitions,
            long recaptureTransitions, bool drawbridgeObserved)
        {
            Errors = errors; AccessScans = accessScans; AccessChanges = accessChanges;
            AccessRepublishes = accessRepublishes; SuppressedRawChanges = suppressedRawChanges;
            PeakTracked = peakTracked; PeakCaptured = peakCaptured;
            PeakBlockedPairs = peakBlockedPairs; CaptureTransitions = captureTransitions;
            RecaptureTransitions = recaptureTransitions; DrawbridgeObserved = drawbridgeObserved;
        }

        internal int Errors { get; }
        internal long AccessScans { get; }
        internal long AccessChanges { get; }
        internal long AccessRepublishes { get; }
        internal long SuppressedRawChanges { get; }
        internal int PeakTracked { get; }
        internal int PeakCaptured { get; }
        internal int PeakBlockedPairs { get; }
        internal long CaptureTransitions { get; }
        internal long RecaptureTransitions { get; }
        internal bool DrawbridgeObserved { get; }
    }

    // Builds immutable policy snapshots outside native callbacks.
    internal sealed unsafe class GateTopologySnapshotProvider
    {
        private const int MaximumErrorsPerCategory = 8;
        private static readonly long TopologySafetyInterval = Math.Max(1, Stopwatch.Frequency);

        private readonly ManualLogSource log;
        private readonly object snapshotLock = new object();

        private volatile TopologySnapshot snapshot = TopologySnapshot.Empty;
        private volatile NativeGateAccessSnapshot accessSnapshot = NativeGateAccessSnapshot.Empty;
        private TopologySnapshot lastStableTopologySnapshot = TopologySnapshot.Empty;
        private NativeGateAccessSnapshot lastStableAccessSnapshot = NativeGateAccessSnapshot.Empty;
        private Action<RouteTilePolicySnapshot> routePolicyConsumer;
        private Action<NativeGateAccessSnapshot> gateAccessConsumer;
        private int epochActive;
        private int epochNumber;
        private int initializedDeferredEpoch;
        private int accessRefreshRequired;
        private int accessPolicyWasCleared;
        private int routePolicyWasCleared;
        private string pendingEpochReason = "map start";
        private long nextSnapshotAt;
        private ulong lastAccessFingerprint;
        private ulong lastTopologySignature;
        private ulong lastTopologyFingerprint;
        private int snapshotErrors;
        private long accessScans;
        private long accessChanges;
        private long accessRepublishes;
        private long suppressedRawAccessChanges;
        private long topologyBuilds;
        private long topologyChanges;
        private long captureTransitions;
        private long recaptureTransitions;
        private long peakTrackedRecords;
        private long peakCapturedRecords;
        private long peakBlockedPairs;
        private int drawbridgeObserved;

        internal GateTopologySnapshotProvider(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        internal void SetRoutePolicyConsumer(Action<RouteTilePolicySnapshot> consumer)
        {
            routePolicyConsumer = consumer;
            consumer?.Invoke(snapshot.RoutePolicy);
        }

        internal void SetGateAccessConsumer(Action<NativeGateAccessSnapshot> consumer)
        {
            gateAccessConsumer = consumer;
            consumer?.Invoke(accessSnapshot);
        }

        internal void BeginExplicitEpoch(string reason)
        {
            if (Interlocked.CompareExchange(ref epochActive, 1, 0) != 0)
                return;
            pendingEpochReason = reason ?? "unspecified";
            Interlocked.Increment(ref epochNumber);
            ResetHotCounters();
        }

        internal void EndEpoch(string reason)
        {
            if (Interlocked.CompareExchange(ref epochActive, 0, 1) != 1)
                return;
            Shared.DebugLogHelper.LogInfo(log,
                $"Gate topology epoch {epochNumber} ended ({reason ?? "unspecified"}): " +
                $"accessScans={Read(ref accessScans)}, accessChanges={Read(ref accessChanges)}, " +
                $"builds={Read(ref topologyBuilds)}, changes={Read(ref topologyChanges)}, " +
                $"snapshotErrors={Volatile.Read(ref snapshotErrors)}.");
        }

        internal string DescribeState()
        {
            NativeGateAccessSnapshot access = accessSnapshot;
            TopologySnapshot topology = snapshot;
            return $"access(fingerprint=0x{access.TopologyFingerprint:X16},raw=0x{access.RawFingerprint:X16}," +
                $"tracked={access.TrackedRecords}," +
                $"captured={access.CapturedRecords},uncaptured={access.UncapturedRecords}," +
                $"blockedPairs={access.BlockedPlayerGatePairs},scans={Read(ref accessScans)}," +
                $"changes={Read(ref accessChanges)},republishes={Read(ref accessRepublishes)}," +
                $"suppressedRaw={Read(ref suppressedRawAccessChanges)}," +
                $"peakTracked={Read(ref peakTrackedRecords)},peakCaptured={Read(ref peakCapturedRecords)}," +
                $"peakBlockedPairs={Read(ref peakBlockedPairs)},captureTransitions={Read(ref captureTransitions)}," +
                $"recaptureTransitions={Read(ref recaptureTransitions)}), " +
                $"route(fingerprint=0x{topology.Fingerprint:X16}," +
                $"combinations={topology.Combinations.Length},builds={Read(ref topologyBuilds)}," +
                $"changes={Read(ref topologyChanges)},drawbridgeObserved={Volatile.Read(ref drawbridgeObserved)}), " +
                $"errors={Volatile.Read(ref snapshotErrors)}";
        }

        internal TopologyCoverageSnapshot GetCoverageSnapshot() => new TopologyCoverageSnapshot(
            Volatile.Read(ref snapshotErrors), Read(ref accessScans), Read(ref accessChanges),
            Read(ref accessRepublishes), Read(ref suppressedRawAccessChanges),
            unchecked((int)Read(ref peakTrackedRecords)), unchecked((int)Read(ref peakCapturedRecords)),
            unchecked((int)Read(ref peakBlockedPairs)), Read(ref captureTransitions),
            Read(ref recaptureTransitions), Volatile.Read(ref drawbridgeObserved) != 0);

        internal void ProcessDeferred()
        {
            if (Volatile.Read(ref epochActive) == 0)
                return;
            try
            {
                InitializeDeferredEpochIfNeeded();
                long now = Stopwatch.GetTimestamp();
                RefreshGateAccess();
                RefreshTopologyIfDue(now);
            }
            catch (Exception ex)
            {
                LogBoundedError(ref snapshotErrors,
                    "Deferred gate-topology snapshot failed without changing game state", ex);
            }
        }

        internal void OnGameTick()
        {
            // Script Extender contract: this is a cheap accepted-record
            // freshness probe only. Full topology/footprint rebuilding remains deferred
            // to onBeforeRender and is capped at once per second unless state changes.
            if (Volatile.Read(ref epochActive) == 0)
                return;
            try
            {
                TopologySnapshot current = snapshot;
                if (current.Combinations.Length == 0)
                    return;
                GameBuildingManagerAPI buildings = GameBuildingManagerAPI.Instance;
                for (int index = 0; index < current.Combinations.Length; index++)
                {
                    GateBridgeInfo info = current.Combinations[index];
                    if ((info.GateId > 0 && HasBuildingStateChanged(
                            buildings, info.GateId, info.GateGlobal,
                            info.Owner, info.CapturedBy, info.GateAliveState, checkCaptured: true)) ||
                        (info.BridgeId > 0 && HasBuildingStateChanged(
                            buildings, info.BridgeId, info.BridgeGlobal,
                            info.Owner, 0, info.BridgeAliveState, checkCaptured: false)))
                    {
                        Volatile.Write(ref nextSnapshotAt, 0);
                        // A capture/owner change invalidates both policies immediately.
                        // The next deferred scan rebuilds them outside native callbacks.
                        snapshot = TopologySnapshot.Empty;
                        accessSnapshot = NativeGateAccessSnapshot.Empty;
                        Volatile.Write(ref routePolicyWasCleared, 1);
                        Volatile.Write(ref accessPolicyWasCleared, 1);
                        Volatile.Write(ref accessRefreshRequired, 1);
                        routePolicyConsumer?.Invoke(RouteTilePolicySnapshot.Empty);
                        gateAccessConsumer?.Invoke(NativeGateAccessSnapshot.Empty);
                        return;
                    }
                }
            }
            catch
            {
                // A tick-side freshness failure is fail-open. The bounded deferred
                // full scan remains authoritative and will retry normally.
                Interlocked.Increment(ref snapshotErrors);
            }
        }

        private static bool HasBuildingStateChanged(
            GameBuildingManagerAPI buildings,
            int buildingId,
            uint globalId,
            int owner,
            int capturedBy,
            int aliveState,
            bool checkCaptured)
        {
            if (buildingId <= 0 || !buildings.IsValidId(buildingId) ||
                !buildings.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                building == null)
                return true;
            return building->r_GlobalId != globalId ||
                building->r_PlayerIdOwner != owner ||
                (checkCaptured && building->r_CapturedByPlayerId != capturedBy) ||
                (int)building->r_AliveState != aliveState;
        }

        internal void RecordSnapshotFailure() => Interlocked.Increment(ref snapshotErrors);

        private void ResetHotCounters()
        {
            Reset(ref accessScans); Reset(ref accessChanges);
            Reset(ref accessRepublishes); Reset(ref suppressedRawAccessChanges);
            Reset(ref topologyBuilds); Reset(ref topologyChanges);
            Reset(ref captureTransitions); Reset(ref recaptureTransitions);
            Reset(ref peakTrackedRecords); Reset(ref peakCapturedRecords);
            Reset(ref peakBlockedPairs);
            Volatile.Write(ref drawbridgeObserved, 0);
            Interlocked.Exchange(ref snapshotErrors, 0);
        }

        private void InitializeDeferredEpochIfNeeded()
        {
            int currentEpoch = Volatile.Read(ref epochNumber);
            if (initializedDeferredEpoch == currentEpoch)
                return;
            initializedDeferredEpoch = currentEpoch;
            long now = Stopwatch.GetTimestamp();
            // Stay outside the native query/building mutation stack. NeedsInit is a
            // valid temporary editor state and is diagnosed after this deferred delay.
            Volatile.Write(ref nextSnapshotAt, now);
            snapshot = TopologySnapshot.Empty;
            accessSnapshot = NativeGateAccessSnapshot.Empty;
            lastStableTopologySnapshot = TopologySnapshot.Empty;
            lastStableAccessSnapshot = NativeGateAccessSnapshot.Empty;
            routePolicyConsumer?.Invoke(RouteTilePolicySnapshot.Empty);
            gateAccessConsumer?.Invoke(NativeGateAccessSnapshot.Empty);
            lastAccessFingerprint = 0;
            lastTopologySignature = 0;
            lastTopologyFingerprint = 0;
            Volatile.Write(ref accessPolicyWasCleared, 0);
            Volatile.Write(ref routePolicyWasCleared, 0);
            Volatile.Write(ref accessRefreshRequired, 1);
            Shared.DebugLogHelper.LogInfo(log,
                $"Gate topology epoch {currentEpoch} started ({pendingEpochReason}). " +
                "Access changes are scanned allocation-free per rendered frame; expensive tile " +
                "topology rebuilds run on signature changes or once per second; " +
                "Same-PCL AI and cursorless command results remain unchanged.");
        }

        private void RefreshGateAccess()
        {
            ulong fingerprint = ComputeGateAccessFingerprint(out int recordCapacity);
            Interlocked.Increment(ref accessScans);
            bool forced = Volatile.Read(ref accessRefreshRequired) != 0;
            if (!forced && fingerprint == lastAccessFingerprint)
                return;

            NativeGateAccessSnapshot rebuilt = BuildGateAccessSnapshot(fingerprint, recordCapacity);
            NativeGateAccessSnapshot previous = lastStableAccessSnapshot;
            bool policyChanged = !previous.PolicyEquals(rebuilt);
            bool rawChanged = fingerprint != lastAccessFingerprint;
            lastAccessFingerprint = fingerprint;
            Volatile.Write(ref accessRefreshRequired, 0);
            if (policyChanged)
            {
                RecordAccessCoverage(previous, rebuilt);
                string changes = FormatAccessChanges(previous, rebuilt, 24);
                lastStableAccessSnapshot = rebuilt;
                accessSnapshot = rebuilt;
                gateAccessConsumer?.Invoke(rebuilt);
                Volatile.Write(ref accessPolicyWasCleared, 0);
                Interlocked.Increment(ref accessChanges);
                Volatile.Write(ref nextSnapshotAt, 0);
                Shared.DebugLogHelper.LogInfo(log,
                    $"Gate access policy changed: fingerprint=0x{rebuilt.TopologyFingerprint:X16}, " +
                    $"tracked={rebuilt.TrackedRecords}, captured={rebuilt.CapturedRecords}, " +
                    $"uncaptured={rebuilt.UncapturedRecords}, blockedPairs={rebuilt.BlockedPlayerGatePairs}, " +
                    $"records=[{changes}].");
                return;
            }

            if (rawChanged)
                Interlocked.Increment(ref suppressedRawAccessChanges);
            if (Interlocked.Exchange(ref accessPolicyWasCleared, 0) != 0)
            {
                // Republish the freshly verified equivalent policy after a tick-side
                // fail-open window without counting it as a semantic change.
                lastStableAccessSnapshot = rebuilt;
                accessSnapshot = rebuilt;
                gateAccessConsumer?.Invoke(rebuilt);
                Interlocked.Increment(ref accessRepublishes);
            }
        }

        private void RefreshTopologyIfDue(long now)
        {
            ulong signature = ComputeTopologySignature(lastStableAccessSnapshot.TopologyFingerprint);
            long due = Volatile.Read(ref nextSnapshotAt);
            bool changed = signature != lastTopologySignature;
            if (!changed && now < due)
                return;
            if (Interlocked.CompareExchange(
                    ref nextSnapshotAt, now + TopologySafetyInterval, due) != due)
                return;
            if (!Monitor.TryEnter(snapshotLock))
                return;
            try
            {
                bool firstBuild = Read(ref topologyBuilds) == 0;
                TopologySnapshot previous = lastStableTopologySnapshot;
                bool policyWasCleared = changed;
                if (policyWasCleared)
                {
                    // A structural transition must never expose stale blocked tiles,
                    // even while the replacement snapshot is being assembled.
                    snapshot = TopologySnapshot.Empty;
                    Volatile.Write(ref routePolicyWasCleared, 1);
                    routePolicyConsumer?.Invoke(RouteTilePolicySnapshot.Empty);
                }
                TopologySnapshot rebuilt = BuildTopologySnapshot(firstBuild, previous);
                snapshot = rebuilt;
                lastStableTopologySnapshot = rebuilt;
                lastTopologySignature = signature;
                Interlocked.Increment(ref topologyBuilds);
                bool policyChanged = rebuilt.Fingerprint != lastTopologyFingerprint;
                bool republishRequired = Interlocked.Exchange(ref routePolicyWasCleared, 0) != 0;
                if (policyChanged || republishRequired)
                    routePolicyConsumer?.Invoke(rebuilt.RoutePolicy);
                RecordTopologyCoverage(rebuilt);
                if (policyChanged)
                {
                    lastTopologyFingerprint = rebuilt.Fingerprint;
                    Interlocked.Increment(ref topologyChanges);
                    Shared.DebugLogHelper.LogInfo(log,
                        $"Gate/drawbridge topology changed: epoch={epochNumber}, " +
                        $"combinations={rebuilt.Combinations.Length}, " +
                        $"fingerprint=0x{rebuilt.Fingerprint:X16}, " +
                        $"entities=[{FormatTopologyChanges(previous, rebuilt, 24)}], " +
                        $"{rebuilt.Rejections.Format()}.");
                }
                if (firstBuild && !string.IsNullOrEmpty(rebuilt.Detail))
                    Shared.DebugLogHelper.LogInfo(log,
                        $"Initial gate/drawbridge topology detail: {rebuilt.Detail}");
            }
            catch (Exception ex)
            {
                LogBoundedError(ref snapshotErrors, "Gate/drawbridge topology snapshot failed", ex);
            }
            finally
            {
                Monitor.Exit(snapshotLock);
            }
        }

        private TopologySnapshot BuildTopologySnapshot(
            bool includeDetail,
            TopologySnapshot previous)
        {
            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            Span<GameBuilding> buildings = buildingApi.GetBuildingsAsSpan();
            var gateInfosById = new Dictionary<int, GateBridgeInfo>();
            var gateBuildingsById = new Dictionary<int, GameBuilding>();
            var combinations = new List<GateBridgeInfo>();
            var detail = includeDetail ? new StringBuilder() : null;
            TopologyRejections rejections = default;
            string[] rejectionSamples = includeDetail
                ? new string[Enum.GetValues(typeof(TopologyDiagnosticDisposition)).Length]
                : null;
            ulong fingerprint = 1469598103934665603UL;

            // Script Extender 2.4 exposes the authoritative macro-connection records.
            // Record zero and inactive records are reserved and must be skipped.
            var gateEntries = GamePathingManagerAPI.Instance.GetPathConnectionArray();
            for (int entryIndex = 0; entryIndex < gateEntries.Length; entryIndex++)
            {
                PathConnectionRecord* entryPointer = gateEntries.GetValuePointer(entryIndex);
                if (entryPointer == null || entryPointer->r_IsActive == 0 ||
                    entryPointer->r_BuildingId <= 0)
                    continue;
                rejections.ScannedGatehouses++;
                int gateId = entryPointer->r_BuildingId;
                if (gateInfosById.ContainsKey(gateId) || !buildingApi.IsValidId(gateId) ||
                    !buildingApi.TryGetBuildingById(gateId, out GameBuilding* gate) || gate == null)
                {
                    RecordRejection(ref rejections, rejectionSamples,
                        TopologyDiagnosticDisposition.InvalidGatehouseId,
                        gateId, 0, 0, 0, 0, "connection-record lookup");
                    continue;
                }
                if (!IsDiagnosticActive(gate->r_AliveState))
                {
                    RecordRejection(ref rejections, rejectionSamples,
                        TopologyDiagnosticDisposition.InvalidGateState,
                        gateId, gate->r_GlobalId, gate->r_PlayerIdOwner,
                        gate->r_CapturedByPlayerId, (int)gate->r_AliveState, "connection-record gate");
                    continue;
                }
                if (gate->r_GlobalId == 0)
                {
                    RecordRejection(ref rejections, rejectionSamples,
                        TopologyDiagnosticDisposition.InvalidGlobalId,
                        gateId, gate->r_GlobalId, gate->r_PlayerIdOwner,
                        gate->r_CapturedByPlayerId, (int)gate->r_AliveState, "connection-record gate");
                    continue;
                }
                GameBuilding gateSnapshot = *gate;
                PathConnectionRecord entry = *entryPointer;
                if (entry.r_SubjectGlobalId != gateSnapshot.r_GlobalId)
                {
                    RecordRejection(ref rejections, rejectionSamples,
                        TopologyDiagnosticDisposition.InconsistentReread,
                        gateId, gateSnapshot.r_GlobalId, gateSnapshot.r_PlayerIdOwner,
                        gateSnapshot.r_CapturedByPlayerId, (int)gateSnapshot.r_AliveState,
                        "record/building global mismatch");
                    continue;
                }
                int entryTile = unchecked((int)entry.r_EntryTileId);
                int exitTile = unchecked((int)entry.r_ExitTileId);
                if (entryTile <= 0 || exitTile <= 0 ||
                    !tileApi.IsValidTileId(entryTile) || !tileApi.IsValidTileId(exitTile))
                {
                    RecordRejection(ref rejections, rejectionSamples,
                        TopologyDiagnosticDisposition.InvalidDoorTiles,
                        gateId, gateSnapshot.r_GlobalId, gateSnapshot.r_PlayerIdOwner,
                        gateSnapshot.r_CapturedByPlayerId, (int)gateSnapshot.r_AliveState,
                        $"entry={entryTile}/exit={exitTile}");
                    continue;
                }
                int entryPcl = ReadPcl(tileApi, entryTile);
                int exitPcl = ReadPcl(tileApi, exitTile);
                if (!TryCollectBuildingTiles(
                        tileApi, buildingApi, gateId, gateSnapshot, out TileDiagnostic[] gateTiles))
                {
                    RecordRejection(ref rejections, rejectionSamples,
                        TopologyDiagnosticDisposition.InvalidFootprint,
                        gateId, gateSnapshot.r_GlobalId, gateSnapshot.r_PlayerIdOwner,
                        gateSnapshot.r_CapturedByPlayerId, (int)gateSnapshot.r_AliveState,
                        "gate uses door-tile fallback");
                    gateTiles = CollectDoorTiles(tileApi, entryTile, exitTile);
                }
                int[] relevantPcls = CollectRelevantPcls(gateTiles);
                bool[] unrelatedByPlayer = BuildUnrelatedPlayers(
                    gateSnapshot.r_PlayerIdOwner, gateSnapshot.r_CapturedByPlayerId);
                var gateInfo = new GateBridgeInfo(
                    gateId, gateSnapshot.r_GlobalId, gateSnapshot.r_PlayerIdOwner,
                    gateSnapshot.r_CapturedByPlayerId, entry.r_IsEnabledOrOpen != 0,
                    (int)gateSnapshot.r_AliveState, entryPcl, exitPcl,
                    0, 0, 0, 0, relevantPcls, gateTiles, unrelatedByPlayer,
                    0, "standalone-gate");
                gateInfosById.Add(gateId, gateInfo);
                gateBuildingsById.Add(gateId, gateSnapshot);
                combinations.Add(gateInfo);
                rejections.AcceptedGatehouses++;
                fingerprint = MixRoutePolicy(fingerprint, gateInfo);
            }

            // A newly placed editor gate may still be NeedsInit and absent from the
            // active connection records. Retain a clearly
            // labelled footprint-only diagnostic record; it never changes game state.
            for (int buildingIndex = 0; buildingIndex < buildings.Length; buildingIndex++)
            {
                int gateId = buildingIndex + 1;
                GameBuilding gate = buildings[buildingIndex];
                if (!IsGatehouseBuildingType(gate.r_BuildingType) ||
                    gateInfosById.ContainsKey(gateId))
                    continue;
                rejections.ScannedGatehouses++;
                if (!IsDiagnosticActive(gate.r_AliveState))
                {
                    RecordRejection(ref rejections, rejectionSamples,
                        TopologyDiagnosticDisposition.InvalidGateState,
                        gateId, gate.r_GlobalId, gate.r_PlayerIdOwner,
                        gate.r_CapturedByPlayerId, (int)gate.r_AliveState, "fallback gate");
                    continue;
                }
                if (gate.r_GlobalId == 0)
                {
                    RecordRejection(ref rejections, rejectionSamples,
                        TopologyDiagnosticDisposition.InvalidGlobalId,
                        gateId, gate.r_GlobalId, gate.r_PlayerIdOwner,
                        gate.r_CapturedByPlayerId, (int)gate.r_AliveState, "fallback gate");
                    continue;
                }
                if (!TryCollectBuildingTiles(
                        tileApi, buildingApi, gateId, gate, out TileDiagnostic[] gateTiles))
                {
                    RecordRejection(ref rejections, rejectionSamples,
                        TopologyDiagnosticDisposition.InvalidFootprint,
                        gateId, gate.r_GlobalId, gate.r_PlayerIdOwner,
                        gate.r_CapturedByPlayerId, (int)gate.r_AliveState, "fallback gate");
                    continue;
                }
                bool[] unrelatedByPlayer = BuildUnrelatedPlayers(
                    gate.r_PlayerIdOwner, gate.r_CapturedByPlayerId);
                var fallbackInfo = new GateBridgeInfo(
                    gateId, gate.r_GlobalId, gate.r_PlayerIdOwner,
                    gate.r_CapturedByPlayerId, false, (int)gate.r_AliveState,
                    -1, -1, 0, 0, 0, 0, CollectRelevantPcls(gateTiles),
                    gateTiles, unrelatedByPlayer, 0, "building-footprint-fallback");
                gateInfosById.Add(gateId, fallbackInfo);
                gateBuildingsById.Add(gateId, gate);
                combinations.Add(fallbackInfo);
                rejections.AcceptedGatehouses++;
                rejections.FallbackGatehouses++;
                fingerprint = MixRoutePolicy(fingerprint, fallbackInfo);
            }

            // Script Extender contract: the Span is zero-based and its
            // public building ID is index + 1. r_GatehouseId is deliberately logged
            // as an opaque raw value until its editor/runtime ID space is confirmed.
            // NeedsInit is active in editor maps, as in ActiveBuildingCache.
            for (int buildingIndex = 0; buildingIndex < buildings.Length; buildingIndex++)
            {
                int buildingId = buildingIndex + 1;
                GameBuilding building = buildings[buildingIndex];
                if (building.r_BuildingType != eStructs.STRUCT_DRAWBRIDGE)
                    continue;
                rejections.ScannedDrawbridges++;
                if (!IsDiagnosticActive(building.r_AliveState))
                {
                    RecordRejection(ref rejections, rejectionSamples,
                        TopologyDiagnosticDisposition.InvalidBridge,
                        buildingId, building.r_GlobalId, building.r_PlayerIdOwner,
                        building.r_CapturedByPlayerId, (int)building.r_AliveState, "drawbridge");
                    continue;
                }
                if (building.r_GlobalId == 0)
                {
                    RecordRejection(ref rejections, rejectionSamples,
                        TopologyDiagnosticDisposition.InvalidGlobalId,
                        buildingId, building.r_GlobalId, building.r_PlayerIdOwner,
                        building.r_CapturedByPlayerId, (int)building.r_AliveState, "drawbridge");
                    continue;
                }
                if (!TryCollectBuildingTiles(
                        tileApi, buildingApi, buildingId, building, out TileDiagnostic[] bridgeTiles))
                {
                    RecordRejection(ref rejections, rejectionSamples,
                        TopologyDiagnosticDisposition.InvalidFootprint,
                        buildingId, building.r_GlobalId, building.r_PlayerIdOwner,
                        building.r_CapturedByPlayerId, (int)building.r_AliveState, "drawbridge");
                    continue;
                }
                int rawGatehouseId = building.r_GatehouseId;
                bool nativeLink = gateInfosById.TryGetValue(rawGatehouseId, out GateBridgeInfo gateInfo) &&
                    gateInfo.Owner == building.r_PlayerIdOwner;
                bool spatialLink = !nativeLink && TryFindUniqueAdjacentGate(
                    bridgeTiles, building.r_PlayerIdOwner, gateInfosById, out gateInfo);
                if (!nativeLink && !spatialLink)
                {
                    RecordRejection(ref rejections, rejectionSamples,
                        TopologyDiagnosticDisposition.InvalidGatehouseId,
                        buildingId, building.r_GlobalId, building.r_PlayerIdOwner,
                        building.r_CapturedByPlayerId, (int)building.r_AliveState,
                        $"drawbridge rawGatehouseId={rawGatehouseId}");
                    var orphanInfo = new GateBridgeInfo(
                        0, 0, building.r_PlayerIdOwner, building.r_CapturedByPlayerId,
                        false, 0, -1, -1, buildingId, building.r_GlobalId,
                        0, (int)building.r_AliveState, CollectRelevantPcls(bridgeTiles),
                        bridgeTiles, BuildUnrelatedPlayers(
                            building.r_PlayerIdOwner, building.r_CapturedByPlayerId),
                        rawGatehouseId, "unlinked-bridge-diagnostic");
                    combinations.Add(orphanInfo);
                    rejections.OrphanBridgeCandidates++;
                    if (detail != null)
                    {
                        string orphan = FormatOrphanBridge(
                            buildingId, building, rawGatehouseId, bridgeTiles,
                            gateBuildingsById, gateInfosById);
                        AppendTopologyDetail(detail, orphan);
                    }
                    continue;
                }
                if (!buildingApi.TryGetBuildingById(buildingId, out GameBuilding* reread) ||
                    reread == null || reread->r_GlobalId != building.r_GlobalId ||
                    reread->r_GatehouseId != rawGatehouseId ||
                    !IsDiagnosticActive(reread->r_AliveState))
                {
                    RecordRejection(ref rejections, rejectionSamples,
                        TopologyDiagnosticDisposition.InconsistentReread,
                        buildingId, building.r_GlobalId, building.r_PlayerIdOwner,
                        building.r_CapturedByPlayerId, (int)building.r_AliveState,
                        $"drawbridge rawGatehouseId={rawGatehouseId}");
                    continue;
                }
                var bridgeInfo = new GateBridgeInfo(
                    gateInfo.GateId, gateInfo.GateGlobal, gateInfo.Owner, gateInfo.CapturedBy,
                    gateInfo.IsOpen, gateInfo.GateAliveState, gateInfo.EntryPcl, gateInfo.ExitPcl,
                    buildingId, building.r_GlobalId, gateInfo.GateId, (int)building.r_AliveState,
                    CollectRelevantPcls(bridgeTiles), bridgeTiles, gateInfo.UnrelatedByPlayer,
                    rawGatehouseId, nativeLink ? "native-building-id" : "unique-footprint-adjacency");
                combinations.Add(bridgeInfo);
                rejections.Add(TopologyDiagnosticDisposition.Accepted);
                fingerprint = MixRoutePolicy(fingerprint, bridgeInfo);
            }
            if (detail != null)
            {
                if (detail.Length == 0)
                    detail.Append("no rejection or orphan samples");
                detail.Append(" | ").Append(rejections.Format());
                for (int index = 0; index < rejectionSamples.Length; index++)
                    if (!string.IsNullOrEmpty(rejectionSamples[index]))
                        AppendTopologyDetail(detail,
                            "firstReject(" + (TopologyDiagnosticDisposition)index + ")=" +
                            rejectionSamples[index]);
            }
            GateBridgeInfo[] combinationArray = combinations.ToArray();
            RouteTilePolicySnapshot routePolicy = previous.Fingerprint == fingerprint
                ? previous.RoutePolicy
                : BuildRoutePolicySnapshot(tileApi, fingerprint, combinationArray);
            return new TopologySnapshot(
                fingerprint, combinationArray, detail?.ToString(), rejections, routePolicy);
        }

        private static ulong ComputeGateAccessFingerprint(out int recordCapacity)
        {
            GameBuildingManagerAPI buildings = GameBuildingManagerAPI.Instance;
            recordCapacity = buildings.GetBuildingsAsSpan().Length + 1;
            var entries = GamePathingManagerAPI.Instance.GetPathConnectionArray();
            ulong fingerprint = 1469598103934665603UL;
            for (int index = 0; index < entries.Length; index++)
            {
                PathConnectionRecord* entry = entries.GetValuePointer(index);
                if (entry == null || entry->r_IsActive == 0 || entry->r_BuildingId <= 0)
                    continue;
                unchecked
                {
                    fingerprint = (fingerprint ^ (uint)entry->r_BuildingId) * 1099511628211UL;
                    fingerprint = (fingerprint ^ entry->r_SubjectGlobalId) * 1099511628211UL;
                    if (buildings.IsValidId(entry->r_BuildingId) &&
                        buildings.TryGetBuildingById(entry->r_BuildingId, out GameBuilding* gate) &&
                        gate != null)
                    {
                        fingerprint = (fingerprint ^ gate->r_GlobalId) * 1099511628211UL;
                        fingerprint = (fingerprint ^ (uint)gate->r_PlayerIdOwner) * 1099511628211UL;
                        fingerprint = (fingerprint ^ (uint)gate->r_CapturedByPlayerId) * 1099511628211UL;
                        fingerprint = (fingerprint ^ (uint)gate->r_AliveState) * 1099511628211UL;
                        fingerprint = (fingerprint ^ BuildRelatedPlayerMask(
                            gate->r_PlayerIdOwner)) * 1099511628211UL;
                        fingerprint = (fingerprint ^ BuildRelatedPlayerMask(
                            gate->r_CapturedByPlayerId)) * 1099511628211UL;
                        fingerprint = (fingerprint ^ BuildUnrelatedPlayerMask(
                            gate->r_PlayerIdOwner, gate->r_CapturedByPlayerId)) * 1099511628211UL;
                    }
                }
            }
            return fingerprint;
        }

        private static NativeGateAccessSnapshot BuildGateAccessSnapshot(
            ulong fingerprint, int recordCapacity)
        {
            if (recordCapacity <= 1)
                return new NativeGateAccessSnapshot(Array.Empty<NativeGateAccessRecord>(), fingerprint);
            var records = new NativeGateAccessRecord[recordCapacity];
            GameBuildingManagerAPI buildings = GameBuildingManagerAPI.Instance;
            var entries = GamePathingManagerAPI.Instance.GetPathConnectionArray();
            for (int index = 0; index < entries.Length; index++)
            {
                PathConnectionRecord* entry = entries.GetValuePointer(index);
                if (entry == null || entry->r_IsActive == 0 || entry->r_BuildingId <= 0 ||
                    entry->r_BuildingId >= records.Length || records[entry->r_BuildingId].Valid ||
                    !buildings.IsValidId(entry->r_BuildingId) ||
                    !buildings.TryGetBuildingById(entry->r_BuildingId, out GameBuilding* gate) ||
                    gate == null || !IsDiagnosticActive(gate->r_AliveState) ||
                    gate->r_GlobalId == 0 || gate->r_GlobalId != entry->r_SubjectGlobalId)
                    continue;
                records[entry->r_BuildingId] = new NativeGateAccessRecord(
                    true, gate->r_PlayerIdOwner, gate->r_CapturedByPlayerId,
                    BuildRelatedPlayerMask(gate->r_PlayerIdOwner),
                    BuildRelatedPlayerMask(gate->r_CapturedByPlayerId),
                    BuildUnrelatedPlayerMask(gate->r_PlayerIdOwner, gate->r_CapturedByPlayerId));
            }
            return new NativeGateAccessSnapshot(records, fingerprint);
        }

        private static ulong ComputeTopologySignature(ulong accessFingerprint)
        {
            ulong signature = accessFingerprint;
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int index = 0; index < buildings.Length; index++)
            {
                GameBuilding building = buildings[index];
                if (!IsGatehouseBuildingType(building.r_BuildingType) &&
                    building.r_BuildingType != eStructs.STRUCT_DRAWBRIDGE)
                    continue;
                unchecked
                {
                    signature = (signature ^ (uint)(index + 1)) * 1099511628211UL;
                    signature = (signature ^ (uint)building.r_BuildingType) * 1099511628211UL;
                    signature = (signature ^ building.r_GlobalId) * 1099511628211UL;
                    signature = (signature ^ (uint)building.r_AliveState) * 1099511628211UL;
                    signature = (signature ^ (uint)building.r_PlayerIdOwner) * 1099511628211UL;
                    signature = (signature ^ (uint)building.r_CapturedByPlayerId) * 1099511628211UL;
                    signature = (signature ^ (uint)building.r_GatehouseId) * 1099511628211UL;
                    signature = (signature ^ building.r_TilePositionXBegin) * 1099511628211UL;
                    signature = (signature ^ building.r_TilePositionYBegin) * 1099511628211UL;
                    signature = (signature ^ building.r_TilePositionXEnd) * 1099511628211UL;
                    signature = (signature ^ building.r_TilePositionYEnd) * 1099511628211UL;
                }
            }
            return signature;
        }

        private static bool TryFindUniqueAdjacentGate(
            TileDiagnostic[] bridgeTiles,
            int bridgeOwner,
            Dictionary<int, GateBridgeInfo> gates,
            out GateBridgeInfo match)
        {
            match = default;
            RouteTilePoint[] bridge = ToFootprintPoints(bridgeTiles);
            var candidates = new List<GateBridgeInfo>();
            var points = new List<RouteTilePoint[]>();
            foreach (KeyValuePair<int, GateBridgeInfo> pair in gates)
            {
                candidates.Add(pair.Value);
                points.Add(ToFootprintPoints(pair.Value.Tiles));
            }
            bool[] eligible = new bool[candidates.Count];
            for (int index = 0; index < candidates.Count; index++)
                eligible[index] = candidates[index].Owner == bridgeOwner;
            int selected = EnemyGatePathfindingPolicy.FindUniqueAdjacentCandidate(
                bridge, points.ToArray(), eligible);
            if (selected < 0)
                return false;
            match = candidates[selected];
            return true;
        }

        private static RouteTilePoint[] ToFootprintPoints(TileDiagnostic[] tiles)
        {
            var points = new List<RouteTilePoint>();
            if (tiles != null)
            {
                for (int index = 0; index < tiles.Length; index++)
                    if (tiles[index].Footprint)
                        points.Add(new RouteTilePoint(tiles[index].X, tiles[index].Y));
            }
            return points.ToArray();
        }

        private static RouteTilePolicySnapshot BuildRoutePolicySnapshot(
            GameTileManagerAPI tiles,
            ulong fingerprint,
            GateBridgeInfo[] combinations)
        {
            int wordCount = (EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive + 63) >> 6;
            ulong[][] gateBits = new ulong[9][];
            ulong[][] bridgeBits = new ulong[9][];
            bool[] hasBlockedTiles = new bool[9];
            var blockedTileLists = new List<RouteBlockedTile>[9];
            var blockedTileIds = new HashSet<int>[9];
            for (int player = 1; player <= 8; player++)
            {
                gateBits[player] = new ulong[wordCount];
                bridgeBits[player] = new ulong[wordCount];
                blockedTileLists[player] = new List<RouteBlockedTile>();
                blockedTileIds[player] = new HashSet<int>();
            }
            var identities = new Dictionary<int, RouteTileIdentity>();
            for (int infoIndex = 0; infoIndex < combinations.Length; infoIndex++)
            {
                GateBridgeInfo info = combinations[infoIndex];
                bool isGate = info.GateId > 0 && info.BridgeId == 0;
                bool isBridge = info.BridgeId > 0 && info.GateId > 0;
                if (!isGate && !isBridge)
                    continue;
                for (int tileIndex = 0; tileIndex < info.Tiles.Length; tileIndex++)
                {
                    TileDiagnostic tile = info.Tiles[tileIndex];
                    if (!tile.Footprint || tile.TileId < 0 ||
                        tile.TileId >= EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive)
                        continue;
                    for (int player = 1; player <= 8; player++)
                    {
                        if (player >= info.UnrelatedByPlayer.Length || !info.UnrelatedByPlayer[player])
                            continue;
                        ulong[] bits = isGate ? gateBits[player] : bridgeBits[player];
                        bits[tile.TileId >> 6] |= 1UL << (tile.TileId & 63);
                        hasBlockedTiles[player] = true;
                        if (blockedTileIds[player].Add(tile.TileId))
                        {
                            blockedTileLists[player].Add(new RouteBlockedTile(
                                tile.TileId, tile.X, tile.Y));
                        }
                    }
                    identities.TryGetValue(tile.TileId, out RouteTileIdentity identity);
                    identities[tile.TileId] = identity.Merge(
                        info.GateId,
                        isBridge ? info.BridgeId : 0);
                }
            }
            int[] rowStarts = new int[EnemyGatePathfindingNativeDefinition.MapGridWidth];
            if (tiles.MapRowLookupTable != null)
            {
                for (int y = 0; y < rowStarts.Length; y++)
                    rowStarts[y] = tiles.MapRowLookupTable[3 * y];
            }
            RouteBlockedTile[][] blockedTiles = new RouteBlockedTile[9][];
            for (int player = 1; player <= 8; player++)
                blockedTiles[player] = blockedTileLists[player].ToArray();
            return new RouteTilePolicySnapshot(
                gateBits, bridgeBits, rowStarts, identities, hasBlockedTiles, fingerprint,
                blockedTiles);
        }

        private static bool TryCollectBuildingTiles(GameTileManagerAPI tiles,
            GameBuildingManagerAPI buildings, int bridgeId, GameBuilding bridge,
            out TileDiagnostic[] diagnostics)
        {
            diagnostics = Array.Empty<TileDiagnostic>();
            var footprint = new HashSet<int>();
            uint gridSize = bridge.r_OccupyTileGridSize;
            if (gridSize == 0 || gridSize > 6)
                return false;
            // Script Extender contract: this API reads gridSize squared
            // inline UInt32 entries. The size is bounded before calling it.
            int[] occupied = buildings.GetOccupiedTileIds(bridgeId);
            int cells = Math.Min(occupied.Length, checked((int)(gridSize * gridSize)));
            for (int index = 0; index < cells; index++)
            {
                int tileId = occupied[index];
                if (tileId > 0 && tiles.IsValidTileId(tileId))
                    footprint.Add(tileId);
            }
            if (footprint.Count == 0)
                return false;

            diagnostics = BuildTileDiagnostics(tiles, footprint);
            return diagnostics.Length != 0;
        }

        private static TileDiagnostic[] CollectDoorTiles(
            GameTileManagerAPI tiles, int entryTile, int exitTile)
        {
            var footprint = new HashSet<int>();
            if (tiles.IsValidTileId(entryTile)) footprint.Add(entryTile);
            if (tiles.IsValidTileId(exitTile)) footprint.Add(exitTile);
            return BuildTileDiagnostics(tiles, footprint);
        }

        private static TileDiagnostic[] BuildTileDiagnostics(
            GameTileManagerAPI tiles, HashSet<int> footprint)
        {
            var all = new Dictionary<int, bool>();
            foreach (int tileId in footprint)
            {
                all[tileId] = true;
                var position = tiles.GetTileVectorFromId(tileId);
                AddPerimeter(tiles, all, footprint, position.X - 1, position.Y);
                AddPerimeter(tiles, all, footprint, position.X + 1, position.Y);
                AddPerimeter(tiles, all, footprint, position.X, position.Y - 1);
                AddPerimeter(tiles, all, footprint, position.X, position.Y + 1);
            }

            var result = new List<TileDiagnostic>(all.Count);
            foreach (KeyValuePair<int, bool> pair in all)
            {
                int tileId = pair.Key;
                var position = tiles.GetTileVectorFromId(tileId);
                result.Add(new TileDiagnostic(tileId, position.X, position.Y, pair.Value,
                    ReadPcl(tiles, tileId), tiles.TileManager.GatePathGrid[tileId],
                    tiles.GetTileBuildingId(tileId), unchecked((int)tiles.GetTilePropertyFlag(tileId)),
                    tiles.IsTileWalkableAndUnoccupied(tileId)));
            }
            result.Sort((left, right) => left.TileId.CompareTo(right.TileId));
            return result.ToArray();
        }

        private static int[] CollectRelevantPcls(TileDiagnostic[] tiles)
        {
            var pcls = new HashSet<int>();
            foreach (TileDiagnostic tile in tiles)
                if (tile.Pcl >= 0) pcls.Add(tile.Pcl);
            return ToArray(pcls);
        }

        private static bool[] BuildUnrelatedPlayers(int owner, int captured)
        {
            bool[] unrelated = new bool[9];
            for (int player = 1; player <= 8; player++)
            {
                unrelated[player] = EnemyGatePathfindingPolicy.IsUnrelatedGateCombination(
                    player, owner, captured, IsValidPlayer, AreAllied);
            }
            return unrelated;
        }

        private static ushort BuildUnrelatedPlayerMask(int owner, int captured)
        {
            ushort mask = 0;
            for (int player = 1; player <= 8; player++)
            {
                if (EnemyGatePathfindingPolicy.IsUnrelatedGateCombination(
                        player, owner, captured, IsValidPlayer, AreAllied))
                    mask |= unchecked((ushort)(1 << player));
            }
            return mask;
        }

        private static ushort BuildRelatedPlayerMask(int subject)
        {
            if (!IsValidPlayer(subject))
                return 0;
            ushort mask = 0;
            for (int player = 1; player <= 8; player++)
                if (IsValidPlayer(player) && AreAllied(player, subject))
                    mask |= unchecked((ushort)(1 << player));
            return mask;
        }

        private static void AppendTopologyDetail(StringBuilder detail, string value)
        {
            if (detail.Length > 0) detail.Append(" | ");
            detail.Append(value);
        }

        private static void RecordRejection(
            ref TopologyRejections rejections,
            string[] samples,
            TopologyDiagnosticDisposition disposition,
            int buildingId,
            uint globalId,
            int owner,
            int captured,
            int aliveState,
            string note)
        {
            rejections.Add(disposition);
            int index = (int)disposition;
            if (samples == null || index < 0 || index >= samples.Length ||
                samples[index] != null)
                return;
            samples[index] = $"building={buildingId}/global={globalId}/owner={owner}/" +
                $"captured={captured}/alive={aliveState}/note={note}";
        }

        private static string FormatOrphanBridge(
            int bridgeId,
            GameBuilding bridge,
            int rawGatehouseId,
            TileDiagnostic[] tiles,
            Dictionary<int, GameBuilding> gates,
            Dictionary<int, GateBridgeInfo> gateInfos)
        {
            List<KeyValuePair<int, int>> candidates = GetSpatialGateCandidates(bridge, gates);
            var text = new StringBuilder();
            text.Append("orphanBridge#").Append(bridgeId).Append("/g").Append(bridge.r_GlobalId)
                .Append(" state=").Append((int)bridge.r_AliveState)
                .Append(" owner=").Append(bridge.r_PlayerIdOwner)
                .Append(" captured=").Append(bridge.r_CapturedByPlayerId)
                .Append(" rawGatehouseId=").Append(rawGatehouseId)
                .Append(" bounds=").Append(bridge.r_TilePositionXBegin).Append('/')
                .Append(bridge.r_TilePositionYBegin).Append('-')
                .Append(bridge.r_TilePositionXEnd).Append('/').Append(bridge.r_TilePositionYEnd)
                .Append(" pcls=").Append(string.Join("/", CollectRelevantPcls(tiles)))
                .Append(" footprintAdjacentSameOwnerGates=[");
            bool firstAdjacent = true;
            RouteTilePoint[] bridgePoints = ToFootprintPoints(tiles);
            foreach (KeyValuePair<int, GateBridgeInfo> pair in gateInfos)
            {
                if (pair.Value.Owner != bridge.r_PlayerIdOwner ||
                    !EnemyGatePathfindingPolicy.AreFootprintsCardinallyAdjacent(
                        bridgePoints, ToFootprintPoints(pair.Value.Tiles)))
                    continue;
                if (!firstAdjacent) text.Append(';');
                text.Append("gate#").Append(pair.Key);
                firstAdjacent = false;
            }
            text.Append("] spatialGateCandidates=[");
            for (int index = 0; index < candidates.Count && index < 8; index++)
            {
                if (index > 0) text.Append(';');
                int gateId = candidates[index].Key;
                GameBuilding gate = gates[gateId];
                text.Append("gate#").Append(gateId).Append("/g").Append(gate.r_GlobalId)
                    .Append("/distance=").Append(candidates[index].Value)
                    .Append("/bounds=").Append(gate.r_TilePositionXBegin).Append('/')
                    .Append(gate.r_TilePositionYBegin).Append('-')
                    .Append(gate.r_TilePositionXEnd).Append('/')
                    .Append(gate.r_TilePositionYEnd);
            }
            if (candidates.Count > 8) text.Append(";+").Append(candidates.Count - 8);
            text.Append("] tiles=[");
            for (int index = 0; index < tiles.Length; index++)
            {
                if (index > 0) text.Append(';');
                text.Append(tiles[index].Format());
            }
            return text.Append(']').ToString();
        }

        private static List<KeyValuePair<int, int>> GetSpatialGateCandidates(
            GameBuilding bridge, Dictionary<int, GameBuilding> gates)
        {
            var candidates = new List<KeyValuePair<int, int>>(gates.Count);
            foreach (KeyValuePair<int, GameBuilding> pair in gates)
                candidates.Add(new KeyValuePair<int, int>(pair.Key, RectDistance(bridge, pair.Value)));
            candidates.Sort((left, right) =>
            {
                int compare = left.Value.CompareTo(right.Value);
                return compare != 0 ? compare : left.Key.CompareTo(right.Key);
            });
            return candidates;
        }

        private static int RectDistance(GameBuilding first, GameBuilding second)
        {
            return EnemyGatePathfindingPolicy.CalculateRectangleDistance(
                first.r_TilePositionXBegin, first.r_TilePositionYBegin,
                first.r_TilePositionXEnd, first.r_TilePositionYEnd,
                second.r_TilePositionXBegin, second.r_TilePositionYBegin,
                second.r_TilePositionXEnd, second.r_TilePositionYEnd);
        }

        private static ulong MixOrphanBridge(
            ulong hash,
            int bridgeId,
            GameBuilding bridge,
            int rawGatehouseId,
            Dictionary<int, GameBuilding> gates)
        {
            unchecked
            {
                hash = (hash ^ (uint)bridgeId) * 1099511628211UL;
                hash = (hash ^ bridge.r_GlobalId) * 1099511628211UL;
                hash = (hash ^ (uint)rawGatehouseId) * 1099511628211UL;
                hash = (hash ^ bridge.r_TilePositionXBegin) * 1099511628211UL;
                hash = (hash ^ bridge.r_TilePositionYBegin) * 1099511628211UL;
                foreach (KeyValuePair<int, int> candidate in GetSpatialGateCandidates(bridge, gates))
                {
                    hash = (hash ^ (uint)candidate.Key) * 1099511628211UL;
                    hash = (hash ^ (uint)candidate.Value) * 1099511628211UL;
                }
                return hash;
            }
        }

        private static void AddPerimeter(GameTileManagerAPI tiles, Dictionary<int, bool> all,
            HashSet<int> footprint, int x, int y)
        {
            if (!tiles.IsTileInsideMapBounds(x, y))
                return;
            int tileId = tiles.GetTileId(x, y);
            if (tileId > 0 && tiles.IsValidTileId(tileId) && !footprint.Contains(tileId) && !all.ContainsKey(tileId))
                all.Add(tileId, false);
        }

        private void LogBoundedError(ref int counter, string category, Exception ex)
        {
            int count = Interlocked.Increment(ref counter);
            if (count <= MaximumErrorsPerCategory)
                Shared.DebugLogHelper.LogWarning(log,
                    $"{category} ({count}/{MaximumErrorsPerCategory}): {ex.GetType().Name}: {ex.Message}");
        }

        private static int ReadPcl(GameTileManagerAPI tiles, int tileId) =>
            tileId >= 0 && tiles.IsValidTileId(tileId)
                ? GamePathingManagerAPI.Instance.GetPathComponentIdByTileId(tileId)
                : -1;

        private static bool IsDiagnosticActive(AliveState aliveState) =>
            aliveState == AliveState.NeedsInit || aliveState == AliveState.IsAlive;

        private static bool IsGatehouseBuildingType(eStructs buildingType) =>
            buildingType == eStructs.STRUCT_GATEHOUSE ||
            buildingType == eStructs.STRUCT_GATE_MAIN ||
            buildingType == eStructs.STRUCT_GATE_INNER ||
            buildingType == eStructs.STRUCT_GATE_WOOD ||
            buildingType == eStructs.STRUCT_GATE_POSTERN;

        private static bool IsValidPlayer(int playerId) => GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId);
        private static bool AreAllied(int first, int second) => first == second ||
            GamePlayerManagerAPI.Instance.IsPlayerAlliedTo(first, second);

        private static int[] ToArray(HashSet<int> values)
        {
            int[] result = new int[values.Count];
            values.CopyTo(result);
            Array.Sort(result);
            return result;
        }

        private static ulong MixRoutePolicy(ulong hash, GateBridgeInfo info)
        {
            unchecked
            {
                hash = (hash ^ (uint)info.GateId) * 1099511628211UL;
                hash = (hash ^ info.GateGlobal) * 1099511628211UL;
                hash = (hash ^ (uint)info.BridgeId) * 1099511628211UL;
                hash = (hash ^ info.BridgeGlobal) * 1099511628211UL;
                ushort blockedMask = 0;
                for (int player = 1; player < info.UnrelatedByPlayer.Length && player <= 8; player++)
                    if (info.UnrelatedByPlayer[player])
                        blockedMask |= unchecked((ushort)(1 << player));
                hash = (hash ^ blockedMask) * 1099511628211UL;
                foreach (TileDiagnostic tile in info.Tiles)
                {
                    if (tile.Footprint)
                        hash = (hash ^ (uint)tile.TileId) * 1099511628211UL;
                }
                return hash;
            }
        }

        private static string FormatAccessChanges(
            NativeGateAccessSnapshot previous,
            NativeGateAccessSnapshot current,
            int limit)
        {
            NativeGateAccessRecord[] oldRecords = previous?.RecordsByBuildingId ??
                Array.Empty<NativeGateAccessRecord>();
            NativeGateAccessRecord[] newRecords = current?.RecordsByBuildingId ??
                Array.Empty<NativeGateAccessRecord>();
            int length = Math.Max(oldRecords.Length, newRecords.Length);
            var text = new StringBuilder();
            int changed = 0;
            for (int buildingId = 1; buildingId < length; buildingId++)
            {
                NativeGateAccessRecord oldRecord = buildingId < oldRecords.Length
                    ? oldRecords[buildingId] : default;
                NativeGateAccessRecord newRecord = buildingId < newRecords.Length
                    ? newRecords[buildingId] : default;
                if (oldRecord.Valid == newRecord.Valid &&
                    oldRecord.OwnerPlayerId == newRecord.OwnerPlayerId &&
                    oldRecord.CapturedByPlayerId == newRecord.CapturedByPlayerId &&
                    oldRecord.OwnerRelatedPlayers == newRecord.OwnerRelatedPlayers &&
                    oldRecord.CapturerRelatedPlayers == newRecord.CapturerRelatedPlayers &&
                    oldRecord.UnrelatedPlayers == newRecord.UnrelatedPlayers)
                    continue;
                changed++;
                if (changed > limit)
                    continue;
                if (text.Length > 0) text.Append(';');
                text.Append('#').Append(buildingId).Append(':')
                    .Append(FormatAccessRecord(oldRecord)).Append("->")
                    .Append(FormatAccessRecord(newRecord));
            }
            if (changed == 0)
                return "fingerprint-only/no-record-diff";
            if (changed > limit)
                text.Append(";+").Append(changed - limit);
            return text.ToString();
        }

        private static string FormatAccessRecord(NativeGateAccessRecord record) => !record.Valid
            ? "absent"
            : $"o{record.OwnerPlayerId}/c{record.CapturedByPlayerId}/" +
                $"om0x{record.OwnerRelatedPlayers:X}/cm0x{record.CapturerRelatedPlayers:X}/" +
                $"bm0x{record.UnrelatedPlayers:X}";

        private static string FormatTopologyChanges(
            TopologySnapshot previous,
            TopologySnapshot current,
            int limit)
        {
            var oldByKey = new Dictionary<long, GateBridgeInfo>();
            foreach (GateBridgeInfo info in previous.Combinations)
                oldByKey[TopologyKey(info)] = info;
            var text = new StringBuilder();
            int changed = 0;
            foreach (GateBridgeInfo info in current.Combinations)
            {
                long key = TopologyKey(info);
                string kind;
                if (!oldByKey.TryGetValue(key, out GateBridgeInfo old))
                    kind = "added";
                else
                {
                    oldByKey.Remove(key);
                    if (RoutePolicyEquals(old, info))
                        continue;
                    kind = "changed";
                }
                changed++;
                if (changed <= limit)
                {
                    if (text.Length > 0) text.Append(';');
                    text.Append(kind).Append(':').Append(info.Format());
                }
            }
            foreach (GateBridgeInfo info in oldByKey.Values)
            {
                changed++;
                if (changed <= limit)
                {
                    if (text.Length > 0) text.Append(';');
                    text.Append("removed:gate#").Append(info.GateId)
                        .Append("/bridge#").Append(info.BridgeId);
                }
            }
            if (changed == 0)
                return "policy-fingerprint-only/no-entity-diff";
            if (changed > limit)
                text.Append(";+").Append(changed - limit);
            return text.ToString();
        }

        private void RecordAccessCoverage(
            NativeGateAccessSnapshot previous,
            NativeGateAccessSnapshot current)
        {
            UpdateMaximum(ref peakTrackedRecords, current.TrackedRecords);
            UpdateMaximum(ref peakCapturedRecords, current.CapturedRecords);
            UpdateMaximum(ref peakBlockedPairs, current.BlockedPlayerGatePairs);
            NativeGateAccessRecord[] oldRecords = previous?.RecordsByBuildingId ??
                Array.Empty<NativeGateAccessRecord>();
            NativeGateAccessRecord[] newRecords = current?.RecordsByBuildingId ??
                Array.Empty<NativeGateAccessRecord>();
            int length = Math.Max(oldRecords.Length, newRecords.Length);
            for (int buildingId = 1; buildingId < length; buildingId++)
            {
                NativeGateAccessRecord oldRecord = buildingId < oldRecords.Length
                    ? oldRecords[buildingId] : default;
                NativeGateAccessRecord newRecord = buildingId < newRecords.Length
                    ? newRecords[buildingId] : default;
                CaptureTransitionKind transition =
                    EnemyGatePathfindingPolicy.ClassifyCaptureTransition(
                        oldRecord.Valid, oldRecord.CapturedByPlayerId,
                        newRecord.Valid, newRecord.CapturedByPlayerId);
                if (transition == CaptureTransitionKind.Captured)
                    Interlocked.Increment(ref captureTransitions);
                else if (transition == CaptureTransitionKind.Recaptured)
                    Interlocked.Increment(ref recaptureTransitions);
            }
        }

        private void RecordTopologyCoverage(TopologySnapshot current)
        {
            for (int index = 0; index < current.Combinations.Length; index++)
            {
                if (current.Combinations[index].BridgeId <= 0)
                    continue;
                Volatile.Write(ref drawbridgeObserved, 1);
                return;
            }
        }

        private static long TopologyKey(GateBridgeInfo info) =>
            (unchecked((long)(uint)info.GateId) << 32) | unchecked((uint)info.BridgeId);

        private static bool RoutePolicyEquals(GateBridgeInfo left, GateBridgeInfo right)
        {
            if (left.GateGlobal != right.GateGlobal || left.BridgeGlobal != right.BridgeGlobal ||
                left.UnrelatedByPlayer.Length != right.UnrelatedByPlayer.Length)
                return false;
            for (int player = 1; player < left.UnrelatedByPlayer.Length; player++)
                if (left.UnrelatedByPlayer[player] != right.UnrelatedByPlayer[player])
                    return false;
            var leftTiles = new List<int>();
            var rightTiles = new List<int>();
            foreach (TileDiagnostic tile in left.Tiles)
                if (tile.Footprint) leftTiles.Add(tile.TileId);
            foreach (TileDiagnostic tile in right.Tiles)
                if (tile.Footprint) rightTiles.Add(tile.TileId);
            leftTiles.Sort();
            rightTiles.Sort();
            if (leftTiles.Count != rightTiles.Count)
                return false;
            for (int index = 0; index < leftTiles.Count; index++)
                if (leftTiles[index] != rightTiles[index])
                    return false;
            return true;
        }

        private static void Reset(ref long value) => Interlocked.Exchange(ref value, 0);
        private static long Read(ref long value) => Interlocked.Read(ref value);

        private static void UpdateMaximum(ref long target, long candidate)
        {
            long observed;
            while (candidate > (observed = Interlocked.Read(ref target)) &&
                Interlocked.CompareExchange(ref target, candidate, observed) != observed)
            { }
        }

        private static ulong Mix(ulong hash, TopologyRejections rejections)
        {
            unchecked
            {
                hash = (hash ^ (uint)rejections.ScannedDrawbridges) * 1099511628211UL;
                hash = (hash ^ (uint)rejections.Accepted) * 1099511628211UL;
                hash = (hash ^ (uint)rejections.ScannedGatehouses) * 1099511628211UL;
                hash = (hash ^ (uint)rejections.AcceptedGatehouses) * 1099511628211UL;
                hash = (hash ^ (uint)rejections.FallbackGatehouses) * 1099511628211UL;
                hash = (hash ^ (uint)rejections.OrphanBridgeCandidates) * 1099511628211UL;
                hash = (hash ^ (uint)rejections.InvalidBridge) * 1099511628211UL;
                hash = (hash ^ (uint)rejections.GatehouseId) * 1099511628211UL;
                hash = (hash ^ (uint)rejections.GateState) * 1099511628211UL;
                hash = (hash ^ (uint)rejections.GlobalId) * 1099511628211UL;
                hash = (hash ^ (uint)rejections.GatehouseEntry) * 1099511628211UL;
                hash = (hash ^ (uint)rejections.DoorTiles) * 1099511628211UL;
                hash = (hash ^ (uint)rejections.Footprint) * 1099511628211UL;
                return (hash ^ (uint)rejections.InconsistentReread) * 1099511628211UL;
            }
        }

        private sealed class TopologySnapshot
        {
            internal static readonly TopologySnapshot Empty = new TopologySnapshot(
                0, Array.Empty<GateBridgeInfo>(), "not captured", default,
                RouteTilePolicySnapshot.Empty);
            internal TopologySnapshot(ulong fingerprint, GateBridgeInfo[] combinations,
                string detail, TopologyRejections rejections,
                RouteTilePolicySnapshot routePolicy)
            { Fingerprint = fingerprint; Combinations = combinations; Detail = detail;
                Rejections = rejections; RoutePolicy = routePolicy ?? RouteTilePolicySnapshot.Empty; }
            internal ulong Fingerprint { get; }
            internal GateBridgeInfo[] Combinations { get; }
            internal string Detail { get; }
            internal TopologyRejections Rejections { get; }
            internal RouteTilePolicySnapshot RoutePolicy { get; }
        }

        private readonly struct GateBridgeInfo
        {
            internal GateBridgeInfo(int gateId, uint gateGlobal, int owner, int capturedBy,
                bool isOpen, int gateAliveState, int entryPcl, int exitPcl, int bridgeId,
                uint bridgeGlobal, int linkedGateId, int bridgeAliveState,
                int[] relevantPcls, TileDiagnostic[] tiles, bool[] unrelatedByPlayer,
                int rawGatehouseId, string linkMethod)
            {
                GateId = gateId; GateGlobal = gateGlobal; Owner = owner; CapturedBy = capturedBy;
                IsOpen = isOpen; GateAliveState = gateAliveState; EntryPcl = entryPcl;
                ExitPcl = exitPcl; BridgeId = bridgeId; BridgeGlobal = bridgeGlobal;
                LinkedGateId = linkedGateId; BridgeAliveState = bridgeAliveState;
                RelevantPcls = relevantPcls; Tiles = tiles; UnrelatedByPlayer = unrelatedByPlayer;
                RawGatehouseId = rawGatehouseId; LinkMethod = linkMethod ?? "unknown";
            }
            internal int GateId { get; }
            internal uint GateGlobal { get; }
            internal int Owner { get; }
            internal int CapturedBy { get; }
            internal bool IsOpen { get; }
            internal int GateAliveState { get; }
            internal int EntryPcl { get; }
            internal int ExitPcl { get; }
            internal int BridgeId { get; }
            internal uint BridgeGlobal { get; }
            internal int LinkedGateId { get; }
            internal int BridgeAliveState { get; }
            internal int[] RelevantPcls { get; }
            internal TileDiagnostic[] Tiles { get; }
            internal bool[] UnrelatedByPlayer { get; }
            internal int RawGatehouseId { get; }
            internal string LinkMethod { get; }

            internal string Format()
            {
                var text = new StringBuilder();
                if (GateId > 0)
                {
                    text.Append("gate#").Append(GateId).Append("/g").Append(GateGlobal)
                        .Append(" owner=").Append(Owner).Append(" captured=").Append(CapturedBy)
                        .Append(" state=").Append(GateAliveState).Append(" open=").Append(IsOpen)
                        .Append(" entryExitPcl=").Append(EntryPcl).Append('/')
                        .Append(ExitPcl).Append(" linkMethod=").Append(LinkMethod);
                }
                else
                {
                    text.Append("unlinkedBridge owner=").Append(Owner)
                        .Append(" captured=").Append(CapturedBy)
                        .Append(" linkMethod=").Append(LinkMethod);
                }
                if (BridgeId > 0)
                {
                    text.Append(" bridge#").Append(BridgeId).Append("/g").Append(BridgeGlobal)
                        .Append(" state=").Append(BridgeAliveState)
                        .Append(" linkedGate=").Append(LinkedGateId)
                        .Append(" rawGatehouseId=").Append(RawGatehouseId);
                }
                ushort blockedMask = 0;
                for (int player = 1; player < UnrelatedByPlayer.Length && player <= 8; player++)
                    if (UnrelatedByPlayer[player])
                        blockedMask |= unchecked((ushort)(1 << player));
                int minX = int.MaxValue;
                int minY = int.MaxValue;
                int maxX = int.MinValue;
                int maxY = int.MinValue;
                int minimumTileId = int.MaxValue;
                int maximumTileId = int.MinValue;
                int footprintCount = 0;
                ulong footprintHash = 1469598103934665603UL;
                for (int index = 0; index < Tiles.Length; index++)
                {
                    if (!Tiles[index].Footprint)
                        continue;
                    footprintCount++;
                    minX = Math.Min(minX, Tiles[index].X);
                    minY = Math.Min(minY, Tiles[index].Y);
                    maxX = Math.Max(maxX, Tiles[index].X);
                    maxY = Math.Max(maxY, Tiles[index].Y);
                    minimumTileId = Math.Min(minimumTileId, Tiles[index].TileId);
                    maximumTileId = Math.Max(maximumTileId, Tiles[index].TileId);
                    unchecked
                    {
                        footprintHash = (footprintHash ^ (uint)Tiles[index].TileId) *
                            1099511628211UL;
                    }
                }
                text.Append(" pcls=").Append(string.Join("/", RelevantPcls))
                    .Append(" blockedMask=0x").Append(blockedMask.ToString("X"))
                    .Append(" bounds=");
                if (footprintCount == 0)
                    text.Append("none");
                else
                    text.Append(minX).Append('/').Append(minY).Append('-')
                        .Append(maxX).Append('/').Append(maxY);
                text.Append(" footprint=").Append(footprintCount);
                if (footprintCount > 0)
                    text.Append("/tileRange=").Append(minimumTileId).Append('-')
                        .Append(maximumTileId).Append("/hash=0x")
                        .Append(footprintHash.ToString("X16"));
                return text.ToString();
            }
        }

        private struct TopologyRejections
        {
            internal int ScannedGatehouses;
            internal int AcceptedGatehouses;
            internal int FallbackGatehouses;
            internal int OrphanBridgeCandidates;
            internal int ScannedDrawbridges;
            internal int Accepted;
            internal int InvalidBridge;
            internal int GatehouseId;
            internal int GateState;
            internal int GlobalId;
            internal int GatehouseEntry;
            internal int DoorTiles;
            internal int Footprint;
            internal int InconsistentReread;

            internal void Add(TopologyDiagnosticDisposition disposition, int count = 1)
            {
                switch (disposition)
                {
                    case TopologyDiagnosticDisposition.Accepted: Accepted += count; break;
                    case TopologyDiagnosticDisposition.InvalidBridge: InvalidBridge += count; break;
                    case TopologyDiagnosticDisposition.InvalidGatehouseId: GatehouseId += count; break;
                    case TopologyDiagnosticDisposition.InvalidGateState: GateState += count; break;
                    case TopologyDiagnosticDisposition.InvalidGlobalId: GlobalId += count; break;
                    case TopologyDiagnosticDisposition.MissingGatehouseEntry: GatehouseEntry += count; break;
                    case TopologyDiagnosticDisposition.InvalidDoorTiles: DoorTiles += count; break;
                    case TopologyDiagnosticDisposition.InvalidFootprint: Footprint += count; break;
                    case TopologyDiagnosticDisposition.InconsistentReread: InconsistentReread += count; break;
                }
            }

            internal string Format() =>
                "topologyRecords(gates=" + ScannedGatehouses + "/accepted=" + AcceptedGatehouses +
                "/fallback=" + FallbackGatehouses +
                ",bridges=" + ScannedDrawbridges + "/accepted=" + Accepted +
                "/orphanCandidates=" + OrphanBridgeCandidates +
                ",rejected=[bridge=" + InvalidBridge + ",gateId=" + GatehouseId +
                ",gateState=" + GateState + ",global=" + GlobalId +
                ",entry=" + GatehouseEntry + ",doors=" + DoorTiles +
                ",footprint=" + Footprint + ",reread=" + InconsistentReread + "])";
        }

        private readonly struct TileDiagnostic
        {
            internal TileDiagnostic(int tileId, int x, int y, bool footprint, int pcl, byte gatePath,
                int buildingId, int flags, bool walkable)
            { TileId = tileId; X = x; Y = y; Footprint = footprint; Pcl = pcl; GatePath = gatePath;
                BuildingId = buildingId; Flags = flags; Walkable = walkable; }
            internal int TileId { get; }
            internal int X { get; }
            internal int Y { get; }
            internal bool Footprint { get; }
            internal int Pcl { get; }
            internal byte GatePath { get; }
            internal int BuildingId { get; }
            internal int Flags { get; }
            internal bool Walkable { get; }
            internal string Format() => (Footprint ? "F" : "R") + TileId + "@" + X + "/" + Y +
                ":p" + Pcl + ":g" + GatePath + ":b" + BuildingId + ":f0x" + Flags.ToString("X") +
                ":w" + (Walkable ? 1 : 0);
        }
    }
}
