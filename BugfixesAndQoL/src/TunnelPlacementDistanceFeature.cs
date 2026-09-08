// Feature: Keep human-placed buildings clear of hostile completed moats.
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
        private bool invalidMoatRecordLogged;
        private bool unavailableMoatLayoutLogged;
        private bool callbackFailureLogged;
        private bool fixedNativeLayoutValidated;

        // CrusaderDE.dll FBCB9319: completed-moat ownership is stored in the moat
        // record, not in WallOwnerGrid. The record owner is already a game player ID.
        private const int MoatIdGridOffset = 0x1EA23F0;
        private const int MoatRecordArrayOffset = 0x1F3EE30;
        private const int MoatRecordCountOffset = 0x2038E30;
        private const int MoatRecordSize = 0x10;
        private const int MoatRecordTileIdOffset = 0x00;
        private const int MoatRecordOwnerOffset = 0x0C;

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
                "Bugfixes and QoL hostile placement-clearance validation subscribed.");
        }

        internal void SetFixedNativeLayoutValidated(bool validated) =>
            fixedNativeLayoutValidated = validated;

        private void OnPlacementValidation(BuildingPlacementValidationEventArgs args)
        {
            try
            {
                if (args == null ||
                    !settings.EnableMod ||
                    !settings.EnableTunnelPlacementDistanceFix ||
                    Shared.GameModeHelper.IsMapEditor())
                {
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
                            $"Building placement-clearance validation ignored invalid player ID {args.PlayerId}.");
                    }
                    return;
                }

                int footprintSize = BuildingScales.GetScale(args.Mappers);
                if (!TunnelPlacementDistancePolicy.ShouldApply(
                        settings.EnableMod,
                        settings.EnableTunnelPlacementDistanceFix,
                        isMapEditor: false,
                        players.IsAIPlayer(args.PlayerId),
                        args.Mappers,
                        footprintSize))
                {
                    return;
                }

                if (args.Unknown1 != footprintSize)
                {
                    LogUnexpectedScaleOnce(args.Mappers, footprintSize, args.Unknown1);
                    return;
                }

                GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
                bool isTunnel = TunnelPlacementDistancePolicy.IsTunnelMapper(args.Mappers);
                bool blocked = TunnelPlacementDistancePolicy.HasHostileOuterRingTile(
                    args.TileX,
                    args.TileY,
                    footprintSize,
                    tiles.IsTileInsideMapBounds,
                    (tileX, tileY) =>
                        (isTunnel && IsHostileStructureTile(
                            tiles, players, args.PlayerId, tileX, tileY)) ||
                        IsHostileCompletedMoatTile(
                            tiles, players, args.PlayerId, tileX, tileY));
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
                    $"Building placement-clearance validation failed and left Vanilla behavior unchanged: {ex}");
            }
        }

        private unsafe bool IsHostileCompletedMoatTile(
            GameTileManagerAPI tiles,
            GamePlayerManagerAPI players,
            int placingPlayerId,
            int tileX,
            int tileY)
        {
            int tileId = tiles.GetTileId(tileX, tileY);
            if ((tiles.GetTilePropertyFlag(tileId) & TilePropertyFlag.IsMoat) == 0)
                return false;

            if (!fixedNativeLayoutValidated)
            {
                if (!unavailableMoatLayoutLogged)
                {
                    unavailableMoatLayoutLogged = true;
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        "Building placement-clearance validation cannot resolve completed-moat ownership because the native layout is not validated; Vanilla behavior remains unchanged for moat clearance.");
                }
                return false;
            }

            byte* tileManager = (byte*)tiles.GetTileManager().ToPointer();
            if (tileManager == null)
                return LogInvalidMoatRecordOnce(tileId, "tile manager unavailable");

            int moatId = *(ushort*)(tileManager + MoatIdGridOffset + tileId * sizeof(ushort));
            int moatRecordCount = *(int*)(tileManager + MoatRecordCountOffset);
            if (moatId <= 0 || moatId > TunnelPlacementDistancePolicy.MaximumMoatRecordId ||
                moatRecordCount <= 0 ||
                moatRecordCount > TunnelPlacementDistancePolicy.MaximumMoatRecordId + 1 ||
                moatId >= moatRecordCount)
            {
                return LogInvalidMoatRecordOnce(
                    tileId, $"moatId={moatId}, moatRecordCount={moatRecordCount}");
            }

            byte* record = tileManager + MoatRecordArrayOffset + moatId * MoatRecordSize;
            int recordTileId = *(int*)(record + MoatRecordTileIdOffset);
            int recordOwnerId = record[MoatRecordOwnerOffset];
            if (!TunnelPlacementDistancePolicy.TryResolveCompletedMoatOwner(
                    tileId,
                    moatId,
                    moatRecordCount,
                    recordTileId,
                    recordOwnerId,
                    players.IsPlayerIdValid,
                    out int ownerId))
            {
                return LogInvalidMoatRecordOnce(
                    tileId,
                    $"moatId={moatId}, recordTileId={recordTileId}, ownerId={recordOwnerId}");
            }

            return TunnelPlacementDistancePolicy.IsHostileOwner(
                placingPlayerId, ownerId, players.IsPlayerAlliedTo);
        }

        private bool LogInvalidMoatRecordOnce(int tileId, string reason)
        {
            if (!invalidMoatRecordLogged)
            {
                invalidMoatRecordLogged = true;
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Building placement-clearance validation ignored an invalid completed-moat record at tile {tileId}: {reason}. Vanilla behavior remains unchanged for that tile.");
            }
            return false;
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
                        $"Building placement-clearance validation ignored invalid building owner ID {ownerId}.");
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
                "Building placement-clearance validation left Vanilla behavior unchanged because " +
                $"the footprint contract was unexpected: mapper={mapper}, apiScale={apiScale}, eventScale={eventScale}.");
        }

        public void Dispose()
        {
            placementSubscription?.Dispose();
            placementSubscription = null;
        }
    }
}
