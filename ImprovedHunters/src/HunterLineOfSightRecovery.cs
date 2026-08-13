using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Zhuqiaomon.Memory;

namespace ImprovedHunters
{
    /// <summary>
    /// Recovers Hunters that repeatedly abandon live chickens because a regular
    /// building blocks the firing line. Movement is issued through Vanilla's
    /// normal order path and only to a pathfinder-confirmed free tile.
    /// </summary>
    internal sealed unsafe class HunterLineOfSightRecovery : IDisposable
    {
        private const int HunterAiStateOffset = 0x2BC;
        private const ushort WaitingHunterAiState = 0x06;
        private const int RequiredBlockedAborts = 3;
        private const int CandidateSearchRadius = 8;
        private const int MinimumShotDistance = 3;
        private const int MaximumShotDistance = 20;
        private const int MaximumPathChecks = 8;
        private const int MaximumLogs = 80;
        private static readonly long AbortWindow = Stopwatch.Frequency * 3;
        private static readonly long RecentProjectileWindow = Stopwatch.Frequency * 2;
        private static readonly long RecoveryCooldown = Stopwatch.Frequency * 3;

        private readonly ManualLogSource log;
        private readonly ImprovedHuntersViewModel settings;
        private readonly Dictionary<int, AbortObservation> aborts = new Dictionary<int, AbortObservation>();
        private readonly Dictionary<int, long> lastProjectileTimestamps = new Dictionary<int, long>();
        private readonly Dictionary<int, long> recoveryCooldowns = new Dictionary<int, long>();
        private int logCount;
        private bool disposed;

        public HunterLineOfSightRecovery(ManualLogSource log, ImprovedHuntersViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Shared.DebugLogHelper.LogInfo(
                log,
                "Improved Hunters line-of-sight recovery initialized: blockedAborts=3, " +
                "candidateRadius=8, maximumShotDistance=20, nativeMoveOrders=True.");
        }

        public bool IsAvailable => !disposed;

        public void RecordProjectileSpawn(int hunterUnitId, long timestamp)
        {
            if (disposed || hunterUnitId <= 0)
                return;

            lastProjectileTimestamps[hunterUnitId] = timestamp;
            aborts.Remove(hunterUnitId);
        }

        public bool TryRecoverAfterTargetAbort(
            SimpleNativeArray<GameUnit> units,
            int hunterUnitId,
            int targetUnitId,
            uint targetGlobalId,
            long timestamp)
        {
            try
            {
                return TryRecoverAfterTargetAbortCore(
                    units,
                    hunterUnitId,
                    targetUnitId,
                    targetGlobalId,
                    timestamp);
            }
            catch (Exception exception)
            {
                LogRecovery(
                    $"Improved Hunters line-of-sight recovery failed safely: " +
                    $"hunter={hunterUnitId}, chicken={targetUnitId}/{targetGlobalId}, error={exception}",
                    warning: true);
                return false;
            }
        }

        private bool TryRecoverAfterTargetAbortCore(
            SimpleNativeArray<GameUnit> units,
            int hunterUnitId,
            int targetUnitId,
            uint targetGlobalId,
            long timestamp)
        {
            if (disposed ||
                !settings.EnableMod ||
                !settings.HuntChicken ||
                !settings.ImprovedPathfinding ||
                units._array == null ||
                hunterUnitId <= 0 ||
                hunterUnitId > units.Length ||
                targetUnitId <= 0 ||
                targetUnitId > units.Length)
            {
                return false;
            }

            if (lastProjectileTimestamps.TryGetValue(hunterUnitId, out long projectileTimestamp) &&
                timestamp - projectileTimestamp <= RecentProjectileWindow)
            {
                // A target transition shortly after a shot is normal and must not
                // be mistaken for the pre-shot visibility failure.
                aborts.Remove(hunterUnitId);
                return false;
            }

            GameUnit* hunter = units.GetValuePointer(hunterUnitId - 1);
            GameUnit* chicken = units.GetValuePointer(targetUnitId - 1);
            if (hunter == null ||
                hunter->r_AliveState != AliveState.IsAlive ||
                hunter->r_UnitChimp != eChimps.CHIMP_TYPE_HUNTER ||
                *(ushort*)((byte*)hunter + HunterAiStateOffset) != WaitingHunterAiState ||
                chicken == null ||
                chicken->r_GlobalId != targetGlobalId ||
                chicken->r_AliveState != AliveState.IsAlive ||
                chicken->r_UnitChimp != eChimps.CHIMP_TYPE_CHICKEN ||
                chicken->r_CurrentHealth == 0)
            {
                aborts.Remove(hunterUnitId);
                return false;
            }

            List<BlockingBuilding> blockers = FindVisibilityBlockers(hunter, chicken);
            if (blockers.Count == 0)
            {
                aborts.Remove(hunterUnitId);
                return false;
            }

            int blockedAbortCount = 1;
            long windowStartedAt = timestamp;
            if (aborts.TryGetValue(hunterUnitId, out AbortObservation previous) &&
                timestamp - previous.WindowStartedAt <= AbortWindow)
            {
                blockedAbortCount = previous.Count + 1;
                windowStartedAt = previous.WindowStartedAt;
            }

            aborts[hunterUnitId] = new AbortObservation(blockedAbortCount, windowStartedAt);
            if (blockedAbortCount < RequiredBlockedAborts ||
                recoveryCooldowns.TryGetValue(hunterUnitId, out long cooldownUntil) && timestamp < cooldownUntil)
            {
                return false;
            }

            aborts.Remove(hunterUnitId);
            recoveryCooldowns[hunterUnitId] = timestamp + RecoveryCooldown;
            if (!TryFindReachableFiringTile(
                    hunterUnitId,
                    hunter,
                    chicken,
                    out int destinationX,
                    out int destinationY,
                    out int pathLength,
                    out int candidatesConsidered))
            {
                LogRecovery(
                    $"Improved Hunters line-of-sight recovery found no reachable firing tile: " +
                    $"hunter={hunterUnitId}/{hunter->r_GlobalId}, chicken={targetUnitId}/{targetGlobalId}, " +
                    $"blockedAborts={blockedAbortCount}, blockers={DescribeBlockers(blockers)}, " +
                    $"hunterTile={hunter->r_CurrentTilePositionX},{hunter->r_CurrentTilePositionY}, " +
                    $"chickenTile={chicken->r_CurrentTilePositionX},{chicken->r_CurrentTilePositionY}, " +
                    $"candidatesConsidered={candidatesConsidered}.",
                    warning: true);
                return false;
            }

            int originX = hunter->r_CurrentTilePositionX;
            int originY = hunter->r_CurrentTilePositionY;
            GameUnitManagerAPI.Instance.MoveToTile(hunterUnitId, destinationX, destinationY);
            LogRecovery(
                $"Improved Hunters line-of-sight recovery issued Vanilla move: " +
                $"hunter={hunterUnitId}/{hunter->r_GlobalId}, chicken={targetUnitId}/{targetGlobalId}, " +
                $"blockedAborts={blockedAbortCount}, blockers={DescribeBlockers(blockers)}, " +
                $"origin={originX},{originY}, destination={destinationX},{destinationY}, " +
                $"pathLength={pathLength}, candidatesConsidered={candidatesConsidered}.");
            return true;
        }

        private static List<BlockingBuilding> FindVisibilityBlockers(GameUnit* hunter, GameUnit* chicken)
        {
            List<BlockingBuilding> result = new List<BlockingBuilding>();
            HashSet<int> seen = new HashSet<int>();
            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            int startX = hunter->r_CurrentTilePositionX;
            int startY = hunter->r_CurrentTilePositionY;
            int endX = chicken->r_CurrentTilePositionX;
            int endY = chicken->r_CurrentTilePositionY;

            VisitLine(startX, startY, endX, endY, (x, y, isStart, isEnd) =>
            {
                if (isStart || isEnd || !tileApi.IsTileInsideMapBounds(x, y))
                    return true;

                int buildingId = tileApi.GetTileBuildingId(tileApi.GetTileId(x, y));
                if (buildingId <= 0 || !seen.Add(buildingId))
                    return true;

                if (!buildingApi.TryGetBuildingById(buildingId, out GameBuilding* building) || building == null)
                {
                    result.Add(new BlockingBuilding(buildingId, eStructs.STRUCT_NULL));
                    return true;
                }

                // Vanilla already ignores Hunter huts for target visibility. They
                // remain forbidden in candidate firing lines below because arrows
                // can still collide with them physically.
                if (building->r_AliveState == AliveState.IsAlive &&
                    building->r_BuildingType != eStructs.STRUCT_HUNTERS_HUT)
                {
                    result.Add(new BlockingBuilding(buildingId, building->r_BuildingType));
                }

                return true;
            });

            return result;
        }

        private static bool TryFindReachableFiringTile(
            int hunterUnitId,
            GameUnit* hunter,
            GameUnit* chicken,
            out int destinationX,
            out int destinationY,
            out int pathLength,
            out int candidatesConsidered)
        {
            destinationX = 0;
            destinationY = 0;
            pathLength = 0;
            candidatesConsidered = 0;
            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            int hunterX = hunter->r_CurrentTilePositionX;
            int hunterY = hunter->r_CurrentTilePositionY;
            int targetX = chicken->r_CurrentTilePositionX;
            int targetY = chicken->r_CurrentTilePositionY;
            int pathOriginX = hunterX;
            int pathOriginY = hunterY;

            if (!IsWalkableCandidate(tileApi, pathOriginX, pathOriginY, hunterUnitId))
            {
                int originTileId = tileApi.IsTileInsideMapBounds(pathOriginX, pathOriginY)
                    ? tileApi.GetTileId(pathOriginX, pathOriginY)
                    : -1;
                int originBuildingId = originTileId >= 0 && tileApi.IsValidTileId(originTileId)
                    ? tileApi.GetTileBuildingId(originTileId)
                    : 0;
                if (originBuildingId == 0 ||
                    originBuildingId != hunter->r_LinkedProductionBuildingId ||
                    !GameBuildingManagerAPI.Instance.TryGetBuildingById(originBuildingId, out GameBuilding* originBuilding) ||
                    originBuilding == null ||
                    originBuilding->r_AliveState != AliveState.IsAlive ||
                    originBuilding->r_BuildingType != eStructs.STRUCT_HUNTERS_HUT)
                {
                    return false;
                }

                UnmanagedVector2<ushort> nearest = tileApi.GetNearestUnoccupiedTile(pathOriginX, pathOriginY, maxRange: 8);
                pathOriginX = nearest.X;
                pathOriginY = nearest.Y;
                if (!IsWalkableCandidate(tileApi, pathOriginX, pathOriginY, hunterUnitId))
                    return false;
            }

            List<FiringCandidate> candidates = new List<FiringCandidate>();
            for (int radius = 1; radius <= CandidateSearchRadius; radius++)
            {
                for (int offsetY = -radius; offsetY <= radius; offsetY++)
                {
                    for (int offsetX = -radius; offsetX <= radius; offsetX++)
                    {
                        if (Math.Max(Math.Abs(offsetX), Math.Abs(offsetY)) != radius)
                            continue;

                        int candidateX = hunterX + offsetX;
                        int candidateY = hunterY + offsetY;
                        int shotDistance = GetChebyshevDistance(candidateX, candidateY, targetX, targetY);
                        if (shotDistance < MinimumShotDistance ||
                            shotDistance > MaximumShotDistance ||
                            !IsWalkableCandidate(tileApi, candidateX, candidateY, hunterUnitId) ||
                            !HasBuildingFreeFiringLine(tileApi, candidateX, candidateY, targetX, targetY))
                        {
                            continue;
                        }

                        candidates.Add(new FiringCandidate(
                            candidateX,
                            candidateY,
                            radius,
                            shotDistance,
                            tileApi.GetTileId(candidateX, candidateY)));
                    }
                }
            }

            candidates.Sort(CompareCandidates);
            int checks = Math.Min(MaximumPathChecks, candidates.Count);
            for (int index = 0; index < checks; index++)
            {
                FiringCandidate candidate = candidates[index];
                candidatesConsidered++;
                if (candidate.X == pathOriginX && candidate.Y == pathOriginY)
                {
                    destinationX = candidate.X;
                    destinationY = candidate.Y;
                    pathLength = GetChebyshevDistance(hunterX, hunterY, candidate.X, candidate.Y);
                    return hunterX != candidate.X || hunterY != candidate.Y;
                }

                List<UnmanagedVector2<ushort>> path = tileApi.FindPath(
                    pathOriginX,
                    pathOriginY,
                    candidate.X,
                    candidate.Y);
                if (path == null || path.Count == 0)
                    continue;

                destinationX = candidate.X;
                destinationY = candidate.Y;
                pathLength = path.Count;
                return true;
            }

            return false;
        }

        private static bool IsWalkableCandidate(GameTileManagerAPI tileApi, int x, int y, int hunterUnitId)
        {
            if (!tileApi.IsTileInsideMapBounds(x, y))
                return false;

            int tileId = tileApi.GetTileId(x, y);
            if (!tileApi.IsValidTileId(tileId) || !tileApi.IsTileWalkableAndUnoccupied(tileId))
                return false;

            int occupyingUnitId = tileApi.GetTileUnitId(tileId);
            return occupyingUnitId == 0 || occupyingUnitId == hunterUnitId;
        }

        private static bool HasBuildingFreeFiringLine(
            GameTileManagerAPI tileApi,
            int startX,
            int startY,
            int endX,
            int endY)
        {
            bool clear = true;
            VisitLine(startX, startY, endX, endY, (x, y, isStart, isEnd) =>
            {
                if (isStart || isEnd)
                    return true;

                if (!tileApi.IsTileInsideMapBounds(x, y) ||
                    tileApi.GetTileBuildingId(tileApi.GetTileId(x, y)) != 0)
                {
                    clear = false;
                    return false;
                }

                return true;
            });
            return clear;
        }

        private static void VisitLine(
            int startX,
            int startY,
            int endX,
            int endY,
            Func<int, int, bool, bool, bool> visitor)
        {
            int x = startX;
            int y = startY;
            int deltaX = Math.Abs(endX - startX);
            int stepX = startX < endX ? 1 : -1;
            int deltaY = -Math.Abs(endY - startY);
            int stepY = startY < endY ? 1 : -1;
            int error = deltaX + deltaY;

            while (true)
            {
                bool isStart = x == startX && y == startY;
                bool isEnd = x == endX && y == endY;
                if (!visitor(x, y, isStart, isEnd) || isEnd)
                    return;

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
        }

        private static int CompareCandidates(FiringCandidate left, FiringCandidate right)
        {
            int movement = left.MovementDistance.CompareTo(right.MovementDistance);
            if (movement != 0)
                return movement;

            int shot = left.ShotDistance.CompareTo(right.ShotDistance);
            if (shot != 0)
                return shot;

            return left.TileId.CompareTo(right.TileId);
        }

        private static int GetChebyshevDistance(int x1, int y1, int x2, int y2)
        {
            return Math.Max(Math.Abs(x2 - x1), Math.Abs(y2 - y1));
        }

        private static string DescribeBlockers(List<BlockingBuilding> blockers)
        {
            if (blockers.Count == 0)
                return "none";

            List<string> descriptions = new List<string>(blockers.Count);
            for (int index = 0; index < blockers.Count; index++)
                descriptions.Add($"{blockers[index].BuildingId}/{blockers[index].Type}");

            return string.Join(";", descriptions);
        }

        private void LogRecovery(string message, bool warning = false)
        {
            if (logCount >= MaximumLogs)
                return;

            logCount++;
            string counted = $"{message} ({logCount}/{MaximumLogs}).";
            if (warning)
                Shared.DebugLogHelper.LogWarning(log, counted);
            else
                Shared.DebugLogHelper.LogInfo(log, counted);
        }

        public void ResetForMap()
        {
            aborts.Clear();
            lastProjectileTimestamps.Clear();
            recoveryCooldowns.Clear();
            logCount = 0;
        }

        public void Dispose()
        {
            disposed = true;
            ResetForMap();
        }

        private readonly struct AbortObservation
        {
            public readonly int Count;
            public readonly long WindowStartedAt;

            public AbortObservation(int count, long windowStartedAt)
            {
                Count = count;
                WindowStartedAt = windowStartedAt;
            }
        }

        private readonly struct BlockingBuilding
        {
            public readonly int BuildingId;
            public readonly eStructs Type;

            public BlockingBuilding(int buildingId, eStructs type)
            {
                BuildingId = buildingId;
                Type = type;
            }
        }

        private readonly struct FiringCandidate
        {
            public readonly int X;
            public readonly int Y;
            public readonly int MovementDistance;
            public readonly int ShotDistance;
            public readonly int TileId;

            public FiringCandidate(int x, int y, int movementDistance, int shotDistance, int tileId)
            {
                X = x;
                Y = y;
                MovementDistance = movementDistance;
                ShotDistance = shotDistance;
                TileId = tileId;
            }
        }
    }
}
