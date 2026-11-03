using System;
using System.IO;

namespace MoatMove
{
    internal sealed unsafe partial class FriendlyMoatMovementRuntime
    {
        // Anchor mode against the real native core (build.bat builds it into .local/anchor
        // before the tests): masks start for alive human players only, the map's first scan completes
        // them in one tick and later requests keep the per-tick budget, a moat route passes the live audit, a terrain change on the route
        // makes the task stale, stale restarts are bounded, and a steady tick allocates nothing.
        // Replan watch (RouteReplanPolicy with AnchorSingleMoves) on the anchor corridor (ground east of
        // x 136, player 1's mask ready): a walking unit 3 steps before a new rule tile re-plans to its
        // unchanged target and keeps an allowed detour; a detour beyond the policy stops it on its next
        // tile; once the core finds a clear route the unit resumes its target. Unit 7 walks east from
        // (140,10) to (160,10) in group 3 (order target 160,10); MoveHandler plays 196280's result. The
        // move resumes as the group's Vanilla command again (one per tribe), not with 196280. Only
        // soldiers are watched (no civilians or siege equipment); a failed detour (196280) ends in an
        // explicit stop, a group re-ordered meanwhile leaves the unit stopped, and MoatMove's fire
        // policy aborts the move after the wait instead of resuming.
        private void ReplanWatchTests(ref int ticks)
        {
            bool singleBefore = settings.AnchorSingleMoves; settings.AnchorSingleMoves = true;
            GameUnitManagerAPI units = GameUnitManagerAPI.Instance;
            GameTribeManagerAPI tribes = GameTribeManagerAPI.Instance;
            GameUnit* walker = units.Units + 7, second = units.Units + 9;
            GameUnit saved = *walker, savedSecond = *second;
            GameTribe* table = InstallTribeTable(8);
            Check(APIShared.Pathfinding.RouteTileRules.TryRegister("tests.moatmove", "replan", out APIShared.Pathfinding.RouteTileRuleSet fire, out _),
                "replan rule registration failed");
            fire.ReplanPolicy = new APIShared.Pathfinding.RouteReplanPolicy(3, 50, 10, 400);
            int[] Steps(int first, int east, int last)
            {
                var steps = new System.Collections.Generic.List<int>();
                if (first >= 0) steps.Add(first);
                for (int i = 0; i < east; i++) steps.Add(2);
                if (last >= 0) steps.Add(last);
                return steps.ToArray();
            }
            int[] straight = Steps(-1, 20, -1), detour = Steps(1, 18, 3), tooLong = Steps(1, 38, 3);
            int[] answer = detour;
            units.MoveHandler = (id, x, y) =>
            {
                if (x == 160 && y == 10) Route(id, 140, 10, answer, x, y);
                else Route(id, x, y, new int[0], x, y);
            };
            try
            {
                walker->r_GlobalId = 9007; walker->r_ControllableForPlayerId = 1; walker->r_AliveState = AliveState.IsAlive; walker->r_TribeId = 3;
                walker->r_IsKilledByProjectile = 0; walker->r_MovementSubstep = 0; walker->r_SelectionAllowed = 1; walker->r_UnitChimp = 0;
                // Unit 9 joins group 3 for the two-member resume at the end only.
                *second = *walker; second->r_GlobalId = 9009; second->r_TribeId = 0; second->r_SelectionAllowed = 0; second->r_PathPlanStateBitFlags = 0;
                Route(7, 140, 10, straight, 160, 10);
                units.Moves.Clear();
                long detours = replanDetours, waits = replanWaitsStarted, resumed = replanResumed;
                Check(fire.Set(143, 10, APIShared.Pathfinding.RouteTileEffect.Penalty, 254), "fire rule rejected");
                AnchorTick(++ticks);
                Check(replanTileCount == 1, "the replan tile set misses the new rule tile");
                // New rule tiles check every 8th unit for 8 ticks: unit 7 is decided within them.
                for (int i = 0; i < 8; i++) ReplanTick(++ticks);
                Check(replanDetours == detours + 1 && units.Moves.Count == 1 && units.Moves[0][0] == 7 && units.Moves[0][1] == 160 && units.Moves[0][2] == 10,
                    $"an allowed detour was not taken (moves={units.Moves.Count} detours={replanDetours - detours})");

                // The same situation with a 40-step answer (allowed: 20 + 10): the unit stops on its next tile.
                Route(7, 140, 10, straight, 160, 10); answer = tooLong; units.Moves.Clear();
                for (int i = 0; i < 40 && replanWaitsStarted == waits; i++) ReplanTick(++ticks);
                Check(replanWaitsStarted == waits + 1 && replanWaits.ContainsKey(7) && units.Moves.Count == 2 &&
                    units.Moves[1][1] == 140 && units.Moves[1][2] == 10 && walker->r_PathPlanStateBitFlags == 0,
                    $"a detour beyond the policy did not stop the unit (moves={units.Moves.Count})");

                // The fire goes out: the next probe finds the clear core route and the unit resumes.
                fire.ClearAll(); AnchorTick(++ticks); answer = straight; units.Moves.Clear(); tribes.Moves.Clear();
                // An exhausted per-tick probe budget moves the probe to the next tick instead of searching.
                long deferred = replanProbesDeferred;
                for (int i = 0; i < 64 && replanProbesDeferred == deferred; i++) { replanProbeTick = ticks + 1; replanProbeLeft = 0; ReplanTick(++ticks); }
                Check(replanProbesDeferred == deferred + 1 && replanResumed == resumed && replanWaits.ContainsKey(7) && units.Moves.Count == 0,
                    "an exhausted probe budget did not defer the waiting unit's probe");
                for (int i = 0; i < 64 && replanResumed == resumed; i++) ReplanTick(++ticks);
                Check(replanResumed == resumed + 1 && !replanWaits.ContainsKey(7) && units.Moves.Count == 0 && tribes.Moves.Count == 1 &&
                    tribes.Moves[0][0] == 3 && tribes.Moves[0][1] == 160 && walker->r_TargetTilePositionX2 == 160 && walker->r_PathPlanStateBitFlags != 0,
                    $"a waiting unit did not resume with its group command once its route was clear (moves={tribes.Moves.Count})");
                // Without rule tiles or waits the watch returns at once.
                long checks = replanChecks;
                for (int i = 0; i < 40; i++) ReplanTick(++ticks);
                Check(replanChecks == checks, "the replan watch checks units without rule tiles");

                // Civilians, workers and firemen (r_SelectionAllowed 0) and siege equipment are not watched.
                Check(fire.Set(143, 10, APIShared.Pathfinding.RouteTileEffect.Penalty, 254), "fire rule rejected");
                AnchorTick(++ticks); replanSettled.Clear(); answer = tooLong;
                long triggers = replanTriggers;
                foreach (int chimp in new[] { -1, eChimps.CHIMP_TYPE_TREBUCHET, eChimps.CHIMP_TYPE_BATTERING_RAM })
                {
                    walker->r_SelectionAllowed = chimp < 0 ? 0 : 1; walker->r_UnitChimp = chimp < 0 ? 0 : chimp;
                    Route(7, 140, 10, straight, 160, 10); units.Moves.Clear();
                    for (int i = 0; i < 48; i++) ReplanTick(++ticks);
                    Check(replanTriggers == triggers && units.Moves.Count == 0, $"the replan watch stopped a non-soldier (chimp {chimp})");
                }
                walker->r_SelectionAllowed = 1; walker->r_UnitChimp = 0;

                // A failed detour (196280 Post result 0, route buffer zeroed) ends in an explicit stop on the
                // movement start and a wait instead of a dead route.
                units.MoveHandler = (id, x, y) =>
                {
                    if (x == 160 && y == 10)
                    {
                        byte* path = nativeUnitManager + NativeUnitPathBufferOffset + id * NativeUnitPathBufferStride;
                        for (int i = 0; i < NativeUnitPathBufferStride; i++) path[i] = 0;
                        GameUnit* unit = units.Units + id;
                        unit->r_TargetTilePositionX2 = x; unit->r_TargetTilePositionY2 = y; unit->r_PathPlanStateBitFlags = 2; unit->r_PathPlanLength = 20;
                        ObserveReplanMove(new UnitMoveHereEventArgs(EventHookPhase.Post, id, x, y, 0) { ReturnValue = 0 });
                    }
                    else Route(id, x, y, new int[0], x, y);
                };
                Route(7, 140, 10, straight, 160, 10); units.Moves.Clear();
                long failures = replanMoveFailures;
                for (int i = 0; i < 48 && !replanWaits.ContainsKey(7); i++) ReplanTick(++ticks);
                Check(replanWaits.ContainsKey(7) && replanMoveFailures == failures + 1 && units.Moves.Count == 2 && units.Moves[1][1] == 140 &&
                    walker->r_PathPlanStateBitFlags == 0 && walker->r_TargetTilePositionX2 == 140,
                    $"a failed detour did not stop the unit (moves={units.Moves.Count})");
                // A foreign policy resumes after the wait, but the group was re-ordered meanwhile: no command,
                // the unit stays stopped.
                TribeOrder plain = tribeOrders[3];
                ObserveTribeOrder(3, true, 170, 10, 0, true, TribeMoveType.DefaultInSync);
                Check(!tribeOrders[3].Plain, "a move outside 196100 counted as a plain player move");
                units.Moves.Clear(); tribes.Moves.Clear();
                long held = replanHeld, expired = replanExpired;
                for (int i = 0; i < 480 && replanWaits.ContainsKey(7); i++) ReplanTick(++ticks);
                tribeOrders[3] = plain;
                Check(!replanWaits.ContainsKey(7) && replanHeld == held + 1 && replanExpired == expired &&
                    units.Moves.Count == 0 && tribes.Moves.Count == 0 && walker->r_PathPlanStateBitFlags == 0 && walker->r_TargetTilePositionX2 == 140,
                    $"a wait of a re-ordered group resumed (moves={tribes.Moves.Count})");

                // MoatMove's fire policy: no clear route within the wait aborts the move; the unit stays.
                units.MoveHandler = (id, x, y) =>
                {
                    if (x == 160 && y == 10) Route(id, 140, 10, answer, x, y);
                    else Route(id, x, y, new int[0], x, y);
                };
                plagueAbortPolicy = new APIShared.Pathfinding.RouteReplanPolicy(3, 50, 10, 400);
                Check(IsAbortPolicy(plagueAbortPolicy) && !IsAbortPolicy(fire.ReplanPolicy), "the plague policy does not abort");
                plagueAbortPolicy = null;
                fireAbortPolicy = fire.ReplanPolicy; replanSettled.Clear(); tribes.Moves.Clear();
                Route(7, 140, 10, straight, 160, 10);
                for (int i = 0; i < 48 && !replanWaits.ContainsKey(7); i++) ReplanTick(++ticks);
                Check(replanWaits.ContainsKey(7), "the fire policy did not start a wait");
                units.Moves.Clear();
                long aborted = replanAborted; expired = replanExpired;
                for (int i = 0; i < 480 && replanWaits.ContainsKey(7); i++) ReplanTick(++ticks);
                Check(!replanWaits.ContainsKey(7) && replanAborted == aborted + 1 && replanExpired == expired && units.Moves.Count == 0 &&
                    tribes.Moves.Count == 0 && walker->r_PathPlanStateBitFlags == 0 && walker->r_TargetTilePositionX2 == 140,
                    $"an expired fire wait did not abort the move (moves={units.Moves.Count})");
                for (int i = 0; i < 48; i++) ReplanTick(++ticks);
                Check(units.Moves.Count == 0 && tribes.Moves.Count == 0, "an aborted unit is moved again");

                // Two stopped members of group 3 with a clear route: one group command moves both.
                fire.ClearAll(); AnchorTick(++ticks); replanSettled.Clear(); units.Moves.Clear(); tribes.Moves.Clear();
                second->r_TribeId = 3; second->r_SelectionAllowed = 1;
                Route(7, 140, 10, new int[0], 140, 10); Route(9, 141, 10, new int[0], 141, 10);
                int due = ticks + 8 - (ticks & 7);
                replanWaits[7] = new ReplanWait { Global = 9007, Player = 1, TargetX = 160, TargetY = 10, StopX = 140, StopY = 10, Remaining = 20,
                    Until = due + 400, NextProbe = due, Policy = fire.ReplanPolicy, Tribe = 3, Order = tribeOrders[3] };
                replanWaits[9] = new ReplanWait { Global = 9009, Player = 1, TargetX = 161, TargetY = 10, StopX = 141, StopY = 10, Remaining = 20,
                    Until = due + 400, NextProbe = due + 40, Policy = fire.ReplanPolicy, Tribe = 3, Order = tribeOrders[3] };
                resumed = replanResumed;
                while (ticks < due) ReplanTick(++ticks);
                Check(replanWaits.Count == 0 && replanResumed == resumed + 2 && tribes.Moves.Count == 1 && tribes.Moves[0][0] == 3 &&
                    walker->r_TargetTilePositionX2 == 160 && second->r_TargetTilePositionX2 == 160 && units.Moves.Count == 0,
                    $"two waiting members of a group did not resume with one group command (moves={tribes.Moves.Count})");

                // A formation order (a player move outside 196100): the member walks alone to its own slot in a
                // tribe of its own and rejoins group 3 on arrival.
                TribeOrder formation = tribeOrders[3]; formation.Plain = false; tribeOrders[3] = formation;
                second->r_TribeId = 0; replanSettled.Clear(); tribes.Moves.Clear();
                Route(7, 140, 10, new int[0], 140, 10);
                due = ticks + 8 - (ticks & 7);
                replanWaits[7] = new ReplanWait { Global = 9007, Player = 1, TargetX = 158, TargetY = 10, StopX = 140, StopY = 10, Remaining = 20,
                    Until = due + 400, NextProbe = due, Policy = fire.ReplanPolicy, Tribe = 3, Order = formation };
                long soloResumed = replanSoloResumed;
                while (ticks < due) ReplanTick(++ticks);
                int solo = walker->r_TribeId;
                Check(replanSoloResumed == soloResumed + 1 && tribes.Moves.Count == 1 && tribes.Moves[0][0] == solo && solo != 3 && tribes.Moves[0][1] == 158 &&
                    walker->r_TargetTilePositionX2 == 158 && replanSolos.ContainsKey(7), $"a formation member did not resume alone to its slot (moves={tribes.Moves.Count})");
                for (int i = 0; i < 16; i++) ReplanTick(++ticks);
                Check(replanSolos.ContainsKey(7) && walker->r_TribeId == solo, "a walking solo member rejoined before arriving");
                Route(7, 158, 10, new int[0], 158, 10);
                for (int i = 0; i < 8 && replanSolos.Count != 0; i++) ReplanTick(++ticks);
                Check(!replanSolos.ContainsKey(7) && walker->r_TribeId == 3 && table[solo].r_AliveState == AliveState.Dead,
                    "a solo member did not rejoin its group on arrival");

                // A queued order (shift queue) is not repeated.
                formation.Queued = true; tribeOrders[3] = formation; tribes.Moves.Clear();
                Route(7, 140, 10, new int[0], 140, 10);
                due = ticks + 8 - (ticks & 7);
                replanWaits[7] = new ReplanWait { Global = 9007, Player = 1, TargetX = 158, TargetY = 10, StopX = 140, StopY = 10, Remaining = 20,
                    Until = due + 400, NextProbe = due, Policy = fire.ReplanPolicy, Tribe = 3, Order = formation };
                long queuedHeld = replanHeldReasons[HeldQueued];
                while (ticks < due) ReplanTick(++ticks);
                Check(!replanWaits.ContainsKey(7) && replanHeldReasons[HeldQueued] == queuedHeld + 1 && tribes.Moves.Count == 0 && walker->r_TribeId == 3,
                    "a queued order was repeated");
            }
            finally
            {
                units.MoveHandler = null; units.Moves.Clear(); fire.ClearAll(); fire.ReplanPolicy = null; AnchorTick(++ticks);
                *walker = saved; *second = savedSecond; ResetReplanWatch(); fireAbortPolicy = null; settings.AnchorSingleMoves = singleBefore;
                RemoveTribeTable(table);
            }
        }

        // A tribe table for Vanilla group commands (GameTribeManagerAPI double): slots 1..size-1 free
        // except group 3 (player 1, global 3003) with a plain player move to 160,10 (two-point order and
        // the replan watch's observed order). The command moves the tribe's members (units 7 and 9) on
        // row 10 straight from their current tile to the commanded tile and sets the walking state.
        private GameTribe* InstallTribeTable(int size)
        {
            var tribes = GameTribeManagerAPI.Instance;
            var table = (GameTribe*)System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeof(GameTribe) * size);
            for (int i = 0; i < size; i++) table[i] = new GameTribe { r_AliveState = AliveState.Dead };
            table[3] = new GameTribe { r_PlayerIdOwner = 1, r_GlobalId = 3003, r_AliveState = AliveState.IsAlive, r_UnitsInGroup = 2,
                r_TribeStance = TribeStance.Aggressive, r_PatrolPoint2TileX = 160, r_PatrolPoint2TileY = 10, r_CurrentPatrolPoints = 2, r_PatrolCurrentTargetIndex = 1 };
            tribeOrders[3] = new TribeOrder { Global = 3003, Sequence = ++tribeOrderSequence, X = 160, Y = 10, NewOrder = true, Move = true, Plain = true,
                MoveType = TribeMoveType.DefaultInSync };
            tribes.Table = table; tribes.TableSize = size; tribes.Moves.Clear(); tribes.Deleted.Clear(); tribes.FailCreate = tribes.FailAssign = false;
            tribes.MoveHandler = (tribe, x, y) =>
            {
                foreach (int id in new[] { 7, 9 })
                {
                    GameUnit* unit = GameUnitManagerAPI.Instance.Units + id;
                    if (unit->r_TribeId != tribe || unit->r_AliveState != AliveState.IsAlive || unit->r_GlobalId == 0) continue;
                    int fromX = unit->r_CurrentTilePositionX, count = Math.Abs(x - fromX);
                    var steps = new int[count];
                    for (int i = 0; i < count; i++) steps[i] = x > fromX ? 2 : 6;
                    Route(id, fromX, 10, steps, x, y);
                    unit->r_AIState = 101;
                }
                return true;
            };
            return table;
        }

        private void RemoveTribeTable(GameTribe* table)
        {
            var tribes = GameTribeManagerAPI.Instance;
            tribes.Table = null; tribes.TableSize = 0; tribes.MoveHandler = null; tribes.Moves.Clear(); tribes.Deleted.Clear();
            tribes.FailCreate = tribes.FailAssign = false;
            System.Runtime.InteropServices.Marshal.FreeHGlobal((IntPtr)table);
        }

        // FireEscape on the anchor corridor (row 10, ground east of x 136): a soldier on a burning tile
        // gets a tribe of its own and a Vanilla group command to the nearest standable tile off the fire
        // front with the fewest burning tiles on the way, returns once the fire is out and then rejoins
        // its group. A standing soldier's window (fireReturnTicks) and a walking one's (policy wait) start
        // on reaching a fire-free tile; another escape keeps them. A new order ends the case. Immune tiles,
        // civilians and siege equipment are left alone; a waiting unit keeps its wait target as return point.
        private void FireEscapeTests(ref int ticks)
        {
            GameUnitManagerAPI units = GameUnitManagerAPI.Instance;
            GameTribeManagerAPI tribes = GameTribeManagerAPI.Instance;
            GameUnit* walker = units.Units + 7, second = units.Units + 9;
            GameUnit saved = *walker, savedSecond = *second;
            var api = GameTileManagerAPI.Instance;
            var availability = new byte[181];
            for (int x = 136; x <= 180; x++) { availability[x] = movementTargetAvailability[10 * MapWidth + x]; movementTargetAvailability[10 * MapWidth + x] = 1; }
            int returnTicksBefore = fireReturnTicks;
            // Escape ticks run every 8th simulation tick (a ref parameter cannot be captured).
            int now = ticks;
            int Next() { now += 8 - (now & 7); return now; }
            void Burn(int from, int to, bool on) { for (int x = from; x <= to; x++) fireTiles.Set(10 * MapWidth + x, on); }
            void Pitch(int from, int to, bool on)
            {
                for (int x = from; x <= to; x++)
                {
                    int tile = api.GetTileId(x, 10);
                    api.TileManager.LogicGrid[tile] = on ? api.TileManager.LogicGrid[tile] | (int)TilePropertyFlag.PitchTrap : api.TileManager.LogicGrid[tile] & ~(int)TilePropertyFlag.PitchTrap;
                }
            }
            void Stand(int x) => Route(7, x, 10, new int[0], x, 10);
            void Arrive(int x) { int tx = walker->r_TargetTilePositionX2, ty = walker->r_TargetTilePositionY2; Route(7, x, 10, new int[0], tx, ty); }
            void Join(int tribe) { walker->r_TribeId = tribe; }
            // Drops the case and puts the soldier back into group 3 (its own tribe removed).
            void Reset()
            {
                ResetFireEscapes();
                int own = walker->r_TribeId;
                if (own != 3) { tribes.UnassignUnit(own, 7); tribes.DeleteTribeSafe(own); tribes.AssignUnit(3, 7); }
            }
            GameTribe* table = InstallTribeTable(8);
            try
            {
                walker->r_GlobalId = 9007; walker->r_ControllableForPlayerId = 1; walker->r_AliveState = AliveState.IsAlive; Join(3);
                walker->r_IsKilledByProjectile = 0; walker->r_MovementSubstep = 0; walker->r_SelectionAllowed = 1; walker->r_UnitChimp = 0;
                fireEscapeActive = true; fireEscapePolicy = new APIShared.Pathfinding.RouteReplanPolicy(3, 50, 10, 400); fireReturnTicks = 800;
                ResetFireEscapes();

                // Burning 149..152, the soldier stands at 150: west crosses one burning tile, east two; 148 lies at
                // the fire front, so the soldier goes on to 147 in a tribe of its own (slot 1), its group keeps one.
                Stand(150); Burn(149, 152, true); units.Moves.Clear();
                long started = escapeStarted;
                FireEscapeEntry entry = null;
                FireEscapeTick(Next());
                Check(escapeStarted == started + 1 && tribes.Moves.Count == 1 && tribes.Moves[0][0] == 1 && tribes.Moves[0][1] == 147 &&
                    walker->r_TribeId == 1 && table[3].r_UnitsInGroup == 1 && table[1].r_TribeStance == TribeStance.Aggressive && units.Moves.Count == 0 &&
                    fireEscapes.TryGetValue(7, out entry) && entry.ReturnX == 150 && entry.ReturnY == 10 && entry.Kind == FireReturnKind.Position,
                    $"a soldier on fire did not escape off the fire front in a tribe of its own (moves={tribes.Moves.Count})");
                // The last step is still in progress (cursor == length, next tile ahead) on a burning tile: no new escape.
                long again = escapeReescaped;
                walker->r_CurrentTilePositionX = 149; walker->r_NextTilePositionX2 = 148; walker->r_CurrentPathPlanIndex = walker->r_PathPlanLength;
                FireEscapeTick(Next());
                Check(escapeReescaped == again && tribes.Moves.Count == 1 && fireEscapes.ContainsKey(7) && !entry.Safe,
                    "a soldier in its last step was sent out of the fire again");
                // Vanilla moves a walker whose target is taken to a neighbouring tile: the case keeps the new target.
                long shifted = escapeShifted;
                walker->r_TargetTilePositionX2 = 146;
                FireEscapeTick(Next());
                Check(escapeShifted == shifted + 1 && fireEscapes.ContainsKey(7) && entry.MoveX == 146, "a shifted target ended the escape");
                walker->r_TargetTilePositionX2 = 147; entry.MoveX = 147;
                // On the way out nothing else happens, however long it takes: the window has not started.
                for (int i = 0; i < 120; i++) FireEscapeTick(Next());
                Check(tribes.Moves.Count == 1 && fireEscapes.ContainsKey(7) && !entry.Safe, "an escaping unit was moved again or its window started early");
                Arrive(147);
                long targetBurning = escapeReturnTargetBurning;
                for (int i = 0; i < 64; i++) FireEscapeTick(Next());
                Check(tribes.Moves.Count == 1 && fireEscapes.ContainsKey(7) && entry.Safe, "a soldier returned into burning fire or left before its window");
                Check(escapeReturnTargetBurning > targetBurning && FireCasesReportable(), "a burning return target was not reported");
                // The fire goes out: the next probe finds the clear core route and the soldier returns, then rejoins.
                Burn(149, 152, false);
                long issued = escapeReturnIssued, arrived = escapeReturnArrived;
                for (int i = 0; i < 8 && !entry.Returning; i++) FireEscapeTick(Next());
                Check(escapeReturnIssued == issued + 1 && tribes.Moves.Count == 2 && tribes.Moves[1][0] == 1 && tribes.Moves[1][1] == 150 &&
                    fireEscapes.ContainsKey(7), $"an escaped soldier did not return once the fire was out (moves={tribes.Moves.Count})");
                FireEscapeTick(Next());
                Check(fireEscapes.ContainsKey(7) && escapeReturnArrived == arrived, "a returning soldier counted as arrived on the way");
                Arrive(150); FireEscapeTick(Next());
                Check(!fireEscapes.ContainsKey(7) && escapeReturnArrived == arrived + 1 && walker->r_TribeId == 3 && table[3].r_UnitsInGroup == 2 &&
                    tribes.Deleted.Count == 1 && tribes.Deleted[0] == 1, "a returned soldier did not rejoin its group");

                // The fire keeps burning: the window of a standing soldier (800 ticks here) starts at 147;
                // another escape there (147 catches fire) keeps it running.
                Stand(150); Burn(149, 152, true); tribes.Moves.Clear(); tribes.Deleted.Clear();
                FireEscapeTick(Next()); Arrive(147); FireEscapeTick(Next());
                int safeAt = now;
                Check(fireEscapes.TryGetValue(7, out entry) && entry.Safe, "the soldier did not escape again");
                for (int i = 0; i < 40; i++) FireEscapeTick(Next());
                Burn(147, 147, true);
                long reescaped = escapeReescaped;
                FireEscapeTick(Next());
                Check(escapeReescaped == reescaped + 1 && tribes.Moves.Count == 2 && tribes.Moves[1][1] == 145 && entry.Until == safeAt + 800,
                    $"a soldier whose escape tile caught fire did not leave again with its window (moves={tribes.Moves.Count})");
                Arrive(145);
                long stayed = escapeStayed;
                while (now - safeAt < 800 - 8) FireEscapeTick(Next());
                Check(fireEscapes.ContainsKey(7), "a standing soldier's window ended early");
                for (int i = 0; i < 2; i++) FireEscapeTick(Next());
                Check(!fireEscapes.ContainsKey(7) && escapeStayed == stayed + 1 && tribes.Moves.Count == 2 && walker->r_TribeId == 3,
                    $"an escaped soldier did not stay and rejoin after its window (moves={tribes.Moves.Count})");
                Burn(147, 147, false);

                // A walking soldier continues to its move target within the policy's 400 ticks only.
                Route(7, 150, 10, new[] { 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2 }, 170, 10); tribes.Moves.Clear();
                FireEscapeTick(Next());
                Check(fireEscapes.TryGetValue(7, out entry) && entry.Kind == FireReturnKind.MoveTarget && entry.ReturnX == 170,
                    "a walking soldier did not keep its move target");
                Arrive(147); FireEscapeTick(Next()); safeAt = now; stayed = escapeStayed;
                while (now - safeAt < 400 - 8) FireEscapeTick(Next());
                Check(fireEscapes.ContainsKey(7), "a walking soldier's window ended early");
                for (int i = 0; i < 2; i++) FireEscapeTick(Next());
                Check(!fireEscapes.ContainsKey(7) && escapeStayed == stayed + 1, "a walking soldier's window did not end after 400 ticks");

                // A new order ends the case and rejoins the group ...
                Stand(150); tribes.Moves.Clear();
                FireEscapeTick(Next()); Arrive(147);
                Check(fireEscapes.ContainsKey(7), "the soldier did not escape again");
                Route(7, 147, 10, new[] { 2, 2, 2 }, 150, 10);
                long ended = escapeEndedOrder;
                FireEscapeTick(Next());
                Check(!fireEscapes.ContainsKey(7) && escapeEndedOrder == ended + 1 && walker->r_TribeId == 3, "a new order did not end the escape");
                // ... and blocks a new escape for a while, so combat AI and escape do not take turns.
                Stand(150); tribes.Moves.Clear();
                FireEscapeTick(Next());
                Check(!fireEscapes.ContainsKey(7) && tribes.Moves.Count == 0, "an escape restarted right after a new order");
                ResetFireEscapes();

                // A waiting unit escapes and later continues to the target of its wait.
                Stand(150); tribes.Moves.Clear();
                replanWaits[7] = new ReplanWait { Global = 9007, Player = 1, TargetX = 170, TargetY = 10, StopX = 150, StopY = 10 };
                FireEscapeTick(Next());
                Check(!replanWaits.ContainsKey(7) && fireEscapes.TryGetValue(7, out entry) && entry.ReturnX == 170 && entry.Kind == FireReturnKind.MoveTarget,
                    "a waiting unit lost its target when it escaped");
                // The group is gone (slot reused): the case ends without a rejoin, the soldier keeps its own tribe.
                table[3].r_GlobalId = 3999;
                Route(7, 150, 10, new[] { 2, 2 }, 152, 10);
                FireEscapeTick(Next());
                table[3].r_GlobalId = 3003;
                Check(!fireEscapes.ContainsKey(7) && walker->r_TribeId == 1 && table[1].r_AliveState == AliveState.IsAlive && table[1].r_UnitsInGroup == 1,
                    "a soldier rejoined a group that no longer exists");
                Reset();
                // An AI soldier whose group is gone leaves its own tribe for Vanilla's repair (0x118660).
                GamePlayerManagerAPI.Instance.AIPlayerMask = 1 << 1;
                Stand(150); tribes.Moves.Clear(); tribes.Deleted.Clear();
                FireEscapeTick(Next());
                table[3].r_GlobalId = 3999;
                Route(7, 150, 10, new[] { 2, 2 }, 152, 10);
                FireEscapeTick(Next());
                table[3].r_GlobalId = 3003; GamePlayerManagerAPI.Instance.AIPlayerMask = 0;
                Check(!fireEscapes.ContainsKey(7) && walker->r_TribeId == 0 && tribes.Deleted.Count == 1,
                    "an AI soldier without its group did not leave its own tribe");
                tribes.AssignUnit(3, 7); ResetFireEscapes();

                // Alone in its tribe the soldier keeps it; without a free tribe slot it does not escape.
                table[3].r_UnitsInGroup = 1; Stand(150); tribes.Moves.Clear(); tribes.Deleted.Clear();
                FireEscapeTick(Next());
                Check(tribes.Moves.Count == 1 && tribes.Moves[0][0] == 3 && walker->r_TribeId == 3 && table[1].r_AliveState == AliveState.Dead,
                    "a soldier alone in its tribe was split off");
                Reset(); table[3].r_UnitsInGroup = 2;
                Stand(150); tribes.Moves.Clear(); tribes.FailCreate = true;
                long failed = escapeFailed;
                FireEscapeTick(Next());
                tribes.FailCreate = false;
                Check(!fireEscapes.ContainsKey(7) && escapeFailed == failed + 1 && tribes.Moves.Count == 0 && walker->r_TribeId == 3,
                    "a failed split moved the soldier");
                ResetFireEscapes();
                // 11B520 returns 1 without admitting the member (no walking state): no case, back into the group.
                var admit = tribes.MoveHandler; tribes.MoveHandler = (t, x, y) => true; walker->r_AIState = 0;
                Stand(150); tribes.Moves.Clear(); tribes.Deleted.Clear(); failed = escapeFailed;
                FireEscapeTick(Next());
                tribes.MoveHandler = admit;
                Check(!fireEscapes.ContainsKey(7) && escapeFailed == failed + 1 && tribes.Moves.Count == 1 && walker->r_TribeId == 3 &&
                    table[1].r_AliveState == AliveState.Dead, "a soldier Vanilla did not admit was kept in its own tribe");
                ResetFireEscapes();

                // Unburnt pitch counts as fire front: 147 is pitch, 146 is chosen; with pitch everywhere in
                // reach the nearest tile off the fire (148) remains.
                Stand(150); tribes.Moves.Clear(); Pitch(147, 147, true);
                FireEscapeTick(Next());
                Check(tribes.Moves.Count == 1 && tribes.Moves[0][1] == 146, $"an escape ended on unburnt pitch (to {(tribes.Moves.Count > 0 ? tribes.Moves[0][1] : -1)})");
                Reset();
                Stand(150); tribes.Moves.Clear(); Pitch(136, 148, true); Pitch(153, 165, true);
                FireEscapeTick(Next());
                Pitch(136, 165, false);
                Check(tribes.Moves.Count == 1 && tribes.Moves[0][1] == 148, "without a tile off the fire front the soldier did not take the nearest");
                Reset();

                // Two soldiers on the same burning tile escape to different tiles.
                *second = *walker; second->r_GlobalId = 9009; second->r_TribeId = 0; tribes.AssignUnit(3, 9);
                Stand(150); Route(9, 150, 10, new int[0], 150, 10); tribes.Moves.Clear();
                FireEscapeTick(Next());
                Check(tribes.Moves.Count == 2 && tribes.Moves[0][1] != tribes.Moves[1][1] && fireEscapes.ContainsKey(9),
                    "two soldiers escaped to the same tile");
                int ownSecond = second->r_TribeId;
                Reset(); tribes.UnassignUnit(ownSecond, 9); tribes.DeleteTribeSafe(ownSecond); *second = savedSecond;

                // Immune tiles, civilians and siege equipment stay.
                int tile = api.GetTileId(150, 10); uint flags = tileFlags[tile];
                started = escapeStarted;
                tileFlags[tile] = flags | 0x01; Stand(150); tribes.Moves.Clear();
                FireEscapeTick(Next());
                tileFlags[tile] = flags;
                Check(escapeStarted == started && tribes.Moves.Count == 0, "a soldier on an immune tile escaped");
                walker->r_SelectionAllowed = 0; FireEscapeTick(Next());
                walker->r_SelectionAllowed = 1; walker->r_UnitChimp = eChimps.CHIMP_TYPE_MANGONEL; FireEscapeTick(Next());
                walker->r_UnitChimp = 0;
                Check(escapeStarted == started && tribes.Moves.Count == 0, "a civilian or siege equipment escaped");

                // Burning tiles without a unit on them allocate nothing.
                Stand(140); FireEscapeTick(Next());
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 16; i++) FireEscapeTick(Next());
                Check(GC.GetAllocatedBytesForCurrentThread() == allocated && tribes.Moves.Count == 0 && units.Moves.Count == 0, "an idle escape tick allocates");
            }
            finally
            {
                units.MoveHandler = null; units.Moves.Clear(); fireTiles.Clear(); ResetFireEscapes(); replanWaits.Clear();
                fireEscapeActive = false; fireEscapePolicy = null; fireReturnTicks = returnTicksBefore;
                for (int x = 136; x <= 180; x++) movementTargetAvailability[10 * MapWidth + x] = availability[x];
                RemoveTribeTable(table);
                *walker = saved; *second = savedSecond; ticks = now;
            }
        }

        // Terrain fixture of PlagueDamageModel: native packed tile ids index plain arrays.
        private sealed class PlagueTerrainFixture : IPlagueTerrain
        {
            public readonly uint[] TileFlags = new uint[PlagueDamageModel.NativeTileCount];
            public readonly byte[] Heights = new byte[PlagueDamageModel.NativeTileCount], Defaults = new byte[PlagueDamageModel.NativeTileCount];
            public readonly short[] Buildings = new short[PlagueDamageModel.NativeTileCount];
            public readonly System.Collections.Generic.Dictionary<int, (eStructs Type, int Base)> Records =
                new System.Collections.Generic.Dictionary<int, (eStructs Type, int Base)>();
            public uint Flags(int tile) => TileFlags[tile];
            public int RawHeight(int tile) => Heights[tile];
            public int DefaultHeight(int tile) => Defaults[tile];
            public int BuildingId(int tile) => Buildings[tile];
            public bool TryGetBuilding(int buildingId, out eStructs type, out int baseHeight)
            {
                bool found = Records.TryGetValue(buildingId, out var record);
                type = record.Type; baseHeight = record.Base;
                return found;
            }
        }

        // PlagueDamageModel (audit 02.11.2026): Vanilla's 120 height gate for neighbours and ring and its
        // wall/elevated/keep/gate/tower exclusions; with an APIShared policy the lanes and standing planes.
        private void PlagueDamageModelTests()
        {
            var terrain = new PlagueTerrainFixture();
            var model = new PlagueDamageModel(terrain);
            const int cloud = 1000, ground = 1001, steep = 1002, wall = 1003, tower = 1004, gate = 1005, raised = 1006, wallCloud = 1007;
            terrain.Heights[cloud] = 10; terrain.Heights[ground] = 10; terrain.Heights[steep] = 130;
            terrain.TileFlags[wall] = PlagueDamageModel.WallFlag; terrain.Heights[wall] = 10;
            terrain.TileFlags[wallCloud] = PlagueDamageModel.WallFlag; terrain.Heights[wallCloud] = 10;
            terrain.TileFlags[tower] = PlagueDamageModel.ElevatedFlag; terrain.Buildings[tower] = 5; terrain.Records[5] = (eStructs.STRUCT_TOWER2, 30);
            terrain.Buildings[gate] = 6; terrain.Records[6] = (eStructs.STRUCT_GATE_MAIN, 0); terrain.Defaults[gate] = 12;
            terrain.TileFlags[raised] = PlagueDamageModel.ElevatedFlag; terrain.Buildings[raised] = 7; terrain.Records[7] = (eStructs.STRUCT_NULL, 0);
            var vanilla = default(PlagueHeightRules);
            Check(model.WouldDamage(cloud, cloud, 0, vanilla) && model.WouldDamage(cloud, ground, 1, vanilla), "plague spares ground tiles");
            terrain.Heights[steep] = 129;
            Check(model.WouldDamage(cloud, steep, 2, vanilla), "plague spares a ring tile 119 units higher");
            terrain.Heights[steep] = 130;
            Check(!model.WouldDamage(cloud, steep, 2, vanilla) && model.WouldDamage(steep, steep, 0, vanilla), "Vanilla's 120 gate of neighbours and ring");
            Check(!model.WouldDamage(cloud, wall, 1, vanilla) && !model.WouldDamage(cloud, tower, 1, vanilla) && !model.WouldDamage(cloud, gate, 1, vanilla) &&
                !model.WouldDamage(cloud, raised, 1, vanilla), "Vanilla plague damages walls, towers, gates or elevated tiles");
            Check(model.Surface(gate) == 12 + 128 && model.Surface(tower) == 148 && PlagueDamageModel.Roof(eStructs.STRUCT_TOWER1) == 296,
                "plague surface heights differ from 0x6B910");
            Check(!model.WouldDamage(cloud, ground, 3, vanilla) && !model.WouldDamage(cloud, PlagueDamageModel.NativeTileCount, 0, vanilla), "plague damage outside its area");

            // Policy (ExtraFeatures: walls and towers on, gates and keeps off, tolerance 20).
            var policy = new PlagueHeightRules { Walls = 21, Towers = 21 };
            Check(model.WouldDamage(wallCloud, wall, 1, policy), "a plague cloud on a wall spares the wall");
            terrain.Heights[wall] = 31;
            Check(!model.WouldDamage(wallCloud, wall, 1, policy), "plague damages beyond the policy's tolerance");
            terrain.Heights[wall] = 30;
            Check(model.WouldDamage(wallCloud, wall, 1, policy), "plague spares a wall within the tolerance");
            Check(!model.WouldDamage(cloud, gate, 1, policy), "plague damages a gate whose lane is off");
            terrain.Heights[cloud] = 178; terrain.TileFlags[cloud] = 0;
            Check(model.WouldDamage(cloud, tower, 1, policy) && !model.WouldDamage(cloud, raised, 1, policy), "plague lanes for towers and other elevated tiles");
            terrain.Heights[cloud] = 10; terrain.Heights[ground] = 31;
            Check(!model.WouldDamage(cloud, ground, 1, policy), "plague damages ground beyond the largest tolerance");
            terrain.Heights[ground] = 30;
            Check(model.WouldDamage(cloud, ground, 1, policy), "ground tiles do not use the largest tolerance");
            Check(!model.WouldDamage(wallCloud, ground, 1, new PlagueHeightRules { Walls = 21 }) || terrain.Heights[ground] - terrain.Heights[wallCloud] <= 20,
                "a cloud on a wall damages the ground below beyond the tolerance");
            Check(model.IsRaised(wall) && model.IsRaised(tower) && model.IsRaised(gate) && !model.IsRaised(ground), "raised plague tiles");
        }

        // Plague escape on the anchor corridor (row 10): like fire, with the plague policy's windows; on a
        // wall only with EscapePlagueRaised; the lord is immune; a cloud an apothecary removes lets the
        // soldier return. A plague wait aborts like a fire wait.
        private void PlagueEscapeTests(ref int ticks)
        {
            GameUnitManagerAPI units = GameUnitManagerAPI.Instance;
            GameTribeManagerAPI tribes = GameTribeManagerAPI.Instance;
            GameUnit* walker = units.Units + 7;
            GameUnit saved = *walker;
            var api = GameTileManagerAPI.Instance;
            var availability = new byte[181];
            for (int x = 136; x <= 180; x++) { availability[x] = movementTargetAvailability[10 * MapWidth + x]; movementTargetAvailability[10 * MapWidth + x] = 1; }
            int now = ticks;
            int Next() { now += 8 - (now & 7); return now; }
            void Cloud(bool on) { for (int x = 148; x <= 152; x++) plagueTiles.Set(10 * MapWidth + x, on); for (int x = 147; x <= 153; x++) plagueNear.Set(10 * MapWidth + x, on); }
            void Stand(int x) => Route(7, x, 10, new int[0], x, 10);
            void Arrive(int x) { int tx = walker->r_TargetTilePositionX2, ty = walker->r_TargetTilePositionY2; Route(7, x, 10, new int[0], tx, ty); }
            var terrain = new PlagueTerrainFixture();
            GameTribe* table = InstallTribeTable(8);
            try
            {
                walker->r_GlobalId = 9007; walker->r_ControllableForPlayerId = 1; walker->r_AliveState = AliveState.IsAlive; walker->r_TribeId = 3;
                walker->r_IsKilledByProjectile = 0; walker->r_MovementSubstep = 0; walker->r_SelectionAllowed = 1; walker->r_UnitChimp = 0;
                plagueEscapeActive = true; plagueEscapePolicy = new APIShared.Pathfinding.RouteReplanPolicy(3, 50, 10, 400); plagueReturnTicks = 800;
                plagueModel = new PlagueDamageModel(terrain);
                ResetFireEscapes();

                // A cloud covers 148..152: the soldier at 150 leaves for 146 (147 is the cloud's margin).
                Stand(150); Cloud(true); tribes.Moves.Clear();
                long started = escapeStartedPlague;
                FireEscapeEntry entry = null;
                FireEscapeTick(Next());
                Check(escapeStartedPlague == started + 1 && tribes.Moves.Count == 1 && tribes.Moves[0][1] == 146 &&
                    fireEscapes.TryGetValue(7, out entry) && entry.Hazard == EscapeHazard.Plague,
                    $"a soldier in plague did not escape past the cloud's margin (to {(tribes.Moves.Count > 0 ? tribes.Moves[0][1] : -1)})");
                Arrive(146); FireEscapeTick(Next());
                Check(entry.Safe && entry.Until == now + 800, "the plague return window does not apply");
                // An apothecary removes the cloud: the next probe returns the soldier.
                Cloud(false);
                for (int i = 0; i < 8 && !entry.Returning; i++) FireEscapeTick(Next());
                Check(entry.Returning && tribes.Moves.Count == 2 && tribes.Moves[1][1] == 150, "a soldier did not return after the cloud was removed");
                Arrive(150); FireEscapeTick(Next());
                Check(!fireEscapes.ContainsKey(7) && walker->r_TribeId == 3, "a returned plague escape did not end");

                // On a wall (raised tile) only with EscapePlagueRaised; the lord is immune.
                int tile = api.GetTileId(150, 10);
                terrain.TileFlags[tile] = PlagueDamageModel.WallFlag;
                Stand(150); Cloud(true); tribes.Moves.Clear(); started = escapeStartedPlague;
                FireEscapeTick(Next());
                Check(escapeStartedPlague == started && tribes.Moves.Count == 0, "a wall garrison left plague without EscapePlagueRaised");
                plagueEscapeRaised = true;
                FireEscapeTick(Next());
                Check(escapeStartedPlague == started + 1 && tribes.Moves.Count == 1, "EscapePlagueRaised did not move the wall garrison");
                ResetFireEscapes(); terrain.TileFlags[tile] = 0; plagueEscapeRaised = false;
                if (walker->r_TribeId != 3) { int own = walker->r_TribeId; tribes.UnassignUnit(own, 7); tribes.DeleteTribeSafe(own); tribes.AssignUnit(3, 7); }
                walker->r_UnitChimp = eChimps.CHIMP_TYPE_LORD; Stand(150); tribes.Moves.Clear(); started = escapeStartedPlague;
                FireEscapeTick(Next());
                walker->r_UnitChimp = 0;
                Check(escapeStartedPlague == started && tribes.Moves.Count == 0, "the plague-immune lord fled");
            }
            finally
            {
                plagueTiles.Clear(); plagueNear.Clear(); ResetFireEscapes(); replanWaits.Clear();
                plagueEscapeActive = plagueEscapeRaised = false; plagueEscapePolicy = null; plagueModel = null; plagueReturnTicks = 180 * 40;
                for (int x = 136; x <= 180; x++) movementTargetAvailability[10 * MapWidth + x] = availability[x];
                RemoveTribeTable(table);
                *walker = saved; ticks = now;
            }
        }

        // Route cost rules (RouteSearchEvents) on the weighted moat field of a group move: an observer's
        // costs are used (the field is built and the change counted), a skip builds no field, and the
        // ground-only task is not published as a preference search.
        private int costRuleMode;
        private void CostRuleTests(int start, int anchor)
        {
            Check(APIShared.Pathfinding.RouteSearchEvents.TryRegister("tests.moatmove", "cost-rules", e =>
            {
                if (costRuleMode == 1) e.MoatEdgeCost = e.GroundEdgeCost * 3;
                else if (costRuleMode == 2) e.SkipOriginalFunction = true;
            }, null, out _), "cost rule registration failed");
            var spec = new AnchorTaskSpec { Player = 1, Anchor = anchor, Ground = 1, Moat = 7, Starts = new[] { start } };
            try
            {
                long changes = anchorCostRuleChanges, skips = anchorCostRuleSkips;
                costRuleMode = 1;
                int task = ComputeAnchorTaskWithCostRules(spec, false);
                Check(task != 0 && anchorCostRuleChanges == changes + 1, "an observer's moat cost did not reach the anchor field");
                ReleaseAnchorTask(task);
                costRuleMode = 2;
                Check(ComputeAnchorTaskWithCostRules(spec, false) == 0 && anchorCostRuleSkips == skips + 1, "a skipped weighted search still built a field");
                int groundTask = ComputeAnchorTaskWithCostRules(new AnchorTaskSpec { Player = 1, Anchor = anchor, Ground = 1, Moat = 0, Starts = new[] { start } }, true);
                Check(groundTask != 0 && anchorCostRuleSkips == skips + 1, "the ground-only task was published as a preference search");
                ReleaseAnchorTask(groundTask);
            }
            finally { costRuleMode = 0; }
        }

        // Which builder calls the core may answer (F4930's first search is a plain ground search): ground
        // mode, and Assassins only within one path region with +0x80 and +0x94 clear. Corridor regions:
        // x < 13 is region 1, x > 135 region 2.
        private void SingleSearchReplaceableTests()
        {
            var api = GameTileManagerAPI.Instance;
            int west = api.GetTileId(11, 10), east = api.GetTileId(140, 10), eastFar = api.GetTileId(170, 10);
            byte* manager = stackalloc byte[0x100];
            for (int i = 0; i < 0x100; i++) manager[i] = 0;
            Check(AnchorSingleSearchReplaceable(manager, west, east), "a ground-mode builder call is not replaceable");
            *(int*)(manager + 0x84) = 1;
            Check(!AnchorSingleSearchReplaceable(manager, east, eastFar), "Vanilla's moat mode was replaced");
            *(int*)(manager + 0x84) = 0; *(int*)(manager + 0x88) = 1;
            Check(AnchorSingleSearchReplaceable(manager, east, eastFar), "an Assassin request within one region is not replaceable");
            Check(!AnchorSingleSearchReplaceable(manager, west, east), "an Assassin request across regions (D9C40 first) was replaced");
            *(int*)(manager + 0x94) = 1;
            Check(!AnchorSingleSearchReplaceable(manager, east, eastFar), "an Assassin request with the DB650 second pass was replaced");
            *(int*)(manager + 0x94) = 0; *(int*)(manager + 0x80) = 1;
            Check(!AnchorSingleSearchReplaceable(manager, east, eastFar), "an Assassin request with +0x80 was replaced");
        }

        // MoatFailureProofs on a synthetic grid (offsets per row like DAFD0's table): a moat-mode failure whose
        // stamps form a closed box is captured after repeated failures in one tick; a covered request replays
        // F4930 up to DAAC0 (79C0, budget 2*(d+4)^2) and skips DAFD0; DAAC0 success restores +0xAC and declines.
        // Any change of a watched edge input (a tile beside the box, a special-structure state) drops the
        // proof; a change deep inside the box does not. Other players, modes, targets inside the box and
        // invalid targets keep Vanilla.
        private int moatDistanceCalls, moatSearchCalls, moatSearchResult, moatSearchBudget, moatDistanceValue = 6;
        private void MoatDistanceStub(IntPtr rectangle, int sx, int sy, int tx, int ty)
        { moatDistanceCalls++; *(int*)((byte*)rectangle + MoatFailureProofs.DistanceOffset) = moatDistanceValue; }
        private int MoatSearchStub(IntPtr manager, int sx, int sy, int tx, int ty, int budget, int mode)
        { moatSearchCalls++; moatSearchBudget = budget; return moatSearchResult; }
        private void MoatFailureProofTests()
        {
            const int width = 401, tiles = MoatFailureProofs.TileCount, generation = 91;
            var memory = new System.Collections.Generic.List<IntPtr>();
            T* Alloc<T>(int count) where T : unmanaged
            {
                IntPtr block = System.Runtime.InteropServices.Marshal.AllocHGlobal(count * sizeof(T)); memory.Add(block);
                for (int i = 0; i < count * sizeof(T); i++) ((byte*)block)[i] = 0;
                return (T*)block;
            }
            try
            {
                short* stamps = Alloc<short>(tiles); byte* valid = Alloc<byte>(800 * 800); byte* directions = Alloc<byte>(tiles);
                uint* flags = Alloc<uint>(tiles); byte* heights = Alloc<byte>(tiles); ushort* buildings = Alloc<ushort>(tiles);
                byte* typeWords = Alloc<byte>(0x32C * 8); short* special = Alloc<short>(tiles); byte* context = Alloc<byte>(0x9C * 8);
                ushort* rows = Alloc<ushort>(tiles); int* rowStarts = Alloc<int>(800 * 3); int* offsetBlock = Alloc<int>(800 * 8 + 8);
                int* offsets = offsetBlock + 8; byte* rectangle = Alloc<byte>(64);
                byte* manager = Alloc<byte>(MoatFailureProofs.RouteLengthOffset + 16);
                int[] dx = { 0, 1, 1, 1, 0, -1, -1, -1 }, dy = { -1, -1, 0, 1, 1, 1, 0, -1 };
                for (int r = -1; r < 800; r++) for (int d = 0; d < 8; d++) if (r * 8 + d >= -8) offsets[r * 8 + d] = dy[d] * width + dx[d];
                for (int t = 0; t < tiles; t++) rows[t] = (ushort)(t / width);
                for (int y = 0; y < 800; y++) rowStarts[y * 3] = y * width;
                for (int i = 0; i < 800 * 800; i++) valid[i] = 1;
                int Tile(int x, int y) => y * width + x;
                for (int y = 10; y <= 20; y++) for (int x = 10; x <= 20; x++) stamps[Tile(x, y)] = generation;
                *(int*)(manager + MoatFailureProofs.GenerationOffset) = generation;
                *(int*)(manager + MoatFailureProofs.MoatModeOffset) = 1;
                var proofs = new MoatFailureProofs(stamps, valid, directions, flags, heights, buildings, typeWords, special, context, rows, rowStarts,
                    offsets, (IntPtr)rectangle, MoatDistanceStub, MoatSearchStub);
                void Request(int sx, int sy, int tx, int ty)
                {
                    *(int*)(manager + MoatFailureProofs.StartXOffset) = sx; *(int*)(manager + MoatFailureProofs.StartYOffset) = sy;
                    *(int*)(manager + MoatFailureProofs.TargetXOffset) = tx; *(int*)(manager + MoatFailureProofs.TargetYOffset) = ty;
                }
                bool Answer(int player, out int result) => proofs.TryAnswer(manager, player, false, out result, out _, out _);
                void Capture(int tick)
                {
                    Request(12, 12, 100, 100);
                    for (int i = 0; i < MoatFailureProofs.CaptureAfterFailures && proofs.ProofCount == 0; i++) proofs.Observe(manager, 1, 0, false, 0, tick, null);
                }

                Request(12, 12, 100, 100);
                Check(!Answer(1, out _), "a moat-mode request is answered without any proof");
                if (MoatFailureProofs.CaptureAfterFailures > 1)
                {
                    proofs.Observe(manager, 1, 0, false, 0, 5, null);
                    Check(proofs.ProofCount == 0, "a moat proof is captured at a player's first failure");
                    proofs.Observe(manager, 1, 0, false, 0, 6, null);
                    Check(proofs.ProofCount == 0, "failures of different ticks add up to a capture");
                }
                Capture(7);
                Check(proofs.ProofCount == 1 && proofs.Captures == 1, "repeated failures in one tick do not capture the closed set");

                // A covered start: F4930's failure writes, 79C0, DAAC0 with 2*(d+4)^2, no DAFD0.
                Request(18, 15, 100, 100);
                *(int*)(manager + MoatFailureProofs.FailureOffset) = 5; *(int*)(manager + MoatFailureProofs.SearchFlagOffset) = 9;
                *(int*)(manager + MoatFailureProofs.RouteLengthOffset) = 9; moatDistanceCalls = moatSearchCalls = 0; moatSearchResult = 0;
                Check(Answer(1, out int known) && known == 0 && proofs.Hits == 1 && moatDistanceCalls == 1 && moatSearchCalls == 1 &&
                    moatSearchBudget == (moatDistanceValue + 4) * (moatDistanceValue + 4) * 2, "a covered moat-mode start is not replayed up to DAAC0");
                Check(*(int*)(manager + MoatFailureProofs.FailureOffset) == 6 && *(int*)(manager + MoatFailureProofs.SearchFlagOffset) == 0 &&
                    *(int*)(manager + MoatFailureProofs.RouteLengthOffset) == 0, "the replay does not leave F4930's moat-mode failure state");
                // DAAC0 finds a route: +0xAC as before, Vanilla's builder runs.
                moatSearchResult = 1;
                Check(!Answer(1, out _) && proofs.Fallbacks == 1 && *(int*)(manager + MoatFailureProofs.FailureOffset) == 6,
                    "a DAAC0 success is not handed back to Vanilla with +0xAC restored");
                moatSearchResult = 0;

                // Requests the proof does not cover.
                Request(30, 15, 100, 100); Check(!Answer(1, out _), "a start outside the moat proof is answered");
                Request(18, 15, 15, 15); Check(!Answer(1, out _), "a target inside the closed set is answered");
                Request(18, 15, 100, 100); Check(!Answer(2, out _), "another player's moat-mode request is answered");
                *(int*)(manager + MoatFailureProofs.MoatModeOffset) = 0; Check(!Answer(1, out _), "a ground-mode request is answered"); *(int*)(manager + MoatFailureProofs.MoatModeOffset) = 1;
                foreach (int mode in new[] { MoatFailureProofs.DirectModeOffset, MoatFailureProofs.AssassinModeOffset })
                { *(int*)(manager + mode) = 1; Check(!Answer(1, out _), "a request with +0x80 or +0x88 is answered"); *(int*)(manager + mode) = 0; }
                valid[100 * 800 + 100] = 0; Check(!Answer(1, out _), "a request with an invalid target is answered"); valid[100 * 800 + 100] = 1;

                // A change deep inside the box keeps the proof; one on a watched tile drops it.
                heights[Tile(15, 15)] = 40;
                Check(Answer(1, out _) && proofs.Hits == 2, "a change inside the closed set drops the moat proof");
                heights[Tile(21, 15)] = 40;
                Check(!Answer(1, out _) && proofs.Stale == 1 && proofs.ProofCount == 0, "a changed tile beside the closed set keeps the moat proof");
                special[Tile(21, 16)] = 3; *(ushort*)(context + 3 * 0x9C + 0x6A) = 4;
                Capture(8);
                Check(proofs.ProofCount == 1 && Answer(1, out _), "a proof with a special-structure tile is not used");
                *(ushort*)(context + 3 * 0x9C + 0x6A) = 7;
                Check(!Answer(1, out _) && proofs.Stale == 2, "a changed special-structure state keeps the moat proof");
                proofs.Reset();
                Check(proofs.ProofCount == 0, "a reset keeps moat proofs");
            }
            finally { foreach (IntPtr block in memory) System.Runtime.InteropServices.Marshal.FreeHGlobal(block); }
        }

        // Failed ground builds (GroundFailureProofs) on a synthetic 401 x 800 tile grid: a closed box of
        // stamped tiles around the start proves every outside target unreachable. Only a player's third
        // failure in the move captures; covered requests replay F4930's failure path (direct attempt, +0xAC, +0x7C, +0x155F68)
        // without DAAC0/DA590 for any target outside the box (members' different slots), with the straight
        // attempt of the request's profile; every other request, an open set, a region change, a target
        // inside the set, another player/mode, F4930's region gate (E2F60 once), a
        // builder success and a direct success keep or restore Vanilla.
        private int groundDirectCalls, groundDefaultCalls, groundDirectResult, groundDirectLength, groundRegionCalls, groundRegionResult = 1;
        private int GroundRegionStub(IntPtr manager, int player, int startRegion, int targetRegion) { groundRegionCalls++; return groundRegionResult; }
        private int GroundDefaultStub(IntPtr manager, int sx, int sy, int tx, int ty) { groundDefaultCalls++; return GroundDirectStub(manager, sx, sy, tx, ty) * 0; }
        private int GroundDirectStub(IntPtr manager, int sx, int sy, int tx, int ty)
        {
            groundDirectCalls++;
            if (groundDirectResult != 0) *(int*)((byte*)manager + GroundFailureProofs.RouteLengthOffset) = groundDirectLength;
            return groundDirectResult;
        }
        private void GroundFailureProofTests()
        {
            const int width = 401, tiles = CursorGroundRejectProof.TileCount, generation = 77;
            var memory = new System.Collections.Generic.List<IntPtr>();
            T* Alloc<T>(int count) where T : unmanaged
            {
                IntPtr block = System.Runtime.InteropServices.Marshal.AllocHGlobal(count * sizeof(T)); memory.Add(block);
                for (int i = 0; i < count * sizeof(T); i++) ((byte*)block)[i] = 0;
                return (T*)block;
            }
            try
            {
                short* stamps = Alloc<short>(tiles); byte* connections = Alloc<byte>(tiles); byte* bits = Alloc<byte>(8);
                int* offsets = Alloc<int>(800 * 8); ushort* rows = Alloc<ushort>(tiles); ushort* regions = Alloc<ushort>(tiles);
                byte* manager = Alloc<byte>(GroundFailureProofs.RouteLengthOffset + 16);
                int[] dx = { 0, 1, 1, 1, 0, -1, -1, -1 }, dy = { -1, -1, 0, 1, 1, 1, 0, -1 }, step = new int[8];
                for (int d = 0; d < 8; d++) step[d] = dy[d] * width + dx[d];
                for (int d = 0; d < 8; d++) bits[d] = (byte)(1 << d);
                for (int r = 0; r < 800; r++) for (int d = 0; d < 8; d++) offsets[r * 8 + d] = step[d];
                for (int t = 0; t < tiles; t++) { rows[t] = (ushort)(t / width); regions[t] = 1; }
                int Tile(int x, int y) => y * width + x;
                bool Inside(int x, int y) => x >= 10 && x <= 20 && y >= 10 && y <= 20;
                // Box 10..20 with edges only between box tiles, stamped like DA590's visited set.
                for (int y = 10; y <= 20; y++)
                    for (int x = 10; x <= 20; x++)
                    {
                        stamps[Tile(x, y)] = generation;
                        for (int d = 0; d < 8; d++)
                        {
                            if (Inside(x + dx[d], y + dy[d])) connections[Tile(x, y)] |= bits[d];
                        }
                    }
                *(int*)(manager + GroundFailureProofs.GenerationOffset) = generation;
                var proofs = new GroundFailureProofs(stamps, connections, bits, offsets, rows, regions, (x, y) => Tile(x, y), GroundDirectStub, GroundDefaultStub, GroundRegionStub);
                void Request(int sx, int sy, int tx, int ty)
                {
                    *(int*)(manager + GroundFailureProofs.StartXOffset) = sx; *(int*)(manager + GroundFailureProofs.StartYOffset) = sy;
                    *(int*)(manager + GroundFailureProofs.TargetXOffset) = tx; *(int*)(manager + GroundFailureProofs.TargetYOffset) = ty;
                }
                bool Answer(int player, int profile, out int result) =>
                    proofs.TryAnswer(manager, player, profile, false, out result, out _, out _);

                Request(12, 12, 100, 100);
                Check(!Answer(1, 0, out _), "a request is answered without any proof");
                proofs.Observe(manager, 1, 0, 0, false, default, null); proofs.Observe(manager, 1, 0, 0, false, default, null);
                Check(proofs.ProofCount == 0 && proofs.Captures == 0, "a proof is captured before a player's third failure");
                Request(13, 12, 105, 100);
                proofs.Observe(manager, 1, 0, 0, false, default, null);
                Check(proofs.ProofCount == 1 && proofs.Captures == 1, "a player's third failure (another slot) does not capture its closed set");

                // A covered start replays F4930's failure path with the native direct attempt only.
                Request(18, 15, 100, 100);
                *(int*)(manager + GroundFailureProofs.DirectFailureOffset) = 5; *(int*)(manager + GroundFailureProofs.SearchFlagOffset) = 9;
                *(int*)(manager + GroundFailureProofs.RouteLengthOffset) = 9; groundDirectCalls = 0; groundDirectResult = 0;
                Check(Answer(1, 0, out int known) && known == 0 && groundDirectCalls == 1 && proofs.Hits == 1, "a covered start is not answered as a failure");
                Check(*(int*)(manager + GroundFailureProofs.DirectFailureOffset) == 6 && *(int*)(manager + GroundFailureProofs.SearchFlagOffset) == 0 &&
                    *(int*)(manager + GroundFailureProofs.RouteLengthOffset) == 0, "the replay does not leave F4930's failure state");

                // Requests the proof does not cover stay with Vanilla.
                groundDirectCalls = 0;
                Request(30, 15, 100, 100); Check(!Answer(1, 0, out _), "a start outside the proof is answered");
                Request(18, 15, 15, 15); Check(!Answer(1, 0, out _), "a target inside the closed set is answered");
                Request(18, 15, 101, 100); groundDirectCalls = 0;
                Check(Answer(1, 0, out _) && groundDirectCalls == 1 && proofs.Hits == 2, "another slot outside the closed set is not answered");
                *(int*)(manager + GroundFailureProofs.DirectFailureOffset) -= 1; groundDirectCalls = 0;
                Request(18, 15, 100, 100); Check(!Answer(2, 0, out _), "another player's request is answered");
                groundDefaultCalls = 0;
                Check(Answer(1, 1, out _) && groundDefaultCalls == 1 && groundDirectCalls == 1, "a request with the F3060 profile does not use F3060");
                *(int*)(manager + GroundFailureProofs.DirectFailureOffset) -= 1; groundDirectCalls = 0; groundDefaultCalls = 0;
                foreach (int mode in new[] { GroundFailureProofs.MoatModeOffset, GroundFailureProofs.AssassinModeOffset })
                { *(int*)(manager + mode) = 1; Check(!Answer(1, 0, out _), "a request in another builder mode is answered"); *(int*)(manager + mode) = 0; }
                // +0x80 only drops DAAC0 from the same failure path (DA590 directly).
                *(int*)(manager + GroundFailureProofs.DirectModeOffset) = 1;
                Check(Answer(1, 0, out _) && groundDirectCalls == 1, "a covered request with +0x80 is not answered");
                *(int*)(manager + GroundFailureProofs.DirectModeOffset) = 0; *(int*)(manager + GroundFailureProofs.DirectFailureOffset) -= 1; groundDirectCalls = 0;
                Check(groundDirectCalls == 0 && groundRegionCalls == 0, "a declined request ran the direct attempt or the region gate");

                // Region gate: another start region asks E2F60 once; unconnected regions (or target region 0) end
                // F4930 without any write, connected ones take the search path.
                int failuresNow = *(int*)(manager + GroundFailureProofs.DirectFailureOffset);
                regions[Tile(18, 15)] = 2; groundRegionResult = 0;
                Check(Answer(1, 0, out int gated) && gated == 0 && groundRegionCalls == 1 && groundDirectCalls == 0 && proofs.GateHits == 1 &&
                    *(int*)(manager + GroundFailureProofs.DirectFailureOffset) == failuresNow, "unconnected regions do not end in F4930's region gate");
                groundRegionResult = 3;
                Check(Answer(1, 0, out _) && groundRegionCalls == 2 && groundDirectCalls == 1 &&
                    *(int*)(manager + GroundFailureProofs.DirectFailureOffset) == failuresNow + 1, "connected regions do not take F4930's search path");
                *(int*)(manager + GroundFailureProofs.DirectFailureOffset) = failuresNow; groundDirectCalls = 0;
                regions[Tile(100, 100)] = 0;
                Check(Answer(1, 0, out _) && groundRegionCalls == 2 && groundDirectCalls == 0 && proofs.GateHits == 2, "target region 0 asked E2F60");
                regions[Tile(100, 100)] = 1; regions[Tile(18, 15)] = 1; groundRegionResult = 1; groundRegionCalls = 0;

                // DEVDIAG verification: predicted only; Vanilla's terminal state confirms or counts a mismatch.
                int before = *(int*)(manager + GroundFailureProofs.DirectFailureOffset);
                *(int*)(manager + GroundFailureProofs.SearchFlagOffset) = 5; *(int*)(manager + GroundFailureProofs.RouteLengthOffset) = 6;
                Check(!proofs.TryAnswer(manager, 1, 0, true, out _, out bool predicted, out GroundFailureProofs.BuildState state) && predicted &&
                    state.Failures == before && state.Flag == 5 && state.Length == 6 && groundRegionCalls == 0, "verification mode skips Vanilla");
                proofs.Observe(manager, 1, 0, 0, true, state, null);
                Check(proofs.Verified == 1 && proofs.Mismatches == 0, "Vanilla's unwritten region-gate failure is not verified");
                *(int*)(manager + GroundFailureProofs.DirectFailureOffset) = before + 1;
                *(int*)(manager + GroundFailureProofs.SearchFlagOffset) = 0; *(int*)(manager + GroundFailureProofs.RouteLengthOffset) = 0;
                proofs.Observe(manager, 1, 0, 0, true, state, null);
                Check(proofs.Verified == 2 && proofs.Mismatches == 0, "a matching Vanilla search failure is not counted as verified");
                *(int*)(manager + GroundFailureProofs.SearchFlagOffset) = 1;
                proofs.Observe(manager, 1, 0, 0, true, state, null);
                Check(proofs.Verified == 2 && proofs.Mismatches == 1, "a failure with another terminal state is verified");
                proofs.Observe(manager, 1, 0, 4, true, state, null);
                Check(proofs.Mismatches == 2 && proofs.MismatchSample != null && proofs.ProofCount == 0, "a Vanilla success against a proof is not a mismatch or keeps the proof");

                // A direct success (never expected) is F4930's success path and drops the proofs.
                Request(12, 12, 100, 100);
                for (int i = 0; i < 3; i++) proofs.Observe(manager, 1, 0, 0, false, default, null);
                Request(18, 15, 100, 100);
                int successes = *(int*)(manager + GroundFailureProofs.DirectSuccessOffset);
                groundDirectResult = 1; groundDirectLength = 7;
                Check(Answer(1, 0, out int length) && length == 7 && *(int*)(manager + GroundFailureProofs.DirectSuccessOffset) == successes + 1 &&
                    proofs.DirectSuccesses == 1 && proofs.ProofCount == 0, "a direct success does not follow F4930's success path");
                groundDirectResult = 0;

                // An open set (an edge leaves the box) is never a proof; Reset drops proofs and counts.
                connections[Tile(20, 15)] |= bits[2];
                Request(12, 12, 100, 100);
                for (int i = 0; i < 3; i++) proofs.Observe(manager, 1, 0, 0, false, default, null);
                Check(proofs.ProofCount == 0 && proofs.CaptureRejects == 1, "an open set was captured as a proof");
                connections[Tile(20, 15)] &= unchecked((byte)~bits[2]);
                proofs.Observe(manager, 1, 0, 0, false, default, null);
                Check(proofs.ProofCount == 1, "a closed set after a rejected capture is not captured");
                proofs.Reset(); Request(18, 15, 100, 100);
                Check(proofs.ProofCount == 0 && !Answer(1, 0, out _), "Reset keeps a proof");
            }
            finally { foreach (IntPtr block in memory) System.Runtime.InteropServices.Marshal.FreeHGlobal(block); }
        }

        // Writes a stored route starting at (x,y) and the move target into unit id's record and buffer.
        private void Route(int id, int x, int y, int[] steps, int targetX, int targetY)
        {
            GameUnit* unit = GameUnitManagerAPI.Instance.Units + id;
            byte* path = nativeUnitManager + NativeUnitPathBufferOffset + id * NativeUnitPathBufferStride;
            for (int i = 0; i < NativeUnitPathBufferStride; i++) path[i] = 0;
            for (int i = 0; i < steps.Length; i++) path[i >> 1] |= (byte)(steps[i] << ((i & 1) * 4));
            unit->r_PreviousTilePositionX = unit->r_NextTilePositionX2 = unit->r_CurrentTilePositionX = x;
            unit->r_PreviousTilePositionY = unit->r_NextTilePositionY2 = unit->r_CurrentTilePositionY = y;
            unit->r_CurrentPathPlanIndex = 0; unit->r_PathPlanLength = steps.Length; unit->r_PathPlanStateBitFlags = steps.Length > 0 ? 2 : 0;
            unit->r_TargetTilePositionX2 = targetX; unit->r_TargetTilePositionY2 = targetY;
        }

        private void AnchorIntegrationTests()
        {
            string directory = Environment.GetEnvironmentVariable("MOAT_TEST_ANCHOR_DIR");
            if (string.IsNullOrEmpty(directory) || !File.Exists(Path.Combine(directory, AnchorNative.Library)))
                throw new FileNotFoundException("Anchor core required: run native/Build-MoatMoveAnchor.ps1 first.", directory);
            var api = GameTileManagerAPI.Instance;
            string directoryBefore = settings.PluginDirectory; int modeBefore = settings.RouteMode;
            // Anchor mode runs on Fast's RequiredOnly flow only.
            settings.PluginDirectory = directory; settings.RouteMode = 1;
            try
            {
                // Same corridor as PlacementTests: ground 10..12, completed moat 13..135, ground 136..180.
                for (int x = 10; x <= 180; x++)
                {
                    int tile = api.GetTileId(x, 10);
                    tileFlags[tile] = x >= 13 && x <= 135 ? CompletedMoatTileFlag : 0x8000;
                    nativeMovementMasks[tile] = 0x44; nativeHeightLayer[tile] = 0; nativeBuildingLayer[tile] = 0;
                    pathRegionGrid[tile] = (ushort)(x < 13 ? 1 : x <= 135 ? 0 : 2);
                }
                nativeMovementMasks[api.GetTileId(10, 10)] = 0x04; nativeMovementMasks[api.GetTileId(180, 10)] = 0x40;
                cursorTopologies.Clear(); placementRevision++; MarkFastTopologyAll(); tick++;
                EnsureCursorTopology(1);

                // Ground shortcut: away from moats one live evaluation decides both policies. Words
                // equal a cache that evaluates both policies for every edge (moat corridor, its rims
                // and diagonal corners).
                var bothPolicies = new FastTraversalCache(MapWidth, MapWidth,
                    (int a, int b, int d, bool g, out bool w, out bool s) => AnchorLiveEdge(1, a, b, d, g, out w, out s));
                var shortcut = new FastTraversalCache(MapWidth, MapWidth,
                    (int a, int b, int d, bool g, out bool w, out bool s) => AnchorLiveEdge(1, a, b, d, g, out w, out s), GroundEqualsFriendly);
                for (int y = 8; y <= 12; y++)
                    for (int x = 8; x <= 182; x++)
                        Check(bothPolicies.Word(y * MapWidth + x) == shortcut.Word(y * MapWidth + x), $"ground shortcut changes the word of {x},{y}");
                Check(shortcut.EdgeEvaluations < bothPolicies.EdgeEvaluations, "ground shortcut saves no live evaluation");

                // Shared words: player-neutral nodes are read once for all players; near a gate closed to
                // one player and along the moat every player keeps its own word. Words equal unshared
                // per-player caches.
                int gateTile = api.GetTileId(60, 10), savedLayer = nativeBuildingLayer[gateTile];
                nativeBuildingLayer[gateTile] = 4242;
                anchorBlocked[2] = new System.Collections.Generic.HashSet<int> { 4242 }; anchorBlockedUnion.Clear(); anchorBlockedUnion.Add(4242);
                var sharedWords = new FastSharedWords(MapWidth * MapWidth);
                FastTraversalCache PlayerCache(int player, bool share) => new FastTraversalCache(MapWidth, MapWidth,
                    (int a, int b, int d, bool g, out bool w, out bool s) => AnchorLiveEdge(player, a, b, d, g, out w, out s), GroundEqualsFriendly,
                    share ? sharedWords : null, share ? AnchorPlayerNeutral : (FastPlayerNeutral)null);
                FastTraversalCache shared1 = PlayerCache(1, true), shared2 = PlayerCache(2, true), own1 = PlayerCache(1, false), own2 = PlayerCache(2, false);
                Check(own1.Word(10 * MapWidth + 59) != own2.Word(10 * MapWidth + 59), "the closed test gate does not differ between players");
                for (int y = 8; y <= 12; y++)
                    for (int x = 8; x <= 182; x++)
                    {
                        int node = y * MapWidth + x;
                        Check(shared1.Word(node) == own1.Word(node) && shared2.Word(node) == own2.Word(node), $"shared words differ from a player's own word at {x},{y}");
                    }
                Check(sharedWords.Reuses > 0, "no player-neutral word was shared");
                nativeBuildingLayer[gateTile] = (ushort)savedLayer; anchorBlocked[2] = null; anchorBlockedUnion.Clear();

                InitializeAnchor();
                Check(anchorAvailable, "anchor core does not load from the plugin directory");

                // Reachability words (anchor mode): every corridor edge answers exactly like the live
                // FriendlyOnly predicate VisitNeighbour used before (allowed, moat kind, structure).
                FastTraversalCache reach = ReachabilityWords(1);
                Check(reach != null, "anchor mode provides no reachability words");
                for (int y = 9; y <= 11; y++)
                    for (int x = 9; x <= 181; x++)
                        for (int d = 0; d < 8; d++)
                        {
                            int nx = x + WeightedMoatRoutePlanner.DirectionX[d], ny = y + WeightedMoatRoutePlanner.DirectionY[d];
                            bool live = weightedMoatRoutePlanner.TryGetTraversalEdge(1, x, y, api.GetTileId(x, y), nx, ny, api.GetTileId(nx, ny), d,
                                false, false, MoatTraversalPolicy.FriendlyOnly, out MoatTraversalEdgeKind kind, out bool liveStructure);
                            bool cached = reach.Edge(y * MapWidth + x, ny * MapWidth + nx, d, false, out bool wet, out bool structure);
                            Check(cached == live && (!live || wet == (kind != MoatTraversalEdgeKind.Ground) && structure == liveStructure),
                                $"reachability word differs from the live predicate at {x},{y} direction {d}");
                        }
                // Seeded from the anchor mask: the same answers, also next to a gate the mask closes.
                nativeBuildingLayer[gateTile] = 4242;
                anchorBlocked[2] = new System.Collections.Generic.HashSet<int> { 4242 };
                anchorMaps[2] = PlayerCache(2, false);
                for (int y = 8; y <= 12; y++) for (int x = 8; x <= 182; x++) anchorMaps[2].Word(y * MapWidth + x);
                // A pending resweep round has not re-read these words yet: they are not seeded.
                ResweepAnchorMasks(); reachWords[2] = null;
                FastTraversalCache pendingReach = ReachabilityWords(2);
                pendingReach.Edge(10 * MapWidth + 20, 10 * MapWidth + 21, 2, false, out _, out _);
                Check(pendingReach.Seeded == 0, "a word not yet re-read by a pending resweep round was seeded");
                // The round finished: every clean anchor word is current.
                anchorSweepCursor = -1; anchorSweepLeft = 0; reachWords[2] = null;
                FastTraversalCache seededReach = ReachabilityWords(2);
                for (int y = 9; y <= 11; y++)
                    for (int x = 9; x <= 181; x++)
                        for (int d = 0; d < 8; d++)
                        {
                            int nx = x + WeightedMoatRoutePlanner.DirectionX[d], ny = y + WeightedMoatRoutePlanner.DirectionY[d];
                            bool live = weightedMoatRoutePlanner.TryGetTraversalEdge(2, x, y, api.GetTileId(x, y), nx, ny, api.GetTileId(nx, ny), d,
                                false, false, MoatTraversalPolicy.FriendlyOnly, out _, out _);
                            Check(seededReach.Edge(y * MapWidth + x, ny * MapWidth + nx, d, false, out _, out _) == live,
                                $"seeded reachability word differs from the live predicate at {x},{y} direction {d}");
                        }
                Check(seededReach.Seeded > 0, "no reachability word was taken from the anchor mask");
                nativeBuildingLayer[gateTile] = (ushort)savedLayer; anchorBlocked[2] = null; anchorMaps[2] = null; reachWords[2] = null;
                ResetAnchorMap();
                // The scheduler scans unit owners on the first tick and every 256 ticks.
                Check(AnchorPlayerScanDue(1), "first anchor tick does not scan unit owners");
                RequestAnchorMasksForPlayers(1 << 1);
                Check(!AnchorPlayerScanDue(2) && AnchorPlayerScanDue(256), "unit owners are not rescanned every 256 ticks");
                // The map's first scan builds its masks completely in one tick (no Vanilla moves while pending).
                int total = MapWidth * MapWidth, ticks = 0;
                AnchorTick(++ticks);
                Check(AnchorReady(1), "the map's first scan did not build its mask in one tick");
                for (int p = 2; p <= 8; p++) Check(anchorMaskNext[p] == -1, "mask started for a player without units");

                // Edge memo of one group order (AnchorEdgeMemo): a repeated edge answers like LiveEdge without a
                // second check; outside an order, after a reported map change, a topology mark or in the next
                // order the edge is checked live again.
                {
                    int memoFrom = 10 * MapWidth + 10, memoDir = -1, memoTo = 0;
                    for (int d = 0; d < 8 && memoDir < 0; d++)
                    {
                        int candidate = memoFrom + WeightedMoatRoutePlanner.DirectionY[d] * MapWidth + WeightedMoatRoutePlanner.DirectionX[d];
                        if (AnchorLiveEdge(1, memoFrom, candidate, d, true, out _, out _)) { memoDir = d; memoTo = candidate; }
                    }
                    Check(memoDir >= 0, "no live ground edge at the memo test node");
                    AnchorLiveEdge(1, memoFrom, memoTo, memoDir, true, out bool liveWet, out bool liveStructure);
                    long memoHits = edgeMemoHits;
                    bool Memo(out bool memoWetOut, out bool memoStructureOut) => AnchorLiveEdgeMemo(1, memoFrom, memoTo, memoDir, true, out memoWetOut, out memoStructureOut);
                    Check(Memo(out _, out _) && Memo(out _, out _) && edgeMemoHits == memoHits, "edge memo used outside a group order");
                    BeginAnchorFrame(77, 0);
                    Check(Memo(out bool firstWet, out bool firstStructure) && edgeMemoHits == memoHits &&
                        firstWet == liveWet && firstStructure == liveStructure, "first check of an order is not live");
                    Check(Memo(out bool memoWet, out bool memoStructure) && edgeMemoHits == memoHits + 1 && memoWet == liveWet && memoStructure == liveStructure,
                        "a repeated edge of an order is not answered from the memo");
                    placementRevision++;
                    Check(Memo(out _, out _) && edgeMemoHits == memoHits + 1, "a reported map change keeps the memo");
                    InvalidateEdgeMemo();
                    Check(Memo(out _, out _) && edgeMemoHits == memoHits + 1, "a topology mark keeps the memo");
                    Check(Memo(out _, out _) && edgeMemoHits == memoHits + 2, "the memo is not refilled after a mark");
                    EndAnchorFrame(77);
                    BeginAnchorFrame(78, 0);
                    Check(Memo(out _, out _) && edgeMemoHits == memoHits + 2, "the next order reuses the previous order's memo");
                    EndAnchorFrame(78);
                }

                int start = 10 * MapWidth + 10, anchor = 10 * MapWidth + 150;
                long tasksBefore = anchorTasks;
                // Profile 8/56 reduces to 1/7; the group's task is ready when the call returns.
                int task = TryComputeAnchorTask(1, anchor, 8, 56, new[] { start }, 1);
                Check(task != 0 && AnchorNative.mma_task_status(anchorContext, task) == AnchorNative.Ready && anchorTasks == tasksBefore + 1,
                    "a group move's task is not computed synchronously");
                Check(TryComputeAnchorTask(1, anchor, 8, -1, new[] { start }, 1) == 0 && TryComputeAnchorTask(1, anchor, 1, 7, new int[0], 0) == 0,
                    "an invalid profile or empty group creates a task");
                anchorDispatchTask = task;
                var plan = new PlanScope(1, 150, 10) { PlayerId = 1 };
                long routes = anchorMoatRoutes;
                Check(TryAnchorRequiredRoute(plan, false, 10, 10, out _, out bool moat, task) && moat &&
                    plan.QualifiedRoute != null && anchorMoatRoutes == routes + 1, "moat route not published through the live audit");
                // Publication skips its second per-edge check only for routes audited in this tick.
                Check(IsAnchorAuditedRoute(plan.QualifiedRoute) && !IsAnchorAuditedRoute(new QualifiedMovementRoute(10, 10, 150, 10, 1,
                    mapEpoch, CaptureCurrentGameTick(), placementRevision, plan.QualifiedRoute.Route, plan.QualifiedRoute.Summary, default, false)),
                    "only the audited route object is marked for publication");
                QualifiedMovementRoute audited = plan.QualifiedRoute;

                // Phase 4 A: the mode probe qualifies the core's moat route. B: a ground decision
                // carries the core's ground route to the builder instead of Vanilla's search.
                var digger = new GameUnit { Digger = true, r_GlobalId = 901, r_ControllableForPlayerId = 1, r_AliveState = AliveState.IsAlive,
                    r_CurrentTilePositionX = 10, r_CurrentTilePositionY = 10, r_NextTilePositionX2 = 10, r_NextTilePositionY2 = 10, r_MovementSubstep = 8 };
                var walker = digger; walker.r_GlobalId = 902;
                walker.r_CurrentTilePositionX = walker.r_NextTilePositionX2 = 137;
                var probe = new PlanScope(1, 150, 10);
                Check(TryAnchorQualifiesMoat(probe, &digger) && probe.QualifiedRoute != null && probe.QualifiedRoute.Summary.MoatEdges > 0,
                    "mode probe ignores the core's moat route");
                Check(!TryPrepareAnchorGround(new PlanScope(1, 150, 10), &digger), "builder publishes a moat answer as ground route");
                int groundTask = TryComputeAnchorTask(1, anchor, 1, 7, new[] { 10 * MapWidth + 137 }, 1);
                Check(groundTask != 0, "ground task not computed");
                anchorDispatchTask = groundTask;
                var groundPlan = new PlanScope(1, 150, 10);
                Check(!TryAnchorQualifiesMoat(new PlanScope(1, 150, 10), &walker), "ground decision qualifies as moat");
                long groundRoutes = anchorGroundRoutes;
                Check(TryPrepareAnchorGround(groundPlan, &walker) && groundPlan.QualifiedRoute != null &&
                    groundPlan.QualifiedRoute.Summary.MoatEdges == 0 && anchorGroundRoutes == groundRoutes + 1, "ground route not prepared for the builder");
                // The builder reuses the mode probe's ground decision instead of a second query.
                // Reuse binds the plan to the live unit slot, so the walker takes slot 2 here.
                GameUnit* pool = GameUnitManagerAPI.Instance.Units;
                pool[2] = walker;
                var reusePlan = new PlanScope(2, 150, 10);
                long reuses = anchorRouteReuses, queries = anchorQueries;
                Check(!TryAnchorQualifiesMoat(reusePlan, pool + 2) && reusePlan.QualifiedRoute != null && TryPrepareAnchorGround(reusePlan, pool + 2) &&
                    anchorRouteReuses == reuses + 1 && anchorQueries == queries + 1, "builder repeats the mode probe's ground query");
                pool[2] = default;
                Check(!TryPrepareAnchorGround(new PlanScope(1, 150, 10) { ExactRouteEndpoints = true }, &walker),
                    "intermediate builder target uses the anchor route");
                Check(!TryPrepareAnchorGround(new PlanScope(1, 150, 10) { ModeObserved = true, VanillaModeDetected = true }, &walker),
                    "Vanilla's own moat mode replaced by a ground route");
                anchorDispatchTask = 0;
                Check(!TryPrepareAnchorGround(new PlanScope(1, 150, 10), &walker) && !TryAnchorQualifiesMoat(new PlanScope(1, 150, 10), &digger),
                    "anchor answers outside an anchor dispatch");
                ReleaseAnchorTask(groundTask);

                // Units that cannot enter moats use the group's ground-only task (moat cost 0):
                // the corridor through the moat is closed for them, its ground part stays open.
                var rider = walker; rider.Digger = false; rider.r_GlobalId = 903;
                var blockedRider = digger; blockedRider.Digger = false; blockedRider.r_GlobalId = 904;
                int onlyGround = TryComputeAnchorTask(1, anchor, 1, 0, new[] { 10 * MapWidth + 137, start }, 2);
                Check(onlyGround != 0, "ground-only task not computed");
                anchorDispatchTask = 0; anchorGroundTask = onlyGround;
                var riderPlan = new PlanScope(1, 150, 10);
                Check(TryPrepareAnchorGround(riderPlan, &rider) && riderPlan.QualifiedRoute != null &&
                    riderPlan.QualifiedRoute.Summary.MoatEdges == 0, "non-digger receives no ground route");
                Check(!TryPrepareAnchorGround(new PlanScope(1, 150, 10), &blockedRider), "ground-only task routes a non-digger through the moat");
                Check(!TryPrepareAnchorGround(new PlanScope(1, 150, 10), &walker), "a digger uses the ground-only task");
                anchorGroundTask = 0; ReleaseAnchorTask(onlyGround);

                // Outside group moves the anchor mode answers nothing and runs no synchronous Fast
                // field (combat resume, AI orders before their mask is ready keep Vanilla).
                var units = GameUnitManagerAPI.Instance.Units;
                units[1] = new GameUnit { Digger = true, r_GlobalId = 905, r_TribeId = 1, r_ControllableForPlayerId = 1, r_AliveState = AliveState.IsAlive,
                    r_CurrentTilePositionX = 10, r_CurrentTilePositionY = 10, r_NextTilePositionX2 = 10, r_NextTilePositionY2 = 10,
                    r_CurrentPositionTileId = (uint)api.GetTileId(10, 10), r_MovementSubstep = 8 };
                anchorDispatchTask = 0;
                long skips = fastAnchorSkips, builds = GetFastRouting(false).Pool.Builds;
                Check(!TryFindFastRequiredRoute(new PlanScope(1, 150, 10), false, true, out _) && fastAnchorSkips == skips + 1 &&
                    GetFastRouting(false).Pool.Builds == builds, "anchor mode runs a synchronous Fast field outside group moves");
                anchorDispatchTask = task;

                // Blocking a ground tile of the route is mirrored into the mask; the task is stale.
                int blocked = api.GetTileId(140, 10);
                nativeMovementMasks[blocked] = 0; MarkFastTopologyTile(blocked); AnchorTick(++ticks);
                Check(AnchorNative.mma_task_status(anchorContext, task) == AnchorNative.Stale, "terrain change leaves the task fresh");
                Check(!IsAnchorAuditedRoute(audited), "an audit mark survives its tick");
                Check(!TryAnchorRequiredRoute(new PlanScope(1, 150, 10) { PlayerId = 1 }, false, 10, 10, out _, out _, task),
                    "stale task still answers");
                nativeMovementMasks[blocked] = 0x44; MarkFastTopologyTile(blocked); AnchorTick(++ticks);
                ReleaseAnchorTask(task); anchorDispatchTask = 0;

                // Group move frames: a nested move uses only its own tasks (none hides the outer
                // ones), its end restores the outer tasks, and a move without Post (skipped
                // original) ends with the enclosing move or at the next tick.
                int outer = TryComputeAnchorTask(1, anchor, 1, 7, new[] { start }, 1);
                int outerGround = TryComputeAnchorTask(1, anchor, 1, 0, new[] { 10 * MapWidth + 137 }, 1);
                int inner = TryComputeAnchorTask(1, anchor, 1, 7, new[] { 10 * MapWidth + 137 }, 1);
                BeginAnchorFrame(7, outer, outerGround); BeginAnchorFrame(8, inner);
                Check(anchorDispatchTask == inner && anchorGroundTask == 0, "nested move does not use only its own tasks");
                BeginAnchorFrame(9, 0);
                Check(anchorDispatchTask == 0, "a nested move without task uses an enclosing move's task");
                EndAnchorFrame(8);
                Check(anchorDispatchTask == outer && anchorGroundTask == outerGround && !anchorLiveTasks.Contains(inner) && anchorFrames.Count == 1,
                    "nested move end does not restore the outer tasks");
                BeginAnchorFrame(10, 0);
                AnchorTick(++ticks);
                Check(anchorFrames.Count == 0 && anchorDispatchTask == 0 && anchorGroundTask == 0 &&
                    !anchorLiveTasks.Contains(outer) && !anchorLiveTasks.Contains(outerGround), "open move frames survive the tick");

                // Members a group move never re-planned are counted with a sample of their ids.
                int counted = TryComputeAnchorTask(1, anchor, 1, 7, new[] { start }, 1);
                long unreachedBefore = anchorUnreachedUnits;
                BeginAnchorFrame(11, counted, 0, new[] { 5, 6, 7 });
                MarkAnchorUnit(5); MarkAnchorUnit(7);
                EndAnchorFrame(11);
                Check(anchorEndedUnreached == 1 && anchorEndedMembers == 3 && anchorUnreachedSample[0] == 6 &&
                    anchorUnreachedUnits == unreachedBefore + 1, "a member without re-plan is not counted");
                anchorEndedUnreached = 0;

                // Pending tasks: a group move captures only its starts; its first AnchorGroupSingles
                // queries get exact single routes, the field is computed at the next query, and never
                // for a move whose members Vanilla does not re-plan.
                long tasksBeforeLazy = anchorTasks, unused = anchorUnusedMoves;
                var spec = new AnchorTaskSpec { Player = 1, Anchor = anchor, Ground = 1, Moat = 7, Starts = new[] { start } };
                BeginAnchorFrame(13, 0, 0, null, spec);
                Check(anchorDispatchTask == AnchorPendingTask && anchorTasks == tasksBeforeLazy, "a group move computes its field before any unit asks");
                EndAnchorFrame(13);
                Check(anchorTasks == tasksBeforeLazy && anchorUnusedMoves == unused + 1 && anchorDispatchTask == 0, "an unused group move computes a field");
                BeginAnchorFrame(14, 0, 0, null, spec);
                long groupSingles = anchorGroupSingleRoutes;
                for (int i = 0; i < AnchorGroupSingles; i++)
                    Check(TryAnchorQualifiesMoat(new PlanScope(1, 150, 10), &digger), "a group single route loses the moat decision");
                Check(anchorTasks == tasksBeforeLazy && anchorDispatchTask == AnchorPendingTask && anchorGroupSingleRoutes == groupSingles + AnchorGroupSingles,
                    "the first queries of a group move do not use exact single routes");
                Check(TryAnchorQualifiesMoat(new PlanScope(1, 150, 10), &digger) && anchorTasks == tasksBeforeLazy + 1 && anchorDispatchTask > 0,
                    "the query after the single routes does not compute the pending field");
                int lazyTask = anchorDispatchTask;
                Check(TryAnchorQualifiesMoat(new PlanScope(1, 150, 10), &digger) && anchorTasks == tasksBeforeLazy + 1, "a later query recomputes the field");
                EndAnchorFrame(14);
                Check(!anchorLiveTasks.Contains(lazyTask) && anchorDispatchTask == 0, "a computed pending task survives its move");

                // Formation plans: BugfixesAndQoL's targets reach the move frame, the first query answers
                // all planned members in one batch, and a member's answer equals its single query.
                long batchHits = anchorBatchHits, batchUnits = anchorBatchUnits;
                GameUnit* members = GameUnitManagerAPI.Instance.Units;
                members[3] = digger; members[3].r_GlobalId = 906;
                BeginAnchorFrame(15, 0, 0, new[] { 3 }, spec);
                ObserveAnchorFormation(15, new[] { 3 }, new uint[] { 906 }, new[] { 150 }, new[] { 10 });
                var batchPlan = new PlanScope(3, 150, 10);
                Check(TryAnchorQualifiesMoat(batchPlan, members + 3) && anchorBatchUnits == batchUnits + 1 && anchorBatchHits == batchHits + 1,
                    "a planned member is not answered from the move's batch");
                anchorBatchIndex.Clear();
                var singlePlan = new PlanScope(3, 150, 10);
                Check(TryAnchorQualifiesMoat(singlePlan, members + 3) && anchorBatchHits == batchHits + 1, "a member without batch entry is not asked singly");
                byte[] batchBytes = batchPlan.QualifiedRoute.Route.Bytes, singleBytes = singlePlan.QualifiedRoute.Route.Bytes;
                bool sameBytes = batchBytes.Length == singleBytes.Length;
                for (int i = 0; sameBytes && i < batchBytes.Length; i++) sameBytes = batchBytes[i] == singleBytes[i];
                Check(sameBytes, "batch and single answers differ");
                EndAnchorFrame(15); members[3] = default;
                // A plan for another tribe never attaches to this move.
                BeginAnchorFrame(16, 0, 0, new[] { 3 }, spec);
                ObserveAnchorFormation(17, new[] { 3 }, new uint[] { 906 }, new[] { 150 }, new[] { 10 });
                Check(anchorFrames[anchorFrames.Count - 1].Targets == null, "a formation plan attaches to another tribe's move");
                EndAnchorFrame(16); AnchorTick(++ticks);

                // A failed live audit re-reads only the stale area (no full resweep); the next tick
                // mirrors the change and the task reports stale.
                int auditTask = TryComputeAnchorTask(1, anchor, 1, 7, new[] { start }, 1);
                long resweeps = anchorResweeps, rejects = anchorEncodeRejects;
                nativeMovementMasks[blocked] = 0;
                Check(!TryAnchorRequiredRoute(new PlanScope(1, 150, 10) { PlayerId = 1 }, false, 10, 10, out _, out _, auditTask) &&
                    anchorEncodeRejects == rejects + 1, "a route over an unreported terrain change passes the live audit");
                AnchorTick(++ticks);
                Check(AnchorNative.mma_task_status(anchorContext, auditTask) == AnchorNative.Stale && anchorResweeps == resweeps && anchorSweepCursor == -1,
                    "a failed audit does not refresh just its stale area");
                nativeMovementMasks[blocked] = 0x44; MarkAnchorArea(10 * MapWidth + 140); AnchorTick(++ticks);
                ReleaseAnchorTask(auditTask);

                // BFQoL's tile hooks report the changed tile: only its area is re-read (no
                // resweep). A nested unit move (terrain: false) leaves the masks untouched.
                int tileTask = TryComputeAnchorTask(1, anchor, 1, 7, new[] { start }, 1);
                long tileResweeps = anchorResweeps;
                nativeMovementMasks[blocked] = 0; InvalidateMovementSearchData(blocked); AnchorTick(++ticks);
                Check(AnchorNative.mma_task_status(anchorContext, tileTask) == AnchorNative.Stale && anchorResweeps == tileResweeps && anchorSweepCursor == -1,
                    "a reported tile change does not refresh just its area");
                nativeMovementMasks[blocked] = 0x44; InvalidateMovementSearchData(terrain: false); AnchorTick(++ticks);
                Check(anchorResweeps == tileResweeps, "a nested unit move requests a terrain resweep");
                InvalidateMovementSearchData(blocked); AnchorTick(++ticks);
                ReleaseAnchorTask(tileTask);

                // A direction byte changed without any report (gates, neighbouring buildings): the
                // native byte watch marks it within one watch round; no resweep.
                int watchTask = TryComputeAnchorTask(1, anchor, 1, 7, new[] { start }, 1);
                long watchResweeps = anchorResweeps;
                nativeMovementMasks[blocked] = 0;
                int watchTicks = (MapWidth * MapWidth + AnchorWatchNodesPerTick - 1) / AnchorWatchNodesPerTick;
                for (int i = 0; i < watchTicks + 1; i++) AnchorTick(++ticks);
                Check(AnchorNative.mma_task_status(anchorContext, watchTask) == AnchorNative.Stale &&
                    anchorResweeps == watchResweeps, "an unreported direction byte change is not mirrored into the mask");
                nativeMovementMasks[blocked] = 0x44;
                for (int i = 0; i < watchTicks + 1; i++) AnchorTick(++ticks);
                ReleaseAnchorTask(watchTask);

                // APIShared route tile rules: a Forbidden tile loses its incoming edges for the selected
                // players only (the anchor mask follows within one tick); clearing the rule restores them.
                Check(APIShared.Pathfinding.RouteTileRules.TryRegister("tests.moatmove", "forbidden", out APIShared.Pathfinding.RouteTileRuleSet tileRules, out _),
                    "tile rule registration failed");
                int ruleFrom = 10 * MapWidth + 149, ruleTo = 10 * MapWidth + 150;
                Check(LiveEdge(1, ruleFrom, ruleTo, 2, false, null, out _, out _), "the corridor edge is closed before the rule");
                Check(tileRules.Set(150, 10, APIShared.Pathfinding.RouteTileEffect.Forbidden, 0, 1U << 1), "rule rejected");
                AnchorTick(++ticks);
                Check(!LiveEdge(1, ruleFrom, ruleTo, 2, false, null, out _, out _) && LiveEdge(2, ruleFrom, ruleTo, 2, false, null, out _, out _),
                    "a Forbidden tile rule does not close the edge for exactly its players");
                Check((anchorMaps[1].Word(ruleFrom) & (1U << 2)) == 0, "the anchor mask keeps an edge into a Forbidden tile");
                Check(LiveEdge(1, ruleTo, ruleFrom, 6, false, null, out _, out _), "a unit on a Forbidden tile cannot leave it");
                tileRules.ClearAll(); AnchorTick(++ticks);
                Check((anchorMaps[1].Word(ruleFrom) & (1U << 2)) != 0, "clearing the tile rule does not restore the edge");
                // A Penalty rule reaches the core: the player's tasks become stale, also when it is removed.
                int penaltyTask = TryComputeAnchorTask(1, anchor, 1, 7, new[] { start }, 1);
                Check(tileRules.Set(150, 10, APIShared.Pathfinding.RouteTileEffect.Penalty, 30, 1U << 1), "penalty rule rejected");
                AnchorTick(++ticks);
                Check(AnchorNative.mma_task_status(anchorContext, penaltyTask) == AnchorNative.Stale, "a Penalty rule does not reach the core");
                ReleaseAnchorTask(penaltyTask);
                penaltyTask = TryComputeAnchorTask(1, anchor, 1, 7, new[] { start }, 1);
                tileRules.ClearAll(); AnchorTick(++ticks);
                Check(AnchorNative.mma_task_status(anchorContext, penaltyTask) == AnchorNative.Stale, "a removed Penalty rule stays in the core");
                ReleaseAnchorTask(penaltyTask);
                ReplanWatchTests(ref ticks);
                FireEscapeTests(ref ticks);
                PlagueDamageModelTests();
                PlagueEscapeTests(ref ticks);
                SingleSearchReplaceableTests();
                GroundFailureProofTests();
                MoatFailureProofTests();
                CostRuleTests(start, anchor);

                // The word-based reachability search (cached words, node tile table) visits the same
                // cells with the same distances and summary as the per-neighbour search.
                Check(AnchorNodeTiles() != null, "the byte watch provides no node tile table after a full round");
                string ReachabilitySnapshot(bool disable)
                {
                    DisableWordReachability = disable;
                    try
                    {
                        EnsureReachabilityMap(1, 10, 10);
                        AdvanceReachabilityMap();
                        var text = new System.Text.StringBuilder();
                        for (int cell = 0; cell < MapWidth * MapWidth; cell++)
                        {
                            if (visitedWithoutMoat[cell] == gridGeneration) text.Append('g').Append(cell).Append(':').Append(distanceWithoutMoat[cell]).Append(' ');
                            if (visitedWithMoat[cell] == gridGeneration) text.Append('m').Append(cell).Append(':').Append(distanceWithMoat[cell]).Append(' ');
                        }
                        RouteProbeSummary summary = cachedRouteSummary;
                        return text.Append("expanded=").Append(cachedReachabilityExpandedNodes).Append(" moatTiles=").Append(summary.FriendlyMoatTiles)
                            .Append(" structural=").Append(summary.StructuralEdgesObserved).Append(" regions=").Append(cachedTraversedRegionCount).ToString();
                    }
                    finally { DisableWordReachability = false; }
                }
                string slowSearch = ReachabilitySnapshot(true), fastSearch = ReachabilitySnapshot(false);
                Check(slowSearch.Split(' ').Length > 100 && fastSearch == slowSearch, "the word-based reachability search differs from the per-neighbour search");
                // An exhausted search answers later starts it visited: a target it never reached is
                // unreachable without a second exhaustive search; a reachable target is still searched;
                // a word change at a visited cell discards it.
                EnsureReachabilityMap(1, 10, 10); AdvanceReachabilityMap();
                long hits = ExhaustedReachabilityHits;
                EnsureReachabilityMap(1, 11, 10, deferTraversal: true);
                AdvanceReachabilityMap(targetCell: 10 * MapWidth + 190);
                Check(ExhaustedReachabilityHits == hits + 1 && reachabilityQueueHead < reachabilityQueueTail && visitedWithoutMoat[10 * MapWidth + 190] != gridGeneration,
                    "an unreachable target is searched again from a start the exhausted search visited");
                AdvanceReachabilityMap(targetCell: 10 * MapWidth + 150);
                Check(ExhaustedReachabilityHits == hits + 1 && (visitedWithMoat[10 * MapWidth + 150] == gridGeneration || visitedWithoutMoat[10 * MapWidth + 150] == gridGeneration),
                    "a reachable target was answered unreachable from the exhausted search");
                int visitedTile = api.GetTileId(170, 10);
                nativeMovementMasks[visitedTile] = 0x40; MarkFastTopologyTile(visitedTile); AdvanceFastMarkEpoch(); RefreshReachabilityWords(1);
                Check(exhaustedReachability[1] == null, "a word change at a visited cell keeps the exhausted search");
                nativeMovementMasks[visitedTile] = 0x44; MarkFastTopologyTile(visitedTile); AdvanceFastMarkEpoch(); RefreshReachabilityWords(1);

                // A resweep request during a running round extends it from the cursor instead of
                // restarting at node 0, so frequent requests still reach every node.
                ResweepAnchorMasks(); AnchorTick(++ticks); AnchorTick(++ticks);
                int cursor = anchorSweepCursor;
                Check(cursor == 2 * AnchorResweepNodesPerTick, $"resweep cursor {cursor} after two ticks");
                ResweepAnchorMasks();
                Check(anchorSweepCursor == cursor && anchorSweepLeft == total, "a resweep request restarts the running round");
                for (int i = 0; i < (total + AnchorResweepNodesPerTick - 1) / AnchorResweepNodesPerTick; i++) AnchorTick(++ticks);
                Check(anchorSweepCursor == -1 && anchorSweepLeft == 0, "an extended resweep round does not finish after one full map");

                // Gate changes: an unchanged access snapshot requests nothing; a changed portal entry
                // without a building marks nothing and needs no resweep; one whose building footprint
                // is unknown falls back to a resweep, as does a changed portal table size.
                int* portal = (int*)nativePathManager;
                int savedCount = portal[0], gate = 0x81, savedState = portal[gate + 0x809], savedBuilding = portal[gate + 0x80C], savedOpen = portal[gate + 0x80F];
                AnchorTick(++ticks); long gateSweeps = anchorResweeps;
                AnchorTick(++ticks);
                Check(anchorResweeps == gateSweeps, "an unchanged access snapshot requests a resweep");
                portal[0] = 2; AnchorTick(++ticks);
                Check(anchorResweeps == gateSweeps + (savedCount == 2 ? 0 : 1), "a changed portal table size does not resweep");
                gateSweeps = anchorResweeps;
                portal[gate + 0x80F] = savedOpen == 0 ? 1 : 0; portal[gate + 0x80C] = 0; AnchorTick(++ticks);
                Check(anchorResweeps == gateSweeps, "a changed portal without building forces a resweep");
                portal[gate + 0x809] = 1; portal[gate + 0x80C] = 9999; AnchorTick(++ticks);
                Check(anchorResweeps == gateSweeps + 1, "a gate with unknown footprint does not fall back to a resweep");
                portal[0] = savedCount; portal[gate + 0x809] = savedState; portal[gate + 0x80C] = savedBuilding; portal[gate + 0x80F] = savedOpen;
                AnchorTick(++ticks);
                for (int i = 0; i < (total + AnchorResweepNodesPerTick - 1) / AnchorResweepNodesPerTick * 2; i++) AnchorTick(++ticks);

                // Steady state: no terrain change, no task work, not a player-request tick.
                AnchorTick(++ticks);
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 8; i++) AnchorTick(++ticks);
                Check((ticks & ~255) == ((ticks - 8) & ~255), "allocation window crossed a player-request tick");
                Check(GC.GetAllocatedBytesForCurrentThread() == allocated, "steady anchor tick allocates");
                RequestAnchorMask(3);
                Check(anchorMaskNext[3] == 0, "a player's move order does not start that player's mask");
                // Later requests keep the per-tick budget.
                AnchorTick(++ticks);
                Check(anchorMaskNext[3] == Math.Min(AnchorMaskNodesPerTick, total), "a later mask request is not built within the per-tick budget");
            }
            finally
            {
                ResetAnchorMap(); anchorAvailable = false;
                settings.PluginDirectory = directoryBefore; settings.RouteMode = modeBefore;
            }
        }
    }
}
