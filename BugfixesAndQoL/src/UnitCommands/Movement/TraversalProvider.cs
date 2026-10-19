using System;
using System.Collections.Generic;
using SHCDESE.Interop;
using static BugfixesAndQoL.UnitCommands.UnitCommandPathRuntime;
namespace BugfixesAndQoL.UnitCommands
{
    internal abstract unsafe class UnitCommandTraversalProvider
    {
        // Optional work policy. The command owner always tries the complete native selector first.
        internal virtual int FindDigWorkTarget(IntPtr tiles, int player, int unit, int vanillaResult) => vanillaResult;
        internal virtual bool TryAllowDigWorkRegion(MoatWorkSelectionScope scope, int region) => false;
        internal virtual bool TryGetDigWorkRoute(MoatWorkSelectionScope scope, int x, int y, out RouteProbeSummary summary)
        { summary = default; return false; }
        internal virtual PendingDigMoatTarget CreatePendingDigMoatTarget(MoatWorkSelectionScope scope, int moatId) => null;
        internal abstract void ResetMapState();
        internal abstract bool TryPrepareGroundReconstruction(PlanScope plan, GameUnit* unit);
        internal abstract bool HasPendingUnit(int id, uint global);
        internal abstract bool RequiredOnly { get; }
        internal abstract bool Enabled { get; }
        internal abstract IMoatSearchKernel CreateKernel(int width, int height, MoatSearchEdge edge);
        internal abstract void InvalidateFastMoatData();
        internal abstract void LogAndResetFastMoatMetrics();
        internal abstract bool HasFastFriendlyMoatBridge(int playerId, int startTileId, int targetTileId);
        internal abstract void ClearDeferredFastMoveScope();
        internal abstract void CaptureDeferredFastMoveScope(MoveCommandScope command);
        internal abstract bool IsDeferredFastMoveAuthorized(PlanScope plan, GameUnit* unit);
        internal abstract bool HasFastFriendlyMoatBridgeForCells(
            int playerId, IList<int> starts, IList<int> targets);
        internal abstract bool TryProbeFastCursorRoute(int playerId, int startTileId, int targetTileId,
            out RouteProbeSummary summary);
        internal abstract void RecordFastSearch(
            WeightedMoatRouteSummary summary, long started, long nodesBefore);
        internal abstract void RecordFastFieldSearch(MoatCandidateField field, long started);
        internal abstract bool TryFindFastRequiredRoute(PlanScope plan, bool reserved, bool evaluateMissing, out RouteProbeSummary summary);
        internal abstract bool TryBuildMovementReachabilityEncoded(int player, int sx, int sy, int tx, int ty, bool reserved,
            out WeightedMoatRouteSummary summary, out WeightedMoatEncodedRoute route);
        internal abstract int[] ResolveMovementCandidates(MoatCandidateField precise, IList<int> starts,
            IList<int> targets, MoatSearchEdge edge, MoatSearchEdge terminal, out int expanded);
        internal abstract bool TryChooseFastFormation(IntPtr manager, int x, int y, out int tile);
    }
}
