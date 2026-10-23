extern alias BugfixesRuntime;
using BepInEx.Logging;
using APIShared;
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
using BugfixesRuntime::BugfixesAndQoL.UnitCommands;
using static BugfixesRuntime::BugfixesAndQoL.UnitCommands.UnitCommandPathRuntime;

namespace MoatMove
{
    // Moved from BugfixesAndQoL UnitCommandPathRuntime.TargetResolution.cs: friendly-moat code runs only with MoatMove.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
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

            if (!Shared.GameBuildingFootprint.TryGetBounds(building, out Shared.GameBuildingFootprintBounds bounds))
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
                    if (!adjacentToFootprint || runtime.movementTargetAvailability[cell] == 0 ||
                        (runtime.tileFlags[tileId] & OrdinaryWalkableTileFlag) == 0 ||
                        (runtime.tileFlags[tileId] & CursorSpecialStructureTileFlagMask) != 0)
                        continue;
                    candidateObserved = true;
                    if (!runtime.ProbeCursorConnectivity(scope.PlayerId, scope.StartTileId, tileId,
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
