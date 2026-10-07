using System;
using System.Collections.Generic;
using SHCDESE.Interop;
using static APIShared.UnitCommands.UnitCommandPathRuntime;
namespace APIShared.UnitCommands
{
    internal abstract unsafe class UnitCommandTraversalProvider
    {
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
