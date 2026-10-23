extern alias BugfixesRuntime;
using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using BugfixesRuntime::BugfixesAndQoL.UnitCommands;
using static BugfixesRuntime::BugfixesAndQoL.UnitCommands.UnitCommandPathRuntime;

namespace MoatMove
{
    // Moved from BugfixesAndQoL UnitCommandPathRuntime.WorkTargetReachability.cs: friendly-moat code runs only with MoatMove.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        internal void EnsureMoatWorkReachability(MoatWorkSelectionScope scope)
        {
            runtime.PrepareMovementSearch(null, scope.PlayerId, scope);
            bool reusable = scope.ReachabilityGeneration > 0 &&
                scope.ReachabilityGeneration == gridGeneration && runtime.cacheMapEpoch == runtime.mapEpoch &&
                runtime.cachePlayerId == scope.PlayerId && runtime.cacheStartX == scope.StartX &&
                runtime.cacheStartY == scope.StartY && !cacheIncludesEnemyRoutes;
            if (reusable)
                return;

            // Only this synchronous selection and its exact resolver hand-off share results.
            // Nested searches may replace the single map; their data must not leak back.
            scope.EndpointRoutes.Clear();
            runtime.cacheMapEpoch = -1;
            long started = (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L);
            long measured = 0;
            Shared.DevDiag.Start(ref measured);
            EnsureReachabilityMap(scope.PlayerId, scope.StartX, scope.StartY, deferTraversal: true, owner: scope);
            Shared.DevDiag.StopSpike(ref measured, "uc.moatWork.reachabilityMap", 10000, () =>
                $"player={scope.PlayerId} unit={scope.UnitId} mode={scope.RelationshipMode} " +
                $"start=({scope.StartX},{scope.StartY}) expandedNodes={cachedReachabilityExpandedNodes} " +
                $"buildsInScope={scope.SearchBuilds + 1}");
            scope.ReachabilityGeneration = gridGeneration;
            scope.SearchBuilds++;
            scope.SearchExpandedNodes += cachedReachabilityExpandedNodes;
            scope.SearchMilliseconds += ((DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L) - started) * 1000.0 /
                Stopwatch.Frequency;
        }

        internal override bool TryGetMoatWorkRouteTraversal(
            MoatWorkSelectionScope scope, int targetX, int targetY,
            out RouteProbeSummary summary)
        {
            EnsureMoatWorkReachability(scope);
            int cell = targetY * MapWidth + targetX;
            if (scope.EndpointRoutes.TryGetValue(cell, out summary))
            {
                scope.EndpointCacheHits++;
                return summary.RouteFound;
            }
            int expandedBefore = cachedReachabilityExpandedNodes;
            long searchStart = (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L);
            summary = GetCachedRouteSummaryForTarget(targetX, targetY);
            scope.SearchExpandedNodes += cachedReachabilityExpandedNodes - expandedBefore;
            scope.SearchMilliseconds += ((DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L) - searchStart) * 1000.0 / Stopwatch.Frequency;
            summary.AttackProbeEvaluated = true;
            summary.RouteFound = summary.ReachedWithMoat && !summary.ReachedWithoutMoat &&
                summary.FriendlyMoatTiles > 0;
            summary.RouteDistance = summary.ReachedWithMoat
                ? distanceWithMoat[cell] : int.MaxValue;
            scope.EndpointRoutes.Add(cell, summary);
            return summary.RouteFound;
        }
    }
}
