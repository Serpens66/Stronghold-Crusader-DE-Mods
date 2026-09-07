// Feature: Prevent tunnels from touching hostile buildings or walls.
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;

namespace BugfixesAndQoL
{
    internal sealed class TunnelPlacementDistanceFeature : IDisposable
    {
        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private IDisposable placementSubscription;
        private bool unexpectedScaleLogged;
        private bool invalidPlayerLogged;
        private bool invalidBuildingLogged;
        private bool invalidWallOwnerLogged;
        private bool callbackFailureLogged;

        internal TunnelPlacementDistanceFeature(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        internal void Initialize()
        {
            if (placementSubscription != null)
                return;

            placementSubscription = BuildingR3EventHooks.OnPlacementValidation.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnPlacementValidation);
            Shared.DebugLogHelper.LogInfo(
                log,
                "Bugfixes and QoL tunnel placement-distance validation subscribed.");
        }

        private void OnPlacementValidation(BuildingPlacementValidationEventArgs args)
        {
            try
            {
                if (args == null ||
                    !settings.EnableMod ||
                    !settings.EnableTunnelPlacementDistanceFix ||
                    Shared.GameModeHelper.IsMapEditor() ||
                    !TunnelPlacementDistancePolicy.IsTargetMapper(args.Mappers))
                {
                    return;
                }

                int footprintSize = BuildingScales.GetScale(args.Mappers);
                if (!TunnelPlacementDistancePolicy.ShouldApply(
                        settings.EnableMod,
                        settings.EnableTunnelPlacementDistanceFix,
                        isMapEditor: false,
                        args.Mappers,
                        footprintSize))
                {
                    LogUnexpectedScaleOnce(args.Mappers, footprintSize, args.Unknown1);
                    return;
                }

                if (args.Unknown1 != footprintSize)
                {
                    LogUnexpectedScaleOnce(args.Mappers, footprintSize, args.Unknown1);
                    return;
                }

                GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
                if (!players.IsPlayerIdValid(args.PlayerId))
                {
                    if (!invalidPlayerLogged)
                    {
                        invalidPlayerLogged = true;
                        Shared.DebugLogHelper.LogWarning(
                            log,
                            $"Tunnel placement-distance validation ignored invalid player ID {args.PlayerId}.");
                    }
                    return;
                }

                GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
                bool blocked = TunnelPlacementDistancePolicy.HasHostileOuterRingTile(
                    args.TileX,
                    args.TileY,
                    footprintSize,
                    tiles.IsTileInsideMapBounds,
                    (tileX, tileY) => IsHostileStructureTile(
                        tiles,
                        players,
                        args.PlayerId,
                        tileX,
                        tileY));
                if (!blocked)
                    return;

                // Only add a rejection. Never clear Vanilla's result or another mod's rule.
                args.CustomValidationRules = true;
                args.ForceBlockPlacementState = true;
            }
            catch (Exception ex)
            {
                if (callbackFailureLogged)
                    return;
                callbackFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Tunnel placement-distance validation failed and left Vanilla behavior unchanged: {ex}");
            }
        }

        private unsafe bool IsHostileStructureTile(
            GameTileManagerAPI tiles,
            GamePlayerManagerAPI players,
            int placingPlayerId,
            int tileX,
            int tileY)
        {
            int tileId = tiles.GetTileId(tileX, tileY);
            int buildingId = tiles.GetTileBuildingId(tileId);
            if (buildingId > 0)
            {
                if (GameBuildingManagerAPI.Instance.TryGetBuildingById(
                        buildingId,
                        out GameBuilding* building))
                {
                    if (building->r_AliveState == AliveState.IsAlive &&
                        IsHostilePlayer(players, placingPlayerId, building->r_PlayerIdOwner))
                        return true;
                }
                else if (!invalidBuildingLogged)
                {
                    invalidBuildingLogged = true;
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Tunnel placement-distance validation could not resolve building ID {buildingId}; that tile remains governed by Vanilla.");
                }
            }

            if ((tiles.GetTilePropertyFlag(tileId) & TilePropertyFlag.IsWall) == 0)
                return false;

            int wallOwnerId = TunnelPlacementDistancePolicy.WallOwnerToGamePlayerId(
                tiles.GetTilePlayerOwnerId(tileId));
            if (!players.IsPlayerIdValid(wallOwnerId))
            {
                if (!invalidWallOwnerLogged)
                {
                    invalidWallOwnerLogged = true;
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Tunnel placement-distance validation ignored invalid wall owner ID {wallOwnerId}.");
                }
                return false;
            }
            return IsHostilePlayer(players, placingPlayerId, wallOwnerId);
        }

        private bool IsHostilePlayer(
            GamePlayerManagerAPI players,
            int placingPlayerId,
            int ownerId)
        {
            if (!players.IsPlayerIdValid(ownerId))
            {
                if (!invalidBuildingLogged)
                {
                    invalidBuildingLogged = true;
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Tunnel placement-distance validation ignored invalid building owner ID {ownerId}.");
                }
                return false;
            }
            return TunnelPlacementDistancePolicy.IsHostileOwner(
                placingPlayerId,
                ownerId,
                players.IsPlayerAlliedTo);
        }

        private void LogUnexpectedScaleOnce(eMappers mapper, int apiScale, int eventScale)
        {
            if (unexpectedScaleLogged)
                return;
            unexpectedScaleLogged = true;
            Shared.DebugLogHelper.LogWarning(
                log,
                "Tunnel placement-distance validation left Vanilla behavior unchanged because " +
                $"the footprint contract was unexpected: mapper={mapper}, apiScale={apiScale}, eventScale={eventScale}.");
        }

        public void Dispose()
        {
            placementSubscription?.Dispose();
            placementSubscription = null;
        }
    }
}
