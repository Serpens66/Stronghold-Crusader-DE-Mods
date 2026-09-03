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

            // SE 1.42.0 emits a zero-based span index in UnitId. Keep the conversion
            // isolated here so the documented one-based game-ID boundary stays explicit.
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
                if (!TryGetLiveGatehouse(args.BuildingId, out GameBuilding* building, out GameGatehouseEntry* gatehouse))
                    return;

                int unitSpanIndex = args.UnitId;
                Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
                if (!Shared.GatehouseQueryUnitIdPolicy.TryConvertSpanIndexToGameId(
                        unitSpanIndex,
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

                if (!firstQueryLogged)
                {
                    firstQueryLogged = true;
                    LogInfo(
                        $"gatehouse reachability query confirmed: buildingId={args.BuildingId}, " +
                        $"rawUnitSpanIndex={unitSpanIndex}, unitId={unitId}, globalId={building->r_GlobalId}.");
                }

                if (TryIsUnitReachableToGate(unitId, unit, gatehouse, out bool reachable) && !reachable)
                    args.ShouldClose = false;
            }
            catch (Exception ex)
            {
                LogFailure(
                    $"gatehouse reachability query failed: buildingId={args.BuildingId}, " +
                    $"rawUnitSpanIndex={args.UnitId}, error={ex}");
            }
        }

        private bool TryIsUnitReachableToGate(
            int unitId,
            GameUnit* unit,
            GameGatehouseEntry* gatehouse,
            out bool reachable)
        {
            reachable = true;
            if (unitId <= 0 || gatehouse == null || unit == null ||
                unit->r_AliveState != AliveState.IsAlive || unit->r_CurrentHealth == 0 ||
                unit->r_ControllableForPlayerId <= 0)
            {
                return false;
            }

            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            int sourceTileId = (int)unit->r_CurrentPositionTileId;
            int entryTileId = (int)gatehouse->r_EntryDoorTileId;
            int exitTileId = (int)gatehouse->r_ExitDoorTileId;
            if (!tileApi.IsValidTileId(sourceTileId) || !tileApi.IsValidTileId(entryTileId) ||
                !tileApi.IsValidTileId(exitTileId))
            {
                return false;
            }

            Span<ushort> pathConnections = tileApi.TileManager.PathConnectionGrid;
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
            int mode = unit->N000001CA;
            var key = new ReachabilityKey(playerId, sourcePcl, entryPcl, exitPcl, mode);
            if (reachabilityCache.TryGetValue(key, out reachable))
                return true;

            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            int entryResult = playerApi.GetNextReachablePCLToDestinationForPlayer(
                playerId,
                entryPcl,
                sourcePcl,
                mode);
            int exitResult = entryResult != 0
                ? entryResult
                : playerApi.GetNextReachablePCLToDestinationForPlayer(
                    playerId,
                    exitPcl,
                    sourcePcl,
                    mode);
            reachable = entryResult != 0 || exitResult != 0;
            reachabilityCache[key] = reachable;
            return true;
        }

        private static bool TryGetLiveGatehouse(
            int buildingId,
            out GameBuilding* building,
            out GameGatehouseEntry* gatehouse)
        {
            building = null;
            gatehouse = null;
            GameBuildingManagerAPI api = GameBuildingManagerAPI.Instance;
            return buildingId > 0 &&
                api.TryGetBuildingById(buildingId, out building) && building != null &&
                building->r_AliveState == AliveState.IsAlive &&
                api.TryGetGatehouseEntryById(buildingId, out gatehouse) && gatehouse != null &&
                gatehouse->r_BuildingId == (uint)buildingId &&
                gatehouse->r_GlobalId == building->r_GlobalId;
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

        private void LogInfo(string message) =>
            log.LogInfo($"[{TimestampNow()}] Bugfixes and QoL {message}");
        private void LogWarning(string message) =>
            log.LogWarning($"[{TimestampNow()}] Bugfixes and QoL {message}");
        private static string TimestampNow() =>
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

        private readonly struct ReachabilityKey : IEquatable<ReachabilityKey>
        {
            public ReachabilityKey(int playerId, int sourcePcl, int entryPcl, int exitPcl, int mode)
            {
                PlayerId = playerId;
                SourcePcl = sourcePcl;
                EntryPcl = entryPcl;
                ExitPcl = exitPcl;
                Mode = mode;
            }

            private int PlayerId { get; }
            private int SourcePcl { get; }
            private int EntryPcl { get; }
            private int ExitPcl { get; }
            private int Mode { get; }

            public bool Equals(ReachabilityKey other) =>
                PlayerId == other.PlayerId && SourcePcl == other.SourcePcl &&
                EntryPcl == other.EntryPcl && ExitPcl == other.ExitPcl && Mode == other.Mode;
            public override bool Equals(object obj) => obj is ReachabilityKey other && Equals(other);

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = PlayerId;
                    hash = hash * 397 ^ SourcePcl;
                    hash = hash * 397 ^ EntryPcl;
                    hash = hash * 397 ^ ExitPcl;
                    return hash * 397 ^ Mode;
                }
            }
        }
    }
}
