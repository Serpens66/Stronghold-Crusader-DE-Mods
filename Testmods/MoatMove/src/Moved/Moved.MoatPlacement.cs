extern alias BugfixesRuntime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SHCDESE.API;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using RedBird.X64.Hooks.Transaction;
using BugfixesRuntime::BugfixesAndQoL.UnitCommands;
using static BugfixesRuntime::BugfixesAndQoL.UnitCommands.UnitCommandPathRuntime;

namespace MoatMove
{
    // Moved from BugfixesAndQoL MoatPlacement.cs: friendly-moat code runs only with MoatMove.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        internal PlacementSearchState CreatePlacementState(CursorRegionGraph graph, int player, int x, int y)
        {
            int anchor = runtime.CursorNode(player, GameTileManagerAPI.Instance.GetTileId(x, y));
            return new PlacementSearchState { Graph = graph, Revision = graph.Revision, Anchor = anchor,
                FromAnchor = graph.StartForwardSearch(anchor), Search = CreatePlacementSearch(player, x, y) };
        }

        internal MoatPlacementSearch CreatePlacementSearch(int player, int x, int y)
        {
            return new MoatPlacementSearch(MapWidth, MapWidth, y * MapWidth + x,
                (from, to) => PlacementEdge(player, from, to));
        }

        internal bool PlacementEdge(int player, int from, int to)
        {
            int x = from % MapWidth, y = from / MapWidth, nx = to % MapWidth, ny = to / MapWidth;
            int a = GameTileManagerAPI.Instance.GetTileId(x, y), b = GameTileManagerAPI.Instance.GetTileId(nx, ny);
            if (!IsValidTileId(a) || !IsValidTileId(b) || runtime.movementTargetAvailability[to] == 0 ||
                runtime.CursorNode(player, a) < 0 || runtime.CursorNode(player, b) < 0) return false;
            for (int d = 0; d < 8; d++)
                if (x + WeightedMoatRoutePlanner.DirectionX[d] == nx && y + WeightedMoatRoutePlanner.DirectionY[d] == ny)
                    return runtime.weightedMoatRoutePlanner.TryGetTraversalEdge(player, x, y, a, nx, ny, b, d,
                        false, false, MoatTraversalPolicy.FriendlyOnly, out _, out _);
            return false;
        }

        internal override bool CanReachPlacementThroughFriendlyMoatTraversal(int unitId, int x, int y)
        {
            var probe = new PlanScope(unitId, x, y) { VanillaFailureProven = true, ExactRouteEndpoints = true };
            return runtime.TryFindRequiredFriendlyCompletedMoatRouteForPlan(probe, true, false, out _);
        }

        internal override void PrepareFriendlyMoatPlacementTraversal(UnitMoveFrame frame, PlacementBatch batch,
            GameUnit* unit, int player, int sx, int sy)
        {
            var args = frame.Args;
            int start = GameTileManagerAPI.Instance.GetTileId(sx, sy);
            CursorTopology topology = EnsureCursorTopology(player);
            int source = runtime.CursorNode(player, start);
            if (source < 0) return;
            if (batch.Revision != runtime.placementRevision) { batch.Searches.Clear(); batch.Revision = runtime.placementRevision; }
            if (!batch.Searches.TryGetValue(player, out PlacementSearchState state) || !state.Matches(topology.Graph))
                batch.Searches[player] = state = CreatePlacementState(topology.Graph, player, batch.X, batch.Y);
            MoatPlacementSearch search = state.Search;
            runtime.weightedMoatRoutePlanner.BeginReachabilityProbe();
            try
            {
                if (!search.TryReserve(args.UnitId, source,
                    cell => !batch.Reserved.Contains(cell) && runtime.PlacementAvailable(player, args.UnitId, cell),
                    cell => state.CanReach(source, runtime.CursorNode(player,
                        GameTileManagerAPI.Instance.GetTileId(cell % MapWidth, cell / MapWidth))), out int chosen)) return;
                runtime.ApplyPlacement(frame, batch, unit, player, search, chosen);
            }
            finally { runtime.weightedMoatRoutePlanner.EndReachabilityProbe(); }
        }

        // Friendly-moat unstacking (MoatMove): a unit standing on an occupied friendly moat tile.
        internal override PlacementUnit CreateFriendlyMoatUnstackUnitTraversal(IntPtr manager, int id)
        {
            if (manager == (IntPtr)runtime.nativeUnitManager &&
                (runtime.nativeExecutingUnitId != null && *runtime.nativeExecutingUnitId == id) &&
                APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) && unit != null &&
                APIShared.UnitAccess.IsReallyAlive(unit) && runtime.CanDigMoat(unit) &&
                GamePlayerManagerAPI.Instance.IsPlayerIdValid(unit->r_ControllableForPlayerId))
            {
                int tile = unchecked((int)unit->r_CurrentPositionTileId);
                if (IsValidTileId(tile) && runtime.IsCompletedMoatTile(tile) &&
                    runtime.IsFriendlyCompletedMoatForWeightedShadow(unit->r_ControllableForPlayerId, tile) &&
                    runtime.IsOccupiedByOtherLivingUnit(tile, id))
                    return new PlacementUnit { Id = id, Global = unit->r_GlobalId,
                        Player = unit->r_ControllableForPlayerId, X = unit->r_CurrentTilePositionX,
                        Y = unit->r_CurrentTilePositionY, Epoch = runtime.mapEpoch, Tick = CaptureCurrentGameTick() };
            }
            return null;
        }

        internal override void FindUnstackPlaceTraversal(PlacementUnit scope, IntPtr manager, int x, int y)
        {
            GameUnit* unit;
            scope.FreeSearchConsumed = true;
            byte* output = (byte*)manager;
            *(int*)(output + 0x44) = 0; *(int*)(output + 0x48) = 0; *(int*)(output + 0x4C) = 0;
            try
            {
                CursorTopology topology = EnsureCursorTopology(scope.Player);
                int source = runtime.CursorNode(scope.Player, GameTileManagerAPI.Instance.GetTileId(x, y));
                if (source < 0) return;
                long key = ((long)scope.Player << 32) | (uint)(y * MapWidth + x);
                if (!runtime.idleSearches.TryGetValue(key, out var state) || !state.Matches(topology.Graph))
                    runtime.idleSearches[key] = state = CreatePlacementState(topology.Graph, scope.Player, x, y);
                var search = state.Search;
                scope.Search = search;
                runtime.weightedMoatRoutePlanner.BeginReachabilityProbe();
                try
                {
                    if (!search.TryReserve(scope.Id, source,
                        cell => cell != y * MapWidth + x && !runtime.idleReservedCells.Contains(cell) && runtime.PlacementAvailable(scope.Player, scope.Id, cell),
                        cell => state.CanReach(source, runtime.CursorNode(scope.Player,
                            GameTileManagerAPI.Instance.GetTileId(cell % MapWidth, cell / MapWidth))), out int chosen)) return;
                    scope.Cell = chosen;
                    if (!runtime.PlacementIdentity(scope, out unit) || !runtime.PlacementAvailable(scope.Player, scope.Id, chosen))
                    { search.Release(scope.Id, chosen); return; }
                    runtime.idleReservedCells.Add(chosen);
                    // Only the documented output triple. Native 181890 owns the move
                    // call, collision test and state transition; no synthetic orders.
                    *(int*)(output + 0x44) = chosen % MapWidth;
                    *(int*)(output + 0x48) = chosen / MapWidth;
                    *(int*)(output + 0x4C) = GameTileManagerAPI.Instance.GetTileId(chosen % MapWidth, chosen / MapWidth);
                }
                finally { runtime.weightedMoatRoutePlanner.EndReachabilityProbe(); }
            }
            catch (Exception ex) { runtime.TryLogDiagnosticFailure("unstack-placement", ex); }
        }
    }
}
