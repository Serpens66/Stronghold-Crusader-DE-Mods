extern alias BugfixesRuntime;
using static BugfixesRuntime::BugfixesAndQoL.UnitCommands.UnitCommandPathRuntime;
using System;
using System.Collections.Generic;
using APIShared.Pathfinding;
using SHCDESE.API;
using SHCDESE.Interop;

namespace MoatMove
{
    // Engine side of APIShared's RouteReplanPolicy (anchor mode with AnchorSingleMoves): a unit whose
    // stored route meets a rule tile of a set with a replan policy (fire that starts after its move)
    // is re-planned to its unchanged move target (196280, the primitive Vanilla's own unit states
    // use; order and state stay), or stopped until a clear route exists.
    // Cost with 10,000 units: each tick checks every 40th unit id (every 8th for 8 ticks after new
    // rule tiles) plus a short hot list of units with such a tile further ahead. A check decodes at
    // most ReplanLookahead steps of the stored route against a bit set; without rule tiles of a
    // replan set the watch returns at once. Everything derives from simulation state and unit ids,
    // so every peer decides alike.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        private const int ReplanSpread = 40, ReplanFastSpread = 8, ReplanLookahead = 40, ReplanWaitProbeTicks = 40, ReplanGroupTicks = 400;
        private readonly ulong[] replanTiles = new ulong[(MapWidth * MapWidth + 63) / 64];
        private int replanTileCount, replanFastUntil = int.MinValue, replanTick;
        private long replanVersion;
        private readonly List<int> replanHot = new List<int>();
        private readonly HashSet<int> replanHotSet = new HashSet<int>();
        private sealed class ReplanWait
        {
            internal uint Global; internal int Player, TargetX, TargetY, StopX, StopY, Remaining, Until, NextProbe;
            internal RouteReplanPolicy Policy;
        }
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
            replanGroupShared, replanUnreadable, replanCursorMismatches, replanTicks, replanMaxTicks;

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
            replanFastUntil = int.MinValue; replanRebuildCursor = -1;
        }

        // Once per simulation tick after AnchorTick (rule changes of this tick are already read).
        private void ReplanTick(int tick)
        {
            replanTick = tick;
            if (!settings.AnchorSingleMoves || !anchorAvailable || runtime.nativeUnitManager == null) return;
            AdvanceReplanRebuild();
            if (replanTileCount == 0 && replanHot.Count == 0 && replanWaits.Count == 0) return;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                if (replanWaits.Count != 0 && (tick & 7) == 0) ProcessReplanWaits(tick);
                if (replanTileCount == 0) { replanHot.Clear(); replanHotSet.Clear(); return; }
                for (int i = replanHot.Count - 1; i >= 0; i--)
                    if (!CheckReplanUnit(replanHot[i], tick)) { replanHotSet.Remove(replanHot[i]); replanHot.RemoveAt(i); }
                int spread = tick - replanFastUntil < 0 ? ReplanFastSpread : ReplanSpread;
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
                unit->r_PathPlanStateBitFlags == 0) return false;
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
            ValidateReplanCursor(unit, path, cursor, x, y);
            int end = Math.Min(length, cursor + lookahead);
            for (int i = cursor; i < end; i++)
            {
                int d = (path[i >> 1] >> ((i & 1) << 2)) & 15;
                if (d > 7) { replanUnreadable++; return -1; }
                x += FastNativeRouteField.Dx[d]; y += FastNativeRouteField.Dy[d];
                if ((uint)x >= MapWidth || (uint)y >= MapWidth) { replanUnreadable++; return -1; }
                int n = y * MapWidth + x;
                if ((replanTiles[n >> 6] & (1UL << (n & 63))) != 0) { node = n; return i - cursor + 1; }
            }
            return -1;
        }

        // Development check of the cursor contract: the stored start plus the first cursor steps is
        // the next tile. Release builds drop the call.
        [System.Diagnostics.Conditional("SERP_DEVDIAG")]
        private void ValidateReplanCursor(GameUnit* unit, byte* path, int cursor, int nextX, int nextY)
        {
            int x = unit->r_PreviousTilePositionX, y = unit->r_PreviousTilePositionY;
            for (int i = 0; i < cursor; i++)
            {
                int d = (path[i >> 1] >> ((i & 1) << 2)) & 15;
                if (d > 7) { replanCursorMismatches++; return; }
                x += FastNativeRouteField.Dx[d]; y += FastNativeRouteField.Dy[d];
            }
            if (x != nextX || y != nextY) replanCursorMismatches++;
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
            ReplanMove(id, targetX, targetY);
            if (!APIShared.UnitAccess.TryGetById(id, out unit, out _) || unit == null || unit->r_GlobalId != global ||
                unit->r_TargetTilePositionX2 != targetX || unit->r_TargetTilePositionY2 != targetY) { replanEnded++; return; }
            int length = unit->r_PathPlanStateBitFlags != 0 ? unit->r_PathPlanLength - unit->r_CurrentPathPlanIndex : -1;
            // Rule tiles further ahead on the new route are decided when the unit gets there.
            // Rule tiles further ahead on the new route are decided when the unit gets there.
            if (length > 0 && policy.AllowsDetour(remaining, length) &&
                FindReplanTile(unit, id, unit->r_CurrentPathPlanIndex, unit->r_PathPlanLength, ReplanLookahead, out _) < 0)
            { replanDetours++; return; }
            if (policy.MaximumWaitTicks == 0)
            {
                // No stop wanted: the unit keeps the new route and is not re-planned again for this target.
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
            ReplanMove(id, stopX, stopY);
            replanWaitsStarted++;
            replanWaits[id] = new ReplanWait { Global = global, Player = player, TargetX = targetX, TargetY = targetY, StopX = stopX, StopY = stopY,
                Remaining = remaining, Until = tick + policy.MaximumWaitTicks, NextProbe = tick + ReplanWaitProbeTicks, Policy = policy };
        }

        // Every 8 ticks: a new order ends a wait; a clear route (rule tile gone or an allowed detour)
        // or the end of the wait time resumes the move to the original target.
        private readonly List<int> replanWaitDone = new List<int>();
        private void ProcessReplanWaits(int tick)
        {
            replanWaitDone.Clear();
            foreach (KeyValuePair<int, ReplanWait> entry in replanWaits)
            {
                int id = entry.Key; ReplanWait wait = entry.Value;
                if (!APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) || unit == null || !APIShared.UnitAccess.IsReallyAlive(unit) ||
                    unit->r_GlobalId != wait.Global) { replanWaitDone.Add(id); replanEnded++; continue; }
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
                    wait.NextProbe = tick + ReplanWaitProbeTicks;
                    if (!ReplanRouteClear(wait)) continue;
                }
                ReplanMove(id, wait.TargetX, wait.TargetY);
                if (expired) replanExpired++; else replanResumed++;
                replanWaitDone.Add(id);
                // After the wait the unit walks its route, even through remaining rule tiles.
                replanSettled[id] = new ReplanSettled { Global = wait.Global, TargetX = wait.TargetX, TargetY = wait.TargetY };
            }
            foreach (int id in replanWaitDone) replanWaits.Remove(id);
        }

        // Whether the core's route from the stop tile avoids every replan tile within the policy's length.
        private bool ReplanRouteClear(ReplanWait wait)
        {
            int start = wait.StopY * MapWidth + wait.StopX, target = wait.TargetY * MapWidth + wait.TargetX;
            if (AnchorNative.mma_single_route(anchorContext, wait.Player, target, 1, 0, start, replanRoute, replanRoute.Length, out AnchorNative.QueryResult result) != 0 ||
                result.Status != AnchorNative.Ready || !wait.Policy.AllowsDetour(wait.Remaining, result.Length)) return false;
            int x = wait.StopX, y = wait.StopY;
            for (int i = 0; i < result.Length; i++)
            {
                x += FastNativeRouteField.Dx[replanRoute[i]]; y += FastNativeRouteField.Dy[replanRoute[i]];
                int n = y * MapWidth + x;
                if ((replanTiles[n >> 6] & (1UL << (n & 63))) != 0) return false;
            }
            return true;
        }

        // Extender move (196280) for a validated unit id only (workspace UnitAccess contract).
        private static void ReplanMove(int id, int x, int y)
        {
            if (APIShared.UnitAccess.TryGetById(id, out GameUnit* checkedUnit, out _)) GameUnitManagerAPI.Instance.MoveToTile(id, x, y, 0);
        }

        private string FormatReplanWatch() =>
            $"replan(tiles={replanTileCount} hot={replanHot.Count} waiting={replanWaits.Count} checks={replanChecks} triggers={replanTriggers} " +
            $"detours={replanDetours} waits={replanWaitsStarted} groupShared={replanGroupShared} resumed={replanResumed} expired={replanExpired} " +
            $"ended={replanEnded} kept={replanKept} unreadable={replanUnreadable} cursorMismatches={replanCursorMismatches} " +
            $"ms={AnchorMs(replanTicks)} maxMs={AnchorMs(replanMaxTicks)})";

        private void ResetReplanMetrics() =>
            replanChecks = replanTriggers = replanDetours = replanWaitsStarted = replanGroupShared = replanResumed = replanExpired = replanEnded =
                replanKept = replanUnreadable = replanCursorMismatches = replanTicks = replanMaxTicks = 0;
    }
}
