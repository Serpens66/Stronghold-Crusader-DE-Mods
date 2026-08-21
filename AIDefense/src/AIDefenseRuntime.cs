using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIDefense
{
    internal sealed unsafe class AIDefenseRuntime : IDisposable
    {
        private const int InitialScanDelayTicks = 20;
        private const int ScanIntervalTicks = 250;
        private const int SummaryLogIntervalTicks = 1000;
        private const int MaximumBuildingOccupiedTiles = 36;

        // The Script Extender currently names this field r_AITribeRole, but diagnostics and the
        // original game's reverse engineering identify it as the per-unit AI behaviour type.
        // Known behaviour buckets use values 0 through 22. A signed -1 sentinel keeps protected
        // defenders outside those counters and, unlike 0, does not mark them as unclassified.
        private const short ProtectedAIBehaviourTypeValue = -1;
        private const AITribeRole16 ProtectedAIBehaviourType = (AITribeRole16)ProtectedAIBehaviourTypeValue;
        private const ushort ProtectedAIBehaviourRelatedValue = 0;

        private const eChimps DefenderType = eChimps.CHIMP_TYPE_ARCHER;

        private static readonly HashSet<eStructs> TowerTypes = new HashSet<eStructs>
        {
            eStructs.STRUCT_TOWER,
            eStructs.STRUCT_TOWER1,
            eStructs.STRUCT_TOWER2,
            eStructs.STRUCT_TOWER3,
            eStructs.STRUCT_TOWER4,
            eStructs.STRUCT_TOWER5,
        };

        private static readonly HashSet<eChimps> RangedDefenderTypes = new HashSet<eChimps>
        {
            eChimps.CHIMP_TYPE_ARCHER,
            eChimps.CHIMP_TYPE_XBOWMAN,
            eChimps.CHIMP_TYPE_ARAB_BOW,
            eChimps.CHIMP_TYPE_ARAB_SLINGER,
            eChimps.CHIMP_TYPE_ARAB_GRENADIER,
            eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER,
            eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER,
        };

        private readonly ManualLogSource log;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly Dictionary<int, ProtectedDefender> protectedByUnitId = new Dictionary<int, ProtectedDefender>();
        private readonly Dictionary<uint, ProtectedDefender> protectedByTowerGlobalId = new Dictionary<uint, ProtectedDefender>();
        private readonly Dictionary<int, ProtectedDefender> protectedByPrivateTribeId = new Dictionary<int, ProtectedDefender>();
        private readonly HashSet<uint> loggedSpawnFailureTowerGlobals = new HashSet<uint>();

        private bool applied;
        private bool mapActive;
        private bool editorBypassActive;
        private bool firstScanPending;
        private int nextScanTick;
        private int nextSummaryLogTick;
        private int permittedAssignmentUnitId;
        private int permittedAssignmentTribeId;
        private long totalBlockedTribeAssignments;
        private long totalBlockedMoveOrders;
        private long totalBlockedPrivateTribeOrders;
        private long totalAllowedTowerMoveOrders;
        private long totalAIBehaviourRepairs;
        private long totalPrivateTribeCreations;
        private long totalPrivateTribeFailures;

        public AIDefenseRuntime(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public void Apply()
        {
            if (applied)
                return;

            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnStartMap));

            subscriptions.Add(MapLoaderR3EventHooks.OnLoadSave.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnLoadSave));

            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnUnloadMap));

            subscriptions.Add(TribeR3EventHooks.OnTribeAssignUnit.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnTribeAssignUnit));

            subscriptions.Add(TribeR3EventHooks.OnTribeIssueOrderMoveHere.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnTribeIssueOrderMoveHere));

            subscriptions.Add(TribeR3EventHooks.OnTribeIssueOrderWithTarget.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnTribeIssueOrderWithTarget));

            subscriptions.Add(TribeR3EventHooks.OnTribeDelete.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnTribeDelete));

            subscriptions.Add(UnitR3EventHooks.OnUnitMoveHere.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnUnitMoveHere));

            subscriptions.Add(UnitR3EventHooks.OnUnitDelete.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnUnitDelete));

            GameTimeManagerAPI.Instance.OnTick += OnGameTick;

            applied = true;
            LogInfo(
                $"AI Defense hooks subscribed: initialScanDelayTicks={InitialScanDelayTicks}, " +
                $"scanIntervalTicks={ScanIntervalTicks}, summaryIntervalTicks={SummaryLogIntervalTicks}, defenderType={DefenderType}, " +
                $"protectedAIBehaviourType={ProtectedAIBehaviourTypeValue}, queryResultsAreOneBasedIds=true, " +
                $"towerLocalMovementAllowed=true.");
        }

        public void Dispose()
        {
            if (!applied)
                return;

            GameTimeManagerAPI.Instance.OnTick -= OnGameTick;

            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();

            subscriptions.Clear();
            ClearTracking();
            mapActive = false;
            editorBypassActive = false;
            applied = false;
        }

        private void OnStartMap(MapStartEventArgs args)
        {
            if (IsMapEditor())
            {
                DisableForMapEditor("OnStartMap reported a map-editor session");
                return;
            }

            BeginMap(
                $"start-map campaignMapId={args.CampaignMapId}, multiplayerSave={args.bMultiplayerSave}");
        }

        private void OnLoadSave(LoadSaveGameEventArgs args)
        {
            if (args.LoadingEditorMap || IsMapEditor())
            {
                DisableForMapEditor(
                    $"editor-map load file={args.FileName ?? "<null>"}, loadingEditorMap={args.LoadingEditorMap}");
                return;
            }

            BeginMap(
                $"load-save file={args.FileName ?? "<null>"}, loadingEditorMap={args.LoadingEditorMap}");
        }

        private void BeginMap(string reason)
        {
            ClearTracking();
            mapActive = true;
            editorBypassActive = false;
            firstScanPending = true;

            int currentTick = GameTimeManagerAPI.Instance.GetFrameProvider().CurrentGameTick;
            nextScanTick = currentTick + InitialScanDelayTicks;
            nextSummaryLogTick = currentTick;

            LogInfo(
                $"Map tracking started: reason={reason}, currentTick={currentTick}, firstScanTick={nextScanTick}.");
        }

        private void OnUnloadMap(MapUnloadEventArgs args)
        {
            if (editorBypassActive)
            {
                LogInfo("Map-editor bypass cleared on map unload.");
            }
            else
            {
                LogInfo(
                    $"Map tracking stopped: protectedDefenders={protectedByUnitId.Count}, " +
                    $"privateTribes={protectedByPrivateTribeId.Count}, blockedTribeAssignments={totalBlockedTribeAssignments}, " +
                    $"blockedPrivateTribeOrders={totalBlockedPrivateTribeOrders}, blockedMoveOrders={totalBlockedMoveOrders}, " +
                    $"allowedTowerMoveOrders={totalAllowedTowerMoveOrders}.");
            }

            mapActive = false;
            editorBypassActive = false;
            ClearTracking();
        }

        private void OnGameTick(int tick)
        {
            if (!IsRuntimeActiveForCurrentContext("game-tick editor detection") || tick < nextScanTick)
                return;

            nextScanTick = tick + ScanIntervalTicks;

            try
            {
                ScanDefenses(tick);
            }
            catch (Exception ex)
            {
                // Stop repeated raw-structure reads after the first incompatible layout symptom.
                mapActive = false;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"AI Defense scan failed and remains inactive for this map: tick={tick}, exception={ex}");
            }
        }

        private void OnTribeAssignUnit(TribeAssignUnitEventArgs args)
        {
            if (args.UnitId == permittedAssignmentUnitId && args.TribeId == permittedAssignmentTribeId)
                return;

            if (!TryGetProtectedDefender(args.UnitId, out ProtectedDefender defender))
                return;

            bool behaviourRepaired = false;
            ushort behaviourRelatedBefore = 0;
            short behaviourTypeBefore = 0;
            int currentTribeId = 0;
            if (GameUnitManagerAPI.Instance.TryGetUnitById(args.UnitId, out GameUnit* unit) && unit != null)
            {
                behaviourRelatedBefore = unit->r_AITribeRoleRelatedUnknown;
                behaviourTypeBefore = (short)unit->r_AITribeRole;
                currentTribeId = unit->r_TribeId;
                behaviourRepaired = EnsureProtectedAIBehaviour(defender, unit, "unexpected tribe assignment");
            }

            args.SkipOriginalFunction = true;
            args.ReturnValue = 0;
            defender.BlockedTribeAssignments++;
            totalBlockedTribeAssignments++;

            if (ShouldLogRepeatedAssignment(defender.BlockedTribeAssignments))
            {
                LogInfo(
                    $"Blocked unexpected tribe assignment: unitId={args.UnitId}, unitGlobalId={defender.UnitGlobalId}, " +
                    $"towerGlobalId={defender.TowerGlobalId}, tribeId={args.TribeId}, unitBlockCount={defender.BlockedTribeAssignments}, " +
                    $"totalBlockCount={totalBlockedTribeAssignments}, currentTribeId={currentTribeId}, " +
                    $"aiBehaviourRelatedBefore={behaviourRelatedBefore}, aiBehaviourTypeBefore={behaviourTypeBefore}, " +
                    $"behaviourRepaired={behaviourRepaired}, " +
                    $"targetTribe=[{DescribeTribe(args.TribeId)}].");
            }
        }

        private void OnTribeIssueOrderMoveHere(TribeIssueOrderMoveHereEventArgs args)
        {
            if (!TryGetProtectedDefenderByPrivateTribe(args.TribeId, out ProtectedDefender defender))
                return;

            if (IsMovementTargetOnTower(defender, args.TileX, args.TileY, out int targetTileId))
            {
                RecordAllowedTowerMove(
                    defender,
                    "private tribe",
                    args.TileX,
                    args.TileY,
                    targetTileId,
                    $"patrol={args.IsPatrolPath}, moveType={args.MoveType}");
                return;
            }

            args.SkipOriginalFunction = true;
            args.ReturnValue = 0;
            defender.BlockedPrivateTribeOrders++;
            totalBlockedPrivateTribeOrders++;

            if (ShouldLogBlockedOrder(defender.BlockedPrivateTribeOrders))
            {
                LogInfo(
                    $"Blocked private tribe movement order: tribeId={args.TribeId}, tribeGlobalId={defender.PrivateTribeGlobalId}, " +
                    $"unitId={defender.UnitId}, unitGlobalId={defender.UnitGlobalId}, towerGlobalId={defender.TowerGlobalId}, " +
                    $"target={args.TileX},{args.TileY}, patrol={args.IsPatrolPath}, moveType={args.MoveType}, " +
                    $"tribeBlockCount={defender.BlockedPrivateTribeOrders}, totalBlockCount={totalBlockedPrivateTribeOrders}.");
            }
        }

        private void OnTribeIssueOrderWithTarget(TribeIssueOrderWithTargetEventArgs args)
        {
            if (!TryGetProtectedDefenderByPrivateTribe(args.TribeId, out ProtectedDefender defender))
                return;

            args.SkipOriginalFunction = true;
            args.ReturnValue = 0;
            defender.BlockedPrivateTribeOrders++;
            totalBlockedPrivateTribeOrders++;

            if (ShouldLogBlockedOrder(defender.BlockedPrivateTribeOrders))
            {
                LogInfo(
                    $"Blocked private tribe target order: tribeId={args.TribeId}, tribeGlobalId={defender.PrivateTribeGlobalId}, " +
                    $"unitId={defender.UnitId}, unitGlobalId={defender.UnitGlobalId}, towerGlobalId={defender.TowerGlobalId}, " +
                    $"command={args.AICommand}, target1={args.TargetValue1}, target2={args.TargetValue2}, arg6={args.a6}, " +
                    $"tribeBlockCount={defender.BlockedPrivateTribeOrders}, totalBlockCount={totalBlockedPrivateTribeOrders}.");
            }
        }

        private void OnTribeDelete(TribeDeleteEventArgs args)
        {
            if (!TryGetProtectedDefenderByPrivateTribe(args.TribeId, out ProtectedDefender defender))
                return;

            uint deletedTribeGlobalId = defender.PrivateTribeGlobalId;
            ClearPrivateTribeTracking(defender);
            LogInfo(
                $"Private defender tribe is being deleted: tribeId={args.TribeId}, tribeGlobalId={deletedTribeGlobalId}, " +
                $"unitId={defender.UnitId}, unitGlobalId={defender.UnitGlobalId}, towerGlobalId={defender.TowerGlobalId}; " +
                $"a replacement tribe will be created by the next defense scan.");
        }

        private void OnUnitMoveHere(UnitMoveHereEventArgs args)
        {
            if (!TryGetProtectedDefender(args.UnitId, out ProtectedDefender defender))
                return;

            if (IsMovementTargetOnTower(defender, args.TileX, args.TileY, out int targetTileId))
            {
                RecordAllowedTowerMove(
                    defender,
                    "direct unit",
                    args.TileX,
                    args.TileY,
                    targetTileId,
                    $"unknown={args.Unknown}");
                return;
            }

            args.SkipOriginalFunction = true;
            args.ReturnValue = 0;
            defender.BlockedMoveOrders++;
            totalBlockedMoveOrders++;

            if (ShouldLogBlockedOrder(defender.BlockedMoveOrders))
            {
                LogInfo(
                    $"Blocked direct movement order: unitId={args.UnitId}, unitGlobalId={defender.UnitGlobalId}, " +
                    $"towerGlobalId={defender.TowerGlobalId}, target={args.TileX},{args.TileY}, unknown={args.Unknown}, " +
                    $"unitBlockCount={defender.BlockedMoveOrders}, totalBlockCount={totalBlockedMoveOrders}.");
            }
        }

        private void OnUnitDelete(UnitDeleteEventArgs args)
        {
            if (!IsRuntimeActiveForCurrentContext("unit-delete editor detection"))
                return;

            int unitId = unchecked((int)args.UnitId);
            if (!protectedByUnitId.TryGetValue(unitId, out ProtectedDefender defender))
                return;

            int privateTribeId = defender.PrivateTribeId;
            uint privateTribeGlobalId = defender.PrivateTribeGlobalId;
            RemoveProtectedDefender(defender);
            LogInfo(
                $"Protected tower defender is being deleted: unitId={unitId}, unitGlobalId={defender.UnitGlobalId}, " +
                $"towerGlobalId={defender.TowerGlobalId}, privateTribeId={privateTribeId}, " +
                $"privateTribeGlobalId={privateTribeGlobalId}; a replacement will be considered by the next defense scan.");
        }

        private void ScanDefenses(int tick)
        {
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;

            List<int> aliveUnitIds = new List<int>();
            unitApi.GetAllUnits(
                aliveUnitIds,
                AliveState.IsAlive,
                relationship: PlayerRelationship.Any,
                povPlayerId: null);

            HashSet<uint> occupiedUnitTileIds = new HashSet<uint>();
            Dictionary<uint, List<int>> rangedUnitsByTileId = new Dictionary<uint, List<int>>();

            foreach (int unitId in aliveUnitIds)
            {
                if (!unitApi.TryGetUnitById(unitId, out GameUnit* unit) || unit == null || unit->r_AliveState != AliveState.IsAlive)
                    continue;

                uint tileId = unit->r_CurrentPositionTileId;
                occupiedUnitTileIds.Add(tileId);

                if (!RangedDefenderTypes.Contains(unit->r_UnitChimp))
                    continue;

                if (!rangedUnitsByTileId.TryGetValue(tileId, out List<int> rangedUnitIds))
                {
                    rangedUnitIds = new List<int>();
                    rangedUnitsByTileId[tileId] = rangedUnitIds;
                }

                rangedUnitIds.Add(unitId);
            }

            int[] aliveBuildingIds = buildingApi.GetAllAliveBuildings();
            HashSet<uint> liveAITowerGlobals = new HashSet<uint>();

            int towersFound = 0;
            int aiTowersFound = 0;
            int alreadyGarrisoned = 0;
            int protectedGarrisons = 0;
            int spawned = 0;
            int spawnFailures = 0;
            bool newFailureLogged = false;

            foreach (int buildingId in aliveBuildingIds)
            {
                if (!buildingApi.TryGetBuildingById(buildingId, out GameBuilding* tower) ||
                    tower == null ||
                    tower->r_AliveState != AliveState.IsAlive ||
                    !TowerTypes.Contains(tower->r_BuildingType))
                {
                    continue;
                }

                towersFound++;

                int ownerPlayerId = tower->r_PlayerIdOwner;
                if (!playerApi.IsPlayerIdValid(ownerPlayerId) || !playerApi.IsAIPlayer(ownerPlayerId))
                    continue;

                aiTowersFound++;
                uint towerGlobalId = tower->r_GlobalId;
                if (towerGlobalId == 0)
                {
                    spawnFailures++;
                    LogInfo(
                        $"AI tower ignored because it has no global id: buildingId={buildingId}, owner={ownerPlayerId}, " +
                        $"type={tower->r_BuildingType}.");
                    continue;
                }

                liveAITowerGlobals.Add(towerGlobalId);
                List<uint> towerTileIds = GetTowerTileIds(tower, tileApi);
                if (towerTileIds.Count == 0)
                {
                    spawnFailures++;
                    if (loggedSpawnFailureTowerGlobals.Add(towerGlobalId))
                    {
                        newFailureLogged = true;
                        LogInfo(
                            $"AI tower has no valid occupied tiles: buildingId={buildingId}, towerGlobalId={towerGlobalId}, " +
                            $"owner={ownerPlayerId}, type={tower->r_BuildingType}, occupyGridSize={tower->r_OccupyTileGridSize}.");
                    }
                    continue;
                }

                if (protectedByTowerGlobalId.TryGetValue(towerGlobalId, out ProtectedDefender protectedDefender))
                {
                    if (IsProtectedDefenderValidForTower(protectedDefender, ownerPlayerId, towerTileIds))
                    {
                        alreadyGarrisoned++;
                        protectedGarrisons++;
                        loggedSpawnFailureTowerGlobals.Remove(towerGlobalId);
                        continue;
                    }

                    ReleaseProtectedDefender(
                        protectedDefender,
                        "unit is no longer alive, owned by the tower player, or positioned on this tower");
                }

                if (HasFriendlyRangedDefender(ownerPlayerId, towerTileIds, rangedUnitsByTileId))
                {
                    alreadyGarrisoned++;
                    loggedSpawnFailureTowerGlobals.Remove(towerGlobalId);
                    continue;
                }

                if (TrySpawnProtectedDefender(
                    buildingId,
                    tower,
                    towerTileIds,
                    occupiedUnitTileIds,
                    out int spawnedUnitId,
                    out uint spawnTileId,
                    out string failureReason))
                {
                    spawned++;
                    occupiedUnitTileIds.Add(spawnTileId);
                    if (!rangedUnitsByTileId.TryGetValue(spawnTileId, out List<int> spawnedTileRangedUnits))
                    {
                        spawnedTileRangedUnits = new List<int>();
                        rangedUnitsByTileId[spawnTileId] = spawnedTileRangedUnits;
                    }

                    spawnedTileRangedUnits.Add(spawnedUnitId);
                    loggedSpawnFailureTowerGlobals.Remove(towerGlobalId);
                }
                else
                {
                    spawnFailures++;
                    if (loggedSpawnFailureTowerGlobals.Add(towerGlobalId))
                    {
                        newFailureLogged = true;
                        LogInfo(
                            $"Could not garrison AI tower: buildingId={buildingId}, towerGlobalId={towerGlobalId}, " +
                            $"owner={ownerPlayerId}, type={tower->r_BuildingType}, reason={failureReason}.");
                    }
                }
            }

            ReleaseDefendersOfMissingTowers(liveAITowerGlobals);

            bool logSummary = firstScanPending || spawned > 0 || newFailureLogged || tick >= nextSummaryLogTick;
            if (logSummary)
            {
                LogInfo(
                    $"Defense scan complete: tick={tick}, aliveUnits={aliveUnitIds.Count}, rangedTiles={rangedUnitsByTileId.Count}, " +
                    $"towers={towersFound}, aiTowers={aiTowersFound}, garrisoned={alreadyGarrisoned}, " +
                    $"protectedGarrisons={protectedGarrisons}, spawned={spawned}, spawnFailures={spawnFailures}, " +
                    $"protectedDefenders={protectedByUnitId.Count}, privateTribes={protectedByPrivateTribeId.Count}, " +
                    $"privateTribeCreations={totalPrivateTribeCreations}, privateTribeFailures={totalPrivateTribeFailures}, " +
                    $"blockedTribeAssignments={totalBlockedTribeAssignments}, blockedPrivateTribeOrders={totalBlockedPrivateTribeOrders}, " +
                    $"blockedMoveOrders={totalBlockedMoveOrders}, allowedTowerMoveOrders={totalAllowedTowerMoveOrders}, " +
                    $"aiBehaviourRepairs={totalAIBehaviourRepairs}.");

                LogAIBehaviourDiagnostics(tick, aliveUnitIds);

                nextSummaryLogTick = tick + SummaryLogIntervalTicks;
            }

            firstScanPending = false;
        }

        private void LogAIBehaviourDiagnostics(int tick, List<int> aliveUnitIds)
        {
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            Dictionary<int, AIBehaviourPlayerDiagnostic> diagnosticsByPlayer = new Dictionary<int, AIBehaviourPlayerDiagnostic>();

            foreach (int unitId in aliveUnitIds)
            {
                if (!unitApi.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null ||
                    unit->r_AliveState != AliveState.IsAlive ||
                    !IsDiagnosticCombatUnit(unit->r_UnitChimp))
                {
                    continue;
                }

                int ownerPlayerId = unit->r_ControllableForPlayerId;
                if (!playerApi.IsPlayerIdValid(ownerPlayerId) || !playerApi.IsAIPlayer(ownerPlayerId))
                    continue;

                if (!diagnosticsByPlayer.TryGetValue(ownerPlayerId, out AIBehaviourPlayerDiagnostic playerDiagnostic))
                {
                    playerDiagnostic = new AIBehaviourPlayerDiagnostic(ownerPlayerId);
                    diagnosticsByPlayer[ownerPlayerId] = playerDiagnostic;
                }

                bool isProtected = protectedByUnitId.ContainsKey(unitId);
                playerDiagnostic.Add(unit, isProtected);
            }

            List<int> playerIds = new List<int>(diagnosticsByPlayer.Keys);
            playerIds.Sort();
            foreach (int playerId in playerIds)
            {
                AIBehaviourPlayerDiagnostic diagnostic = diagnosticsByPlayer[playerId];
                string totalArmy = "<unavailable>";
                if (playerApi.TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources) && resources != null)
                    totalArmy = resources->r_TotalArmy.ToString();

                LogInfo(
                    $"AI behaviour diagnostic summary: tick={tick}, owner={playerId}, totalArmy={totalArmy}, " +
                    $"combatUnits={diagnostic.TotalUnits}, tribeAssigned={diagnostic.TribeAssignedUnits}, " +
                    $"behaviourTypeZero={diagnostic.BehaviourTypeZeroUnits}, protected={diagnostic.ProtectedUnits}, " +
                    $"behaviourBuckets={diagnostic.Buckets.Count}.");

                List<AIBehaviourDiagnosticBucket> buckets = new List<AIBehaviourDiagnosticBucket>(diagnostic.Buckets.Values);
                buckets.Sort(CompareDiagnosticBuckets);
                foreach (AIBehaviourDiagnosticBucket bucket in buckets)
                {
                    LogInfo(
                        $"AI behaviour diagnostic bucket: tick={tick}, owner={playerId}, behaviourType={bucket.BehaviourType}, " +
                        $"related={bucket.Related}, tribeAssigned={bucket.TribeAssigned}, protected={bucket.Protected}, count={bucket.Count}, " +
                        $"types=[{FormatTypeCounts(bucket.TypeCounts)}], tribeIds=[{FormatIntValues(bucket.TribeIds)}], " +
                        $"raw428=[{FormatUIntValues(bucket.Raw428Values)}], raw42C=[{FormatUIntValues(bucket.Raw42CValues)}], " +
                        $"raw430=[{FormatUShortValues(bucket.Raw430Values)}], raw432=[{FormatUShortValues(bucket.Raw432Values)}].");
                }
            }
        }

        private static int CompareDiagnosticBuckets(AIBehaviourDiagnosticBucket left, AIBehaviourDiagnosticBucket right)
        {
            int comparison = left.BehaviourType.CompareTo(right.BehaviourType);
            if (comparison != 0)
                return comparison;

            comparison = left.Related.CompareTo(right.Related);
            if (comparison != 0)
                return comparison;

            comparison = left.Protected.CompareTo(right.Protected);
            if (comparison != 0)
                return comparison;

            return left.TribeAssigned.CompareTo(right.TribeAssigned);
        }

        private static string FormatTypeCounts(Dictionary<eChimps, int> typeCounts)
        {
            List<eChimps> types = new List<eChimps>(typeCounts.Keys);
            types.Sort((left, right) => ((int)left).CompareTo((int)right));
            StringBuilder result = new StringBuilder();

            foreach (eChimps type in types)
            {
                if (result.Length > 0)
                    result.Append(',');

                string typeName = type.ToString();
                if (typeName.StartsWith("CHIMP_TYPE_", StringComparison.Ordinal))
                    typeName = typeName.Substring("CHIMP_TYPE_".Length);

                result.Append(typeName);
                result.Append(':');
                result.Append(typeCounts[type]);
            }

            return result.ToString();
        }

        private static string FormatIntValues(HashSet<int> values)
        {
            List<int> sorted = new List<int>(values);
            sorted.Sort();
            return FormatLimitedValues(sorted, value => value.ToString());
        }

        private static string FormatUIntValues(HashSet<uint> values)
        {
            List<uint> sorted = new List<uint>(values);
            sorted.Sort();
            return FormatLimitedValues(sorted, value => $"0x{value:X8}");
        }

        private static string FormatUShortValues(HashSet<ushort> values)
        {
            List<ushort> sorted = new List<ushort>(values);
            sorted.Sort();
            return FormatLimitedValues(sorted, value => $"0x{value:X4}");
        }

        private static string FormatLimitedValues<T>(List<T> values, Func<T, string> formatter)
        {
            const int maximumValues = 8;
            StringBuilder result = new StringBuilder();
            int valuesToWrite = Math.Min(values.Count, maximumValues);

            for (int i = 0; i < valuesToWrite; i++)
            {
                if (i > 0)
                    result.Append(',');

                result.Append(formatter(values[i]));
            }

            if (values.Count > maximumValues)
            {
                result.Append(",+");
                result.Append(values.Count - maximumValues);
                result.Append(" more");
            }

            return result.ToString();
        }

        private static bool IsDiagnosticCombatUnit(eChimps type)
        {
            switch (type)
            {
                case eChimps.CHIMP_TYPE_TUNNELER:
                case eChimps.CHIMP_TYPE_ARCHER:
                case eChimps.CHIMP_TYPE_XBOWMAN:
                case eChimps.CHIMP_TYPE_SPEARMAN:
                case eChimps.CHIMP_TYPE_PIKEMAN:
                case eChimps.CHIMP_TYPE_MACEMAN:
                case eChimps.CHIMP_TYPE_SWORDSMAN:
                case eChimps.CHIMP_TYPE_KNIGHT:
                case eChimps.CHIMP_TYPE_LADDERMAN:
                case eChimps.CHIMP_TYPE_ENGINEER:
                case eChimps.CHIMP_TYPE_MONK:
                case eChimps.CHIMP_TYPE_ARCHER_debug:
                case eChimps.CHIMP_TYPE_CATAPULT:
                case eChimps.CHIMP_TYPE_TREBUCHET:
                case eChimps.CHIMP_TYPE_MANGONEL:
                case eChimps.CHIMP_TYPE_FIREMAN:
                case eChimps.CHIMP_TYPE_SIEGE_TOWER:
                case eChimps.CHIMP_TYPE_BATTERING_RAM:
                case eChimps.CHIMP_TYPE_PORTABLE_SHIELD:
                case eChimps.CHIMP_TYPE_BALLISTA:
                case eChimps.CHIMP_TYPE_WAR_DOG:
                case eChimps.CHIMP_TYPE_ARAB_BOW:
                case eChimps.CHIMP_TYPE_ARAB_SLAVE:
                case eChimps.CHIMP_TYPE_ARAB_SLINGER:
                case eChimps.CHIMP_TYPE_ARAB_ASSASIN:
                case eChimps.CHIMP_TYPE_ARAB_HORSEMAN:
                case eChimps.CHIMP_TYPE_ARAB_SWORDSMAN:
                case eChimps.CHIMP_TYPE_ARAB_GRENADIER:
                case eChimps.CHIMP_TYPE_ARAB_BALLISTA:
                case eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER:
                case eChimps.CHIMP_TYPE_BEDOUIN_HEALER:
                case eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH:
                case eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER:
                case eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER:
                case eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL:
                case eChimps.CHIMP_TYPE_BEDOUIN_SAPPER:
                case eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER:
                    return true;
                default:
                    return false;
            }
        }

        private static string DescribeTribe(int tribeId)
        {
            GameTribeManagerAPI tribeApi = GameTribeManagerAPI.Instance;
            if (!tribeApi.IsValidId(tribeId) || !tribeApi.TryGetTribeById(tribeId, out GameTribe* tribe) || tribe == null)
                return $"id={tribeId},invalid=true";

            return $"id={tribeId},globalId={tribe->r_GlobalId},owner={tribe->r_PlayerIdOwner},state={tribe->r_AliveState}," +
                $"stance={tribe->r_TribeStance},units={tribe->r_UnitsInGroup},leader={tribe->r_LeaderUnitId}," +
                $"patrolMode={tribe->r_PatrolMode},attackNearest={tribe->r_bAttackNearestUnit}";
        }

        private static List<uint> GetTowerTileIds(GameBuilding* tower, GameTileManagerAPI tileApi)
        {
            int tileCount = Math.Min((int)tower->r_OccupyTileGridSize, MaximumBuildingOccupiedTiles);
            List<uint> tileIds = new List<uint>(tileCount);
            HashSet<uint> seen = new HashSet<uint>();
            uint* occupiedTileIds = &tower->r_OccupiedTileIdsArrayBegin;

            for (int i = 0; i < tileCount; i++)
            {
                uint tileId = occupiedTileIds[i];
                if (tileId > int.MaxValue || !tileApi.IsValidTileId((int)tileId) || !seen.Add(tileId))
                    continue;

                tileIds.Add(tileId);
            }

            return tileIds;
        }

        private static bool HasFriendlyRangedDefender(
            int ownerPlayerId,
            List<uint> towerTileIds,
            Dictionary<uint, List<int>> rangedUnitsByTileId)
        {
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;

            foreach (uint tileId in towerTileIds)
            {
                if (!rangedUnitsByTileId.TryGetValue(tileId, out List<int> rangedUnitIds))
                    continue;

                foreach (int unitId in rangedUnitIds)
                {
                    if (unitApi.TryGetUnitById(unitId, out GameUnit* unit) &&
                        unit != null &&
                        unit->r_AliveState == AliveState.IsAlive &&
                        unit->r_ControllableForPlayerId == ownerPlayerId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool TrySpawnProtectedDefender(
            int buildingId,
            GameBuilding* tower,
            List<uint> towerTileIds,
            HashSet<uint> occupiedUnitTileIds,
            out int unitId,
            out uint spawnTileId,
            out string failureReason)
        {
            unitId = 0;
            spawnTileId = 0;
            failureReason = null;

            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            bool foundTile = false;
            byte bestHeight = 0;
            long bestDistance = long.MaxValue;
            int bestTileX = 0;
            int bestTileY = 0;

            int towerCenterX2 = tower->r_TilePositionXBegin + tower->r_TilePositionXEnd;
            int towerCenterY2 = tower->r_TilePositionYBegin + tower->r_TilePositionYEnd;

            foreach (uint tileId in towerTileIds)
            {
                if (occupiedUnitTileIds.Contains(tileId))
                    continue;

                var tilePosition = tileApi.GetTileVectorFromId((int)tileId);
                byte height = tileApi.GetTileHeight((int)tileId);
                long deltaX2 = (tilePosition.X * 2L) - towerCenterX2;
                long deltaY2 = (tilePosition.Y * 2L) - towerCenterY2;
                long distance = (deltaX2 * deltaX2) + (deltaY2 * deltaY2);

                if (!foundTile || height > bestHeight || (height == bestHeight && distance < bestDistance))
                {
                    foundTile = true;
                    spawnTileId = tileId;
                    bestHeight = height;
                    bestDistance = distance;
                    bestTileX = tilePosition.X;
                    bestTileY = tilePosition.Y;
                }
            }

            if (!foundTile)
            {
                failureReason = "no unoccupied occupied-tile entry is available";
                return false;
            }

            int ownerPlayerId = tower->r_PlayerIdOwner;
            long createdId = GameUnitManagerAPI.Instance.CreateUnitLocal(
                playerOwnerId: ownerPlayerId,
                playerColorId: ownerPlayerId,
                localTileX: bestTileX,
                localTileY: bestTileY,
                heightElevation: bestHeight,
                chimp: DefenderType);

            if (createdId <= 0 || createdId > int.MaxValue)
            {
                failureReason = $"CreateUnitLocal returned invalid id {createdId}";
                return false;
            }

            unitId = (int)createdId;
            if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                unit == null ||
                !IsUnitActive(unit->r_AliveState) ||
                unit->r_GlobalId == 0)
            {
                failureReason = $"spawned unit {unitId} could not be validated";
                return false;
            }

            int initialTribeId = unit->r_TribeId;
            ushort initialAIBehaviourRelated = unit->r_AITribeRoleRelatedUnknown;
            short initialAIBehaviourType = (short)unit->r_AITribeRole;
            ProtectedDefender defender = new ProtectedDefender(
                unitId,
                unit->r_GlobalId,
                tower->r_GlobalId,
                ownerPlayerId,
                towerTileIds);

            protectedByUnitId[unitId] = defender;
            protectedByTowerGlobalId[tower->r_GlobalId] = defender;

            if (initialTribeId != 0)
            {
                bool unassigned = GameTribeManagerAPI.Instance.UnassignUnit(initialTribeId, unitId);
                LogInfo(
                    $"Spawned defender unexpectedly started in a tribe: unitId={unitId}, unitGlobalId={unit->r_GlobalId}, " +
                    $"initialTribeId={initialTribeId}, unassignIssued={unassigned}, tribeAfter={unit->r_TribeId}.");
            }

            bool privateTribeAssigned = TryEnsurePrivateTribe(
                defender,
                unit,
                "spawn initialization",
                out string privateTribeFailureReason);

            LogInfo(
                $"Spawned protected tower defender: buildingId={buildingId}, towerGlobalId={tower->r_GlobalId}, " +
                $"towerType={tower->r_BuildingType}, owner={ownerPlayerId}, towerBegin={tower->r_TilePositionXBegin},{tower->r_TilePositionYBegin}, " +
                $"towerEnd={tower->r_TilePositionXEnd},{tower->r_TilePositionYEnd}, spawnTileId={spawnTileId}, " +
                $"spawnTile={bestTileX},{bestTileY}, tileHeight={bestHeight}, buildingHeight={tower->r_HeightElevation}, " +
                $"unitId={unitId}, unitGlobalId={unit->r_GlobalId}, unitState={unit->r_AliveState}, initialTribeId={initialTribeId}, " +
                $"initialAIBehaviourRelated={initialAIBehaviourRelated}, initialAIBehaviourType={initialAIBehaviourType}, " +
                $"privateTribeAssigned={privateTribeAssigned}, privateTribeId={defender.PrivateTribeId}, " +
                $"privateTribeGlobalId={defender.PrivateTribeGlobalId}, privateTribeFailure={privateTribeFailureReason ?? "<none>"}, " +
                $"aiBehaviourRelatedAfter={unit->r_AITribeRoleRelatedUnknown}, aiBehaviourTypeAfter={(short)unit->r_AITribeRole}, " +
                $"selectable=true.");

            return true;
        }

        private bool IsProtectedDefenderValidForTower(
            ProtectedDefender defender,
            int ownerPlayerId,
            List<uint> towerTileIds)
        {
            if (!TryGetProtectedDefender(defender.UnitId, out ProtectedDefender current) || !ReferenceEquals(defender, current))
                return false;

            if (!GameUnitManagerAPI.Instance.TryGetUnitById(defender.UnitId, out GameUnit* unit) || unit == null)
                return false;

            if (unit->r_ControllableForPlayerId != ownerPlayerId || !towerTileIds.Contains(unit->r_CurrentPositionTileId))
                return false;

            TryEnsurePrivateTribe(defender, unit, "defense scan", out _);
            return true;
        }

        private bool TryGetProtectedDefender(int unitId, out ProtectedDefender defender)
        {
            defender = null;
            if (!IsRuntimeActiveForCurrentContext("protected-defender event editor detection"))
                return false;

            if (!protectedByUnitId.TryGetValue(unitId, out ProtectedDefender candidate))
                return false;

            if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                unit == null ||
                unit->r_GlobalId != candidate.UnitGlobalId ||
                !IsUnitActive(unit->r_AliveState))
            {
                RemoveProtectedDefender(candidate);
                return false;
            }

            defender = candidate;
            return true;
        }

        private bool TryGetProtectedDefenderByPrivateTribe(int tribeId, out ProtectedDefender defender)
        {
            defender = null;
            if (!IsRuntimeActiveForCurrentContext("private-tribe event editor detection"))
                return false;

            if (tribeId <= 0 ||
                !protectedByPrivateTribeId.TryGetValue(tribeId, out ProtectedDefender candidate) ||
                candidate.PrivateTribeId != tribeId)
            {
                return false;
            }

            if (!TryGetProtectedDefender(candidate.UnitId, out ProtectedDefender current) || !ReferenceEquals(candidate, current))
            {
                ClearPrivateTribeTracking(candidate);
                return false;
            }

            if (!TryGetExactPrivateTribe(candidate, out GameTribe* tribe) || tribe == null)
            {
                ClearPrivateTribeTracking(candidate);
                return false;
            }

            defender = candidate;
            return true;
        }

        private bool IsRuntimeActiveForCurrentContext(string reason)
        {
            if (!mapActive)
                return false;
            if (!IsMapEditor())
                return true;

            DisableForMapEditor(reason);
            return false;
        }

        private void DisableForMapEditor(string reason)
        {
            mapActive = false;
            ClearTracking();
            if (editorBypassActive)
                return;

            editorBypassActive = true;
            LogInfo($"Map-editor bypass active: reason={reason}; scans, defender creation, tribe interception, and movement-order interception remain disabled.");
        }

        private static bool IsMapEditor()
        {
            return GamePlayerManagerAPI.Instance?.IsInMapEditor() ?? false;
        }

        private bool TryEnsurePrivateTribe(
            ProtectedDefender defender,
            GameUnit* unit,
            string reason,
            out string failureReason)
        {
            failureReason = null;
            if (IsPrivateTribeValid(defender, unit))
            {
                EnsureProtectedAIBehaviour(defender, unit, "private tribe validation");
                return true;
            }

            if (defender.PrivateTribeId != 0)
                CleanupStalePrivateTribe(defender, unit, reason);

            if (unit->r_TribeId != 0)
            {
                int unexpectedTribeId = unit->r_TribeId;
                bool unassigned = false;
                bool clearedStaleBackReference = false;
                GameTribeManagerAPI unexpectedTribeApi = GameTribeManagerAPI.Instance;

                if (unexpectedTribeApi.TryGetTribeById(unexpectedTribeId, out GameTribe* unexpectedTribe) &&
                    unexpectedTribe != null &&
                    IsTribeActive(unexpectedTribe->r_AliveState))
                {
                    unassigned = unexpectedTribeApi.UnassignUnit(unexpectedTribeId, defender.UnitId);
                }
                else
                {
                    unit->r_TribeId = 0;
                    unit->r_TribeLeaderUnitId = 0;
                    clearedStaleBackReference = true;
                }

                LogInfo(
                    $"Removing protected defender from an unexpected tribe before private assignment: " +
                    $"unitId={defender.UnitId}, unitGlobalId={defender.UnitGlobalId}, towerGlobalId={defender.TowerGlobalId}, " +
                    $"unexpectedTribeId={unexpectedTribeId}, unassignIssued={unassigned}, " +
                    $"clearedStaleBackReference={clearedStaleBackReference}, tribeAfter={unit->r_TribeId}, reason={reason}.");

                if ((!unassigned && !clearedStaleBackReference) || unit->r_TribeId != 0)
                    return RecordPrivateTribeFailure(defender, reason, $"could not leave unexpected tribe {unexpectedTribeId}", out failureReason);
            }

            EnsureProtectedAIBehaviour(defender, unit, "before private tribe creation");

            GameTribeManagerAPI tribeApi = GameTribeManagerAPI.Instance;
            long createdId = tribeApi.Create(defender.OwnerPlayerId, false);
            if (createdId <= 0 || createdId > int.MaxValue)
                return RecordPrivateTribeFailure(defender, reason, $"Create returned invalid id {createdId}", out failureReason);

            int privateTribeId = (int)createdId;
            if (!tribeApi.TryGetTribeById(privateTribeId, out GameTribe* privateTribe) ||
                privateTribe == null ||
                !IsTribeActive(privateTribe->r_AliveState) ||
                privateTribe->r_GlobalId == 0 ||
                privateTribe->r_PlayerIdOwner != defender.OwnerPlayerId)
            {
                tribeApi.DeleteTribeSafe(privateTribeId);
                return RecordPrivateTribeFailure(defender, reason, $"created tribe {privateTribeId} could not be validated", out failureReason);
            }

            defender.PrivateTribeId = privateTribeId;
            defender.PrivateTribeGlobalId = privateTribe->r_GlobalId;
            protectedByPrivateTribeId[privateTribeId] = defender;

            bool assignmentIssued;
            permittedAssignmentUnitId = defender.UnitId;
            permittedAssignmentTribeId = privateTribeId;
            try
            {
                assignmentIssued = tribeApi.AssignUnit(privateTribeId, defender.UnitId);
            }
            finally
            {
                permittedAssignmentUnitId = 0;
                permittedAssignmentTribeId = 0;
            }

            if (!assignmentIssued || unit->r_TribeId != privateTribeId)
            {
                CleanupFailedPrivateTribeCreation(defender, unit);
                return RecordPrivateTribeFailure(
                    defender,
                    reason,
                    $"assignment to tribe {privateTribeId} did not stick (issued={assignmentIssued}, tribeAfter={unit->r_TribeId})",
                    out failureReason);
            }

            bool stanceSet = tribeApi.SetStance(privateTribeId, TribeStance.Hold);
            EnsureProtectedAIBehaviour(defender, unit, "after private tribe assignment");
            totalPrivateTribeCreations++;

            LogInfo(
                $"Created private defender tribe: unitId={defender.UnitId}, unitGlobalId={defender.UnitGlobalId}, " +
                $"towerGlobalId={defender.TowerGlobalId}, owner={defender.OwnerPlayerId}, reason={reason}, " +
                $"tribeId={privateTribeId}, tribeGlobalId={defender.PrivateTribeGlobalId}, tribeState={privateTribe->r_AliveState}, " +
                $"stanceSet={stanceSet}, stance={privateTribe->r_TribeStance}, unitTribeId={unit->r_TribeId}, " +
                $"aiBehaviourRelated={unit->r_AITribeRoleRelatedUnknown}, aiBehaviourType={(short)unit->r_AITribeRole}, " +
                $"totalPrivateTribeCreations={totalPrivateTribeCreations}.");

            return true;
        }

        private bool IsPrivateTribeValid(ProtectedDefender defender, GameUnit* unit)
        {
            return defender.PrivateTribeId > 0 &&
                unit->r_TribeId == defender.PrivateTribeId &&
                TryGetExactPrivateTribe(defender, out GameTribe* tribe) &&
                tribe != null &&
                IsTribeActive(tribe->r_AliveState) &&
                tribe->r_PlayerIdOwner == defender.OwnerPlayerId;
        }

        private bool TryGetExactPrivateTribe(ProtectedDefender defender, out GameTribe* tribe)
        {
            tribe = null;
            if (defender.PrivateTribeId <= 0 || defender.PrivateTribeGlobalId == 0)
                return false;

            return GameTribeManagerAPI.Instance.TryGetTribeById(defender.PrivateTribeId, out tribe) &&
                tribe != null &&
                tribe->r_GlobalId == defender.PrivateTribeGlobalId;
        }

        private void CleanupStalePrivateTribe(ProtectedDefender defender, GameUnit* unit, string reason)
        {
            int staleTribeId = defender.PrivateTribeId;
            uint staleTribeGlobalId = defender.PrivateTribeGlobalId;
            bool exactTribeFound = TryGetExactPrivateTribe(defender, out GameTribe* staleTribe) && staleTribe != null;
            bool unassigned = false;
            bool clearedStaleBackReference = false;
            bool deleteMarked = false;

            if (unit->r_TribeId == staleTribeId)
            {
                if (exactTribeFound && IsTribeActive(staleTribe->r_AliveState))
                {
                    unassigned = GameTribeManagerAPI.Instance.UnassignUnit(staleTribeId, defender.UnitId);
                }
                else
                {
                    unit->r_TribeId = 0;
                    unit->r_TribeLeaderUnitId = 0;
                    clearedStaleBackReference = true;
                }
            }

            if (exactTribeFound && IsTribeActive(staleTribe->r_AliveState))
                deleteMarked = GameTribeManagerAPI.Instance.DeleteTribeSafe(staleTribeId);

            ClearPrivateTribeTracking(defender);
            LogInfo(
                $"Cleaned up stale private defender tribe: unitId={defender.UnitId}, unitGlobalId={defender.UnitGlobalId}, " +
                $"towerGlobalId={defender.TowerGlobalId}, tribeId={staleTribeId}, tribeGlobalId={staleTribeGlobalId}, " +
                $"exactTribeFound={exactTribeFound}, unassigned={unassigned}, clearedStaleBackReference={clearedStaleBackReference}, " +
                $"deleteMarked={deleteMarked}, unitTribeAfter={unit->r_TribeId}, reason={reason}.");
        }

        private void CleanupFailedPrivateTribeCreation(ProtectedDefender defender, GameUnit* unit)
        {
            int failedTribeId = defender.PrivateTribeId;
            bool exactTribeFound = TryGetExactPrivateTribe(defender, out GameTribe* failedTribe) && failedTribe != null;

            if (unit->r_TribeId == failedTribeId && exactTribeFound && IsTribeActive(failedTribe->r_AliveState))
                GameTribeManagerAPI.Instance.UnassignUnit(failedTribeId, defender.UnitId);

            if (exactTribeFound && IsTribeActive(failedTribe->r_AliveState))
                GameTribeManagerAPI.Instance.DeleteTribeSafe(failedTribeId);

            ClearPrivateTribeTracking(defender);
        }

        private bool RecordPrivateTribeFailure(
            ProtectedDefender defender,
            string reason,
            string detail,
            out string failureReason)
        {
            failureReason = detail;
            defender.PrivateTribeFailures++;
            totalPrivateTribeFailures++;

            if (ShouldLogBlockedOrder(defender.PrivateTribeFailures))
            {
                LogInfo(
                    $"Private defender tribe setup failed: unitId={defender.UnitId}, unitGlobalId={defender.UnitGlobalId}, " +
                    $"towerGlobalId={defender.TowerGlobalId}, owner={defender.OwnerPlayerId}, reason={reason}, detail={detail}, " +
                    $"unitFailureCount={defender.PrivateTribeFailures}, totalFailureCount={totalPrivateTribeFailures}.");
            }

            return false;
        }

        private void ReleaseDefendersOfMissingTowers(HashSet<uint> liveAITowerGlobals)
        {
            List<ProtectedDefender> defendersToRelease = new List<ProtectedDefender>();

            foreach (KeyValuePair<uint, ProtectedDefender> entry in protectedByTowerGlobalId)
            {
                if (!liveAITowerGlobals.Contains(entry.Key))
                    defendersToRelease.Add(entry.Value);
            }

            foreach (ProtectedDefender defender in defendersToRelease)
                ReleaseProtectedDefender(defender, "tower is no longer a living AI-owned tower");
        }

        private void ReleaseProtectedDefender(ProtectedDefender defender, string reason)
        {
            int privateTribeId = defender.PrivateTribeId;
            uint privateTribeGlobalId = defender.PrivateTribeGlobalId;
            bool unassigned = false;
            bool clearedStaleBackReference = false;
            bool deleteMarked = false;

            if (GameUnitManagerAPI.Instance.TryGetUnitById(defender.UnitId, out GameUnit* unit) &&
                unit != null &&
                unit->r_GlobalId == defender.UnitGlobalId &&
                IsUnitActive(unit->r_AliveState))
            {
                DetachPrivateTribe(
                    defender,
                    unit,
                    out unassigned,
                    out clearedStaleBackReference,
                    out deleteMarked);
            }

            RemoveProtectedDefender(defender);
            LogInfo(
                $"Released protected tower defender: unitId={defender.UnitId}, unitGlobalId={defender.UnitGlobalId}, " +
                $"towerGlobalId={defender.TowerGlobalId}, privateTribeId={privateTribeId}, " +
                $"privateTribeGlobalId={privateTribeGlobalId}, unassigned={unassigned}, " +
                $"clearedStaleBackReference={clearedStaleBackReference}, deleteMarked={deleteMarked}, reason={reason}.");
        }

        private void DetachPrivateTribe(
            ProtectedDefender defender,
            GameUnit* unit,
            out bool unassigned,
            out bool clearedStaleBackReference,
            out bool deleteMarked)
        {
            unassigned = false;
            clearedStaleBackReference = false;
            deleteMarked = false;

            int privateTribeId = defender.PrivateTribeId;
            bool exactTribeFound = TryGetExactPrivateTribe(defender, out GameTribe* privateTribe) && privateTribe != null;

            if (privateTribeId > 0 && unit->r_TribeId == privateTribeId)
            {
                if (exactTribeFound && IsTribeActive(privateTribe->r_AliveState))
                {
                    unassigned = GameTribeManagerAPI.Instance.UnassignUnit(privateTribeId, defender.UnitId);
                }
                else
                {
                    unit->r_TribeId = 0;
                    unit->r_TribeLeaderUnitId = 0;
                    clearedStaleBackReference = true;
                }
            }

            if (exactTribeFound && IsTribeActive(privateTribe->r_AliveState))
                deleteMarked = GameTribeManagerAPI.Instance.DeleteTribeSafe(privateTribeId);

            ClearPrivateTribeTracking(defender);
        }

        private bool EnsureProtectedAIBehaviour(ProtectedDefender defender, GameUnit* unit, string reason)
        {
            ushort relatedBefore = unit->r_AITribeRoleRelatedUnknown;
            short behaviourTypeBefore = (short)unit->r_AITribeRole;
            if (relatedBefore == ProtectedAIBehaviourRelatedValue && behaviourTypeBefore == ProtectedAIBehaviourTypeValue)
                return false;

            unit->r_AITribeRoleRelatedUnknown = ProtectedAIBehaviourRelatedValue;
            unit->r_AITribeRole = ProtectedAIBehaviourType;
            defender.AIBehaviourRepairs++;
            totalAIBehaviourRepairs++;

            if (ShouldLogRepeatedAssignment(defender.AIBehaviourRepairs))
            {
                LogInfo(
                    $"Set protected defender AI behaviour: unitId={defender.UnitId}, unitGlobalId={defender.UnitGlobalId}, " +
                    $"towerGlobalId={defender.TowerGlobalId}, reason={reason}, " +
                    $"aiBehaviourRelated={relatedBefore}->{unit->r_AITribeRoleRelatedUnknown}, " +
                    $"aiBehaviourType={behaviourTypeBefore}->{(short)unit->r_AITribeRole}, " +
                    $"unitRepairCount={defender.AIBehaviourRepairs}, totalRepairCount={totalAIBehaviourRepairs}.");
            }

            return true;
        }

        private static bool IsMovementTargetOnTower(
            ProtectedDefender defender,
            int tileX,
            int tileY,
            out int targetTileId)
        {
            targetTileId = -1;
            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            if (!tileApi.IsTileInsideMapBounds(tileX, tileY))
                return false;

            targetTileId = tileApi.GetTileId(tileX, tileY);
            return tileApi.IsValidTileId(targetTileId) && defender.TowerTileIds.Contains((uint)targetTileId);
        }

        private void RecordAllowedTowerMove(
            ProtectedDefender defender,
            string source,
            int tileX,
            int tileY,
            int targetTileId,
            string details)
        {
            defender.AllowedTowerMoveOrders++;
            totalAllowedTowerMoveOrders++;

            if (ShouldLogBlockedOrder(defender.AllowedTowerMoveOrders))
            {
                LogInfo(
                    $"Allowed protected defender movement on tower: source={source}, unitId={defender.UnitId}, " +
                    $"unitGlobalId={defender.UnitGlobalId}, towerGlobalId={defender.TowerGlobalId}, " +
                    $"target={tileX},{tileY}, targetTileId={targetTileId}, towerTileCount={defender.TowerTileIds.Count}, " +
                    $"details=[{details}], unitAllowCount={defender.AllowedTowerMoveOrders}, " +
                    $"totalAllowCount={totalAllowedTowerMoveOrders}.");
            }
        }

        private void RemoveProtectedDefender(ProtectedDefender defender)
        {
            ClearPrivateTribeTracking(defender);

            if (protectedByUnitId.TryGetValue(defender.UnitId, out ProtectedDefender unitEntry) && ReferenceEquals(unitEntry, defender))
                protectedByUnitId.Remove(defender.UnitId);

            if (protectedByTowerGlobalId.TryGetValue(defender.TowerGlobalId, out ProtectedDefender towerEntry) && ReferenceEquals(towerEntry, defender))
                protectedByTowerGlobalId.Remove(defender.TowerGlobalId);
        }

        private void ClearPrivateTribeTracking(ProtectedDefender defender)
        {
            if (defender.PrivateTribeId > 0 &&
                protectedByPrivateTribeId.TryGetValue(defender.PrivateTribeId, out ProtectedDefender tribeEntry) &&
                ReferenceEquals(tribeEntry, defender))
            {
                protectedByPrivateTribeId.Remove(defender.PrivateTribeId);
            }

            defender.PrivateTribeId = 0;
            defender.PrivateTribeGlobalId = 0;
        }

        private void ClearTracking()
        {
            protectedByUnitId.Clear();
            protectedByTowerGlobalId.Clear();
            protectedByPrivateTribeId.Clear();
            loggedSpawnFailureTowerGlobals.Clear();
            permittedAssignmentUnitId = 0;
            permittedAssignmentTribeId = 0;
            totalBlockedTribeAssignments = 0;
            totalBlockedMoveOrders = 0;
            totalBlockedPrivateTribeOrders = 0;
            totalAllowedTowerMoveOrders = 0;
            totalAIBehaviourRepairs = 0;
            totalPrivateTribeCreations = 0;
            totalPrivateTribeFailures = 0;
        }

        private static bool IsUnitActive(AliveState state)
        {
            return state == AliveState.NeedsInit || state == AliveState.IsAlive;
        }

        private static bool IsTribeActive(AliveState state)
        {
            return state == AliveState.NeedsInit || state == AliveState.IsAlive;
        }

        private static bool ShouldLogBlockedOrder(int count)
        {
            return count <= 5 || count % 25 == 0;
        }

        private static bool ShouldLogRepeatedAssignment(int count)
        {
            return count <= 3 || count % 100 == 0;
        }

        private void LogInfo(string message)
        {
            Shared.DebugLogHelper.LogInfo(log, message);
        }

        private sealed class AIBehaviourPlayerDiagnostic
        {
            public AIBehaviourPlayerDiagnostic(int ownerPlayerId)
            {
                OwnerPlayerId = ownerPlayerId;
            }

            public int OwnerPlayerId { get; }
            public int TotalUnits { get; private set; }
            public int TribeAssignedUnits { get; private set; }
            public int BehaviourTypeZeroUnits { get; private set; }
            public int ProtectedUnits { get; private set; }
            public Dictionary<string, AIBehaviourDiagnosticBucket> Buckets { get; } =
                new Dictionary<string, AIBehaviourDiagnosticBucket>();

            public void Add(GameUnit* unit, bool isProtected)
            {
                short behaviourType = (short)unit->r_AITribeRole;
                ushort related = unit->r_AITribeRoleRelatedUnknown;
                bool tribeAssigned = unit->r_TribeId != 0;

                TotalUnits++;
                if (tribeAssigned)
                    TribeAssignedUnits++;
                if (behaviourType == 0)
                    BehaviourTypeZeroUnits++;
                if (isProtected)
                    ProtectedUnits++;

                string key = $"{behaviourType}|{related}|{tribeAssigned}|{isProtected}";
                if (!Buckets.TryGetValue(key, out AIBehaviourDiagnosticBucket bucket))
                {
                    bucket = new AIBehaviourDiagnosticBucket(behaviourType, related, tribeAssigned, isProtected);
                    Buckets[key] = bucket;
                }

                bucket.Add(unit);
            }
        }

        private sealed class AIBehaviourDiagnosticBucket
        {
            public AIBehaviourDiagnosticBucket(short behaviourType, ushort related, bool tribeAssigned, bool isProtected)
            {
                BehaviourType = behaviourType;
                Related = related;
                TribeAssigned = tribeAssigned;
                Protected = isProtected;
            }

            public short BehaviourType { get; }
            public ushort Related { get; }
            public bool TribeAssigned { get; }
            public bool Protected { get; }
            public int Count { get; private set; }
            public Dictionary<eChimps, int> TypeCounts { get; } = new Dictionary<eChimps, int>();
            public HashSet<int> TribeIds { get; } = new HashSet<int>();
            public HashSet<uint> Raw428Values { get; } = new HashSet<uint>();
            public HashSet<uint> Raw42CValues { get; } = new HashSet<uint>();
            public HashSet<ushort> Raw430Values { get; } = new HashSet<ushort>();
            public HashSet<ushort> Raw432Values { get; } = new HashSet<ushort>();

            public void Add(GameUnit* unit)
            {
                Count++;

                eChimps type = unit->r_UnitChimp;
                if (TypeCounts.TryGetValue(type, out int typeCount))
                    TypeCounts[type] = typeCount + 1;
                else
                    TypeCounts[type] = 1;

                if (unit->r_TribeId != 0)
                    TribeIds.Add(unit->r_TribeId);

                Raw428Values.Add(unit->N000000D8);
                Raw42CValues.Add(unit->N000001FE);
                Raw430Values.Add(unit->N000000D9);
                Raw432Values.Add(unit->r_FarmerAIRelatedUnknown);
            }
        }

        private sealed class ProtectedDefender
        {
            public ProtectedDefender(
                int unitId,
                uint unitGlobalId,
                uint towerGlobalId,
                int ownerPlayerId,
                IEnumerable<uint> towerTileIds)
            {
                UnitId = unitId;
                UnitGlobalId = unitGlobalId;
                TowerGlobalId = towerGlobalId;
                OwnerPlayerId = ownerPlayerId;
                TowerTileIds = new HashSet<uint>(towerTileIds ?? throw new ArgumentNullException(nameof(towerTileIds)));
            }

            public int UnitId { get; }
            public uint UnitGlobalId { get; }
            public uint TowerGlobalId { get; }
            public int OwnerPlayerId { get; }
            public HashSet<uint> TowerTileIds { get; }
            public int BlockedTribeAssignments { get; set; }
            public int BlockedMoveOrders { get; set; }
            public int BlockedPrivateTribeOrders { get; set; }
            public int AllowedTowerMoveOrders { get; set; }
            public int AIBehaviourRepairs { get; set; }
            public int PrivateTribeId { get; set; }
            public uint PrivateTribeGlobalId { get; set; }
            public int PrivateTribeFailures { get; set; }
        }
    }
}
