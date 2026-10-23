extern alias BugfixesRuntime;
using SHCDESE.API;
using APIShared;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using BugfixesRuntime::BugfixesAndQoL.UnitCommands;
using static BugfixesRuntime::BugfixesAndQoL.UnitCommands.UnitCommandPathRuntime;

namespace MoatMove
{
    // Moved from BugfixesAndQoL DirectMoatCommandScopes.cs: friendly-moat code runs only with MoatMove.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        internal override DirectFillCommandScope CreateDirectFillCommandScopeTraversal(TribeIssueOrderWithTargetEventArgs args)
        {
            if (args.AICommand != TribeAICommand.Unknown7 ||
                args.TribeId < 0 || args.TribeId >= MaximumTribeCount ||
                args.TargetValue1 < 0 || args.TargetValue1 >= MapWidth ||
                args.TargetValue2 < 0 || args.TargetValue2 >= MapWidth ||
                !runtime.TryCaptureOrderedActiveGroupUnits(
                    runtime.nativeTribeManager, args.TribeId, out int[] groupUnitIds))
            {
                return null;
            }

            int targetTileId = GameTileManagerAPI.Instance.GetTileId(
                args.TargetValue1, args.TargetValue2);
            if (!IsValidTileId(targetTileId) || !runtime.IsCompletedMoatTile(targetTileId))
                return null;

            int playerId = -1;
            var units = new List<DirectFillUnitStart>();
            foreach (int unitId in groupUnitIds)
            {
                if (!APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                    unit == null || !APIShared.UnitAccess.IsReallyAlive(unit) ||
                    unit->r_TribeId != args.TribeId || !runtime.CanDigMoat(unit))
                {
                    continue;
                }
                int unitPlayerId = unit->r_ControllableForPlayerId;
                int startX = unit->r_CurrentTilePositionX;
                int startY = unit->r_CurrentTilePositionY;
                if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(unitPlayerId) ||
                    startX < 0 || startX >= MapWidth || startY < 0 || startY >= MapWidth)
                {
                    continue;
                }
                if (playerId < 0)
                    playerId = unitPlayerId;
                if (unitPlayerId != playerId)
                    continue;
                int startTileId = GameTileManagerAPI.Instance.GetTileId(startX, startY);
                if (!IsValidTileId(startTileId))
                    continue;
                units.Add(new DirectFillUnitStart(
                    unitId, startX, startY, runtime.pathRegionGrid[startTileId]));
            }

            if (playerId < 0 || units.Count == 0 ||
                runtime.ResolveCompletedMoatRelationship(playerId, targetTileId) !=
                    CompletedMoatRelationship.Enemy)
            {
                return null;
            }

            return new DirectFillCommandScope(
                runtime.mapEpoch, args.TribeId, playerId, args.TargetValue1, args.TargetValue2,
                targetTileId, units.ToArray());
        }

        internal override bool TryAllowDirectFillRegionPairTraversal(
            DirectFillCommandScope scope, int candidateRegion, int startRegion)
        {
            string key = $"{candidateRegion}:{startRegion}";
            if (scope.RegionDecisions.TryGetValue(key, out bool cached))
                return cached;

            bool allowed = false;
            RouteProbeSummary observed = new RouteProbeSummary(scope.PlayerId);
            int selectedUnitId = 0;
            var probedStarts = new HashSet<int>();
            foreach (DirectFillUnitStart start in scope.Units)
            {
                if (start.Region != startRegion)
                    continue;
                int startKey = start.Region > 0
                    ? start.Region
                    : -GameTileManagerAPI.Instance.GetTileId(start.X, start.Y) - 1;
                if (!probedStarts.Add(startKey))
                    continue;
                EnsureReachabilityMap(scope.PlayerId, start.X, start.Y);
                RouteProbeSummary summary = GetCachedRouteSummaryForRegion(candidateRegion);
                observed.MergeObservations(summary);
                if (summary.ReachedWithMoat && !summary.ReachedWithoutMoat &&
                    summary.FriendlyMoatTiles > 0)
                {
                    allowed = true;
                    selectedUnitId = start.UnitId;
                    break;
                }
            }
            scope.RegionDecisions[key] = allowed;
            scope.RegionFallbackEvaluations++;
            if (allowed)
            {
                scope.RegionFallbackAllowed++;
                scope.SelectedUnitId = selectedUnitId;
                scope.Route.MergeObservations(observed);
            }
            return allowed;
        }

        internal override void LogDirectFillCommandTraversal(DirectFillCommandScope scope, TribeIssueOrderWithTargetEventArgs args)
        {
            try
            {
                string signature = $"{scope.MapEpoch}:{scope.TribeId}:{scope.TargetTileId}:" +
                    $"{scope.RegionFallbackAllowed}:{scope.RegionFallbackEvaluations}:" +
                    $"{scope.SelectedUnitId}:{args.ReturnValue}";
                if (string.Equals(signature, runtime.lastDirectFillDecision, StringComparison.Ordinal))
                    return;
                runtime.lastDirectFillDecision = signature;
                runtime.LogDetailedInfo(
                    $"Bugfixes and QoL stage=friendly-moat-movement-direct-fill-command tribe={scope.TribeId} " +
                    $"player={scope.PlayerId} target=({scope.TargetX},{scope.TargetY})/" +
                    $"{scope.TargetTileId} diggers={scope.Units.Length} " +
                    $"regionFallbacks={scope.RegionFallbackAllowed}/" +
                    $"{scope.RegionFallbackEvaluations} selectedUnit={scope.SelectedUnitId} " +
                    $"return={args.ReturnValue} {scope.Route.ToLogFields()}.");
            }
            catch
            {
                // The synchronous scope is already cleared; diagnostics must not affect dispatch.
            }
        }
    }
}
