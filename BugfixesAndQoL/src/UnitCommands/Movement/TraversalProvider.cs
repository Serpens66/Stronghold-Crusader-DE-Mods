using System;
using System.Collections.Generic;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using static BugfixesAndQoL.UnitCommands.UnitCommandPathRuntime;
namespace BugfixesAndQoL.UnitCommands
{
    internal abstract unsafe class UnitCommandTraversalProvider
    {
        // Friendly-moat movement (MoatMove). BugfixesAndQoL owns the native hooks and calls these
        // only while TraversalEnabled; without the provider every hook keeps the Vanilla result.
        internal abstract void AttackApproachFloodTraversal(
            IntPtr pathManager, int tribeId, int targetContext, uint targetX, uint targetY,
            int requestedResults, int sourceRegion, int movementClass);
        internal abstract void BuildingApproachBuilderTraversal(
            IntPtr pathManager, int tribeId, int buildingId, int requestedResults,
            int sourceRegion, int movementClass);
        internal abstract void BuildingCandidateConsumerTraversal(
            IntPtr tribeManager, int tribeId, int builderVariant);
        internal abstract int ResolveAttackScopeRegionPairTraversal(AttackApproachDiagnosticScope scope,
            int movementClass, int sourceRegion, int targetRegion, int routeKind, int vanillaResult);
        internal abstract bool ProbeCursorConnectivityTraversal(int player, int start, int target, out RouteProbeSummary summary);
        internal abstract bool TryQualifySelectedGroupCursorRouteTraversal(
            AttackCursorPairScope template, int[] ids, string token,
            ref AttackCursorPairScope boundScope, out CursorGroupRouteSummary group);
        internal abstract DirectFillCommandScope CreateDirectFillCommandScopeTraversal(TribeIssueOrderWithTargetEventArgs args);
        internal abstract bool TryAllowDirectFillRegionPairTraversal(
            DirectFillCommandScope scope, int candidateRegion, int startRegion);
        internal abstract void LogDirectFillCommandTraversal(DirectFillCommandScope scope, TribeIssueOrderWithTargetEventArgs args);
        internal abstract bool CanReachPlacementThroughFriendlyMoatTraversal(int unitId, int x, int y);
        internal abstract void PrepareFriendlyMoatPlacementTraversal(UnitMoveFrame frame, PlacementBatch batch,
            GameUnit* unit, int player, int sx, int sy);
        internal abstract PlacementUnit CreateFriendlyMoatUnstackUnitTraversal(IntPtr manager, int id);
        internal abstract void FindUnstackPlaceTraversal(PlacementUnit scope, IntPtr manager, int x, int y);
        internal abstract bool TryGetMoatWorkRouteTraversal(
            MoatWorkSelectionScope scope, int targetX, int targetY,
            out RouteProbeSummary summary);
        internal abstract int BuildPathWithCompletedMoatRouteVariantTraversal(
            IntPtr pathManager, int movementClass, int movementProfile,
            PlanScope resolvedPlan, bool planResolved, bool reconstruction);
        internal abstract int SelectOwnerSafeGroupMoatModeTraversal(
            IntPtr tribeManager, int tribeId, int vanillaResult, MoveCommandScope command);
        internal abstract bool TryFindRequiredFriendlyRouteTraversal(
            PlanScope plan,
            bool exactTarget,
            bool allowReservedTarget,
            out RouteProbeSummary summary,
            bool evaluateMissing);
        internal abstract int ResolveScopedCompletedMoatModeTraversal(
            IntPtr unitManager, int unitId, int vanillaResult, UnitMoveFrame moveFrame);
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
        // Terrain changed around one tile (native 3x3 tile/moat update): a provider may refresh
        // only the affected area. The default keeps the global invalidation.
        internal virtual void InvalidateFastMoatTile(int tile) => InvalidateFastMoatData();
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

        // Diagnostic readouts for the dedicated DEVDIAG build. Always declared, with neutral defaults, so a
        // diagnostic BugfixesAndQoL and an ordinary provider (or the reverse) stay binary compatible.
        internal virtual long CursorQueryCount => 0;
        internal virtual long CursorQueryTicks => 0;
        internal virtual long CursorTopologyTicks => 0;
        internal virtual string DescribeRejectedQueueWaypointTraversal(MoveCommandScope command) => "provider-diagnostics-unavailable";
    }
}
