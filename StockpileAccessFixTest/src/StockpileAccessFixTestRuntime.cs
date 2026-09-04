using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
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
        private const int BlockerOccupancyTimeoutTicks = 50;
        internal const int BlockerApproachSearchRadius = 8;
        internal const int AutomaticTriggerRetryTicks = 50;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate long RevalidateBuildingAccessDelegate(
            NativePointer<GameBuildingManager> buildingManager,
            int buildingId,
            int requiredCandidate);

        private readonly ManualLogSource log;
        private readonly RevalidateBuildingAccessDelegate revalidateBuildingAccess;
        private readonly Dictionary<int, StockpileAccessEpisodePolicy> episodes =
            new Dictionary<int, StockpileAccessEpisodePolicy>();
        private readonly Dictionary<int, TrackedRoute> trackedRoutes =
            new Dictionary<int, TrackedRoute>();
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
        private int nextAutomaticTriggerTick;
        private bool automaticTestCompleted;
        private int latestSimulationTick;
        private TestInjectionPhase testInjectionPhase;
        private int testVictimUnitId;
        private uint testVictimUnitGlobalId;
        private int testBlockerUnitId;
        private uint testBlockerUnitGlobalId;
        private ushort testBlockerOriginalX;
        private ushort testBlockerOriginalY;
        private ushort testBlockerOriginalTargetX;
        private ushort testBlockerOriginalTargetY;
        private ushort testBlockerStorageBuildingId;
        private ushort testBlockerApproachX;
        private ushort testBlockerApproachY;
        private int testBlockerApproachTileId;
        private int testBlockerX;
        private int testBlockerY;
        private int testBlockerSpawnTick;
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
                .Subscribe(_ => TryRestoreTestBlocker("map unload")));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(_ => EndMap()));
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
                $"testTrigger=automaticCivilianOccupancyOncePerMap, testNoBugTimeoutTicks={AutomaticNoBugTimeoutTicks}, " +
                $"blockerOccupancyTimeoutTicks={BlockerOccupancyTimeoutTicks}, " +
                $"blockerApproachSearchRadius={BlockerApproachSearchRadius}, " +
                $"automaticTriggerRetryTicks={AutomaticTriggerRetryTicks}, naturalOccupancyPreferred=true, " +
                "syntheticBlockerRequiresIndependentTargetAndUnregisteredOrigin=true, testCleanup=automatic.");
        }

        public void Dispose()
        {
            if (!applied)
                return;

            TryRestoreTestBlocker("runtime dispose");
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
            latestSimulationTick = 0;
            nextAutomaticTriggerTick = 0;
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
            ClearTestInjectionIdentity();
        }

        private void OnGameTick(int tick)
        {
            if (!mapActive || disabledForMap)
                return;

            try
            {
                latestSimulationTick = tick;
                // A route armed while scanning tick N is mutated before scanning tick N+1.
                // The Script Extender documents this pre-tick callback as its synchronized game-loop context.
                ProcessAutomaticTestTrigger();
                ScanWorkers(tick);
                ObserveAutomaticTestTimeout(tick);
            }
            catch (Exception exception)
            {
                disabledForMap = true;
                try
                {
                    TryRestoreTestBlocker("diagnostic disabled after runtime failure");
                }
                catch (Exception cleanupException)
                {
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"STOCKPILE_TEST_BLOCKER_FAILED: tick={tick}, " +
                        $"reason=emergency cleanup failed, exception={cleanupException}");
                }
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
                TrackFetchRoute(tick, observation);

                if (!episodes.TryGetValue(unitId, out StockpileAccessEpisodePolicy episode))
                {
                    bool hasMatchingRecentlyActiveRoute =
                        trackedRoutes.TryGetValue(unitId, out TrackedRoute activeRoute) &&
                        activeRoute.Signature.Equals(new RouteSignature(observation)) &&
                        tick - activeRoute.LastActiveTick <= 2;
                    if (!StockpileAccessEpisodePolicy.CanStartCandidate(
                            observation,
                            hasMatchingRecentlyActiveRoute))
                    {
                        continue;
                    }
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
                            "reason=previouslyActiveFetchRouteBecameNoPath+notAtCachedEntry.");
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
            in StockpileObservation observation)
        {
            if (!observation.IsValidFetchRoute)
            {
                trackedRoutes.Remove(observation.UnitId);
                return;
            }

            if (observation.PathFlags == 0)
                return;

            if (!automaticTestCompleted && testInjectionPhase == TestInjectionPhase.None &&
                !hasTriggerRoute && tick >= nextAutomaticTriggerTick &&
                observation.UnitType == eChimps.CHIMP_TYPE_FLETCHER)
            {
                triggerRoute = new TriggerRoute(observation);
                hasTriggerRoute = true;
                LogInfo(
                    $"STOCKPILE_TEST_BLOCKER_READY: tick={tick}, ownerId={GetUnitOwner(observation.UnitId)}, " +
                    $"{Describe(observation)}, action=native occupant or independent moving Fletcher will block " +
                    "the internal stockpile access on the next simulation tick.");
            }

            RouteSignature route = new RouteSignature(observation);
            bool routeChanged =
                !trackedRoutes.TryGetValue(observation.UnitId, out TrackedRoute previous) ||
                !previous.Signature.Equals(route);
            // Refresh this on every active-path tick. A stale route observed much earlier must not
            // authorize a later generic idle state as a stockpile-access failure.
            trackedRoutes[observation.UnitId] = new TrackedRoute(route, tick);
            if (!routeChanged)
                return;

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
            if (IsTestVictimWorker(observation))
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
                "changedAIState=false, changedAlternatePathConnectionId=false, teleported=false.");
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

        private void ProcessAutomaticTestTrigger()
        {
            if (!mapActive || disabledForMap || automaticTestCompleted)
                return;

            if (testInjectionPhase == TestInjectionPhase.AwaitingOccupancy)
            {
                TryForceRouteFailureAfterOccupancy();
                return;
            }

            if (!hasTriggerRoute)
            {
                return;
            }

            TryTeleportTestBlocker();
        }

        private void TryTeleportTestBlocker()
        {
            if (!mapActive || disabledForMap)
            {
                RejectTriggerRoute("no active diagnostic map");
                return;
            }
            if (testInjectionPhase != TestInjectionPhase.None)
            {
                RejectTriggerRoute("another civilian occupancy injection is already active");
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
                !current.IsValidFetchRoute || current.PathFlags == 0 ||
                current.TargetX != triggerRoute.TargetX || current.TargetY != triggerRoute.TargetY ||
                (current.CurrentX == current.TargetX && current.CurrentY == current.TargetY))
            {
                RejectTriggerRoute("armed fetch route is no longer active and unchanged");
                return;
            }

            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            int accessX = current.TargetX;
            int accessY = current.TargetY;
            bool isConnection = TryInspectStockpileConnectionTile(
                tileApi,
                accessX,
                accessY,
                out int tileId,
                out TilePropertyFlag priorFlags,
                out ushort priorBuildingId,
                out ushort priorUnitId);
            if (!isConnection)
            {
                RejectTriggerRoute(
                    $"cached access is not an internal GoodsyardConnection tile: " +
                    $"access={accessX}/{accessY}, tileId={tileId}, buildingId={priorBuildingId}, " +
                    $"unitId={priorUnitId}, flags=0x{unchecked((uint)priorFlags):X8}");
                return;
            }

            if (priorUnitId != 0)
            {
                if (!TryValidateLivingOccupant(priorUnitId, accessX, accessY, out uint occupantGlobalId))
                {
                    RejectTriggerRoute(
                        $"TileUnitIdGrid reported stale occupant unitId={priorUnitId} at access={accessX}/{accessY}");
                    return;
                }

                PrepareTestVictim(current, accessX, accessY);
                LogInfo(
                    $"STOCKPILE_TEST_NATURAL_OCCUPANCY_USED: tick={latestSimulationTick}, " +
                    $"victimUnitId={current.UnitId}, victimOwnerId={GetUnitOwner(current.UnitId)}, " +
                    $"occupantUnitId={priorUnitId}, occupantGlobalId={occupantGlobalId}, " +
                    $"occupantOwnerId={GetUnitOwner(priorUnitId)}, target={accessX}/{accessY}, " +
                    $"tileId={tileId}, flags=0x{unchecked((uint)priorFlags):X8}.");
                if (!TryForceVictimRouteFailure(priorUnitId, "existingVanillaOccupancy"))
                {
                    ClearTestInjectionIdentity();
                    RejectTriggerRoute("existing native occupant did not make the victim MoveToTile call fail");
                }
                return;
            }

            if (!TryFindFetchingFletcherBlocker(current, out int blockerUnitId, out StockpileObservation blocker))
            {
                RejectTriggerRoute(
                    $"no same-owner moving Fletcher with an independent target and unregistered origin was available");
                return;
            }

            if (!TryFindFreeBlockerApproach(
                    tileApi,
                    current,
                    out ushort approachX,
                    out ushort approachY,
                    out int approachTileId))
            {
                RejectTriggerRoute(
                    $"no free walkable approach within radius={BlockerApproachSearchRadius} of " +
                    $"cached access={accessX}/{accessY} was available");
                return;
            }

            PrepareTestVictim(current, accessX, accessY);
            testBlockerUnitId = blockerUnitId;
            testBlockerUnitGlobalId = blocker.UnitGlobalId;
            testBlockerOriginalX = blocker.CurrentX;
            testBlockerOriginalY = blocker.CurrentY;
            testBlockerOriginalTargetX = blocker.TargetX;
            testBlockerOriginalTargetY = blocker.TargetY;
            testBlockerStorageBuildingId = blocker.StorageBuildingId;
            testBlockerApproachX = approachX;
            testBlockerApproachY = approachY;
            testBlockerApproachTileId = approachTileId;
            GameUnitManagerAPI.Instance.SetCurrentLocalTilePosition(
                blockerUnitId,
                new UnmanagedVector2<ushort>(approachX, approachY));
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(blockerUnitId, out GameUnit* teleported) ||
                teleported == null || teleported->r_GlobalId != blocker.UnitGlobalId ||
                teleported->r_CurrentTilePositionX != approachX || teleported->r_CurrentTilePositionY != approachY)
            {
                RestoreUnregisteredBlocker();
                ClearTestInjectionIdentity();
                RejectTriggerRoute("SetCurrentLocalTilePosition did not place the selected Fletcher on the approach tile");
                return;
            }

            pendingMoveCapture = true;
            pendingMoveResultSeen = false;
            pendingMoveUnitId = blockerUnitId;
            pendingMoveX = accessX;
            pendingMoveY = accessY;
            pendingMoveResult = 0;
            try
            {
                GameUnitManagerAPI.Instance.MoveToTile(blockerUnitId, accessX, accessY, 0);
            }
            finally
            {
                pendingMoveCapture = false;
            }

            if (!GameUnitManagerAPI.Instance.TryGetUnitById(blockerUnitId, out GameUnit* routedBlocker) ||
                routedBlocker == null || routedBlocker->r_GlobalId != blocker.UnitGlobalId ||
                ((routedBlocker->r_CurrentTilePositionX != accessX ||
                  routedBlocker->r_CurrentTilePositionY != accessY) &&
                 (routedBlocker->r_TargetTilePositionX2 != accessX ||
                  routedBlocker->r_TargetTilePositionY2 != accessY ||
                  routedBlocker->r_PathPlanStateBitFlags == 0)) ||
                (pendingMoveResultSeen && pendingMoveResult == 0))
            {
                string moveResult = pendingMoveResultSeen ? pendingMoveResult.ToString() : "notCaptured";
                RestoreUnregisteredBlocker();
                ClearTestInjectionIdentity();
                RejectTriggerRoute($"Vanilla MoveToTile rejected the Fletcher approach; moveResult={moveResult}");
                return;
            }

            testInjectionPhase = TestInjectionPhase.AwaitingOccupancy;
            LogInfo(
                $"STOCKPILE_TEST_BLOCKER_SPAWNED: victimUnitId={current.UnitId}, victimGlobalId={current.UnitGlobalId}, " +
                $"victimOwnerId={GetUnitOwner(current.UnitId)}, " +
                $"blockerUnitId={blockerUnitId}, blockerGlobalId={blocker.UnitGlobalId}, " +
                $"blockerOwnerId={GetUnitOwner(blockerUnitId)}, " +
                $"blockerWorker={blocker.UnitType}, blockerState={blocker.State}, " +
                $"blockerOriginalPosition={blocker.CurrentX}/{blocker.CurrentY}, " +
                $"blockerOriginalTarget={blocker.TargetX}/{blocker.TargetY}, " +
                $"blockerOriginalStockpile={blocker.StorageBuildingId}, " +
                $"blockerApproach={approachX}/{approachY}, blockerApproachTileId={approachTileId}, " +
                $"cachedAccess={accessX}/{accessY}, tileId={tileId}, priorFlags=0x{unchecked((uint)priorFlags):X8}, " +
                $"blockerMoveResult={(pendingMoveResultSeen ? pendingMoveResult.ToString() : "notCaptured")}, " +
                "mechanism=SetCurrentLocalTilePositionAdjacent+VanillaMoveToTile, directTileMutation=false, " +
                "nextStep=waitForTileUnitIdGridOccupancyThenRetryVictimMoveHere.");
        }

        private void PrepareTestVictim(
            in StockpileObservation victim,
            int accessX,
            int accessY)
        {
            testVictimUnitId = victim.UnitId;
            testVictimUnitGlobalId = victim.UnitGlobalId;
            testBlockerX = accessX;
            testBlockerY = accessY;
            testBlockerSpawnTick = latestSimulationTick;
            testBlockerRecoveryStarted = false;
        }

        private bool TryFindFetchingFletcherBlocker(
            in StockpileObservation victim,
            out int blockerUnitId,
            out StockpileObservation blocker)
        {
            blockerUnitId = 0;
            blocker = default;
            GameUnitManager* manager = GameUnitManagerAPI.Instance.GetUnitManager();
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            if (manager == null || units._array == null)
                return false;

            int victimOwner = GetUnitOwner(victim.UnitId);
            if (victimOwner < 0)
                return false;

            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            int unitCount = checked((int)manager->r_NextUnitId) - 1;
            for (int spanIndex = 0; spanIndex < unitCount; spanIndex++)
            {
                int unitId = spanIndex + 1;
                if (unitId == victim.UnitId)
                    continue;
                GameUnit* unit = units.GetValuePointer(spanIndex);
                if (unit == null || unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_UnitChimp != eChimps.CHIMP_TYPE_FLETCHER)
                {
                    continue;
                }

                StockpileObservation candidate = Capture(unitId, unit);
                if (!IsEligibleFletcherBlocker(victim, candidate) ||
                    unit->r_SpawnedForPlayerIndex != victimOwner)
                {
                    continue;
                }

                // Direct teleport is safe only while the moving unit is absent from the
                // native occupancy grid; otherwise its origin would retain a stale unit ID.
                int originTileId = tileApi.GetTileId(candidate.CurrentX, candidate.CurrentY);
                if (!tileApi.IsValidTileId(originTileId) || tileApi.GetTileUnitId(originTileId) != 0)
                    continue;

                blockerUnitId = unitId;
                blocker = candidate;
                return true;
            }

            return false;
        }

        internal static bool IsEligibleFletcherBlocker(
            in StockpileObservation victim,
            in StockpileObservation candidate) =>
            victim.UnitType == eChimps.CHIMP_TYPE_FLETCHER &&
            candidate.UnitId != victim.UnitId && candidate.UnitGlobalId != 0 &&
            candidate.Alive && candidate.UnitType == eChimps.CHIMP_TYPE_FLETCHER &&
            candidate.State == 1 && candidate.IsValidFetchRoute &&
            candidate.PathFlags != 0 &&
            (candidate.TargetX != victim.TargetX || candidate.TargetY != victim.TargetY) &&
            (candidate.CurrentX != candidate.TargetX || candidate.CurrentY != candidate.TargetY);

        private static bool TryFindFreeBlockerApproach(
            GameTileManagerAPI tileApi,
            in StockpileObservation victim,
            out ushort approachX,
            out ushort approachY,
            out int approachTileId)
        {
            approachX = 0;
            approachY = 0;
            approachTileId = -1;
            for (int radius = 1; radius <= BlockerApproachSearchRadius; radius++)
            {
                for (int offsetY = -radius; offsetY <= radius; offsetY++)
                {
                    for (int offsetX = -radius; offsetX <= radius; offsetX++)
                    {
                        if (Math.Max(Math.Abs(offsetX), Math.Abs(offsetY)) != radius)
                            continue;

                        int x = victim.TargetX + offsetX;
                        int y = victim.TargetY + offsetY;
                        if (!tileApi.IsTileInsideMapBounds(x, y) ||
                            (x == victim.CurrentX && y == victim.CurrentY))
                        {
                            continue;
                        }

                        int tileId = tileApi.GetTileId(x, y);
                        if (!tileApi.IsValidTileId(tileId))
                            continue;

                        TilePropertyFlag flags = tileApi.GetTilePropertyFlag(tileId);
                        ushort buildingId = tileApi.GetTileBuildingId(tileId);
                        ushort unitId = tileApi.GetTileUnitId(tileId);
                        if (!IsSafeBlockerApproachTile(
                                flags,
                                buildingId,
                                unitId,
                                tileApi.IsTileWalkableAndUnoccupied(tileId)))
                        {
                            continue;
                        }

                        approachX = checked((ushort)x);
                        approachY = checked((ushort)y);
                        approachTileId = tileId;
                        return true;
                    }
                }
            }

            return false;
        }

        internal static bool IsSafeBlockerApproachTile(
            TilePropertyFlag flags,
            ushort buildingId,
            ushort unitId,
            bool vanillaWalkable) =>
            buildingId == 0 && unitId == 0 &&
            (flags == TilePropertyFlag.GoodsyardConnection || vanillaWalkable);

        private int GetUnitOwner(int unitId)
        {
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) || unit == null)
                return -1;
            return checked((int)unit->r_SpawnedForPlayerIndex);
        }

        private void TryForceRouteFailureAfterOccupancy()
        {
            int tileId = GameTileManagerAPI.Instance.GetTileId(testBlockerX, testBlockerY);
            ushort occupyingUnitId = GameTileManagerAPI.Instance.GetTileUnitId(tileId);
            if (occupyingUnitId != testBlockerUnitId)
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(testBlockerUnitId, out GameUnit* blocker) ||
                    blocker == null || blocker->r_AliveState != AliveState.IsAlive ||
                    blocker->r_GlobalId != testBlockerUnitGlobalId)
                {
                    TryRestoreTestBlocker("blocker identity changed before native occupancy");
                    RejectTriggerRoute("blocker identity changed before native occupancy");
                    return;
                }
                if (occupyingUnitId == 0 &&
                    (blocker->r_TargetTilePositionX2 != testBlockerX ||
                     blocker->r_TargetTilePositionY2 != testBlockerY ||
                     blocker->r_PathPlanStateBitFlags == 0) &&
                    (blocker->r_CurrentTilePositionX != testBlockerX ||
                     blocker->r_CurrentTilePositionY != testBlockerY))
                {
                    TryRestoreTestBlocker("blocker lost its Vanilla route before native occupancy");
                    RejectTriggerRoute("blocker lost its Vanilla route before native occupancy");
                    return;
                }
                if (latestSimulationTick - testBlockerSpawnTick < BlockerOccupancyTimeoutTicks)
                    return;

                string reason = $"TileUnitIdGrid never reported blockerUnitId={testBlockerUnitId} at " +
                    $"target={testBlockerX}/{testBlockerY}; observedUnitId={occupyingUnitId}";
                TryRestoreTestBlocker("native occupancy timed out");
                RejectTriggerRoute(reason);
                return;
            }

            int formerBlockerUnitId = testBlockerUnitId;
            bool routeFailed = TryForceVictimRouteFailure(
                occupyingUnitId,
                "syntheticFletcherOccupancy");
            bool blockerResumed = ResumeRegisteredBlockerToOriginalTarget(
                "occupancy confirmed and victim route retried");
            if (!routeFailed)
            {
                RejectTriggerRoute("occupied access did not make the victim MoveToTile call fail");
                return;
            }
            if (!blockerResumed)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"STOCKPILE_TEST_BLOCKER_FAILED: blockerUnitId={formerBlockerUnitId}, " +
                    "reason=Vanilla could not confirm the blocker route back to its original target.");
            }
        }

        private bool TryForceVictimRouteFailure(ushort occupyingUnitId, string occupancySource)
        {
            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            int targetTileId = tileApi.GetTileId(testBlockerX, testBlockerY);
            if (!tileApi.IsValidTileId(targetTileId) ||
                tileApi.GetTileUnitId(targetTileId) != occupyingUnitId)
            {
                return false;
            }

            if (!GameUnitManagerAPI.Instance.TryGetUnitById(testVictimUnitId, out GameUnit* victimUnit) ||
                victimUnit == null || victimUnit->r_GlobalId != testVictimUnitGlobalId)
            {
                return false;
            }

            StockpileObservation before = Capture(testVictimUnitId, victimUnit);
            if (!before.IsValidFetchRoute || before.UnitGlobalId != testVictimUnitGlobalId ||
                before.TargetX != testBlockerX || before.TargetY != testBlockerY ||
                (before.CurrentX == before.TargetX && before.CurrentY == before.TargetY))
            {
                return false;
            }

            pendingMoveCapture = true;
            pendingMoveResultSeen = false;
            pendingMoveUnitId = testVictimUnitId;
            pendingMoveX = testBlockerX;
            pendingMoveY = testBlockerY;
            pendingMoveResult = 0;
            try
            {
                GameUnitManagerAPI.Instance.MoveToTile(testVictimUnitId, testBlockerX, testBlockerY, 0);
            }
            finally
            {
                pendingMoveCapture = false;
            }

            StockpileObservation after = Capture(testVictimUnitId, victimUnit);
            bool routeFailed = after.PathFlags == 0 &&
                (!pendingMoveResultSeen || pendingMoveResult == 0);
            LogInfo(
                $"STOCKPILE_TEST_OCCUPANCY_CONFIRMED: tick={latestSimulationTick}, " +
                $"victimUnitId={testVictimUnitId}, occupantUnitId={occupyingUnitId}, " +
                $"target={testBlockerX}/{testBlockerY}, tileUnitId={occupyingUnitId}, " +
                $"occupancySource={occupancySource}, " +
                $"moveResult={(pendingMoveResultSeen ? pendingMoveResult.ToString() : "notCaptured")}, " +
                $"pathFlagsAfter={after.PathFlags}, routeFailureCreated={routeFailed}.");
            if (!routeFailed)
                return false;

            automaticTestCompleted = true;
            hasTriggerRoute = false;
            testBlockerSpawnTick = latestSimulationTick;
            testBlockerRecoveryStarted = false;
            LogInfo(
                $"STOCKPILE_TEST_FAULT_INJECTED: tick={latestSimulationTick}, " +
                $"victimUnitId={testVictimUnitId}, victimGlobalId={testVictimUnitGlobalId}, " +
                $"occupantUnitId={occupyingUnitId}, target={testBlockerX}/{testBlockerY}, " +
                $"occupancySource={occupancySource}, " +
                "reason=vanillaMoveHereRejectedDynamicallyOccupiedStockpileAccess.");
            return true;
        }

        internal static bool IsFreeStockpileConnectionTile(
            TilePropertyFlag flags,
            ushort buildingId,
            ushort unitId) =>
            buildingId == 0 && unitId == 0 &&
            flags == TilePropertyFlag.GoodsyardConnection;

        internal static bool IsStockpileConnectionTile(
            TilePropertyFlag flags,
            ushort buildingId) =>
            buildingId == 0 && flags == TilePropertyFlag.GoodsyardConnection;

        private static bool TryInspectStockpileConnectionTile(
            GameTileManagerAPI tileApi,
            int x,
            int y,
            out int tileId,
            out TilePropertyFlag flags,
            out ushort buildingId,
            out ushort unitId)
        {
            tileId = -1;
            flags = TilePropertyFlag.None;
            buildingId = 0;
            unitId = 0;
            if (!tileApi.IsTileInsideMapBounds(x, y))
                return false;

            tileId = tileApi.GetTileId(x, y);
            if (!tileApi.IsValidTileId(tileId))
                return false;
            buildingId = tileApi.GetTileBuildingId(tileId);
            unitId = tileApi.GetTileUnitId(tileId);
            flags = tileApi.GetTilePropertyFlag(tileId);
            return IsStockpileConnectionTile(flags, buildingId);
        }

        private static bool TryValidateLivingOccupant(
            int unitId,
            int expectedX,
            int expectedY,
            out uint globalId)
        {
            globalId = 0;
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                unit == null || unit->r_AliveState != AliveState.IsAlive ||
                unit->r_CurrentTilePositionX != expectedX ||
                unit->r_CurrentTilePositionY != expectedY)
            {
                return false;
            }

            globalId = unit->r_GlobalId;
            return globalId != 0;
        }

        internal void TryRestoreTestBlocker(string reason)
        {
            if (testBlockerUnitId <= 0)
                return;

            if (testInjectionPhase == TestInjectionPhase.AwaitingOccupancy)
            {
                // Once MoveToTile owns the blocker, only another native move may safely clear
                // any occupancy it has already registered along the route.
                ResumeRegisteredBlockerToOriginalTarget(reason);
                return;
            }

            bool restored = RestoreUnregisteredBlocker();
            LogInfo(
                $"STOCKPILE_TEST_BLOCKER_REMOVED: blockerUnitId={testBlockerUnitId}, " +
                $"blockerGlobalId={testBlockerUnitGlobalId}, restored={restored}, " +
                $"disposition=unregisteredApproachTeleportRolledBack, requestedBy={reason}.");
            ClearBlockerIdentity();
        }

        private bool RestoreUnregisteredBlocker()
        {
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(testBlockerUnitId, out GameUnit* blocker) ||
                blocker == null || blocker->r_AliveState != AliveState.IsAlive ||
                blocker->r_GlobalId != testBlockerUnitGlobalId)
            {
                return false;
            }

            ushort stateBeforeRestore = blocker->r_AIState;
            GameUnitManagerAPI.Instance.SetCurrentLocalTilePosition(
                testBlockerUnitId,
                new UnmanagedVector2<ushort>(testBlockerOriginalX, testBlockerOriginalY));
            if (stateBeforeRestore == 1 && testBlockerOriginalTargetX != 0 && testBlockerOriginalTargetY != 0)
            {
                GameUnitManagerAPI.Instance.MoveToTile(
                    testBlockerUnitId,
                    testBlockerOriginalTargetX,
                    testBlockerOriginalTargetY,
                    0);
            }

            return blocker->r_CurrentTilePositionX == testBlockerOriginalX &&
                blocker->r_CurrentTilePositionY == testBlockerOriginalY;
        }

        private bool ResumeRegisteredBlockerToOriginalTarget(string reason)
        {
            bool routeAccepted = false;
            string disposition;
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(testBlockerUnitId, out GameUnit* blocker) ||
                blocker == null || blocker->r_AliveState != AliveState.IsAlive ||
                blocker->r_GlobalId != testBlockerUnitGlobalId)
            {
                disposition = "blockerIdentityChanged";
            }
            else if (blocker->r_AIState != 1 ||
                ReadUInt16(blocker, StockpileAccessFixNativeDefinition.UnitStorageBuildingIdOffset) !=
                    testBlockerStorageBuildingId ||
                testBlockerOriginalTargetX == 0 || testBlockerOriginalTargetY == 0)
            {
                disposition = $"blockerStateOrOriginalTargetChanged:state={blocker->r_AIState}";
            }
            else
            {
                pendingMoveCapture = true;
                pendingMoveResultSeen = false;
                pendingMoveUnitId = testBlockerUnitId;
                pendingMoveX = testBlockerOriginalTargetX;
                pendingMoveY = testBlockerOriginalTargetY;
                pendingMoveResult = 0;
                try
                {
                    GameUnitManagerAPI.Instance.MoveToTile(
                        testBlockerUnitId,
                        testBlockerOriginalTargetX,
                        testBlockerOriginalTargetY,
                        0);
                }
                finally
                {
                    pendingMoveCapture = false;
                }

                routeAccepted =
                    (!pendingMoveResultSeen || pendingMoveResult != 0) &&
                    ((blocker->r_CurrentTilePositionX == testBlockerOriginalTargetX &&
                      blocker->r_CurrentTilePositionY == testBlockerOriginalTargetY) ||
                     (blocker->r_TargetTilePositionX2 == testBlockerOriginalTargetX &&
                      blocker->r_TargetTilePositionY2 == testBlockerOriginalTargetY &&
                      blocker->r_PathPlanStateBitFlags != 0));
                disposition = routeAccepted
                    ? "VanillaMoveToOriginalTargetAccepted"
                    : "VanillaMoveToOriginalTargetRejected";
            }

            LogInfo(
                $"STOCKPILE_TEST_BLOCKER_REMOVED: blockerUnitId={testBlockerUnitId}, " +
                $"blockerGlobalId={testBlockerUnitGlobalId}, restored={routeAccepted}, " +
                $"disposition={disposition}, originalTarget={testBlockerOriginalTargetX}/{testBlockerOriginalTargetY}, " +
                $"approach={testBlockerApproachX}/{testBlockerApproachY}, " +
                $"approachTileId={testBlockerApproachTileId}, requestedBy={reason}.");
            ClearBlockerIdentity();
            return routeAccepted;
        }

        private void RejectTriggerRoute(string reason)
        {
            nextAutomaticTriggerTick = checked(latestSimulationTick + AutomaticTriggerRetryTicks);
            hasTriggerRoute = false;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"STOCKPILE_TEST_BLOCKER_FAILED: tick={latestSimulationTick}, reason={reason}, " +
                $"retryAtTick={nextAutomaticTriggerTick}.");
        }

        private void ObserveAutomaticTestTimeout(int tick)
        {
            if (!automaticTestCompleted || testVictimUnitId <= 0 || testBlockerRecoveryStarted ||
                tick - testBlockerSpawnTick < AutomaticNoBugTimeoutTicks)
            {
                return;
            }

            LogInfo(
                $"STOCKPILE_TEST_AUTOMATION_RESULT: outcome=noStableBugSignature, tick={tick}, " +
                $"unitId={testVictimUnitId}, globalId={testVictimUnitGlobalId}, " +
                $"target={testBlockerX}/{testBlockerY}, " +
                $"timeoutTicks={AutomaticNoBugTimeoutTicks}.");
            ClearTestInjectionIdentity();
        }

        private void CompleteAutomaticTestIfMatching(
            in StockpileObservation observation,
            string outcome)
        {
            if (!automaticTestCompleted || observation.UnitId != testVictimUnitId)
                return;

            LogInfo(
                $"STOCKPILE_TEST_AUTOMATION_RESULT: outcome={outcome}, " +
                $"unitId={observation.UnitId}, observedGlobalId={observation.UnitGlobalId}, " +
                $"expectedGlobalId={testVictimUnitGlobalId}, " +
                $"target={testBlockerX}/{testBlockerY}.");
            ClearTestInjectionIdentity();
        }

        private bool IsTestVictimWorker(in StockpileObservation observation) =>
            automaticTestCompleted && observation.UnitId == testVictimUnitId &&
            observation.UnitGlobalId == testVictimUnitGlobalId;

        private void ClearBlockerIdentity()
        {
            testBlockerUnitId = 0;
            testBlockerUnitGlobalId = 0;
            testBlockerOriginalX = 0;
            testBlockerOriginalY = 0;
            testBlockerOriginalTargetX = 0;
            testBlockerOriginalTargetY = 0;
            testBlockerStorageBuildingId = 0;
            testBlockerApproachX = 0;
            testBlockerApproachY = 0;
            testBlockerApproachTileId = 0;
            testInjectionPhase = TestInjectionPhase.None;
        }

        private void ClearTestInjectionIdentity()
        {
            ClearBlockerIdentity();
            testVictimUnitId = 0;
            testVictimUnitGlobalId = 0;
            testBlockerX = 0;
            testBlockerY = 0;
            testBlockerSpawnTick = 0;
            testBlockerRecoveryStarted = false;
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
            $"pathFlags={observation.PathFlags}, alternatePathConnectionId={observation.AlternatePathConnectionId}, " +
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
            nextAutomaticTriggerTick = 0;
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

        private readonly struct TrackedRoute
        {
            internal TrackedRoute(RouteSignature signature, int lastActiveTick)
            {
                Signature = signature;
                LastActiveTick = lastActiveTick;
            }

            internal RouteSignature Signature { get; }
            internal int LastActiveTick { get; }
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

        private enum TestInjectionPhase
        {
            None,
            AwaitingOccupancy
        }
    }
}
