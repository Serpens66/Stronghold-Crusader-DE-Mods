extern alias BugfixesRuntime;
using System;
using System.Collections.Generic;
using BugfixesRuntime::BugfixesAndQoL.UnitCommands;
using SHCDESE.API;
using SHCDESE.Interop;
using static BugfixesRuntime::BugfixesAndQoL.UnitCommands.UnitCommandPathRuntime;

namespace MoatMove
{
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        private readonly Dictionary<FastUnitIdentity, MoveCommandScope> deferredMoves =
            new Dictionary<FastUnitIdentity, MoveCommandScope>();
        internal override void InvalidateFastMoatData() => MarkFastTopologyAll();
        internal override void ResetMapState()
        {
            deferredMoves.Clear();
            ResetFastCommandMap();
        }
        internal override void ClearDeferredFastMoveScope() => deferredMoves.Clear();
        internal override void CaptureDeferredFastMoveScope(MoveCommandScope command)
        {
            RetainFastDistribution(command);
            if (command == null || !RequiredOnlyMode) return;
            runtime.EnsureMoveCommandGroupSummary(command);
            foreach (int id in command.ActiveUnitIdsAtDispatch)
                if (APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) && unit != null)
                {
                    var key = new FastUnitIdentity(id, unit->r_GlobalId);
                    deferredMoves.Remove(key);
                    if (command.MoatRelevant) deferredMoves[key] = command;
                }
        }
        internal override bool IsDeferredFastMoveAuthorized(PlanScope plan, GameUnit* unit)
        {
            if (plan == null || unit == null || activeMoveCommand != null) return false;
            return deferredMoves.TryGetValue(new FastUnitIdentity(plan.UnitId, unit->r_GlobalId), out MoveCommandScope command) &&
                command.TribeId == unit->r_TribeId && command.TargetX == plan.TargetX && command.TargetY == plan.TargetY;
        }
        // These are prefilters only. The original Fast implementation deliberately
        // leaves the actual decision to its exact reusable traversal fields.
        internal override bool HasFastFriendlyMoatBridge(int player, int start, int target) =>
            GamePlayerManagerAPI.Instance.IsPlayerIdValid(player) && IsValidTileId(start) && IsValidTileId(target);
        internal override bool HasFastFriendlyMoatBridgeForCells(int player, IList<int> starts, IList<int> targets) =>
            GamePlayerManagerAPI.Instance.IsPlayerIdValid(player) && starts.Count != 0 && targets.Count != 0;
        internal override void RecordFastSearch(WeightedMoatRouteSummary summary, long started, long before) { }
        internal override void RecordFastFieldSearch(MoatCandidateField field, long started) { }
        internal override bool HasPendingUnit(int id, uint global) =>
            fastCommands.HasPredecessor(new[] { new FastUnitIdentity(id, global) });
    }
}
