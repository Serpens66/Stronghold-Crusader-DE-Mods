using System;
using System.Collections.Generic;
using Pathfinding.UnitCommands;
using SHCDESE.API;
using SHCDESE.Interop;
using static Pathfinding.UnitCommands.UnitCommandPathRuntime;

namespace Pathfinding.Engine
{
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        internal override void InvalidateFastMoatData() { ResetGroundFailureProofs(); MarkFastTopologyAll(); }
        // Native 3x3 tile or moat update: only that area (+-2 tiles, the edges it can change) is
        // re-read, so AI building activity no longer forces full anchor mask resweeps.
        internal override void ObserveFormationDestinations(int tribeId, int[] unitIds, uint[] globalIds, int[] targetX, int[] targetY) =>
            ObserveAnchorFormation(tribeId, unitIds, globalIds, targetX, targetY);
        internal override void InvalidateFastMoatTile(int tile)
        {
            ResetGroundFailureProofs();
            if (IsValidTileId(tile)) MarkFastTopologyTile(tile); else MarkFastTopologyAll();
        }
        // A native mask rebuild changes only direction bytes: the anchor byte watch marks every
        // changed tile within one round (16 ticks), so no global resweep is needed once it has
        // recorded all bytes (pf-iter-16: rebuild resweeps forced live searches of 150-190 ms).
        internal override void InvalidateFastMoatDirections()
        {
            ResetGroundFailureProofs();
            if (AnchorNodeTiles() != null) { anchorRebuildsWatched++; return; }
            MarkFastTopologyAll();
        }
        internal override void ResetMapState() => ResetFastCommandMap();
        internal override void RetainFastMoveDistribution(MoveCommandScope command) => RetainFastDistribution(command);
        // These are prefilters only. The original Fast implementation deliberately
        // leaves the actual decision to its exact reusable traversal fields.
        internal override bool HasFastFriendlyMoatBridge(int player, int start, int target) =>
            GamePlayerManagerAPI.Instance.IsPlayerIdValid(player) && IsValidTileId(start) && IsValidTileId(target);
        internal override bool HasFastFriendlyMoatBridgeForCells(int player, IList<int> starts, IList<int> targets) =>
            GamePlayerManagerAPI.Instance.IsPlayerIdValid(player) && starts.Count != 0 && targets.Count != 0;
        internal override void RecordFastSearch(WeightedMoatRouteSummary summary, long started, long before) { }
        internal override void RecordFastFieldSearch(MoatCandidateField field, long started) { }
        internal override bool HasPendingUnit(int id, uint global) =>
            fastCommands.HasPending(new FastUnitIdentity(id, global));
    }
}
