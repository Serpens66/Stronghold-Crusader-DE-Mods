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
    // Builds immutable policy snapshots outside native callbacks.
    internal sealed unsafe class GateTopologySnapshotProvider
    {
        private const int MaximumTopologyDetailLogs = 32;
        private const int MaximumErrorsPerCategory = 8;
        private static readonly long SnapshotInterval = Math.Max(1, Stopwatch.Frequency / 4);

        private readonly ManualLogSource log;
        private readonly object snapshotLock = new object();

        private volatile TopologySnapshot snapshot = TopologySnapshot.Empty;
        private Action<RouteTilePolicySnapshot> routePolicyConsumer;
        private Action<NativeGateAccessSnapshot> gateAccessConsumer;
        private int epochActive;
        private int epochNumber;
        private int initializedDeferredEpoch;
        private string pendingEpochReason = "map start";
        private long nextSnapshotAt;
        private ulong lastTopologyFingerprint;
        private int topologyDetailLogs;
        private int snapshotErrors;
        private long topologyBuilds;
        private long topologyChanges;

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
            consumer?.Invoke(snapshot.GateAccessPolicy);
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
                $"builds={Read(ref topologyBuilds)}, changes={Read(ref topologyChanges)}, " +
                $"snapshotErrors={Volatile.Read(ref snapshotErrors)}.");
        }

        internal void ProcessDeferred()
        {
            if (Volatile.Read(ref epochActive) == 0)
                return;
            try
            {
                InitializeDeferredEpochIfNeeded();
                RefreshTopologyIfDue(Stopwatch.GetTimestamp());
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
            // to onBeforeRender and is still capped at four times per second.
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
            Reset(ref topologyBuilds); Reset(ref topologyChanges);
            Interlocked.Exchange(ref topologyDetailLogs, 0);
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
            Volatile.Write(ref nextSnapshotAt, now + SnapshotInterval);
            snapshot = TopologySnapshot.Empty;
            routePolicyConsumer?.Invoke(RouteTilePolicySnapshot.Empty);
            gateAccessConsumer?.Invoke(NativeGateAccessSnapshot.Empty);
            lastTopologyFingerprint = 0;
            Shared.DebugLogHelper.LogInfo(log,
                $"Gate topology epoch {currentEpoch} started ({pendingEpochReason}). " +
                "Topology and immutable access snapshots are built only from onBeforeRender; " +
                "Same-PCL AI and cursorless command results remain unchanged.");
        }

        private void RefreshTopologyIfDue(long now)
        {
            long due = Volatile.Read(ref nextSnapshotAt);
            if (now < due || Interlocked.CompareExchange(ref nextSnapshotAt, now + SnapshotInterval, due) != due)
                return;
            if (!Monitor.TryEnter(snapshotLock))
                return;
            try
            {
                TopologySnapshot rebuilt = BuildTopologySnapshot();
                snapshot = rebuilt;
                routePolicyConsumer?.Invoke(rebuilt.RoutePolicy);
                gateAccessConsumer?.Invoke(rebuilt.GateAccessPolicy);
                Interlocked.Increment(ref topologyBuilds);
                if (rebuilt.Fingerprint != lastTopologyFingerprint)
                {
                    lastTopologyFingerprint = rebuilt.Fingerprint;
                    Interlocked.Increment(ref topologyChanges);
                    if (Interlocked.Increment(ref topologyDetailLogs) <= MaximumTopologyDetailLogs)
                    {
                        Shared.DebugLogHelper.LogInfo(log,
                            $"Gate/drawbridge topology changed: epoch={epochNumber}, combinations={rebuilt.Combinations.Length}, " +
                            $"fingerprint=0x{rebuilt.Fingerprint:X16}. {rebuilt.Detail}");
                    }
                }
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

        private TopologySnapshot BuildTopologySnapshot()
        {
            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            Span<GameBuilding> buildings = buildingApi.GetBuildingsAsSpan();
            var gateInfosById = new Dictionary<int, GateBridgeInfo>();
            var gateBuildingsById = new Dictionary<int, GameBuilding>();
            var combinations = new List<GateBridgeInfo>();
            var detail = new StringBuilder();
            TopologyRejections rejections = default;
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
                    rejections.Add(TopologyDiagnosticDisposition.InvalidGatehouseId);
                    continue;
                }
                if (!IsDiagnosticActive(gate->r_AliveState))
                {
                    rejections.Add(TopologyDiagnosticDisposition.InvalidGateState);
                    continue;
                }
                if (gate->r_GlobalId == 0)
                {
                    rejections.Add(TopologyDiagnosticDisposition.InvalidGlobalId);
                    continue;
                }
                GameBuilding gateSnapshot = *gate;
                PathConnectionRecord entry = *entryPointer;
                if (entry.r_SubjectGlobalId != gateSnapshot.r_GlobalId)
                {
                    rejections.Add(TopologyDiagnosticDisposition.InconsistentReread);
                    continue;
                }
                int entryTile = unchecked((int)entry.r_EntryTileId);
                int exitTile = unchecked((int)entry.r_ExitTileId);
                if (entryTile <= 0 || exitTile <= 0 ||
                    !tileApi.IsValidTileId(entryTile) || !tileApi.IsValidTileId(exitTile))
                {
                    rejections.Add(TopologyDiagnosticDisposition.InvalidDoorTiles);
                    continue;
                }
                int entryPcl = ReadPcl(tileApi, entryTile);
                int exitPcl = ReadPcl(tileApi, exitTile);
                if (!TryCollectBuildingTiles(
                        tileApi, buildingApi, gateId, gateSnapshot, out TileDiagnostic[] gateTiles))
                {
                    rejections.Add(TopologyDiagnosticDisposition.InvalidFootprint);
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
                fingerprint = Mix(fingerprint, gateInfo);
                AppendTopologyDetail(detail, gateInfo.Format());
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
                    rejections.Add(TopologyDiagnosticDisposition.InvalidGateState);
                    continue;
                }
                if (gate.r_GlobalId == 0)
                {
                    rejections.Add(TopologyDiagnosticDisposition.InvalidGlobalId);
                    continue;
                }
                if (!TryCollectBuildingTiles(
                        tileApi, buildingApi, gateId, gate, out TileDiagnostic[] gateTiles))
                {
                    rejections.Add(TopologyDiagnosticDisposition.InvalidFootprint);
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
                fingerprint = Mix(fingerprint, fallbackInfo);
                AppendTopologyDetail(detail, fallbackInfo.Format());
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
                    rejections.Add(TopologyDiagnosticDisposition.InvalidBridge);
                    continue;
                }
                if (building.r_GlobalId == 0)
                {
                    rejections.Add(TopologyDiagnosticDisposition.InvalidGlobalId);
                    continue;
                }
                if (!TryCollectBuildingTiles(
                        tileApi, buildingApi, buildingId, building, out TileDiagnostic[] bridgeTiles))
                {
                    rejections.Add(TopologyDiagnosticDisposition.InvalidFootprint);
                    continue;
                }
                int rawGatehouseId = building.r_GatehouseId;
                bool nativeLink = gateInfosById.TryGetValue(rawGatehouseId, out GateBridgeInfo gateInfo) &&
                    gateInfo.Owner == building.r_PlayerIdOwner;
                bool spatialLink = !nativeLink && TryFindUniqueAdjacentGate(
                    bridgeTiles, building.r_PlayerIdOwner, gateInfosById, out gateInfo);
                if (!nativeLink && !spatialLink)
                {
                    rejections.Add(TopologyDiagnosticDisposition.InvalidGatehouseId);
                    var orphanInfo = new GateBridgeInfo(
                        0, 0, building.r_PlayerIdOwner, building.r_CapturedByPlayerId,
                        false, 0, -1, -1, buildingId, building.r_GlobalId,
                        0, (int)building.r_AliveState, CollectRelevantPcls(bridgeTiles),
                        bridgeTiles, BuildUnrelatedPlayers(
                            building.r_PlayerIdOwner, building.r_CapturedByPlayerId),
                        rawGatehouseId, "unlinked-bridge-diagnostic");
                    combinations.Add(orphanInfo);
                    rejections.OrphanBridgeCandidates++;
                    fingerprint = Mix(fingerprint, orphanInfo);
                    string orphan = FormatOrphanBridge(
                        buildingId, building, rawGatehouseId, bridgeTiles,
                        gateBuildingsById, gateInfosById);
                    AppendTopologyDetail(detail, orphan);
                    fingerprint = MixOrphanBridge(
                        fingerprint, buildingId, building, rawGatehouseId, gateBuildingsById);
                    continue;
                }
                if (!buildingApi.TryGetBuildingById(buildingId, out GameBuilding* reread) ||
                    reread == null || reread->r_GlobalId != building.r_GlobalId ||
                    reread->r_GatehouseId != rawGatehouseId ||
                    !IsDiagnosticActive(reread->r_AliveState))
                {
                    rejections.Add(TopologyDiagnosticDisposition.InconsistentReread);
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
                fingerprint = Mix(fingerprint, bridgeInfo);
                AppendTopologyDetail(detail, bridgeInfo.Format());
            }
            if (detail.Length == 0)
                detail.Append("no active gatehouse or drawbridge record");
            detail.Append(" | ").Append(rejections.Format());
            fingerprint = Mix(fingerprint, rejections);
            GateBridgeInfo[] combinationArray = combinations.ToArray();
            TopologySnapshot previous = snapshot;
            RouteTilePolicySnapshot routePolicy = previous.Fingerprint == fingerprint
                ? previous.RoutePolicy
                : BuildRoutePolicySnapshot(tileApi, fingerprint, combinationArray);
            NativeGateAccessSnapshot gateAccessPolicy = previous.Fingerprint == fingerprint
                ? previous.GateAccessPolicy
                : BuildGateAccessSnapshot(fingerprint, combinationArray);
            return new TopologySnapshot(
                fingerprint, combinationArray, detail.ToString(), rejections,
                routePolicy, gateAccessPolicy);
        }

        private static NativeGateAccessSnapshot BuildGateAccessSnapshot(
            ulong fingerprint,
            GateBridgeInfo[] combinations)
        {
            int maximumGateId = 0;
            for (int index = 0; index < combinations.Length; index++)
                if (combinations[index].GateId > maximumGateId)
                    maximumGateId = combinations[index].GateId;
            if (maximumGateId <= 0)
                return NativeGateAccessSnapshot.Empty;

            var records = new NativeGateAccessRecord[maximumGateId + 1];
            for (int index = 0; index < combinations.Length; index++)
            {
                GateBridgeInfo info = combinations[index];
                if (info.GateId <= 0 || info.GateId >= records.Length ||
                    records[info.GateId].Valid)
                    continue;

                ushort unrelatedPlayers = 0;
                bool[] unrelated = info.UnrelatedByPlayer;
                if (unrelated != null)
                {
                    int count = Math.Min(8, unrelated.Length - 1);
                    for (int player = 1; player <= count; player++)
                        if (unrelated[player])
                            unrelatedPlayers |= unchecked((ushort)(1 << player));
                }
                records[info.GateId] = new NativeGateAccessRecord(
                    true, info.Owner, info.CapturedBy, unrelatedPlayers);
            }
            return new NativeGateAccessSnapshot(records, fingerprint);
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

        private static void AppendTopologyDetail(StringBuilder detail, string value)
        {
            if (detail.Length > 0) detail.Append(" | ");
            detail.Append(value);
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

        private static ulong Mix(ulong hash, GateBridgeInfo info)
        {
            unchecked
            {
                hash = (hash ^ (uint)info.GateId) * 1099511628211UL;
                hash = (hash ^ info.GateGlobal) * 1099511628211UL;
                hash = (hash ^ (uint)info.Owner) * 1099511628211UL;
                hash = (hash ^ (uint)info.CapturedBy) * 1099511628211UL;
                hash = (hash ^ (info.IsOpen ? 1UL : 0UL)) * 1099511628211UL;
                hash = (hash ^ (uint)info.GateAliveState) * 1099511628211UL;
                hash = (hash ^ (uint)info.EntryPcl) * 1099511628211UL;
                hash = (hash ^ (uint)info.ExitPcl) * 1099511628211UL;
                hash = (hash ^ (uint)info.BridgeId) * 1099511628211UL;
                hash = (hash ^ info.BridgeGlobal) * 1099511628211UL;
                hash = (hash ^ (uint)info.LinkedGateId) * 1099511628211UL;
                hash = (hash ^ (uint)info.BridgeAliveState) * 1099511628211UL;
                hash = (hash ^ (uint)info.RawGatehouseId) * 1099511628211UL;
                foreach (char character in info.LinkMethod)
                    hash = (hash ^ character) * 1099511628211UL;
                foreach (TileDiagnostic tile in info.Tiles)
                {
                    hash = (hash ^ (uint)tile.TileId) * 1099511628211UL;
                    hash = (hash ^ (uint)tile.Pcl) * 1099511628211UL;
                    hash = (hash ^ tile.GatePath) * 1099511628211UL;
                    hash = (hash ^ (uint)tile.BuildingId) * 1099511628211UL;
                    hash = (hash ^ (uint)tile.Flags) * 1099511628211UL;
                    hash = (hash ^ (tile.Walkable ? 1UL : 0UL)) * 1099511628211UL;
                }
                return hash;
            }
        }

        private static void Reset(ref long value) => Interlocked.Exchange(ref value, 0);
        private static long Read(ref long value) => Interlocked.Read(ref value);

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
                RouteTilePolicySnapshot.Empty, NativeGateAccessSnapshot.Empty);
            internal TopologySnapshot(ulong fingerprint, GateBridgeInfo[] combinations,
                string detail, TopologyRejections rejections,
                RouteTilePolicySnapshot routePolicy,
                NativeGateAccessSnapshot gateAccessPolicy)
            { Fingerprint = fingerprint; Combinations = combinations; Detail = detail;
                Rejections = rejections; RoutePolicy = routePolicy ?? RouteTilePolicySnapshot.Empty;
                GateAccessPolicy = gateAccessPolicy ?? NativeGateAccessSnapshot.Empty; }
            internal ulong Fingerprint { get; }
            internal GateBridgeInfo[] Combinations { get; }
            internal string Detail { get; }
            internal TopologyRejections Rejections { get; }
            internal RouteTilePolicySnapshot RoutePolicy { get; }
            internal NativeGateAccessSnapshot GateAccessPolicy { get; }
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
                text.Append(" pcls=").Append(string.Join("/", RelevantPcls)).Append(" tiles=[");
                for (int index = 0; index < Tiles.Length; index++)
                {
                    if (index > 0) text.Append(';');
                    text.Append(Tiles[index].Format());
                }
                return text.Append(']').ToString();
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
