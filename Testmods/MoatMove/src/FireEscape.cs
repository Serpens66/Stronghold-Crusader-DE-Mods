extern alias BugfixesRuntime;
using static BugfixesRuntime::BugfixesAndQoL.UnitCommands.UnitCommandPathRuntime;
using System;
using System.Collections.Generic;
using APIShared.Pathfinding;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace MoatMove
{
    // Hazard tiles as a bit set per map node: burning tiles (type-9 fire records in state 2,
    // FireRouteRules) or tiles where a plague cloud damages units (PlagueRouteRules).
    // Members are public on the internal class: the test harness turns 8-space "internal" members private.
    internal sealed class HazardTileSet
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

    // Options EscapeFire (with AvoidFire) and EscapePlague (with AvoidPlague): a soldier standing on a burning
    // tile or a tile a plague cloud damages leaves it; plague on walls, gates, towers and keeps only with
    // EscapePlagueRaised (the garrison would leave its post). The rules below are written for fire and apply
    // to plague alike (plagueTiles, plagueNear as front, PlagueWaitSeconds and PlagueReturnSeconds):
    // a soldier standing on a burning tile leaves it for the nearest
    // standable tile without fire, preferably one that is neither unburnt pitch nor next to fire (the
    // fire front: pitch fire spreads there next), along the route that enters the fewest burning tiles
    // (it must cross fire to get out, so the replan watch leaves escaping units alone). Afterwards it
    // returns: a standing soldier to its old position within FireReturnSeconds, a walking or waiting one
    // to its old move target within FireWaitSeconds, once that is fire-free and reachable without fire
    // within the fire policy's detour rule. Both windows start when the soldier reaches a fire-free tile;
    // after them it stays where it is.
    // Moves are Vanilla group commands (11B520 via SoldierTribeMoves): the soldier gets a tribe of its own
    // for the case and rejoins its group when the case ends, so the native command sets the walking state
    // and animation (0x196280 alone publishes a route only; the unit glides).
    // Vanilla has no flee behaviour: fire damages only units on the burning tile itself (0x99420 ->
    // 0x9E030 -> 0x192400), except on tiles with flags 0xBA7001B1 (walls, raised, crenels, fords ...).
    // Runs on the simulation tick, in unit id order and with fixed neighbour order: every peer decides alike.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        private const int EscapeInterval = 8, EscapeRadius = 12, EscapeWindow = 2 * EscapeRadius + 1, EscapeRetryTicks = 40, EscapeProbeTicks = 40;
        // 0x99420 skips fire damage on tiles with these flags; 0x30 marks tiles a unit may not be ordered to.
        private const uint FireImmuneTileFlags = 0xBA7001B1u, EscapeBlockedTargetFlags = 0x30u;
        // Escape key: hazard tiles entered (bits 13+), a target at the fire front or near a cloud (bit 12), then steps.
        private const int EscapeFireCost = 8192, EscapeFrontCost = 4096;
        private readonly HazardTileSet fireTiles = new HazardTileSet();
        // Plague: damaging tiles and their one-tile margin (clouds drift), filled by PlagueRouteRules.
        private readonly HazardTileSet plagueTiles = new HazardTileSet(), plagueNear = new HazardTileSet();
        private bool plagueEscapeActive, plagueEscapeRaised;
        private RouteReplanPolicy plagueEscapePolicy;
        private int plagueReturnTicks = 180 * 40;
        private enum EscapeHazard : byte { Fire, Plague }
        // Set with the plague rules: raised tiles for EscapePlagueRaised; cloud count and policy for the log.
        private PlagueDamageModel plagueModel;
        private int plagueCloudCount;
        private bool plaguePolicyActive;
        // Set when the fire rules are registered with EscapeFire; the policy supplies the detour rule and
        // the window to continue a move, fireReturnTicks the window to return to a standing position.
        private bool fireEscapeActive;
        private RouteReplanPolicy fireEscapePolicy;
        private int fireReturnTicks = 120 * 40;
        private enum FireReturnKind : byte { Position, MoveTarget }
        private sealed class FireEscapeEntry
        {
            internal uint Global; internal int Player, MoveX, MoveY, ReturnX, ReturnY, Until, NextProbe;
            internal FireReturnKind Kind;
            internal EscapeHazard Hazard;
            // Safe: reached a fire-free tile (the window runs from then). Returning: the return move is out.
            internal bool Safe, Returning;
            internal SoldierTribe Tribe;
        }
        // Ordered by unit id: returns happen in the same order on every peer.
        private readonly SortedDictionary<int, FireEscapeEntry> fireEscapes = new SortedDictionary<int, FireEscapeEntry>();
        // Unit id -> first tick of the next attempt after a failed escape (combat AI may overwrite routes).
        private readonly Dictionary<int, int> fireEscapeRetry = new Dictionary<int, int>();
        private readonly List<int> fireEscapeDone = new List<int>();
        private readonly int[] escapeCost = new int[EscapeWindow * EscapeWindow], escapeGeneration = new int[EscapeWindow * EscapeWindow];
        private readonly long[] escapeHeap = new long[EscapeWindow * EscapeWindow * 8];
        private int escapeGenerationCounter;
        private long escapeChecks, escapeStarted, escapeReescaped, escapeReturnIssued, escapeReturnArrived, escapeStayed, escapeEndedOrder, escapeEndedGone,
            escapeFailed, escapeReturnTargetBurning, escapeEndedTribe, escapeShifted, escapeStartedPlague, escapeTicks, escapeMaxTicks;
        // Return probes by outcome (ProbeClear .. ProbeRuleTile, RouteReplanWatch.CoreRouteProbe).
        private readonly long[] escapeReturnProbes = new long[4];

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

        // A unit walks while its route has steps left or its last step is still in progress: the cursor
        // already counts the step towards the next tile (FIRE_EFFECTS.md), so cursor == length alone leaves
        // the unit one tile before its target, possibly still on a burning tile.
        private static bool IsWalking(GameUnit* unit) =>
            unit->r_PathPlanStateBitFlags != 0 && (unit->r_CurrentPathPlanIndex < unit->r_PathPlanLength ||
                unit->r_NextTilePositionX2 != unit->r_CurrentTilePositionX || unit->r_NextTilePositionY2 != unit->r_CurrentTilePositionY);

        // Every EscapeInterval ticks after the fire and plague scans of this tick.
        private bool FireEscapeEnabled => fireEscapeActive && fireEscapePolicy != null;
        private bool PlagueEscapeEnabled => plagueEscapeActive && plagueEscapePolicy != null;
        private void FireEscapeTick(int tick)
        {
            if (!FireEscapeEnabled && !PlagueEscapeEnabled || runtime.nativeUnitManager == null || (tick % EscapeInterval) != 0) return;
            if (fireTiles.Count == 0 && plagueTiles.Count == 0 && fireEscapes.Count == 0) return;
            long started = System.Diagnostics.Stopwatch.GetTimestamp();
            try
            {
                if (fireEscapes.Count != 0) ProcessFireEscapes(tick);
                if (fireTiles.Count == 0 && plagueTiles.Count == 0) return;
                for (int id = 1; id <= MaximumUnitCount; id++)
                {
                    if (!APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) || unit == null || !APIShared.UnitAccess.IsReallyAlive(unit)) continue;
                    int x = unit->r_CurrentTilePositionX, y = unit->r_CurrentTilePositionY;
                    if ((uint)x >= MapWidth || (uint)y >= MapWidth) continue;
                    int node = y * MapWidth + x;
                    if (!fireTiles.Contains(node) && !plagueTiles.Contains(node)) continue;
                    escapeChecks++;
                    if (!IsReplanSoldier(unit) || fireEscapes.ContainsKey(id)) continue;
                    int player = unit->r_ControllableForPlayerId;
                    if (player < 1 || player > 8) continue;
                    if (fireEscapeRetry.TryGetValue(id, out int retry)) { if (tick - retry < 0) continue; fireEscapeRetry.Remove(id); }
                    if (!TryGetEscapeHazard(unit, x, y, out EscapeHazard hazard)) continue;
                    StartFireEscape(id, unit, player, x, y, hazard, tick);
                }
            }
            finally
            {
                long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - started;
                escapeTicks += elapsed; escapeMaxTicks = Math.Max(escapeMaxTicks, elapsed);
            }
        }

        // A burning tile that damages units standing on it (0x99420's immunity mask).
        private bool IsFireDamageTile(int x, int y)
        {
            if ((uint)x >= MapWidth || (uint)y >= MapWidth || !fireTiles.Contains(y * MapWidth + x)) return false;
            int tile = GameTileManagerAPI.Instance.GetTileId(x, y);
            return IsValidTileId(tile) && (runtime.tileFlags[tile] & FireImmuneTileFlags) == 0;
        }

        // A plague tile the unit would take damage on and should leave: not a plague-immune unit type, and on
        // walls, gates, towers and keeps only with EscapePlagueRaised.
        private bool IsPlagueEscapeTile(GameUnit* unit, int x, int y)
        {
            if ((uint)x >= MapWidth || (uint)y >= MapWidth || !plagueTiles.Contains(y * MapWidth + x) || IsPlagueImmune(unit)) return false;
            return plagueEscapeRaised || !IsPlagueRaisedTile(x, y);
        }

        // 0x32BC40: among the soldiers only the lord and the healer are immune to plague.
        private static bool IsPlagueImmune(GameUnit* unit) =>
            unit->r_UnitChimp == eChimps.CHIMP_TYPE_LORD || unit->r_UnitChimp == eChimps.CHIMP_TYPE_HEALER;

        // Walls, stairs, gates, towers, keeps and other raised tiles (PlagueDamageModel's lanes).
        private bool IsPlagueRaisedTile(int x, int y)
        {
            int tile = GameTileManagerAPI.Instance.GetTileId(x, y);
            if (!IsValidTileId(tile)) return false;
            return plagueModel != null ? plagueModel.IsRaised(tile) : (runtime.tileFlags[tile] & PlagueDamageModel.VanillaImmuneFlags) != 0;
        }

        // The hazard a soldier on (x, y) leaves: fire first (it damages every tick).
        private bool TryGetEscapeHazard(GameUnit* unit, int x, int y, out EscapeHazard hazard)
        {
            hazard = EscapeHazard.Fire;
            if (FireEscapeEnabled && IsFireDamageTile(x, y)) return true;
            hazard = EscapeHazard.Plague;
            return PlagueEscapeEnabled && IsPlagueEscapeTile(unit, x, y);
        }

        private bool IsHazardNode(int node) => fireTiles.Contains(node) || plagueTiles.Contains(node);

        private RouteReplanPolicy EscapePolicy(EscapeHazard hazard) => hazard == EscapeHazard.Plague ? plagueEscapePolicy : fireEscapePolicy;

        private void StartFireEscape(int id, GameUnit* unit, int player, int x, int y, EscapeHazard hazard, int tick)
        {
            if (!TryFindFireEscape(id, player, x, y, out int escapeX, out int escapeY))
            { escapeFailed++; fireEscapeRetry[id] = tick + EscapeRetryTicks; return; }
            // A moving unit continues to its move target later (a unit waiting in front of fire to the target
            // of its wait), a standing one returns to this tile.
            bool moving = IsWalking(unit);
            var kind = moving ? FireReturnKind.MoveTarget : FireReturnKind.Position;
            int returnX = moving ? unit->r_TargetTilePositionX2 : x, returnY = moving ? unit->r_TargetTilePositionY2 : y;
            uint global = unit->r_GlobalId;
            if (replanWaits.TryGetValue(id, out ReplanWait wait) && wait.Global == global)
            { kind = FireReturnKind.MoveTarget; returnX = wait.TargetX; returnY = wait.TargetY; }
            if (!TryIsolateSoldier(id, global, player, out SoldierTribe tribe))
            { escapeFailed++; fireEscapeRetry[id] = tick + EscapeRetryTicks; return; }
            // Escaping takes over from a wait in front of the fire.
            replanWaits.Remove(id); replanSettled.Remove(id);
            if (!TryMoveSoldier(id, global, tribe, escapeX, escapeY, out int moveX, out int moveY))
            {
                RejoinSoldier(id, global, tribe);
                escapeFailed++; fireEscapeRetry[id] = tick + EscapeRetryTicks; return;
            }
            fireEscapes[id] = new FireEscapeEntry { Global = global, Player = player, MoveX = moveX, MoveY = moveY, ReturnX = returnX, ReturnY = returnY,
                Kind = kind, Hazard = hazard, Tribe = tribe };
            escapeStarted++;
            if (hazard == EscapeHazard.Plague) escapeStartedPlague++;
            LogEscape("start", id, fireEscapes[id]);
        }

        // Nearest standable tile without fire within EscapeRadius: Dijkstra over the provider's live edges
        // (FriendlyOnly, as the core's masks) with the key (burning tiles entered, target at the fire front,
        // steps, window index). The fire count orders the heap, so the first target off the fire front is
        // chosen unless a fire-front target needs fewer burning tiles.
        private bool TryFindFireEscape(int id, int player, int startX, int startY, out int escapeX, out int escapeY)
        {
            escapeX = escapeY = -1;
            if (++escapeGenerationCounter == int.MaxValue) { escapeGenerationCounter = 1; Array.Clear(escapeGeneration, 0, escapeGeneration.Length); }
            int generation = escapeGenerationCounter, count = 0, frontX = -1, frontY = -1, frontFire = int.MaxValue;
            int originX = startX - EscapeRadius, originY = startY - EscapeRadius;
            int startLocal = EscapeRadius * EscapeWindow + EscapeRadius;
            escapeGeneration[startLocal] = generation; escapeCost[startLocal] = 0;
            HeapPush(ref count, (long)startLocal);
            while (count != 0)
            {
                long entry = HeapPop(ref count);
                int local = (int)(entry & 0xFFFF), cost = (int)(entry >> 16);
                if (cost != escapeCost[local]) continue;
                // Every fire-front target with as few burning tiles is found: no better one can follow.
                if (cost / EscapeFireCost > frontFire) break;
                int x = originX + local % EscapeWindow, y = originY + local / EscapeWindow, node = y * MapWidth + x;
                if (local != startLocal && !IsHazardNode(node) && IsEscapeTarget(x, y) && !IsEscapeTaken(id, x, y))
                {
                    if (!IsFireFront(x, y)) { escapeX = x; escapeY = y; return true; }
                    if (frontX < 0) { frontX = x; frontY = y; frontFire = cost / EscapeFireCost; }
                }
                for (int d = 0; d < 8; d++)
                {
                    int nx = x + FastRouteField.Dx[d], ny = y + FastRouteField.Dy[d];
                    if ((uint)nx >= MapWidth || (uint)ny >= MapWidth || nx < originX || ny < originY || nx >= originX + EscapeWindow || ny >= originY + EscapeWindow) continue;
                    int next = (ny - originY) * EscapeWindow + (nx - originX), nextNode = ny * MapWidth + nx;
                    int nextCost = (cost / EscapeFireCost + (IsHazardNode(nextNode) ? 1 : 0)) * EscapeFireCost + cost % EscapeFireCost + 1;
                    if (escapeGeneration[next] == generation && escapeCost[next] <= nextCost) continue;
                    if (!LiveEdge(player, node, nextNode, d, false, null, out _, out _)) continue;
                    escapeGeneration[next] = generation; escapeCost[next] = nextCost;
                    HeapPush(ref count, ((long)nextCost << 16) | (uint)next);
                }
            }
            if (frontX < 0) return false;
            escapeX = frontX; escapeY = frontY;
            return true;
        }

        // Another soldier already escapes to this tile: Vanilla moves the later arrivals to a neighbouring
        // tile, which would read as a new order. Runs on escape events only (the enumeration allocates).
        private bool IsEscapeTaken(int id, int x, int y)
        {
            foreach (KeyValuePair<int, FireEscapeEntry> other in fireEscapes)
                if (other.Key != id && !other.Value.Returning && other.Value.MoveX == x && other.Value.MoveY == y) return true;
            return false;
        }

        private bool IsEscapeTarget(int x, int y)
        {
            int tile = GameTileManagerAPI.Instance.GetTileId(x, y);
            return IsValidTileId(tile) && runtime.movementTargetAvailability[y * MapWidth + x] != 0 &&
                (runtime.tileFlags[tile] & EscapeBlockedTargetFlags) == 0;
        }

        // Unburnt pitch ignites from burning neighbours (0x7D280), and fire spreads to adjacent tiles
        // (0x99420): such a target is likely to burn soon; a plague cloud's margin likewise (it drifts).
        private bool IsFireFront(int x, int y)
        {
            if (plagueNear.Contains(y * MapWidth + x)) return true;
            int tile = GameTileManagerAPI.Instance.GetTileId(x, y);
            if (GameTileManagerAPI.Instance.HasTilePropertyFlag(tile, TilePropertyFlag.PitchTrap)) return true;
            for (int d = 0; d < 8; d++)
            {
                int nx = x + FastRouteField.Dx[d], ny = y + FastRouteField.Dy[d];
                if ((uint)nx < MapWidth && (uint)ny < MapWidth && fireTiles.Contains(ny * MapWidth + nx)) return true;
            }
            return false;
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
        // the end of the window ends the case; the soldier then rejoins its group.
        private void ProcessFireEscapes(int tick)
        {
            fireEscapeDone.Clear();
            foreach (KeyValuePair<int, FireEscapeEntry> pair in fireEscapes)
            {
                int id = pair.Key; FireEscapeEntry entry = pair.Value;
                if (!APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) || unit == null || !APIShared.UnitAccess.IsReallyAlive(unit) ||
                    unit->r_GlobalId != entry.Global)
                { LogEscape("end-gone", id, entry); EndFireEscape(id, entry); escapeEndedGone++; continue; }
                int targetX = unit->r_TargetTilePositionX2, targetY = unit->r_TargetTilePositionY2;
                // Vanilla moves a walker whose target tile is taken to a neighbouring tile (same order and tribe).
                if (unit->r_TribeId == entry.Tribe.Own && (targetX != entry.MoveX || targetY != entry.MoveY) && IsMoveCommanded(unit) &&
                    Math.Abs(targetX - entry.MoveX) <= 1 && Math.Abs(targetY - entry.MoveY) <= 1)
                { LogEscape("shifted", id, entry); entry.MoveX = targetX; entry.MoveY = targetY; escapeShifted++; }
                if (unit->r_TribeId != entry.Tribe.Own || targetX != entry.MoveX || targetY != entry.MoveY)
                {
                    // A new order (player selection, combat AI): no new escape for a while, so the two do not take turns every 8 ticks.
                    LogEscape("end-order", id, entry);
                    if (unit->r_TribeId != entry.Tribe.Own) escapeEndedTribe++;
                    EndFireEscape(id, entry); escapeEndedOrder++; fireEscapeRetry[id] = tick + EscapeRetryTicks;
                    continue;
                }
                bool moving = IsWalking(unit);
                int x = unit->r_CurrentTilePositionX, y = unit->r_CurrentTilePositionY;
                // On the way out of the fire nothing else happens; a returning soldier leaves new fire again.
                if (moving && !entry.Returning) continue;
                if (TryGetEscapeHazard(unit, x, y, out EscapeHazard again))
                {
                    if (again == EscapeHazard.Fire) entry.Hazard = EscapeHazard.Fire;
                    // Leave again, keeping the return point and a running window.
                    if (!TryFindFireEscape(id, entry.Player, x, y, out int againX, out int againY)) { escapeFailed++; continue; }
                    if (TryMoveSoldier(id, entry.Global, entry.Tribe, againX, againY, out int moveX, out int moveY))
                    { entry.MoveX = moveX; entry.MoveY = moveY; entry.Returning = false; escapeReescaped++; LogEscape("again", id, entry); }
                    else { LogEscape("again-failed", id, entry); EndFireEscape(id, entry); escapeFailed++; }
                    continue;
                }
                if (moving) continue;
                if (entry.Returning)
                {
                    // Arrived at its old position or, when Vanilla shifted a taken tile, next to it.
                    if (Math.Abs(x - entry.ReturnX) <= 1 && Math.Abs(y - entry.ReturnY) <= 1) escapeReturnArrived++; else escapeStayed++;
                    LogEscape("end-returned", id, entry);
                    EndFireEscape(id, entry);
                    continue;
                }
                if (!entry.Safe)
                {
                    entry.Safe = true; entry.NextProbe = tick + EscapeProbeTicks;
                    entry.Until = tick + (entry.Kind == FireReturnKind.Position ? (entry.Hazard == EscapeHazard.Plague ? plagueReturnTicks : fireReturnTicks) :
                        EscapePolicy(entry.Hazard).MaximumWaitTicks);
                    LogEscape("safe", id, entry);
                }
                if (tick - entry.Until >= 0) { LogEscape("end-window", id, entry); EndFireEscape(id, entry); escapeStayed++; continue; }
                if (tick - entry.NextProbe < 0) continue;
                if (x == entry.ReturnX && y == entry.ReturnY) { EndFireEscape(id, entry); escapeReturnArrived++; continue; }
                if (IsHazardNode(entry.ReturnY * MapWidth + entry.ReturnX)) { entry.NextProbe = tick + EscapeProbeTicks; escapeReturnTargetBurning++; continue; }
                if (!AnchorReady(entry.Player)) { entry.NextProbe = tick + EscapeProbeTicks; continue; }
                if (tick != replanProbeTick) { replanProbeTick = tick; replanProbeLeft = ReplanProbeTickSettles; }
                if (replanProbeLeft <= 0) { entry.NextProbe = tick + 1; replanProbesDeferred++; continue; }
                entry.NextProbe = tick + EscapeProbeTicks;
                int distance = Math.Max(Math.Abs(entry.ReturnX - x), Math.Abs(entry.ReturnY - y));
                int probe = CoreRouteProbe(entry.Player, x, y, entry.ReturnX, entry.ReturnY, EscapePolicy(entry.Hazard), distance);
                escapeReturnProbes[probe]++;
                if (probe != ProbeClear) { LogFireCase("return-blocked", id, $"probe={probe}"); continue; }
                if (TryMoveSoldier(id, entry.Global, entry.Tribe, entry.ReturnX, entry.ReturnY, out int returnX, out int returnY))
                { entry.MoveX = returnX; entry.MoveY = returnY; entry.Returning = true; escapeReturnIssued++; LogEscape("return", id, entry); }
                else { LogEscape("return-failed", id, entry); EndFireEscape(id, entry); escapeFailed++; }
            }
            foreach (int id in fireEscapeDone) fireEscapes.Remove(id);
        }

        private void EndFireEscape(int id, FireEscapeEntry entry)
        {
            fireEscapeDone.Add(id);
            RejoinSoldier(id, entry.Global, entry.Tribe);
        }

        // The burning tiles belong to FireRouteRules (cleared by its Reset with its own state). On a map
        // change the tribes belong to the old map: the cases are dropped without rejoining.
        private void ResetFireEscapes() { fireEscapes.Clear(); fireEscapeRetry.Clear(); fireCaseLogs = 0; }

        private string FormatFireEscapes() =>
            $"plague(clouds={plagueCloudCount} tiles={plagueTiles.Count} near={plagueNear.Count} heightPolicy={(plaguePolicyActive ? "APIShared" : "Vanilla")}) " +
            $"escape(burning={fireTiles.Count} plagueTiles={plagueTiles.Count} active={fireEscapes.Count} checks={escapeChecks} started={escapeStarted} startedPlague={escapeStartedPlague} " +
            $"reescaped={escapeReescaped} shifted={escapeShifted} " +
            $"returnIssued={escapeReturnIssued} returnArrived={escapeReturnArrived} stayed={escapeStayed} endedOrder={escapeEndedOrder} endedOrderTribe={escapeEndedTribe} " +
            $"endedGone={escapeEndedGone} failed={escapeFailed} returnWaits(targetHazard,noRoute,tooLong,hazard)={escapeReturnTargetBurning}," +
            $"{escapeReturnProbes[ProbeNoRoute]},{escapeReturnProbes[ProbeTooLong]},{escapeReturnProbes[ProbeRuleTile]} " +
            $"ms={AnchorMs(escapeTicks)} maxMs={AnchorMs(escapeMaxTicks)})";

        // DEVDIAG: one line per fire case event (escape start, again, safe, return, end; held resume) with
        // the unit's command state, at most FireCaseLogLimit per map. Release builds drop the calls.
        private const int FireCaseLogLimit = 400;
        private int fireCaseLogs;

        [System.Diagnostics.Conditional("SERP_DEVDIAG")]
        private void LogFireCase(string phase, int id, string detail)
        {
            if (fireCaseLogs >= FireCaseLogLimit) return;
            fireCaseLogs++;
            string state = "unit=missing";
            if (APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _) && unit != null)
                state = $"global={unit->r_GlobalId} type={unit->r_UnitChimp} player={unit->r_ControllableForPlayerId} alive={(int)unit->r_AliveState} " +
                    $"at={unit->r_CurrentTilePositionX},{unit->r_CurrentTilePositionY} target={unit->r_TargetTilePositionX2},{unit->r_TargetTilePositionY2} " +
                    $"tribe={unit->r_TribeId} aiState={unit->r_AIState} path={unit->r_PathPlanStateBitFlags}:{unit->r_CurrentPathPlanIndex}/{unit->r_PathPlanLength}";
            int tick = replanTick;
            Shared.DevDiag.Log(() => $"MOATMOVE_FIRE_CASE tick={tick} phase={phase} unit={id} {state} {detail}");
        }

        [System.Diagnostics.Conditional("SERP_DEVDIAG")]
        private void LogEscape(string phase, int id, FireEscapeEntry entry) =>
            LogFireCase(phase, id, $"hazard={entry.Hazard} kind={entry.Kind} move={entry.MoveX},{entry.MoveY} return={entry.ReturnX},{entry.ReturnY} safe={entry.Safe} " +
                $"returning={entry.Returning} until={entry.Until} own={entry.Tribe.Own}/{entry.Tribe.OwnGlobal} original={entry.Tribe.Original}/{entry.Tribe.OriginalGlobal} split={entry.Tribe.Split}");

        // The performance line also appears without group moves while fire burns, cases are open or the
        // window had fire waits or escapes (their outcome follows the fire, not a command).
        private bool FireCasesReportable() =>
            fireTiles.Count != 0 || plagueTiles.Count != 0 || fireEscapes.Count != 0 || replanWaits.Count != 0 || escapeStarted != 0 || escapeStayed != 0 ||
            escapeReturnArrived != 0 || escapeEndedOrder != 0 || escapeEndedGone != 0 || escapeFailed != 0 ||
            replanWaitsStarted != 0 || replanResumed != 0 || replanExpired != 0 || replanAborted != 0 || replanHeld != 0 || replanEnded != 0;

        private void ResetFireEscapeMetrics()
        {
            escapeChecks = escapeStarted = escapeReescaped = escapeReturnIssued = escapeReturnArrived = escapeStayed = escapeEndedOrder = escapeEndedGone =
                escapeFailed = escapeReturnTargetBurning = escapeEndedTribe = escapeShifted = escapeStartedPlague = escapeTicks = escapeMaxTicks = 0;
            Array.Clear(escapeReturnProbes, 0, escapeReturnProbes.Length);
        }
    }
}
