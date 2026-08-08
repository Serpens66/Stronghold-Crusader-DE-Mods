using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RandomEvents
{
    internal sealed unsafe class SignpostPlacementService : IDisposable
    {
        private const int MapCenter = 400;
        private const int GridSize = 800;
        private const int MaximumEdgeDepth = 10;
        private const double MinimumKeepDistance = 100.0;

        private readonly ManualLogSource log;
        private readonly ScenarioSignpostRegistry registry;
        private readonly IDisposable spawnSubscription;
        private bool captureSpawn;
        private int captureX;
        private int captureY;
        private int capturedBuildingId = -1;

        public SignpostPlacementService(ManualLogSource log, ScenarioSignpostRegistry registry)
        {
            this.log = log;
            this.registry = registry;
            spawnSubscription = BuildingR3EventHooks.OnBuildingSpawn.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(OnBuildingSpawn);
        }

        public bool TryInitialize(RandomEventsSaveStateV1 state)
        {
            if (state.SignpostsInitialized)
                return true;

            if (!registry.IsAvailable)
            {
                LogError(
                    "Automatic edge-signpost initialization is disabled for this match because the native registry is unavailable. " +
                    $"Random events remain active. Reason: {registry.UnavailableReason}");
                state.SignpostsInitialized = true;
                return true;
            }

            if (!TryGetParticipatingKeepCenters(out List<MapPoint> keeps))
                return false;

            int[] selected = new[] { -1, -1, -1, -1 };
            HashSet<int> used = new HashSet<int>();
            int[] registered = registry.ReadRegisteredBuildingIds();

            for (int sideIndex = 0; sideIndex < 4; sideIndex++)
            {
                MapEdge side = (MapEdge)sideIndex;
                ExistingCandidate best = FindBestExisting(side, registered, used, keeps);
                if (best.BuildingId <= 0)
                    continue;

                selected[sideIndex] = best.BuildingId;
                used.Add(best.BuildingId);
                LogInfo(
                    $"Reusing registered Vanilla signpost: side={side}, buildingId={best.BuildingId}, " +
                    $"edgeDepth={best.EdgeDepth:0.00}, minimumKeepDistance={best.MinimumKeepDistance:0.00}.");
            }

            int localPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
            for (int sideIndex = 0; sideIndex < 4; sideIndex++)
            {
                if (selected[sideIndex] > 0)
                    continue;

                MapEdge side = (MapEdge)sideIndex;
                if (!registry.HasFreeSlot())
                {
                    LogWarning($"Signpost side skipped: side={side}, reason=all eight Vanilla slots occupied.");
                    continue;
                }

                if (TryPlaceForSide(side, localPlayerId, keeps, out int buildingId))
                    selected[sideIndex] = buildingId;
                else
                    LogWarning($"Signpost side skipped: side={side}, reason=no valid candidate within {MaximumEdgeDepth} tiles of the edge.");
            }

            state.SignpostBuildingIds = selected;
            state.SignpostsInitialized = true;
            LogInfo($"Signpost initialization completed once: ids=[{string.Join(",", selected)}].");
            return true;
        }

        public void Dispose()
        {
            spawnSubscription?.Dispose();
        }

        private bool TryPlaceForSide(MapEdge side, int localPlayerId, List<MapPoint> keeps, out int buildingId)
        {
            buildingId = -1;
            for (int depth = 0; depth <= MaximumEdgeDepth; depth++)
            {
                List<PlacementCandidate> candidates = GetCandidates(side, depth, keeps);
                candidates.Sort((left, right) =>
                {
                    int score = right.MinimumKeepDistance.CompareTo(left.MinimumKeepDistance);
                    if (score != 0) return score;
                    int x = left.X.CompareTo(right.X);
                    return x != 0 ? x : left.Y.CompareTo(right.Y);
                });

                foreach (PlacementCandidate candidate in candidates)
                {
                    if (!registry.HasFreeSlot())
                        return false;

                    int spawnedId = SpawnSignpost(localPlayerId, candidate.X, candidate.Y);
                    if (spawnedId <= 0)
                    {
                        LogInfo($"Vanilla signpost placement failed; trying next candidate: side={side}, depth={depth}, tile=({candidate.X},{candidate.Y}).");
                        continue;
                    }

                    if (registry.TryRegister(spawnedId, out int slot))
                    {
                        buildingId = spawnedId;
                        LogInfo(
                            $"Placed Vanilla signpost: side={side}, depth={depth}, tile=({candidate.X},{candidate.Y}), " +
                            $"buildingId={spawnedId}, slot={slot}, minimumKeepDistance={candidate.MinimumKeepDistance:0.00}.");
                        return true;
                    }

                    // A prefab created for this attempt must not remain as an unregistered scenery object.
                    GameBuildingManagerAPI.Instance.DeleteBuildingSafe(spawnedId);
                    LogWarning($"Removed unregistered signpost after native registration failed: buildingId={spawnedId}.");
                }

                // If Vanilla rejects every candidate at this depth, continue farther inward.
            }
            return false;
        }

        private int SpawnSignpost(int playerId, int x, int y)
        {
            captureSpawn = true;
            captureX = x;
            captureY = y;
            capturedBuildingId = -1;
            long result;
            try
            {
                result = GameBuildingManagerAPI.Instance.CreatePrefab(
                    playerId, x, y, eMappers.MAPPER_SIGNPOST, 2, 0, true, false);
            }
            finally
            {
                captureSpawn = false;
            }

            int candidateId = capturedBuildingId > 0 ? capturedBuildingId : unchecked((int)result);
            if (candidateId <= 0 ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(candidateId, out GameBuilding* building) ||
                building->r_BuildingType != eStructs.STRUCT_SIGNPOST ||
                (building->r_AliveState != AliveState.NeedsInit && building->r_AliveState != AliveState.IsAlive) ||
                building->r_TilePositionXBegin != x || building->r_TilePositionYBegin != y)
            {
                return -1;
            }
            return candidateId;
        }

        private void OnBuildingSpawn(BuildingSpawnEventArgs args)
        {
            if (!captureSpawn || args.Building != eStructs.STRUCT_SIGNPOST || args.TileX != captureX || args.TileY != captureY)
                return;
            capturedBuildingId = unchecked((int)args.ReturnValue);
        }

        private static List<PlacementCandidate> GetCandidates(MapEdge side, int depth, List<MapPoint> keeps)
        {
            List<PlacementCandidate> result = new List<PlacementCandidate>();
            int radius = GameTileManagerAPI.Instance.GetCurrentMapSize() / 2;
            int constant = GetEdgeConstant(side, radius, depth);

            for (int x = 0; x < GridSize; x++)
            {
                int y = side == MapEdge.MinimumSum || side == MapEdge.MaximumSum
                    ? constant - x
                    : x - constant;
                if (!IsFreeWalkableFootprint(x, y))
                    continue;

                double distance = MinimumDistance(x + 0.5, y + 0.5, keeps);
                if (distance + 0.0001 < MinimumKeepDistance)
                    continue;
                result.Add(new PlacementCandidate(x, y, distance));
            }
            return result;
        }

        private static bool IsFreeWalkableFootprint(int x, int y)
        {
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            for (int offsetY = 0; offsetY < 2; offsetY++)
            {
                for (int offsetX = 0; offsetX < 2; offsetX++)
                {
                    int tileX = x + offsetX;
                    int tileY = y + offsetY;
                    if (!tiles.IsTileInsideMapBounds(tileX, tileY) ||
                        !tiles.IsTileWalkableAndUnoccupied(tiles.GetTileId(tileX, tileY)))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private static ExistingCandidate FindBestExisting(
            MapEdge side,
            int[] registered,
            HashSet<int> used,
            List<MapPoint> keeps)
        {
            ExistingCandidate best = default;
            foreach (int id in registered)
            {
                if (id <= 0 || used.Contains(id) ||
                    !GameBuildingManagerAPI.Instance.TryGetBuildingById(id, out GameBuilding* building) ||
                    building->r_AliveState != AliveState.IsAlive ||
                    building->r_BuildingType != eStructs.STRUCT_SIGNPOST)
                {
                    continue;
                }

                double x = (building->r_TilePositionXBegin + building->r_TilePositionXEnd) / 2.0;
                double y = (building->r_TilePositionYBegin + building->r_TilePositionYEnd) / 2.0;
                double edgeDepth = DistanceFromEdge(side, x, y);
                if (edgeDepth > MaximumEdgeDepth + 0.0001)
                    continue;
                double keepDistance = MinimumDistance(x, y, keeps);
                if (keepDistance + 0.0001 < MinimumKeepDistance)
                    continue;
                if (best.BuildingId <= 0 || keepDistance > best.MinimumKeepDistance)
                    best = new ExistingCandidate(id, edgeDepth, keepDistance);
            }
            return best;
        }

        private static bool TryGetParticipatingKeepCenters(out List<MapPoint> keeps)
        {
            keeps = new List<MapPoint>();
            int[] activePlayers = Shared.ActivePlayerHelper.GetActivePlayerIds();
            if (activePlayers.Length == 0)
                return false;

            foreach (int playerId in activePlayers)
            {
                int keepId = GamePlayerManagerAPI.Instance.GetPlayerKeepId(playerId);
                if (keepId <= 0 || !GameBuildingManagerAPI.Instance.TryGetBuildingById(keepId, out GameBuilding* keep) ||
                    (keep->r_AliveState != AliveState.NeedsInit && keep->r_AliveState != AliveState.IsAlive))
                {
                    return false;
                }
                keeps.Add(new MapPoint(
                    (keep->r_TilePositionXBegin + keep->r_TilePositionXEnd) / 2.0,
                    (keep->r_TilePositionYBegin + keep->r_TilePositionYEnd) / 2.0));
            }
            return true;
        }

        private static int GetEdgeConstant(MapEdge side, int radius, int depth)
        {
            switch (side)
            {
                case MapEdge.MinimumSum: return MapCenter * 2 - radius + depth;
                case MapEdge.MaximumSum: return MapCenter * 2 + radius - depth;
                case MapEdge.MaximumDifference: return radius - depth;
                default: return -radius + depth;
            }
        }

        private static double DistanceFromEdge(MapEdge side, double x, double y)
        {
            int radius = GameTileManagerAPI.Instance.GetCurrentMapSize() / 2;
            switch (side)
            {
                case MapEdge.MinimumSum: return Math.Abs(x + y - (MapCenter * 2 - radius));
                case MapEdge.MaximumSum: return Math.Abs(x + y - (MapCenter * 2 + radius));
                case MapEdge.MaximumDifference: return Math.Abs(x - y - radius);
                default: return Math.Abs(x - y + radius);
            }
        }

        private static double MinimumDistance(double x, double y, List<MapPoint> keeps)
        {
            double minimum = double.MaxValue;
            foreach (MapPoint keep in keeps)
            {
                double deltaX = x - keep.X;
                double deltaY = y - keep.Y;
                minimum = Math.Min(minimum, Math.Sqrt(deltaX * deltaX + deltaY * deltaY));
            }
            return minimum;
        }

        private void LogInfo(string message) => Shared.DebugLogHelper.LogInfo(log, message);
        private void LogWarning(string message) => Shared.DebugLogHelper.LogWarning(log, message);
        private void LogError(string message) => Shared.DebugLogHelper.LogError(log, message);

        private enum MapEdge { MinimumSum, MaximumSum, MaximumDifference, MinimumDifference }

        private readonly struct MapPoint
        {
            public MapPoint(double x, double y) { X = x; Y = y; }
            public double X { get; }
            public double Y { get; }
        }

        private readonly struct PlacementCandidate
        {
            public PlacementCandidate(int x, int y, double distance) { X = x; Y = y; MinimumKeepDistance = distance; }
            public int X { get; }
            public int Y { get; }
            public double MinimumKeepDistance { get; }
        }

        private readonly struct ExistingCandidate
        {
            public ExistingCandidate(int id, double edgeDepth, double distance) { BuildingId = id; EdgeDepth = edgeDepth; MinimumKeepDistance = distance; }
            public int BuildingId { get; }
            public double EdgeDepth { get; }
            public double MinimumKeepDistance { get; }
        }
    }
}
