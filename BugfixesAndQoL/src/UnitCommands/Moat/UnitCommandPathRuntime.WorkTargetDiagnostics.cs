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
        internal void LogMoatWorkSelection(MoatWorkSelectionScope scope)
        {
            double elapsedMilliseconds = (Stopwatch.GetTimestamp() - scope.StartTimestamp) *
                1000.0 / Stopwatch.Frequency;
            if (scope.DigFallbackEvaluations == 0 && scope.FillFallbackEvaluations == 0 &&
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
                selectedByFallback = scope.RelationshipMode == 1
                    ? scope.RegionDecisions.TryGetValue(selectedRegion, out bool allowed) && allowed
                    : scope.FillApproaches.ContainsKey(scope.SelectedMoatId);
                if (scope.RelationshipMode == 1)
                    scope.RegionSummaries.TryGetValue(selectedRegion, out selectedRoute);
                else if (scope.FillApproaches.TryGetValue(
                    scope.SelectedMoatId, out MoatWorkApproach selectedApproach))
                {
                    selectedRoute = selectedApproach.Summary;
                }
            }
            string kind = scope.RelationshipMode == 1 ? "dig" : "fill";
            string signature =
                $"{mapEpoch}:{kind}:{scope.StartTileId}:{scope.SelectedMoatId}:" +
                $"{scope.DigFallbackEvaluations}:{scope.DigFallbackAllowed}:" +
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
                $"digRegions={scope.DigFallbackAllowed}/{scope.DigFallbackEvaluations} " +
                $"fillApproaches={scope.FillFallbackAllowed}/{scope.FillFallbackEvaluations} " +
                $"regionHelper={scope.LastRegionHelper ?? "none"} " +
                $"movementProfile={scope.LastMovementProfile} " +
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
        }

        internal void LogResolvedFillMoatApproach(PendingFillMoatApproach pending)
        {
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
        }

        internal void LogResolvedDigMoatTarget(PendingDigMoatTarget pending)
        {
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
        }
    }
}
