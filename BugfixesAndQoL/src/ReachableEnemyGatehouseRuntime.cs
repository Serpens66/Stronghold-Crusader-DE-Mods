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
        private const int MaximumSaneFootprintSize = 512;

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly Dictionary<ReachabilityKey, bool> reachabilityCache =
            new Dictionary<ReachabilityKey, bool>();
        private IDisposable gatehouseQuerySubscription;
        private bool reachabilityAvailable;
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
                if (!TryGetLiveGatehouse(
                        args.BuildingId,
                        out GameBuilding* building,
                        out PathConnectionRecord* gatehouse))
                {
                    return;
                }

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
                args.ShouldClose = Shared.GatehouseQueryUnitIdPolicy.ResolveCandidateDecision(
                    args.ShouldClose,
                    vanillaCandidateCanClose);
                if (args.ShouldClose != true)
                    return;

                bool evaluationAvailable = TryIsUnitReachableToGate(
                    unitId,
                    unit,
                    args.BuildingId,
                    building,
                    gatehouse,
                    out bool reachable);
                if (evaluationAvailable && !reachable)
                    args.ShouldClose = false;
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
            int gatehouseBuildingId,
            GameBuilding* gatehouseBuilding,
            PathConnectionRecord* gatehouse,
            out bool reachable)
        {
            reachable = true;
            if (unitId <= 0 || gatehouseBuildingId <= 0 || gatehouseBuilding == null ||
                gatehouse == null || unit == null ||
                unit->r_AliveState != AliveState.IsAlive || unit->r_CurrentHealth == 0 ||
                unit->r_ControllableForPlayerId <= 0)
            {
                return false;
            }

            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            GamePathingManagerAPI pathingApi = GamePathingManagerAPI.Instance;
            int sourceTileId = (int)unit->r_CurrentPositionTileId;
            int entryTileId = (int)gatehouse->r_EntryTileId;
            int exitTileId = (int)gatehouse->r_ExitTileId;
            if (!tileApi.IsValidTileId(sourceTileId) || !tileApi.IsValidTileId(entryTileId) ||
                !tileApi.IsValidTileId(exitTileId) ||
                !TryValidateGateEndpoint(gatehouse->r_EntryTilePositionX, gatehouse->r_EntryTilePositionY,
                    entryTileId, tileApi) ||
                !TryValidateGateEndpoint(gatehouse->r_ExitTilePositionX, gatehouse->r_ExitTilePositionY,
                    exitTileId, tileApi))
            {
                return false;
            }

            Span<ushort> pathConnections = pathingApi.GetPathComponentGrid();
            if ((uint)sourceTileId >= (uint)pathConnections.Length ||
                (uint)entryTileId >= (uint)pathConnections.Length ||
                (uint)exitTileId >= (uint)pathConnections.Length)
            {
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
            if (sourcePcl <= 0 || entryPcl <= 0 || exitPcl <= 0)
                return false;

            if (!TryCollectSynchronizedDrawbridges(
                    gatehouseBuilding,
                    tileApi,
                    out List<SynchronizedDrawbridgeSnapshot> drawbridges,
                    out int synchronizedGroupSignature))
            {
                return false;
            }

            var key = new ReachabilityKey(
                playerId,
                sourcePcl,
                entryPcl,
                exitPcl,
                mode,
                synchronizedGroupSignature);
            if (reachabilityCache.TryGetValue(key, out reachable))
                return true;

            PathConnectionQueryMode queryMode = (PathConnectionQueryMode)mode;
            int entryResult = FindRoute(
                pathingApi, playerId, sourcePcl, entryPcl, queryMode);
            int exitResult = entryResult != 0
                ? 0
                : FindRoute(pathingApi, playerId, sourcePcl, exitPcl, queryMode);
            reachable = entryResult != 0 || exitResult != 0;
            if (!reachable && drawbridges.Count > 0)
            {
                reachable = SynchronizedGatehouseReachabilityPolicy.CanReachAnyExteriorApproach(
                    drawbridges,
                    component => FindRoute(
                        pathingApi,
                        playerId,
                        sourcePcl,
                        component,
                        queryMode) != 0);
            }

            reachabilityCache[key] = reachable;
            return true;
        }

        private static bool TryCollectSynchronizedDrawbridges(
            GameBuilding* gatehouseBuilding,
            GameTileManagerAPI tileApi,
            out List<SynchronizedDrawbridgeSnapshot> drawbridges,
            out int signature)
        {
            drawbridges = new List<SynchronizedDrawbridgeSnapshot>(
                SynchronizedGatehouseReachabilityPolicy.MaximumSynchronizedDrawbridges);
            signature = 17;
            if (gatehouseBuilding == null || gatehouseBuilding->r_OccupyTileGridSize == 0 ||
                gatehouseBuilding->r_OccupyTileGridSize > MaximumSaneFootprintSize)
                return false;

            List<VanillaFootprintCandidate> candidates =
                SynchronizedGatehouseReachabilityPolicy.BuildOrderedFootprintCandidates(
                    gatehouseBuilding->r_TilePositionXBegin,
                    gatehouseBuilding->r_TilePositionYBegin,
                    (int)gatehouseBuilding->r_OccupyTileGridSize);
            int GetBuildingIdAt(int x, int y)
            {
                if (!tileApi.IsTileInsideMapBounds(x, y))
                    return 0;
                int tileId = tileApi.GetTileId(x, y);
                return tileApi.IsValidTileId(tileId) ? tileApi.GetTileBuildingId(tileId) : 0;
            }

            bool IsEligibleDrawbridge(int buildingId) =>
                TryGetLiveDrawbridge(buildingId, out _);

            List<int> selectedIds =
                SynchronizedGatehouseReachabilityPolicy.CollectFirstDistinctBuildingIds(
                    candidates,
                    GetBuildingIdAt,
                    IsEligibleDrawbridge);

            for (int selectedIndex = 0; selectedIndex < selectedIds.Count; selectedIndex++)
            {
                int buildingId = selectedIds[selectedIndex];
                if (!TryGetLiveDrawbridge(buildingId, out GameBuilding* drawbridge) ||
                    drawbridge == null || drawbridge->r_GlobalId == 0)
                    return false;

                var snapshot = new SynchronizedDrawbridgeSnapshot(
                    buildingId,
                    drawbridge->r_GlobalId);
                var seenApproaches = new HashSet<long>();
                bool foundContact = false;
                for (int candidateIndex = 0; candidateIndex < candidates.Count; candidateIndex++)
                {
                    VanillaFootprintCandidate candidate = candidates[candidateIndex];
                    if (GetBuildingIdAt(candidate.X, candidate.Y) != buildingId)
                        continue;

                    foundContact = true;
                    bool traced = SynchronizedGatehouseReachabilityPolicy.TryTraceExteriorApproach(
                        candidate,
                        buildingId,
                        tileApi.IsTileInsideMapBounds,
                        GetBuildingIdAt,
                        tileApi.GetTileId,
                        tileId =>
                        {
                            Span<ushort> currentComponents =
                                GamePathingManagerAPI.Instance.GetPathComponentGrid();
                            return (uint)tileId < (uint)currentComponents.Length
                                ? currentComponents[tileId]
                                : 0;
                        },
                        out DrawbridgeApproachSnapshot approach);
                    if (!traced)
                        continue;

                    long approachKey = ((long)(uint)approach.ExteriorTileId << 32) |
                        (uint)approach.ExteriorPcl;
                    if (seenApproaches.Add(approachKey))
                        snapshot.Approaches.Add(approach);
                }

                if (!foundContact || snapshot.Approaches.Count == 0)
                    return false;

                drawbridges.Add(snapshot);
            }

            signature = SynchronizedGatehouseReachabilityPolicy.ComputeDrawbridgeSignature(
                drawbridges);
            return true;
        }

        private static int FindRoute(
            GamePathingManagerAPI pathingApi,
            int playerId,
            int sourcePcl,
            int destinationPcl,
            PathConnectionQueryMode queryMode) =>
            sourcePcl == destinationPcl
                ? sourcePcl
                : pathingApi.FindNextComponentTowardDestination(
                    playerId,
                    sourcePcl,
                    destinationPcl,
                    queryMode);

        private static bool TryGetLiveDrawbridge(
            int buildingId,
            out GameBuilding* drawbridge)
        {
            drawbridge = null;
            return buildingId > 0 &&
                GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out drawbridge) &&
                drawbridge != null && drawbridge->r_AliveState == AliveState.IsAlive &&
                drawbridge->r_BuildingType == eStructs.STRUCT_DRAWBRIDGE;
        }

        private static bool TryValidateGateEndpoint(
            int x,
            int y,
            int expectedTileId,
            GameTileManagerAPI tileApi) =>
            tileApi.IsTileInsideMapBounds(x, y) &&
            tileApi.GetTileId(x, y) == expectedTileId;

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
                GamePathingManagerAPI.Instance.TryGetPathConnectionRecordByBuildingId(
                    buildingId,
                    out gatehouse) &&
                gatehouse != null && gatehouse->r_BuildingId == buildingId &&
                gatehouse->r_SubjectGlobalId == building->r_GlobalId;
        }

        private void ClearCache()
        {
            reachabilityCache.Clear();
            lastCacheTick = int.MinValue;
        }

        private void LogFailure(string message)
        {
            if (failureLogs >= MaximumFailureLogs)
                return;
            failureLogs++;
            LogWarning($"{message}. Vanilla remains authoritative ({failureLogs}/{MaximumFailureLogs}).");
        }

        private void LogWarning(string message) =>
            log.LogWarning($"[{TimestampNow()}] Bugfixes and QoL {message}");
        private static string TimestampNow() =>
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

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
