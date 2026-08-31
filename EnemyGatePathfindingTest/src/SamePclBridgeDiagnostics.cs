using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace EnemyGatePathfindingTest
{
    internal sealed unsafe class SamePclBridgeDiagnostics
    {
        internal const int UnknownRole = 0;
        internal const int HumanRole = 1;
        internal const int AiRole = 2;

        private const int MaximumHumanSamples = 48;
        private const int MaximumHumanCursorSamples = 32;
        private const int MaximumAiSamples = 64;
        private const int MaximumAiSamplesPerPlayer = 8;
        private const int MaximumTopologyDetailLogs = 32;
        private const int MaximumCorrelationFailureLogs = 16;
        private const int MaximumErrorsPerCategory = 8;
        private const int PendingQueryCapacity = 512;
        private const int PendingMoveCapacity = 128;
        private static readonly long SnapshotInterval = Math.Max(1, Stopwatch.Frequency / 4);
        private static readonly long SummaryInterval = Stopwatch.Frequency * 10L;
        // UPDATE REVIEW (CrusaderDE.dll + Script Extender 1.42.0): revalidate that
        // cursor globals and MoveHere TileX/TileY use the same tile-coordinate space.
        private static readonly long CorrelationWindow = Math.Max(1, Stopwatch.Frequency * 3L / 2L);

        private readonly ManualLogSource log;
        private readonly int* cursorTargetX;
        private readonly int* cursorTargetY;
        private readonly object snapshotLock = new object();
        private readonly object sampleLock = new object();
        private readonly HashSet<string> sampleKeys = new HashSet<string>(StringComparer.Ordinal);
        private readonly int[] aiSamplesByPlayer = new int[9];
        private readonly long[] queriesByPlayer = new long[9];
        private readonly PendingQuery[] pendingQueries = new PendingQuery[PendingQueryCapacity];
        private readonly PendingQuery[] drainQueries = new PendingQuery[PendingQueryCapacity];
        private readonly PendingMove[] pendingMoves = new PendingMove[PendingMoveCapacity];
        private readonly PendingMove[] drainMoves = new PendingMove[PendingMoveCapacity];
        private readonly PendingQuery[] recentQueries = new PendingQuery[PendingQueryCapacity];
        private readonly QueryCorrelationCandidate[] correlationCandidates =
            new QueryCorrelationCandidate[PendingQueryCapacity];
        private readonly PendingQuery[] correlationSourceQueries =
            new PendingQuery[PendingQueryCapacity];

        private volatile TopologySnapshot snapshot = TopologySnapshot.Empty;
        private int epochActive;
        private int epochNumber;
        private int initializedDeferredEpoch;
        private string pendingEpochReason = "first valid PCL query";
        private long pendingWriteSequence;
        private long pendingReadSequence;
        private int pendingGate;
        private long pendingMoveWriteSequence;
        private long pendingMoveReadSequence;
        private int pendingMoveGate;
        private int recentQueryCount;
        private int recentQueryNext;
        private long nextSnapshotAt;
        private long nextSummaryAt;
        private ulong lastTopologyFingerprint;
        private int humanSamples;
        private int humanCursorSamples;
        private int aiSamples;
        private int topologyDetailLogs;
        private int queryErrors;
        private int snapshotErrors;
        private int sampleErrors;
        private int correlationFailureLogs;

        private long queries;
        private long humanQueries;
        private long aiQueries;
        private long unknownQueries;
        private long samePclQueries;
        private long differentPclQueries;
        private long positiveResults;
        private long negativeResults;
        private long filterReachedQueries;
        private long filterBypassedQueries;
        private long samePclCandidates;
        private long differentPclGateCandidates;
        private long topologyBuilds;
        private long topologyChanges;
        private long moveHereCalls;
        private long moveHereHuman;
        private long moveHereAi;
        private long moveHerePositive;
        private long moveHereNegative;
        private long moveHereCorrelated;
        private long moveHereCorrelationNoHistory;
        private long moveHereCorrelationNoPlayer;
        private long moveHereCorrelationNoCoordinates;
        private long moveHereCorrelationNoPositiveSamePcl;
        private long moveHereCorrelationPclMismatch;
        private long suppressedHumanSamples;
        private long suppressedAiSamples;
        private long droppedPendingQueries;
        private long droppedPendingMoves;

        [ThreadStatic]
        private static int moveHereDepth;
        [ThreadStatic]
        private static PendingMove activeMove;

        internal SamePclBridgeDiagnostics(ManualLogSource log, int* cursorTargetX, int* cursorTargetY)
        {
            this.log = log;
            this.cursorTargetX = cursorTargetX;
            this.cursorTargetY = cursorTargetY;
        }

        internal void BeginExplicitEpoch(string reason)
        {
            if (Interlocked.CompareExchange(ref epochActive, 1, 0) != 0)
                return;
            pendingEpochReason = reason ?? "unspecified";
            Interlocked.Increment(ref epochNumber);
            ResetHotCounters();
        }

        internal void EnsureEpochForQuery()
        {
            if (Volatile.Read(ref epochActive) == 0)
                BeginExplicitEpoch("first valid PCL query; supports map editor without OnStartMap(Post)");
        }

        internal void EndEpoch(string reason)
        {
            if (Interlocked.CompareExchange(ref epochActive, 0, 1) != 1)
                return;
            LogSummary("final", reason);
        }

        internal void OnMoveHere(UnitMoveHereEventArgs args)
        {
            if (args == null)
                return;
            if (args.Phase == EventHookPhase.Pre)
            {
                // Primitive context only. No API calls or logging are allowed while
                // Script Extender is about to enter the native MoveHere function.
                if (Volatile.Read(ref epochActive) == 0)
                    BeginExplicitEpoch("first MoveHere command");
                moveHereDepth = 1;
                activeMove = new PendingMove(
                    Stopwatch.GetTimestamp(), args.UnitId, args.TileX, args.TileY, args.Unknown, 0);
                Interlocked.Increment(ref moveHereCalls);
            }
            else if (args.Phase == EventHookPhase.Post)
            {
                if (moveHereDepth > 0)
                {
                    if (args.ReturnValue == 0)
                        Interlocked.Increment(ref moveHereNegative);
                    else
                        Interlocked.Increment(ref moveHerePositive);
                    QueuePendingMove(activeMove.WithResult(args.ReturnValue));
                    moveHereDepth = 0;
                    activeMove = default;
                }
            }
        }

        private void QueuePendingMove(PendingMove move)
        {
            if (Interlocked.CompareExchange(ref pendingMoveGate, 1, 0) != 0)
            {
                Interlocked.Increment(ref droppedPendingMoves);
                return;
            }
            try
            {
                long write = pendingMoveWriteSequence;
                long read = pendingMoveReadSequence;
                if (write - read >= PendingMoveCapacity)
                {
                    Interlocked.Increment(ref droppedPendingMoves);
                    return;
                }
                pendingMoves[(int)(write % PendingMoveCapacity)] = move;
                pendingMoveWriteSequence = write + 1;
            }
            finally
            {
                Volatile.Write(ref pendingMoveGate, 0);
            }
        }

        internal void ObserveQuery(
            int playerId,
            int sourcePcl,
            int targetPcl,
            int mode,
            long result,
            int filterRecordCount)
        {
            EnsureEpochForQuery();
            long timestamp = Stopwatch.GetTimestamp();
            Interlocked.Increment(ref queries);
            if (playerId >= 1 && playerId < queriesByPlayer.Length)
                Interlocked.Increment(ref queriesByPlayer[playerId]);
            if (result == 0)
                Interlocked.Increment(ref negativeResults);
            else
                Interlocked.Increment(ref positiveResults);

            bool samePcl = sourcePcl == targetPcl;
            if (samePcl)
                Interlocked.Increment(ref samePclQueries);
            else
                Interlocked.Increment(ref differentPclQueries);
            if (filterRecordCount == 0)
                Interlocked.Increment(ref filterBypassedQueries);
            else
                Interlocked.Increment(ref filterReachedQueries);
            if (!EnemyGatePathfindingPolicy.ShouldQueueDeferredDiagnostic(
                sourcePcl, targetPcl, result, filterRecordCount))
                return;
            if (Interlocked.CompareExchange(ref pendingGate, 1, 0) != 0)
            {
                Interlocked.Increment(ref droppedPendingQueries);
                return;
            }
            try
            {
                long write = pendingWriteSequence;
                long read = pendingReadSequence;
                if (write - read >= PendingQueryCapacity)
                {
                    Interlocked.Increment(ref droppedPendingQueries);
                    return;
                }
                int cursorX = cursorTargetX == null ? -1 : *cursorTargetX;
                int cursorY = cursorTargetY == null ? -1 : *cursorTargetY;
                pendingQueries[(int)(write % PendingQueryCapacity)] = new PendingQuery(
                    timestamp, playerId, sourcePcl, targetPcl, mode, result,
                    filterRecordCount, cursorX, cursorY);
                pendingWriteSequence = write + 1;
            }
            finally
            {
                Volatile.Write(ref pendingGate, 0);
            }
        }

        internal void ProcessDeferred()
        {
            if (Volatile.Read(ref epochActive) == 0)
                return;
            try
            {
                InitializeDeferredEpochIfNeeded();
                long now = Stopwatch.GetTimestamp();
                RefreshTopologyIfDue(now);
                TopologySnapshot current = snapshot;
                int drainCount = DrainPendingQueries();
                for (int index = 0; index < drainCount; index++)
                {
                    PendingQuery query = drainQueries[index];
                    AddRecentQuery(query);
                    int role = ResolveRole(query.PlayerId);
                    CandidateMatch candidate = FindCandidate(
                        current, query.PlayerId, query.SourcePcl, query.TargetPcl);
                    if (candidate.Found)
                    {
                        if (query.SourcePcl == query.TargetPcl)
                            Interlocked.Increment(ref samePclCandidates);
                        else
                            Interlocked.Increment(ref differentPclGateCandidates);
                    }
                    bool potentiallyRelevant = candidate.Found || query.FilterRecordCount > 0;
                    CursorContext cursor = role == HumanRole && potentiallyRelevant
                        ? CaptureCursorContext(query)
                        : default;
                    if (ShouldSampleQuery(query, role, candidate, cursor) &&
                        TryReserveSample(query.PlayerId, role, query.Mode,
                            query.SourcePcl, query.TargetPcl,
                            query.CursorX, query.CursorY, cursor.SelectionSignature,
                            candidate, "cursor"))
                    {
                        LogSample(query, role, candidate, cursor, default, default, false);
                    }
                }
                int moveCount = DrainPendingMoves();
                for (int index = 0; index < moveCount; index++)
                    ProcessDeferredMove(drainMoves[index], current);
                RefreshRoleCounters();
                MaybeLogPeriodicSummary(now);
            }
            catch (Exception ex)
            {
                LogBoundedError(ref queryErrors,
                    "Deferred Same-PCL diagnostics failed without changing game state", ex);
            }
        }

        internal void RecordHotPathFailure()
        {
            Interlocked.Increment(ref queryErrors);
        }

        private int DrainPendingQueries()
        {
            if (Interlocked.CompareExchange(ref pendingGate, 1, 0) != 0)
                return 0;
            try
            {
                long read = pendingReadSequence;
                long write = pendingWriteSequence;
                int count = (int)Math.Min(PendingQueryCapacity, Math.Max(0, write - read));
                for (int index = 0; index < count; index++)
                    drainQueries[index] = pendingQueries[(int)((read + index) % PendingQueryCapacity)];
                pendingReadSequence = read + count;
                return count;
            }
            finally
            {
                Volatile.Write(ref pendingGate, 0);
            }
        }

        private int DrainPendingMoves()
        {
            if (Interlocked.CompareExchange(ref pendingMoveGate, 1, 0) != 0)
                return 0;
            try
            {
                long read = pendingMoveReadSequence;
                long write = pendingMoveWriteSequence;
                int count = (int)Math.Min(PendingMoveCapacity, Math.Max(0, write - read));
                for (int index = 0; index < count; index++)
                    drainMoves[index] = pendingMoves[(int)((read + index) % PendingMoveCapacity)];
                pendingMoveReadSequence = read + count;
                return count;
            }
            finally
            {
                Volatile.Write(ref pendingMoveGate, 0);
            }
        }

        private void AddRecentQuery(PendingQuery query)
        {
            recentQueries[recentQueryNext] = query;
            recentQueryNext = (recentQueryNext + 1) % recentQueries.Length;
            if (recentQueryCount < recentQueries.Length)
                recentQueryCount++;
        }

        private void ResetHotCounters()
        {
            Volatile.Write(ref pendingWriteSequence, 0);
            Volatile.Write(ref pendingReadSequence, 0);
            Volatile.Write(ref pendingMoveWriteSequence, 0);
            Volatile.Write(ref pendingMoveReadSequence, 0);
            recentQueryCount = 0;
            recentQueryNext = 0;
            Reset(ref queries); Reset(ref humanQueries); Reset(ref aiQueries); Reset(ref unknownQueries);
            Reset(ref samePclQueries); Reset(ref differentPclQueries);
            Reset(ref positiveResults); Reset(ref negativeResults);
            Reset(ref filterReachedQueries); Reset(ref filterBypassedQueries);
            Reset(ref samePclCandidates); Reset(ref differentPclGateCandidates);
            Reset(ref topologyBuilds); Reset(ref topologyChanges);
            Reset(ref moveHereCalls); Reset(ref moveHereHuman); Reset(ref moveHereAi);
            Reset(ref moveHerePositive); Reset(ref moveHereNegative); Reset(ref moveHereCorrelated);
            Reset(ref moveHereCorrelationNoHistory); Reset(ref moveHereCorrelationNoPlayer);
            Reset(ref moveHereCorrelationNoCoordinates); Reset(ref moveHereCorrelationNoPositiveSamePcl);
            Reset(ref moveHereCorrelationPclMismatch);
            Reset(ref suppressedHumanSamples); Reset(ref suppressedAiSamples);
            Reset(ref droppedPendingQueries); Reset(ref droppedPendingMoves);
            humanSamples = 0;
            humanCursorSamples = 0;
            aiSamples = 0;
            for (int player = 0; player < queriesByPlayer.Length; player++)
                Reset(ref queriesByPlayer[player]);
            Interlocked.Exchange(ref topologyDetailLogs, 0);
            Interlocked.Exchange(ref correlationFailureLogs, 0);
            Interlocked.Exchange(ref queryErrors, 0);
            Interlocked.Exchange(ref snapshotErrors, 0);
            Interlocked.Exchange(ref sampleErrors, 0);
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
            Volatile.Write(ref nextSummaryAt, now + SummaryInterval);
            snapshot = TopologySnapshot.Empty;
            lastTopologyFingerprint = 0;
            lock (sampleLock)
            {
                sampleKeys.Clear();
                Array.Clear(aiSamplesByPlayer, 0, aiSamplesByPlayer.Length);
                humanSamples = 0;
                humanCursorSamples = 0;
                aiSamples = 0;
            }
            Shared.DebugLogHelper.LogInfo(log,
                $"Same-PCL diagnostic epoch {currentEpoch} started ({pendingEpochReason}). " +
                "Hot-path collection is primitive-only; topology and samples are deferred to onBeforeRender. " +
                "Same-PCL return values remain unchanged.");
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

            // UPDATE REVIEW (Script Extender 1.42.0): the native gatehouse array is
            // authoritative for standalone gates and supplies the public building ID.
            var gateEntries = buildingApi.GetGatehouseArray();
            for (int entryIndex = 0; entryIndex < gateEntries.Length; entryIndex++)
            {
                GameGatehouseEntry* entryPointer = gateEntries.GetValuePointer(entryIndex);
                if (entryPointer == null || entryPointer->r_BuildingId == 0 ||
                    entryPointer->r_BuildingId > int.MaxValue)
                    continue;
                rejections.ScannedGatehouses++;
                int gateId = unchecked((int)entryPointer->r_BuildingId);
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
                GameGatehouseEntry entry = *entryPointer;
                if (entry.r_GlobalId != gateSnapshot.r_GlobalId)
                {
                    rejections.Add(TopologyDiagnosticDisposition.InconsistentReread);
                    continue;
                }
                int entryTile = unchecked((int)entry.r_EntryDoorTileId);
                int exitTile = unchecked((int)entry.r_ExitDoorTileId);
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
                    gateSnapshot.r_CapturedByPlayerId, entry.r_IsOpen != 0,
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

            // UPDATE REVIEW (Script Extender 1.42.0): a newly placed editor gate may
            // still be NeedsInit and absent from GetGatehouseArray(). Retain a clearly
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

            // UPDATE REVIEW (Script Extender 1.42.0): the Span is zero-based and its
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
                if (!gateInfosById.TryGetValue(rawGatehouseId, out GateBridgeInfo gateInfo))
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
                        buildingId, building, rawGatehouseId, bridgeTiles, gateBuildingsById);
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
                    buildingId, building.r_GlobalId, rawGatehouseId, (int)building.r_AliveState,
                    CollectRelevantPcls(bridgeTiles), bridgeTiles, gateInfo.UnrelatedByPlayer,
                    rawGatehouseId, "native-building-id");
                combinations.Add(bridgeInfo);
                rejections.Add(TopologyDiagnosticDisposition.Accepted);
                fingerprint = Mix(fingerprint, bridgeInfo);
                AppendTopologyDetail(detail, bridgeInfo.Format());
            }
            if (detail.Length == 0)
                detail.Append("no active gatehouse or drawbridge record");
            detail.Append(" | ").Append(rejections.Format());
            fingerprint = Mix(fingerprint, rejections);
            return new TopologySnapshot(
                fingerprint, combinations.ToArray(), detail.ToString(), rejections);
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
            // UPDATE REVIEW (Script Extender 1.42.0): this API reads gridSize squared
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
            Dictionary<int, GameBuilding> gates)
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
                .Append(" spatialGateCandidates=[");
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

        private CandidateMatch FindCandidate(
            TopologySnapshot current, int playerId, int sourcePcl, int targetPcl)
        {
            for (int infoIndex = current.Combinations.Length - 1; infoIndex >= 0; infoIndex--)
            {
                GateBridgeInfo info = current.Combinations[infoIndex];
                if (playerId < 1 || playerId >= info.UnrelatedByPlayer.Length ||
                    !info.UnrelatedByPlayer[playerId])
                    continue;
                if (EnemyGatePathfindingPolicy.IsTopologyRelevantToQuery(
                        sourcePcl, targetPcl, info.EntryPcl, info.ExitPcl,
                        info.RelevantPcls))
                    return new CandidateMatch(info);
            }
            return default;
        }

        private static bool ShouldSampleQuery(
            PendingQuery query, int role, CandidateMatch match, CursorContext cursor)
        {
            bool relevant = match.Found || query.FilterRecordCount > 0;
            if (!relevant || role == UnknownRole)
                return false;
            return role == AiRole || (cursor.Valid && cursor.SelectionMatchesPlayer);
        }

        private CursorContext CaptureCursorContext(PendingQuery query)
        {
            try
            {
                GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
                if (!tiles.IsTileInsideMapBounds(query.CursorX, query.CursorY))
                    return default;
                int tile = tiles.GetTileId(query.CursorX, query.CursorY);
                if (!tiles.IsValidTileId(tile))
                    return default;
                int[] selected = GamePlayerManagerAPI.Instance.GetSelectedChimps();
                int count = Math.Min(selected.Length, 16);
                int[] captured = new int[count];
                ulong signature = 1469598103934665603UL;
                bool matches = false;
                for (int index = 0; index < count; index++)
                {
                    int unitId = selected[index];
                    captured[index] = unitId;
                    signature = unchecked((signature ^ (uint)unitId) * 1099511628211UL);
                    if (GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) &&
                        unit != null && unit->r_ControllableForPlayerId == query.PlayerId)
                    {
                        matches = true;
                    }
                }
                return new CursorContext(true, tile, ReadPcl(tiles, tile), captured,
                    selected.Length, signature, matches);
            }
            catch (Exception ex)
            {
                LogBoundedError(ref sampleErrors, "Deferred cursor/selection capture failed", ex);
                return default;
            }
        }

        private void ProcessDeferredMove(PendingMove move, TopologySnapshot current)
        {
            MoveResolution resolution = ResolveMove(move);
            if (resolution.Role == HumanRole) Interlocked.Increment(ref moveHereHuman);
            else if (resolution.Role == AiRole) Interlocked.Increment(ref moveHereAi);

            int candidateCount = CopyRecentCorrelationCandidates();
            int correlationIndex = resolution.PlayerId > 0 && resolution.TargetPcl >= 0
                ? EnemyGatePathfindingPolicy.FindNearestPrecedingCorrelation(
                    correlationCandidates, candidateCount, move.Timestamp, CorrelationWindow,
                    resolution.PlayerId, move.TileX, move.TileY, resolution.TargetPcl)
                : -1;
            if (correlationIndex < 0)
            {
                string reason = ClassifyCorrelationFailure(move, resolution, candidateCount);
                CountCorrelationFailure(reason);
                LogCorrelationFailureBounded(move, resolution, reason);
                return;
            }

            Interlocked.Increment(ref moveHereCorrelated);
            PendingQuery query = correlationSourceQueries[correlationIndex];
            CandidateMatch candidate = FindCandidate(
                current, query.PlayerId, query.SourcePcl, query.TargetPcl);
            if (!candidate.Found)
                return;
            CursorContext cursor = CaptureCursorContext(query);
            if (TryReserveSample(query.PlayerId, resolution.Role, query.Mode,
                    query.SourcePcl, query.TargetPcl,
                    query.CursorX, query.CursorY, cursor.SelectionSignature,
                    candidate, "move-correlated"))
            {
                LogSample(query, resolution.Role, candidate, cursor, move, resolution, true);
            }
        }

        private MoveResolution ResolveMove(PendingMove move)
        {
            try
            {
                int playerId = -1;
                int sourceTile = -1;
                int sourcePcl = -1;
                int sourceX = -1;
                int sourceY = -1;
                if (GameUnitManagerAPI.Instance.TryGetUnitById(move.UnitId, out GameUnit* unit) && unit != null)
                {
                    playerId = unit->r_ControllableForPlayerId;
                    sourceTile = unchecked((int)unit->r_CurrentPositionTileId);
                    sourceX = unit->r_CurrentTilePositionX;
                    sourceY = unit->r_CurrentTilePositionY;
                    sourcePcl = ReadPcl(GameTileManagerAPI.Instance, sourceTile);
                }
                GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
                int targetTile = tiles.IsTileInsideMapBounds(move.TileX, move.TileY)
                    ? tiles.GetTileId(move.TileX, move.TileY) : -1;
                int targetPcl = ReadPcl(tiles, targetTile);
                return new MoveResolution(playerId, ResolveRole(playerId), sourceX, sourceY,
                    sourceTile, sourcePcl, targetTile, targetPcl);
            }
            catch (Exception ex)
            {
                LogBoundedError(ref sampleErrors, "Deferred MoveHere resolution failed", ex);
                return default;
            }
        }

        private int CopyRecentCorrelationCandidates()
        {
            int count = recentQueryCount;
            int start = count == recentQueries.Length ? recentQueryNext : 0;
            for (int index = 0; index < count; index++)
            {
                PendingQuery query = recentQueries[(start + index) % recentQueries.Length];
                correlationSourceQueries[index] = query;
                correlationCandidates[index] = new QueryCorrelationCandidate(
                    query.Timestamp, query.PlayerId, query.CursorX, query.CursorY,
                    query.SourcePcl, query.TargetPcl, query.Result);
            }
            return count;
        }

        private string ClassifyCorrelationFailure(
            PendingMove move, MoveResolution resolution, int candidateCount)
        {
            if (candidateCount == 0 || resolution.PlayerId <= 0 || resolution.TargetPcl < 0)
                return "no-valid-history-or-move";
            bool inWindow = false;
            bool player = false;
            bool coordinates = false;
            bool positiveSame = false;
            for (int index = 0; index < candidateCount; index++)
            {
                QueryCorrelationCandidate candidate = correlationCandidates[index];
                long age = move.Timestamp - candidate.Timestamp;
                if (age < 0 || age > CorrelationWindow)
                    continue;
                inWindow = true;
                if (candidate.PlayerId != resolution.PlayerId)
                    continue;
                player = true;
                if (candidate.CursorX != move.TileX || candidate.CursorY != move.TileY)
                    continue;
                coordinates = true;
                if (candidate.SourcePcl != candidate.TargetPcl || candidate.Result == 0)
                    continue;
                positiveSame = true;
                if (candidate.TargetPcl == resolution.TargetPcl)
                    return "unexpected-match-miss";
            }
            if (!inWindow) return "no-query-in-window";
            if (!player) return "player-mismatch";
            if (!coordinates) return "target-coordinate-mismatch";
            if (!positiveSame) return "no-positive-same-pcl";
            return "target-pcl-mismatch";
        }

        private void CountCorrelationFailure(string reason)
        {
            if (reason == "player-mismatch") Interlocked.Increment(ref moveHereCorrelationNoPlayer);
            else if (reason == "target-coordinate-mismatch") Interlocked.Increment(ref moveHereCorrelationNoCoordinates);
            else if (reason == "no-positive-same-pcl") Interlocked.Increment(ref moveHereCorrelationNoPositiveSamePcl);
            else if (reason == "target-pcl-mismatch") Interlocked.Increment(ref moveHereCorrelationPclMismatch);
            else Interlocked.Increment(ref moveHereCorrelationNoHistory);
        }

        private void LogCorrelationFailureBounded(
            PendingMove move, MoveResolution resolution, string reason)
        {
            if (resolution.Role != HumanRole)
                return;
            int count = Interlocked.Increment(ref correlationFailureLogs);
            if (count > MaximumCorrelationFailureLogs)
                return;
            Shared.DebugLogHelper.LogInfo(log,
                $"MoveHere correlation miss ({count}/{MaximumCorrelationFailureLogs}): " +
                $"role={FormatRole(resolution.Role)}, player={resolution.PlayerId}, unit={move.UnitId}, " +
                $"target={move.TileX}/{move.TileY}, targetTile={resolution.TargetTile}, " +
                $"targetPcl={resolution.TargetPcl}, result={move.Result}, reason={reason}.");
        }

        private bool TryReserveSample(int playerId, int role, int mode,
            int sourcePcl, int targetPcl,
            int cursorX, int cursorY, ulong selectionSignature,
            CandidateMatch match, string origin)
        {
            string key = playerId + ":" + sourcePcl + ":" + targetPcl + ":" + mode + ":" + match.GateId + ":" +
                match.BridgeId + ":" + origin + ":" + cursorX + ":" + cursorY + ":" +
                selectionSignature.ToString("X");
            lock (sampleLock)
            {
                if (!sampleKeys.Add(key))
                    return false;
                if (role == UnknownRole)
                    return false;
                if (role == AiRole)
                {
                    if (aiSamples >= MaximumAiSamples || playerId < 1 || playerId > 8 ||
                        aiSamplesByPlayer[playerId] >= MaximumAiSamplesPerPlayer)
                    {
                        Interlocked.Increment(ref suppressedAiSamples);
                        return false;
                    }
                    aiSamples++;
                    aiSamplesByPlayer[playerId]++;
                    return true;
                }
                if (humanSamples >= MaximumHumanSamples)
                {
                    Interlocked.Increment(ref suppressedHumanSamples);
                    return false;
                }
                if (origin == "cursor" && humanCursorSamples >= MaximumHumanCursorSamples)
                {
                    Interlocked.Increment(ref suppressedHumanSamples);
                    return false;
                }
                humanSamples++;
                if (origin == "cursor") humanCursorSamples++;
                return true;
            }
        }

        private void LogSample(PendingQuery query, int role, CandidateMatch match,
            CursorContext cursor, PendingMove pendingMove, MoveResolution move, bool correlatedMove)
        {
            var message = new StringBuilder();
            message.Append("Gate-path diagnostic sample: role=").Append(FormatRole(role))
                .Append(", player=").Append(query.PlayerId)
                .Append(", sourcePcl=").Append(query.SourcePcl)
                .Append(", targetPcl=").Append(query.TargetPcl)
                .Append(", mode=").Append(query.Mode)
                .Append(", pclResult=").Append(query.Result)
                .Append(", samePcl=").Append(query.SourcePcl == query.TargetPcl)
                .Append(", capturerFilter=").Append(query.FilterRecordCount == 0 ? "bypassed" : "reached")
                .Append(", callerClass=").Append(correlatedMove ? "cursor-to-MoveHere" : "central-direct-unavailable")
                .Append(", candidate=").Append(match.Found);
            if (match.Found)
                message.Append(", gate=").Append(match.Info.Format());
            if (correlatedMove)
                AppendMoveHere(message, pendingMove, move);
            if (role == HumanRole)
                AppendCursorAndSelection(message, query, cursor);
            Shared.DebugLogHelper.LogInfo(log, message.ToString());
        }

        private static void AppendMoveHere(
            StringBuilder message, PendingMove pending, MoveResolution move)
        {
            message.Append(", moveHere=[unit=").Append(pending.UnitId)
                .Append(", player=").Append(move.PlayerId)
                .Append(", source=").Append(move.SourceX).Append('/').Append(move.SourceY)
                .Append(", sourceTile=").Append(move.SourceTile)
                .Append(", sourcePcl=").Append(move.SourcePcl)
                .Append(", target=").Append(pending.TileX).Append('/').Append(pending.TileY)
                .Append(", targetTile=").Append(move.TargetTile)
                .Append(", targetPcl=").Append(move.TargetPcl)
                .Append(", unknown=").Append(pending.Unknown)
                .Append(", result=").Append(pending.Result).Append(']');
        }

        private static void AppendCursorAndSelection(
            StringBuilder message, PendingQuery query, CursorContext cursor)
        {
            message.Append(", cursor=[xy=").Append(query.CursorX).Append('/').Append(query.CursorY)
                .Append(", valid=").Append(cursor.Valid)
                .Append(", tile=").Append(cursor.Tile).Append(", pcl=").Append(cursor.Pcl)
                .Append(", pclResult=").Append(query.Result).Append(']')
                .Append(", selected=[");
            int[] selected = cursor.SelectedUnitIds ?? Array.Empty<int>();
            for (int index = 0; index < selected.Length; index++)
            {
                if (index > 0) message.Append(';');
                message.Append(selected[index]);
                AppendUnit(message, selected[index]);
            }
            if (cursor.TotalSelected > selected.Length)
                message.Append(";+").Append(cursor.TotalSelected - selected.Length);
            message.Append("] selectionMatchesPlayer=").Append(cursor.SelectionMatchesPlayer);
        }

        private static void AppendUnit(StringBuilder message, int unitId)
        {
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) || unit == null)
            {
                message.Append("(invalid)");
                return;
            }
            int tile = unchecked((int)unit->r_CurrentPositionTileId);
            int pcl = tile >= 0 && GameTileManagerAPI.Instance.IsValidTileId(tile)
                ? ReadPcl(GameTileManagerAPI.Instance, tile) : -1;
            message.Append("(owner=").Append(unit->r_ControllableForPlayerId)
                .Append(", sourceXY=").Append(unit->r_CurrentTilePositionX).Append('/')
                .Append(unit->r_CurrentTilePositionY).Append(", sourceTile=").Append(tile)
                .Append(", sourcePcl=").Append(pcl).Append(')');
        }

        private void MaybeLogPeriodicSummary(long now)
        {
            long due = Volatile.Read(ref nextSummaryAt);
            if (now < due || Interlocked.CompareExchange(ref nextSummaryAt, now + SummaryInterval, due) != due)
                return;
            LogSummary("periodic", "10-second interval");
        }

        private void RefreshRoleCounters()
        {
            long human = 0;
            long ai = 0;
            long unknown = Math.Max(0, Read(ref queries));
            for (int player = 1; player <= 8; player++)
            {
                long count = Read(ref queriesByPlayer[player]);
                unknown = Math.Max(0, unknown - count);
                int role = ResolveRole(player);
                if (role == HumanRole)
                {
                    human += count;
                }
                else if (role == AiRole)
                {
                    ai += count;
                }
                else
                {
                    unknown += count;
                }
            }
            Interlocked.Exchange(ref humanQueries, human);
            Interlocked.Exchange(ref aiQueries, ai);
            Interlocked.Exchange(ref unknownQueries, unknown);
        }

        private static int ResolveRole(int playerId)
        {
            try
            {
                GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
                if (!players.IsPlayerIdValid(playerId))
                    return UnknownRole;
                return players.IsAIPlayer(playerId) ? AiRole : HumanRole;
            }
            catch
            {
                return UnknownRole;
            }
        }

        private void LogSummary(string kind, string reason)
        {
            long moveTotal = Read(ref moveHereCalls);
            long moveHuman = Read(ref moveHereHuman);
            long moveAi = Read(ref moveHereAi);
            long moveUnknown = EnemyGatePathfindingPolicy.CalculateUnknownRoleCount(
                moveTotal, moveHuman, moveAi);
            TopologySnapshot current = snapshot;
            Shared.DebugLogHelper.LogInfo(log,
                $"Same-PCL {kind} summary: epoch={epochNumber}, reason={reason}, queries={Read(ref queries)} " +
                $"(human={Read(ref humanQueries)}, ai={Read(ref aiQueries)}, unknown={Read(ref unknownQueries)}), " +
                $"same={Read(ref samePclQueries)}, different={Read(ref differentPclQueries)}, " +
                $"positive={Read(ref positiveResults)}, negative={Read(ref negativeResults)}, " +
                $"capturerFilter(reached={Read(ref filterReachedQueries)}, bypassed={Read(ref filterBypassedQueries)}), " +
                $"candidates(samePcl={Read(ref samePclCandidates)}," +
                $"differentPclGate={Read(ref differentPclGateCandidates)}), MoveHere={moveTotal} " +
                $"(human={moveHuman}, ai={moveAi}, unknown={moveUnknown}, " +
                $"positive={Read(ref moveHerePositive)}, negative={Read(ref moveHereNegative)}, " +
                $"correlated={Read(ref moveHereCorrelated)}, misses=[history={Read(ref moveHereCorrelationNoHistory)}," +
                $"player={Read(ref moveHereCorrelationNoPlayer)},xy={Read(ref moveHereCorrelationNoCoordinates)}," +
                $"samePositive={Read(ref moveHereCorrelationNoPositiveSamePcl)}," +
                $"pcl={Read(ref moveHereCorrelationPclMismatch)}]), topology(builds={Read(ref topologyBuilds)}, " +
                $"changes={Read(ref topologyChanges)}, {current.Rejections.Format()}), " +
                $"samples(human={humanSamples}/cursor={humanCursorSamples}, ai={aiSamples}, " +
                $"suppressedHuman={Read(ref suppressedHumanSamples)}, suppressedAi={Read(ref suppressedAiSamples)}), " +
                $"pendingDropped(query={Read(ref droppedPendingQueries)},move={Read(ref droppedPendingMoves)}), " +
                $"topologyDetailLogs={Math.Min(topologyDetailLogs, MaximumTopologyDetailLogs)}/" +
                $"suppressed={Math.Max(0, topologyDetailLogs - MaximumTopologyDetailLogs)}, " +
                $"errors(query={queryErrors}, snapshot={snapshotErrors}, sample={sampleErrors}).");
        }

        private void LogBoundedError(ref int counter, string category, Exception ex)
        {
            int count = Interlocked.Increment(ref counter);
            if (count <= MaximumErrorsPerCategory)
                Shared.DebugLogHelper.LogWarning(log,
                    $"{category} ({count}/{MaximumErrorsPerCategory}): {ex.GetType().Name}: {ex.Message}");
        }

        private static int ReadPcl(GameTileManagerAPI tiles, int tileId) =>
            tileId >= 0 && tiles.IsValidTileId(tileId) ? tiles.TileManager.PathConnectionGrid[tileId] : -1;

        private static bool IsDiagnosticActive(AliveState aliveState) =>
            EnemyGatePathfindingPolicy.IsDiagnosticBuildingActive((int)aliveState);

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

        private static string FormatRole(int role) => role == HumanRole ? "human" : role == AiRole ? "ai" : "unknown";
        private static void Reset(ref long value) => Interlocked.Exchange(ref value, 0);
        private static long Read(ref long value) => Interlocked.Read(ref value);

        private readonly struct PendingQuery
        {
            internal PendingQuery(long timestamp, int playerId, int sourcePcl, int targetPcl,
                int mode, long result, int filterRecordCount, int cursorX, int cursorY)
            {
                Timestamp = timestamp;
                PlayerId = playerId;
                SourcePcl = sourcePcl;
                TargetPcl = targetPcl;
                Mode = mode;
                Result = result;
                FilterRecordCount = filterRecordCount;
                CursorX = cursorX;
                CursorY = cursorY;
            }

            internal long Timestamp { get; }
            internal int PlayerId { get; }
            internal int SourcePcl { get; }
            internal int TargetPcl { get; }
            internal int Mode { get; }
            internal long Result { get; }
            internal int FilterRecordCount { get; }
            internal int CursorX { get; }
            internal int CursorY { get; }
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

        private readonly struct PendingMove
        {
            internal PendingMove(long timestamp, int unitId, int tileX, int tileY, int unknown, long result)
            { Timestamp = timestamp; UnitId = unitId; TileX = tileX; TileY = tileY;
                Unknown = unknown; Result = result; }
            internal long Timestamp { get; }
            internal int UnitId { get; }
            internal int TileX { get; }
            internal int TileY { get; }
            internal int Unknown { get; }
            internal long Result { get; }
            internal PendingMove WithResult(long result) =>
                new PendingMove(Timestamp, UnitId, TileX, TileY, Unknown, result);
        }

        private readonly struct CursorContext
        {
            internal CursorContext(bool valid, int tile, int pcl, int[] selectedUnitIds,
                int totalSelected, ulong selectionSignature, bool selectionMatchesPlayer)
            { Valid = valid; Tile = tile; Pcl = pcl; SelectedUnitIds = selectedUnitIds;
                TotalSelected = totalSelected; SelectionSignature = selectionSignature;
                SelectionMatchesPlayer = selectionMatchesPlayer; }
            internal bool Valid { get; }
            internal int Tile { get; }
            internal int Pcl { get; }
            internal int[] SelectedUnitIds { get; }
            internal int TotalSelected { get; }
            internal ulong SelectionSignature { get; }
            internal bool SelectionMatchesPlayer { get; }
        }

        private readonly struct MoveResolution
        {
            internal MoveResolution(int playerId, int role, int sourceX, int sourceY,
                int sourceTile, int sourcePcl, int targetTile, int targetPcl)
            { PlayerId = playerId; Role = role; SourceX = sourceX; SourceY = sourceY;
                SourceTile = sourceTile; SourcePcl = sourcePcl; TargetTile = targetTile;
                TargetPcl = targetPcl; }
            internal int PlayerId { get; }
            internal int Role { get; }
            internal int SourceX { get; }
            internal int SourceY { get; }
            internal int SourceTile { get; }
            internal int SourcePcl { get; }
            internal int TargetTile { get; }
            internal int TargetPcl { get; }
        }

        private readonly struct CandidateMatch
        {
            internal CandidateMatch(GateBridgeInfo info) { Info = info; Found = true; }
            internal bool Found { get; }
            internal GateBridgeInfo Info { get; }
            internal int GateId => Found ? Info.GateId : 0;
            internal int BridgeId => Found ? Info.BridgeId : 0;
        }

        private sealed class TopologySnapshot
        {
            internal static readonly TopologySnapshot Empty = new TopologySnapshot(
                0, Array.Empty<GateBridgeInfo>(), "not captured", default);
            internal TopologySnapshot(ulong fingerprint, GateBridgeInfo[] combinations,
                string detail, TopologyRejections rejections)
            { Fingerprint = fingerprint; Combinations = combinations; Detail = detail;
                Rejections = rejections; }
            internal ulong Fingerprint { get; }
            internal GateBridgeInfo[] Combinations { get; }
            internal string Detail { get; }
            internal TopologyRejections Rejections { get; }
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
