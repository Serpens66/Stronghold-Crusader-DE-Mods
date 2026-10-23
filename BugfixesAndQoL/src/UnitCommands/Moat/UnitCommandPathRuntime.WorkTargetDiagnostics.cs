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
        [System.Diagnostics.Conditional("SERP_DEVDIAG")]
        internal void LogMoatWorkSelection(MoatWorkSelectionScope scope)
        {
#if SERP_DEVDIAG
            double elapsedMilliseconds = ((DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L) - scope.StartTimestamp) *
                1000.0 / Stopwatch.Frequency;
            if (scope.FillFallbackEvaluations == 0 &&
                elapsedMilliseconds < 50.0)
                return;
            bool selectedByFallback = false;
            int selectedRegion = 0;
            RouteProbeSummary selectedRoute = default;
            if (scope.SelectedMoatId > 0 && TryReadMoatRecordTile(
                    scope.TileManager, scope.SelectedMoatId,
                    out int selectedTileId, out _, out _))
            {
                selectedRegion = pathRegionGrid[selectedTileId];
                selectedByFallback = scope.FillApproaches.ContainsKey(scope.SelectedMoatId);
                if (scope.FillApproaches.TryGetValue(
                    scope.SelectedMoatId, out MoatWorkApproach selectedApproach))
                {
                    selectedRoute = selectedApproach.Summary;
                }
            }
            string kind = scope.RelationshipMode == 1 ? "dig" : "fill";
            string signature =
                $"{mapEpoch}:{kind}:{scope.StartTileId}:{scope.SelectedMoatId}:" +
                $"{scope.FillFallbackEvaluations}:{scope.FillFallbackAllowed}:" +
                $"{scope.CheckedApproachTiles}:{scope.FillFriendlyMoatEndpoints}:" +
                $"{scope.FillInvalidTileRejected}:{scope.FillHeightRejected}:" +
                $"{scope.FillEnemyOrInvalidMoatRejected}:{scope.FillGroundBlockedRejected}:" +
                $"{scope.FillOccupiedRejected}:{scope.FillOwnerRouteRejected}:" +
                $"{selectedByFallback}:{selectedRegion}:" +
                $"{scope.Route.FriendlyMoatTiles}:{scope.Route.EnemyMoatTiles}";
            if (lastMoatWorkSelectionByUnit.TryGetValue(scope.UnitId, out string previous) &&
                string.Equals(previous, signature, StringComparison.Ordinal))
            {
                return;
            }
            lastMoatWorkSelectionByUnit[scope.UnitId] = signature;
            LogDetailedInfo(
                $"Bugfixes and QoL stage=friendly-moat-movement-work-target-selection-fallback kind={kind} unit={scope.UnitId} " +
                $"player={scope.PlayerId} start=({scope.StartX},{scope.StartY})/" +
                $"region={scope.StartRegion} selectedMoat={scope.SelectedMoatId} " +
                $"selectedRegion={selectedRegion} selectedByFallback={selectedByFallback} " +
                $"fillApproaches={scope.FillFallbackAllowed}/{scope.FillFallbackEvaluations} " +
                $"checkedApproachTiles={scope.CheckedApproachTiles} " +
                $"friendlyMoatEndpoints={scope.FillFriendlyMoatEndpoints} " +
                $"rejectedInvalid={scope.FillInvalidTileRejected} " +
                $"rejectedHeight={scope.FillHeightRejected} " +
                $"rejectedEnemyOrInvalidMoat={scope.FillEnemyOrInvalidMoatRejected} " +
                $"rejectedGroundBlocked={scope.FillGroundBlockedRejected} " +
                $"rejectedOccupied={scope.FillOccupiedRejected} " +
                $"rejectedOwnerRoute={scope.FillOwnerRouteRejected} " +
                $"searchBuilds={scope.SearchBuilds} endpointQueries={scope.EndpointRoutes.Count} " +
                $"endpointCacheHits={scope.EndpointCacheHits} expanded={scope.SearchExpandedNodes} " +
                $"searchMs={scope.SearchMilliseconds:F3} elapsedMs={elapsedMilliseconds:F3} " +
                $"selectedRoute=[{selectedRoute.ToLogFields()}] " +
                $"observedRoutes=[{scope.Route.ToLogFields()}].");
#endif
        }

        [System.Diagnostics.Conditional("SERP_DEVDIAG")]
        internal void LogResolvedFillMoatApproach(PendingFillMoatApproach pending)
        {
#if SERP_DEVDIAG
            MoatWorkApproach approach = pending.Approach;
            string signature =
                $"{mapEpoch}:{pending.MoatId}:{approach.MoatTileId}:{approach.TileId}:" +
                $"{approach.X}:{approach.Y}:{approach.NativeOrder}:" +
                $"{approach.Summary.ObservedOwnerMask}";
            if (lastMoatWorkApproachByUnit.TryGetValue(
                    pending.UnitId, out string previous) &&
                string.Equals(previous, signature, StringComparison.Ordinal))
            {
                return;
            }
            lastMoatWorkApproachByUnit[pending.UnitId] = signature;
            LogDetailedInfo(
                $"Bugfixes and QoL stage=friendly-moat-movement-work-approach-tile kind=fill unit={pending.UnitId} " +
                $"player={pending.PlayerId} moat={pending.MoatId}/tile={approach.MoatTileId} " +
                $"approach=({approach.X},{approach.Y})/{approach.TileId} " +
                $"nativeOrder={approach.NativeOrder} handoff=owner-qualified-plan " +
                $"{approach.Summary.ToLogFields()}.");
#endif
        }

        [System.Diagnostics.Conditional("SERP_DEVDIAG")]
        internal void LogResolvedDigMoatTarget(PendingDigMoatTarget pending)
        {
#if SERP_DEVDIAG
            string signature =
                $"{mapEpoch}:dig:{pending.MoatId}:{pending.TileId}:" +
                $"{pending.X}:{pending.Y}:{pending.Summary.ObservedOwnerMask}";
            if (lastMoatWorkApproachByUnit.TryGetValue(
                    pending.UnitId, out string previous) &&
                string.Equals(previous, signature, StringComparison.Ordinal))
            {
                return;
            }
            lastMoatWorkApproachByUnit[pending.UnitId] = signature;
            LogDetailedInfo(
                $"Bugfixes and QoL stage=friendly-moat-movement-work-approach-tile kind=dig unit={pending.UnitId} " +
                $"player={pending.PlayerId} moat={pending.MoatId}/tile={pending.TileId} " +
                $"approach=({pending.X},{pending.Y})/{pending.TileId} " +
                $"selectedRegion={pending.TargetRegion} handoff=owner-qualified-plan " +
                $"{pending.Summary.ToLogFields()}.");
#endif
        }
    }
}
