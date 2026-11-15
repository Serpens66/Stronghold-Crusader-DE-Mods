using System;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace AIHunterPreyTest
{
    // Testmod-only ordinary construction. The caller owns the rare simulation-tick schedule
    // and the prey decision; this class never creates units, supplies goods or clears terrain.
    internal sealed unsafe class AutomaticHunterBuilder
    {
        private const int MaximumCandidates = 256;
        private const int MaximumPlacementAttempts = 64;
        private const int SearchRadius = 32;
        private const int SearchStep = 4;
        // Vanilla's ordinary-building call uses 15 for automatic orientation.
        private const int AutomaticOrientation = 15;
        private readonly IDisposable spawnSubscription;
        private bool placing;
        private int capturePlayer, captureX, captureY, spawnedId;
        private uint spawnedGlobalId;

        internal AutomaticHunterBuilder()
        {
            // Kept with the statically rooted runtime for the entire process; never unhook
            // on the destruction of BepInEx's short-lived startup component.
            spawnSubscription = BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(ObserveSpawn);
        }

        internal bool TryBuild(int playerId, int maximumHuts, out string reason)
        {
            reason = "not-eligible";
            if (placing || maximumHuts < 1 || playerId < 1 || playerId > GamePlayerManagerAPI.MAX_PLAYERS)
                return false;
            var players = GamePlayerManagerAPI.Instance;
            if (!players.IsAIPlayer(playerId) || !players.TryGetPlayerResourcesById(playerId, out var resources))
                return false;

            var api = GameBuildingManagerAPI.Instance;
            var manager = api.GetBuildingManager();
            // Same capacity reserve as Vanilla's AI building callers (manager +0x54).
            if (manager.IsNull || manager.Pointer->FreeBuildings <= 20)
            {
                reason = "building-capacity";
                return false;
            }
            int huts = 0;
            var buildings = api.GetBuildingsAsSpan();
            for (int spanIndex = 0; spanIndex < buildings.Length; spanIndex++)
            {
                ref readonly GameBuilding building = ref buildings[spanIndex];
                if (building.r_PlayerIdOwner == playerId && building.r_BuildingType == eStructs.STRUCT_HUNTERS_HUT &&
                    (building.r_AliveState == AliveState.IsAlive || building.r_AliveState == AliveState.NeedsInit))
                    huts++;
            }
            if (huts >= maximumHuts)
            {
                reason = "hut-limit";
                return false;
            }
            if (!CanAfford(players, api, playerId))
            {
                reason = "insufficient-goods";
                return false;
            }

            int granaryId = checked((int)resources->r_FirstGranaryId);
            int keepId = players.GetPlayerKeepId(playerId);
            if (!IsOwnedBuilding(api, keepId, playerId, out _) ||
                !IsOwnedBuilding(api, granaryId, playerId, out GameBuilding* granary) ||
                granary->r_BuildingType != eStructs.STRUCT_GRANARY)
            {
                reason = "keep-or-granary-unavailable";
                return false;
            }
            int centerX = granary->r_TilePositionXBegin + (int)granary->r_OccupyTileGridSize / 2;
            int centerY = granary->r_TilePositionYBegin + (int)granary->r_OccupyTileGridSize / 2;
            var door = players.GetPlayerKeepDoorPosition(playerId);
            var pathing = GamePathingManagerAPI.Instance;
            if (!pathing.TryGetPathComponentId(door.X, door.Y, out ushort keepComponent) || keepComponent == 0)
            {
                reason = "keep-component-unavailable";
                return false;
            }
            int scale = BuildingScales.GetScale(eMappers.MAPPER_HUNTER);
            if (scale != 3)
                throw new InvalidOperationException("Hunter footprint differs from the audited three-tile square.");

            int candidates = 0, attempts = 0;
            // Fixed order and bounded work, independent of wall clock, frame rate or selected player.
            for (int radius = SearchStep; radius <= SearchRadius; radius += SearchStep)
            {
                for (int offset = -radius; offset <= radius; offset += SearchStep)
                {
                    if (TryCandidate(playerId, centerX + offset, centerY - radius, scale, keepComponent,
                        ref candidates, ref attempts, out reason)) return true;
                    if (Finished(candidates, attempts, reason)) return false;
                    if (TryCandidate(playerId, centerX + offset, centerY + radius, scale, keepComponent,
                        ref candidates, ref attempts, out reason)) return true;
                    if (Finished(candidates, attempts, reason)) return false;
                }
                for (int offset = -radius + SearchStep; offset < radius; offset += SearchStep)
                {
                    if (TryCandidate(playerId, centerX - radius, centerY + offset, scale, keepComponent,
                        ref candidates, ref attempts, out reason)) return true;
                    if (Finished(candidates, attempts, reason)) return false;
                    if (TryCandidate(playerId, centerX + radius, centerY + offset, scale, keepComponent,
                        ref candidates, ref attempts, out reason)) return true;
                    if (Finished(candidates, attempts, reason)) return false;
                }
            }
            reason = $"no-site candidates={candidates} nativeAttempts={attempts}";
            return false;
        }

        private static bool Finished(int candidates, int attempts, string reason) =>
            candidates >= MaximumCandidates || attempts >= MaximumPlacementAttempts || reason == "unconfirmed-native-creation";

        private bool TryCandidate(int playerId, int x, int y, int scale, ushort component,
            ref int candidates, ref int attempts, out string reason)
        {
            reason = "no-site";
            candidates++;
            var tiles = GameTileManagerAPI.Instance;
            var pathing = GamePathingManagerAPI.Instance;
            for (int dx = 0; dx < scale; dx++)
                for (int dy = 0; dy < scale; dy++)
                {
                    int tx = x + dx, ty = y + dy;
                    if (!tiles.IsTileInsideMapBounds(tx, ty) ||
                        !pathing.TryGetPathComponentId(tx, ty, out ushort tileComponent) || tileComponent != component ||
                        !tiles.IsTileWalkableAndUnoccupied(tiles.GetTileId(tx, ty)))
                        return false;
                }

            var api = GameBuildingManagerAPI.Instance;
            // Recheck immediately before the actual build. CreatePrefab debits costs but does
            // not perform the separate CC420 affordability check used by Vanilla's AI callers.
            if (!CanAfford(GamePlayerManagerAPI.Instance, api, playerId))
            {
                attempts = MaximumPlacementAttempts;
                reason = "insufficient-goods";
                return false;
            }
            attempts++;
            capturePlayer = playerId; captureX = x; captureY = y; spawnedId = 0; spawnedGlobalId = 0;
            placing = true;
            try
            {
                // Do not interpret this return value: the native constructor is void.
                api.CreatePrefab(playerId, x, y, eMappers.MAPPER_HUNTER, scale,
                    AutomaticOrientation, false, false);
            }
            finally { placing = false; }

            if (spawnedId != 0 && APIShared.BuildingAccess.TryGetById(api, spawnedId, out GameBuilding* built, out _) &&
                built->r_GlobalId == spawnedGlobalId && built->r_PlayerIdOwner == playerId &&
                built->r_BuildingType == eStructs.STRUCT_HUNTERS_HUT &&
                built->r_TilePositionXBegin == x && built->r_TilePositionYBegin == y &&
                (built->r_AliveState == AliveState.NeedsInit || built->r_AliveState == AliveState.IsAlive))
            {
                reason = $"built id={spawnedId} global={spawnedGlobalId} player={playerId} tile=({x},{y})";
                return true;
            }
            // An unblocked call without a confirmed spawn is not permission to attempt another
            // build: a failed observer must never create duplicates or charge a second time.
            if (!tiles.TileManager.IsPlacementBlocked) reason = "unconfirmed-native-creation";
            return false;
        }

        private void ObserveSpawn(BuildingSpawnEventArgs args)
        {
            if (!placing || spawnedId != 0 || args.Phase != EventHookPhase.Post || args.PlayerId != capturePlayer ||
                args.Building != eStructs.STRUCT_HUNTERS_HUT || args.TileX != captureX || args.TileY != captureY ||
                args.ReturnValue <= 0 || args.ReturnValue > int.MaxValue)
                return;
            int id = (int)args.ReturnValue;
            if (!APIShared.BuildingAccess.TryGetById(id, out GameBuilding* building, out _)) return;
            spawnedId = id;
            spawnedGlobalId = building->r_GlobalId;
        }

        private static bool IsOwnedBuilding(GameBuildingManagerAPI api, int id, int playerId, out GameBuilding* building) =>
            APIShared.BuildingAccess.TryGetById(api, id, out building, out _) && building->r_PlayerIdOwner == playerId &&
            building->r_AliveState == AliveState.IsAlive;

        private static bool CanAfford(GamePlayerManagerAPI players, GameBuildingManagerAPI api, int playerId)
        {
            eStructs type = eStructs.STRUCT_HUNTERS_HUT;
            return Has(players, playerId, eGoods.STORED_WOOD_PLANKS, api.GetWoodCost(type)) &&
                Has(players, playerId, eGoods.STORED_STONE_BLOCKS, api.GetStoneCost(type)) &&
                Has(players, playerId, eGoods.STORED_IRON_INGOTS, api.GetIronIngotCost(type)) &&
                Has(players, playerId, eGoods.STORED_PITCH_RAW, api.GetRawPitchCost(type)) &&
                Has(players, playerId, eGoods.STORED_GOLD, api.GetGoldCost(type));
        }

        private static bool Has(GamePlayerManagerAPI players, int playerId, eGoods good, int required) =>
            required >= 0 && players.HasGoodsAmount(playerId, good, required);
    }
}
