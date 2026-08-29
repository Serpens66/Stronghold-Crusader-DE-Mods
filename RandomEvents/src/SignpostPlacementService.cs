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

        public bool TryInitialize(RandomEventsRuntimeState state)
        {
            if (state.SignpostsInitialized)
            {
                TrackProtectedSignposts(state.SignpostBuildingIds);
                registry.SetEligibleSignposts(state.SignpostBuildingIds);
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

            List<PlayerReachability> participantReachability;
            try
            {
                if (!TryBuildParticipantReachability(state.IncludeAIPlayers, out participantReachability, out _))
                    return false;
            }
            catch (Exception ex)
            {
                LogError(
                    "Automatic signpost placement is disabled for this match because Vanilla path connectivity " +
                    $"could not be read safely; signpost-dependent events will be skipped. Error: {ex}");
                state.SignpostsInitialized = true;
                return true;
            }

            int[] selected = new[] { -1, -1, -1, -1 };
            HashSet<int> used = new HashSet<int>();
            int[] registered = registry.ReadRegisteredBuildingIds();
            foreach (int buildingId in registered.Distinct())
            {
                if (buildingId <= 0)
                    continue;
                bool usableAndReachable =
                    GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* signpost) &&
                    signpost->r_BuildingType == eStructs.STRUCT_SIGNPOST &&
                    (signpost->r_AliveState == AliveState.NeedsInit || signpost->r_AliveState == AliveState.IsAlive) &&
                    IsReachableFromEveryParticipant(
                        signpost->r_TilePositionXBegin,
                        signpost->r_TilePositionYBegin,
                        signpost->r_TilePositionXEnd,
                        signpost->r_TilePositionYEnd,
                        participantReachability);
                if (!usableAndReachable)
                    registry.TryUnregister(buildingId);
            }
            registered = registry.ReadRegisteredBuildingIds();
            int placementSeed = GetPlacementSeed(state);
            Random placementRandom = new Random(placementSeed);
            for (int sideIndex = 0; sideIndex < 4; sideIndex++)
            {
                MapEdge side = (MapEdge)sideIndex;
                ExistingCandidate best = FindBestExisting(side, registered, used, keeps, participantReachability);
                if (best.BuildingId <= 0)
                    continue;

                selected[sideIndex] = best.BuildingId;
                used.Add(best.BuildingId);
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

                if (TryPlaceForSide(side, keeps, participantReachability, placementRandom, out int buildingId))
                    selected[sideIndex] = buildingId;
                else
                    LogWarning(
                        $"Signpost side skipped: side={side}, reason=no valid candidate between " +
                        $"{MinimumEdgeDepth} and {MaximumEdgeDepth} tiles of the edge.");
            }

            if (selected.All(buildingId => buildingId <= 0))
            {
                LogWarning(
                    $"No usable registered signpost was found at the map edges; trying one center fallback " +
                    $"within radius {CenterFallbackRadius}.");
                if (registry.HasFreeSlot() &&
                    TryPlaceCenterFallback(keeps, participantReachability, placementRandom, out int fallbackBuildingId))
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
            registry.SetEligibleSignposts(selected);
            state.SignpostsInitialized = true;
            if (!registry.HasUsableRegisteredSignpost())
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
            registry.ResetMapState();
        }

        private bool TryPlaceForSide(
            MapEdge side,
            List<MapPoint> keeps,
            List<PlayerReachability> participantReachability,
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
                List<PlacementCandidate> candidates = GetCandidates(side, depth, keeps, participantReachability);
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
                List<PlacementCandidate> preferredCandidates = candidates
                    .Where(candidate => CandidateDistance(candidate, best) <= MaximumRandomOffset + 0.0001)
                    .ToList();
                List<PlacementCandidate> fallbackCandidates = candidates
                    .Where(candidate => CandidateDistance(candidate, best) > MaximumRandomOffset + 0.0001)
                    .ToList();
                Shuffle(preferredCandidates, random);
                Shuffle(fallbackCandidates, random);
                preferredCandidates.AddRange(fallbackCandidates);
                foreach (PlacementCandidate candidate in preferredCandidates)
                {
                    if (!registry.HasFreeSlot())
                        return false;

                    int spawnedId = SpawnSignpost(candidate.X, candidate.Y);
                    if (spawnedId <= 0)
                        continue;

                    if (registry.TryRegister(spawnedId, out _))
                    {
                        buildingId = spawnedId;
                        return true;
                    }

                    // A prefab created for this attempt must not remain as an unregistered scenery object.
                    GameBuildingManagerAPI.Instance.DeleteBuildingSafe(spawnedId);
                    LogWarning($"Removed unregistered signpost after native registration failed: buildingId={spawnedId}.");
                }
            }
            return false;
        }

        private bool TryPlaceCenterFallback(
            List<MapPoint> keeps,
            List<PlayerReachability> participantReachability,
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
                        !IsFreeWalkableFootprint(x, y) ||
                        !IsReachableFromEveryParticipant(x, y, x + 1, y + 1, participantReachability))
                    {
                        continue;
                    }

                    double keepDistance = MinimumDistance(x + 0.5, y + 0.5, keeps);
                    candidates.Add(new PlacementCandidate(x, y, keepDistance));
                }
            }

            Shuffle(candidates, random);
            // This is an emergency path for maps without a usable edge. Prefer the safest
            // available center tile even when a keep makes the normal 100-tile rule impossible.
            candidates.Sort((left, right) => right.MinimumKeepDistance.CompareTo(left.MinimumKeepDistance));
            int failedPlacements = 0;
            foreach (PlacementCandidate candidate in candidates)
            {
                int spawnedId = SpawnSignpost(candidate.X, candidate.Y);
                if (spawnedId <= 0)
                {
                    failedPlacements++;
                    continue;
                }

                if (registry.TryRegister(spawnedId, out _))
                {
                    buildingId = spawnedId;
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

        private static int GetPlacementSeed(RandomEventsRuntimeState state)
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
            bool previousBypassEnabled = GameTileManagerAPI.Instance.TileManager.UsePlacementBlockedOverride;
            bool previousBypassValue = GameTileManagerAPI.Instance.TileManager.PlacementBlockedOverrideValue;
            try
            {
                // Nature (player 0) is not a valid player-placement owner. The caller already
                // validates bounds, occupancy, flat terrain, and participant path connectivity.
                result = GameBuildingManagerAPI.Instance.CreatePrefab(
                    NaturePlayerId, x, y, eMappers.MAPPER_SIGNPOST, 2, 0, true, true);
            }
            finally
            {
                // CreatePrefab clears the shared override after its call. Restore the prior
                // state as well as handling an exception before the API can clear it itself.
                GameTileManagerAPI.Instance.TileManager.PlacementBlockedOverrideValue = previousBypassValue;
                GameTileManagerAPI.Instance.TileManager.UsePlacementBlockedOverride = previousBypassEnabled;
                captureSpawn = false;
            }

            int candidateId = capturedBuildingId > 0 ? capturedBuildingId : unchecked((int)result);
            if (candidateId <= 0 ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(candidateId, out GameBuilding* building))
            {
                return -1;
            }

            if (building->r_BuildingType != eStructs.STRUCT_SIGNPOST ||
                building->r_PlayerIdOwner != NaturePlayerId ||
                (building->r_AliveState != AliveState.NeedsInit && building->r_AliveState != AliveState.IsAlive) ||
                building->r_TilePositionXBegin != x || building->r_TilePositionYBegin != y)
            {
                // Do not leave a malformed prefab behind when Vanilla or another placement hook altered the attempt.
                if (building->r_BuildingType == eStructs.STRUCT_SIGNPOST &&
                    building->r_PlayerIdOwner == NaturePlayerId)
                {
                    GameBuildingManagerAPI.Instance.DeleteBuildingSafe(candidateId);
                }
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

        private static List<PlacementCandidate> GetCandidates(
            MapEdge side,
            int depth,
            List<MapPoint> keeps,
            List<PlayerReachability> participantReachability)
        {
            List<PlacementCandidate> result = new List<PlacementCandidate>();
            int radius = GameTileManagerAPI.Instance.GetCurrentMapSize() / 2;
            int constant = GetEdgeConstant(side, radius, depth);

            for (int x = 0; x < GridSize; x++)
            {
                int y = side == MapEdge.MinimumSum || side == MapEdge.MaximumSum
                    ? constant - x
                    : x - constant;
                if (!IsFreeWalkableFootprint(x, y) ||
                    !IsReachableFromEveryParticipant(x, y, x + 1, y + 1, participantReachability))
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
            int footprintHeight = -1;
            for (int offsetY = 0; offsetY < 2; offsetY++)
            {
                for (int offsetX = 0; offsetX < 2; offsetX++)
                {
                    int tileX = x + offsetX;
                    int tileY = y + offsetY;
                    if (!tiles.IsTileInsideMapBounds(tileX, tileY))
                    {
                        return false;
                    }

                    int tileId = tiles.GetTileId(tileX, tileY);
                    if (!tiles.IsTileWalkableAndUnoccupied(tileId))
                        return false;

                    int tileHeight = tiles.GetTileHeight(tileId);
                    if (footprintHeight < 0)
                        footprintHeight = tileHeight;
                    else if (tileHeight != footprintHeight)
                        return false;
                }
            }
            return true;
        }

        private static ExistingCandidate FindBestExisting(
            MapEdge side,
            int[] registered,
            HashSet<int> used,
            List<MapPoint> keeps,
            List<PlayerReachability> participantReachability)
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
                if (!IsReachableFromEveryParticipant(
                        building->r_TilePositionXBegin,
                        building->r_TilePositionYBegin,
                        building->r_TilePositionXEnd,
                        building->r_TilePositionYEnd,
                        participantReachability))
                {
                    continue;
                }
                double keepDistance = MinimumDistance(x, y, keeps);
                if (keepDistance + 0.0001 < MinimumKeepDistance)
                    continue;
                if (best.BuildingId <= 0 || keepDistance > best.MinimumKeepDistance)
                    best = new ExistingCandidate(id, edgeDepth, keepDistance);
            }
            return best;
        }

        private static bool TryBuildParticipantReachability(
            bool includeAIPlayers,
            out List<PlayerReachability> result,
            out string failure)
        {
            result = new List<PlayerReachability>();
            failure = string.Empty;
            GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
            foreach (int playerId in Shared.ActivePlayerHelper.GetActivePlayerIds())
            {
                if (!players.IsPlayerIdValid(playerId) ||
                    (!includeAIPlayers && players.IsAIPlayer(playerId)))
                    continue;
                result.Add(new PlayerReachability(playerId));
            }

            if (result.Count == 0)
            {
                failure = "no active event participant is available for the Vanilla reachability check.";
                return false;
            }

            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            foreach (PlayerReachability player in result)
            {
                for (int index = 0; index < buildings.Length; index++)
                {
                    ref GameBuilding building = ref buildings[index];
                    if (building.r_PlayerIdOwner != player.PlayerId ||
                        (building.r_AliveState != AliveState.NeedsInit && building.r_AliveState != AliveState.IsAlive))
                    {
                        continue;
                    }

                    AddApproachComponents(
                        building.r_TilePositionXBegin,
                        building.r_TilePositionYBegin,
                        building.r_TilePositionXEnd,
                        building.r_TilePositionYEnd,
                        player.Components);
                }
            }

            AddOwnedWallApproachComponents(result);
            foreach (PlayerReachability player in result)
            {
                if (player.Components.Count == 0)
                {
                    failure = $"player {player.PlayerId} has no initialized building or wall approach tile yet.";
                    return false;
                }
            }
            return true;
        }

        private static void AddOwnedWallApproachComponents(List<PlayerReachability> players)
        {
            Dictionary<int, HashSet<ushort>> componentsByPlayer = players.ToDictionary(
                player => player.PlayerId,
                player => player.Components);
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            for (int y = 0; y < GridSize; y++)
            {
                for (int x = 0; x < GridSize; x++)
                {
                    if (!tiles.IsTileInsideMapBounds(x, y))
                        continue;
                    int tileId = tiles.GetTileId(x, y);
                    int ownerId = tiles.GetTilePlayerOwnerId(tileId);
                    if (!componentsByPlayer.TryGetValue(ownerId, out HashSet<ushort> components) ||
                        (tiles.GetTilePropertyFlag(tileId) & TilePropertyFlag.IsWall) == 0)
                    {
                        continue;
                    }
                    AddApproachComponents(x, y, x, y, components);
                }
            }
        }

        private static bool IsReachableFromEveryParticipant(
            int beginX,
            int beginY,
            int endX,
            int endY,
            List<PlayerReachability> participantReachability)
        {
            HashSet<ushort> sourceComponents = new HashSet<ushort>();
            AddApproachComponents(beginX, beginY, endX, endY, sourceComponents, includeFootprint: true);
            if (sourceComponents.Count == 0)
                return false;

            foreach (PlayerReachability player in participantReachability)
            {
                bool connected = false;
                foreach (ushort component in sourceComponents)
                {
                    if (player.Components.Contains(component))
                    {
                        connected = true;
                        break;
                    }
                }
                if (!connected)
                    return false;
            }
            return true;
        }

        private static void AddApproachComponents(
            int beginX,
            int beginY,
            int endX,
            int endY,
            HashSet<ushort> components,
            bool includeFootprint = false)
        {
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            Span<ushort> pathConnections = tiles.TileManager.PathConnectionGrid;
            for (int y = beginY - 1; y <= endY + 1; y++)
            {
                for (int x = beginX - 1; x <= endX + 1; x++)
                {
                    bool inside = x >= beginX && x <= endX && y >= beginY && y <= endY;
                    if ((!includeFootprint && inside) || !tiles.IsTileInsideMapBounds(x, y))
                        continue;
                    int tileId = tiles.GetTileId(x, y);
                    if (!tiles.IsTileWalkableAndUnoccupied(tileId))
                        continue;
                    ushort component = pathConnections[tileId];
                    if (component != 0)
                        components.Add(component);
                }
            }
        }

        private static bool TryGetParticipatingKeepCenters(out List<MapPoint> keeps)
        {
            keeps = new List<MapPoint>();
            if (!Shared.ActivePlayerKeepReadiness.TryCapture(
                    out Shared.ActivePlayerKeepSnapshot snapshot,
                    out _))
            {
                return false;
            }

            foreach (int keepId in snapshot.KeepBuildingIds)
            {
                if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(keepId, out GameBuilding* keep))
                    return false;
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

        private sealed class PlayerReachability
        {
            public PlayerReachability(int playerId) { PlayerId = playerId; }
            public int PlayerId { get; }
            public HashSet<ushort> Components { get; } = new HashSet<ushort>();
        }
    }
}
