using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SHCDESE.API;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using RedBird.X64.Hooks.Transaction;

namespace BugfixesAndQoL
{
    internal sealed unsafe partial class FriendlyMoatMovementRuntime
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate long CommonGroupMoveDelegate(IntPtr manager, int tribe, short x, short y, short patrol, int newOrder);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate long NativeUnstackDelegate(IntPtr manager, int unit);
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate void FreePlaceDelegate(IntPtr manager, int ignoredUnit, int x, int y);
        private CommonGroupMoveDelegate originalCommonGroupMove;
        private NativeUnstackDelegate originalUnstack;
        private FreePlaceDelegate originalFreePlace;
        private RedBirdDetour<CommonGroupMoveDelegate> commonGroupMoveDetour;
        private RedBirdDetour<NativeUnstackDelegate> nativeUnstackDetour;
        private RedBirdDetour<FreePlaceDelegate> freePlaceDetour;
        private PlacementBatch placementBatch;
        private PlacementUnit unstackUnit;
        private byte* nativePlaceReservations;
        private int* nativeExecutingUnitId;
        private int idlePlacementTick = -1, idlePlacementEpoch = -1;
        private long idlePlacementRevision = -1;
        private readonly Dictionary<long, PlacementSearchState> idleSearches = new Dictionary<long, PlacementSearchState>();
        private readonly HashSet<int> idleReservedCells = new HashSet<int>();
        private long placementRevision;
        private long placementCalls, placementSlots, placementRollbacks, unstackCalls, unstackMoves;

        private sealed class PlacementBatch
        {
            public int Epoch, Tick, Tribe, X, Y;
            public long Revision;
            public MoveCommandScope Command;
            public readonly Dictionary<int, PlacementSearchState> Searches = new Dictionary<int, PlacementSearchState>();
            public readonly List<PlacementUnit> Pending = new List<PlacementUnit>();
            public readonly HashSet<int> Reserved = new HashSet<int>();
        }

        private sealed class PlacementUnit
        {
            public int Id, Player, X, Y, Epoch, Tick;
            public int AppliedX, AppliedY;
            public int Cell = -1;
            public uint Global;
            public ushort OldX, OldY;
            public bool Finished, Validated, FreeSearchConsumed, Released;
            public bool FieldsOwned = true;
            public MoatPlacementSearch Search;
            public UnitMoveHereEventArgs Args;
            public PlacementBatch Batch;
        }

        private sealed class PlacementSearchState
        {
            public CursorRegionGraph Graph;
            public long Revision;
            public MoatPlacementSearch Search;
            public int Anchor;
            public long ReverseExpanded;
            public CursorRegionGraph.ForwardSearch FromAnchor;
            public readonly Dictionary<int, bool> ToAnchor = new Dictionary<int, bool>();
            public bool Matches(CursorRegionGraph graph) => ReferenceEquals(Graph, graph) && Revision == graph.Revision;
            public bool CanReach(int source, int target)
            {
                long before = Graph.PlacementExpandedNodes;
                try
                {
                    if (source == target && source >= 0) return true;
                    if (!ToAnchor.TryGetValue(source, out bool reachesAnchor))
                        ToAnchor[source] = reachesAnchor = Graph.CanReach(source, Anchor, true);
                    // This is a proof, not an imposed waypoint. Units still search their
                    // own shortest paths. Directed alternatives bypassing the anchor remain valid.
                    if (reachesAnchor && FromAnchor.CanReach(target)) return true;
                    return Graph.CanReach(source, target, true);
                }
                finally { ReverseExpanded += Graph.PlacementExpandedNodes - before; }
            }
        }

        private PlacementSearchState CreatePlacementState(CursorRegionGraph graph, int player, int x, int y)
        {
            int anchor = CursorNode(player, GameTileManagerAPI.Instance.GetTileId(x, y));
            return new PlacementSearchState { Graph = graph, Revision = graph.Revision, Anchor = anchor,
                FromAnchor = graph.StartForwardSearch(anchor), Search = CreatePlacementSearch(player, x, y) };
        }

        private void InstallPlacementAdapters(
            HookTransaction transaction, ReadOnlySpan<byte> memory, ulong libraryBase)
        {
            InstallFormationSlotAdapter(transaction, memory, libraryBase);
            nativePlaceReservations = (byte*)(libraryBase + 0x51D75F0);
            nativeExecutingUnitId = (int*)(libraryBase + 0x9302C4);
            // Entry-only detours: Win64 argument registers/stack and nonvolatile
            // registers are preserved by the delegate ABI and original trampoline.
            // The verified prologues have no incoming flags or hidden live registers.
            commonGroupMoveDetour = InstallConnectivityObserver(transaction, memory, libraryBase, 0x118E00,
                "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 54 41 55 41 56 41 57 48 83 EC 30 48 63 F2 45 33 ED 8B D6 45 8B F1 45",
                (CommonGroupMoveDelegate)ObserveCommonGroupMove);
            nativeUnstackDetour = InstallConnectivityObserver(transaction, memory, libraryBase, 0x181890,
                "48 89 5C 24 08 48 89 74 24 10 57 48 83 EC 30 48 63 DA 48 8D 35 07 D5 ED 03 4C 69 CB 90 04 00 00 48 8B F9 4C 03 C9 49 63",
                (NativeUnstackDelegate)ObserveNativeUnstack);
            freePlaceDetour = InstallConnectivityObserver(transaction, memory, libraryBase, 0xF03C0,
                "89 54 24 10 53 56 57 48 83 EC 40 FF 81 A0 00 00 00 44 8B DA 33 D2 49 63 F0 49 63 F9 48 8B D9 48 89 51 48 89 51 44 81 FE",
                (FreePlaceDelegate)FindUnstackPlace);
        }

        private void CompletePlacementAdapterInstallation()
        {
            originalFormationSlot = formationSlotDetour.Original;
            originalAssassinGroundFormationSlot = assassinGroundFormationSlotDetour.Original;
            originalCommonGroupMove = commonGroupMoveDetour.Original;
            originalUnstack = nativeUnstackDetour.Original;
            originalFreePlace = freePlaceDetour.Original;
            Shared.DebugLogHelper.LogInfo(log,
                "Bugfixes and QoL friendly-moat-movement placement hooks installed: commonGroup=0x118E00 formationSlot=0xE1D30 assassinGroundSlot=0xE0970 unstack=0x181890 freePlace=0xF03C0; native Unit event retained, terrain unchanged.");
        }

        private long ObserveCommonGroupMove(IntPtr manager, int tribe, short x, short y, short patrol, int newOrder)
        {
            PlacementBatch previous = placementBatch;
            PlacementBatch batch = null;
            long started = Stopwatch.GetTimestamp();
            try
            {
                try
                {
                    // Clear even for irrelevant nested calls: never borrow a parent's slots.
                    placementBatch = null;
                    if (!disposed && manager == nativeTribeManager && activeMoveCommand != null &&
                        activeMoveCommand.TribeId == tribe && activeAttackCommand == null && activeMoatWorkSelection == null &&
                        (uint)x < MapWidth && (uint)y < MapWidth &&
                        (activeMoveCommand.UnitsOnMoatAtDispatch > 0 ||
                         IsCompletedMoatTile(GameTileManagerAPI.Instance.GetTileId(x, y))))
                    {
                        batch = new PlacementBatch { Epoch = mapEpoch, Tick = CaptureCurrentGameTick(),
                            Revision = placementRevision, Tribe = tribe, X = x, Y = y, Command = activeMoveCommand };
                        placementBatch = batch; placementCalls++;
                        activeMoveCommand.MoatRelevant = true;
                    }
                }
                catch (Exception ex) { placementBatch = null; TryLogDiagnosticFailure("common-placement-context", ex); }
                return originalCommonGroupMove(manager, tribe, x, y, patrol, newOrder);
            }
            finally
            {
                if (batch != null)
                {
                    foreach (PlacementUnit pending in batch.Pending) FinishPlacement(pending, false);
                    long nodes = 0, checks = 0, hits = 0, connectivity = 0;
                    foreach (var state in batch.Searches.Values)
                    { nodes += state.Search.ExpandedNodes; checks += state.Search.ReachabilityChecks; hits += state.Search.CacheHits;
                        connectivity += state.ReverseExpanded + state.FromAnchor.ExpandedNodes; }
                    try { LogCommandDiagnostic($"stage=placement tribe={tribe} candidatesExpanded={nodes} " +
                        $"connectivityExpanded={connectivity} regionChecks={checks} cacheHits={hits} allocated={batch.Pending.Count} " +
                        $"elapsedMs={(Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency:F3}"); }
                    catch { }
                }
                placementBatch = previous;
                // Nested native commands can change occupancy and terrain synchronously.
                if (previous != null) { previous.Searches.Clear(); previous.Revision = placementRevision; }
            }
        }

        private MoatPlacementSearch CreatePlacementSearch(int player, int x, int y)
        {
            return new MoatPlacementSearch(MapWidth, MapWidth, y * MapWidth + x,
                (from, to) => PlacementEdge(player, from, to));
        }

        private bool PlacementEdge(int player, int from, int to)
        {
            int x = from % MapWidth, y = from / MapWidth, nx = to % MapWidth, ny = to / MapWidth;
            int a = GameTileManagerAPI.Instance.GetTileId(x, y), b = GameTileManagerAPI.Instance.GetTileId(nx, ny);
            if (!IsValidTileId(a) || !IsValidTileId(b) || movementTargetAvailability[to] == 0 ||
                CursorNode(player, a) < 0 || CursorNode(player, b) < 0) return false;
            for (int d = 0; d < 8; d++)
                if (x + WeightedMoatRoutePlanner.DirectionX[d] == nx && y + WeightedMoatRoutePlanner.DirectionY[d] == ny)
                    return weightedMoatRoutePlanner.TryGetTraversalEdge(player, x, y, a, nx, ny, b, d,
                        false, false, MoatTraversalPolicy.FriendlyOnly, out _, out _);
            return false;
        }

        private bool PlacementAvailable(int player, int unitId, int cell)
        {
            if (movementTargetAvailability[cell] == 0) return false;
            int tile = GameTileManagerAPI.Instance.GetTileId(cell % MapWidth, cell / MapWidth);
            return IsValidTileId(tile) && (tileFlags[tile] & MovementBlockedLowTileFlagMask) == 0 &&
                (nativePlaceReservations != null && nativePlaceReservations[tile] == 0) &&
                CursorNode(player, tile) >= 0 && !IsOccupiedByOtherLivingUnit(tile, unitId);
        }

        private void PreparePlacement(UnitMoveFrame frame)
        {
            PlacementBatch batch = placementBatch;
            var args = frame.Args;
            if (batch == null || frame.Parent != null || args.SkipOriginalFunction ||
                batch.Epoch != mapEpoch || batch.Tick != CaptureCurrentGameTick() ||
                !ReferenceEquals(batch.Command, activeMoveCommand) || args.TileX != batch.X || args.TileY != batch.Y ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(args.UnitId, out GameUnit* unit) || unit == null ||
                unit->r_TribeId != batch.Tribe || unit->r_AliveState != AliveState.IsAlive || !CanDigMoat(unit)) return;
            if (unit->r_AttackMoveToTargetTileX != batch.X || unit->r_AttackMoveToTargetTileY != batch.Y) return;
            int player = unit->r_ControllableForPlayerId;
            if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(player)) return;
            GetNativeMovementStart(unit, out int sx, out int sy);
            if ((uint)sx >= MapWidth || (uint)sy >= MapWidth) return;
            int start = GameTileManagerAPI.Instance.GetTileId(sx, sy);
            CursorTopology topology = EnsureCursorTopology(player);
            int source = CursorNode(player, start);
            if (source < 0) return;
            if (batch.Revision != placementRevision) { batch.Searches.Clear(); batch.Revision = placementRevision; }
            if (!batch.Searches.TryGetValue(player, out PlacementSearchState state) || !state.Matches(topology.Graph))
                batch.Searches[player] = state = CreatePlacementState(topology.Graph, player, batch.X, batch.Y);
            MoatPlacementSearch search = state.Search;
            weightedMoatRoutePlanner.BeginReachabilityProbe();
            try
            {
                if (!search.TryReserve(args.UnitId, source,
                    cell => !batch.Reserved.Contains(cell) && PlacementAvailable(player, args.UnitId, cell),
                    cell => state.CanReach(source, CursorNode(player,
                        GameTileManagerAPI.Instance.GetTileId(cell % MapWidth, cell / MapWidth))), out int chosen)) return;
                var pending = new PlacementUnit { Id = args.UnitId, Global = unit->r_GlobalId, Player = player,
                    X = chosen % MapWidth, Y = chosen / MapWidth,
                    AppliedX = chosen % MapWidth, AppliedY = chosen / MapWidth, Cell = chosen, Epoch = mapEpoch,
                    Tick = CaptureCurrentGameTick(), Search = search, Args = args, Batch = batch,
                    OldX = unit->r_AttackMoveToTargetTileX, OldY = unit->r_AttackMoveToTargetTileY };
                batch.Pending.Add(pending); batch.Reserved.Add(chosen); frame.Placement = pending;
                args.TileX = pending.X; args.TileY = pending.Y;
                unit->r_AttackMoveToTargetTileX = (ushort)pending.X;
                unit->r_AttackMoveToTargetTileY = (ushort)pending.Y;
                placementSlots++;
            }
            finally { weightedMoatRoutePlanner.EndReachabilityProbe(); }
        }

        private void SynchronizePlacement(UnitMoveFrame frame)
        {
            PlacementUnit pending = frame?.Placement;
            if (pending == null || pending.Finished) return;
            // The original has already copied the Pre arguments into native registers.
            // Never rewrite that Args object here to request a different native goal.
            bool firstUse = !pending.Validated;
            pending.Validated = true;
            if (frame.Args.SkipOriginalFunction || frame.Args.UnitId != pending.Id ||
                !PlacementIdentity(pending, out GameUnit* unit))
            { FinishPlacement(pending, false); return; }
            if (unit->r_AttackMoveToTargetTileX != pending.AppliedX || unit->r_AttackMoveToTargetTileY != pending.AppliedY)
            { pending.FieldsOwned = false; ReleasePlacement(pending); }
            if (frame.Args.TileX != pending.X || frame.Args.TileY != pending.Y)
            {
                ReleasePlacement(pending);
                if (pending.FieldsOwned && (uint)frame.Args.TileX < MapWidth && (uint)frame.Args.TileY < MapWidth)
                {
                    pending.AppliedX = frame.Args.TileX; pending.AppliedY = frame.Args.TileY;
                    unit->r_AttackMoveToTargetTileX = (ushort)pending.AppliedX;
                    unit->r_AttackMoveToTargetTileY = (ushort)pending.AppliedY;
                }
            }
            // Late occupation does not change copied native arguments. Give up the slot;
            // Vanilla and the existing endpoint/path audit decide that actual movement.
            if (firstUse && !PlacementAvailable(pending.Player, pending.Id, pending.Cell)) ReleasePlacement(pending);
        }

        private void ReleasePlacement(PlacementUnit pending)
        {
            if (pending.Released) return;
            pending.Released = true; pending.Search.Release(pending.Id, pending.Cell);
            pending.Batch?.Reserved.Remove(pending.Cell); placementRollbacks++;
        }

        private bool PlacementIdentity(PlacementUnit pending, out GameUnit* unit)
        {
            unit = null;
            return pending.Epoch == mapEpoch && pending.Tick == CaptureCurrentGameTick() &&
                GameUnitManagerAPI.Instance.TryGetUnitById(pending.Id, out unit) && unit != null &&
                unit->r_GlobalId == pending.Global && unit->r_ControllableForPlayerId == pending.Player &&
                unit->r_AliveState == AliveState.IsAlive;
        }

        private void FinishPlacement(PlacementUnit pending, bool accepted)
        {
            if (pending == null || pending.Finished) return;
            pending.Finished = true;
            if (accepted && !pending.Args.SkipOriginalFunction && pending.Args.UnitId == pending.Id &&
                pending.Args.TileX == pending.AppliedX && pending.Args.TileY == pending.AppliedY && PlacementIdentity(pending, out _)) return;
            ReleasePlacement(pending);
            if (!pending.Validated && pending.Args.UnitId == pending.Id && pending.Args.TileX == pending.X && pending.Args.TileY == pending.Y)
            { pending.Args.TileX = pending.OldX; pending.Args.TileY = pending.OldY; }
            if (pending.FieldsOwned && PlacementIdentity(pending, out GameUnit* unit) &&
                unit->r_AttackMoveToTargetTileX == pending.AppliedX && unit->r_AttackMoveToTargetTileY == pending.AppliedY)
            { unit->r_AttackMoveToTargetTileX = pending.OldX; unit->r_AttackMoveToTargetTileY = pending.OldY; }
        }

        private long ObserveNativeUnstack(IntPtr manager, int id)
        {
            PlacementUnit previous = unstackUnit;
            unstackUnit = null;
            try
            {
                try
                {
                    if (idlePlacementTick != CaptureCurrentGameTick() || idlePlacementEpoch != mapEpoch ||
                        idlePlacementRevision != placementRevision)
                    {
                        idleSearches.Clear(); idleReservedCells.Clear();
                        idlePlacementTick = CaptureCurrentGameTick(); idlePlacementEpoch = mapEpoch;
                        idlePlacementRevision = placementRevision;
                    }
                    if (!disposed && manager == (IntPtr)nativeUnitManager &&
                        (nativeExecutingUnitId != null && *nativeExecutingUnitId == id) &&
                        GameUnitManagerAPI.Instance.TryGetUnitById(id, out GameUnit* unit) && unit != null &&
                        unit->r_AliveState == AliveState.IsAlive && CanDigMoat(unit) &&
                        GamePlayerManagerAPI.Instance.IsPlayerIdValid(unit->r_ControllableForPlayerId))
                    {
                        int tile = unchecked((int)unit->r_CurrentPositionTileId);
                        if (IsValidTileId(tile) && IsCompletedMoatTile(tile) &&
                            IsFriendlyCompletedMoatForWeightedShadow(unit->r_ControllableForPlayerId, tile) &&
                            IsOccupiedByOtherLivingUnit(tile, id))
                            unstackUnit = new PlacementUnit { Id = id, Global = unit->r_GlobalId,
                                Player = unit->r_ControllableForPlayerId, X = unit->r_CurrentTilePositionX,
                                Y = unit->r_CurrentTilePositionY, Epoch = mapEpoch, Tick = CaptureCurrentGameTick() };
                    }
                }
                catch (Exception ex) { unstackUnit = null; TryLogDiagnosticFailure("unstack-context", ex); }
                if (unstackUnit != null) unstackCalls++;
                long result = originalUnstack(manager, id);
                if (unstackUnit != null && result != 0) unstackMoves++;
                else if (unstackUnit?.Search != null)
                { unstackUnit.Search.Release(id, unstackUnit.Cell); idleReservedCells.Remove(unstackUnit.Cell); }
                return result;
            }
            finally { unstackUnit = previous; }
        }

        private void FindUnstackPlace(IntPtr manager, int ignoredUnit, int x, int y)
        {
            PlacementUnit scope = unstackUnit;
            if (scope == null || scope.FreeSearchConsumed || manager != nativePathManager || ignoredUnit != -1 ||
                x != scope.X || y != scope.Y || !PlacementIdentity(scope, out GameUnit* unit))
            { originalFreePlace(manager, ignoredUnit, x, y); return; }
            scope.FreeSearchConsumed = true;
            byte* output = (byte*)manager;
            *(int*)(output + 0x44) = 0; *(int*)(output + 0x48) = 0; *(int*)(output + 0x4C) = 0;
            try
            {
                CursorTopology topology = EnsureCursorTopology(scope.Player);
                int source = CursorNode(scope.Player, GameTileManagerAPI.Instance.GetTileId(x, y));
                if (source < 0) return;
                long key = ((long)scope.Player << 32) | (uint)(y * MapWidth + x);
                if (!idleSearches.TryGetValue(key, out var state) || !state.Matches(topology.Graph))
                    idleSearches[key] = state = CreatePlacementState(topology.Graph, scope.Player, x, y);
                var search = state.Search;
                scope.Search = search;
                weightedMoatRoutePlanner.BeginReachabilityProbe();
                try
                {
                    if (!search.TryReserve(scope.Id, source,
                        cell => cell != y * MapWidth + x && !idleReservedCells.Contains(cell) && PlacementAvailable(scope.Player, scope.Id, cell),
                        cell => state.CanReach(source, CursorNode(scope.Player,
                            GameTileManagerAPI.Instance.GetTileId(cell % MapWidth, cell / MapWidth))), out int chosen)) return;
                    scope.Cell = chosen;
                    if (!PlacementIdentity(scope, out unit) || !PlacementAvailable(scope.Player, scope.Id, chosen))
                    { search.Release(scope.Id, chosen); return; }
                    idleReservedCells.Add(chosen);
                    // Only the documented output triple. Native 181890 owns the move
                    // call, collision test and state transition; no synthetic orders.
                    *(int*)(output + 0x44) = chosen % MapWidth;
                    *(int*)(output + 0x48) = chosen / MapWidth;
                    *(int*)(output + 0x4C) = GameTileManagerAPI.Instance.GetTileId(chosen % MapWidth, chosen / MapWidth);
                }
                finally { weightedMoatRoutePlanner.EndReachabilityProbe(); }
            }
            catch (Exception ex) { TryLogDiagnosticFailure("unstack-placement", ex); }
        }
    }
}
