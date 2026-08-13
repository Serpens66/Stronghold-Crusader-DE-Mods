using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Zhuqiaomon.Memory;

namespace ImprovedHunters
{
    /// <summary>
    /// Temporary, behavior-neutral diagnostics for Hunter/chicken visibility and
    /// movement failures. This deliberately uses only established Script Extender
    /// events and the runtime's existing safe unit scan; no native hook is installed.
    /// </summary>
    internal sealed unsafe class HunterVisibilityDiagnostic : IDisposable
    {
        private const int HunterPathStateOffset = 0xF2;
        private const int HunterPathState2Offset = 0xF4;
        private const int HunterLastCommandOffset = 0x398;
        private const int HunterOrderBlockedOffset = 0x3FE;
        private const int HunterTargetUnitIdOffset = 0x39A;
        private const int HunterTargetGlobalIdOffset = 0x39C;
        private const int ChickenReservationOffset = 0x448;
        private const int WaitingHunterAiState = 0x06;
        private const int MaxWaitingLogs = 80;
        private const int MaxProjectileLogs = 80;
        private const int MaxLineTiles = 160;
        private static readonly long RecentTargetLifetime = Stopwatch.Frequency * 10;
        private static readonly long WaitingRepeatInterval = Stopwatch.Frequency * 2;

        private readonly ManualLogSource log;
        private readonly ImprovedHuntersViewModel settings;
        private readonly object diagnosticStateLock = new object();
        private readonly Dictionary<int, RecentChickenTarget> recentAssignedTargets =
            new Dictionary<int, RecentChickenTarget>();
        private readonly Dictionary<int, RecentChickenTarget> recentAcceptedTargets =
            new Dictionary<int, RecentChickenTarget>();
        private readonly Dictionary<int, RecentProjectile> recentProjectiles =
            new Dictionary<int, RecentProjectile>();
        private readonly Dictionary<int, WaitingObservation> waitingObservations =
            new Dictionary<int, WaitingObservation>();
        private int waitingLogs;
        private int projectileLogs;
        private bool actorMatchLogged;
        private bool actorMissLogged;
        private bool scanConfirmedLogged;
        private bool scanFailureLogged;
        private bool projectileFailureLogged;
        private bool disposed;

        private readonly struct RecentChickenTarget
        {
            public readonly int UnitId;
            public readonly uint GlobalId;
            public readonly long Timestamp;
            public readonly string Source;

            public RecentChickenTarget(int unitId, uint globalId, long timestamp, string source)
            {
                UnitId = unitId;
                GlobalId = globalId;
                Timestamp = timestamp;
                Source = source;
            }
        }

        private readonly struct RecentProjectile
        {
            public readonly uint ChickenGlobalId;
            public readonly long Timestamp;

            public RecentProjectile(uint chickenGlobalId, long timestamp)
            {
                ChickenGlobalId = chickenGlobalId;
                Timestamp = timestamp;
            }
        }

        private readonly struct WaitingObservation
        {
            public readonly uint ChickenGlobalId;
            public readonly ushort HunterTileX;
            public readonly ushort HunterTileY;
            public readonly ushort ChickenTileX;
            public readonly ushort ChickenTileY;
            public readonly long Timestamp;

            public WaitingObservation(
                uint chickenGlobalId,
                ushort hunterTileX,
                ushort hunterTileY,
                ushort chickenTileX,
                ushort chickenTileY,
                long timestamp)
            {
                ChickenGlobalId = chickenGlobalId;
                HunterTileX = hunterTileX;
                HunterTileY = hunterTileY;
                ChickenTileX = chickenTileX;
                ChickenTileY = chickenTileY;
                Timestamp = timestamp;
            }
        }

        public HunterVisibilityDiagnostic(ManualLogSource log, ImprovedHuntersViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Shared.DebugLogHelper.LogInfo(
                log,
                "Improved Hunters temporary visibility diagnostic initialized: " +
                "mode=safe-event-and-unit-scan, nativeHooks=False, behaviorNeutral=True.");
        }

        public bool IsAvailable => !disposed;

        public void RecordActorResolution(
            int reportedHunterUnitId,
            int reconstructedHunterUnitId,
            int queryUnitId,
            bool captureMatched)
        {
            if (disposed || !settings.EnableMod)
                return;

            if (captureMatched)
            {
                if (actorMatchLogged)
                    return;

                actorMatchLogged = true;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Hunters visibility diagnostic actor capture confirmed: " +
                    $"reported={reportedHunterUnitId}, reconstructed={reconstructedHunterUnitId}, " +
                    $"query={queryUnitId}, idsMatch={reportedHunterUnitId == reconstructedHunterUnitId}.");
                return;
            }

            if (actorMissLogged)
                return;

            actorMissLogged = true;
            Shared.DebugLogHelper.LogWarning(
                log,
                $"Improved Hunters visibility diagnostic observed a query without matching native actor capture: " +
                $"reported={reportedHunterUnitId}, query={queryUnitId}; runtime validation decides whether Vanilla is left unchanged.");
        }

        public void RecordAcceptedChickenTarget(int hunterUnitId, int chickenUnitId, uint chickenGlobalId)
        {
            RecordRecentTarget(recentAcceptedTargets, hunterUnitId, chickenUnitId, chickenGlobalId, "accepted-query");
        }

        public void RecordAssignedChickenTarget(int hunterUnitId, int chickenUnitId, uint chickenGlobalId)
        {
            RecordRecentTarget(recentAssignedTargets, hunterUnitId, chickenUnitId, chickenGlobalId, "native-assigned-target");
        }

        public void RecordProjectileSpawn(
            int hunterUnitId,
            int chickenUnitId,
            uint chickenGlobalId,
            long projectileReturnValue,
            string hunterSource)
        {
            if (disposed || !settings.EnableMod || !settings.HuntChicken)
                return;

            lock (diagnosticStateLock)
            {
                recentProjectiles[hunterUnitId] = new RecentProjectile(
                    chickenGlobalId,
                    Stopwatch.GetTimestamp());
            }

            if (projectileLogs >= MaxProjectileLogs)
                return;

            try
            {
                GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
                if (!unitApi.TryGetUnitById(hunterUnitId, out GameUnit* hunter) ||
                    hunter == null ||
                    hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER ||
                    !TryResolveChicken(unitApi, chickenUnitId, chickenGlobalId, out GameUnit* chicken))
                {
                    return;
                }

                projectileLogs++;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Hunters visibility projectile path accepted: " +
                    $"hunter={hunterUnitId}/{hunter->r_GlobalId}, chicken={chickenUnitId}/{chickenGlobalId}, " +
                    $"hunterSource={hunterSource}, projectileReturnValue={projectileReturnValue}, " +
                    $"hunterPosition={DescribeUnitPosition(hunter)}, chickenPosition={DescribeUnitPosition(chicken)}, " +
                    $"{DescribeLineContext(hunter, chicken)} ({projectileLogs}/{MaxProjectileLogs}).");
            }
            catch (Exception exception)
            {
                if (projectileFailureLogged)
                    return;

                projectileFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters visibility projectile diagnostic failed; behavior is unchanged: {exception}");
            }
        }

        public void ProcessNativeScan(SimpleNativeArray<GameUnit> units, long timestamp)
        {
            if (disposed ||
                !settings.EnableMod ||
                !settings.HuntChicken ||
                waitingLogs >= MaxWaitingLogs ||
                units._array == null ||
                units.Length == 0)
            {
                return;
            }

            try
            {
                GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
                for (int index = 0; index < units.Length && waitingLogs < MaxWaitingLogs; index++)
                {
                    GameUnit* hunter = units.GetValuePointer(index);
                    if (hunter == null ||
                        hunter->r_AliveState == AliveState.None ||
                        hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER)
                    {
                        continue;
                    }

                    byte* hunterBytes = (byte*)hunter;
                    ushort aiState = *(ushort*)(hunterBytes + 0x2BC);
                    if (aiState != WaitingHunterAiState)
                        continue;

                    int hunterUnitId = index + 1;
                    if (!TryResolveDiagnosticChicken(
                            hunterUnitId,
                            hunter,
                            unitApi,
                            timestamp,
                            out int chickenUnitId,
                            out uint expectedChickenGlobalId,
                            out long targetTimestamp,
                            out GameUnit* chicken,
                            out string targetSource))
                    {
                        continue;
                    }

                    if (!ShouldLogWaitingObservation(hunterUnitId, chicken, hunter, timestamp))
                        continue;

                    if (!scanConfirmedLogged)
                    {
                        scanConfirmedLogged = true;
                        Shared.DebugLogHelper.LogInfo(
                            log,
                            $"Improved Hunters visibility diagnostic safe scan confirmed: " +
                            $"hunter={hunterUnitId}, chicken={chickenUnitId}, targetSource={targetSource}.");
                    }

                    ushort nativeTargetUnitId = *(ushort*)(hunterBytes + HunterTargetUnitIdOffset);
                    uint nativeTargetGlobalId = *(uint*)(hunterBytes + HunterTargetGlobalIdOffset);
                    byte* chickenBytes = (byte*)chicken;
                    int distance = Math.Max(
                        Math.Abs(chicken->r_CurrentTilePositionX - hunter->r_CurrentTilePositionX),
                        Math.Abs(chicken->r_CurrentTilePositionY - hunter->r_CurrentTilePositionY));
                    string projectileContext = DescribeRecentProjectile(
                        hunterUnitId,
                        chicken->r_GlobalId,
                        timestamp);
                    string targetAge = targetTimestamp > 0
                        ? GetAgeMilliseconds(targetTimestamp, timestamp).ToString()
                        : "native";

                    waitingLogs++;
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Improved Hunters visibility waiting Hunter scan: " +
                        $"hunter={hunterUnitId}/{hunter->r_GlobalId}, aiState=0x{aiState:X}, " +
                        $"pathState={*(ushort*)(hunterBytes + HunterPathStateOffset)}, " +
                        $"pathState2={*(ushort*)(hunterBytes + HunterPathState2Offset)}, " +
                        $"lastCommand={*(ushort*)(hunterBytes + HunterLastCommandOffset)}, " +
                        $"orderBlocked={*(byte*)(hunterBytes + HunterOrderBlockedOffset)}, " +
                        $"nativeTarget={nativeTargetUnitId}/{nativeTargetGlobalId}, " +
                        $"diagnosticTarget={chickenUnitId}/{expectedChickenGlobalId}/{chicken->r_GlobalId}, " +
                        $"targetSource={targetSource}, targetAgeMs={targetAge}, " +
                        $"identityMatches={expectedChickenGlobalId == chicken->r_GlobalId}, " +
                        $"chickenOwner={chicken->r_ControllableForPlayerId}, color={chicken->r_SpritePlayerColorId}, " +
                        $"aliveState={(short)chicken->r_AliveState}, health={chicken->r_CurrentHealth}/{chicken->r_MaxHealth}, " +
                        $"reservation={*(ushort*)(chickenBytes + ChickenReservationOffset)}, distance={distance}, " +
                        $"{projectileContext}, hunterPosition={DescribeUnitPosition(hunter)}, " +
                        $"chickenPosition={DescribeUnitPosition(chicken)}, {DescribeLineContext(hunter, chicken)} " +
                        $"({waitingLogs}/{MaxWaitingLogs}).");
                }
            }
            catch (Exception exception)
            {
                if (scanFailureLogged)
                    return;

                scanFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Improved Hunters visibility safe-scan diagnostic failed; behavior is unchanged: {exception}");
            }
        }

        public void ResetForMap()
        {
            lock (diagnosticStateLock)
            {
                recentAssignedTargets.Clear();
                recentAcceptedTargets.Clear();
                recentProjectiles.Clear();
                waitingObservations.Clear();
            }

            waitingLogs = 0;
            projectileLogs = 0;
            actorMatchLogged = false;
            actorMissLogged = false;
            scanConfirmedLogged = false;
            scanFailureLogged = false;
            projectileFailureLogged = false;
        }

        private void RecordRecentTarget(
            Dictionary<int, RecentChickenTarget> targets,
            int hunterUnitId,
            int chickenUnitId,
            uint chickenGlobalId,
            string source)
        {
            if (disposed ||
                !settings.EnableMod ||
                !settings.HuntChicken ||
                hunterUnitId <= 0 ||
                chickenUnitId <= 0 ||
                chickenGlobalId == 0)
            {
                return;
            }

            lock (diagnosticStateLock)
            {
                targets[hunterUnitId] = new RecentChickenTarget(
                    chickenUnitId,
                    chickenGlobalId,
                    Stopwatch.GetTimestamp(),
                    source);
            }
        }

        private bool TryResolveDiagnosticChicken(
            int hunterUnitId,
            GameUnit* hunter,
            GameUnitManagerAPI unitApi,
            long timestamp,
            out int targetUnitId,
            out uint expectedTargetGlobalId,
            out long targetTimestamp,
            out GameUnit* chicken,
            out string targetSource)
        {
            targetUnitId = 0;
            expectedTargetGlobalId = 0;
            targetTimestamp = 0;
            chicken = null;
            targetSource = null;

            byte* hunterBytes = (byte*)hunter;
            ushort nativeTargetUnitId = *(ushort*)(hunterBytes + HunterTargetUnitIdOffset);
            uint nativeTargetGlobalId = *(uint*)(hunterBytes + HunterTargetGlobalIdOffset);
            if (TryResolveChicken(unitApi, nativeTargetUnitId, nativeTargetGlobalId, out chicken))
            {
                targetUnitId = nativeTargetUnitId;
                expectedTargetGlobalId = nativeTargetGlobalId;
                targetSource = "native-target";
                return true;
            }

            if (TryResolveRecentTarget(
                    recentAssignedTargets,
                    hunterUnitId,
                    unitApi,
                    timestamp,
                    out RecentChickenTarget recentAssigned,
                    out chicken))
            {
                targetUnitId = recentAssigned.UnitId;
                expectedTargetGlobalId = recentAssigned.GlobalId;
                targetTimestamp = recentAssigned.Timestamp;
                targetSource = recentAssigned.Source;
                return true;
            }

            if (TryResolveRecentTarget(
                    recentAcceptedTargets,
                    hunterUnitId,
                    unitApi,
                    timestamp,
                    out RecentChickenTarget recentAccepted,
                    out chicken))
            {
                targetUnitId = recentAccepted.UnitId;
                expectedTargetGlobalId = recentAccepted.GlobalId;
                targetTimestamp = recentAccepted.Timestamp;
                targetSource = recentAccepted.Source;
                return true;
            }

            return false;
        }

        private bool TryResolveRecentTarget(
            Dictionary<int, RecentChickenTarget> targets,
            int hunterUnitId,
            GameUnitManagerAPI unitApi,
            long timestamp,
            out RecentChickenTarget recent,
            out GameUnit* chicken)
        {
            recent = default;
            chicken = null;
            lock (diagnosticStateLock)
            {
                if (!targets.TryGetValue(hunterUnitId, out recent) ||
                    timestamp - recent.Timestamp > RecentTargetLifetime)
                {
                    targets.Remove(hunterUnitId);
                    return false;
                }
            }

            return TryResolveChicken(unitApi, recent.UnitId, recent.GlobalId, out chicken);
        }

        private bool ShouldLogWaitingObservation(
            int hunterUnitId,
            GameUnit* chicken,
            GameUnit* hunter,
            long timestamp)
        {
            lock (diagnosticStateLock)
            {
                if (waitingObservations.TryGetValue(hunterUnitId, out WaitingObservation previous) &&
                    previous.ChickenGlobalId == chicken->r_GlobalId &&
                    previous.HunterTileX == hunter->r_CurrentTilePositionX &&
                    previous.HunterTileY == hunter->r_CurrentTilePositionY &&
                    previous.ChickenTileX == chicken->r_CurrentTilePositionX &&
                    previous.ChickenTileY == chicken->r_CurrentTilePositionY &&
                    timestamp - previous.Timestamp < WaitingRepeatInterval)
                {
                    return false;
                }

                waitingObservations[hunterUnitId] = new WaitingObservation(
                    chicken->r_GlobalId,
                    hunter->r_CurrentTilePositionX,
                    hunter->r_CurrentTilePositionY,
                    chicken->r_CurrentTilePositionX,
                    chicken->r_CurrentTilePositionY,
                    timestamp);
                return true;
            }
        }

        private string DescribeRecentProjectile(int hunterUnitId, uint chickenGlobalId, long timestamp)
        {
            lock (diagnosticStateLock)
            {
                if (!recentProjectiles.TryGetValue(hunterUnitId, out RecentProjectile projectile) ||
                    projectile.ChickenGlobalId != chickenGlobalId ||
                    timestamp - projectile.Timestamp > RecentTargetLifetime)
                {
                    return "recentMatchingProjectile=none";
                }

                return $"recentMatchingProjectile=ageMs:{GetAgeMilliseconds(projectile.Timestamp, timestamp)}";
            }
        }

        private static bool TryResolveChicken(
            GameUnitManagerAPI unitApi,
            int unitId,
            uint globalId,
            out GameUnit* chicken)
        {
            chicken = null;
            return unitId > 0 &&
                globalId != 0 &&
                unitApi.TryGetUnitById(unitId, out chicken) &&
                chicken != null &&
                chicken->r_GlobalId == globalId &&
                chicken->r_UnitChimp == eChimps.CHIMP_TYPE_CHICKEN;
        }

        private static long GetAgeMilliseconds(long timestamp, long now)
        {
            long elapsed = Math.Max(0, now - timestamp);
            return elapsed * 1000 / Stopwatch.Frequency;
        }

        private static string DescribeUnitPosition(GameUnit* unit)
        {
            return $"tile:{unit->r_CurrentTilePositionX},{unit->r_CurrentTilePositionY}/" +
                $"world:{unit->r_CurrentWorldPositionX},{unit->r_CurrentWorldPositionY}/" +
                $"elevation:{unit->r_HeightElevation}/" +
                $"lookAt:{unit->r_LookAtWorldPositionX},{unit->r_LookAtWorldPositionY},{unit->r_LookAtHeight}";
        }

        private static string DescribeLineContext(GameUnit* hunter, GameUnit* chicken)
        {
            try
            {
                int startX = hunter->r_CurrentTilePositionX;
                int startY = hunter->r_CurrentTilePositionY;
                int endX = chicken->r_CurrentTilePositionX;
                int endY = chicken->r_CurrentTilePositionY;
                GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
                GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
                SortedDictionary<int, int> buildingTileCounts = new SortedDictionary<int, int>();
                int sampledTiles = 0;
                int minimumHeight = int.MaxValue;
                int maximumHeight = int.MinValue;

                int x = startX;
                int y = startY;
                int deltaX = Math.Abs(endX - startX);
                int stepX = startX < endX ? 1 : -1;
                int deltaY = -Math.Abs(endY - startY);
                int stepY = startY < endY ? 1 : -1;
                int error = deltaX + deltaY;

                while (sampledTiles < MaxLineTiles)
                {
                    if (tileApi.IsTileInsideMapBounds(x, y))
                    {
                        int tileId = tileApi.GetTileId(x, y);
                        int height = tileApi.GetTileHeight(tileId);
                        minimumHeight = Math.Min(minimumHeight, height);
                        maximumHeight = Math.Max(maximumHeight, height);
                        int buildingId = tileApi.GetTileBuildingId(tileId);
                        if (buildingId > 0)
                        {
                            buildingTileCounts.TryGetValue(buildingId, out int count);
                            buildingTileCounts[buildingId] = count + 1;
                        }
                    }

                    sampledTiles++;
                    if (x == endX && y == endY)
                        break;

                    int twiceError = error * 2;
                    if (twiceError >= deltaY)
                    {
                        error += deltaY;
                        x += stepX;
                    }

                    if (twiceError <= deltaX)
                    {
                        error += deltaX;
                        y += stepY;
                    }
                }

                StringBuilder buildings = new StringBuilder();
                foreach (KeyValuePair<int, int> pair in buildingTileCounts)
                {
                    if (buildings.Length > 0)
                        buildings.Append(';');

                    if (buildingApi.TryGetBuildingById(pair.Key, out GameBuilding* building) && building != null)
                    {
                        buildings.Append(pair.Key)
                            .Append('/')
                            .Append(building->r_BuildingType)
                            .Append("/owner:")
                            .Append(building->r_PlayerIdOwner)
                            .Append("/tiles:")
                            .Append(pair.Value)
                            .Append("/baseElevation:")
                            .Append(building->r_HeightElevation)
                            .Append("/bounds:")
                            .Append(building->r_TilePositionXBegin)
                            .Append(',')
                            .Append(building->r_TilePositionYBegin)
                            .Append('-')
                            .Append(building->r_TilePositionXEnd)
                            .Append(',')
                            .Append(building->r_TilePositionYEnd)
                            .Append("/grid:")
                            .Append(building->r_OccupyTileGridSize);
                    }
                    else
                    {
                        buildings.Append(pair.Key)
                            .Append("/unresolved/tiles:")
                            .Append(pair.Value);
                    }
                }

                string heightRange = minimumHeight == int.MaxValue
                    ? "none"
                    : $"{minimumHeight}-{maximumHeight}";
                return $"line=start:{startX},{startY}/end:{endX},{endY}/" +
                    $"sampledTiles:{sampledTiles}/terrainHeight:{heightRange}/" +
                    $"truncated:{x != endX || y != endY}/buildings:{(buildings.Length == 0 ? "none" : buildings.ToString())}";
            }
            catch (Exception exception)
            {
                return $"line=analysis-failed:{exception.GetType().Name}:{exception.Message}";
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            lock (diagnosticStateLock)
            {
                recentAssignedTargets.Clear();
                recentAcceptedTargets.Clear();
                recentProjectiles.Clear();
                waitingObservations.Clear();
            }
        }
    }
}
