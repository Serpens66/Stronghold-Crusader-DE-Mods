using APIShared;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.AI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AIBuildDiagnoseTest
{
    internal sealed unsafe class AIBuildDiagnoseRuntime : IAivBuildStepObserver
    {
        private const string ProbeSaveName = "test_canari_nowoodcutters_probe.sav";
        private const int ProbePlayer = 6;
        private const int ProbeX = 345;
        private const int ProbeY = 485;
        private const int GridHistoryLimit = 64;
        private const int ExistingAppleFarmLimit = 16;
        private const int AppleFarmLimit = 32;
        private const int AppleFarmSnapshotLimit = 96;
        private readonly ManualLogSource log;
        private readonly bool fixesLoaded;
        private readonly IDisposable buildingSubscription;
        private readonly IDisposable buildStructureSubscription;
        private readonly IDisposable wallSubscription;
        private readonly IDisposable loadingSubscription;
        private readonly List<string> earlyGridHistory = new List<string>();
        private readonly List<string> earlyFarmHistory = new List<string>();
        private readonly List<AppleFarmWatch> appleFarms = new List<AppleFarmWatch>();
        private int appleFarmDropped;
        private int appleFarmSnapshots;
        private int appleFarmSnapshotDropped;
        private int earlyGridDropped;
        private int earlyFarmDropped;
        private int gridModeZeroCalls;
        private int gridModeOneCalls;
        private long gridSequence;
        private ulong gridState;
        private string lastGridSignature;
        private string loadingSaveName;
        private bool firstGridMismatchSeen;
        private string firstGridMismatchLine;
        private readonly Dictionary<long, Attempt> attempts = new Dictionary<long, Attempt>();
        private readonly Dictionary<string, int> outcomes = new Dictionary<string, int>();
        private readonly Dictionary<string, int> routeCauses = new Dictionary<string, int>();
        private readonly Dictionary<string, int> nearbyCauses = new Dictionary<string, int>();
        private readonly HashSet<string> detailedRoutes = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> detailedNearby = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> seen = new Dictionary<string, int>();
        private readonly int[] schedulerCalls = new int[9];
        private readonly int[] woodBuildCalls = new int[9];
        private readonly int[] woodSearchCalls = new int[9];
        private readonly int[] hutSpawns = new int[9];
        private readonly int[] initialHuts = new int[9];
        private readonly string[] lastObservedStage = new string[9];
        private readonly List<WallObservation> wallHistory = new List<WallObservation>();
        private readonly Dictionary<string, WallObservation> pendingWalls =
            new Dictionary<string, WallObservation>();
        private readonly HashSet<string> observedWallTargets = new HashSet<string>();
        private int wallHistoryDropped;
        private bool active;
        private bool firstTick;
        private long sessionId;
        private int lastTick;
        private long observedTickCount;
        private int nextSummaryTick;
        private bool probeSession;
        private bool probePending;
        private bool probeDone;
        private bool probeRunning;
        private long probeAttemptId;
        private long probeSpawnId;

        internal AIBuildDiagnoseRuntime(ManualLogSource logger, bool hasFixes)
        {
            log = logger ?? throw new ArgumentNullException(nameof(logger));
            fixesLoaded = hasFixes;
            buildingSubscription = BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(OnBuildingSpawn);
            buildStructureSubscription = BuildingR3EventHooks.OnBuildStructure.Observable.Subscribe(OnBuildStructure);
            wallSubscription = AIR3EventHooks.OnAIBuildWall.Observable.Subscribe(OnAIBuildWall);
            loadingSubscription = Shared.MissionEvents.Loading.Subscribe(OnMapLoading);
            if (ApiShared.Current.TryGetAivBuildStep(AIBuildDiagnosePlugin.Guid,
                out IAivBuildStepCapability steps, out NativeCapabilityDiagnostic diagnostic))
            {
                if (!steps.TryRegisterObserver("wood-build-context", this, out diagnostic))
                    Log("AI_BUILD_AIV_OBSERVER_UNAVAILABLE: " + diagnostic?.Reason);
            }
            else Log("AI_BUILD_AIV_OBSERVER_UNAVAILABLE: " + diagnostic?.Reason);
        }

        private void OnMapLoading(MissionLifecycleNotification notification)
        {
            if (notification.Phase != MissionInitializationPhase.BeforeLoad) return;
            active = false;
            gridState = 0;
            gridSequence = 0;
            gridModeZeroCalls = gridModeOneCalls = 0;
            lastGridSignature = null;
            firstGridMismatchSeen = false;
            firstGridMismatchLine = null;
            lastTick = -1;
            observedTickCount = 0;
            earlyGridHistory.Clear();
            earlyFarmHistory.Clear();
            appleFarms.Clear();
            appleFarmDropped = appleFarmSnapshots = appleFarmSnapshotDropped = 0;
            earlyGridDropped = earlyFarmDropped = 0;
            loadingSaveName = notification.Context.FilePath;
        }

        internal void OnSessionStarted(Shared.GameplaySessionStartedContext session)
        {
            sessionId = session.SessionId;
            active = !session.IsEditor && !session.IsReplay;
            probeSession = active && session.IsLoadedSave &&
                string.Equals(Path.GetFileName(session.SaveFileName ?? ""), ProbeSaveName,
                    StringComparison.OrdinalIgnoreCase);
            probePending = probeDone = probeRunning = false;
            probeAttemptId = probeSpawnId = 0;
            firstTick = false;
            lastTick = -1;
            observedTickCount = 0;
            nextSummaryTick = 0;
            seen.Clear();
            attempts.Clear();
            outcomes.Clear();
            routeCauses.Clear();
            nearbyCauses.Clear();
            detailedRoutes.Clear();
            detailedNearby.Clear();
            wallHistory.Clear();
            appleFarms.Clear();
            appleFarmDropped = appleFarmSnapshots = appleFarmSnapshotDropped = 0;
            pendingWalls.Clear();
            observedWallTargets.Clear();
            wallHistoryDropped = 0;
            Array.Clear(schedulerCalls, 0, schedulerCalls.Length);
            Array.Clear(woodBuildCalls, 0, woodBuildCalls.Length);
            Array.Clear(woodSearchCalls, 0, woodSearchCalls.Length);
            Array.Clear(hutSpawns, 0, hutSpawns.Length);
            Array.Clear(initialHuts, 0, initialHuts.Length);
            Array.Clear(lastObservedStage, 0, lastObservedStage.Length);
            Log($"AI_BUILD_SESSION: session={sessionId}, kind={session.Kind}, loadedSave={session.IsLoadedSave}, " +
                $"file={session.SaveFileName}, fixesLoaded={fixesLoaded}, active={active}, mode={session.Mode.ToDiagnosticString()}.");
            Log($"AI_BUILD_GRID_HISTORY: session={sessionId}, loadingFile={loadingSaveName ?? "unknown"}, " +
                $"sessionFile={session.SaveFileName}, retained={earlyGridHistory.Count}, " +
                $"dropped={earlyGridDropped}, mode0Calls={gridModeZeroCalls}, mode1Calls={gridModeOneCalls}.");
            foreach (string entry in earlyGridHistory) Log("AI_BUILD_GRID_EARLY: session=" + sessionId + ", " + entry);
            if (firstGridMismatchLine != null && earlyGridDropped != 0)
                Log("AI_BUILD_GRID_FIRST_MISMATCH_PRESERVED: session=" + sessionId + ", " + firstGridMismatchLine);
            foreach (string entry in earlyFarmHistory) Log("AI_BUILD_APPLEFARM_EARLY: session=" + sessionId + ", " + entry);
            if (earlyFarmDropped != 0)
                Log($"AI_BUILD_APPLEFARM_EARLY_OVERFLOW: session={sessionId}, dropped={earlyFarmDropped}.");
            earlyGridHistory.Clear();
            earlyFarmHistory.Clear();
            Log($"AI_BUILD_PROBE_ARMED: session={sessionId}, armed={probeSession}, " +
                $"requiredSave={ProbeSaveName}, player={ProbePlayer}, target=({ProbeX},{ProbeY}).");
            if (active)
            {
                CaptureInitialAppleFarms();
                CaptureInitialHuts();
                LogPlayers("start");
            }
        }

        internal void OnSessionEnded()
        {
            if (active) LogSummary("end");
            if (active)
                Log($"AI_BUILD_APPLEFARM_OBSERVER_SUMMARY: session={sessionId}, tracked={appleFarms.Count}, " +
                    $"dropped={appleFarmDropped}, snapshots={appleFarmSnapshots}, " +
                    $"snapshotDropped={appleFarmSnapshotDropped}.");
            active = false;
            probeSession = probePending = probeRunning = false;
            gridState = 0;
        }

        internal void OnTick(int tick)
        {
            if (!active) return;
            lastTick = tick;
            observedTickCount++;
            foreach (AppleFarmWatch farm in appleFarms)
            {
                if (!farm.IsNew || farm.FiveTickDone || observedTickCount < farm.DueTickCount)
                    continue;
                farm.FiveTickDone = true;
                try { CaptureAppleFarm(farm, "five-ticks-after-spawn"); }
                catch (Exception ex) { Log("AI_BUILD_APPLEFARM_TICK_CAPTURE_FAILED: " + ex); }
            }
            if (gridState != 0)
                ObserveGrid("tick", AiBuildDiagnostic.CaptureEconomyGridEvidence(gridState, -1));
            if (probePending)
            {
                try { RunPlacementProbe(); }
                catch (Exception ex)
                {
                    probePending = false;
                    probeDone = true;
                    probeRunning = false;
                    Log("AI_BUILD_PROBE_FAILED_CLOSED: " + ex);
                }
            }
            if (!firstTick)
            {
                firstTick = true;
                Log($"AI_BUILD_POST_STARTUP_TICK: session={sessionId}, tick={tick}.");
                LogPlayers("first-tick");
            }
            if (tick >= nextSummaryTick)
            {
                LogSummary("periodic");
                nextSummaryTick = tick + 500;
            }
        }

        internal void OnNativeRecord(AiBuildDiagnosticRecord record)
        {
            if (record.EconomyGridEvidence != null)
            {
                ObserveGrid(record.Stage, record.EconomyGridEvidence);
                if (active && record.Stage == "economy-grid-after")
                    ObserveAppleFarmsAfterGridUpdate(record.EconomyGridEvidence.Mode);
                return;
            }
            if (!active || record.PlayerId < 1 || record.PlayerId > 8) return;
            lastObservedStage[record.PlayerId] = record.Stage;
            switch (record.Stage)
            {
                case "scheduler-before": schedulerCalls[record.PlayerId]++; break;
                case "wood-build-before":
                    if (woodBuildCalls[record.PlayerId]++ == 0)
                        LogResources(record.PlayerId, "first-wood-build");
                    break;
                case "wood-search-before": woodSearchCalls[record.PlayerId]++; break;
            }
            Attempt attempt = null;
            if (record.AttemptId != 0)
            {
                if (record.Stage == "wood-build-before")
                    attempts[record.AttemptId] = new Attempt(record.PlayerId);
                attempts.TryGetValue(record.AttemptId, out attempt);
                if (attempt != null)
                {
                    if (record.Stage == "route-result") { attempt.RouteSeen = true; attempt.RouteResult = (int)record.A; }
                    if (record.Stage == "route-evidence") attempt.RouteEvidence = record.RouteEvidence;
                    if (record.Stage == "wood-nearby-path-before")
                        attempt.NearbyBefore = record.NearbyPathEvidence;
                    if (record.Stage == "wood-nearby-path-after")
                        attempt.NearbyAfter = record.NearbyPathEvidence;
                    if (record.Stage == "wood-search-after") attempt.SearchX = (int)record.B;
                    if (record.Stage == "wood-nearby-after") attempt.NearX = (int)record.A;
                    if (record.Stage != "wood-build-after") attempt.LastStep = record.Stage;
                }
            }
            string key = record.PlayerId + ":" + record.Stage + ":" + record.A + ":" + record.B + ":" + record.C + ":" + record.D;
            if (!seen.TryGetValue(key, out int count)) count = 0;
            seen[key] = count + 1;
            // First occurrence of each exact state is retained; periodic repeats show persistence.
            if (count < 2 || count == 9 || count == 99 || count % 500 == 499)
                Log($"AI_BUILD_TRACE: session={sessionId}, tick={lastTick}, player={record.PlayerId}, " +
                    $"attempt={record.AttemptId}, stage={record.Stage}, {Describe(record)}, repeat={count + 1}.");
            if (record.Stage == "wood-build-after" && attempt != null)
            {
                string outcome = Classify(attempt);
                string routeCause = attempt.RouteSeen && attempt.RouteResult == 0
                    ? AnalyzeRoute(attempt.RouteEvidence) :
                    (attempt.RouteSeen ? "not-rejected" : "unobserved");
                string nearbyCause = AnalyzeNearby(attempt);
                if (attempt.NearbyAfter != null)
                    LogNearbyEvidence(record.PlayerId, record.AttemptId, attempt, nearbyCause);
                if (probeSession && !probeDone && !probePending &&
                    record.PlayerId == ProbePlayer && attempt.RouteSeen &&
                    attempt.RouteResult == 0 && attempt.NearbyAfter != null &&
                    attempt.NearbyAfter.ResultX * 5 == ProbeX &&
                    attempt.NearbyAfter.ResultY * 5 == ProbeY)
                {
                    probeAttemptId = record.AttemptId;
                    probePending = true;
                    Log($"AI_BUILD_PROBE_QUEUED: session={sessionId}, tick={lastTick}, " +
                        $"attempt={probeAttemptId}, nextTick=true.");
                }
                if (attempt.RouteSeen && attempt.RouteResult == 0)
                {
                    string causeKey = record.PlayerId + ":" + routeCause;
                    routeCauses.TryGetValue(causeKey, out int causeCount);
                    routeCauses[causeKey] = causeCount + 1;
                    LogRouteEvidence(record.PlayerId, record.AttemptId, attempt.RouteEvidence, routeCause);
                }
                string outcomeKey = record.PlayerId + ":" + outcome;
                outcomes.TryGetValue(outcomeKey, out int outcomeCount);
                outcomes[outcomeKey] = ++outcomeCount;
                if (outcomeCount <= 2 || outcomeCount == 10 || outcomeCount % 100 == 0)
                    Log($"AI_BUILD_ATTEMPT: session={sessionId}, tick={lastTick}, player={record.PlayerId}, " +
                        $"attempt={record.AttemptId}, observedLast={attempt.LastStep}, inference={outcome}, " +
                        $"routeCause={routeCause}, " +
                        $"nearbyCause={nearbyCause}, " +
                        $"route={(attempt.RouteSeen ? attempt.RouteResult.ToString() : "unobserved")}, " +
                        $"buildPre={attempt.BuildPre}, buildPost={attempt.BuildPost}, " +
                        $"spawnPre={attempt.SpawnPre}, spawnPost={attempt.SpawnPost}, spawnId={attempt.SpawnId}.");
                attempts.Remove(record.AttemptId);
            }
        }

        private void ObserveGrid(string stage, AiEconomyGridEvidence evidence)
        {
            if (evidence == null) return;
            if (stage == "economy-grid-before")
            {
                if (evidence.Mode == 0) gridModeZeroCalls++;
                else if (evidence.Mode == 1) gridModeOneCalls++;
            }
            if (evidence.State != 0 && evidence.Status == "ok") gridState = evidence.State;
            string signature = GridSignature(evidence);
            bool changed = !string.Equals(signature, lastGridSignature, StringComparison.Ordinal);
            bool mismatch = evidence.Status == "ok" &&
                evidence.StoredForeignCount != evidence.CurrentDifferentCount;
            bool firstMismatch = mismatch && !firstGridMismatchSeen;
            if (firstMismatch) firstGridMismatchSeen = true;
            bool force = evidence.Mode == 1 ||
                (evidence.Mode == 0 && gridModeZeroCalls == 1) || firstMismatch;
            if (!changed && !force) return;
            lastGridSignature = signature;
            gridSequence++;
            string line = $"seq={gridSequence}, tick={lastTick}, stage={stage}, mode={evidence.Mode}, " +
                $"state=0x{evidence.State:X}, status={evidence.Status}, " +
                $"reference={evidence.ReferenceComponent}, storedForeign={evidence.StoredForeignCount}, " +
                $"liveDifferent={evidence.CurrentDifferentCount}, liveZero={evidence.CurrentZeroCount}, " +
                $"treeFlagTiles={evidence.TreeFlagCount}, appleFarmFlagTiles={evidence.AppleFarmFlagCount}, " +
                $"storedTreeWeight={evidence.TreeWeight}, mismatch={mismatch}, firstMismatch={firstMismatch}, " +
                $"changed={changed}, tileValues={GridTiles(evidence)}; " +
                "treeWeightIsRawVanillaValue-not-a-tree-flag-count.";
            if (firstMismatch) firstGridMismatchLine = line;
            if (active) Log("AI_BUILD_GRID_TRANSITION: session=" + sessionId + ", " + line);
            else
            {
                if (earlyGridHistory.Count == GridHistoryLimit)
                {
                    earlyGridHistory.RemoveAt(0);
                    earlyGridDropped++;
                }
                earlyGridHistory.Add(line);
            }
        }

        private static string GridSignature(AiEconomyGridEvidence evidence)
        {
            var value = new StringBuilder(330);
            value.Append(evidence.Status).Append(':').Append(evidence.ReferenceComponent)
                .Append(':').Append(evidence.StoredForeignCount).Append(':')
                .Append(evidence.TreeWeight);
            foreach (AiPathTileSample tile in evidence.Tiles)
                value.Append('|').Append(tile.X).Append(',').Append(tile.Y).Append(',')
                    .Append(tile.NativeComponent).Append(',').Append(tile.ApiComponent)
                    .Append(',').Append(tile.PropertyFlags).Append(',').Append(tile.Organism);
            return value.ToString();
        }

        private static string GridTiles(AiEconomyGridEvidence evidence)
        {
            var value = new StringBuilder(300);
            foreach (AiPathTileSample tile in evidence.Tiles)
            {
                if (value.Length != 0) value.Append('|');
                value.Append(tile.X).Append(',').Append(tile.Y).Append(':')
                    .Append(tile.NativeComponent).Append('/').Append(tile.ApiComponent)
                    .Append(':').Append(tile.PropertyFlags.ToString("X8"))
                    .Append(':').Append(tile.Organism)
                    .Append(':').Append(tile.BuildingId)
                    .Append(':').Append(tile.Status);
            }
            return value.ToString();
        }

        private void OnBuildStructure(BuildStructureEventArgs args)
        {
            if (probeRunning && args.PlayerId == ProbePlayer &&
                args.Mappers == eMappers.MAPPER_WOODSMAN)
            {
                Log($"AI_BUILD_PROBE_STRUCTURE: session={sessionId}, attempt={probeAttemptId}, " +
                    $"phase={args.Phase}, tile=({args.TileX},{args.TileY}), " +
                    $"scale={args.BuildingScaleUnknown}, free={args.IsFree}.");
                return;
            }
            if (!active || args.Mappers != eMappers.MAPPER_WOODSMAN ||
                !AiBuildDiagnostic.TryGetCurrentWoodAttempt(out long id, out int owner) ||
                owner != args.PlayerId || !attempts.TryGetValue(id, out Attempt attempt)) return;
            if (args.Phase == EventHookPhase.Pre) attempt.BuildPre = true;
            else if (args.Phase == EventHookPhase.Post) attempt.BuildPost = true;
            attempt.LastStep = "build-structure-" + args.Phase;
            string placement = AiBuildDiagnostic.TryReadPlacementStatus(out int preparation,
                out int rejected, out int mode)
                ? $"placementPreparation={preparation}, placementRejected={rejected}, placementMode={mode}"
                : "placementStatus=unavailable";
            try
            {
                int cost = GameBuildingManagerAPI.Instance.GetWoodCost(eStructs.STRUCT_WOODCUTTERS_HUT);
                Log($"AI_BUILD_STRUCTURE: session={sessionId}, tick={lastTick}, player={owner}, attempt={id}, " +
                    $"phase={args.Phase}, tile=({args.TileX},{args.TileY}), mapper={args.Mappers}, " +
                    $"scale={args.BuildingScaleUnknown}, free={args.IsFree}, woodCost={cost}, " +
                    $"{ReadResources(owner)}, {placement}.");
            }
            catch (Exception ex) { Log("AI_BUILD_STRUCTURE_OBSERVATION_FAILED: " + ex); }
        }

        private void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            if (args.Building == eStructs.STRUCT_APPLEFARM)
            {
                string entry = $"tick={lastTick}, phase={args.Phase}, player={args.PlayerId}, " +
                    $"tile=({args.TileX},{args.TileY}), " +
                    $"buildingId={(args.Phase == EventHookPhase.Post ? args.ReturnValue.ToString() : "pending")}; " +
                    "eventDoesNotProveTreeCreation.";
                if (active) Log("AI_BUILD_APPLEFARM: session=" + sessionId + ", " + entry);
                else
                {
                    if (earlyFarmHistory.Count == GridHistoryLimit)
                    {
                        earlyFarmHistory.RemoveAt(0);
                        earlyFarmDropped++;
                    }
                    earlyFarmHistory.Add(entry);
                }
                if (active && args.Phase == EventHookPhase.Post && args.ReturnValue > 0 &&
                    args.ReturnValue <= int.MaxValue)
                    TrackAppleFarm((int)args.ReturnValue, args.PlayerId, args.TileX, args.TileY,
                        true, "spawn-post");
            }
            if (!active ||
                args.Building != eStructs.STRUCT_WOODCUTTERS_HUT ||
                args.PlayerId < 1 || args.PlayerId > 8) return;
            if (probeRunning && args.PlayerId == ProbePlayer)
            {
                if (args.Phase == EventHookPhase.Post) probeSpawnId = args.ReturnValue;
                Log($"AI_BUILD_PROBE_SPAWN: session={sessionId}, attempt={probeAttemptId}, " +
                    $"phase={args.Phase}, tile=({args.TileX},{args.TileY}), " +
                    $"buildingId={(args.Phase == EventHookPhase.Post ? args.ReturnValue.ToString() : "pending")}.");
                return;
            }
            long id = 0;
            if (AiBuildDiagnostic.TryGetCurrentWoodAttempt(out long current, out int owner) &&
                owner == args.PlayerId && attempts.TryGetValue(current, out Attempt attempt))
            {
                id = current;
                if (args.Phase == EventHookPhase.Pre) attempt.SpawnPre = true;
                else if (args.Phase == EventHookPhase.Post)
                {
                    attempt.SpawnPost = true;
                    attempt.SpawnId = args.ReturnValue;
                }
                attempt.LastStep = "building-spawn-" + args.Phase;
            }
            if (args.Phase == EventHookPhase.Post && args.ReturnValue > 0) hutSpawns[args.PlayerId]++;
            Log($"AI_BUILD_WOODCUTTER_SPAWN: session={sessionId}, tick={lastTick}, player={args.PlayerId}, " +
                $"attempt={id}, phase={args.Phase}, buildingId={(args.Phase == EventHookPhase.Post ? args.ReturnValue.ToString() : "pending")}, " +
                $"tile=({args.TileX},{args.TileY}).");
        }

        private void OnAIBuildWall(AIBuildWallEventArgs args)
        {
            if (!active || args.PlayerId < 1 || args.PlayerId > 8) return;
            try
            {
                WallObservation wall = CaptureWall(args);
                wall.Tick = lastTick;
                string key = args.PlayerId + ":" + args.TileX + ":" + args.TileY;
                if (args.Phase == EventHookPhase.Pre) pendingWalls[key] = wall;
                else if (args.Phase == EventHookPhase.Post)
                {
                    if (pendingWalls.TryGetValue(key, out WallObservation before))
                    {
                        wall.Materialized = before.Status == "ok" && wall.Status == "ok" &&
                            (before.PropertyFlags & 0x100u) == 0 &&
                            (wall.PropertyFlags & 0x100u) != 0;
                        pendingWalls.Remove(key);
                    }
                }
                if (wallHistory.Count == 1024) { wallHistory.RemoveAt(0); wallHistoryDropped++; }
                wallHistory.Add(wall);
                foreach (string target in observedWallTargets)
                {
                    string[] parts = target.Split(':');
                    if (Math.Abs(wall.X - int.Parse(parts[0])) <= 12 &&
                        Math.Abs(wall.Y - int.Parse(parts[1])) <= 12)
                    {
                        LogWall(wall, "live-near-wood-target", 0);
                        break;
                    }
                }
            }
            catch (Exception ex) { Log("AI_BUILD_WALL_OBSERVATION_FAILED: " + ex); }
        }

        private static WallObservation CaptureWall(AIBuildWallEventArgs args)
        {
            var wall = new WallObservation
            {
                PlayerId = args.PlayerId, X = args.TileX, Y = args.TileY,
                Phase = args.Phase.ToString(), Mapper = args.Mappers.ToString(),
                Status = "outside-map"
            };
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            if (!tiles.IsTileInsideMapBounds(wall.X, wall.Y)) return wall;
            int tileId = tiles.GetTileId(wall.X, wall.Y);
            Span<byte> owners = tiles.GetWallOwnerLayer();
            if ((uint)tileId >= (uint)owners.Length)
            { wall.Status = "wall-layer-out-of-range"; return wall; }
            wall.TileId = tileId;
            wall.PropertyFlags = (uint)tiles.GetTilePropertyFlag(tileId);
            wall.BuildingId = tiles.GetTileBuildingId(tileId);
            wall.WallOwner = owners[tileId];
            wall.Status = "ok";
            return wall;
        }

        private void FlushWallHistory(int playerId, int x, int y, long attemptId)
        {
            if (!observedWallTargets.Add(x + ":" + y)) return;
            int related = 0;
            foreach (WallObservation wall in wallHistory)
                if (Math.Abs(wall.X - x) <= 12 && Math.Abs(wall.Y - y) <= 12)
                {
                    related++;
                    LogWall(wall, "history-near-wood-target", attemptId);
                }
            Log($"AI_BUILD_WALL_HISTORY: session={sessionId}, player={playerId}, attempt={attemptId}, " +
                $"target=({x},{y}), related={related}, retained={wallHistory.Count}, " +
                $"dropped={wallHistoryDropped}; dropped history cannot prove absence of earlier walls.");
        }

        private void LogWall(WallObservation wall, string relation, long attemptId) =>
            Log($"AI_BUILD_WALL_EVENT: session={sessionId}, tick={wall.Tick}, " +
                $"relation={relation}, attempt={attemptId}, player={wall.PlayerId}, " +
                $"phase={wall.Phase}, mapper={wall.Mapper}, tile=({wall.X},{wall.Y}), " +
                $"tileId={wall.TileId}, rawFlags=0x{wall.PropertyFlags:X8}, " +
                $"swamp={((wall.PropertyFlags & 0x20000000u) != 0)}, " +
                $"wallPresent={((wall.PropertyFlags & 0x100u) != 0)}, " +
                $"materializedFromPre={wall.Materialized}, buildingId={wall.BuildingId}, " +
                $"wallOwner={wall.WallOwner}, status={wall.Status}.");

        private sealed class WallObservation
        {
            internal int PlayerId, X, Y, TileId, BuildingId, WallOwner, Tick;
            internal uint PropertyFlags;
            internal string Phase, Mapper, Status;
            internal bool Materialized;
        }

        private static string Classify(Attempt attempt)
        {
            if (attempt.SpawnPost && attempt.SpawnId > 0) return "spawn-observed";
            if (attempt.SpawnPre) return "spawn-entered-without-successful-post";
            if (attempt.BuildPre) return "building-creation-entered-before-spawn-aborted";
            if (attempt.RouteSeen && attempt.RouteResult == 0) return "route-rejected";
            if (attempt.RouteSeen) return "route-accepted-building-event-unobserved";
            if (attempt.NearX >= 0) return "near-position-found-route-unobserved";
            if (attempt.SearchX >= 0) return "search-found-near-position-unobserved";
            return "search-or-earlier-exit";
        }

        private static string AnalyzeRoute(AiRouteEvidence evidence)
        {
            if (evidence == null) return "snapshot-unobserved";
            if (evidence.Status != "ok") return "snapshot-unavailable:" + evidence.Status;
            if (evidence.Bypass != 0) return "route-bypass-result-divergence";
            if (evidence.SourceComponent <= 0) return "source-component-result-divergence";
            if (evidence.TargetComponent == 0) return "target-component-zero";
            if (evidence.TargetComponent < 0) return "target-component-invalid";
            if (evidence.SourceComponent == evidence.TargetComponent)
                return "same-component-result-divergence";
            if (!CanReach(evidence, (connection, snapshot) => true)) return "no-macro-connection";
            if (!CanReach(evidence, (connection, snapshot) => connection.Active == 1))
                return "inactive-connection-filter";
            if (!CanReach(evidence, (connection, snapshot) =>
                connection.Active == 1 && connection.Open != 0))
                return "closed-connection-filter";
            if (!CanReach(evidence, (connection, snapshot) =>
                connection.Active == 1 && connection.Open != 0 && connection.ConnectionClass != 1))
                return "ladder-class-filter";
            if (CanReach(evidence, IsVanillaEligible)) return "eligible-graph-result-divergence";
            foreach (AiRouteConnection connection in evidence.Connections)
                if (connection.Active == 1 && connection.Open != 0 &&
                    connection.ConnectionClass != 1 &&
                    connection.OwnerToken == int.MinValue &&
                    (connection.GateFlag == 0 || connection.GateFlag == int.MinValue))
                    return "connection-filter-or-unavailable-access-data";
            return "player-access-filter";
        }

        private static bool IsVanillaEligible(AiRouteConnection connection,
            AiRouteEvidence evidence) => connection.Active == 1 && connection.Open != 0 &&
            connection.ConnectionClass != 1 &&
            (connection.OwnerToken == evidence.PlayerToken ||
             connection.GateFlag != 0 && connection.GateFlag != int.MinValue);

        private static AiPathTileSample FindAnchor(AiNearbyPathEvidence evidence, int x, int y)
        {
            if (evidence == null) return null;
            foreach (AiPathTileSample sample in evidence.Anchors)
                if (sample.X == x && sample.Y == y) return sample;
            foreach (AiPathTileSample sample in evidence.Footprint)
                if (sample.X == x && sample.Y == y) return sample;
            if (x < 0 || y < 0 || x % 5 != 0 || y % 5 != 0) return null;
            return evidence.GetCapturedAnchor(x / 5, y / 5);
        }

        private static bool IsComparableAnchor(AiPathTileSample sample) =>
            sample != null && (sample.Status == "ok" || sample.Status == "components-only");

        private static bool HasViewDifference(AiNearbyPathEvidence evidence)
        {
            if (evidence == null) return false;
            foreach (AiPathTileSample sample in evidence.Anchors)
                if (sample.Status == "ok" && sample.NativeComponent != sample.ApiComponent)
                    return true;
            foreach (AiPathTileSample sample in evidence.Footprint)
                if (sample.Status == "ok" && sample.NativeComponent != sample.ApiComponent)
                    return true;
            return false;
        }

        private static string AnalyzeNearby(Attempt attempt)
        {
            AiNearbyPathEvidence before = attempt.NearbyBefore;
            AiNearbyPathEvidence after = attempt.NearbyAfter;
            if (before == null || after == null) return "nearby-snapshot-unobserved";
            if (before.Status != "ok" || after.Status != "ok")
                return "nearby-snapshot-unavailable:" + before.Status + "/" + after.Status;
            if (HasViewDifference(before) || HasViewDifference(after) ||
                attempt.RouteEvidence != null && attempt.RouteEvidence.Status == "ok" &&
                (attempt.RouteEvidence.SourceComponent != attempt.RouteEvidence.SourceNativeComponent ||
                 attempt.RouteEvidence.TargetComponent != attempt.RouteEvidence.TargetNativeComponent))
                return "native-api-component-view-divergence";
            if (after.ResultX < 0 || after.ResultY < 0) return "no-nearby-result";
            int x = after.ResultX * 5;
            int y = after.ResultY * 5;
            AiPathTileSample preTarget = FindAnchor(before, x, y);
            AiPathTileSample postTarget = FindAnchor(after, x, y);
            if (!IsComparableAnchor(preTarget) || !IsComparableAnchor(postTarget))
                return "candidate-outside-measured-anchor-window";
            if (preTarget.NativeComponent != preTarget.ApiComponent ||
                postTarget.NativeComponent != postTarget.ApiComponent)
                return "native-api-component-view-divergence";
            if (preTarget.NativeComponent != postTarget.NativeComponent)
                return "component-changed-during-nearby-search";
            if (attempt.RouteEvidence != null && attempt.RouteEvidence.Status == "ok" &&
                postTarget.NativeComponent != attempt.RouteEvidence.TargetNativeComponent)
                return "component-changed-after-nearby-search";
            return postTarget.NativeComponent == 0
                ? "vanilla-coarse-search-selected-zero-component-anchor"
                : "vanilla-coarse-search-selected-positive-component-anchor";
        }

        private void LogNearbyEvidence(int playerId, long attemptId, Attempt attempt, string inference)
        {
            AiNearbyPathEvidence before = attempt.NearbyBefore;
            AiNearbyPathEvidence after = attempt.NearbyAfter;
            int x = after.ResultX * 5;
            int y = after.ResultY * 5;
            AiPathTileSample preOrigin = FindAnchor(before, after.InputX * 5, after.InputY * 5);
            AiPathTileSample postOrigin = FindAnchor(after, after.InputX * 5, after.InputY * 5);
            AiPathTileSample preTarget = FindAnchor(before, x, y);
            AiPathTileSample postTarget = FindAnchor(after, x, y);
            AiCoarseCellSample preCell = FindCoarseCell(before, after.ResultX, after.ResultY);
            AiCoarseCellSample postCell = after.ResultCell;
            string signature = playerId + ":" + x + ":" + y + ":" + inference;
            bool full = detailedNearby.Add(signature);
            nearbyCauses.TryGetValue(signature, out int count);
            nearbyCauses[signature] = ++count;
            if (full || count == 2 || count == 10 || count % 100 == 0)
                Log($"AI_BUILD_NEARBY_EVIDENCE: session={sessionId}, tick={lastTick}, " +
                    $"player={playerId}, attempt={attemptId}, input=({after.InputX},{after.InputY}), " +
                    $"result=({after.ResultX},{after.ResultY}), " +
                    $"originBefore={FormatSample(preOrigin)}, originAfter={FormatSample(postOrigin)}, " +
                    $"targetBefore={FormatSample(preTarget)}, targetAfter={FormatSample(postTarget)}, " +
                    $"coarseOriginBefore={FormatCoarse(before.InputCell)}, " +
                    $"coarseOriginAfter={FormatCoarse(after.InputCell)}, " +
                    $"coarseTargetBefore={FormatCoarse(preCell)}, " +
                    $"coarseTargetAfter={FormatCoarse(postCell)}, " +
                    $"routeTargetNative={attempt.RouteEvidence?.TargetNativeComponent.ToString() ?? "unobserved"}, " +
                    $"observedLast={attempt.LastStep}, " +
                    $"inference={inference}, repeat={count}, fullSamples={full}.");
            if (!full) return;
            if (after.ResultX >= 0 && after.ResultY >= 0)
                FlushWallHistory(playerId, x, y, attemptId);
            LogCoarseCells("before", playerId, attemptId, before?.NearbyCells);
            LogCoarseCells("after", playerId, attemptId, after.NearbyCells);
            LogSamples("before-anchor", playerId, attemptId, before?.Anchors);
            LogSamples("after-anchor", playerId, attemptId, after.Anchors);
            LogSamples("after-footprint", playerId, attemptId, after.Footprint);
            LogCoarseExplanation(playerId, attemptId, before, after);
            LogSamples("selected-coarse-5x5", playerId, attemptId, after.CoarseTiles);
            LogFootprintArea("footprint-and-ring", playerId, attemptId,
                x, y, after.FootprintRing);
            LogPlacementIndicators(playerId, attemptId, x, y,
                after.Footprint, after.FootprintRing);
        }

        private void LogCoarseExplanation(int playerId, long attemptId,
            AiNearbyPathEvidence before, AiNearbyPathEvidence after)
        {
            AiCoarseCellSample selected = FindCoarseCell(before, after.ResultX, after.ResultY);
            int[] raw = ParseCoarseBytes(selected);
            if (raw == null)
            {
                Log($"AI_BUILD_COARSE_EXPLAIN: session={sessionId}, attempt={attemptId}, status=unavailable.");
                return;
            }
            int different = 0, zero = 0, trees = 0, swamps = 0, buildings = 0;
            foreach (AiPathTileSample tile in after.CoarseTiles)
            {
                if (tile.Status != "ok") continue;
                if (tile.NativeComponent != after.ReferenceComponent) different++;
                if (tile.NativeComponent == 0) zero++;
                if ((tile.PropertyFlags & (uint)TilePropertyFlag.IsTree) != 0) trees++;
                if ((tile.PropertyFlags & (uint)TilePropertyFlag.IsSwamp) != 0) swamps++;
                if (tile.BuildingId != 0) buildings++;
            }
            Log($"AI_BUILD_COARSE_EXPLAIN: session={sessionId}, tick={lastTick}, " +
                $"player={playerId}, attempt={attemptId}, cell=({after.ResultX},{after.ResultY}), " +
                $"referenceComponent={after.ReferenceComponent}, storedForeignCount={raw[0]}, " +
                $"currentDifferentComponentTiles={different}, currentZeroComponentTiles={zero}, " +
                $"treeTiles={trees}, swampTiles={swamps}, buildingTiles={buildings}, " +
                $"treeWeight={raw[3]}, stone={raw[4]}, iron={raw[5]}, pitch={raw[6]}, " +
                $"swamp={raw[7]}, minHeight={raw[8]}, maxHeight={raw[9]}, " +
                $"heightRangeRejected={raw[10]}, structureOrReservation={raw[11]}, " +
                $"outsideUsableMap={raw[12]}, impassableEdge={raw[15]}, " +
                $"candidateFirstFailure={CoarseFailure(raw)}, " +
                "componentDifferenceMeaning=inference-current-grid-versus-stored-counter, " +
                "treeWeightMeaning=raw-Vanilla-value-not-tree-flag-count.");
            foreach (AiCoarseCellSample cell in before.NearbyCells)
            {
                int[] values = ParseCoarseBytes(cell);
                if (values == null) continue;
                Log($"AI_BUILD_LOCAL_CANDIDATE: session={sessionId}, attempt={attemptId}, " +
                    $"cell=({cell.X},{cell.Y}), firstFailedPredicate={CoarseFailure(values)}, " +
                    "actualBfsVisit=unobserved.");
            }
        }

        private static int[] ParseCoarseBytes(AiCoarseCellSample cell)
        {
            if (cell == null || cell.Bytes == null) return null;
            string[] parts = cell.Bytes.Split('-');
            if (parts.Length < 16) return null;
            var result = new int[16];
            for (int i = 0; i < 16; i++)
                if (!int.TryParse(parts[i], System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out result[i])) return null;
            return result;
        }

        // Order follows the audited 0x58950 acceptance branch, not a generic buildability test.
        private static string CoarseFailure(int[] cell)
        {
            if ((sbyte)cell[0] >= 15) return "traversal-foreign-component-limit";
            if (cell[10] != 0) return "height-range";
            if (cell[11] != 0) return "structure-or-reservation";
            if (cell[12] != 0) return "outside-usable-map";
            if (cell[15] != 0) return "impassable-edge";
            if (cell[3] != 0) return "tree-weight";
            if ((sbyte)cell[4] >= 1) return "stone-count";
            if (cell[0] != 0) return "foreign-component-count";
            return "none-coarse-eligible";
        }

        private void LogSamples(string phase, int playerId, long attemptId,
            IReadOnlyList<AiPathTileSample> samples)
        {
            if (samples == null) return;
            foreach (AiPathTileSample sample in samples)
                Log($"AI_BUILD_PATH_TILE: session={sessionId}, player={playerId}, " +
                    $"attempt={attemptId}, phase={phase}, {FormatSample(sample)}.");
        }

        private void LogCoarseCells(string phase, int playerId, long attemptId,
            IReadOnlyList<AiCoarseCellSample> cells)
        {
            if (cells == null) return;
            foreach (AiCoarseCellSample cell in cells)
                Log($"AI_BUILD_COARSE_CELL: session={sessionId}, player={playerId}, " +
                    $"attempt={attemptId}, phase={phase}, {FormatCoarse(cell)}.");
        }

        private static string FormatSample(AiPathTileSample sample) => sample == null
            ? "unobserved" : sample.Status == "components-only"
            ? $"tile=({sample.X},{sample.Y}) native={sample.NativeComponent} api={sample.ApiComponent} " +
              "tileLayers=unobserved status=components-only"
            : $"tile=({sample.X},{sample.Y}) id={sample.TileId} " +
              $"native={sample.NativeComponent} api={sample.ApiComponent} " +
              $"rawFlags=0x{sample.PropertyFlags:X8} swamp={((sample.PropertyFlags & 0x20000000u) != 0)} " +
              $"wall={((sample.PropertyFlags & 0x100u) != 0)} " +
              $"tree={((sample.PropertyFlags & (uint)TilePropertyFlag.IsTree) != 0)} " +
              $"appleFarm={((sample.PropertyFlags & (uint)TilePropertyFlag.IsAppleFarm) != 0)} " +
              $"type={sample.TileType} organism={sample.Organism} occupancy={sample.Occupancy} " +
              $"height={sample.Height} buildingId={sample.BuildingId} " +
              $"wallOwner={sample.WallOwner} status={sample.Status}";

        private static AiCoarseCellSample FindCoarseCell(AiNearbyPathEvidence evidence, int x, int y)
        {
            if (evidence == null) return null;
            foreach (AiCoarseCellSample cell in evidence.NearbyCells)
                if (cell.X == x && cell.Y == y) return cell;
            return evidence.GetCapturedCoarseCell(x, y);
        }

        private static string FormatCoarse(AiCoarseCellSample cell) => cell == null
            ? "unobserved" : $"({cell.X},{cell.Y}) bytes={cell.Bytes} status={cell.Status}";

        private static bool CanReach(AiRouteEvidence evidence,
            Func<AiRouteConnection, AiRouteEvidence, bool> eligible)
        {
            var reached = new HashSet<int> { evidence.SourceComponent };
            bool changed;
            do
            {
                changed = false;
                foreach (AiRouteConnection connection in evidence.Connections)
                {
                    if (!eligible(connection, evidence)) continue;
                    if (!reached.Contains(connection.A) && !reached.Contains(connection.B) &&
                        !reached.Contains(connection.C)) continue;
                    if (connection.A > 0) changed |= reached.Add(connection.A);
                    if (connection.B > 0) changed |= reached.Add(connection.B);
                    if (connection.C > 0) changed |= reached.Add(connection.C);
                    if (reached.Contains(evidence.TargetComponent)) return true;
                }
            } while (changed);
            return reached.Contains(evidence.TargetComponent);
        }

        private void LogRouteEvidence(int playerId, long attemptId, AiRouteEvidence evidence, string cause)
        {
            if (evidence == null)
            {
                Log($"AI_BUILD_ROUTE_EVIDENCE: session={sessionId}, player={playerId}, attempt={attemptId}, status=missing.");
                return;
            }
            string signature = playerId + ":" + evidence.SourceComponent + ":" +
                evidence.TargetComponent + ":" + cause;
            bool first = detailedRoutes.Add(signature);
            Log($"AI_BUILD_ROUTE_EVIDENCE: session={sessionId}, tick={lastTick}, player={playerId}, " +
                $"attempt={attemptId}, sourceTile={evidence.SourceTile}, targetTile={evidence.TargetTile}, " +
                $"target=({evidence.TileX},{evidence.TileY}), sourceComponent={evidence.SourceComponent}, " +
                $"targetComponent={evidence.TargetComponent}, " +
                $"sourceNative={evidence.SourceNativeComponent}, targetNative={evidence.TargetNativeComponent}, " +
                $"mode={evidence.QueryMode}, " +
                $"bypass={evidence.Bypass}, playerAccessToken={evidence.PlayerToken}, " +
                $"records={evidence.Connections.Count}, status={evidence.Status}, inference={cause}, " +
                $"fullRecords={first}.");
            if (!first) return;
            foreach (AiRouteConnection connection in evidence.Connections)
            {
                if (connection.A <= 0 && connection.B <= 0 && connection.C <= 0 &&
                    connection.Active == 0) continue;
                Log($"AI_BUILD_ROUTE_RECORD: session={sessionId}, player={playerId}, attempt={attemptId}, " +
                    $"id={connection.Id}, active={connection.Active}, open={connection.Open}, " +
                    $"class={connection.ConnectionClass}, owner={connection.Owner}, building={connection.BuildingId}, " +
                    $"components=({connection.A},{connection.B},{connection.C}), " +
                    $"ownerAccessToken={connection.OwnerToken}, gateFlag={connection.GateFlag}, " +
                    $"eligibleInference={IsVanillaEligible(connection, evidence)}.");
            }
        }

        private sealed class Attempt
        {
            internal Attempt(int playerId) { PlayerId = playerId; }
            internal int PlayerId;
            internal int SearchX = -1;
            internal int NearX = -1;
            internal bool RouteSeen;
            internal int RouteResult;
            internal AiRouteEvidence RouteEvidence;
            internal AiNearbyPathEvidence NearbyBefore;
            internal AiNearbyPathEvidence NearbyAfter;
            internal bool BuildPre;
            internal bool BuildPost;
            internal bool SpawnPre;
            internal bool SpawnPost;
            internal long SpawnId;
            internal string LastStep = "wood-build-before";
        }

        private void LogPlayers(string phase)
        {
            try
            {
                var players = GamePlayerManagerAPI.Instance;
                for (int playerId = 1; playerId <= 8; playerId++)
                {
                    if (!players.IsAIPlayer(playerId)) continue;
                    Log($"AI_BUILD_PLAYER: session={sessionId}, phase={phase}, player={playerId}, " +
                        $"lord={players.GetAILord(playerId)}, {ReadResources(playerId)}.");
                }
            }
            catch (Exception ex) { Log("AI_BUILD_PLAYER_READ_FAILED: " + ex); }
        }

        private void LogSummary(string phase)
        {
            try
            {
                Log($"AI_BUILD_GRID_SUMMARY: session={sessionId}, phase={phase}, tick={lastTick}, " +
                    $"mode0Calls={gridModeZeroCalls}, mode1Calls={gridModeOneCalls}, " +
                    $"firstMismatchSeen={firstGridMismatchSeen}, earlyDropped={earlyGridDropped}.");
                var players = GamePlayerManagerAPI.Instance;
                for (int playerId = 1; playerId <= 8; playerId++)
                {
                    if (!players.IsAIPlayer(playerId)) continue;
                    string outcomeText = "";
                    foreach (KeyValuePair<string, int> outcome in outcomes)
                        if (outcome.Key.StartsWith(playerId + ":", StringComparison.Ordinal))
                            outcomeText += outcome.Key.Substring(2) + "=" + outcome.Value + ",";
                    string routeCauseText = "";
                    foreach (KeyValuePair<string, int> cause in routeCauses)
                        if (cause.Key.StartsWith(playerId + ":", StringComparison.Ordinal))
                            routeCauseText += cause.Key.Substring(2) + "=" + cause.Value + ",";
                    Log($"AI_BUILD_SUMMARY: session={sessionId}, phase={phase}, tick={lastTick}, " +
                        $"player={playerId}, lord={players.GetAILord(playerId)}, scheduler={schedulerCalls[playerId]}, " +
                        $"woodBuild={woodBuildCalls[playerId]}, woodSearch={woodSearchCalls[playerId]}, " +
                        $"initialHuts={initialHuts[playerId]}, newHuts={hutSpawns[playerId]}, " +
                        $"wallEventsRetained={wallHistory.Count}, wallEventsDropped={wallHistoryDropped}, " +
                        $"routeHookReady={AiBuildDiagnostic.RouteReady}, " +
                        $"lastObserved={lastObservedStage[playerId] ?? "none"}, " +
                        $"inference={InferStage(playerId)}, attemptOutcomes={outcomeText}, " +
                        $"routeCauses={routeCauseText}.");
                }
                LogPlayers(phase);
            }
            catch (Exception ex) { Log("AI_BUILD_SUMMARY_FAILED: " + ex); }
        }

        private void LogResources(int playerId, string phase)
        {
            try
            {
                Log($"AI_BUILD_RESOURCES: session={sessionId}, phase={phase}, tick={lastTick}, " +
                    $"player={playerId}, {ReadResources(playerId)}.");
            }
            catch (Exception ex) { Log("AI_BUILD_RESOURCE_READ_FAILED: " + ex); }
        }

        private static string ReadResources(int playerId)
        {
            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(playerId,
                out GamePlayerResources* resources) || resources == null)
                return "resources=unavailable";
            return $"gold={resources->r_TotalGoodsGold}, woodLogs={resources->r_TotalGoodsWoodLogs}, " +
                $"woodPlanks={resources->r_TotalGoodsWoodPlanks}";
        }

        private void LogFootprintArea(string phase, int playerId, long attemptId,
            int anchorX, int anchorY, IReadOnlyList<AiPathTileSample> samples)
        {
            if (samples == null) return;
            foreach (AiPathTileSample sample in samples)
            {
                bool footprint = sample.X >= anchorX && sample.X < anchorX + 3 &&
                    sample.Y >= anchorY && sample.Y < anchorY + 3;
                string role = sample.X == anchorX && sample.Y == anchorY ? "anchor-footprint" :
                    footprint ? "footprint" : "perimeter-possible-access";
                Log($"AI_BUILD_PLACE_TILE: session={sessionId}, player={playerId}, " +
                    $"attempt={attemptId}, phase={phase}, role={role}, " +
                    $"apparentBuildingFree={(sample.Status == "ok" && sample.BuildingId == 0)}, " +
                    $"{FormatSample(sample)}.");
            }
        }

        private void LogPlacementIndicators(int playerId, long attemptId,
            int x, int y, IReadOnlyList<AiPathTileSample> footprint,
            IReadOnlyList<AiPathTileSample> area)
        {
            int footprintTrees = 0, footprintSwamps = 0, footprintBuildings = 0;
            int accessiblePerimeter = 0, blockedPerimeter = 0;
            foreach (AiPathTileSample tile in footprint)
            {
                if ((tile.PropertyFlags & (uint)TilePropertyFlag.IsTree) != 0) footprintTrees++;
                if ((tile.PropertyFlags & (uint)TilePropertyFlag.IsSwamp) != 0) footprintSwamps++;
                if (tile.BuildingId != 0 ||
                    (tile.PropertyFlags & (uint)TilePropertyFlag.IsBuilding) != 0)
                    footprintBuildings++;
            }
            foreach (AiPathTileSample tile in area)
            {
                if (tile.X >= x && tile.X < x + 3 && tile.Y >= y && tile.Y < y + 3)
                    continue;
                if (tile.Status == "ok" && tile.NativeComponent > 0) accessiblePerimeter++;
                else blockedPerimeter++;
            }
            Log($"AI_BUILD_PLACEMENT_INDICATORS: session={sessionId}, attempt={attemptId}, " +
                $"player={playerId}, anchor=({x},{y}), footprintTiles={footprint.Count}, " +
                $"footprintTrees={footprintTrees}, footprintSwamps={footprintSwamps}, " +
                $"footprintBuildings={footprintBuildings}, " +
                $"perimeterPositiveComponents={accessiblePerimeter}, " +
                $"perimeterOther={blockedPerimeter}, actualAccessTile=unobserved-before-spawn, " +
                "vanillaPlacement=unobserved-before-probe; countsAreObservations.");
        }

        private void RunPlacementProbe()
        {
            probePending = false;
            probeDone = true; // Never retry after a native call, including an exception.
            if (!active || !probeSession ||
                !GamePlayerManagerAPI.Instance.IsAIPlayer(ProbePlayer) ||
                !GameTileManagerAPI.Instance.IsTileInsideMapBounds(ProbeX + 2, ProbeY + 2))
            {
                Log($"AI_BUILD_PROBE_SKIPPED: session={sessionId}, attempt={probeAttemptId}, " +
                    "reason=session-player-or-footprint-changed.");
                return;
            }
            if (GameTileManagerAPI.Instance.TileManager.UsePlacementBlockedOverride ||
                !AiBuildDiagnostic.TryReadPlacementStatus(out int prePreparation,
                    out int preRejected, out int preMode) || prePreparation != 0)
            {
                Log($"AI_BUILD_PROBE_SKIPPED: session={sessionId}, attempt={probeAttemptId}, " +
                    "reason=placement-override-or-preparation-state.");
                return;
            }
            IReadOnlyList<AiPathTileSample> before =
                AiBuildDiagnostic.CaptureTiles(ProbeX - 1, ProbeY - 1, 5, 5);
            string resourcesBefore = ReadResources(ProbePlayer);
            if (before.Count != 25)
            {
                Log($"AI_BUILD_PROBE_SKIPPED: session={sessionId}, attempt={probeAttemptId}, " +
                    "reason=tile-snapshot-unavailable.");
                return;
            }
            Log($"AI_BUILD_PROBE_BEGIN: session={sessionId}, tick={lastTick}, attempt={probeAttemptId}, " +
                $"player={ProbePlayer}, anchor=({ProbeX},{ProbeY}), mapper=0x33, scale=3, " +
                $"variant=15, free=false, bypassPlacementRules=false, " +
                $"preparation={prePreparation}, rejected={preRejected}, mode={preMode}, {resourcesBefore}.");
            LogFootprintArea("probe-before-footprint-and-ring", ProbePlayer,
                probeAttemptId, ProbeX, ProbeY, before);
            long callResult = 0;
            string failure = "none";
            try
            {
                probeRunning = true;
                // Exact 0x51540 -> 0x6D580 construction parameters, in the disposable save only.
                callResult = GameBuildingManagerAPI.Instance.CreatePrefab(ProbePlayer,
                    ProbeX, ProbeY, eMappers.MAPPER_WOODSMAN, 3, 15, false, false);
            }
            catch (Exception ex) { failure = ex.ToString(); }
            finally { probeRunning = false; }
            IReadOnlyList<AiPathTileSample> after =
                AiBuildDiagnostic.CaptureTiles(ProbeX - 1, ProbeY - 1, 5, 5);
            LogFootprintArea("probe-after-footprint-and-ring", ProbePlayer,
                probeAttemptId, ProbeX, ProbeY, after);
            string access = "unobserved-no-spawn";
            if (probeSpawnId > 0 && probeSpawnId <= int.MaxValue)
            {
                var point = GameBuildingManagerAPI.Instance.GetAccessPosition((int)probeSpawnId);
                access = $"({point.X},{point.Y})";
                if (GameTileManagerAPI.Instance.IsTileInsideMapBounds(point.X, point.Y))
                {
                    IReadOnlyList<AiPathTileSample> accessTile =
                        AiBuildDiagnostic.CaptureTiles(point.X, point.Y, 1, 1);
                    LogSamples("probe-actual-access", ProbePlayer, probeAttemptId, accessTile);
                }
            }
            bool changed = false;
            if (after.Count == before.Count)
                for (int i = 0; i < before.Count; i++)
                    if (before[i].PropertyFlags != after[i].PropertyFlags ||
                        before[i].BuildingId != after[i].BuildingId ||
                        before[i].Organism != after[i].Organism ||
                        before[i].NativeComponent != after[i].NativeComponent)
                    { changed = true; break; }
            string placement = AiBuildDiagnostic.TryReadPlacementStatus(out int preparation,
                out int rejected, out int mode)
                ? $"preparation={preparation}, rejected={rejected}, mode={mode}"
                : "placementStatus=unavailable";
            Log($"AI_BUILD_PROBE_RESULT: session={sessionId}, attempt={probeAttemptId}, " +
                $"callResult={callResult}, spawnId={probeSpawnId}, access={access}, " +
                $"tileChanged={changed}, {placement}, resourcesBefore=[{resourcesBefore}], " +
                $"resourcesAfter=[{ReadResources(ProbePlayer)}], exception={failure}, " +
                $"observedPlacement={(probeSpawnId > 0 ? "spawned" : "no-spawn")}.");
        }

        private void CaptureInitialAppleFarms()
        {
            int found = 0;
            try
            {
                Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
                for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
                {
                    ref GameBuilding building = ref buildings[spanIndex];
                    if (building.r_BuildingType != eStructs.STRUCT_APPLEFARM ||
                        (building.r_AliveState != AliveState.IsAlive &&
                         building.r_AliveState != AliveState.NeedsInit)) continue;
                    found++;
                    if (appleFarms.Count >= ExistingAppleFarmLimit)
                    {
                        appleFarmDropped++;
                        continue;
                    }
                    TrackAppleFarm(spanIndex + 1, building.r_PlayerIdOwner,
                        building.r_TilePositionXBegin, building.r_TilePositionYBegin,
                        false, "session-start");
                }
                Log($"AI_BUILD_APPLEFARM_INITIAL_SCAN: session={sessionId}, found={found}, " +
                    $"tracked={appleFarms.Count}, dropped={appleFarmDropped}.");
            }
            catch (Exception ex) { Log("AI_BUILD_APPLEFARM_INITIAL_SCAN_FAILED: " + ex); }
        }

        private void TrackAppleFarm(int buildingId, int playerId, int eventX, int eventY,
            bool isNew, string stage)
        {
            try
            {
                if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId,
                    out GameBuilding* building) || building == null ||
                    building->r_BuildingType != eStructs.STRUCT_APPLEFARM)
                {
                    Log($"AI_BUILD_APPLEFARM_UNRESOLVED: session={sessionId}, stage={stage}, " +
                        $"buildingId={buildingId}, eventTile=({eventX},{eventY}).");
                    return;
                }
                foreach (AppleFarmWatch existing in appleFarms)
                    if (existing.BuildingId == buildingId && existing.GlobalId == building->r_GlobalId)
                        return;
                if (appleFarms.Count >= AppleFarmLimit)
                {
                    appleFarmDropped++;
                    Log($"AI_BUILD_APPLEFARM_OVERFLOW: session={sessionId}, dropped={appleFarmDropped}, " +
                        $"buildingId={buildingId}.");
                    return;
                }
                var farm = new AppleFarmWatch
                {
                    BuildingId = buildingId,
                    GlobalId = building->r_GlobalId,
                    PlayerId = building->r_PlayerIdOwner,
                    X = building->r_TilePositionXBegin,
                    Y = building->r_TilePositionYBegin,
                    Size = checked((int)building->r_OccupyTileGridSize),
                    IsNew = isNew,
                    DueTickCount = observedTickCount + 5
                };
                appleFarms.Add(farm);
                Log($"AI_BUILD_APPLEFARM_TRACKED: session={sessionId}, tick={lastTick}, " +
                    $"stage={stage}, buildingId={buildingId}, globalId={farm.GlobalId}, " +
                    $"player={farm.PlayerId}, eventPlayer={playerId}, eventTile=({eventX},{eventY}), " +
                    $"buildingTile=({farm.X},{farm.Y}), occupiedGridSize={farm.Size}, " +
                    $"alive={building->r_AliveState}, dueObservedTick={(isNew ? farm.DueTickCount.ToString() : "none")}.");
                CaptureAppleFarm(farm, stage);
            }
            catch (Exception ex)
            {
                Log($"AI_BUILD_APPLEFARM_TRACK_FAILED: session={sessionId}, stage={stage}, " +
                    $"buildingId={buildingId}, error={ex}.");
            }
        }

        private void ObserveAppleFarmsAfterGridUpdate(int mode)
        {
            foreach (AppleFarmWatch farm in appleFarms)
            {
                if (farm.GridUpdateDone) continue;
                farm.GridUpdateDone = true;
                try { CaptureAppleFarm(farm, "after-grid-update-mode-" + mode); }
                catch (Exception ex) { Log("AI_BUILD_APPLEFARM_GRID_CAPTURE_FAILED: " + ex); }
            }
        }

        private void CaptureAppleFarm(AppleFarmWatch farm, string stage)
        {
            if (appleFarmSnapshots >= AppleFarmSnapshotLimit)
            {
                appleFarmSnapshotDropped++;
                if (appleFarmSnapshotDropped == 1)
                    Log($"AI_BUILD_APPLEFARM_SNAPSHOT_OVERFLOW: session={sessionId}, " +
                        $"limit={AppleFarmSnapshotLimit}.");
                return;
            }
            appleFarmSnapshots++;
            if (!AiBuildDiagnostic.HasObserver)
            {
                Log($"AI_BUILD_APPLEFARM_SNAPSHOT: session={sessionId}, stage={stage}, " +
                    $"buildingId={farm.BuildingId}, status=native-observer-unavailable.");
                return;
            }
            string buildingStatus = "unavailable";
            try
            {
                if (GameBuildingManagerAPI.Instance.TryGetBuildingById(farm.BuildingId,
                    out GameBuilding* current) && current != null)
                {
                    if (current->r_BuildingType == eStructs.STRUCT_APPLEFARM &&
                        (current->r_GlobalId == farm.GlobalId || farm.GlobalId == 0))
                    {
                        buildingStatus = current->r_AliveState.ToString();
                        if (farm.GlobalId == 0) farm.GlobalId = current->r_GlobalId;
                        farm.PlayerId = current->r_PlayerIdOwner;
                        int currentSize = checked((int)current->r_OccupyTileGridSize);
                        if (currentSize >= 1 && currentSize <= 15)
                        {
                            farm.X = current->r_TilePositionXBegin;
                            farm.Y = current->r_TilePositionYBegin;
                            farm.Size = currentSize;
                        }
                    }
                    else buildingStatus = "identity-changed";
                }
            }
            catch (Exception ex) { buildingStatus = "read-failed:" + ex.GetType().Name; }
            if (farm.Size < 1 || farm.Size > 15 || farm.X < 0 || farm.Y < 0 ||
                farm.X + farm.Size > 800 || farm.Y + farm.Size > 800)
            {
                Log($"AI_BUILD_APPLEFARM_SNAPSHOT: session={sessionId}, stage={stage}, " +
                    $"buildingId={farm.BuildingId}, buildingStatus={buildingStatus}, status=invalid-footprint, " +
                    $"origin=({farm.X},{farm.Y}), size={farm.Size}.");
                return;
            }
            int startX = Math.Max(0, farm.X / 5 - 1);
            int endX = Math.Min(159, (farm.X + farm.Size - 1) / 5 + 1);
            int startY = Math.Max(0, farm.Y / 5 - 1);
            int endY = Math.Min(159, (farm.Y + farm.Size - 1) / 5 + 1);
            int valid = 0, mismatched = 0, unavailable = 0;
            int footprintTiles = 0, footprintBuildingTiles = 0;
            int footprintAppleFlags = 0, footprintTreeFlags = 0, footprintZero = 0;
            var cellLines = new List<string>();
            for (int coarseX = startX; coarseX <= endX; coarseX++)
                for (int coarseY = startY; coarseY <= endY; coarseY++)
                {
                    AiEconomyGridEvidence evidence = AiBuildDiagnostic.CaptureEconomyGridEvidence(
                        coarseX, coarseY);
                    bool ready = evidence.Status == "ok";
                    bool mismatch = ready && evidence.StoredForeignCount != evidence.CurrentDifferentCount;
                    if (ready) valid++; else unavailable++;
                    if (mismatch) mismatched++;
                    foreach (AiPathTileSample tile in evidence.Tiles)
                    {
                        if (tile.X < farm.X || tile.X >= farm.X + farm.Size ||
                            tile.Y < farm.Y || tile.Y >= farm.Y + farm.Size || tile.Status != "ok")
                            continue;
                        footprintTiles++;
                        if (tile.BuildingId == farm.BuildingId) footprintBuildingTiles++;
                        if ((tile.PropertyFlags & (uint)TilePropertyFlag.IsAppleFarm) != 0)
                            footprintAppleFlags++;
                        if ((tile.PropertyFlags & (uint)TilePropertyFlag.IsTree) != 0)
                            footprintTreeFlags++;
                        if (tile.NativeComponent == 0) footprintZero++;
                    }
                    cellLines.Add($"AI_BUILD_APPLEFARM_CELL: session={sessionId}, tick={lastTick}, " +
                        $"stage={stage}, buildingId={farm.BuildingId}, cell=({coarseX},{coarseY}), " +
                        $"status={evidence.Status}, reference={evidence.ReferenceComponent}, " +
                        $"storedForeign={evidence.StoredForeignCount}, liveDifferent={evidence.CurrentDifferentCount}, " +
                        $"liveZero={evidence.CurrentZeroCount}, appleFlags={evidence.AppleFarmFlagCount}, " +
                        $"treeFlags={evidence.TreeFlagCount}, storedTreeWeight={evidence.TreeWeight}, " +
                        $"mismatch={(ready ? mismatch.ToString() : "unobserved")}, " +
                        $"tiles={GridTiles(evidence)}.");
                }
            Log($"AI_BUILD_APPLEFARM_SNAPSHOT: session={sessionId}, tick={lastTick}, " +
                $"stage={stage}, buildingId={farm.BuildingId}, globalId={farm.GlobalId}, " +
                $"player={farm.PlayerId}, buildingStatus={buildingStatus}, " +
                $"occupiedGridBounds=({farm.X},{farm.Y})+{farm.Size}x{farm.Size}, " +
                $"coarseRange=({startX},{startY})-({endX},{endY}), " +
                $"cells={cellLines.Count}, valid={valid}, mismatch={mismatched}, unavailable={unavailable}, " +
                $"boundedTilesRead={footprintTiles}, buildingTiles={footprintBuildingTiles}, " +
                $"boundedAppleFlags={footprintAppleFlags}, " +
                $"boundedTreeFlags={footprintTreeFlags}, boundedPclZero={footprintZero}; " +
                "mismatchComparesRawStoredForeignWithCurrentPclCount; treeWeightIsRawVanillaValue.");
            foreach (string line in cellLines) Log(line);
        }

        private sealed class AppleFarmWatch
        {
            internal int BuildingId, PlayerId, X, Y, Size;
            internal uint GlobalId;
            internal long DueTickCount;
            internal bool IsNew, FiveTickDone, GridUpdateDone;
        }

        private void CaptureInitialHuts()
        {
            try
            {
                Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
                for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
                {
                    ref GameBuilding building = ref buildings[spanIndex];
                    int playerId = building.r_PlayerIdOwner;
                    if (playerId >= 1 && playerId <= 8 &&
                        building.r_AliveState == AliveState.IsAlive &&
                        building.r_BuildingType == eStructs.STRUCT_WOODCUTTERS_HUT)
                        initialHuts[playerId]++;
                }
            }
            catch (Exception ex) { Log("AI_BUILD_INITIAL_HUT_SCAN_FAILED: " + ex); }
        }

        private string InferStage(int playerId)
        {
            if (hutSpawns[playerId] > 0) return "woodcutter-spawn-observed";
            if (schedulerCalls[playerId] == 0) return "scheduler-not-observed";
            if (woodBuildCalls[playerId] == 0) return "wood-build-call-not-observed";
            if (woodSearchCalls[playerId] == 0) return "wood-build-returned-before-search";
            return "search-or-later-stage-see-trace";
        }

        private static string Describe(AiBuildDiagnosticRecord record)
        {
            switch (record.Stage)
            {
                case "scheduler-before":
                case "scheduler-after":
                    return $"villageSlot={record.A}, schedulerDelay={record.B}, economyPhase={record.C}";
                case "wood-build-before":
                    return $"fixSession={record.A}, fixEnabled={record.B}, loadedSave={record.C}, fixMapRelevant={record.D}";
                case "wood-search-before":
                case "wood-search-after":
                    return $"cooldown={record.A}, resultX={record.B}, resultY={record.C}";
                case "wood-nearby-before":
                    return $"coarseX={record.A}, coarseY={record.B}";
                case "wood-build-after":
                case "wood-nearby-after":
                    return $"resultX={record.A}, resultY={record.B}";
                case "wood-candidate-scan":
                    return $"visitedCells={record.A}, formalCandidates={record.B}, bestScore={record.C}, generation={record.D}";
                case "wood-candidate-scan-error":
                    return $"scanHResult={record.A}";
                case "route-components":
                    return $"sourceComponent={record.A}, targetComponent={record.B}, sourceTile={record.C}, targetTile={record.D}";
                case "route-result":
                    return $"result={record.A}, mapperIndex={record.B}, target=({record.C},{record.D})";
                case "route-evidence":
                    return $"status={record.RouteEvidence?.Status ?? "missing"}, sourceComponent={record.A}, " +
                        $"targetComponent={record.B}, sourceNative={record.RouteEvidence?.SourceNativeComponent}, " +
                        $"targetNative={record.RouteEvidence?.TargetNativeComponent}, " +
                        $"sourceTile={record.C}, targetTile={record.D}";
                case "wood-nearby-path-before":
                case "wood-nearby-path-after":
                    return $"status={record.NearbyPathEvidence?.Status ?? "missing"}, " +
                        $"input=({record.A},{record.B}), result=({record.C},{record.D})";
                default:
                    return $"a={record.A}, b={record.B}, c={record.C}, d={record.D}";
            }
        }

        public IAivBuildStepInvocation TryBegin(AivBuildStepContext context)
        {
            if (!active || context.PlayerId < 1 || context.PlayerId > 8) return null;
            return new BuildStepObservation(this, context.PlayerId, context.FrameIndex);
        }

        private sealed class BuildStepObservation : IAivBuildStepInvocation
        {
            private readonly AIBuildDiagnoseRuntime owner;
            private readonly int playerId;
            private readonly int frameIndex;
            internal BuildStepObservation(AIBuildDiagnoseRuntime owner, int playerId, int frameIndex)
            { this.owner = owner; this.playerId = playerId; this.frameIndex = frameIndex; }
            public void Complete(AivBuildStepCompletion completion)
            {
                if (!owner.active) return;
                string key = "aiv:" + playerId + ":" + frameIndex + ":" + completion.VanillaResult;
                if (!owner.seen.TryGetValue(key, out int count)) count = 0;
                owner.seen[key] = count + 1;
                if (count == 0)
                    owner.Log($"AI_BUILD_AIV_STEP: session={owner.sessionId}, tick={owner.lastTick}, " +
                        $"player={playerId}, frame={frameIndex}, completed={completion.VanillaCompleted}, result={completion.VanillaResult}.");
            }
        }

        private void Log(string value) => Shared.DebugLogHelper.LogInfo(log, value);
    }
}
