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
        private const int MinimumEdgeDepth = 2;
        private const int MaximumEdgeDepth = 12;
        private const double MinimumKeepDistance = 100.0;
        private const double MaximumRandomOffset = 100.0;
        private const int CenterFallbackRadius = 50;
        private const int NaturePlayerId = 0;

        private readonly ManualLogSource log;
        private readonly ScenarioSignpostRegistry registry;
        private readonly IDisposable spawnSubscription;
        private readonly IDisposable damageSubscription;
        private readonly HashSet<int> protectedSignpostIds = new HashSet<int>();
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
            damageSubscription = BuildingR3EventHooks.OnBuildingTileTakeDamage.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnBuildingTileTakeDamage);
        }

        public bool TryInitialize(RandomEventsSaveStateV2 state)
        {
            if (state.SignpostsInitialized)
            {
                TrackProtectedSignposts(state.SignpostBuildingIds);
                return true;
            }

            if (!registry.IsAvailable)
            {
                LogError(
                    "Automatic edge-signpost initialization is disabled for this match because the native registry is unavailable. " +
                    "Events that require a signpost will be skipped. " +
                    $"Reason: {registry.UnavailableReason}");
                state.SignpostsInitialized = true;
                return true;
            }

            if (!TryGetParticipatingKeepCenters(out List<MapPoint> keeps))
                return false;

            int[] selected = new[] { -1, -1, -1, -1 };
            HashSet<int> used = new HashSet<int>();
            int[] registered = registry.ReadRegisteredBuildingIds();
            int placementSeed = GetPlacementSeed(state);
            Random placementRandom = new Random(placementSeed);
            LogInfo(
                $"Randomized signpost placement initialized: seed={placementSeed}, " +
                $"edgeDepth={MinimumEdgeDepth}-{MaximumEdgeDepth}, randomOffsetRadius={MaximumRandomOffset:0}, " +
                $"minimumKeepDistance={MinimumKeepDistance:0}.");

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

                if (TryPlaceForSide(side, keeps, placementRandom, out int buildingId))
                    selected[sideIndex] = buildingId;
                else
                    LogWarning(
                        $"Signpost side skipped: side={side}, reason=no valid candidate between " +
                        $"{MinimumEdgeDepth} and {MaximumEdgeDepth} tiles of the edge.");
            }

            if (!registry.HasUsableRegisteredSignpost())
            {
                LogWarning(
                    $"No usable registered signpost was found at the map edges; trying one center fallback " +
                    $"within radius {CenterFallbackRadius}.");
                if (registry.HasFreeSlot() &&
                    TryPlaceCenterFallback(keeps, placementRandom, out int fallbackBuildingId))
                {
                    // The save field remains four-wide; index zero also records the single emergency signpost.
                    selected[0] = fallbackBuildingId;
                }
                else if (!registry.HasFreeSlot())
                {
                    LogError(
                        "Emergency center signpost placement skipped because all eight Vanilla signpost slots are occupied.");
                }
            }

            state.SignpostBuildingIds = selected;
            TrackProtectedSignposts(selected);
            state.SignpostsInitialized = true;
            if (registry.HasUsableRegisteredSignpost())
            {
                LogInfo($"Signpost initialization completed once: ids=[{string.Join(",", selected)}].");
            }
            else
            {
                LogError(
                    "Signpost initialization completed without any usable registered signpost. " +
                    "Lion, bandit, and archer events will not be dispatched in this match.");
            }
            return true;
        }

        public void Dispose()
        {
            spawnSubscription?.Dispose();
            damageSubscription?.Dispose();
        }

        public void ResetMapState()
        {
            protectedSignpostIds.Clear();
        }

        private bool TryPlaceForSide(
            MapEdge side,
            List<MapPoint> keeps,
            Random random,
            out int buildingId)
        {
            buildingId = -1;
            List<int> depths = Enumerable.Range(
                MinimumEdgeDepth,
                MaximumEdgeDepth - MinimumEdgeDepth + 1).ToList();
            Shuffle(depths, random);

            foreach (int depth in depths)
            {
                List<PlacementCandidate> candidates = GetCandidates(side, depth, keeps);
                candidates.Sort((left, right) =>
                {
                    int score = right.MinimumKeepDistance.CompareTo(left.MinimumKeepDistance);
                    if (score != 0) return score;
                    int x = left.X.CompareTo(right.X);
                    return x != 0 ? x : left.Y.CompareTo(right.Y);
                });
                if (candidates.Count == 0)
                    continue;

                PlacementCandidate best = candidates[0];
                List<PlacementCandidate> randomizedCandidates = candidates
                    .Where(candidate => CandidateDistance(candidate, best) <= MaximumRandomOffset + 0.0001)
                    .ToList();
                Shuffle(randomizedCandidates, random);
                LogInfo(
                    $"Randomized signpost candidate set: side={side}, depth={depth}, " +
                    $"validCandidates={candidates.Count}, randomPool={randomizedCandidates.Count}, " +
                    $"bestTile=({best.X},{best.Y}), bestMinimumKeepDistance={best.MinimumKeepDistance:0.00}.");

                int failedPlacements = 0;
                foreach (PlacementCandidate candidate in randomizedCandidates)
                {
                    if (!registry.HasFreeSlot())
                        return false;

                    int spawnedId = SpawnSignpost(candidate.X, candidate.Y);
                    if (spawnedId <= 0)
                    {
                        failedPlacements++;
                        continue;
                    }

                    if (registry.TryRegister(spawnedId, out int slot))
                    {
                        buildingId = spawnedId;
                        LogInfo(
                            $"Placed Vanilla signpost: side={side}, depth={depth}, tile=({candidate.X},{candidate.Y}), " +
                            $"buildingId={spawnedId}, slot={slot}, minimumKeepDistance={candidate.MinimumKeepDistance:0.00}, " +
                            $"randomOffsetFromBest={CandidateDistance(candidate, best):0.00}, owner=Nature, damageProtected=true.");
                        return true;
                    }

                    // A prefab created for this attempt must not remain as an unregistered scenery object.
                    GameBuildingManagerAPI.Instance.DeleteBuildingSafe(spawnedId);
                    LogWarning($"Removed unregistered signpost after native registration failed: buildingId={spawnedId}.");
                }

                if (failedPlacements > 0)
                {
                    LogInfo(
                        $"Vanilla rejected randomized signpost candidates at depth; trying another random depth: " +
                        $"side={side}, depth={depth}, failedPlacements={failedPlacements}.");
                }
            }
            return false;
        }

        private bool TryPlaceCenterFallback(
            List<MapPoint> keeps,
            Random random,
            out int buildingId)
        {
            buildingId = -1;
            List<PlacementCandidate> candidates = new List<PlacementCandidate>();
            for (int x = MapCenter - CenterFallbackRadius; x <= MapCenter + CenterFallbackRadius; x++)
            {
                for (int y = MapCenter - CenterFallbackRadius; y <= MapCenter + CenterFallbackRadius; y++)
                {
                    double deltaX = x + 0.5 - MapCenter;
                    double deltaY = y + 0.5 - MapCenter;
                    if (deltaX * deltaX + deltaY * deltaY > CenterFallbackRadius * CenterFallbackRadius ||
                        !IsFreeWalkableFootprint(x, y))
                    {
                        continue;
                    }

                    double keepDistance = MinimumDistance(x + 0.5, y + 0.5, keeps);
                    if (keepDistance + 0.0001 >= MinimumKeepDistance)
                        candidates.Add(new PlacementCandidate(x, y, keepDistance));
                }
            }

            Shuffle(candidates, random);
            int failedPlacements = 0;
            foreach (PlacementCandidate candidate in candidates)
            {
                int spawnedId = SpawnSignpost(candidate.X, candidate.Y);
                if (spawnedId <= 0)
                {
                    failedPlacements++;
                    continue;
                }

                if (registry.TryRegister(spawnedId, out int slot))
                {
                    buildingId = spawnedId;
                    double deltaX = candidate.X + 0.5 - MapCenter;
                    double deltaY = candidate.Y + 0.5 - MapCenter;
                    LogInfo(
                        $"Placed emergency center Vanilla signpost: tile=({candidate.X},{candidate.Y}), " +
                        $"buildingId={spawnedId}, slot={slot}, minimumKeepDistance={candidate.MinimumKeepDistance:0.00}, " +
                        $"centerDistance={Math.Sqrt(deltaX * deltaX + deltaY * deltaY):0.00}, " +
                        "owner=Nature, damageProtected=true.");
                    return true;
                }

                GameBuildingManagerAPI.Instance.DeleteBuildingSafe(spawnedId);
                LogWarning($"Removed unregistered center fallback signpost: buildingId={spawnedId}.");
            }

            LogError(
                $"Emergency center signpost placement failed: radius={CenterFallbackRadius}, " +
                $"validCandidates={candidates.Count}, VanillaRejected={failedPlacements}.");
            return false;
        }

        private static int GetPlacementSeed(RandomEventsSaveStateV2 state)
        {
            // Derive a stable side-placement stream without consuming the saved event-roll PRNG.
            ulong value = state.PrngState0 ^ (state.PrngState1 + 0x9E3779B97F4A7C15UL);
            value ^= (ulong)GameTileManagerAPI.Instance.GetCurrentMapSize() * 0xBF58476D1CE4E5B9UL;
            value ^= value >> 30;
            value *= 0xBF58476D1CE4E5B9UL;
            value ^= value >> 27;
            value *= 0x94D049BB133111EBUL;
            value ^= value >> 31;
            return unchecked((int)(value ^ (value >> 32)));
        }

        private static double CandidateDistance(PlacementCandidate left, PlacementCandidate right)
        {
            double deltaX = left.X - right.X;
            double deltaY = left.Y - right.Y;
            return Math.Sqrt(deltaX * deltaX + deltaY * deltaY);
        }

        private static void Shuffle<T>(IList<T> values, Random random)
        {
            for (int index = values.Count - 1; index > 0; index--)
            {
                int swapIndex = random.Next(index + 1);
                T temporary = values[index];
                values[index] = values[swapIndex];
                values[swapIndex] = temporary;
            }
        }

        private int SpawnSignpost(int x, int y)
        {
            captureSpawn = true;
            captureX = x;
            captureY = y;
            capturedBuildingId = -1;
            long result;
            try
            {
                result = GameBuildingManagerAPI.Instance.CreatePrefab(
                    NaturePlayerId, x, y, eMappers.MAPPER_SIGNPOST, 2, 0, true, false);
            }
            finally
            {
                captureSpawn = false;
            }

            int candidateId = capturedBuildingId > 0 ? capturedBuildingId : unchecked((int)result);
            if (candidateId <= 0 ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(candidateId, out GameBuilding* building) ||
                building->r_BuildingType != eStructs.STRUCT_SIGNPOST ||
                building->r_PlayerIdOwner != NaturePlayerId ||
                (building->r_AliveState != AliveState.NeedsInit && building->r_AliveState != AliveState.IsAlive) ||
                building->r_TilePositionXBegin != x || building->r_TilePositionYBegin != y)
            {
                return -1;
            }
            return candidateId;
        }

        private void TrackProtectedSignposts(IEnumerable<int> buildingIds)
        {
            if (buildingIds == null)
                return;

            foreach (int buildingId in buildingIds)
            {
                if (buildingId <= 0 ||
                    !GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                    building->r_BuildingType != eStructs.STRUCT_SIGNPOST ||
                    building->r_PlayerIdOwner != NaturePlayerId)
                {
                    continue;
                }
                protectedSignpostIds.Add(buildingId);
            }
        }

        private void OnBuildingTileTakeDamage(BuildingTileTakeDamageEventArgs args)
        {
            int buildingId = GameTileManagerAPI.Instance.GetTileBuildingId(args.TileId);
            if (!protectedSignpostIds.Contains(buildingId) ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                building->r_BuildingType != eStructs.STRUCT_SIGNPOST ||
                building->r_PlayerIdOwner != NaturePlayerId)
            {
                return;
            }

            // Neutral ownership prevents normal targeting; zero damage also guards indirect hits.
            args.Damage = 0;
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
                if (edgeDepth + 0.0001 < MinimumEdgeDepth || edgeDepth > MaximumEdgeDepth + 0.0001)
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
