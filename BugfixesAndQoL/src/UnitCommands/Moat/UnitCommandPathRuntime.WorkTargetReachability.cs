using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime
    {
        internal void EnsureMoatWorkReachability(MoatWorkSelectionScope scope)
        {
            PrepareMovementSearch(null, scope.PlayerId, scope);
            bool reusable = scope.ReachabilityGeneration > 0 &&
                scope.ReachabilityGeneration == gridGeneration && cacheMapEpoch == mapEpoch &&
                cachePlayerId == scope.PlayerId && cacheStartX == scope.StartX &&
                cacheStartY == scope.StartY && !cacheIncludesEnemyRoutes;
            if (reusable)
                return;

            // Only this synchronous selection and its exact resolver hand-off share results.
            // Nested searches may replace the single map; their data must not leak back.
            scope.EndpointRoutes.Clear();
            cacheMapEpoch = -1;
            long started = Stopwatch.GetTimestamp();
            EnsureReachabilityMap(scope.PlayerId, scope.StartX, scope.StartY, deferTraversal: true, owner: scope);
            scope.ReachabilityGeneration = gridGeneration;
            scope.SearchBuilds++;
            scope.SearchExpandedNodes += cachedReachabilityExpandedNodes;
            scope.SearchMilliseconds += (Stopwatch.GetTimestamp() - started) * 1000.0 /
                Stopwatch.Frequency;
        }

        internal bool TryGetMoatWorkRoute(
            MoatWorkSelectionScope scope, int targetX, int targetY,
            out RouteProbeSummary summary)
        {
            summary = default;
            if (scope == null || !scope.Matches(mapEpoch, GameTileManagerAPI.Instance.GetTileManager()) ||
                scope.CapturedTick != CaptureCurrentGameTick() ||
                (uint)targetX >= MapWidth || (uint)targetY >= MapWidth ||
                !APIShared.UnitAccess.TryGetById(scope.UnitId, out GameUnit* unit, out _) ||
                unit == null || !APIShared.UnitAccess.IsReallyAlive(unit) || !CanDigMoat(unit) ||
                unit->r_ControllableForPlayerId != scope.PlayerId ||
                unit->r_CurrentTilePositionX != scope.StartX ||
                unit->r_CurrentTilePositionY != scope.StartY)
                return false;

            if (!TraversalEnabled)
            {
                if (!ManualCommandsEnabled || !IsCompletedMoatTile(scope.StartTileId) ||
                    !ProbeNativeManualPath(scope.UnitId, targetX, targetY)) return false;
                summary = new RouteProbeSummary(scope.PlayerId) {
                    RouteFound = true, AttackProbeEvaluated = true, ReachedWithMoat = true,
                    FriendlyMoatTiles = 1, StartRegion = scope.StartRegion,
                    TargetRegion = pathRegionGrid[GameTileManagerAPI.Instance.GetTileId(targetX, targetY)],
                    RouteDistance = Math.Max(Math.Abs(targetX - scope.StartX), Math.Abs(targetY - scope.StartY)) };
                return true;
            }
            EnsureMoatWorkReachability(scope);
            int cell = targetY * MapWidth + targetX;
            if (scope.EndpointRoutes.TryGetValue(cell, out summary))
            {
                scope.EndpointCacheHits++;
                return summary.RouteFound;
            }
            int expandedBefore = cachedReachabilityExpandedNodes;
            long searchStart = Stopwatch.GetTimestamp();
            summary = GetCachedRouteSummaryForTarget(targetX, targetY);
            scope.SearchExpandedNodes += cachedReachabilityExpandedNodes - expandedBefore;
            scope.SearchMilliseconds += (Stopwatch.GetTimestamp() - searchStart) * 1000.0 / Stopwatch.Frequency;
            summary.AttackProbeEvaluated = true;
            summary.RouteFound = summary.ReachedWithMoat && !summary.ReachedWithoutMoat &&
                summary.FriendlyMoatTiles > 0;
            summary.RouteDistance = summary.ReachedWithMoat
                ? distanceWithMoat[cell] : int.MaxValue;
            scope.EndpointRoutes.Add(cell, summary);
            return summary.RouteFound;
        }

        internal bool TryAllowDigWorkRegionPair(
            IntPtr pathManager,
            int playerId,
            int sourceRegion,
            int targetRegion,
            int movementProfile,
            int vanillaResult)
        {
            try
            {
                MoatWorkSelectionScope scope = activeMoatWorkSelection;
                if (vanillaResult != 0 || scope == null || scope.RelationshipMode != 1 ||
                    pathManager != nativePathManager || playerId != scope.PlayerId ||
                    sourceRegion < 0 || sourceRegion > MaximumRegionId ||
                    targetRegion <= 0 || targetRegion > MaximumRegionId)
                {
                    return false;
                }
                return EvaluateDigWorkRegion(scope, targetRegion, "E2610", movementProfile);
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("dig-moat-work-region-pair", ex);
                return false;
            }
        }

        internal bool TryAllowDigWorkRegionSearch(
            IntPtr pathManager,
            int playerId,
            int targetRegion,
            int startX,
            int startY,
            int vanillaResult,
            out int effectiveResult)
        {
            effectiveResult = vanillaResult;
            try
            {
                MoatWorkSelectionScope scope = activeMoatWorkSelection;
                if (scope == null || scope.RelationshipMode != 1 ||
                    pathManager != nativePathManager || playerId != scope.PlayerId ||
                    targetRegion <= 0 || targetRegion > MaximumRegionId ||
                    startX != scope.StartX || startY != scope.StartY ||
                    vanillaResult != 0)
                {
                    return false;
                }
                if (!EvaluateDigWorkRegion(scope, targetRegion, "E7C40", 0))
                    return false;
                effectiveResult = targetRegion;
                return true;
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("dig-moat-work-region-search", ex);
                effectiveResult = vanillaResult;
                return false;
            }
        }

        internal bool EvaluateDigWorkRegion(
            MoatWorkSelectionScope scope,
            int targetRegion,
            string helper,
            int movementProfile)
        {
            if (!scope.Matches(mapEpoch, scope.TileManager) || disposed ||
                scope.TileManager != GameTileManagerAPI.Instance.GetTileManager() ||
                !APIShared.UnitAccess.TryGetById(
                    scope.UnitId, out GameUnit* unit, out _) ||
                unit == null || !APIShared.UnitAccess.IsReallyAlive(unit) ||
                unit->r_ControllableForPlayerId != scope.PlayerId || !CanDigMoat(unit) ||
                unit->r_CurrentTilePositionX != scope.StartX ||
                unit->r_CurrentTilePositionY != scope.StartY)
            {
                return false;
            }
            if (scope.RegionDecisions.TryGetValue(targetRegion, out bool cached))
                return cached;

            scope.DigFallbackEvaluations++;
            EnsureMoatWorkReachability(scope);
            RouteProbeSummary summary = GetCachedRouteSummaryForRegion(targetRegion);
            bool allowed = summary.ReachedWithMoat && !summary.ReachedWithoutMoat &&
                summary.FriendlyMoatTiles > 0;
            summary.RouteFound = allowed;
            summary.AttackProbeEvaluated = true;
            scope.RegionDecisions[targetRegion] = allowed;
            scope.RegionSummaries[targetRegion] = summary;
            scope.LastRegionHelper = helper;
            scope.LastMovementProfile = movementProfile;
            scope.MergeRoute(summary);
            if (allowed)
                scope.DigFallbackAllowed++;
            return allowed;
        }
    }
}
