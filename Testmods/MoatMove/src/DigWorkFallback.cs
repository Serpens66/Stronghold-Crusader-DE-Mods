extern alias BugfixesRuntime;
using System;
using System.Collections.Generic;
using SHCDESE.API;
using SHCDESE.Interop;
using BugfixesRuntime::BugfixesAndQoL.UnitCommands;
using static BugfixesRuntime::BugfixesAndQoL.UnitCommands.UnitCommandPathRuntime;

namespace MoatMove
{
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        private sealed class DigWorkState
        {
            internal readonly Dictionary<int, bool> Regions = new Dictionary<int, bool>();
            internal int Generation;
        }

        internal override int FindDigWorkTarget(IntPtr tiles, int player, int unit, int vanillaResult)
        {
            if (vanillaResult >= 0 || !runtime.TraversalEnabled ||
                !runtime.TryCreateMoatWorkSelectionScope(tiles, player, unit, 1, out MoatWorkSelectionScope scope))
                return vanillaResult;

            // Retry only after the complete native selection found no work. Native
            // filtering, scoring and the single winning reservation stay authoritative.
            scope.TraversalWorkState = new DigWorkState();
            int result = runtime.SelectMoatWorkTarget(scope, tiles, player, unit, 1);
            if (result > 0)
            {
                try
                {
                    if (runtime.TryCreatePendingDigMoatTarget(scope, result, out PendingDigMoatTarget pending))
                        pendingDigMoatTarget = pending;
                }
                catch (Exception ex) { runtime.TryLogDiagnosticFailure("dig-moat-provider-handoff", ex); }
            }
            return result;
        }

        private bool IsCurrentDigWork(MoatWorkSelectionScope scope)
        {
            return runtime.TraversalEnabled && scope != null && scope.RelationshipMode == 1 &&
                scope.TraversalWorkState is DigWorkState &&
                scope.Matches(runtime.mapEpoch, GameTileManagerAPI.Instance.GetTileManager()) &&
                scope.CapturedTick == CaptureCurrentGameTick() &&
                APIShared.UnitAccess.TryGetById(scope.UnitId, out GameUnit* unit, out _) &&
                unit != null && APIShared.UnitAccess.IsReallyAlive(unit) && runtime.CanDigMoat(unit) &&
                unit->r_ControllableForPlayerId == scope.PlayerId &&
                unit->r_CurrentTilePositionX == scope.StartX && unit->r_CurrentTilePositionY == scope.StartY;
        }

        internal override bool TryAllowDigWorkRegion(MoatWorkSelectionScope scope, int region)
        {
            if (!IsCurrentDigWork(scope) || region <= 0 || region > MaximumRegionId) return false;
            var state = (DigWorkState)scope.TraversalWorkState;
            runtime.EnsureMoatWorkReachability(scope);
            if (state.Generation != scope.ReachabilityGeneration)
            {
                state.Regions.Clear();
                state.Generation = scope.ReachabilityGeneration;
            }
            if (state.Regions.TryGetValue(region, out bool cached)) return cached;
            int before = runtime.cachedReachabilityExpandedNodes;
            // A failed native query is the reason for the extension. Find a positive
            // moat witness; do not exhaust a second ground search to prove its absence.
            runtime.AdvanceReachabilityMap(targetRegion: region);
            scope.SearchExpandedNodes += runtime.cachedReachabilityExpandedNodes - before;
            bool allowed = runtime.reachedFriendlyMoatRegions[region] == runtime.gridGeneration;
            state.Regions[region] = allowed;
            return allowed;
        }

        internal override bool TryGetDigWorkRoute(MoatWorkSelectionScope scope, int x, int y,
            out RouteProbeSummary summary)
        {
            summary = default;
            if (!IsCurrentDigWork(scope) || (uint)x >= MapWidth || (uint)y >= MapWidth) return false;
            int tile = GameTileManagerAPI.Instance.GetTileId(x, y);
            if (!IsValidTileId(tile)) return false;
            runtime.EnsureMoatWorkReachability(scope);
            int cell = y * MapWidth + x;
            if (scope.EndpointRoutes.TryGetValue(cell, out summary))
            { scope.EndpointCacheHits++; return summary.RouteFound; }
            int before = runtime.cachedReachabilityExpandedNodes;
            runtime.AdvanceReachabilityMap(targetCell: cell, requireMoat: true);
            scope.SearchExpandedNodes += runtime.cachedReachabilityExpandedNodes - before;
            summary = runtime.cachedRouteSummary;
            summary.TargetRegion = runtime.pathRegionGrid[tile];
            summary.AttackProbeEvaluated = true;
            summary.RouteFound = summary.ReachedWithMoat = runtime.visitedWithMoat[cell] == runtime.gridGeneration;
            summary.ReachedWithoutMoat = false;
            summary.RouteDistance = summary.RouteFound ? runtime.distanceWithMoat[cell] : int.MaxValue;
            scope.EndpointRoutes[cell] = summary;
            return summary.RouteFound;
        }

        internal override PendingDigMoatTarget CreatePendingDigMoatTarget(MoatWorkSelectionScope scope, int moatId)
        {
            if (!IsCurrentDigWork(scope) ||
                !runtime.TryReadMoatRecordTile(scope.TileManager, moatId, out int tile, out int x, out int y)) return null;
            int region = runtime.pathRegionGrid[tile];
            if (!((DigWorkState)scope.TraversalWorkState).Regions.TryGetValue(region, out bool allowed) || !allowed ||
                !TryGetDigWorkRoute(scope, x, y, out RouteProbeSummary summary)) return null;
            return new PendingDigMoatTarget(runtime.mapEpoch, scope.TileManager, scope.UnitId, scope.PlayerId,
                moatId, scope.StartX, scope.StartY, tile, x, y, region, summary) { SearchScope = scope };
        }
    }
}
