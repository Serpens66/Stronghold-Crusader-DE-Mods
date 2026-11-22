using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using APIShared;
using APIShared.Automation;
using APIShared.Pathfinding;
using APIShared.Simulation;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace PathfindingHazardTest
{
    // Self-generated hazard acceptance: one Skirmish, a flat open area near the own keep, fresh soldiers per
    // case and real fire (native 0x9C120 through the Extender wrapper) and plague clouds (0x9B2B0 through the
    // Extender's CreateProjectile with the arguments Vanilla's natural plague 0xD17D0 passes). Pathfinding's
    // gated hazard options are enabled only on a private copy of its mission options (Bridge lease).
    // Unity runs the steps; native objects are touched under InSimulation or on the process-lifetime
    // SimulationTickEvents callback (order 700, after Pathfinding's scheduler at 600). Nothing is saved.
    internal sealed unsafe class Fixture
    {
        // Geometry around the area centre (cx, cy): wait actors start StartOffset tiles left of the barrier
        // column and walk to TargetOffset tiles right of it; the barrier spans BarrierHalf rows each way, so
        // every way around is far beyond the leased 50 % / 10 tiles detour rule. Box: the verified area.
        private const int StartOffset = 14, TargetOffset = 8, BarrierHalf = 21, BoxLeft = 18, BoxRight = 12, BoxHalf = 25;
        private const int MaximumCaseTicks = 12000, PlagueCloudSpacing = 3;
        private static readonly int[] Dx = { 0, 1, 1, 1, 0, -1, -1, -1 }, Dy = { -1, -1, 0, 1, 1, 1, 0, -1 };
        private readonly string map;
        private readonly HazardCase[] cases;
        private readonly int waitSeconds;
        private readonly Action<string> log;
        private readonly ConcurrentQueue<string> messages = new ConcurrentQueue<string>();
        private readonly List<Actor> actors = new List<Actor>();
        private IMissionLifecycleCapability missions;
        private long session;
        private int owner, aiOwner, cx, cy, caseStart, lastTick;
        private volatile Action queuedCommand;
        private volatile bool commandCompleted, observing, caseDone, stopped;
        private volatile string commandError, error;
        private HazardCase current;
        private Bridge bridge;
        private MethodInfo spawnFire;

        private sealed class Actor
        {
            internal int Id, Owner, Tribe, OriginX, OriginY, TargetX, TargetY, WaitStarted, WaitUntil, StopX, StopY;
            internal uint Global, TribeGlobal;
            internal eChimps Type;
            internal bool Ai, Control, Borrowed, SawCase, SawSafe, SawReturning, Exposed, Waited, PastDeadline, Arrived;
        }

        internal Fixture(string map, HazardCase[] cases, int waitSeconds, Action<string> log)
        { this.map = map; this.cases = cases; this.waitSeconds = waitSeconds; this.log = log; }

        internal void Stop() { stopped = true; observing = false; queuedCommand = null; }

        internal IEnumerable<AutomationStep> Steps()
        {
            yield return new AutomationStep("skirmish", TimeSpan.FromSeconds(240), Start);
            yield return new AutomationStep("prepare-area", TimeSpan.FromSeconds(180), Prepare, true);
            foreach (HazardCase item in cases)
            {
                HazardCase captured = item;
                yield return new AutomationStep(item.Name, TimeSpan.FromSeconds(300), run => RunCase(run, captured), true);
            }
        }

        private IEnumerable<AutomationWait> Start(AutomationRun run)
        {
            var plugin = BepInEx.Bootstrap.Chainloader.PluginInfos["Pathfinding_Serp"].Instance;
            object settings = plugin.GetType().GetProperty("Settings").GetValue(plugin);
            // Fixed values: the runner starts with the user's personal settings. The hazard options stay off here;
            // only the private option lease of each case enables them.
            SetSetting(settings, "EnableMod", true); SetSetting(settings, "EnablePathfindingCore", true);
            SetSetting(settings, "AnchorSingleMoves", true); SetSetting(settings, "EnableFriendlyMoatTraversal", false);
            SetSetting(settings, "AvoidFire", false); SetSetting(settings, "AvoidPlague", false);
            SetSetting(settings, "EnableMoveFormationEnhancements", false);
            // Quiet test world: random events (fires, wolves ...) would disturb the hazard fixtures. The runner
            // restores the settings files after the run.
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("RandomEvents_Serp", out var randomEvents))
                SetSetting(randomEvents.Instance.GetType().GetProperty("Settings").GetValue(randomEvents.Instance), "EnableMod", false);
            // Vanilla plague lifetime (800 handler ticks): a cloud then drifts at most 26 * 1/8 tile, less than the
            // 4 tiles between the barrier's damage edge and the stop tile. A longer ExtraFeatures lifetime (user
            // profile x4) let a cloud reach and kill the waiting soldier whose escape the wait case disables
            // (matrix run b4953049 on 22.11.2026).
            if (BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("ExtraFeatures_Serp", out var extraFeatures))
                SetSetting(extraFeatures.Instance.GetType().GetProperty("Settings").GetValue(extraFeatures.Instance), "PlagueDurationMultiplier", 1.0);
            foreach (var wait in run.StartSkirmish(map, new[] { AILords.SK_RAT }, peaceTimeMinutes: 60)) yield return wait;
        }

        private IEnumerable<AutomationWait> Prepare(AutomationRun run)
        {
            yield return run.WaitForPlayState(run.CurrentMission);
            if (run.CurrentMission.Mode.IsRealMultiplayer) throw new InvalidOperationException("Hazard driver is SP only.");
            if (MainViewModel.Instance.Show_HUD_Briefing) MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
            yield return AutomationWait.Until("briefing closed", () => !MainViewModel.Instance.Show_HUD_Briefing);
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 16);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            session = run.CurrentMission.SessionId;
            if (!ApiShared.ForMod(Plugin.Guid).TryGetMissionLifecycle(out missions, out var diagnostic))
                throw new InvalidOperationException(diagnostic.Reason);
            Director.instance.SetEngineFrameRate(250);
            spawnFire = typeof(GameUnitManagerAPI).Assembly.GetType("SHCDESE.Detours.BulkProjectileDetours", true)
                .GetMethod("c_game_spawn_fire_internal_hook_impl", BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(int), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int) }, null);
            Require(spawnFire != null && spawnFire.ReturnType == typeof(int), "Installed SE fire wrapper ABI changed.");
            Execute(run, () => {
                owner = GamePlayerManagerAPI.Instance.GetLocalPlayerId();
                Require(owner >= 1 && owner <= 8 && !GamePlayerManagerAPI.Instance.IsAIPlayer(owner), "No human local player.");
                aiOwner = 0;
                for (int p = 1; p <= 8 && aiOwner == 0; p++)
                    if (p != owner && GamePlayerManagerAPI.Instance.IsPlayerIdValid(p) && GamePlayerManagerAPI.Instance.IsAIPlayer(p)) aiOwner = p;
                Require(aiOwner != 0, "No AI opponent.");
            });
            var keep = run.InSimulation(run.CurrentMission, () => GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(owner));
            bool found = false;
            for (int distance = 20; distance <= 90 && !found; distance += 5)
                foreach (int[] offset in new[] { new[] { distance, 0 }, new[] { -distance, 0 }, new[] { 0, distance }, new[] { 0, -distance },
                    new[] { distance, distance }, new[] { -distance, -distance }, new[] { distance, -distance }, new[] { -distance, distance } })
                {
                    int x = keep.X + offset[0], y = keep.Y + offset[1];
                    if (run.InSimulation(run.CurrentMission, () => OpenArea(x, y))) { cx = x; cy = y; found = true; break; }
                    yield return AutomationWait.NextFrame;
                }
            Require(found, "No flat open " + (BoxLeft + BoxRight + 1) + "x" + (2 * BoxHalf + 1) + " area of one path component near the own keep.");
            bridge = null;
            if (!SimulationTickEvents.TryRegister(Plugin.Guid, "hazard-observer", Tick, out string reason, 700))
                throw new InvalidOperationException(reason);
            log("ready map=" + map + " owner=" + owner + " ai=" + aiOwner + " keep=" + keep.X + "," + keep.Y + " area=" + cx + "," + cy +
                " session=" + session + " cases=" + string.Join(",", cases.Select(c => c.Name)) + " waitSeconds=" + waitSeconds);
        }

        // Every tile of the box inside the map, walkable, unoccupied, no wall or building, within 8 height
        // units of the centre and in the centre's path component.
        private bool OpenArea(int x, int y)
        {
            var tiles = GameTileManagerAPI.Instance;
            if (!tiles.IsTileInsideMapBounds(x, y)) return false;
            int centre = tiles.GetTileId(x, y);
            if (!tiles.IsValidTileId(centre)) return false;
            var components = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            int component = components[centre], height = tiles.GetTileHeight(centre);
            for (int yy = y - BoxHalf; yy <= y + BoxHalf; yy++)
                for (int xx = x - BoxLeft; xx <= x + BoxRight; xx++)
                {
                    if (!tiles.IsTileInsideMapBounds(xx, yy)) return false;
                    int tile = tiles.GetTileId(xx, yy);
                    if (!tiles.IsValidTileId(tile) || !tiles.IsTileWalkableAndUnoccupied(tile) || tiles.GetChimpLayer()[tile] != 0 ||
                        components[tile] != component || Math.Abs(tiles.GetTileHeight(tile) - height) > 8 ||
                        (tiles.GetTilePropertyFlag(tile) & (TilePropertyFlag.IsWall | TilePropertyFlag.IsBuilding | TilePropertyFlag.IsElevated)) != 0)
                        return false;
                }
            return true;
        }

        private IEnumerable<AutomationWait> RunCase(AutomationRun run, HazardCase item)
        {
            var mission = run.CurrentMission;
            Require(mission.SessionId == session, "Mission changed.");
            foreach (var wait in run.SetPause(mission, false)) yield return wait;
            // Fire or clouds of the previous case still burn natively (the restored options stop the scan).
            yield return AutomationWait.Until("no native fire or plague record left", () => run.InSimulation(mission, () => NativeHazards() == 0));
            current = item; caseDone = false; error = null;
            if (item.Kind == CaseKind.ImmuneCrossing)
            {
                // A spawned CHIMP_TYPE_HEALER is the apothecary's civilian, not a soldier, so the immune actor is a
                // lord: the player's own lord walks to the start tile first (hazard options still off); without one
                // (test AI.map has none for the human) SpawnActors spawns a lord, and no second lord is ever spawned.
                Execute(run, StageLord);
                yield return AutomationWait.Until("lord at the crossing start", () => run.InSimulation(mission, LordStaged));
            }
            Execute(run, () => SpawnActors(item));
            yield return run.WaitForTicks(mission, 16);
            yield return AutomationWait.Until("live actors and owner route mask", () => run.InSimulation(mission, ActorsReady));
            foreach (var wait in OnSimulationTick(run, () => Activate(item))) yield return wait;
            if (item.Escape)
            {
                Execute(run, () => SpawnAtActors(item));
                observing = true;
                // The fire wrapper may jitter its target: refill until the scanner holds the actor's tile.
                for (int attempt = 0; ; attempt++)
                {
                    yield return run.WaitForTicks(mission, 8);
                    if (run.InSimulation(mission, ActorEndangered)) break;
                    Require(attempt < 4, "The actor's tile never became a witnessed hazard.");
                    Execute(run, () => SpawnAtActors(item));
                }
            }
            else
            {
                foreach (var wait in OnSimulationTick(run, OrderActors)) yield return wait;
                yield return AutomationWait.Until("actors walking", () => run.InSimulation(mission, AllWalking));
                observing = true;
                Execute(run, () => SpawnBarrier(item, false));
                for (int attempt = 0; ; attempt++)
                {
                    yield return run.WaitForTicks(mission, 8);
                    string state = run.InSimulation(mission, BarrierState);
                    Drain();
                    if (state == null) break;
                    Require(attempt < 4, "Barrier not closed after refills: " + state);
                    log("barrier refill attempt=" + (attempt + 1) + " " + state);
                    Execute(run, () => SpawnBarrier(item, true));
                }
                log("barrier closed case=" + item.Name);
            }
            yield return AutomationWait.Until("observed " + item.Name, () => {
                Drain();
                if (error != null) throw new InvalidOperationException(error);
                return caseDone;
            });
            Drain();
            foreach (var wait in OnSimulationTick(run, EndCase)) yield return wait;
            Drain();
            log("PASS case=" + item.Name + " cleanup=restored");
        }

        // ---- Fixture construction (native, under InSimulation or on the tick) ----

        private void SpawnActors(HazardCase item)
        {
            actors.Clear();
            switch (item.Kind)
            {
                case CaseKind.Escape:
                    actors.Add(Spawn(owner, eChimps.CHIMP_TYPE_SPEARMAN, cx, cy, cx, cy, false));
                    // AI soldiers keep Vanilla's control: one stands in the same fire and must get no stop or escape.
                    if (!item.Plague) actors.Add(Spawn(aiOwner, eChimps.CHIMP_TYPE_SPEARMAN, cx + 1, cy, cx + 1, cy, true));
                    break;
                case CaseKind.ImmuneCrossing:
                    if (lord == null)
                    {
                        lord = Spawn(owner, eChimps.CHIMP_TYPE_LORD, cx - StartOffset, cy - 2, cx + TargetOffset, cy - 2, false);
                        // Never kill what the game registered as the player's lord (defeat); it stays after the case.
                        lord.Borrowed = GamePlayerManagerAPI.Instance.GetLordUnitId(owner) == lord.Id;
                    }
                    actors.Add(lord);
                    actors.Add(Spawn(owner, eChimps.CHIMP_TYPE_SPEARMAN, cx - StartOffset, cy + 2, cx + TargetOffset, cy + 2, false, control: true));
                    break;
                default:
                    actors.Add(Spawn(owner, eChimps.CHIMP_TYPE_SPEARMAN, cx - StartOffset, cy, cx + TargetOffset, cy, false));
                    break;
            }
            Emit("actors " + string.Join(" ", actors.Select(a => a.Type + ":" + a.Id + "/" + a.Global + "@" + a.OriginX + "," + a.OriginY +
                "->" + a.TargetX + "," + a.TargetY + " owner=" + a.Owner + " tribe=" + a.Tribe + (a.Ai ? " ai" : "") + (a.Control ? " control" : ""))));
        }

        // A fresh soldier in a one-man group of its own owner (a single-member tribe's slot is the commanded tile).
        private Actor Spawn(int player, eChimps type, int x, int y, int targetX, int targetY, bool ai, bool control = false)
        {
            var tiles = GameTileManagerAPI.Instance;
            int height = tiles.GetTileHeight(tiles.GetTileId(x, y));
            long created = GameUnitManagerAPI.Instance.CreateUnitLocal(player, player, x, y, height, type);
            GameUnit* unit = null;
            Require(created > 0 && created <= int.MaxValue && UnitAccess.TryGetById((int)created, out unit, out _) && unit != null,
                "Unit spawn failed: " + type);
            int id = (int)created;
            Require(unit->r_UnitChimp == type && unit->r_CurrentTilePositionX == x && unit->r_CurrentTilePositionY == y, "Spawn projected or wrong type.");
            var actor = new Actor { Id = id, Global = unit->r_GlobalId, Owner = player, Type = type, OriginX = x, OriginY = y,
                TargetX = targetX, TargetY = targetY, Ai = ai, Control = control };
            if (!ai) AssignOwnTribe(actor, unit);
            return actor;
        }

        // ---- Plague-immune lord (0x32BC40: among controllable soldiers only CHIMP_TYPE_LORD is immune) ----

        private Actor lord;
        private void StageLord()
        {
            lord = null;
            int id = GamePlayerManagerAPI.Instance.GetLordUnitId(owner);
            if (id == 0) return;
            GameUnit* unit = null;
            bool found = id > 0 && UnitAccess.TryGetById(id, out unit, out _) && unit != null;
            Require(found && unit->r_UnitChimp == eChimps.CHIMP_TYPE_LORD && unit->r_GlobalId == (uint)GamePlayerManagerAPI.Instance.GetLordUnitGlobalId(owner) &&
                unit->r_ControllableForPlayerId == owner && UnitAccess.IsReallyAlive(unit), "The own lord is not a live controllable lord: id=" + id + " found=" + found +
                (found ? " type=" + unit->r_UnitChimp + " global=" + unit->r_GlobalId + "/" + GamePlayerManagerAPI.Instance.GetLordUnitGlobalId(owner) +
                    " controllable=" + unit->r_ControllableForPlayerId + " alive=" + UnitAccess.IsReallyAlive(unit) + " tribe=" + unit->r_TribeId : ""));
            lord = new Actor { Id = id, Global = unit->r_GlobalId, Owner = owner, Type = eChimps.CHIMP_TYPE_LORD, OriginX = cx - StartOffset, OriginY = cy - 2,
                TargetX = cx + TargetOffset, TargetY = cy - 2, Borrowed = true };
            AssignOwnTribe(lord, unit);
            Require(GameTribeManagerAPI.Instance.IssueMoveHereCommand(lord.Tribe, lord.OriginX, lord.OriginY, false, 1, TribeMoveType.DefaultInSync),
                "Lord staging move rejected.");
        }

        private bool LordStaged()
        {
            if (lord == null) return true;
            GameUnit* unit = ActorUnit(lord);
            return !Walking(unit) && unit->r_CurrentTilePositionX == lord.OriginX && unit->r_CurrentTilePositionY == lord.OriginY;
        }

        private void AssignOwnTribe(Actor actor, GameUnit* unit)
        {
            int tribe = checked((int)GameTribeManagerAPI.Instance.Create(actor.Owner, false));
            Require(tribe > 0, "Group creation failed.");
            if (unit->r_TribeId > 0) GameTribeManagerAPI.Instance.UnassignUnit(unit->r_TribeId, actor.Id);
            Require(GameTribeManagerAPI.Instance.AssignUnit(tribe, actor.Id), "Group assignment failed.");
            Require(GameTribeManagerAPI.Instance.TryGetTribeById(tribe, out GameTribe* group) && group != null, "Group missing.");
            actor.Tribe = tribe; actor.TribeGlobal = group->r_GlobalId;
        }

        private bool ActorsReady()
        {
            foreach (Actor actor in actors) if (!UnitAccess.IsReallyAlive(ActorUnit(actor))) return false;
            // Military updates set r_SelectionAllowed (Pathfinding's soldier filter) on their first ticks.
            foreach (Actor actor in actors) if (ActorUnit(actor)->r_SelectionAllowed == 0) return false;
            var progress = Field<int[]>(Provider(), "anchorMaskNext");
            Require(progress[owner] != -2, "Owner route mask build failed.");
            return progress[owner] >= RouteTileRules.MapSize * RouteTileRules.MapSize;
        }

        private void Activate(HazardCase item)
        {
            Require(missions.Current?.SessionId == session, "Activation mission mismatch.");
            bridge = new Bridge();
            bridge.Activate(item, waitSeconds);
            caseStart = lastTick;
            Emit("activated case=" + item.Name + " tick=" + lastTick + " (simulation callback; setup is not a PASS)");
        }

        private void OrderActors()
        {
            foreach (Actor actor in actors)
                Require(GameTribeManagerAPI.Instance.IssueMoveHereCommand(actor.Tribe, actor.TargetX, actor.TargetY, false, 1, TribeMoveType.DefaultInSync),
                    "Move command rejected for " + actor.Type + ".");
            Emit("ordered tick=" + lastTick);
        }

        private bool AllWalking() => actors.All(actor => Walking(ActorUnit(actor)));

        private bool ActorEndangered() => bridge.Hazard(current.Plague, actors[0].OriginX, actors[0].OriginY) || bridge.Case("fireEscapes", actors[0].Id) != null;

        private void SpawnAtActors(HazardCase item)
        {
            Actor actor = actors[0];
            int made = 0;
            if (item.Plague)
            {
                // Vanilla's natural plague puts 6..10 clouds on one tile; 8 here.
                for (int i = 0; i < 8; i++) if (SpawnCloud(actor.OriginX, actor.OriginY)) made++;
            }
            else
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++) if (SpawnFire(actor.OriginX + dx, actor.OriginY + dy)) made++;
            Require(made > 0, "No hazard record spawned.");
            Emit("hazard at actor spawned=" + made + " tick=" + lastTick);
        }

        // Fire: a three-tile band on the barrier column; plague: one cloud every PlagueCloudSpacing rows (5 x 5
        // damage area each). A refill only adds hazard on column tiles the production set does not hold.
        private void SpawnBarrier(HazardCase item, bool refill)
        {
            int made = 0;
            if (item.Plague)
            {
                for (int y = cy - BarrierHalf; y <= cy + BarrierHalf; y += refill ? 1 : PlagueCloudSpacing)
                    if ((!refill || !bridge.Hazard(true, cx, y)) && SpawnCloud(cx, y)) made++;
            }
            else
                for (int y = cy - BarrierHalf; y <= cy + BarrierHalf; y++)
                    for (int x = cx - 1; x <= cx + 1; x++)
                        if ((!refill || !bridge.Hazard(false, x, y)) && SpawnFire(x, y)) made++;
            Emit("barrier " + (refill ? "refill" : "spawn") + "=" + made + " tick=" + lastTick);
        }

        // Native 0x9C120 through the Extender wrapper (its Pre/Post run): world coordinates are tile*8 + 4; the
        // spread argument 2 adds no type-0x1A record (UiAcceptance runs of 09.11.2026). Its target may jitter.
        private bool SpawnFire(int x, int y)
        {
            var tiles = GameTileManagerAPI.Instance;
            int height = tiles.GetTileHeight(tiles.GetTileId(x, y));
            return (int)spawnFire.Invoke(null, new object[] { owner, x * 8 + 4, y * 8 + 4, height, 2, 0 }) > 0;
        }

        // 0x9B2B0 with exactly the arguments of Vanilla's natural plague 0xD17D0 (audit 22.11.2026):
        // (manager, 0, 0, 0, x*8, y*8, raw height, 0, 0, 0, 0x16, 0). The record must be type 22.
        private bool SpawnCloud(int x, int y)
        {
            var tiles = GameTileManagerAPI.Instance;
            int height = tiles.GetTileHeight(tiles.GetTileId(x, y));
            long id = GameProjectileManagerAPI.Instance.CreateProjectile(0, 0, x * 8, y * 8, height, 0, 0, 0, ProjectileType.Disease, 0);
            if (id <= 0 || id >= EffectSlots) return false;
            byte* record = Effects() + id * EffectStride;
            Require(*(ushort*)(record + EffectType) == (ushort)ProjectileType.Disease, "Created projectile " + id + " is no Disease record in Pathfinding's bound manager.");
            return true;
        }

        // Null when the barrier is closed: no hazard-free way from the walker to its target inside the box
        // within the detour rule (independent BFS over the native direction grid and the production set).
        private string BarrierState()
        {
            foreach (Actor actor in actors)
            {
                if (actor.Type == eChimps.CHIMP_TYPE_LORD) continue; // the immune lord is meant to cross
                GameUnit* unit = ActorUnit(actor);
                int x = unit->r_CurrentTilePositionX, y = unit->r_CurrentTilePositionY;
                int straight = Math.Max(Math.Abs(actor.TargetX - x), Math.Abs(actor.TargetY - y));
                int allowed = Math.Max(straight * 3 / 2, straight + 10), found = Distance(x, y, actor.TargetX, actor.TargetY);
                if (found >= 0 && found <= allowed) return "actor=" + actor.Id + " at=" + x + "," + y + " clearWay=" + found + " allowed=" + allowed;
            }
            return null;
        }

        private int Distance(int sx, int sy, int tx, int ty)
        {
            int minX = cx - BoxLeft, minY = cy - BoxHalf, width = BoxLeft + BoxRight + 1, height = 2 * BoxHalf + 1;
            if (sx < minX || sy < minY || sx >= minX + width || sy >= minY + height) return -1;
            var distance = Enumerable.Repeat(-1, width * height).ToArray();
            var queue = new Queue<int>();
            int start = (sy - minY) * width + sx - minX, target = (ty - minY) * width + tx - minX;
            distance[start] = 0; queue.Enqueue(start);
            while (queue.Count != 0)
            {
                int a = queue.Dequeue(), x = minX + a % width, y = minY + a / width;
                if (a == target) return distance[a];
                for (int d = 0; d < 8; d++)
                {
                    int nx = x + Dx[d], ny = y + Dy[d];
                    if (nx < minX || nx >= minX + width || ny < minY || ny >= minY + height ||
                        !GamePathingManagerAPI.Instance.HasPathEdge(x, y, (PackedPathDirection)d)) continue;
                    int next = (ny - minY) * width + nx - minX;
                    if (distance[next] >= 0 || bridge.Hazard(current.Plague, nx, ny)) continue;
                    distance[next] = distance[a] + 1; queue.Enqueue(next);
                }
            }
            return -1;
        }

        // ---- Observation on the simulation tick ----

        private IEnumerable<AutomationWait> OnSimulationTick(AutomationRun run, Action command)
        {
            Require(!stopped && queuedCommand == null, "Simulation command queue unavailable.");
            commandError = null; commandCompleted = false;
            queuedCommand = command; // Published last; the tick callback owns native execution.
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return AutomationWait.Until("simulation command completed", () => commandCompleted);
            Drain();
            Require(commandError == null, commandError);
        }

        internal void Tick(int tick)
        {
            if (stopped) return;
            lastTick = tick;
            Action command = queuedCommand;
            if (command != null)
            {
                queuedCommand = null;
                try
                {
                    Require(missions?.Current?.SessionId == session, "Test mission was replaced.");
                    command();
                }
                catch (Exception failure) { commandError = Unwrap(failure); }
                finally { commandCompleted = true; }
            }
            if (!observing || caseDone || error != null) return;
            try
            {
                Require(missions?.Current?.SessionId == session, "Test mission was replaced.");
                Require(bridge != null && bridge.OwnsLease, "Mission options were replaced; the test lease is no longer current.");
                Require(unchecked(tick - caseStart) <= MaximumCaseTicks, "Native observation deadline reached without the required transitions.");
                switch (current.Kind)
                {
                    case CaseKind.Escape: ObserveEscape(tick); break;
                    case CaseKind.ImmuneCrossing: ObserveImmune(tick); break;
                    default: ObserveWait(tick); break;
                }
            }
            catch (Exception failure) { error = "case=" + current.Name + " tick=" + tick + " " + Unwrap(failure); observing = false; }
        }

        private void ObserveEscape(int tick)
        {
            Actor actor = actors[0];
            GameUnit* unit = ActorUnit(actor);
            object entry = bridge.Case("fireEscapes", actor.Id);
            foreach (Actor ai in actors.Where(a => a.Ai)) ObserveAi(ai);
            if (entry != null)
            {
                if (!actor.SawCase) Emit("escape-start tick=" + tick + " tribe=" + unit->r_TribeId);
                actor.SawCase = true;
                actor.SawSafe |= Bridge.Value<bool>(entry, "Safe");
                actor.SawReturning |= Bridge.Value<bool>(entry, "Returning");
                return;
            }
            if (!actor.SawCase) return;
            int x = unit->r_CurrentTilePositionX, y = unit->r_CurrentTilePositionY;
            Require(actor.SawSafe && actor.SawReturning && !bridge.Hazard(current.Plague, x, y),
                "Escape ended without observed safe arrival and issued return.");
            if (Walking(unit)) return;
            Require(Math.Abs(x - actor.OriginX) <= 1 && Math.Abs(y - actor.OriginY) <= 1, "Return not confirmed by arrival at/next to the origin: at=" + x + "," + y);
            RequireOriginalGroup(actor, unit);
            foreach (Actor ai in actors.Where(a => a.Ai))
                Require(ai.Exposed, "AI negative case unproven: the AI soldier was never seen on a hazard tile.");
            Finish(tick, "escape-safe-return-arrival" + (actors.Any(a => a.Ai) ? "+ai-untouched" : ""));
        }

        // The AI soldier may die in the fire or be ordered by its AI; it must never get a Pathfinding stop or escape.
        private void ObserveAi(Actor ai)
        {
            if (!UnitAccess.TryGetById(ai.Id, out GameUnit* unit, out _) || unit == null || unit->r_GlobalId != ai.Global || !UnitAccess.IsReallyAlive(unit)) return;
            if (bridge.Hazard(current.Plague, unit->r_CurrentTilePositionX, unit->r_CurrentTilePositionY)) ai.Exposed = true;
            Require(bridge.Case("fireEscapes", ai.Id) == null && bridge.Case("replanWaits", ai.Id) == null, "An AI soldier got a Pathfinding hazard case.");
        }

        private void ObserveWait(int tick)
        {
            Actor actor = actors[0];
            GameUnit* unit = ActorUnit(actor);
            object entry = bridge.Case("replanWaits", actor.Id);
            int x = unit->r_CurrentTilePositionX;
            Require(actor.Waited || entry != null || x < cx - 1, "The walker passed the barrier column without a wait.");
            if (!actor.Waited)
            {
                if (entry == null) return;
                actor.Waited = true; actor.WaitStarted = tick; actor.WaitUntil = Bridge.Value<int>(entry, "Until");
                actor.StopX = unit->r_TargetTilePositionX2; actor.StopY = unit->r_TargetTilePositionY2;
                Emit("wait-start tick=" + tick + " until=" + actor.WaitUntil + " at=" + x + "," + unit->r_CurrentTilePositionY);
            }
            // The engine's replan counters are window metrics that the performance log resets periodically
            // (ResetReplanMetrics, matrix run 231b1bf8 on 22.11.2026), so the transition is proven from state:
            // an abort is the only exit that ends the wait at/after Until with the unit kept at its stop tile and
            // a settled stop entry; a superseding order removes that entry, a resume makes it walk.
            if (current.Kind == CaseKind.WaitBounded)
            {
                if (entry != null) return;
                string state = "at=" + x + "," + unit->r_CurrentTilePositionY + " target=" + unit->r_TargetTilePositionX2 + "," + unit->r_TargetTilePositionY2 +
                    " stop=" + actor.StopX + "," + actor.StopY + " until=" + actor.WaitUntil + " walking=" + Walking(unit) + " tribe=" + unit->r_TribeId + "/" + actor.Tribe;
                Require(unchecked(tick - actor.WaitUntil) >= 0, "Bounded wait ended before the actor's own deadline: " + state);
                Require(!Walking(unit) && unit->r_TargetTilePositionX2 == actor.StopX && unit->r_TargetTilePositionY2 == actor.StopY,
                    "Bounded wait did not end in a stop at the stop tile: " + state);
                Require(bridge.Case("replanSettled", actor.Id) != null, "Bounded wait ended without the abort's settled stop: " + state);
                Require(BarrierHolds(), "The hazard was gone at the abort; the abort is not proven to be the expiry.");
                Require(unit->r_TargetTilePositionX2 != actor.TargetX || unit->r_TargetTilePositionY2 != actor.TargetY,
                    "Bounded abort kept the destination as movement target.");
                RequireOriginalGroup(actor, unit);
                Finish(tick, "bounded-abort-with-hazard");
                return;
            }
            if (!actor.PastDeadline)
            {
                if (unchecked(tick - actor.WaitStarted) < waitSeconds * 40 + 16) return;
                Require(entry != null && BarrierHolds(),
                    "Unlimited wait did not persist beyond the finite deadline while the hazard remained.");
                actor.PastDeadline = true; Emit("unlimited wait survived the finite deadline tick=" + tick);
            }
            if (entry != null || Walking(unit)) return;
            if (unit->r_CurrentTilePositionX != actor.TargetX || unit->r_CurrentTilePositionY != actor.TargetY) return;
            // A resumed non-plain move walks in a tribe of its own and rejoins on arrival (ProcessReplanSolos).
            if (unit->r_TribeId != actor.Tribe || bridge.Case("replanSolos", actor.Id) != null) return;
            RequireOriginalGroup(actor, unit);
            Finish(tick, "unlimited-safe-resume-arrival");
        }

        private void ObserveImmune(int tick)
        {
            Actor immune = actors[0], control = actors[1];
            GameUnit* unit = ActorUnit(immune);
            Require(bridge.Case("replanWaits", immune.Id) == null, "The plague-immune lord was stopped in front of plague.");
            if (bridge.Hazard(true, unit->r_CurrentTilePositionX, unit->r_CurrentTilePositionY)) immune.Exposed = true;
            if (!control.Waited && bridge.Case("replanWaits", control.Id) != null) { control.Waited = true; Emit("control-wait tick=" + tick); }
            if (!immune.Arrived && !Walking(unit) && Math.Abs(unit->r_CurrentTilePositionX - immune.TargetX) <= 1 &&
                Math.Abs(unit->r_CurrentTilePositionY - immune.TargetY) <= 1)
            { immune.Arrived = true; Emit("lord-arrived tick=" + tick + " crossedPlague=" + immune.Exposed); }
            if (!immune.Arrived || !control.Waited) return;
            Require(immune.Exposed, "The lord arrived without being seen on a plague tile; the crossing is unproven.");
            Finish(tick, "immune-lord-crossed+control-waited");
        }

        private bool BarrierHolds()
        {
            for (int y = cy - BarrierHalf; y <= cy + BarrierHalf; y++)
                if (bridge.Hazard(current.Plague, cx, y)) return true;
            return false;
        }

        private void RequireOriginalGroup(Actor actor, GameUnit* unit)
        {
            Require(unit->r_TribeId == actor.Tribe && GameTribeManagerAPI.Instance.TryGetTribeById(actor.Tribe, out GameTribe* tribe) &&
                tribe != null && tribe->r_AliveState == AliveState.IsAlive && tribe->r_GlobalId == actor.TribeGlobal && tribe->r_PlayerIdOwner == actor.Owner,
                "Actor did not keep or rejoin its original group.");
        }

        private void Finish(int tick, string result) { caseDone = true; observing = false; Emit("observed-result=" + result + " case=" + current.Name + " tick=" + tick); }

        // Restores the original options and movement cases, then removes the actors (on the simulation tick).
        private void EndCase()
        {
            observing = false;
            if (bridge != null)
            {
                if (missions.Current?.SessionId == session && bridge.OwnsLease) bridge.Restore();
                bridge = null;
            }
            foreach (Actor actor in actors)
            {
                if (actor.Borrowed || !UnitAccess.TryGetById(actor.Id, out GameUnit* unit, out _) || unit == null || unit->r_GlobalId != actor.Global || !UnitAccess.IsReallyAlive(unit)) continue;
                if (APIShared.UnitAccess.TryGetById(actor.Id, out _, out _)) GameUnitManagerAPI.Instance.KillUnit(actor.Id);
            }
            actors.Clear();
            Emit("case ended; original options and movement cases restored, actors removed");
        }

        // ---- Native helpers ----

        // Pathfinding's validated effect manager binding (FireRouteRules.Effects): records 1..5999 of 0xE8 bytes,
        // +0x3C state, +0x3E type (FIRE_EFFECTS.md, PLAGUE_HEIGHT.md). Read only.
        private const int EffectSlots = 6000, EffectStride = 0xE8, EffectState = 0x3C, EffectType = 0x3E;
        private static Func<object, IntPtr> effectsGetter;
        private static byte* Effects()
        {
            object rules = Field<object>(Provider(), "fireRules") ?? throw new InvalidOperationException("Pathfinding fire rules are not bound.");
            if (effectsGetter == null)
            {
                // Mono's PropertyInfo.GetValue rejects pointer return types (run 60060f54, 22.11.2026), so the getter
                // is called through a tiny dynamic method that returns the pointer as IntPtr.
                Type type = rules.GetType();
                MethodInfo getter = type.GetProperty("Effects", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?.GetGetMethod(true) ??
                    throw new MissingMemberException(type.FullName, "Effects");
                var method = new System.Reflection.Emit.DynamicMethod("PathfindingHazardTest.Effects", typeof(IntPtr), new[] { typeof(object) }, type.Module, true);
                var il = method.GetILGenerator();
                il.Emit(System.Reflection.Emit.OpCodes.Ldarg_0);
                il.Emit(System.Reflection.Emit.OpCodes.Castclass, type);
                il.Emit(System.Reflection.Emit.OpCodes.Call, getter);
                il.Emit(System.Reflection.Emit.OpCodes.Conv_I);
                il.Emit(System.Reflection.Emit.OpCodes.Ret);
                effectsGetter = (Func<object, IntPtr>)method.CreateDelegate(typeof(Func<object, IntPtr>));
            }
            byte* effects = (byte*)effectsGetter(rules);
            Require(effects != null, "Effect manager unbound.");
            return effects;
        }

        // New or active fire (9) and plague (22) records.
        private static int NativeHazards()
        {
            byte* effects = Effects();
            int count = 0;
            for (int slot = 1; slot < EffectSlots; slot++)
            {
                byte* record = effects + slot * EffectStride;
                short state = *(short*)(record + EffectState), type = *(short*)(record + EffectType);
                if ((state == 1 || state == 2) && (type == 9 || type == (short)ProjectileType.Disease)) count++;
            }
            return count;
        }

        private GameUnit* ActorUnit(Actor actor)
        {
            bool found = UnitAccess.TryGetById(actor.Id, out GameUnit* unit, out _) && unit != null;
            if (!found || unit->r_GlobalId != actor.Global || unit->r_ControllableForPlayerId != actor.Owner)
                Require(false, "Actor " + actor.Type + ":" + actor.Id + " died or changed identity/owner: found=" + found +
                    (found ? " global=" + unit->r_GlobalId + "/" + actor.Global + " controllable=" + unit->r_ControllableForPlayerId + "/" + actor.Owner +
                        " type=" + unit->r_UnitChimp + " alive=" + UnitAccess.IsReallyAlive(unit) + " at=" + unit->r_CurrentTilePositionX + "," + unit->r_CurrentTilePositionY : ""));
            return unit;
        }

        private static bool Walking(GameUnit* unit) => unit->r_PathPlanStateBitFlags != 0 &&
            (unit->r_CurrentPathPlanIndex < unit->r_PathPlanLength || unit->r_NextTilePositionX2 != unit->r_CurrentTilePositionX ||
             unit->r_NextTilePositionY2 != unit->r_CurrentTilePositionY);

        private static object Provider()
        {
            var assembly = BepInEx.Bootstrap.Chainloader.PluginInfos["Pathfinding_Serp"].Instance.GetType().Assembly;
            return assembly.GetType("Pathfinding.Engine.EngineHost", true).GetField("runtime", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) ??
                throw new InvalidOperationException("No active Pathfinding engine.");
        }

        private static T Field<T>(object target, string name) =>
            (T)(target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public) ??
                throw new MissingFieldException(target.GetType().FullName, name)).GetValue(target);

        private void Emit(string text) => messages.Enqueue(text);
        private void Drain() { while (messages.TryDequeue(out string text)) log(text); }
        private static void SetSetting(object settings, string name, object value)
        {
            PropertyInfo property = settings.GetType().GetProperty(name) ?? throw new MissingMemberException(name);
            property.SetValue(settings, value);
            Require(Equals(property.GetValue(settings), value), "Setting rejected: " + name);
        }
        private static void Execute(AutomationRun run, Action action) => run.InSimulation(run.CurrentMission, () => { action(); return 0; });
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message ?? "Fixture assertion failed."); }
        private static string Unwrap(Exception error) =>
            (error is TargetInvocationException invocation && invocation.InnerException != null ? invocation.InnerException : error).Message;
    }
}
