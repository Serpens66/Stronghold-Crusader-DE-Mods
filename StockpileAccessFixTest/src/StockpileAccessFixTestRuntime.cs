using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Zhuqiaomon.Memory;

namespace StockpileAccessFixTest
{
    internal sealed unsafe class StockpileAccessFixTestRuntime : IDisposable
    {
        private const int AutomaticNoBugTimeoutTicks = 400;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate long RevalidateBuildingAccessDelegate(
            NativePointer<GameBuildingManager> buildingManager,
            int buildingId,
            int requiredCandidate);

        private readonly ManualLogSource log;
        private readonly RevalidateBuildingAccessDelegate revalidateBuildingAccess;
        private readonly Dictionary<int, StockpileAccessEpisodePolicy> episodes =
            new Dictionary<int, StockpileAccessEpisodePolicy>();
        private readonly Dictionary<int, RouteSignature> trackedRoutes =
            new Dictionary<int, RouteSignature>();
        private readonly HashSet<int> observedUnitIds = new HashSet<int>();
        private readonly List<int> staleUnitIds = new List<int>();
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();

        private bool applied;
        private bool mapActive;
        private bool disabledForMap;
        private bool pendingMoveCapture;
        private bool pendingMoveResultSeen;
        private int pendingMoveUnitId;
        private int pendingMoveX;
        private int pendingMoveY;
        private long pendingMoveResult;
        private bool hasTriggerRoute;
        private TriggerRoute triggerRoute;
        private bool hasRejectedTriggerRoute;
        private TriggerRoute rejectedTriggerRoute;
        private int rejectedTriggerUntilTick;
        private bool automaticTestCompleted;
        private string pendingTestBlockerCleanupReason;
        private int latestSimulationTick;
        private bool captureBlockerSpawn;
        private int captureBlockerX;
        private int captureBlockerY;
        private int capturedBlockerBuildingId;
        private int testBlockerBuildingId;
        private uint testBlockerGlobalId;
        private int testBlockerX;
        private int testBlockerY;
        private int testBlockerSpawnTick;
        private int testBlockerUnitId;
        private uint testBlockerUnitGlobalId;
        private bool testBlockerRecoveryStarted;
        private long trackedCount;
        private long candidateCount;
        private long confirmedCount;
        private long reselectionCount;
        private long appliedCount;
        private long progressCount;
        private long verifiedCount;
        private long failedCount;

        internal StockpileAccessFixTestRuntime(
            ManualLogSource log,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            if (memory.IsEmpty || libraryBase == 0)
                throw new ArgumentException("The Crusader library is unavailable.");
            if (!string.Equals(
                    StockpileAccessFixNativeDefinition.ReferenceSha256,
                    Shared.DebugLogHelper.CurrentNativeSha256,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("The mod and shared native hash contracts disagree.");
            }

            ValidateManagedLayouts();
            Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                StockpileAccessFixNativeDefinition.RevalidateBuildingAccessPattern,
                StockpileAccessFixNativeDefinition.RevalidateBuildingAccessRva,
                referenceHashMatches,
                "revalidate stockpile access",
                log);
            if (resolution.Rva != StockpileAccessFixNativeDefinition.RevalidateBuildingAccessRva)
                throw new InvalidOperationException("The access helper resolved outside its audited RVA.");

            revalidateBuildingAccess = Marshal.GetDelegateForFunctionPointer<RevalidateBuildingAccessDelegate>(
                (IntPtr)(libraryBase + unchecked((ulong)resolution.Rva)));
        }

        internal void Apply()
        {
            if (applied)
                return;

            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(args => BeginMap($"new map campaignMapId={args.CampaignMapId}")));
            subscriptions.Add(MapLoaderR3EventHooks.OnLoadSave.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(args => BeginMap($"loaded save file={args.FileName ?? "<null>"}")));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(_ => TryRemoveTestBlocker("map unload")));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(_ => EndMap()));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingSpawn.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(CaptureBlockerSpawn));
            subscriptions.Add(UnitR3EventHooks.OnUnitMoveHere.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(CaptureMoveResult));
            GameTimeManagerAPI.Instance.OnTick += OnGameTick;
            applied = true;

            LogInfo(
                "STOCKPILE_ACCESS_DIAGNOSTIC_READY: correctionActive=true, scanEverySimulationTick=true, " +
                $"requiredConsecutiveTicks={StockpileAccessEpisodePolicy.RequiredConsecutiveTicks}, " +
                $"retryCooldownTicks={StockpileAccessEpisodePolicy.RetryCooldownTicks}, " +
                $"verificationTimeoutTicks={StockpileAccessEpisodePolicy.VerificationTimeoutTicks}, " +
                "unitIdsAndBuildingIdsAreOneBased=true, moveHereUsesScriptExtenderApi=true, " +
                $"testTrigger=automaticOncePerMap, testNoBugTimeoutTicks={AutomaticNoBugTimeoutTicks}, " +
                "testCleanup=automatic.");
        }

        public void Dispose()
        {
            if (!applied)
                return;

            TryRemoveTestBlocker("runtime dispose");
            GameTimeManagerAPI.Instance.OnTick -= OnGameTick;
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
            subscriptions.Clear();
            ClearTracking();
            mapActive = false;
            applied = false;
        }

        private void BeginMap(string reason)
        {
            ClearTracking();
            automaticTestCompleted = false;
            pendingTestBlockerCleanupReason = null;
            latestSimulationTick = 0;
            hasRejectedTriggerRoute = false;
            rejectedTriggerRoute = default;
            rejectedTriggerUntilTick = 0;
            mapActive = true;
            disabledForMap = false;
            trackedCount = 0;
            candidateCount = 0;
            confirmedCount = 0;
            reselectionCount = 0;
            appliedCount = 0;
            progressCount = 0;
            verifiedCount = 0;
            failedCount = 0;
            LogInfo($"STOCKPILE_ACCESS_MAP_TRACKING_STARTED: reason={reason}.");
        }

        private void EndMap()
        {
            LogInfo(
                $"STOCKPILE_ACCESS_MAP_SUMMARY: tracked={trackedCount}, candidates={candidateCount}, " +
                $"confirmed={confirmedCount}, reselections={reselectionCount}, applied={appliedCount}, " +
                $"progress={progressCount}, verified={verifiedCount}, failed={failedCount}, " +
                $"activeEpisodes={episodes.Count}, disabled={disabledForMap}.");
            mapActive = false;
            disabledForMap = false;
            ClearTracking();
        }

        private void OnGameTick(int tick)
        {
            if (!mapActive || disabledForMap)
                return;

            try
            {
                latestSimulationTick = tick;
                ScanWorkers(tick);
                ObserveAutomaticTestTimeout(tick);
            }
            catch (Exception exception)
            {
                disabledForMap = true;
                RequestTestBlockerCleanup("diagnostic disabled after runtime failure");
                ClearTracking();
                Shared.DebugLogHelper.LogError(
                    log,
                    $"STOCKPILE_ACCESS_DIAGNOSTIC_DISABLED: tick={tick}, " +
                    $"reason=worker memory inspection or native recovery failed, exception={exception}");
            }
        }

        private void ScanWorkers(int tick)
        {
            GameUnitManager* manager = GameUnitManagerAPI.Instance.GetUnitManager();
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            observedUnitIds.Clear();

            if (manager == null || units._array == null)
                throw new InvalidOperationException("The unit manager or unit array is unavailable.");

            int nextUnitId = checked((int)manager->r_NextUnitId);
            if (nextUnitId < 1 || nextUnitId > units.Length + 1)
            {
                throw new InvalidOperationException(
                    $"The unit manager reported an invalid next unit ID: {nextUnitId}, capacity={units.Length}.");
            }

            int unitCount = nextUnitId - 1;
            for (int spanIndex = 0; spanIndex < unitCount; spanIndex++)
            {
                GameUnit* unit = units.GetValuePointer(spanIndex);
                if (unit == null || !StockpileWorkerContracts.TryGet(unit->r_UnitChimp, out _))
                    continue;

                int unitId = spanIndex + 1;
                observedUnitIds.Add(unitId);
                StockpileObservation observation = Capture(unitId, unit);
                bool isLocalPlayerWorker =
                    unit->r_SpawnedForPlayerIndex == GamePlayerManagerAPI.Instance.GetLocalPlayerId();
                TrackFetchRoute(tick, observation, isLocalPlayerWorker);

                if (!episodes.TryGetValue(unitId, out StockpileAccessEpisodePolicy episode))
                {
                    if (!observation.HasIdleBugSignature)
                        continue;
                    episode = new StockpileAccessEpisodePolicy();
                    episodes.Add(unitId, episode);
                }

                StockpileEpisodeAction action = episode.Observe(observation, tick);
                switch (action)
                {
                    case StockpileEpisodeAction.CandidateStarted:
                        candidateCount++;
                        LogInfo(
                            $"STOCKPILE_ACCESS_BUG_CANDIDATE: tick={tick}, {Describe(observation)}, " +
                            "reason=noPath+pendingMarker+notAtCachedEntry.");
                        break;
                    case StockpileEpisodeAction.ConfirmAndRepair:
                        ConfirmAndRepair(tick, unit, observation, episode);
                        break;
                    case StockpileEpisodeAction.Progress:
                        progressCount++;
                        LogInfo($"STOCKPILE_ACCESS_FIX_PROGRESS: tick={tick}, {Describe(observation)}.");
                        break;
                    case StockpileEpisodeAction.Verified:
                        verifiedCount++;
                        LogInfo(
                                $"STOCKPILE_ACCESS_FIX_VERIFIED: tick={tick}, {Describe(observation)}, " +
                                "reason=workerLeftStockpileFetchState.");
                        CompleteAutomaticTestIfMatching(observation, "fixVerified");
                        break;
                    case StockpileEpisodeAction.Unverified:
                        failedCount++;
                        Shared.DebugLogHelper.LogWarning(
                            log,
                                $"STOCKPILE_ACCESS_FIX_FAILED: tick={tick}, {Describe(observation)}, " +
                                $"reason=verificationTimedOutOrIdentityChanged, retryAfterTicks={StockpileAccessEpisodePolicy.RetryCooldownTicks}.");
                        CompleteAutomaticTestIfMatching(observation, "fixVerificationFailed");
                        break;
                }

                if (episode.CanDiscard)
                    episodes.Remove(unitId);
            }

            RemoveStaleTracking();
        }

        private void TrackFetchRoute(
            int tick,
            in StockpileObservation observation,
            bool isLocalPlayerWorker)
        {
            if (!observation.IsValidFetchRoute)
            {
                trackedRoutes.Remove(observation.UnitId);
                return;
            }

            if (observation.PathFlags == 0)
                return;

            if (!automaticTestCompleted && testBlockerBuildingId <= 0 &&
                isLocalPlayerWorker && observation.PathMarker != 0)
            {
                TriggerRoute nextTrigger = new TriggerRoute(observation);
                bool rejectionExpired = latestSimulationTick >= rejectedTriggerUntilTick;
                if ((!hasRejectedTriggerRoute || !rejectedTriggerRoute.Equals(nextTrigger) || rejectionExpired) &&
                    (!hasTriggerRoute || !triggerRoute.Equals(nextTrigger)))
                {
                    triggerRoute = nextTrigger;
                    hasTriggerRoute = true;
                    LogInfo(
                        $"STOCKPILE_TEST_BLOCKER_READY: tick={tick}, {Describe(observation)}, " +
                        "action=automatic wall placement queued for next Unity frame.");
                }
            }

            RouteSignature route = new RouteSignature(observation);
            if (trackedRoutes.TryGetValue(observation.UnitId, out RouteSignature previous) && previous.Equals(route))
                return;

            trackedRoutes[observation.UnitId] = route;
            trackedCount++;
            LogInfo(
                $"STOCKPILE_FETCH_ROUTE_TRACKED: tick={tick}, {Describe(observation)}, " +
                "instruction=block target while leaving another stockpile access reachable.");
        }

        private void ConfirmAndRepair(
            int tick,
            GameUnit* unit,
            in StockpileObservation observation,
            StockpileAccessEpisodePolicy episode)
        {
            if (IsTestBlockerWorker(observation))
                testBlockerRecoveryStarted = true;
            confirmedCount++;
            LogInfo(
                $"STOCKPILE_ACCESS_BUG_CONFIRMED: tick={tick}, {Describe(observation)}, " +
                $"consecutiveTicks={StockpileAccessEpisodePolicy.RequiredConsecutiveTicks}.");

            StockpileObservation freshBefore = Capture(observation.UnitId, unit);
            if (!freshBefore.HasIdleBugSignature || !freshBefore.IsSameStuckSnapshotAs(observation))
            {
                episode.RecordRepairOutcome(freshBefore, tick, routeAccepted: false);
                RecordRepairFailure(tick, freshBefore, "candidateChangedBeforeRepair");
                return;
            }

            NativePointer<GameBuildingManager> buildingManager = GameBuildingManagerAPI.Instance.GetBuildingManager();
            GameBuildingManager* buildingManagerPointer = buildingManager;
            if (buildingManagerPointer == null)
                throw new InvalidOperationException("The building manager is unavailable.");

            ushort oldEntryX = freshBefore.EntryX;
            ushort oldEntryY = freshBefore.EntryY;
            long accessResult = revalidateBuildingAccess(
                buildingManager,
                freshBefore.StorageBuildingId,
                1);

            StockpileObservation afterReselection = Capture(observation.UnitId, unit);
            reselectionCount++;
            LogInfo(
                $"STOCKPILE_ACCESS_RESELECTED: tick={tick}, unitId={afterReselection.UnitId}, " +
                $"globalId={afterReselection.UnitGlobalId}, worker={afterReselection.UnitType}, " +
                $"stockpileBuildingId={afterReselection.StorageBuildingId}, accessResult={accessResult}, " +
                $"oldEntry={oldEntryX}/{oldEntryY}, newEntry={afterReselection.EntryX}/{afterReselection.EntryY}, " +
                $"entryChanged={oldEntryX != afterReselection.EntryX || oldEntryY != afterReselection.EntryY}.");

            if (accessResult == 0 || afterReselection.EntryX == 0 || afterReselection.EntryY == 0 ||
                !afterReselection.Alive || !afterReselection.OwnedStockpile ||
                !afterReselection.StorageGenerationMatches)
            {
                episode.RecordRepairOutcome(afterReselection, tick, routeAccepted: false);
                RecordRepairFailure(tick, afterReselection, "vanillaFoundNoValidStockpileAccess");
                return;
            }

            pendingMoveCapture = true;
            pendingMoveResultSeen = false;
            pendingMoveUnitId = afterReselection.UnitId;
            pendingMoveX = afterReselection.EntryX;
            pendingMoveY = afterReselection.EntryY;
            pendingMoveResult = 0;
            try
            {
                GameUnitManagerAPI.Instance.MoveToTile(
                    afterReselection.UnitId,
                    afterReselection.EntryX,
                    afterReselection.EntryY,
                    0);
            }
            finally
            {
                pendingMoveCapture = false;
            }

            StockpileObservation afterMove = Capture(observation.UnitId, unit);
            bool routeAccepted = pendingMoveResultSeen
                ? pendingMoveResult != 0
                : afterMove.PathFlags != 0;
            episode.RecordRepairOutcome(afterMove, tick, routeAccepted);
            if (!routeAccepted)
            {
                RecordRepairFailure(
                    tick,
                    afterMove,
                    pendingMoveResultSeen ? $"moveHereReturned{pendingMoveResult}" : "moveHerePostEventMissingAndNoActivePath");
                return;
            }

            appliedCount++;
            LogInfo(
                $"STOCKPILE_ACCESS_FIX_APPLIED: tick={tick}, {Describe(afterMove)}, " +
                $"oldEntry={oldEntryX}/{oldEntryY}, newEntry={afterReselection.EntryX}/{afterReselection.EntryY}, " +
                $"moveResult={(pendingMoveResultSeen ? pendingMoveResult.ToString() : "notCaptured")}, " +
                "changedAIState=false, changedPathMarker=false, teleported=false.");
        }

        private void CaptureMoveResult(UnitMoveHereEventArgs args)
        {
            if (!pendingMoveCapture || args.UnitId != pendingMoveUnitId ||
                args.TileX != pendingMoveX || args.TileY != pendingMoveY)
            {
                return;
            }

            pendingMoveResult = args.ReturnValue;
            pendingMoveResultSeen = true;
        }

        internal void ProcessAutomaticTestTrigger()
        {
            if (!string.IsNullOrEmpty(pendingTestBlockerCleanupReason))
            {
                string reason = pendingTestBlockerCleanupReason;
                pendingTestBlockerCleanupReason = null;
                TryRemoveTestBlocker(reason);
            }

            if (!mapActive || disabledForMap || automaticTestCompleted ||
                testBlockerBuildingId > 0 || !hasTriggerRoute)
            {
                return;
            }

            TrySpawnTestBlocker();
        }

        private void TrySpawnTestBlocker()
        {
            if (!mapActive || disabledForMap)
            {
                RejectTriggerRoute("no active diagnostic map");
                return;
            }
            if (testBlockerBuildingId > 0)
            {
                RejectTriggerRoute($"test blocker {testBlockerBuildingId} already exists");
                return;
            }
            if (!hasTriggerRoute)
                return;
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(triggerRoute.UnitId, out GameUnit* unit) || unit == null)
            {
                RejectTriggerRoute("armed worker slot is no longer valid");
                return;
            }

            StockpileObservation current = Capture(triggerRoute.UnitId, unit);
            if (current.UnitGlobalId != triggerRoute.UnitGlobalId ||
                !current.IsValidFetchRoute || current.PathFlags == 0 || current.PathMarker == 0 ||
                current.TargetX != triggerRoute.TargetX || current.TargetY != triggerRoute.TargetY ||
                (current.CurrentX == current.TargetX && current.CurrentY == current.TargetY))
            {
                RejectTriggerRoute("armed fetch route is no longer active and unchanged");
                return;
            }

            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            int x = current.TargetX;
            int y = current.TargetY;
            if (!tileApi.IsTileInsideMapBounds(x, y))
            {
                RejectTriggerRoute($"target {x}/{y} is outside playable bounds");
                return;
            }

            int tileId = tileApi.GetTileId(x, y);
            ushort priorBuildingId = tileApi.GetTileBuildingId(tileId);
            TilePropertyFlag priorFlags = tileApi.GetTilePropertyFlag(tileId);
            if (!IsSafeExternalBlockerTile(priorFlags, priorBuildingId))
            {
                RejectTriggerRoute(
                    $"target {x}/{y} is not a free external land tile: tileId={tileId}, " +
                    $"buildingId={priorBuildingId}, flags=0x{unchecked((uint)priorFlags):X8}");
                return;
            }

            captureBlockerSpawn = true;
            captureBlockerX = x;
            captureBlockerY = y;
            capturedBlockerBuildingId = 0;
            long placementResult;
            bool priorOverrideEnabled = tileApi.TileManager.UsePlacementBlockedOverride;
            bool priorOverrideValue = tileApi.TileManager.PlacementBlockedOverrideValue;
            try
            {
                placementResult = GameBuildingManagerAPI.Instance.CreatePrefab(
                    checked((int)unit->r_SpawnedForPlayerIndex),
                    x,
                    y,
                    eMappers.MAPPER_WOODWALL,
                    1,
                    0,
                    true,
                    true);
            }
            finally
            {
                // CreatePrefab owns this global override during placement; preserve any prior caller state.
                tileApi.TileManager.PlacementBlockedOverrideValue = priorOverrideValue;
                tileApi.TileManager.UsePlacementBlockedOverride = priorOverrideEnabled;
                captureBlockerSpawn = false;
            }

            int buildingId = capturedBlockerBuildingId > 0
                ? capturedBlockerBuildingId
                : tileApi.GetTileBuildingId(tileId);
            GameBuilding* blocker = null;
            bool blockerFound = buildingId > 0 &&
                GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out blocker);
            if (placementResult == 0 || !blockerFound ||
                blocker == null || blocker->r_BuildingType != eStructs.STRUCT_WOOD_WALL ||
                blocker->r_PlayerIdOwner != unit->r_SpawnedForPlayerIndex ||
                tileApi.GetTileBuildingId(tileId) != buildingId ||
                tileApi.IsTileWalkableAndUnoccupied(tileId))
            {
                if (buildingId > 0 && blocker != null &&
                    blocker->r_BuildingType == eStructs.STRUCT_WOOD_WALL &&
                    blocker->r_PlayerIdOwner == unit->r_SpawnedForPlayerIndex)
                {
                    GameBuildingManagerAPI.Instance.DeleteBuildingSafe(buildingId);
                }
                RejectTriggerRoute(
                    $"Vanilla wall placement did not create a verified blocker: result={placementResult}, " +
                    $"candidateBuildingId={buildingId}, target={x}/{y}");
                return;
            }

            testBlockerBuildingId = buildingId;
            testBlockerGlobalId = blocker->r_GlobalId;
            testBlockerX = x;
            testBlockerY = y;
            testBlockerSpawnTick = latestSimulationTick;
            testBlockerUnitId = current.UnitId;
            testBlockerUnitGlobalId = current.UnitGlobalId;
            testBlockerRecoveryStarted = false;
            automaticTestCompleted = true;
            hasTriggerRoute = false;
            LogInfo(
                $"STOCKPILE_TEST_BLOCKER_SPAWNED: unitId={current.UnitId}, globalId={current.UnitGlobalId}, " +
                $"worker={current.UnitType}, blockerBuildingId={buildingId}, blockerGlobalId={testBlockerGlobalId}, " +
                $"target={x}/{y}, tileId={tileId}, priorFlags=0x{unchecked((uint)priorFlags):X8}, " +
                "mapper=MAPPER_WOODWALL, bypassedPlacementRules=true, directTileMutation=false, " +
                "cleanup=automaticAfterResultOrTimeout.");
        }

        internal void TryRemoveTestBlocker(string reason)
        {
            if (testBlockerBuildingId <= 0)
                return;

            int buildingId = testBlockerBuildingId;
            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* blocker) ||
                blocker == null || blocker->r_GlobalId != testBlockerGlobalId ||
                blocker->r_BuildingType != eStructs.STRUCT_WOOD_WALL)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"STOCKPILE_TEST_BLOCKER_REMOVE_FAILED: blockerBuildingId={buildingId}, " +
                    $"blockerGlobalId={testBlockerGlobalId}, target={testBlockerX}/{testBlockerY}, " +
                    $"reason=identity mismatch or blocker already gone, requestedBy={reason}.");
                ClearTestBlockerIdentity();
                return;
            }

            bool marked = GameBuildingManagerAPI.Instance.DeleteBuildingSafe(buildingId);
            if (marked)
            {
                LogInfo(
                    $"STOCKPILE_TEST_BLOCKER_REMOVED: blockerBuildingId={buildingId}, " +
                    $"blockerGlobalId={testBlockerGlobalId}, target={testBlockerX}/{testBlockerY}, requestedBy={reason}.");
            }
            else
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"STOCKPILE_TEST_BLOCKER_REMOVE_FAILED: blockerBuildingId={buildingId}, " +
                    $"blockerGlobalId={testBlockerGlobalId}, target={testBlockerX}/{testBlockerY}, requestedBy={reason}.");
            }
            ClearTestBlockerIdentity();
        }

        internal static bool IsSafeExternalBlockerTile(TilePropertyFlag flags, ushort buildingId) =>
            buildingId == 0 && (flags == TilePropertyFlag.Free || flags == TilePropertyFlag.None);

        private void CaptureBlockerSpawn(BuildingSpawnEventArgs args)
        {
            if (!captureBlockerSpawn || args.Building != eStructs.STRUCT_WOOD_WALL ||
                args.TileX != captureBlockerX || args.TileY != captureBlockerY)
            {
                return;
            }
            capturedBlockerBuildingId = unchecked((int)args.ReturnValue);
        }

        private void RejectTriggerRoute(string reason)
        {
            if (hasTriggerRoute)
            {
                rejectedTriggerRoute = triggerRoute;
                hasRejectedTriggerRoute = true;
                rejectedTriggerUntilTick = checked(latestSimulationTick + 50);
            }
            hasTriggerRoute = false;
            Shared.DebugLogHelper.LogWarning(log, $"STOCKPILE_TEST_BLOCKER_FAILED: reason={reason}.");
        }

        private void ObserveAutomaticTestTimeout(int tick)
        {
            if (testBlockerBuildingId <= 0 || testBlockerRecoveryStarted ||
                !string.IsNullOrEmpty(pendingTestBlockerCleanupReason) ||
                tick - testBlockerSpawnTick < AutomaticNoBugTimeoutTicks)
            {
                return;
            }

            LogInfo(
                $"STOCKPILE_TEST_AUTOMATION_RESULT: outcome=noStableBugSignature, tick={tick}, " +
                $"unitId={testBlockerUnitId}, globalId={testBlockerUnitGlobalId}, " +
                $"blockerBuildingId={testBlockerBuildingId}, target={testBlockerX}/{testBlockerY}, " +
                $"timeoutTicks={AutomaticNoBugTimeoutTicks}.");
            RequestTestBlockerCleanup("automatic test produced no stable bug signature");
        }

        private void CompleteAutomaticTestIfMatching(
            in StockpileObservation observation,
            string outcome)
        {
            if (testBlockerBuildingId <= 0 || observation.UnitId != testBlockerUnitId)
                return;

            LogInfo(
                $"STOCKPILE_TEST_AUTOMATION_RESULT: outcome={outcome}, " +
                $"unitId={observation.UnitId}, observedGlobalId={observation.UnitGlobalId}, " +
                $"expectedGlobalId={testBlockerUnitGlobalId}, blockerBuildingId={testBlockerBuildingId}, " +
                $"target={testBlockerX}/{testBlockerY}.");
            RequestTestBlockerCleanup("automatic test completed: " + outcome);
        }

        private bool IsTestBlockerWorker(in StockpileObservation observation) =>
            testBlockerBuildingId > 0 && observation.UnitId == testBlockerUnitId &&
            observation.UnitGlobalId == testBlockerUnitGlobalId;

        private void RequestTestBlockerCleanup(string reason)
        {
            if (testBlockerBuildingId > 0 && string.IsNullOrEmpty(pendingTestBlockerCleanupReason))
                pendingTestBlockerCleanupReason = reason;
        }

        private void ClearTestBlockerIdentity()
        {
            testBlockerBuildingId = 0;
            testBlockerGlobalId = 0;
            testBlockerX = 0;
            testBlockerY = 0;
            testBlockerSpawnTick = 0;
            testBlockerUnitId = 0;
            testBlockerUnitGlobalId = 0;
            testBlockerRecoveryStarted = false;
            pendingTestBlockerCleanupReason = null;
        }

        private void RecordRepairFailure(int tick, in StockpileObservation observation, string reason)
        {
            failedCount++;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"STOCKPILE_ACCESS_FIX_FAILED: tick={tick}, {Describe(observation)}, reason={reason}, " +
                $"retryAfterTicks={StockpileAccessEpisodePolicy.RetryCooldownTicks}.");
            CompleteAutomaticTestIfMatching(observation, "repairFailed:" + reason);
        }

        private static StockpileObservation Capture(int unitId, GameUnit* unit)
        {
            bool alive = unit != null && unit->r_AliveState == AliveState.IsAlive;
            eChimps unitType = unit == null ? eChimps.CHIMP_TYPE_NULL : unit->r_UnitChimp;
            ushort state = unit == null ? (ushort)0 : unit->r_AIState;
            bool supported = StockpileWorkerContracts.TryGet(unitType, out StockpileWorkerContract contract) &&
                state == contract.FetchState;
            ushort storageBuildingId = unit == null
                ? (ushort)0
                : ReadUInt16(unit, StockpileAccessFixNativeDefinition.UnitStorageBuildingIdOffset);
            uint storedBuildingGlobalId = unit == null
                ? 0
                : ReadUInt32(unit, StockpileAccessFixNativeDefinition.UnitStoredBuildingGlobalIdOffset);

            bool ownedStockpile = false;
            bool storageGenerationMatches = false;
            ushort entryX = 0;
            ushort entryY = 0;
            if (storageBuildingId > 0 &&
                GameBuildingManagerAPI.Instance.TryGetBuildingById(storageBuildingId, out GameBuilding* building) &&
                building != null)
            {
                ownedStockpile = building->r_AliveState == AliveState.IsAlive &&
                    building->r_BuildingType == eStructs.STRUCT_GOODS_YARD &&
                    unit != null && building->r_PlayerIdOwner == unit->r_SpawnedForPlayerIndex;
                storageGenerationMatches = building->r_GlobalId == storedBuildingGlobalId;
                entryX = ReadUInt16(building, StockpileAccessFixNativeDefinition.BuildingEntryXOffset);
                entryY = ReadUInt16(building, StockpileAccessFixNativeDefinition.BuildingEntryYOffset);
            }

            return new StockpileObservation(
                unitId,
                unit == null ? 0 : unit->r_GlobalId,
                unitType,
                state,
                alive,
                supported,
                ownedStockpile,
                storageGenerationMatches,
                unit == null ? (ushort)0 : unit->r_PathPlanStateBitFlags,
                unit == null ? (ushort)0 : unit->r_PathPlanRelated3,
                unit == null ? (ushort)0 : unit->r_CurrentTilePositionX,
                unit == null ? (ushort)0 : unit->r_CurrentTilePositionY,
                unit == null ? (ushort)0 : unit->r_TargetTilePositionX2,
                unit == null ? (ushort)0 : unit->r_TargetTilePositionY2,
                entryX,
                entryY,
                storageBuildingId,
                unit == null ? (ushort)0 : unit->r_LinkedProductionBuildingId);
        }

        private static ushort ReadUInt16(void* pointer, int offset) =>
            *(ushort*)((byte*)pointer + offset);

        private static uint ReadUInt32(void* pointer, int offset) =>
            *(uint*)((byte*)pointer + offset);

        private static string Describe(in StockpileObservation observation) =>
            $"unitId={observation.UnitId}, globalId={observation.UnitGlobalId}, " +
            $"worker={observation.UnitType}, state={observation.State}, " +
            $"position={observation.CurrentX}/{observation.CurrentY}, " +
            $"target={observation.TargetX}/{observation.TargetY}, " +
            $"cachedEntry={observation.EntryX}/{observation.EntryY}, " +
            $"pathFlags={observation.PathFlags}, pathMarker={observation.PathMarker}, " +
            $"stockpileBuildingId={observation.StorageBuildingId}, " +
            $"productionBuildingId={observation.ProductionBuildingId}";

        private void RemoveStaleTracking()
        {
            staleUnitIds.Clear();
            foreach (KeyValuePair<int, StockpileAccessEpisodePolicy> pair in episodes)
            {
                if (!observedUnitIds.Contains(pair.Key))
                    staleUnitIds.Add(pair.Key);
            }
            foreach (int unitId in staleUnitIds)
            {
                episodes[unitId].Cancel();
                episodes.Remove(unitId);
            }

            staleUnitIds.Clear();
            foreach (int unitId in trackedRoutes.Keys)
            {
                if (!observedUnitIds.Contains(unitId))
                    staleUnitIds.Add(unitId);
            }
            foreach (int unitId in staleUnitIds)
                trackedRoutes.Remove(unitId);
        }

        private void ClearTracking()
        {
            foreach (StockpileAccessEpisodePolicy episode in episodes.Values)
                episode.Cancel();
            episodes.Clear();
            trackedRoutes.Clear();
            observedUnitIds.Clear();
            staleUnitIds.Clear();
            pendingMoveCapture = false;
            pendingMoveResultSeen = false;
            hasTriggerRoute = false;
            triggerRoute = default;
            hasRejectedTriggerRoute = false;
            rejectedTriggerRoute = default;
            rejectedTriggerUntilTick = 0;
            captureBlockerSpawn = false;
            capturedBlockerBuildingId = 0;
        }

        private static void ValidateManagedLayouts()
        {
            if (Marshal.SizeOf(typeof(GameUnit)) != StockpileAccessFixNativeDefinition.GameUnitSize)
                throw new InvalidOperationException("GameUnit layout differs from the audited 0x490-byte contract.");
            if (Marshal.SizeOf(typeof(GameBuilding)) != StockpileAccessFixNativeDefinition.GameBuildingSize)
                throw new InvalidOperationException("GameBuilding layout differs from the audited 0x32C-byte contract.");
            if (Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_AIState)).ToInt32() != 0x2BC ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_PathPlanStateBitFlags)).ToInt32() != 0xF2 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_PathPlanRelated3)).ToInt32() != 0x290 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_GlobalId)).ToInt32() != 0x94 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_CurrentTilePositionX)).ToInt32() != 0xC0 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_TargetTilePositionX2)).ToInt32() != 0xE8 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_LinkedProductionBuildingId)).ToInt32() != 0x334)
            {
                throw new InvalidOperationException("Worker state/path fields differ from the audited layout.");
            }
            if (Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_AliveState)).ToInt32() != 0xD0 ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_BuildingType)).ToInt32() != 0xD2 ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_PlayerIdOwner)).ToInt32() != 0xD6 ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_GlobalId)).ToInt32() != 0xD8)
            {
                throw new InvalidOperationException("Stockpile identity fields differ from the audited layout.");
            }

            // These four unnamed native fields are intentionally accessed only through audited offsets.
            if (StockpileAccessFixNativeDefinition.UnitStoredBuildingGlobalIdOffset != 0x9C ||
                StockpileAccessFixNativeDefinition.UnitStorageBuildingIdOffset != 0x332 ||
                StockpileAccessFixNativeDefinition.BuildingEntryXOffset != 0xFE ||
                StockpileAccessFixNativeDefinition.BuildingEntryYOffset != 0x100)
            {
                throw new InvalidOperationException("Raw stockpile-access offsets differ from the audited contract.");
            }
        }

        private void LogInfo(string message) => Shared.DebugLogHelper.LogInfo(log, message);

        private readonly struct RouteSignature : IEquatable<RouteSignature>
        {
            internal RouteSignature(in StockpileObservation observation)
            {
                GlobalId = observation.UnitGlobalId;
                State = observation.State;
                StorageBuildingId = observation.StorageBuildingId;
                TargetX = observation.TargetX;
                TargetY = observation.TargetY;
            }

            private uint GlobalId { get; }
            private ushort State { get; }
            private ushort StorageBuildingId { get; }
            private ushort TargetX { get; }
            private ushort TargetY { get; }

            public bool Equals(RouteSignature other) =>
                GlobalId == other.GlobalId && State == other.State &&
                StorageBuildingId == other.StorageBuildingId &&
                TargetX == other.TargetX && TargetY == other.TargetY;
        }

        private readonly struct TriggerRoute : IEquatable<TriggerRoute>
        {
            internal TriggerRoute(in StockpileObservation observation)
            {
                UnitId = observation.UnitId;
                UnitGlobalId = observation.UnitGlobalId;
                TargetX = observation.TargetX;
                TargetY = observation.TargetY;
            }

            internal int UnitId { get; }
            internal uint UnitGlobalId { get; }
            internal ushort TargetX { get; }
            internal ushort TargetY { get; }

            public bool Equals(TriggerRoute other) =>
                UnitId == other.UnitId && UnitGlobalId == other.UnitGlobalId &&
                TargetX == other.TargetX && TargetY == other.TargetY;
        }
    }
}
