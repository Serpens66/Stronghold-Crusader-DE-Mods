extern alias BugfixesRuntime;
using static BugfixesRuntime::BugfixesAndQoL.UnitCommands.UnitCommandPathRuntime;
using System;
using System.Collections.Generic;
using APIShared.Pathfinding;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace MoatMove
{
    // Engine side of APIShared's RouteReplanPolicy (anchor mode with AnchorSingleMoves): a soldier whose
    // stored route meets a rule tile of a set with a replan policy (fire that starts after its move)
    // is re-planned to its unchanged move target (196280, the primitive Vanilla's own unit states
    // use; order and walking state stay), or stopped until a clear route exists; a stopped unit resumes
    // through its group's Vanilla command (ResumeReplanGroup). Only soldiers (IsReplanSoldier):
    // a stopped fireman in state 3 extinguishes his building from afar (0x159C30 -> 0xC3C80) and
    // workers treat a forced route end as reaching their job target (0x188340). MoatMove's own fire
    // and plague policies (IsAbortPolicy) end a wait without a clear route by aborting the move: the unit stays.
    // Cost with 10,000 units: each tick checks every 40th unit id (every 8th for 8 ticks after new
    // rule tiles) plus a short hot list of units with such a tile further ahead. A check decodes at
    // most ReplanLookahead steps of the stored route against a bit set; without rule tiles of a
    // replan set the watch returns at once. Everything derives from simulation state and unit ids,
    // so every peer decides alike.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        private const int ReplanSpread = 40, ReplanFastSpread = 8, ReplanLookahead = 40, ReplanWaitProbeTicks = 40, ReplanGroupTicks = 400;
        private readonly ulong[] replanTiles = new ulong[(MapWidth * MapWidth + 63) / 64];
        private int replanTileCount, replanTick;
        private int? replanFastUntil;
        private long replanVersion;
        private readonly List<int> replanHot = new List<int>();
        private readonly HashSet<int> replanHotSet = new HashSet<int>();
        private sealed class ReplanWait
        {
            internal uint Global; internal int Player, TargetX, TargetY, StopX, StopY, Remaining, Until, NextProbe;
            internal RouteReplanPolicy Policy;
            // The group command the unit followed (tribeOrders at the stop); Order.Sequence 0: none observed.
            internal int Tribe; internal TribeOrder Order;
        }
        // Last move or target order per tribe, observed on the simulation thread (Extender events of 11B520
        // and of the target order). A wait resumes only while its tribe got no newer order.
        // - Plain (an ordinary player move inside 196100, not queued, no patrol, or any AI move): the group
        //   command is given again with its own arguments.
        // - Other player moves (BugfixesAndQoL formations issue 11B520 directly): each waiting member walks
        //   alone to its own slot (replanSolos), so the formation keeps its places; a repeated group command
        //   would turn it into Vanilla slots.
        // - Queued moves (shift queue): not repeated; a repeat drops a multiplayer queue.
        // - Target orders (attack): not repeated (no direct API) and not split off: the unit keeps its order
        //   and re-plans to its move target (ResumeReplanMove), as before the group resume existed.
        private struct TribeOrder
        {
            internal uint Global; internal int Sequence, X, Y; internal short Patrol; internal bool NewOrder, Move, Plain, Queued;
            internal TribeMoveType MoveType;
        }
        private readonly Dictionary<int, TribeOrder> tribeOrders = new Dictionary<int, TribeOrder>();
        private int tribeOrderSequence;
        // Tribe of the ordinary (or queued) player move (196100) in progress, 0 outside of it.
        private int playerMoveTribe, playerQueuedTribe;
        // Members resumed alone to their slot in a tribe of their own; they rejoin their group on arrival.
        private sealed class ReplanSolo { internal uint Global; internal int MoveX, MoveY; internal SoldierTribe Tribe; }
        private readonly SortedDictionary<int, ReplanSolo> replanSolos = new SortedDictionary<int, ReplanSolo>();
        private readonly List<int> replanSoloDone = new List<int>();
        // Why a resume was held: newer order, group gone or member left, not the two-point order any
        // more, Vanilla refused (command or split), member not walking, queued order.
        private const int HeldNewerOrder = 0, HeldGroup = 1, HeldPoints = 2, HeldRefused = 3, HeldNotMoving = 4, HeldQueued = 5;
        private readonly long[] replanHeldReasons = new long[6];
        private long replanSoloResumed, replanSoloRejoined, replanMoveResumed;
        // Ordered by unit id: waits resume in the same order on every peer.
        private readonly SortedDictionary<int, ReplanWait> replanWaits = new SortedDictionary<int, ReplanWait>();
        // Units left alone until their move target changes (route kept, wait resumed or ended).
        private struct ReplanSettled { internal uint Global; internal int TargetX, TargetY; }
        private readonly Dictionary<int, ReplanSettled> replanSettled = new Dictionary<int, ReplanSettled>();
        // One group command (ReplanGroupKey) shares a wait decision of the same rule version.
        private struct ReplanGroupWait { internal long Version; internal int Tick; }
        private readonly Dictionary<long, ReplanGroupWait> replanGroupWaits = new Dictionary<long, ReplanGroupWait>();
        private readonly byte[] replanRoute = new byte[2000];
        private long replanChecks, replanTriggers, replanDetours, replanWaitsStarted, replanResumed, replanExpired, replanEnded, replanKept,
            replanGroupShared, replanUnreadable, replanCursorMatches, replanCursorMismatches, replanTicks, replanMaxTicks, replanProbesDeferred,
            replanAborted, replanMoveFailures, replanHeld;
        // MoatMove's fire and plague policies: an expired wait aborts the move instead of walking through the rule tiles.
        private RouteReplanPolicy fireAbortPolicy, plagueAbortPolicy;
        private bool IsAbortPolicy(RouteReplanPolicy policy) => policy != null && (ReferenceEquals(policy, fireAbortPolicy) || ReferenceEquals(policy, plagueAbortPolicy));
        // 196280's result for ReplanMove, from the Extender's OnUnitMoveHere Post (MoveToTile drops it).
        private int replanMoveUnit;
        private long replanMoveResult;
        // Settles all wait probes (ReplanRouteClear) of one simulation tick may use (~10 ms). Waits that start
        // together probe together: 225 units of moving hazard zones behind a map-wide barrier took 169 ms in one
        // tick (pf-cons-1). Over the budget a probe moves to the next tick; settles are deterministic.
        private const int ReplanProbeTickSettles = 250000;
        private int replanProbeTick = int.MinValue, replanProbeLeft;

        // A journaled rule tile: whether any set with a replan policy now holds it for some player.
        private void NoteReplanTile(int x, int y)
        {
            bool on = false;
            if (RouteTileRules.HasReplanRules)
                for (int p = 1; p <= 8 && !on; p++) on = RouteTileRules.GetReplanPolicy(x, y, p) != null;
            int node = y * MapWidth + x; ulong bit = 1UL << (node & 63);
            bool was = (replanTiles[node >> 6] & bit) != 0;
            if (on == was) return;
            if (on) { replanTiles[node >> 6] |= bit; replanTileCount++; replanFastUntil = replanTick + ReplanFastSpread; }
            else { replanTiles[node >> 6] &= ~bit; replanTileCount--; }
            replanVersion++;
        }

        // A new map or lost journal changes: the bit set is rebuilt from the rules in chunks (a full
        // pass over 640,000 tiles and eight players would stall the tick); journaled changes keep
        // applying meanwhile. Without replan rules it is complete at once.
        private const int ReplanRebuildNodesPerTick = 32768;
        private int replanRebuildCursor = -1;
        private void RebuildReplanTiles()
        {
            Array.Clear(replanTiles, 0, replanTiles.Length); replanTileCount = 0; replanVersion++;
            replanRebuildCursor = RouteTileRules.HasReplanRules ? 0 : -1;
        }

        private void AdvanceReplanRebuild()
        {
            if (replanRebuildCursor < 0) return;
            if (!RouteTileRules.HasReplanRules) { replanRebuildCursor = -1; return; }
            int end = Math.Min(MapWidth * MapWidth, replanRebuildCursor + ReplanRebuildNodesPerTick);
            for (int node = replanRebuildCursor; node < end; node++) NoteReplanTile(node % MapWidth, node / MapWidth);
            replanRebuildCursor = end >= MapWidth * MapWidth ? -1 : end;
        }

        private void ResetReplanWatch()
        {
            Array.Clear(replanTiles, 0, replanTiles.Length); replanTileCount = 0; replanVersion++;
            replanHot.Clear(); replanHotSet.Clear(); replanWaits.Clear(); replanSettled.Clear(); replanGroupWaits.Clear();
            replanFastUntil = null; replanRebuildCursor = -1; tribeOrders.Clear(); playerMoveTribe = playerQueuedTribe = 0;
            // The tribes belong to the old map: solo walkers are dropped without rejoining. A save keeps
            // them, its orders and its waits (MovementCaseSave), and its load restores them.
            replanSolos.Clear();
        }

        // Pre of a move (11B520) or target order of a tribe, on the simulation thread only. queued: the move
        // type carries BugfixesAndQoL's queue marker (decoded by the caller).
        private void ObserveTribeOrder(int tribeId, bool move, int x, int y, short patrol, bool newOrder, TribeMoveType moveType, bool queued = false)
        {
            if (tribeId <= 0 || tribeId >= MaximumTribeCount || !GameTribeManagerAPI.Instance.TryGetTribeById(tribeId, out GameTribe* tribe) || tribe == null) return;
            bool ai = GamePlayerManagerAPI.Instance.IsAIPlayer(tribe->r_PlayerIdOwner);
            tribeOrders[tribeId] = new TribeOrder { Global = tribe->r_GlobalId, Sequence = ++tribeOrderSequence, X = x, Y = y, Patrol = patrol,
                NewOrder = newOrder, MoveType = moveType, Move = move,
                Plain = move && (ai || (playerMoveTribe == tribeId && patrol == 0 && newOrder)),
                Queued = move && !ai && (queued || playerQueuedTribe == tribeId) };
        }

        // Once per simulation tick after AnchorTick (rule changes of this tick are already read).
        private void ReplanTick(int tick)
        {
            replanTick = tick;
            if (!settings.AnchorSingleMoves || !anchorAvailable || runtime.nativeUnitManager == null) return;
            AdvanceReplanRebuild();
            if (replanTileCount == 0 && replanHot.Count == 0 && replanWaits.Count == 0 && replanSolos.Count == 0) return;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                if (replanWaits.Count != 0 && (tick & 7) == 0) ProcessReplanWaits(tick);
                if (replanSolos.Count != 0 && (tick & 7) == 0) ProcessReplanSolos();
                if (replanTileCount == 0) { replanHot.Clear(); replanHotSet.Clear(); return; }
                for (int i = replanHot.Count - 1; i >= 0; i--)
                    if (!CheckReplanUnit(replanHot[i], tick)) { replanHotSet.Remove(replanHot[i]); replanHot.RemoveAt(i); }
                int spread = replanFastUntil.HasValue && unchecked(tick - replanFastUntil.Value) < 0 ? ReplanFastSpread : ReplanSpread;
                int slot = ((tick % spread) + spread) % spread;
                for (int id = slot == 0 ? spread : slot; id <= MaximumUnitCount; id += spread)
                    if (!replanHotSet.Contains(id) && CheckReplanUnit(id, tick) && replanHotSet.Add(id)) replanHot.Add(id);
            }
            finally
            {
                long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - started;
                replanTicks += elapsed; replanMaxTicks = Math.Max(replanMaxTicks, elapsed);
            }
        }

        // True: keep the unit on the hot list (a rule tile lies ahead beyond its trigger distance).
        private bool CheckReplanUnit(int id, int tick)
        {
            if (!APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) || unit == null || !APIShared.UnitAccess.IsReallyAlive(unit) ||
                unit->r_PathPlanStateBitFlags == 0 || !IsReplanSoldier(unit) || fireEscapes.ContainsKey(id)) return false;
            int cursor = unit->r_CurrentPathPlanIndex, length = unit->r_PathPlanLength;
            if (cursor >= length || length > 2000) return false;
            int player = unit->r_ControllableForPlayerId;
            if (player < 1 || player > 8 || !AnchorReady(player) || replanWaits.ContainsKey(id)) return false;
            int targetX = unit->r_TargetTilePositionX2, targetY = unit->r_TargetTilePositionY2;
            if (replanSettled.TryGetValue(id, out ReplanSettled settled))
            {
                if (settled.Global == unit->r_GlobalId && settled.TargetX == targetX && settled.TargetY == targetY) return false;
                replanSettled.Remove(id);
            }
            replanChecks++;
            int distance = FindReplanTile(unit, id, cursor, length, ReplanLookahead, out int node);
            if (distance < 0) return false;
            RouteReplanPolicy policy = RouteTileRules.GetReplanPolicy(node % MapWidth, node / MapWidth, player);
            if (policy == null) return false;
            if (distance > policy.TriggerDistance) return true;
            DecideReplan(id, unit, player, policy, length - cursor, targetX, targetY, tick);
            return false;
        }

        // Steps from the unit's next tile to the first replan tile within lookahead steps, or -1.
        // The stored route continues at the next tile: the cursor advances when a step begins.
        private int FindReplanTile(GameUnit* unit, int id, int cursor, int length, int lookahead, out int node)
        {
            node = -1;
            byte* path = runtime.nativeUnitManager + NativeUnitPathBufferOffset + id * NativeUnitPathBufferStride;
            int x = unit->r_NextTilePositionX2, y = unit->r_NextTilePositionY2;
            ValidateReplanCursor(unit, path, cursor, length, x, y);
            int end = Math.Min(length, cursor + lookahead);
            for (int i = cursor; i < end; i++)
            {
                int d = (path[i >> 1] >> ((i & 1) << 2)) & 15;
                if (d > 7) { replanUnreadable++; return -1; }
                x += FastRouteField.Dx[d]; y += FastRouteField.Dy[d];
                if ((uint)x >= MapWidth || (uint)y >= MapWidth) { replanUnreadable++; return -1; }
                int n = y * MapWidth + x;
                if ((replanTiles[n >> 6] & (1UL << (n & 63))) != 0) { node = n; return i - cursor + 1; }
            }
            return -1;
        }

        // Development check of the cursor contract: the remaining steps from the next tile end at the
        // move target (196280 stores the current tile as the route start, so the stored start is no
        // reference while a step is in progress; region intermediates also count as mismatches).
        // Release builds drop the call.
        [System.Diagnostics.Conditional("SERP_DEVDIAG")]
        private void ValidateReplanCursor(GameUnit* unit, byte* path, int cursor, int length, int nextX, int nextY)
        {
            int x = nextX, y = nextY;
            for (int i = cursor; i < length; i++)
            {
                int d = (path[i >> 1] >> ((i & 1) << 2)) & 15;
                if (d > 7) { replanCursorMismatches++; return; }
                x += FastRouteField.Dx[d]; y += FastRouteField.Dy[d];
            }
            if (x == unit->r_TargetTilePositionX2 && y == unit->r_TargetTilePositionY2) replanCursorMatches++;
            else replanCursorMismatches++;
        }

        // One group command: the same tribe and a move target in the same 16 x 16 cell (formation
        // members have nearby but different targets).
        private static long ReplanGroupKey(GameUnit* unit, int targetX, int targetY) =>
            unit->r_TribeId <= 0 ? 0 : ((long)unit->r_TribeId << 32) | (uint)((targetY >> 4) * (MapWidth >> 4) + (targetX >> 4));

        // The unit is at most TriggerDistance steps before a rule tile: re-plan to the same target and
        // keep the new route if the policy allows it and it avoids rule tiles, else stop and wait.
        private void DecideReplan(int id, GameUnit* unit, int player, RouteReplanPolicy policy, int remaining, int targetX, int targetY, int tick)
        {
            replanTriggers++;
            GetNativeMovementStart(unit, out int startX, out int startY);
            if ((uint)startX >= MapWidth || (uint)startY >= MapWidth || (uint)targetX >= MapWidth || (uint)targetY >= MapWidth) return;
            uint global = unit->r_GlobalId;
            long group = ReplanGroupKey(unit, targetX, targetY);
            if (group != 0 && policy.MaximumWaitTicks > 0 && replanGroupWaits.TryGetValue(group, out ReplanGroupWait shared) &&
                shared.Version == replanVersion && tick - shared.Tick < ReplanGroupTicks)
            {
                // A member of the same group command already found no allowed route.
                replanGroupShared++;
                StartReplanWait(id, unit, player, policy, remaining, targetX, targetY, startX, startY, tick);
                return;
            }
            bool moved = ReplanMove(id, targetX, targetY);
            if (!APIShared.UnitAccess.TryGetById(id, out unit, out _) || unit == null || unit->r_GlobalId != global ||
                unit->r_TargetTilePositionX2 != targetX || unit->r_TargetTilePositionY2 != targetY) { replanEnded++; return; }
            // A failed 196280 keeps state, cursor and length but zeroes the route buffer: never decode it.
            int length = moved && unit->r_PathPlanStateBitFlags != 0 ? unit->r_PathPlanLength - unit->r_CurrentPathPlanIndex : -1;
            // Rule tiles further ahead on the new route are decided when the unit gets there.
            if (length > 0 && policy.AllowsDetour(remaining, length) &&
                FindReplanTile(unit, id, unit->r_CurrentPathPlanIndex, unit->r_PathPlanLength, ReplanLookahead, out _) < 0)
            { replanDetours++; return; }
            if (policy.MaximumWaitTicks == 0)
            {
                // No stop wanted: the unit keeps the new route and is not re-planned again for this target.
                if (!moved) StopReplanUnit(id);
                replanKept++;
                replanSettled[id] = new ReplanSettled { Global = global, TargetX = targetX, TargetY = targetY };
                return;
            }
            if (group != 0) replanGroupWaits[group] = new ReplanGroupWait { Version = replanVersion, Tick = tick };
            StartReplanWait(id, unit, player, policy, remaining, targetX, targetY, startX, startY, tick);
        }

        // Stops the unit on its next tile (a move to that tile ends the stored route there).
        private void StartReplanWait(int id, GameUnit* unit, int player, RouteReplanPolicy policy, int remaining,
            int targetX, int targetY, int stopX, int stopY, int tick)
        {
            uint global = unit->r_GlobalId;
            var wait = new ReplanWait { Global = global, Player = player, TargetX = targetX, TargetY = targetY, StopX = stopX, StopY = stopY,
                Remaining = remaining, Until = tick + policy.MaximumWaitTicks, NextProbe = tick + ReplanWaitProbeTicks, Policy = policy, Tribe = unit->r_TribeId };
            // Move and target orders: a target order is kept, so the resume does not turn an attack into a move.
            if (TryGetLiveTribe(wait.Tribe, 0, player, out GameTribe* tribe) && tribeOrders.TryGetValue(wait.Tribe, out TribeOrder order) &&
                order.Global == tribe->r_GlobalId) wait.Order = order;
            // stopX/stopY is the movement start (GetNativeMovementStart): a stop, not a move to count.
            StopReplanUnitAt(id, stopX, stopY);
            replanWaitsStarted++;
            replanWaits[id] = wait;
        }

        // Every 8 ticks: a new order ends a wait; a clear route (rule tile gone or an allowed detour)
        // or the end of the wait time resumes the move to the original target. The move resumes as
        // Vanilla's group command again (ResumeReplanGroup): 0x11B520 restores the walking state and
        // animation of the stopped soldiers, which 0x196280 alone does not.
        private readonly List<int> replanWaitDone = new List<int>();
        // Tribes whose group command was given again in this pass: their other waits end with it.
        private readonly List<int> replanResumedTribes = new List<int>();
        private void ProcessReplanWaits(int tick)
        {
            replanWaitDone.Clear(); replanResumedTribes.Clear();
            foreach (KeyValuePair<int, ReplanWait> entry in replanWaits)
            {
                int id = entry.Key; ReplanWait wait = entry.Value;
                if (!APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) || unit == null || !APIShared.UnitAccess.IsReallyAlive(unit) ||
                    unit->r_GlobalId != wait.Global) { replanWaitDone.Add(id); replanEnded++; continue; }
                // A group command given again earlier in this pass moves this member as well.
                if (wait.Tribe != 0 && replanResumedTribes.Contains(wait.Tribe)) { replanWaitDone.Add(id); replanResumed++; SettleResumed(id, wait); continue; }
                if (unit->r_TargetTilePositionX2 != wait.StopX || unit->r_TargetTilePositionY2 != wait.StopY ||
                    (unit->r_PathPlanStateBitFlags != 0 && unit->r_CurrentPathPlanIndex < unit->r_PathPlanLength))
                {
                    // Another order (player, AI state, group command) took over.
                    replanWaitDone.Add(id); replanEnded++;
                    replanSettled[id] = new ReplanSettled { Global = wait.Global, TargetX = unit->r_TargetTilePositionX2, TargetY = unit->r_TargetTilePositionY2 };
                    continue;
                }
                bool expired = tick - wait.Until >= 0;
                if (!expired && tick - wait.NextProbe < 0) continue;
                if (!expired)
                {
                    if (tick != replanProbeTick) { replanProbeTick = tick; replanProbeLeft = ReplanProbeTickSettles; }
                    if (replanProbeLeft <= 0) { wait.NextProbe = tick + 1; replanProbesDeferred++; continue; }
                    wait.NextProbe = tick + ReplanWaitProbeTicks;
                    if (!ReplanRouteClear(wait)) continue;
                }
                replanWaitDone.Add(id);
                if (expired && IsAbortPolicy(wait.Policy))
                {
                    // Fire or plague policy: no clear route within the wait time aborts the move; the unit stays stopped.
                    replanAborted++;
                    replanSettled[id] = new ReplanSettled { Global = wait.Global, TargetX = wait.StopX, TargetY = wait.StopY };
                    continue;
                }
                if (!ResumeReplanGroup(id, wait, out int held))
                {
                    // No command (group gone or re-ordered, queued order, Vanilla refused): the unit stays stopped.
                    replanHeld++; replanHeldReasons[held]++;
                    LogReplanHeld(id, wait, held);
                    replanSettled[id] = new ReplanSettled { Global = wait.Global, TargetX = wait.StopX, TargetY = wait.StopY };
                    continue;
                }
                if (expired) replanExpired++; else replanResumed++;
                // After the wait (other policies) the unit walks its route, even through remaining rule tiles.
                SettleResumed(id, wait);
            }
            // Waits of a resumed tribe checked earlier in this pass end with its command as well.
            if (replanResumedTribes.Count != 0)
                foreach (KeyValuePair<int, ReplanWait> entry in replanWaits)
                    if (entry.Value.Tribe != 0 && replanResumedTribes.Contains(entry.Value.Tribe) && !replanWaitDone.Contains(entry.Key))
                    { replanWaitDone.Add(entry.Key); replanResumed++; SettleResumed(entry.Key, entry.Value); }
            foreach (int id in replanWaitDone) replanWaits.Remove(id);
        }

        [System.Diagnostics.Conditional("SERP_DEVDIAG")]
        private void LogReplanHeld(int id, ReplanWait wait, int held) =>
            LogFireCase("resume-held", id, $"why={held} waitTribe={wait.Tribe} order={wait.Order.Sequence}:{wait.Order.X},{wait.Order.Y} plain={wait.Order.Plain} " +
                $"queued={wait.Order.Queued} newOrder={wait.Order.NewOrder} moveType={wait.Order.MoveType} target={wait.TargetX},{wait.TargetY}");

        private void SettleResumed(int id, ReplanWait wait)
        {
            if (APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) && unit != null && unit->r_GlobalId == wait.Global)
                replanSettled[id] = new ReplanSettled { Global = wait.Global, TargetX = unit->r_TargetTilePositionX2, TargetY = unit->r_TargetTilePositionY2 };
        }

        // Resumes the move of a waiting unit (see TribeOrder): the tribe (global id, owner) must still hold
        // the unit and have no newer order. A plain order is given again with its own arguments (a player
        // move must also still be 11B520's two-point order to that tile: r_PatrolPoint2, count 2, index 1);
        // another player move sends the unit alone to its own slot (ResumeReplanSolo); a queued one is held.
        // A target order and a group without an observed order (an order not seen on the simulation thread,
        // or a load without saved movement cases) keep the unit in its group with its order: it re-plans to
        // its move target (ResumeReplanMove). A unit without a tribe walks alone to its move target.
        private bool ResumeReplanGroup(int id, ReplanWait wait, out int held)
        {
            held = HeldGroup;
            TribeOrder order = wait.Order;
            if (wait.Tribe <= 0) return ResumeReplanSolo(id, wait, out held);
            GameUnit* member;
            if (order.Sequence == 0)
            {
                if (!TryGetLiveTribe(wait.Tribe, 0, wait.Player, out _) || !TryGetSoldier(id, wait.Global, out member) || member->r_TribeId != wait.Tribe) return false;
                return ResumeReplanMove(id, wait, out held);
            }
            if (!tribeOrders.TryGetValue(wait.Tribe, out TribeOrder last) || last.Sequence != order.Sequence) { held = HeldNewerOrder; return false; }
            if (!TryGetLiveTribe(wait.Tribe, order.Global, wait.Player, out GameTribe* tribe) ||
                !APIShared.UnitAccess.TryGetById(id, out member, out _) || member == null || member->r_TribeId != wait.Tribe) return false;
            if (!order.Move) return ResumeReplanMove(id, wait, out held);
            if (order.Queued) { held = HeldQueued; return false; }
            if (!order.Plain) return ResumeReplanSolo(id, wait, out held);
            if (order.NewOrder && (tribe->r_CurrentPatrolPoints != 2 || tribe->r_PatrolCurrentTargetIndex != 1 ||
                tribe->r_PatrolPoint2TileX != order.X || tribe->r_PatrolPoint2TileY != order.Y)) { held = HeldPoints; return false; }
            // The repeat is the same plain player move: its own Pre keeps the tribe's order plain.
            int previous = playerMoveTribe; playerMoveTribe = wait.Tribe;
            bool issued;
            try { issued = GameTribeManagerAPI.Instance.IssueMoveHereCommand(wait.Tribe, order.X, order.Y, order.Patrol != 0, order.NewOrder ? 1 : 0, order.MoveType); }
            finally { playerMoveTribe = previous; }
            if (!issued) { held = HeldRefused; return false; }
            replanResumedTribes.Add(wait.Tribe);
            LogFireCase("resume-group", id, $"tribe={wait.Tribe} order={order.X},{order.Y} newOrder={order.NewOrder} moveType={order.MoveType}");
            held = HeldNotMoving;
            return APIShared.UnitAccess.TryGetById(id, out member, out _) && member != null && IsMoveCommanded(member);
        }

        // The unit keeps its tribe and order (target order, or none observed) and re-plans to its move target
        // with 196280, the primitive Vanilla's own unit states use. A failed re-plan zeroes the route buffer
        // but keeps the route state (DecideReplan): the unit gets a clean stop where it waited instead.
        private bool ResumeReplanMove(int id, ReplanWait wait, out int held)
        {
            held = HeldRefused;
            if (!ReplanMove(id, wait.TargetX, wait.TargetY)) { StopReplanUnitAt(id, wait.StopX, wait.StopY); return false; }
            replanMoveResumed++;
            LogFireCase("resume-move", id, $"tribe={wait.Tribe} order={wait.Order.Sequence} move={wait.Order.Move} target={wait.TargetX},{wait.TargetY}");
            return true;
        }

        // The unit alone to its own move target (its formation slot) in a tribe of its own; it rejoins its
        // group on arrival or when another order takes over (ProcessReplanSolos).
        private bool ResumeReplanSolo(int id, ReplanWait wait, out int held)
        {
            held = HeldRefused;
            if (!TryIsolateSoldier(id, wait.Global, wait.Player, out SoldierTribe own)) return false;
            if (!TryMoveSoldier(id, wait.Global, own, wait.TargetX, wait.TargetY, out int moveX, out int moveY))
            { RejoinSoldier(id, wait.Global, own); held = HeldNotMoving; return false; }
            if (own.Split) replanSolos[id] = new ReplanSolo { Global = wait.Global, MoveX = moveX, MoveY = moveY, Tribe = own };
            replanSoloResumed++;
            LogFireCase("solo-resume", id, $"move={moveX},{moveY} own={own.Own} original={own.Original} split={own.Split}");
            return true;
        }

        // Every 8 ticks with the waits: a solo walker rejoins its group once it stands (arrived or stopped)
        // or another order took over; a dead one only frees its own tribe.
        private void ProcessReplanSolos()
        {
            replanSoloDone.Clear();
            foreach (KeyValuePair<int, ReplanSolo> entry in replanSolos)
            {
                int id = entry.Key; ReplanSolo solo = entry.Value;
                // Stopped again in front of fire or escaping: the walker keeps its own tribe until that ends.
                if (replanWaits.ContainsKey(id) || fireEscapes.ContainsKey(id)) continue;
                // Still walking to its slot (Vanilla may shift a taken tile to a neighbour; FireEscape does the same).
                if (TryGetSoldier(id, solo.Global, out GameUnit* unit) && unit->r_TribeId == solo.Tribe.Own &&
                    Math.Abs(unit->r_TargetTilePositionX2 - solo.MoveX) <= 1 && Math.Abs(unit->r_TargetTilePositionY2 - solo.MoveY) <= 1 &&
                    IsWalking(unit)) continue;
                replanSoloDone.Add(id);
                LogFireCase("solo-end", id, $"move={solo.MoveX},{solo.MoveY} own={solo.Tribe.Own} original={solo.Tribe.Original}");
                RejoinSoldier(id, solo.Global, solo.Tribe);
                replanSoloRejoined++;
            }
            foreach (int id in replanSoloDone) replanSolos.Remove(id);
        }

        // Whether the core's route from the stop tile avoids every replan tile within the policy's length.
        // A probe over ReplanProbeSettles (or the tick's remaining probe budget) counts as not clear: the
        // wait ends at its expiry anyway.
        private const int ReplanProbeSettles = 65536;
        private bool ReplanRouteClear(ReplanWait wait)
        {
            int probe = CoreRouteProbe(wait.Player, wait.StopX, wait.StopY, wait.TargetX, wait.TargetY, wait.Policy, wait.Remaining);
            replanProbeOutcomes[probe]++;
            return probe == ProbeClear;
        }

        // Outcome of a probe: clear, no route (unreachable or over the settle budget), longer than the
        // policy's detour rule, or through a replan or burning tile. Counted for the performance line.
        private const int ProbeClear = 0, ProbeNoRoute = 1, ProbeTooLong = 2, ProbeRuleTile = 3;
        private readonly long[] replanProbeOutcomes = new long[4];

        // Whether the core's route from (startX,startY) avoids every replan and burning tile and fits the
        // policy against `remaining` steps. Consumes the tick's probe budget.
        private int CoreRouteProbe(int player, int startX, int startY, int targetX, int targetY, RouteReplanPolicy policy, int remaining)
        {
            int start = startY * MapWidth + startX, target = targetY * MapWidth + targetX;
            int code = AnchorNative.mma_single_route_limited(anchorContext, player, target, 1, 0, start, Math.Min(ReplanProbeSettles, Math.Max(1, replanProbeLeft)),
                replanRoute, replanRoute.Length, out AnchorNative.QueryResult result);
            replanProbeLeft -= result.Settles;
            if (code != 0 || result.Status != AnchorNative.Ready) return ProbeNoRoute;
            if (!policy.AllowsDetour(remaining, result.Length)) return ProbeTooLong;
            int x = startX, y = startY;
            for (int i = 0; i < result.Length; i++)
            {
                x += FastRouteField.Dx[replanRoute[i]]; y += FastRouteField.Dy[replanRoute[i]];
                int n = y * MapWidth + x;
                if ((replanTiles[n >> 6] & (1UL << (n & 63))) != 0 || IsHazardNode(n)) return ProbeRuleTile;
            }
            return ProbeClear;
        }

        // Extender move (196280) for a validated unit id only (workspace UnitAccess contract). True when
        // 196280 published a route; its result comes from the Extender's Post event (ObserveReplanMove),
        // without one (another subscriber skipped the original) from the unit's fresh route fields.
        private bool ReplanMove(int id, int x, int y)
        {
            GameUnit* unit;
            replanMoveUnit = id; replanMoveResult = -1;
            try
            {
                if (APIShared.UnitAccess.TryGetById(id, out unit, out _)) GameUnitManagerAPI.Instance.MoveToTile(id, x, y, 0);
                else return false;
            }
            finally { replanMoveUnit = 0; }
            if (unit == null) return false;
            if (replanMoveResult >= 0) { if (replanMoveResult == 0) replanMoveFailures++; return replanMoveResult != 0; }
            bool published = unit->r_PathPlanStateBitFlags != 0 && unit->r_CurrentPathPlanIndex == 0 && unit->r_PathPlanLength > 0;
            if (!published) replanMoveFailures++;
            return published;
        }

        // OnUnitMoveHere Post of the unit ReplanMove is moving (a nested move of another unit is ignored).
        private void ObserveReplanMove(UnitMoveHereEventArgs args)
        {
            if (args.Phase == EventHookPhase.Post && replanMoveUnit != 0 && args.UnitId == replanMoveUnit) replanMoveResult = args.ReturnValue;
        }

        // A clean stop on the unit's movement start (its next tile while a step is in progress): a move to
        // the start writes state 0 and an empty route.
        private void StopReplanUnit(int id)
        {
            if (!APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) || unit == null) return;
            GetNativeMovementStart(unit, out int x, out int y);
            StopReplanUnitAt(id, x, y);
        }

        private void StopReplanUnitAt(int id, int x, int y)
        {
            if ((uint)x >= MapWidth || (uint)y >= MapWidth) return;
            if (APIShared.UnitAccess.TryGetById(id, out _, out _)) GameUnitManagerAPI.Instance.MoveToTile(id, x, y, 0);
        }

        private string FormatReplanWatch() =>
            $"replan(tiles={replanTileCount} hot={replanHot.Count} waiting={replanWaits.Count} checks={replanChecks} triggers={replanTriggers} " +
            $"detours={replanDetours} waits={replanWaitsStarted} groupShared={replanGroupShared} resumed={replanResumed} expired={replanExpired} " +
            $"ended={replanEnded} kept={replanKept} aborted={replanAborted} held={replanHeld} heldWhy(newer,group,points,refused,notMoving,queued)={string.Join(",", replanHeldReasons)} solo(resumed={replanSoloResumed} rejoined={replanSoloRejoined} active={replanSolos.Count}) moveResumed={replanMoveResumed} probes(clear,noRoute,tooLong,fire)={replanProbeOutcomes[ProbeClear]},{replanProbeOutcomes[ProbeNoRoute]},{replanProbeOutcomes[ProbeTooLong]},{replanProbeOutcomes[ProbeRuleTile]} moveFailures={replanMoveFailures} unreadable={replanUnreadable} probesDeferred={replanProbesDeferred} cursor(matches={replanCursorMatches} mismatches={replanCursorMismatches}) " +
            $"ms={AnchorMs(replanTicks)} maxMs={AnchorMs(replanMaxTicks)})";

        private void ResetReplanMetrics()
        {
            replanChecks = replanTriggers = replanDetours = replanWaitsStarted = replanGroupShared = replanResumed = replanExpired = replanEnded =
                replanKept = replanAborted = replanHeld = replanMoveFailures = replanUnreadable = replanCursorMatches = replanCursorMismatches = replanTicks = replanMaxTicks = replanProbesDeferred = 0;
            Array.Clear(replanProbeOutcomes, 0, replanProbeOutcomes.Length); Array.Clear(replanHeldReasons, 0, replanHeldReasons.Length);
            replanSoloResumed = replanSoloRejoined = replanMoveResumed = 0;
        }
    }
}
