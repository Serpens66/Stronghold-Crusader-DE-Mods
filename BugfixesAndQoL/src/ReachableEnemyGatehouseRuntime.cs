// Feature: Prevent unreachable enemies from closing protected inner gatehouses.
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace BugfixesAndQoL
{
    internal sealed unsafe class ReachableEnemyGatehouseRuntime : IDisposable
    {
        private const int MaximumFailureLogs = 20;

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly Dictionary<ReachabilityKey, bool> reachabilityCache =
            new Dictionary<ReachabilityKey, bool>();
        private readonly GatehouseReachabilityDiagnosticThrottle diagnosticThrottle =
            new GatehouseReachabilityDiagnosticThrottle();
        private IDisposable gatehouseQuerySubscription;
        private bool reachabilityAvailable;
        private bool firstQueryLogged;
        private int lastCacheTick = int.MinValue;
        private int failureLogs;

        public ReachableEnemyGatehouseRuntime(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void Initialize()
        {
            if (gatehouseQuerySubscription != null)
                return;

            // The installed manifest-selected Script Extender supplies a one-based Unit game ID.
            // The shared adapter validates it without conversion.
            gatehouseQuerySubscription = BuildingR3EventHooks.OnGatehouseQuery.Observable
                .Subscribe(OnGatehouseQuery);
        }

        public void SetNativeCompatibility(bool referenceHashMatches)
        {
            reachabilityAvailable = referenceHashMatches;
            ClearCache();
            if (!referenceHashMatches)
            {
                LogWarning(
                    "gatehouse PCL reachability filtering is unavailable because the installed DLL " +
                    "differs from the audited build; Vanilla candidate handling remains active.");
            }
        }

        public void Dispose()
        {
            gatehouseQuerySubscription?.Dispose();
            gatehouseQuerySubscription = null;
            ClearCache();
        }

        private void OnGatehouseQuery(GatehouseQueryEventArgs args)
        {
            if (!settings.EnableMod || !settings.RequireReachableEnemyForAutomaticGateClosing ||
                !reachabilityAvailable || args == null)
            {
                return;
            }

            try
            {
                if (!TryGetLiveGatehouse(args.BuildingId, out GameBuilding* building, out PathConnectionRecord* gatehouse))
                    return;

                int candidateUnitId = args.UnitId;
                Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
                if (!Shared.GatehouseQueryUnitIdPolicy.TryValidateGameId(
                        candidateUnitId,
                        units.Length,
                        out int unitId) ||
                    !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null)
                {
                    return;
                }

                bool vanillaCandidateCanClose =
                    unit->r_AliveState == AliveState.IsAlive &&
                    unit->r_UnitChimp != eChimps.CHIMP_TYPE_LION &&
                    unit->r_ControllableForPlayerId != 0;
                bool? incomingDecision = args.ShouldClose;
                args.ShouldClose = Shared.GatehouseQueryUnitIdPolicy.ResolveCandidateDecision(
                    args.ShouldClose,
                    vanillaCandidateCanClose);
                if (args.ShouldClose != true)
                    return;
                bool? vanillaDecision = args.ShouldClose;

                if (!firstQueryLogged)
                {
                    firstQueryLogged = true;
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        $"gatehouse reachability query confirmed: buildingId={args.BuildingId}, " +
                        $"eventUnitId={candidateUnitId}, unitId={unitId}, globalId={building->r_GlobalId}.");
                }

                bool evaluationAvailable = TryIsUnitReachableToGate(
                        unitId,
                        unit,
                        building->r_PlayerIdOwner,
                        gatehouse,
                        out bool reachable,
                        out ReachabilityEvaluation evaluation);
                if (evaluationAvailable && !reachable)
                    args.ShouldClose = false;

                MaybeLogGatehouseDiagnostic(
                    args.BuildingId,
                    building,
                    gatehouse,
                    unitId,
                    unit,
                    incomingDecision,
                    vanillaDecision,
                    args.ShouldClose,
                    evaluationAvailable,
                    reachable,
                    evaluation);
            }
            catch (Exception ex)
            {
                LogFailure(
                    $"gatehouse reachability query failed: buildingId={args.BuildingId}, " +
                    $"eventUnitId={args.UnitId}, error={ex}");
            }
        }

        private bool TryIsUnitReachableToGate(
            int unitId,
            GameUnit* unit,
            int gatehouseOwnerId,
            PathConnectionRecord* gatehouse,
            out bool reachable,
            out ReachabilityEvaluation evaluation)
        {
            reachable = true;
            evaluation = new ReachabilityEvaluation();
            if (unitId <= 0 || gatehouse == null || unit == null ||
                unit->r_AliveState != AliveState.IsAlive || unit->r_CurrentHealth == 0 ||
                unit->r_ControllableForPlayerId <= 0)
            {
                evaluation.FailureStage = "invalid-unit-or-gate";
                return false;
            }

            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            GamePathingManagerAPI pathingApi = GamePathingManagerAPI.Instance;
            int sourceTileId = (int)unit->r_CurrentPositionTileId;
            int entryTileId = (int)gatehouse->r_EntryTileId;
            int exitTileId = (int)gatehouse->r_ExitTileId;
            if (!tileApi.IsValidTileId(sourceTileId) || !tileApi.IsValidTileId(entryTileId) ||
                !tileApi.IsValidTileId(exitTileId))
            {
                evaluation.FailureStage = "invalid-tile-id";
                return false;
            }

            Span<ushort> pathConnections = pathingApi.GetPathComponentGrid();
            if ((uint)sourceTileId >= (uint)pathConnections.Length ||
                (uint)entryTileId >= (uint)pathConnections.Length ||
                (uint)exitTileId >= (uint)pathConnections.Length)
            {
                evaluation.FailureStage = "tile-outside-pcl-grid";
                return false;
            }

            int tick = GameTimeManagerAPI.Instance.CaptureTimeStamp().CapturedGameTick;
            if (tick != lastCacheTick)
            {
                reachabilityCache.Clear();
                lastCacheTick = tick;
            }

            int playerId = unit->r_ControllableForPlayerId;
            int sourcePcl = pathConnections[sourceTileId];
            int entryPcl = pathConnections[entryTileId];
            int exitPcl = pathConnections[exitTileId];
            int mode = unit->r_PathConnectionMode;
            evaluation.Tick = tick;
            evaluation.SourceTileId = sourceTileId;
            evaluation.SourcePcl = sourcePcl;
            evaluation.EntryPcl = entryPcl;
            evaluation.ExitPcl = exitPcl;
            evaluation.Mode = mode;
            if (!TryCreatePortalSnapshot(*gatehouse, tileApi, out GatePortalSnapshot gatehousePortal) ||
                !TryCollectSynchronizedDrawbridges(
                    gatehouseOwnerId,
                    unit,
                    gatehousePortal,
                    tileApi,
                    pathingApi,
                    out List<GatePortalSnapshot> drawbridges,
                    out int synchronizedGroupSignature,
                    out int diagnosticSignature))
            {
                evaluation.FailureStage = "portal-or-drawbridge-collection";
                return false;
            }

            evaluation.GatehousePortal = gatehousePortal;
            evaluation.Drawbridges = drawbridges;
            evaluation.SynchronizedGroupSignature = synchronizedGroupSignature;
            evaluation.DrawbridgeDiagnosticSignature = diagnosticSignature;
            evaluation.GroupComponents = FormatGroupComponents(gatehousePortal, drawbridges);

            var key = new ReachabilityKey(
                playerId,
                sourcePcl,
                entryPcl,
                exitPcl,
                mode,
                synchronizedGroupSignature);
            if (reachabilityCache.TryGetValue(key, out reachable))
            {
                evaluation.CacheHit = true;
                evaluation.Reachable = reachable;
                return true;
            }

            PathConnectionQueryMode queryMode = (PathConnectionQueryMode)mode;
            int entryResult = pathingApi.FindNextComponentTowardDestination(
                playerId,
                sourcePcl,
                entryPcl,
                queryMode);
            int exitResult = entryResult != 0
                ? entryResult
                : pathingApi.FindNextComponentTowardDestination(
                    playerId,
                    sourcePcl,
                    exitPcl,
                    queryMode);
            reachable = entryResult != 0 || exitResult != 0;
            evaluation.EntryResult = entryResult;
            evaluation.ExitResult = exitResult;
            evaluation.DirectReachable = reachable;
            if (!reachable && drawbridges.Count > 0)
            {
                reachable = SynchronizedGatehouseReachabilityPolicy.CanReachSynchronizedGroup(
                    gatehousePortal,
                    drawbridges,
                    component => component == sourcePcl ||
                        pathingApi.FindNextComponentTowardDestination(
                            playerId,
                            sourcePcl,
                            component,
                            queryMode) != 0);
            }
            evaluation.GroupReachable = !evaluation.DirectReachable && reachable;
            evaluation.Reachable = reachable;
            reachabilityCache[key] = reachable;
            return true;
        }

        private static bool TryCollectSynchronizedDrawbridges(
            int buildingOwnerId,
            GameUnit* unit,
            GatePortalSnapshot gatehouse,
            GameTileManagerAPI tileApi,
            GamePathingManagerAPI pathingApi,
            out List<GatePortalSnapshot> drawbridges,
            out int signature,
            out int diagnosticSignature)
        {
            drawbridges = new List<GatePortalSnapshot>(
                SynchronizedGatehouseReachabilityPolicy.MaximumSynchronizedDrawbridges);
            signature = ComputePortalSignature(gatehouse);
            diagnosticSignature = signature;
            if (buildingOwnerId <= 0 || unit == null)
                return false;

            Span<PathConnectionRecord> records = pathingApi.GetPathConnectionRecords();
            int lastRecordId = Math.Min(
                GamePathingManagerAPI.LAST_PATH_CONNECTION_RECORD_ID,
                records.Length - 1);
            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            for (int recordId = GamePathingManagerAPI.FIRST_PATH_CONNECTION_RECORD_ID;
                 recordId <= lastRecordId;
                 recordId++)
            {
                ref PathConnectionRecord record = ref records[recordId];
                if (record.r_BuildingId > 0 &&
                    buildingApi.TryGetBuildingById(record.r_BuildingId, out GameBuilding* diagnosticBuilding) &&
                    diagnosticBuilding != null &&
                    diagnosticBuilding->r_AliveState == AliveState.IsAlive &&
                    diagnosticBuilding->r_BuildingType == eStructs.STRUCT_DRAWBRIDGE &&
                    diagnosticBuilding->r_PlayerIdOwner == buildingOwnerId)
                {
                    unchecked
                    {
                        diagnosticSignature = diagnosticSignature * 397 ^ recordId;
                        diagnosticSignature = diagnosticSignature * 397 ^ record.r_IsActive;
                        diagnosticSignature = diagnosticSignature * 397 ^ record.r_IsEnabledOrOpen;
                        diagnosticSignature = diagnosticSignature * 397 ^ record.r_PathComponentA;
                        diagnosticSignature = diagnosticSignature * 397 ^ record.r_PathComponentB;
                        diagnosticSignature = diagnosticSignature * 397 ^ record.r_PathComponentC;
                        diagnosticSignature = diagnosticSignature * 397 ^ (int)diagnosticBuilding->r_AIWalkableState;
                    }
                }

                if (record.r_IsActive == 0 || record.r_BuildingId <= 0 ||
                    !buildingApi.TryGetBuildingById(record.r_BuildingId, out GameBuilding* drawbridge) ||
                    drawbridge == null || drawbridge->r_AliveState != AliveState.IsAlive ||
                    drawbridge->r_BuildingType != eStructs.STRUCT_DRAWBRIDGE ||
                    drawbridge->r_PlayerIdOwner != buildingOwnerId ||
                    drawbridge->r_GlobalId == 0 ||
                    record.r_SubjectGlobalId != drawbridge->r_GlobalId ||
                    !pathingApi.CanUnitTypeUsePathConnectionClass(
                        unit->r_UnitChimp,
                        record.r_ConnectionClass) ||
                    !TryCreatePortalSnapshot(record, tileApi, out GatePortalSnapshot bridgePortal) ||
                    !SynchronizedGatehouseReachabilityPolicy.IsAssociatedDrawbridge(
                        gatehouse,
                        bridgePortal))
                {
                    continue;
                }

                if (drawbridges.Count >=
                    SynchronizedGatehouseReachabilityPolicy.MaximumSynchronizedDrawbridges)
                {
                    return false;
                }

                drawbridges.Add(bridgePortal);
                unchecked
                {
                    signature = signature * 397 ^ recordId;
                    signature = signature * 397 ^ record.r_BuildingId;
                    signature = signature * 397 ^ (int)record.r_SubjectGlobalId;
                    signature = signature * 397 ^ ComputePortalSignature(bridgePortal);
                }
            }

            return true;
        }

        private static bool TryCreatePortalSnapshot(
            PathConnectionRecord record,
            GameTileManagerAPI tileApi,
            out GatePortalSnapshot snapshot)
        {
            snapshot = new GatePortalSnapshot(
                record.r_PathComponentA,
                record.r_PathComponentB,
                record.r_PathComponentC,
                record.r_EntryTilePositionX,
                record.r_EntryTilePositionY,
                record.r_ExitTilePositionX,
                record.r_ExitTilePositionY);
            if (!snapshot.HasValidComponents ||
                !tileApi.IsValidTileId(record.r_EntryTileId) ||
                !tileApi.IsValidTileId(record.r_ExitTileId) ||
                !tileApi.IsTileInsideMapBounds(snapshot.EntryX, snapshot.EntryY) ||
                !tileApi.IsTileInsideMapBounds(snapshot.ExitX, snapshot.ExitY))
            {
                return false;
            }

            return tileApi.GetTileId(snapshot.EntryX, snapshot.EntryY) == record.r_EntryTileId &&
                tileApi.GetTileId(snapshot.ExitX, snapshot.ExitY) == record.r_ExitTileId;
        }

        private static int ComputePortalSignature(GatePortalSnapshot portal)
        {
            unchecked
            {
                int hash = portal.FirstPcl;
                hash = hash * 397 ^ portal.SecondPcl;
                hash = hash * 397 ^ portal.ThirdPcl;
                hash = hash * 397 ^ portal.EntryX;
                hash = hash * 397 ^ portal.EntryY;
                hash = hash * 397 ^ portal.ExitX;
                return hash * 397 ^ portal.ExitY;
            }
        }

        private static bool TryGetLiveGatehouse(
            int buildingId,
            out GameBuilding* building,
            out PathConnectionRecord* gatehouse)
        {
            building = null;
            gatehouse = null;
            GameBuildingManagerAPI api = GameBuildingManagerAPI.Instance;
            return buildingId > 0 &&
                api.TryGetBuildingById(buildingId, out building) && building != null &&
                building->r_AliveState == AliveState.IsAlive &&
                GamePathingManagerAPI.Instance.TryGetPathConnectionRecordByBuildingId(buildingId, out gatehouse) &&
                gatehouse != null && gatehouse->r_BuildingId == buildingId &&
                gatehouse->r_SubjectGlobalId == building->r_GlobalId;
        }

        private void MaybeLogGatehouseDiagnostic(
            int gatehouseId,
            GameBuilding* gatehouseBuilding,
            PathConnectionRecord* gatehouseRecord,
            int unitId,
            GameUnit* unit,
            bool? incomingDecision,
            bool? vanillaDecision,
            bool? finalDecision,
            bool evaluationAvailable,
            bool reachable,
            ReachabilityEvaluation evaluation)
        {
            if (gatehouseBuilding == null || gatehouseRecord == null || unit == null ||
                evaluation == null || evaluation.CacheHit)
            {
                return;
            }

            string fingerprint = string.Join(
                "|",
                FormatDecision(incomingDecision),
                FormatDecision(vanillaDecision),
                FormatDecision(finalDecision),
                evaluationAvailable ? "evaluated" : evaluation.FailureStage,
                reachable ? "reachable" : "unreachable",
                gatehouseRecord->r_IsActive.ToString(CultureInfo.InvariantCulture),
                gatehouseRecord->r_IsEnabledOrOpen.ToString(CultureInfo.InvariantCulture),
                ((int)gatehouseBuilding->r_AIWalkableState).ToString(CultureInfo.InvariantCulture),
                evaluation.SourcePcl.ToString(CultureInfo.InvariantCulture),
                evaluation.EntryPcl.ToString(CultureInfo.InvariantCulture),
                evaluation.ExitPcl.ToString(CultureInfo.InvariantCulture),
                evaluation.EntryResult.ToString(CultureInfo.InvariantCulture),
                evaluation.ExitResult.ToString(CultureInfo.InvariantCulture),
                evaluation.SynchronizedGroupSignature.ToString(CultureInfo.InvariantCulture),
                evaluation.DrawbridgeDiagnosticSignature.ToString(CultureInfo.InvariantCulture));

            GatehouseDiagnosticEmission emission = diagnosticThrottle.Observe(
                gatehouseId,
                gatehouseBuilding->r_GlobalId,
                unitId,
                unit->r_GlobalId,
                fingerprint,
                out int suppressedRepeats);
            if (emission == GatehouseDiagnosticEmission.None)
                return;

            if (emission == GatehouseDiagnosticEmission.Summary)
            {
                LogDiagnostic(
                    $"GATE_REACH_DIAG summary: gateId={gatehouseId}, gateGlobalId={gatehouseBuilding->r_GlobalId}, " +
                    $"unitId={unitId}, unitGlobalId={unit->r_GlobalId}, unchangedRepeats={suppressedRepeats}, " +
                    $"fingerprint={fingerprint}.");
                return;
            }

            LogDiagnostic(
                $"GATE_REACH_DIAG decision: tick={evaluation.Tick}, gateId={gatehouseId}, " +
                $"gateGlobalId={gatehouseBuilding->r_GlobalId}, owner={gatehouseBuilding->r_PlayerIdOwner}, " +
                $"type={(int)gatehouseBuilding->r_BuildingType}, unitId={unitId}, " +
                $"unitGlobalId={unit->r_GlobalId}, unitPlayer={unit->r_ControllableForPlayerId}, " +
                $"unitType={(int)unit->r_UnitChimp}, sourceTile={evaluation.SourceTileId}, " +
                $"mode={evaluation.Mode}, incoming={FormatDecision(incomingDecision)}, " +
                $"vanilla={FormatDecision(vanillaDecision)}, final={FormatDecision(finalDecision)}, " +
                $"evaluationAvailable={evaluationAvailable}, reachable={reachable}, " +
                $"failureStage={evaluation.FailureStage}, suppressedSinceChange={suppressedRepeats}.");
            LogDiagnostic(
                $"GATE_REACH_DIAG gate: begin=({gatehouseBuilding->r_TilePositionXBegin}," +
                $"{gatehouseBuilding->r_TilePositionYBegin}), end=({gatehouseBuilding->r_TilePositionXEnd}," +
                $"{gatehouseBuilding->r_TilePositionYEnd}), occupyGridSize={gatehouseBuilding->r_OccupyTileGridSize}, " +
                $"aiWalkableState={(int)gatehouseBuilding->r_AIWalkableState}, " +
                $"recordActive={gatehouseRecord->r_IsActive}, recordOpen={gatehouseRecord->r_IsEnabledOrOpen}, " +
                $"recordGlobalId={gatehouseRecord->r_RecordGlobalId}, recordSubjectGlobalId={gatehouseRecord->r_SubjectGlobalId}, " +
                $"connectionClass={(int)gatehouseRecord->r_ConnectionClass}, " +
                $"entry=({gatehouseRecord->r_EntryTilePositionX},{gatehouseRecord->r_EntryTilePositionY})/" +
                $"{gatehouseRecord->r_EntryTileId}, exit=({gatehouseRecord->r_ExitTilePositionX}," +
                $"{gatehouseRecord->r_ExitTilePositionY})/{gatehouseRecord->r_ExitTileId}, " +
                $"components=[{gatehouseRecord->r_PathComponentA},{gatehouseRecord->r_PathComponentB}," +
                $"{gatehouseRecord->r_PathComponentC}].");
            LogDiagnostic(
                $"GATE_REACH_DIAG routes: sourcePcl={evaluation.SourcePcl}, entryPcl={evaluation.EntryPcl}, " +
                $"exitPcl={evaluation.ExitPcl}, entryResult={evaluation.EntryResult}, " +
                $"exitResult={evaluation.ExitResult}, directReachable={evaluation.DirectReachable}, " +
                $"groupReachable={evaluation.GroupReachable}, acceptedDrawbridges={evaluation.Drawbridges.Count}, " +
                $"groupComponents=[{evaluation.GroupComponents}], groupSignature={evaluation.SynchronizedGroupSignature}, " +
                $"drawbridgeStateSignature={evaluation.DrawbridgeDiagnosticSignature}.");

            LogDrawbridgeDiagnostics(
                gatehouseBuilding->r_PlayerIdOwner,
                unit,
                evaluation.GatehousePortal);
        }

        private void LogDrawbridgeDiagnostics(
            int ownerId,
            GameUnit* unit,
            GatePortalSnapshot gatehousePortal)
        {
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            int found = 0;
            int accepted = 0;
            for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
            {
                ref GameBuilding drawbridge = ref buildings[spanIndex];
                if (drawbridge.r_AliveState != AliveState.IsAlive ||
                    drawbridge.r_BuildingType != eStructs.STRUCT_DRAWBRIDGE ||
                    drawbridge.r_PlayerIdOwner != ownerId)
                {
                    continue;
                }

                found++;
                int buildingId = spanIndex + 1;
                string reason;
                string recordDescription;
                if (!GamePathingManagerAPI.Instance.TryGetPathConnectionRecordByBuildingId(
                        buildingId,
                        out PathConnectionRecord* record) ||
                    record == null)
                {
                    reason = "missing-path-record";
                    recordDescription = "record=none";
                }
                else
                {
                    reason = EvaluateDrawbridgeDiagnostic(
                        buildingId,
                        drawbridge,
                        record,
                        unit,
                        gatehousePortal,
                        ref accepted);
                    recordDescription =
                        $"recordActive={record->r_IsActive}, recordOpen={record->r_IsEnabledOrOpen}, " +
                        $"recordBuildingId={record->r_BuildingId}, recordGlobalId={record->r_RecordGlobalId}, " +
                        $"recordSubjectGlobalId={record->r_SubjectGlobalId}, " +
                        $"connectionClass={(int)record->r_ConnectionClass}, " +
                        $"entry=({record->r_EntryTilePositionX},{record->r_EntryTilePositionY})/{record->r_EntryTileId}, " +
                        $"exit=({record->r_ExitTilePositionX},{record->r_ExitTilePositionY})/{record->r_ExitTileId}, " +
                        $"components=[{record->r_PathComponentA},{record->r_PathComponentB},{record->r_PathComponentC}]";
                }

                LogDiagnostic(
                    $"GATE_REACH_DIAG drawbridge: buildingId={buildingId}, globalId={drawbridge.r_GlobalId}, " +
                    $"owner={drawbridge.r_PlayerIdOwner}, begin=({drawbridge.r_TilePositionXBegin}," +
                    $"{drawbridge.r_TilePositionYBegin}), end=({drawbridge.r_TilePositionXEnd}," +
                    $"{drawbridge.r_TilePositionYEnd}), occupyGridSize={drawbridge.r_OccupyTileGridSize}, " +
                    $"aiWalkableState={(int)drawbridge.r_AIWalkableState}, decision={reason}, {recordDescription}.");
            }

            if (found == 0)
                LogDiagnostic($"GATE_REACH_DIAG drawbridge: owner={ownerId}, result=no-live-owned-drawbridges.");
        }

        private static string EvaluateDrawbridgeDiagnostic(
            int buildingId,
            GameBuilding drawbridge,
            PathConnectionRecord* record,
            GameUnit* unit,
            GatePortalSnapshot gatehousePortal,
            ref int accepted)
        {
            if (record->r_IsActive == 0)
                return "rejected-inactive-record";
            if (record->r_BuildingId != buildingId)
                return "rejected-record-building-id";
            if (drawbridge.r_GlobalId == 0 || record->r_SubjectGlobalId != drawbridge.r_GlobalId)
                return "rejected-global-id";
            if (unit == null || !GamePathingManagerAPI.Instance.CanUnitTypeUsePathConnectionClass(
                    unit->r_UnitChimp,
                    record->r_ConnectionClass))
            {
                return "rejected-connection-class";
            }

            if (!TryCreatePortalSnapshot(
                    *record,
                    GameTileManagerAPI.Instance,
                    out GatePortalSnapshot bridgePortal))
            {
                return "rejected-invalid-portal";
            }

            DrawbridgeAssociationResult association =
                SynchronizedGatehouseReachabilityPolicy.EvaluateAssociation(
                    gatehousePortal,
                    bridgePortal);
            if (association != DrawbridgeAssociationResult.Accepted)
                return "rejected-" + association.ToString();

            accepted++;
            return accepted <= SynchronizedGatehouseReachabilityPolicy.MaximumSynchronizedDrawbridges
                ? "accepted-by-current-heuristic"
                : "accepted-but-overflow";
        }

        private static string FormatGroupComponents(
            GatePortalSnapshot gatehouse,
            IReadOnlyList<GatePortalSnapshot> drawbridges)
        {
            var components = new HashSet<int>();
            AddPortalComponents(components, gatehouse);
            for (int index = 0; index < drawbridges.Count; index++)
                AddPortalComponents(components, drawbridges[index]);

            var sorted = new List<int>(components);
            sorted.Sort();
            return string.Join(",", sorted);
        }

        private static void AddPortalComponents(HashSet<int> components, GatePortalSnapshot portal)
        {
            if (portal.FirstPcl > 0)
                components.Add(portal.FirstPcl);
            if (portal.SecondPcl > 0)
                components.Add(portal.SecondPcl);
            if (portal.ThirdPcl > 0)
                components.Add(portal.ThirdPcl);
        }

        private static string FormatDecision(bool? decision) =>
            decision.HasValue ? decision.Value.ToString() : "null";

        private void LogDiagnostic(string message) =>
            Shared.DebugLogHelper.LogDebug(log, message);

        private void ClearCache()
        {
            reachabilityCache.Clear();
            diagnosticThrottle.Clear();
            lastCacheTick = int.MinValue;
        }

        private void LogFailure(string message)
        {
            if (failureLogs >= MaximumFailureLogs)
                return;
            failureLogs++;
            LogWarning($"{message}. Vanilla remains authoritative ({failureLogs}/{MaximumFailureLogs}).");
        }

        private void LogInfo(string message) =>
            log.LogInfo($"[{TimestampNow()}] Bugfixes and QoL {message}");
        private void LogWarning(string message) =>
            log.LogWarning($"[{TimestampNow()}] Bugfixes and QoL {message}");
        private static string TimestampNow() =>
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

        private sealed class ReachabilityEvaluation
        {
            internal int Tick { get; set; }
            internal int SourceTileId { get; set; }
            internal int SourcePcl { get; set; }
            internal int EntryPcl { get; set; }
            internal int ExitPcl { get; set; }
            internal int Mode { get; set; }
            internal int EntryResult { get; set; }
            internal int ExitResult { get; set; }
            internal int SynchronizedGroupSignature { get; set; }
            internal int DrawbridgeDiagnosticSignature { get; set; }
            internal bool CacheHit { get; set; }
            internal bool DirectReachable { get; set; }
            internal bool GroupReachable { get; set; }
            internal bool Reachable { get; set; }
            internal string FailureStage { get; set; } = "none";
            internal string GroupComponents { get; set; } = string.Empty;
            internal GatePortalSnapshot GatehousePortal { get; set; }
            internal List<GatePortalSnapshot> Drawbridges { get; set; } =
                new List<GatePortalSnapshot>();
        }

        private readonly struct ReachabilityKey : IEquatable<ReachabilityKey>
        {
            public ReachabilityKey(
                int playerId,
                int sourcePcl,
                int entryPcl,
                int exitPcl,
                int mode,
                int synchronizedGroupSignature)
            {
                PlayerId = playerId;
                SourcePcl = sourcePcl;
                EntryPcl = entryPcl;
                ExitPcl = exitPcl;
                Mode = mode;
                SynchronizedGroupSignature = synchronizedGroupSignature;
            }

            private int PlayerId { get; }
            private int SourcePcl { get; }
            private int EntryPcl { get; }
            private int ExitPcl { get; }
            private int Mode { get; }
            private int SynchronizedGroupSignature { get; }

            public bool Equals(ReachabilityKey other) =>
                PlayerId == other.PlayerId && SourcePcl == other.SourcePcl &&
                EntryPcl == other.EntryPcl && ExitPcl == other.ExitPcl &&
                Mode == other.Mode &&
                SynchronizedGroupSignature == other.SynchronizedGroupSignature;
            public override bool Equals(object obj) => obj is ReachabilityKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = PlayerId;
                    hash = hash * 397 ^ SourcePcl;
                    hash = hash * 397 ^ EntryPcl;
                    hash = hash * 397 ^ ExitPcl;
                    hash = hash * 397 ^ Mode;
                    return hash * 397 ^ SynchronizedGroupSignature;
                }
            }
        }
    }
}
