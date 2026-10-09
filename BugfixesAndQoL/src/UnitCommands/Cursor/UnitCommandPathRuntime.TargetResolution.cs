using BepInEx.Logging;
using APIShared;
using RedBird.Backends.NativeX64;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime : IDisposable
    {
        internal bool TryResolveHostileLivingUnitFromRawCursor(
            int playerId,
            uint rawUnitId,
            out int targetUnitId,
            out uint targetUnitGlobalId,
            out int targetX,
            out int targetY,
            out int targetTileId)
        {
            targetUnitId = -1;
            targetUnitGlobalId = 0;
            targetX = -1;
            targetY = -1;
            targetTileId = -1;
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            if (!playerApi.IsPlayerIdValid(playerId) || rawUnitId == 0 || rawUnitId > int.MaxValue)
                return false;

            int unitId = (int)rawUnitId;
            if (!APIShared.UnitAccess.TryGetById(unitId, out GameUnit* target, out _) ||
                target == null || !APIShared.UnitAccess.IsReallyAlive(target) ||
                target->r_GlobalId == 0)
            {
                return false;
            }

            int ownerId = target->r_ControllableForPlayerId;
            if (!playerApi.IsPlayerIdValid(ownerId) || ownerId == playerId ||
                playerApi.IsPlayerAlliedTo(playerId, ownerId))
            {
                return false;
            }

            int x = target->r_CurrentTilePositionX;
            int y = target->r_CurrentTilePositionY;
            if (x < 0 || x >= MapWidth || y < 0 || y >= MapWidth)
                return false;
            int tileId = GameTileManagerAPI.Instance.GetTileId(x, y);
            if (!IsValidTileId(tileId))
                return false;

            targetUnitId = unitId;
            targetUnitGlobalId = target->r_GlobalId;
            targetX = x;
            targetY = y;
            targetTileId = tileId;
            return true;
        }

        internal bool TryResolveHostileLivingBuildingFromRawCursor(
            int playerId,
            uint rawBuildingId,
            uint rawHoverBuildingTileId,
            uint rawMouseTileId2,
            uint rawMouseTileId,
            int rawMouseX,
            int rawMouseY,
            out int targetX,
            out int targetY,
            out int targetTileId,
            out BuildingCursorTarget target)
        {
            targetX = -1;
            targetY = -1;
            targetTileId = -1;
            target = default;
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            if (!playerApi.IsPlayerIdValid(playerId) || rawBuildingId == 0 ||
                rawBuildingId > int.MaxValue)
            {
                return false;
            }

            int buildingId = (int)rawBuildingId;
            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(
                    buildingId, out GameBuilding* building) ||
                building == null || building->r_AliveState != AliveState.IsAlive ||
                building->r_GlobalId == 0 || IsWallStairOrRampStructure(building->r_BuildingType))
            {
                return false;
            }

            int ownerId = building->r_PlayerIdOwner;
            if (!playerApi.IsPlayerIdValid(ownerId) || ownerId == playerId ||
                playerApi.IsPlayerAlliedTo(playerId, ownerId))
            {
                return false;
            }

            BuildingHoverTileSource hoverTileSource;
            if (TryResolveRawBuildingFootprintTile(
                    buildingId, building, rawHoverBuildingTileId,
                    out targetX, out targetY, out targetTileId))
            {
                hoverTileSource = BuildingHoverTileSource.BuildingTile;
            }
            else if (TryResolveRawBuildingFootprintTile(
                         buildingId, building, rawMouseTileId2,
                         out targetX, out targetY, out targetTileId))
            {
                hoverTileSource = BuildingHoverTileSource.MouseTile2;
            }
            else if (TryResolveRawBuildingFootprintTile(
                         buildingId, building, rawMouseTileId,
                         out targetX, out targetY, out targetTileId))
            {
                hoverTileSource = BuildingHoverTileSource.MouseTile;
            }
            else if (TryResolveNearestBuildingFootprintTile(
                         buildingId, building, rawMouseTileId, rawMouseX, rawMouseY,
                         out targetX, out targetY, out targetTileId))
            {
                hoverTileSource = BuildingHoverTileSource.NearestFootprint;
            }
            else
            {
                return false;
            }

            target = new BuildingCursorTarget
            {
                BuildingId = buildingId,
                GlobalId = building->r_GlobalId,
                OwnerId = ownerId,
                BuildingType = building->r_BuildingType,
                HoverTileId = targetTileId,
                HoverTileSource = hoverTileSource
            };
            return true;
        }

        internal bool TryResolveNearestBuildingFootprintTile(
            int buildingId,
            GameBuilding* building,
            uint rawMouseTileId,
            int rawMouseX,
            int rawMouseY,
            out int targetX,
            out int targetY,
            out int targetTileId)
        {
            targetX = -1;
            targetY = -1;
            targetTileId = -1;
            if (building == null)
                return false;

            int mouseX;
            int mouseY;
            if (rawMouseTileId <= int.MaxValue && IsValidTileId((int)rawMouseTileId))
            {
                // The cursor dispatcher can leave r_MouseTileX/Y at (0,0) over a sprite
                // overhang while r_MouseTileId and r_HoverOverBuildingId remain valid.
                UnmanagedVector2<ushort> mousePosition =
                    GameTileManagerAPI.Instance.GetTileVectorFromId((int)rawMouseTileId);
                mouseX = mousePosition.X;
                mouseY = mousePosition.Y;
                if (rawMouseX >= 0 && rawMouseX < MapWidth &&
                    rawMouseY >= 0 && rawMouseY < MapWidth &&
                    GameTileManagerAPI.Instance.GetTileId(rawMouseX, rawMouseY) ==
                        (int)rawMouseTileId)
                {
                    mouseX = rawMouseX;
                    mouseY = rawMouseY;
                }
            }
            else
            {
                return false;
            }

            long bestDistanceSquared = long.MaxValue;
            uint gridSize = building->r_OccupyTileGridSize;
            if (gridSize == 0 || gridSize > APIShared.Internal.GameBuildingFootprint.MaximumGridSize)
                return false;
            int tileCount = checked((int)(gridSize * gridSize));
            uint* occupiedTileIds = &building->r_OccupiedTileIdsArrayBegin;
            for (int index = 0; index < tileCount; index++)
            {
                uint rawCandidateTileId = occupiedTileIds[index];
                if (rawCandidateTileId > int.MaxValue)
                    continue;
                int candidateTileId = (int)rawCandidateTileId;
                if (!IsValidTileId(candidateTileId) ||
                    GameTileManagerAPI.Instance.GetTileBuildingId(candidateTileId) != buildingId)
                    continue;
                UnmanagedVector2<ushort> candidate = GameTileManagerAPI.Instance.GetTileVectorFromId(candidateTileId);
                int x = candidate.X;
                int y = candidate.Y;

                    long deltaX = x - mouseX;
                    long deltaY = y - mouseY;
                    long distanceSquared = deltaX * deltaX + deltaY * deltaY;
                    if (distanceSquared > bestDistanceSquared ||
                        (distanceSquared == bestDistanceSquared &&
                         targetTileId >= 0 && candidateTileId >= targetTileId))
                    {
                        continue;
                    }

                    bestDistanceSquared = distanceSquared;
                    targetX = x;
                    targetY = y;
                    targetTileId = candidateTileId;
            }

            return targetTileId >= 0;
        }

        internal static string FormatBuildingHoverTileSource(BuildingHoverTileSource source)
        {
            switch (source)
            {
                case BuildingHoverTileSource.BuildingTile:
                    return "buildingTile";
                case BuildingHoverTileSource.MouseTile2:
                    return "mouse2";
                case BuildingHoverTileSource.MouseTile:
                    return "mouse";
                case BuildingHoverTileSource.NearestFootprint:
                    return "nearest-footprint";
                default:
                    return "none";
            }
        }

        internal bool TryResolveRawBuildingFootprintTile(
            int buildingId,
            GameBuilding* building,
            uint rawTileId,
            out int targetX,
            out int targetY,
            out int targetTileId)
        {
            targetX = -1;
            targetY = -1;
            targetTileId = -1;
            if (building == null || rawTileId > int.MaxValue || !IsValidTileId((int)rawTileId))
                return false;

            int candidateTileId = (int)rawTileId;
            if (GameTileManagerAPI.Instance.GetTileBuildingId(candidateTileId) != buildingId ||
                !APIShared.Internal.GameBuildingFootprint.ContainsTileId(building, candidateTileId))
                return false;
            UnmanagedVector2<ushort> candidatePosition =
                GameTileManagerAPI.Instance.GetTileVectorFromId(candidateTileId);
            targetX = candidatePosition.X;
            targetY = candidatePosition.Y;
            targetTileId = candidateTileId;
            return true;
        }

        internal bool TryGetHostileLivingBuildingForCursor(
            int playerId,
            int targetTileId,
            out BuildingCursorTarget target,
            out bool wallLike)
        {
            target = default;
            wallLike = false;
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            if (!playerApi.IsPlayerIdValid(playerId) || !IsValidTileId(targetTileId))
                return false;

            int hoveredBuildingId = playerApi.GetHoveredBuildingId();
            int hoverTileId = playerApi.GetHoveredBuildingTileId();
            int structureBuildingId = GameTileManagerAPI.Instance.GetTileBuildingId(targetTileId);
            int buildingId = hoveredBuildingId > 0 ? hoveredBuildingId : structureBuildingId;
            bool hoverTileBelongs = IsValidTileId(hoverTileId) && buildingId > 0 &&
                GameTileManagerAPI.Instance.GetTileBuildingId(hoverTileId) == buildingId;
            if (buildingId <= 0 ||
                (structureBuildingId != buildingId && !hoverTileBelongs) ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                building == null || building->r_AliveState != AliveState.IsAlive ||
                building->r_GlobalId == 0)
            {
                return false;
            }

            eStructs buildingType = building->r_BuildingType;
            wallLike = IsWallStairOrRampStructure(buildingType);
            target = new BuildingCursorTarget
            {
                BuildingId = buildingId,
                GlobalId = building->r_GlobalId,
                OwnerId = building->r_PlayerIdOwner,
                BuildingType = buildingType,
                HoverTileId = hoverTileId
            };
            if (wallLike)
                return false;

            int ownerId = building->r_PlayerIdOwner;
            return playerApi.IsPlayerIdValid(ownerId) && ownerId != playerId &&
                !playerApi.IsPlayerAlliedTo(playerId, ownerId);
        }

        internal static bool IsWallStairOrRampStructure(eStructs buildingType) =>
            buildingType == eStructs.STRUCT_WOOD_WALL ||
            buildingType == eStructs.STRUCT_STONE_WALL ||
            buildingType == eStructs.STRUCT_CRENAL_WALL ||
            buildingType == eStructs.STRUCT_STAIRS ||
            buildingType == eStructs.STRUCT_WAS_WALL;

        internal bool TryValidateHostileBuildingTarget(
            int buildingId,
            uint buildingGlobalId,
            int playerId,
            out GameBuilding* building)
        {
            building = null;
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            if (buildingId <= 0 || buildingGlobalId == 0 ||
                !playerApi.IsPlayerIdValid(playerId) ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(
                    buildingId, out GameBuilding* candidate) ||
                candidate == null || candidate->r_AliveState != AliveState.IsAlive ||
                candidate->r_GlobalId != buildingGlobalId ||
                IsWallStairOrRampStructure(candidate->r_BuildingType))
            {
                return false;
            }

            int ownerId = candidate->r_PlayerIdOwner;
            if (!playerApi.IsPlayerIdValid(ownerId) || ownerId == playerId ||
                playerApi.IsPlayerAlliedTo(playerId, ownerId))
            {
                return false;
            }

            building = candidate;
            return true;
        }

        internal static bool TryGetUnitAttackMoveTile(GameUnit* unit, out int tileId)
        {
            tileId = -1;
            if (unit == null || unit->r_AttackMoveToTargetTileX < 0 ||
                unit->r_AttackMoveToTargetTileX >= MapWidth ||
                unit->r_AttackMoveToTargetTileY < 0 ||
                unit->r_AttackMoveToTargetTileY >= MapWidth)
            {
                return false;
            }

            tileId = GameTileManagerAPI.Instance.GetTileId(
                unit->r_AttackMoveToTargetTileX, unit->r_AttackMoveToTargetTileY);
            return IsValidTileId(tileId);
        }

        internal bool IsExactBuildingContextTile(
            int buildingId, GameBuilding* building, int footprintTileId)
        {
            if (building == null || !IsValidTileId(footprintTileId) ||
                GameTileManagerAPI.Instance.GetTileBuildingId(footprintTileId) != buildingId)
            {
                return false;
            }

            // DA020 uses the StructureGrid identity and this exact flag mask. Some buildings
            // reserve valid context tiles outside their smaller record bounding rectangle.
            return (tileFlags[footprintTileId] & BuildingContextBlockingTileFlagMask) == 0;
        }

        internal bool IsValidBuildingApproachPair(
            int buildingId,
            GameBuilding* building,
            int approachTileId,
            int footprintTileId)
        {
            if (!IsExactBuildingContextTile(buildingId, building, footprintTileId) ||
                !IsValidTileId(approachTileId))
            {
                return false;
            }

            // StructureGrid also reserves surrounding tiles for some buildings. Those tiles
            // remain valid movement endpoints when Vanilla's native occupancy mask exposes at
            // least one traversable direction; the Assassin fix uses the same distinction.
            ushort approachBuildingId =
                GameTileManagerAPI.Instance.GetTileBuildingId(approachTileId);
            if (approachBuildingId != 0 && nativeMovementMasks[approachTileId] == 0)
                return false;

            UnmanagedVector2<ushort> approach =
                GameTileManagerAPI.Instance.GetTileVectorFromId(approachTileId);
            UnmanagedVector2<ushort> footprint =
                GameTileManagerAPI.Instance.GetTileVectorFromId(footprintTileId);
            int approachX = approach.X;
            int approachY = approach.Y;
            int footprintX = footprint.X;
            int footprintY = footprint.Y;
            if (approachX < 0 || approachX >= MapWidth || approachY < 0 ||
                approachY >= MapWidth ||
                GameTileManagerAPI.Instance.GetTileId(approachX, approachY) != approachTileId ||
                Math.Abs(approachX - footprintX) + Math.Abs(approachY - footprintY) != 1)
            {
                return false;
            }

            return IsWalkableBuildingApproachEndpoint(approachTileId);
        }

        internal bool IsWalkableBuildingApproachEndpoint(int tileId)
        {
            if (!IsValidTileId(tileId))
                return false;
            UnmanagedVector2<ushort> position =
                GameTileManagerAPI.Instance.GetTileVectorFromId(tileId);
            int x = position.X;
            int y = position.Y;
            if (x < 0 || x >= MapWidth || y < 0 || y >= MapWidth ||
                GameTileManagerAPI.Instance.GetTileId(x, y) != tileId ||
                movementTargetAvailability[y * MapWidth + x] == 0)
            {
                return false;
            }

            ushort reservedByBuilding = GameTileManagerAPI.Instance.GetTileBuildingId(tileId);
            if (reservedByBuilding != 0)
                return nativeMovementMasks[tileId] != 0;
            return (tileFlags[tileId] & OrdinaryWalkableTileFlag) != 0 &&
                (tileFlags[tileId] & CursorSpecialStructureTileFlagMask) == 0;
        }

        internal bool TryProbeBuildingApproachCursorRoute(
            AttackCursorPairScope scope,
            out bool normalReachable,
            out bool friendlyMoatSeparated,
            out int approachX,
            out int approachY,
            out RouteProbeSummary summary)
        {
            normalReachable = false;
            friendlyMoatSeparated = false;
            approachX = -1;
            approachY = -1;
            summary = default;
            if (scope == null || scope.BuildingId <= 0 ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(
                    scope.BuildingId, out GameBuilding* building) || building == null ||
                building->r_AliveState != AliveState.IsAlive ||
                building->r_GlobalId != scope.BuildingGlobalId ||
                building->r_PlayerIdOwner != scope.BuildingOwnerId ||
                building->r_BuildingType != scope.BuildingType ||
                IsWallStairOrRampStructure(building->r_BuildingType))
            {
                return false;
            }

            if (!APIShared.Internal.GameBuildingFootprint.TryGetBounds(building, out APIShared.Internal.GameBuildingFootprintBounds bounds))
                return false;
            int minX = Math.Max(0, bounds.MinX - 1);
            int minY = Math.Max(0, bounds.MinY - 1);
            int maxX = Math.Min(MapWidth - 1, bounds.MaxX + 1);
            int maxY = Math.Min(MapWidth - 1, bounds.MaxY + 1);

            RouteProbeSummary observed = new RouteProbeSummary(scope.PlayerId);
            bool reachableWithMoat = false;
            bool candidateObserved = false;
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            IntPtr tileManager = GameTileManagerAPI.Instance.GetTileManager();
            if (tileManager == IntPtr.Zero || !playerApi.IsPlayerIdValid(scope.PlayerId))
                return false;

            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    int tileId = GameTileManagerAPI.Instance.GetTileId(x, y);
                    if (!IsValidTileId(tileId) ||
                        GameTileManagerAPI.Instance.GetTileBuildingId(tileId) == scope.BuildingId)
                    {
                        continue;
                    }
                    bool adjacentToFootprint = false;
                    for (int dy = -1; dy <= 1 && !adjacentToFootprint; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if ((dx == 0 && dy == 0) || x + dx < 0 || x + dx >= MapWidth ||
                                y + dy < 0 || y + dy >= MapWidth)
                                continue;
                            int adjacentTile = GameTileManagerAPI.Instance.GetTileId(x + dx, y + dy);
                            if (IsValidTileId(adjacentTile) &&
                                GameTileManagerAPI.Instance.GetTileBuildingId(adjacentTile) == scope.BuildingId)
                            {
                                adjacentToFootprint = true;
                                break;
                            }
                        }
                    }
                    int cell = y * MapWidth + x;
                    if (!adjacentToFootprint || movementTargetAvailability[cell] == 0 ||
                        (tileFlags[tileId] & OrdinaryWalkableTileFlag) == 0 ||
                        (tileFlags[tileId] & CursorSpecialStructureTileFlagMask) != 0)
                        continue;
                    candidateObserved = true;
                    if (!ProbeCursorConnectivity(scope.PlayerId, scope.StartTileId, tileId,
                        out RouteProbeSummary candidate)) continue;
                    bool withoutMoat = candidate.ReachedWithoutMoat;
                    bool withMoat = candidate.ReachedWithMoat;
                    observed.MergeObservations(candidate);
                    normalReachable |= withoutMoat;
                    reachableWithMoat |= withMoat;
                    if ((withoutMoat || withMoat) && approachX < 0)
                    {
                        approachX = x;
                        approachY = y;
                    }
                }
            }

            friendlyMoatSeparated = reachableWithMoat && !normalReachable &&
                observed.FriendlyMoatTiles > 0;
            observed.ReachedWithoutMoat = normalReachable;
            observed.ReachedWithMoat = reachableWithMoat;
            observed.RouteFound = normalReachable || reachableWithMoat;
            summary = observed;
            return candidateObserved;
        }

    }
}
