extern alias BugfixesRuntime;
using static BugfixesRuntime::BugfixesAndQoL.UnitCommands.UnitCommandPathRuntime;
using System;
using System.Collections.Generic;
using APIShared.Pathfinding;
using SHCDESE.API;
using SHCDESE.Interop;

namespace MoatMove
{
    // Burning tiles (type-9 fire records in state 2, FireRouteRules) as a bit set per map node.
    // Members are public on the internal class: the test harness turns 8-space "internal" members private.
    internal sealed class FireTileSet
    {
        public readonly ulong[] Bits = new ulong[(RouteTileRules.MapSize * RouteTileRules.MapSize + 63) / 64];
        public int Count { get; private set; }
        public bool Contains(int node) => (Bits[node >> 6] & (1UL << (node & 63))) != 0;
        public void Set(int node, bool on)
        {
            ulong bit = 1UL << (node & 63);
            bool was = (Bits[node >> 6] & bit) != 0;
            if (on == was) return;
            if (on) { Bits[node >> 6] |= bit; Count++; } else { Bits[node >> 6] &= ~bit; Count--; }
        }
        public void Clear() { Array.Clear(Bits, 0, Bits.Length); Count = 0; }
    }

    // Option EscapeFire (with AvoidFire): a soldier standing on a burning tile leaves it for the nearest
    // standable tile without fire, along the route that enters the fewest burning tiles (it must cross
    // fire to get out, so the replan watch leaves escaping units alone). Afterwards it returns to its old
    // position, or continues to its old move target, once that is fire-free and reachable without fire
    // within the fire policy's detour rule; after the policy's wait time it stays where it is.
    // Vanilla has no flee behaviour: fire damages only units on the burning tile itself (0x99420 ->
    // 0x9E030 -> 0x192400), except on tiles with flags 0xBA7001B1 (walls, raised, crenels, fords ...).
    // Runs on the simulation tick, in unit id order and with fixed neighbour order: every peer decides alike.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        private const int EscapeInterval = 8, EscapeRadius = 12, EscapeWindow = 2 * EscapeRadius + 1, EscapeRetryTicks = 40, EscapeProbeTicks = 40;
        // 0x99420 skips fire damage on tiles with these flags; 0x30 marks tiles a unit may not be ordered to.
        private const uint FireImmuneTileFlags = 0xBA7001B1u, EscapeBlockedTargetFlags = 0x30u;
        private readonly FireTileSet fireTiles = new FireTileSet();
        // Set when the fire rules are registered with EscapeFire; the policy supplies wait time and detour rule.
        private bool fireEscapeActive;
        private RouteReplanPolicy fireEscapePolicy;
        private sealed class FireEscapeEntry
        {
            internal uint Global; internal int Player, EscapeX, EscapeY, ReturnX, ReturnY, Until, NextProbe;
        }
        // Ordered by unit id: returns happen in the same order on every peer.
        private readonly SortedDictionary<int, FireEscapeEntry> fireEscapes = new SortedDictionary<int, FireEscapeEntry>();
        // Unit id -> first tick of the next attempt after a failed escape (combat AI may overwrite routes).
        private readonly Dictionary<int, int> fireEscapeRetry = new Dictionary<int, int>();
        private readonly List<int> fireEscapeDone = new List<int>();
        private readonly int[] escapeCost = new int[EscapeWindow * EscapeWindow], escapeGeneration = new int[EscapeWindow * EscapeWindow];
        private readonly long[] escapeHeap = new long[EscapeWindow * EscapeWindow * 8];
        private int escapeGenerationCounter;
        private long escapeChecks, escapeStarted, escapeReturned, escapeStayed, escapeEnded, escapeFailed, escapeTicks, escapeMaxTicks;

        // Soldiers only: r_SelectionAllowed (+0x2A0) is set by every military update and 0 for workers,
        // civilians and firemen. Siege equipment is military too, but Vanilla's group move (0x11B520)
        // filters it by type and crew; a direct 0x196280 would bypass that, so it is left alone as well.
        private static bool IsReplanSoldier(GameUnit* unit)
        {
            if (unit->r_SelectionAllowed == 0) return false;
            var type = unit->r_UnitChimp;
            return type != eChimps.CHIMP_TYPE_TREBUCHET && type != eChimps.CHIMP_TYPE_MANGONEL && type != eChimps.CHIMP_TYPE_BALLISTA &&
                type != eChimps.CHIMP_TYPE_CATAPULT && type != eChimps.CHIMP_TYPE_ARAB_BALLISTA && type != eChimps.CHIMP_TYPE_SIEGE_TOWER &&
                type != eChimps.CHIMP_TYPE_BATTERING_RAM && type != eChimps.CHIMP_TYPE_PORTABLE_SHIELD;
        }

        // Every EscapeInterval ticks after the fire scan of this tick.
        private void FireEscapeTick(int tick)
        {
            if (!fireEscapeActive || fireEscapePolicy == null || runtime.nativeUnitManager == null || (tick % EscapeInterval) != 0) return;
            if (fireTiles.Count == 0 && fireEscapes.Count == 0) return;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                if (fireEscapes.Count != 0) ProcessFireEscapes(tick);
                if (fireTiles.Count == 0) return;
                for (int id = 1; id <= MaximumUnitCount; id++)
                {
                    if (!APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) || unit == null || !APIShared.UnitAccess.IsReallyAlive(unit)) continue;
                    int x = unit->r_CurrentTilePositionX, y = unit->r_CurrentTilePositionY;
                    if ((uint)x >= MapWidth || (uint)y >= MapWidth || !fireTiles.Contains(y * MapWidth + x)) continue;
                    escapeChecks++;
                    if (!IsReplanSoldier(unit) || fireEscapes.ContainsKey(id)) continue;
                    int player = unit->r_ControllableForPlayerId;
                    if (player < 1 || player > 8) continue;
                    if (fireEscapeRetry.TryGetValue(id, out int retry)) { if (tick - retry < 0) continue; fireEscapeRetry.Remove(id); }
                    int tile = GameTileManagerAPI.Instance.GetTileId(x, y);
                    if (!IsValidTileId(tile) || (runtime.tileFlags[tile] & FireImmuneTileFlags) != 0) continue;
                    StartFireEscape(id, unit, player, x, y, tick);
                }
            }
            finally
            {
                long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - started;
                escapeTicks += elapsed; escapeMaxTicks = Math.Max(escapeMaxTicks, elapsed);
            }
        }

        private void StartFireEscape(int id, GameUnit* unit, int player, int x, int y, int tick)
        {
            if (!TryFindFireEscape(player, x, y, out int escapeX, out int escapeY))
            { escapeFailed++; fireEscapeRetry[id] = tick + EscapeRetryTicks; return; }
            // A moving unit continues to its move target later (a unit waiting in front of fire to the target
            // of its wait), a standing one returns to this tile.
            bool moving = unit->r_PathPlanStateBitFlags != 0 && unit->r_CurrentPathPlanIndex < unit->r_PathPlanLength;
            int returnX = moving ? unit->r_TargetTilePositionX2 : x, returnY = moving ? unit->r_TargetTilePositionY2 : y;
            if (replanWaits.TryGetValue(id, out ReplanWait wait) && wait.Global == unit->r_GlobalId) { returnX = wait.TargetX; returnY = wait.TargetY; }
            uint global = unit->r_GlobalId;
            // Escaping takes over from a wait in front of the fire.
            replanWaits.Remove(id); replanSettled.Remove(id);
            if (!ReplanMove(id, escapeX, escapeY))
            { StopReplanUnit(id); escapeFailed++; fireEscapeRetry[id] = tick + EscapeRetryTicks; return; }
            fireEscapes[id] = new FireEscapeEntry { Global = global, Player = player, EscapeX = escapeX, EscapeY = escapeY, ReturnX = returnX, ReturnY = returnY,
                Until = tick + fireEscapePolicy.MaximumWaitTicks, NextProbe = tick + EscapeProbeTicks };
            escapeStarted++;
        }

        // Nearest standable tile without fire within EscapeRadius: Dijkstra over the provider's live edges
        // (FriendlyOnly, as the core's masks) with the key (burning tiles entered, steps, window index).
        private bool TryFindFireEscape(int player, int startX, int startY, out int escapeX, out int escapeY)
        {
            escapeX = escapeY = -1;
            if (++escapeGenerationCounter == int.MaxValue) { escapeGenerationCounter = 1; Array.Clear(escapeGeneration, 0, escapeGeneration.Length); }
            int generation = escapeGenerationCounter, count = 0;
            int originX = startX - EscapeRadius, originY = startY - EscapeRadius;
            int startLocal = EscapeRadius * EscapeWindow + EscapeRadius;
            escapeGeneration[startLocal] = generation; escapeCost[startLocal] = 0;
            HeapPush(ref count, (long)startLocal);
            while (count != 0)
            {
                long entry = HeapPop(ref count);
                int local = (int)(entry & 0xFFFF), cost = (int)(entry >> 16);
                if (cost != escapeCost[local]) continue;
                int x = originX + local % EscapeWindow, y = originY + local / EscapeWindow, node = y * MapWidth + x;
                if (local != startLocal && !fireTiles.Contains(node) && IsEscapeTarget(x, y)) { escapeX = x; escapeY = y; return true; }
                for (int d = 0; d < 8; d++)
                {
                    int nx = x + FastRouteField.Dx[d], ny = y + FastRouteField.Dy[d];
                    if ((uint)nx >= MapWidth || (uint)ny >= MapWidth || nx < originX || ny < originY || nx >= originX + EscapeWindow || ny >= originY + EscapeWindow) continue;
                    int next = (ny - originY) * EscapeWindow + (nx - originX), nextNode = ny * MapWidth + nx;
                    // Key: burning tiles entered (bits 12+ of the cost), then steps.
                    int nextCost = cost + (fireTiles.Contains(nextNode) ? 4096 : 0) + 1;
                    if (escapeGeneration[next] == generation && escapeCost[next] <= nextCost) continue;
                    if (!LiveEdge(player, node, nextNode, d, false, null, out _, out _)) continue;
                    escapeGeneration[next] = generation; escapeCost[next] = nextCost;
                    HeapPush(ref count, ((long)nextCost << 16) | (uint)next);
                }
            }
            return false;
        }

        private bool IsEscapeTarget(int x, int y)
        {
            int tile = GameTileManagerAPI.Instance.GetTileId(x, y);
            return IsValidTileId(tile) && runtime.movementTargetAvailability[y * MapWidth + x] != 0 &&
                (runtime.tileFlags[tile] & EscapeBlockedTargetFlags) == 0;
        }

        private void HeapPush(ref int count, long value)
        {
            if (count == escapeHeap.Length) return;
            int i = count++;
            while (i > 0) { int parent = (i - 1) >> 1; if (escapeHeap[parent] <= value) break; escapeHeap[i] = escapeHeap[parent]; i = parent; }
            escapeHeap[i] = value;
        }

        private long HeapPop(ref int count)
        {
            long top = escapeHeap[0], last = escapeHeap[--count];
            int i = 0;
            while (true)
            {
                int child = 2 * i + 1;
                if (child >= count) break;
                if (child + 1 < count && escapeHeap[child + 1] < escapeHeap[child]) child++;
                if (last <= escapeHeap[child]) break;
                escapeHeap[i] = escapeHeap[child]; i = child;
            }
            if (count > 0) escapeHeap[i] = last;
            return top;
        }

        // Escaped units return once their old position or move target is fire-free and the core reaches
        // it without fire within the detour rule (reference: the straight distance). A new order, death or
        // the end of the wait time ends the case.
        private void ProcessFireEscapes(int tick)
        {
            fireEscapeDone.Clear();
            foreach (KeyValuePair<int, FireEscapeEntry> pair in fireEscapes)
            {
                int id = pair.Key; FireEscapeEntry entry = pair.Value;
                if (!APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) || unit == null || !APIShared.UnitAccess.IsReallyAlive(unit) ||
                    unit->r_GlobalId != entry.Global)
                { fireEscapeDone.Add(id); escapeEnded++; continue; }
                if (unit->r_TargetTilePositionX2 != entry.EscapeX || unit->r_TargetTilePositionY2 != entry.EscapeY)
                {
                    // A new order (player, combat AI): no new escape for a while, so the two do not take turns every 8 ticks.
                    fireEscapeDone.Add(id); escapeEnded++; fireEscapeRetry[id] = tick + EscapeRetryTicks;
                    continue;
                }
                // Still on the way out of the fire.
                if (unit->r_PathPlanStateBitFlags != 0 && unit->r_CurrentPathPlanIndex < unit->r_PathPlanLength) continue;
                int x = unit->r_CurrentTilePositionX, y = unit->r_CurrentTilePositionY;
                // The fire reached the escape tile: leave again, keeping the return point.
                if ((uint)x < MapWidth && (uint)y < MapWidth && fireTiles.Contains(y * MapWidth + x))
                {
                    int tile = GameTileManagerAPI.Instance.GetTileId(x, y);
                    if (IsValidTileId(tile) && (runtime.tileFlags[tile] & FireImmuneTileFlags) == 0 &&
                        TryFindFireEscape(entry.Player, x, y, out int againX, out int againY))
                    {
                        if (ReplanMove(id, againX, againY)) { entry.EscapeX = againX; entry.EscapeY = againY; escapeStarted++; }
                        else { StopReplanUnit(id); escapeFailed++; }
                    }
                    continue;
                }
                if (tick - entry.Until >= 0) { fireEscapeDone.Add(id); escapeStayed++; continue; }
                if (tick - entry.NextProbe < 0) continue;
                if (x == entry.ReturnX && y == entry.ReturnY) { fireEscapeDone.Add(id); escapeReturned++; continue; }
                if (fireTiles.Contains(entry.ReturnY * MapWidth + entry.ReturnX) || !AnchorReady(entry.Player)) { entry.NextProbe = tick + EscapeProbeTicks; continue; }
                if (tick != replanProbeTick) { replanProbeTick = tick; replanProbeLeft = ReplanProbeTickSettles; }
                if (replanProbeLeft <= 0) { entry.NextProbe = tick + 1; replanProbesDeferred++; continue; }
                entry.NextProbe = tick + EscapeProbeTicks;
                int distance = Math.Max(Math.Abs(entry.ReturnX - x), Math.Abs(entry.ReturnY - y));
                if (!CoreRouteClear(entry.Player, x, y, entry.ReturnX, entry.ReturnY, fireEscapePolicy, distance)) continue;
                if (ReplanMove(id, entry.ReturnX, entry.ReturnY)) escapeReturned++;
                else { StopReplanUnit(id); escapeFailed++; }
                fireEscapeDone.Add(id);
            }
            foreach (int id in fireEscapeDone) fireEscapes.Remove(id);
        }

        // The burning tiles belong to FireRouteRules (cleared by its Reset with its own state).
        private void ResetFireEscapes() { fireEscapes.Clear(); fireEscapeRetry.Clear(); }

        private string FormatFireEscapes() =>
            $"escape(burning={fireTiles.Count} active={fireEscapes.Count} checks={escapeChecks} started={escapeStarted} returned={escapeReturned} " +
            $"stayed={escapeStayed} ended={escapeEnded} failed={escapeFailed} ms={AnchorMs(escapeTicks)} maxMs={AnchorMs(escapeMaxTicks)})";

        private void ResetFireEscapeMetrics() =>
            escapeChecks = escapeStarted = escapeReturned = escapeStayed = escapeEnded = escapeFailed = escapeTicks = escapeMaxTicks = 0;
    }
}
