using System;
using System.Collections.Generic;
using SHCDESE.Interop;
namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime
    {
        internal UnitCommandTraversalProvider Traversal => UnitCommandPathAPI.Traversal;
        internal bool TraversalEnabled => !nativeManualProbe && !disposed && CurrentOptions.TraversalEnabled && Traversal?.Enabled == true;
        internal bool ManualCommandsEnabled => !disposed && settings.EnableMod && settings.EnableImprovedManualUnitCommands;
        internal bool TryPrepareGroundReconstruction(PlanScope plan, GameUnit* unit) =>
            TraversalEnabled && RequiredOnlyMode && Traversal.TryPrepareGroundReconstruction(plan, unit);
        internal const int FastSearchNodeBudget = 16384;
        internal long fastVanillaBypasses, fastSearches;
        internal void InvalidateFastMoatData()
        {
            // Maintenance must continue while policy is disabled; no route is searched.
            Traversal?.InvalidateFastMoatData();
             return;
        }
        internal void LogAndResetFastMoatMetrics()
        {
            Traversal?.LogAndResetFastMoatMetrics();
             return;
        }
        internal bool HasFastFriendlyMoatBridge(int playerId, int startTileId, int targetTileId)
        {
            if (TraversalEnabled) return Traversal.HasFastFriendlyMoatBridge(playerId, startTileId, targetTileId);
             return false;
        }
        internal void ClearDeferredFastMoveScope()
        {
            Traversal?.ClearDeferredFastMoveScope();
             return;
        }
        internal void CaptureDeferredFastMoveScope(MoveCommandScope command)
        {
            if (TraversalEnabled) Traversal.CaptureDeferredFastMoveScope(command);
             return;
        }
        internal bool IsDeferredFastMoveAuthorized(PlanScope plan, GameUnit* unit)
        {
            if (TraversalEnabled) return Traversal.IsDeferredFastMoveAuthorized(plan, unit);
             return false;
        }
        internal bool HasFastFriendlyMoatBridgeForCells(
            int playerId, IList<int> starts, IList<int> targets)
        {
            if (TraversalEnabled) return Traversal.HasFastFriendlyMoatBridgeForCells(playerId, starts, targets);
             return false;
        }
        internal bool TryProbeFastCursorRoute(int playerId, int startTileId, int targetTileId,
            out RouteProbeSummary summary)
        {
            if (TraversalEnabled) return Traversal.TryProbeFastCursorRoute(playerId, startTileId, targetTileId, out summary);
            summary = default; return false;
        }
        internal void RecordFastSearch(
            WeightedMoatRouteSummary summary, long started, long nodesBefore)
        {
            if (TraversalEnabled) Traversal.RecordFastSearch(summary, started, nodesBefore);
             return;
        }
        internal void RecordFastFieldSearch(MoatCandidateField field, long started)
        {
            if (TraversalEnabled) Traversal.RecordFastFieldSearch(field, started);
             return;
        }
        internal bool TryFindFastRequiredRoute(PlanScope plan, bool reserved, bool evaluateMissing, out RouteProbeSummary summary)
        {
            if (TraversalEnabled) return Traversal.TryFindFastRequiredRoute(plan, reserved, evaluateMissing, out summary);
            summary = default; return false;
        }
        internal bool TryBuildMovementReachabilityEncoded(int player, int sx, int sy, int tx, int ty, bool reserved,
            out WeightedMoatRouteSummary summary, out WeightedMoatEncodedRoute route)
        {
            if (TraversalEnabled) return Traversal.TryBuildMovementReachabilityEncoded(player, sx, sy, tx, ty, reserved, out summary, out route);
            summary = default; route = default; return false;
        }
        internal int[] ResolveMovementCandidates(MoatCandidateField precise, IList<int> starts,
            IList<int> targets, MoatSearchEdge edge, MoatSearchEdge terminal, out int expanded)
        {
            if (TraversalEnabled) return Traversal.ResolveMovementCandidates(precise, starts, targets, edge, terminal, out expanded);
            expanded = 0; var result = precise.Resolve(starts, targets, edge, terminal); expanded = precise.Expanded; return result;
        }
        internal bool TryChooseFastFormation(IntPtr manager, int x, int y, out int tile)
        {
            if (TraversalEnabled) return Traversal.TryChooseFastFormation(manager, x, y, out tile);
            tile = default; return false;
        }
    }
}
