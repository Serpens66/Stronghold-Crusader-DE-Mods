using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using APIShared;
using APIShared.Automation;
using APIShared.Simulation;
using CrusaderDE;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace PathfindingSurfaceTest
{
    // Static Plugin.active and process-lifetime publishers own this fixture after startup cleanup.
    // All native objects are accessed under InSimulation or the existing simulation-tick publisher.
    // The disposable Skirmish is never saved. No executable memory, private state writes or OS input.
    internal sealed partial class Fixture
    {
        private readonly string map, scenario;
        private readonly Action<string> log;
        private readonly List<Actor> actors = new List<Actor>();
        private readonly List<string> observations = new List<string>();
        private readonly Dictionary<string, long> metrics = new Dictionary<string, long>();
        private object provider;
        private FieldInfo maskProgress;
        private int owner, cx, cy, elevation, tribe, phase;
        private bool ring;
        private volatile bool capture, stopped;
        private volatile string error;
        private volatile Action queuedCommand;
        private volatile bool commandCompleted;
        private volatile string commandError;
        private IMissionLifecycleCapability missions;
        private IDisposable moves;
        private long session;
        private int lastTick;
        private static readonly int[] Dx = { 0,1,1,1,0,-1,-1,-1 }, Dy = { -1,-1,0,1,1,1,0,-1 };
        private static readonly eChimps[] Mounted = { eChimps.CHIMP_TYPE_KNIGHT, eChimps.CHIMP_TYPE_ARAB_HORSEMAN,
            eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER, eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL };
        private sealed class Actor
        {
            internal int Id, X, Y, StartX, StartY, TargetX, TargetY, LastResult = -1, MoveCalls, AcceptedRoutes, RaisedRoutes, Samples;
            internal uint Global;
            internal eChimps Type;
            internal bool Mounted, Arrived, Moved;
        }
        internal Fixture(string map, string scenario, Action<string> log) { this.map = map; this.scenario = scenario; this.log = log; }
        internal static bool Owns(string value) => value == "surface-matrix" || value == "surface-groups-no-singles";
        internal void Stop() { stopped = true; capture = false; queuedCommand = null; }
        internal IEnumerable<AutomationStep> Steps()
        {
            yield return new AutomationStep("skirmish", TimeSpan.FromSeconds(240), Start);
            yield return new AutomationStep("real-wall-and-stairs", TimeSpan.FromSeconds(150), Prepare, true);
            if (scenario == "surface-matrix")
                foreach (var type in Mounted.Concat(new[] { eChimps.CHIMP_TYPE_SPEARMAN }))
                {
                    eChimps captured = type;
                    yield return new AutomationStep("single-" + type, TimeSpan.FromSeconds(90), run => Exercise(run, new[] { captured }, true, false), true);
                }
            yield return new AutomationStep("mixed-detour", TimeSpan.FromSeconds(120),
                run => Exercise(run, Mounted.Concat(new[] { eChimps.CHIMP_TYPE_SPEARMAN }).ToArray(), false, false), true);
            yield return new AutomationStep("close-ground-route", TimeSpan.FromSeconds(60), CloseRing, true);
            foreach (var type in Mounted)
            {
                eChimps captured = type;
                yield return new AutomationStep("blocked-" + type, TimeSpan.FromSeconds(90), run => Exercise(run, new[] { captured }, false, true), true);
            }
            yield return new AutomationStep("foot-still-crosses", TimeSpan.FromSeconds(90),
                run => Exercise(run, new[] { eChimps.CHIMP_TYPE_SPEARMAN }, false, false), true);
        }
        private IEnumerable<AutomationWait> Start(AutomationRun run)
        {
            var plugin = BepInEx.Bootstrap.Chainloader.PluginInfos["Pathfinding_Serp"].Instance;
            object settings = plugin.GetType().GetProperty("Settings").GetValue(plugin);
            Set(settings, "EnableMod", true); Set(settings, "EnablePathfindingCore", true);
            Set(settings, "AnchorSingleMoves", scenario == "surface-matrix");
            Set(settings, "EnableFriendlyMoatTraversal", false);
            Set(settings, "AvoidFire", false); Set(settings, "AvoidPlague", false);
            Set(settings, "EnableMoveFormationEnhancements", false);
            foreach (var wait in run.StartSkirmish(map, new[] { AILords.SK_RAT }, peaceTimeMinutes: 60)) yield return wait;
        }
        private IEnumerable<AutomationWait> Prepare(AutomationRun run)
        {
            yield return run.WaitForPlayState(run.CurrentMission);
            if (MainViewModel.Instance.Show_HUD_Briefing) MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
            yield return AutomationWait.Until("briefing closed", () => !MainViewModel.Instance.Show_HUD_Briefing);
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 16);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            session = run.CurrentMission.SessionId;
            if (!ApiShared.ForMod(Plugin.Guid).TryGetMissionLifecycle(out missions, out var diagnostic))
                throw new InvalidOperationException(diagnostic.Reason);
            Director.instance.SetEngineFrameRate(250);
            owner = run.InSimulation(run.CurrentMission, () => GamePlayerManagerAPI.Instance.GetLocalPlayerId());
            Require(owner >= 1 && owner <= 8, "No controlled player");
            Execute(run, SupplyWallStone);
            bool found = false;
            var keep = run.InSimulation(run.CurrentMission, () => GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(owner));
            for (int dy = -48; dy <= 48 && !found; dy += 6)
                for (int dx = -48; dx <= 48 && !found; dx += 6)
                {
                    int x = keep.X + dx, y = keep.Y + dy;
                    bool flat = run.InSimulation(run.CurrentMission, () => FlatRegion(x, y));
                    if (flat)
                    {
                        cx = x; cy = y;
                        found = run.InSimulation(run.CurrentMission, TryBuildBarrier);
                    }
                    yield return AutomationWait.NextFrame;
                }
            Require(found, "No normal wall command established the complete barrier on a vacant flat39x33 region; see wall-outcome diagnostics");
            Execute(run, () => {
                BuildObserved("stairs-left", cx - 1, cy, cx - 8, cy, eMappers.MAPPER_STAIR);
                BuildObserved("stairs-right", cx + 1, cy, cx + 8, cy, eMappers.MAPPER_STAIR);
            });
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 32);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            Execute(run, VerifyTopology);
            provider = pluginProvider();
            maskProgress = provider.GetType().GetField("anchorMaskNext", BindingFlags.Instance | BindingFlags.NonPublic) ??
                throw new MissingFieldException("anchorMaskNext");
            moves = UnitR3EventHooks.OnUnitMoveHere.Observable.Subscribe(ObserveMove);
            if (!SimulationTickEvents.TryRegister(Plugin.Guid, "surface-observer", Tick, out string reason, 750))
                throw new InvalidOperationException(reason);
            log("ready map=" + map + " owner=" + owner + " center=" + cx + "," + cy + " session=" + session + " scenario=" + scenario);
        }
        private bool FlatRegion(int x, int y)
        {
            var tiles = GameTileManagerAPI.Instance;
            var keep = GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(owner);
            var buildings = GameBuildingManagerAPI.Instance;
            int range = buildings.GetKeepProximityRange(tiles.GetCurrentMapSize());
            int overrideRange = buildings.KeepProximityOverride.GetValue();
            if (overrideRange > 0) range = Math.Min(range, overrideRange);
            if (Math.Max(Math.Abs(x + 19 - keep.X), Math.Abs(x - 16 - keep.X)) >= range ||
                Math.Max(Math.Abs(y - 16 - keep.Y), Math.Abs(y + 16 - keep.Y)) >= range) return false;
            int firstHeight = -1;
            for (int yy = y - 16; yy <= y + 16; yy++) for (int xx = x - 16; xx <= x + 22; xx++)
            {
                if (!tiles.IsTileInsideMapBounds(xx, yy)) return false;
                int tile = tiles.GetTileId(xx, yy);
                if (!tiles.IsValidTileId(tile) || !tiles.IsTileWalkableAndUnoccupied(tile) || tiles.GetChimpLayer()[tile] != 0 ||
                    (tiles.GetTilePropertyFlag(tile) & (TilePropertyFlag.IsWall | TilePropertyFlag.IsBuilding)) != 0) return false;
                int height = tiles.GetTileHeight(tile);
                if (firstHeight >= 0 && firstHeight != height) return false;
                firstHeight = height;
            }
            elevation = firstHeight; return true;
        }
        private void VerifyTopology()
        {
            var tiles = GameTileManagerAPI.Instance;
            for (int y = cy - 12; y <= cy + 12; y++)
                Require((tiles.GetTilePropertyFlag(tiles.GetTileId(cx, y)) & TilePropertyFlag.IsWall) != 0, "Missing wall tile " + cx + "," + y);
            int stairs = 0;
            for (int x = cx - 8; x <= cx + 8; x++)
                if ((tiles.GetTilePropertyFlag(tiles.GetTileId(x, cy)) & TilePropertyFlag.IsStairs) != 0) stairs++;
            Require(stairs >= 2, "Two-sided staircase was not actually constructed");
            int any = Distance(cx - 11, cy, cx + 10, cy, false, out bool raised);
            int ground = Distance(cx - 11, cy, cx + 10, cy, true, out _);
            Require(any >= 1 && raised, "Unrestricted shortest route does not enter real wall/stair tiles");
            Require(ring ? ground < 0 : ground > any, "Ground-route topology not established: any=" + any + " ground=" + ground + " ring=" + ring);
            if (ring)
            {
                for (int x = cx; x <= cx + 18; x++)
                    Require(Wall(x, cy - 12) && Wall(x, cy + 12), "Ring top/bottom gap");
                for (int y = cy - 12; y <= cy + 12; y++) Require(Wall(cx + 18, y), "Ring right-side gap");
            }
            log("topology ring=" + ring + " actualStairs=" + stairs + " shortestAny=" + any + " shortestGround=" + ground + " raisedShortcut=" + raised);
        }
        private bool Wall(int x, int y) => (GameTileManagerAPI.Instance.GetTilePropertyFlag(GameTileManagerAPI.Instance.GetTileId(x, y)) & TilePropertyFlag.IsWall) != 0;
        // Independent bounded oracle over the actual public native direction grid. The closed ring's
        // complete perimeter is separately proven, so a global ground path cannot enter it elsewhere.
        private int Distance(int sx, int sy, int tx, int ty, bool restricted, out bool raised)
        {
            int minX = cx - 16, minY = cy - 16, width = 39, height = 33;
            var distance = Enumerable.Repeat(-1, width * height).ToArray();
            var touchedRaised = new bool[distance.Length]; var queue = new Queue<int>();
            int start = (sy - minY) * width + sx - minX, target = (ty - minY) * width + tx - minX;
            distance[start] = 0; queue.Enqueue(start);
            while (queue.Count != 0)
            {
                int a = queue.Dequeue(), x = minX + a % width, y = minY + a / width;
                if (a == target) { raised = touchedRaised[a]; return distance[a]; }
                for (int d = 0; d < 8; d++)
                {
                    int nx = x + Dx[d], ny = y + Dy[d];
                    if (nx < minX || nx >= minX + width || ny < minY || ny >= minY + height ||
                        !GamePathingManagerAPI.Instance.HasPathEdge(x, y, (PackedPathDirection)d)) continue;
                    int next = (ny - minY) * width + nx - minX;
                    if (distance[next] >= 0) continue;
                    bool high = Raised(nx, ny);
                    if (restricted && high) continue;
                    distance[next] = distance[a] + 1; touchedRaised[next] = touchedRaised[a] || high; queue.Enqueue(next);
                }
            }
            raised = false; return -1;
        }
        private static bool Raised(int x, int y) => (GameTileManagerAPI.Instance.GetTilePropertyFlag(GameTileManagerAPI.Instance.GetTileId(x, y)) &
            (TilePropertyFlag.IsWall | TilePropertyFlag.IsElevated)) != 0;
        private IEnumerable<AutomationWait> CloseRing(AutomationRun run)
        {
            Execute(run, () => {
                BuildObserved("ring-top", cx, cy - 12, cx + 18, cy - 12, eMappers.MAPPER_WALL);
                BuildObserved("ring-bottom", cx, cy + 12, cx + 18, cy + 12, eMappers.MAPPER_WALL);
                BuildObserved("ring-right", cx + 18, cy - 12, cx + 18, cy + 12, eMappers.MAPPER_WALL);
            });
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 32);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            ring = true; Execute(run, VerifyTopology);
        }
        private IEnumerable<AutomationWait> Exercise(AutomationRun run, eChimps[] types, bool individual, bool blocked)
        {
            Execute(run, () => Spawn(types));
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 12);
            yield return AutomationWait.Until("live fixture actors", () => run.InSimulation(run.CurrentMission, AllLive));
            // Real spawned actors naturally activate the player's asynchronous core-mask build.
            // This acceptance tests acceleration after readiness, not its intentional Vanilla warmup fallback.
            yield return AutomationWait.Until("current player anchor mask ready", () => run.InSimulation(run.CurrentMission, AnchorMaskReady));
            log("mask-ready owner=" + owner + " scenario=" + scenario);
            Execute(run, () => GamePlayerManagerAPI.Instance.SetSelectedChimps(actors.Select(a => a.Id).ToList()));
            yield return run.WaitForTicks(run.CurrentMission, 8);
            yield return AutomationWait.Until("exact group membership", () => run.InSimulation(run.CurrentMission, GroupReady));
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            if (individual)
            {
                foreach (var wait in RunOnSimulationTick(run, () =>
                    Require(GameTribeManagerAPI.Instance.IssueMoveHereCommand(tribe, cx - 11, cy - 7), "Warmup move wrapper rejected"))) yield return wait;
                yield return run.WaitForTicks(run.CurrentMission, 8);
                foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
                Execute(run, RequireFirstMoving);
            }
            foreach (var wait in RunOnSimulationTick(run, () => {
                capture = true; phase++; CaptureMetrics();
                foreach (var actor in actors) { actor.StartX = actor.X; actor.StartY = actor.Y; actor.LastResult = -1; actor.MoveCalls = 0; }
                if (individual) MoveActor(actors[0]);
                else Require(GameTribeManagerAPI.Instance.IssueMoveHereCommand(tribe, cx + 10, cy), "Main move wrapper rejected");
                CheckImmediate(blocked, individual);
                if (individual && scenario == "surface-matrix")
                    Require(Metric("anchorSingleRoutes") > metrics["anchorSingleRoutes"], "No actual fast individual route was published");
                if (!individual && !blocked)
                    Require(Metric("anchorPublished") > metrics["anchorPublished"], "No actual fast group route was published");
                log("command phase=" + phase + " tick=" + lastTick + " simThread=" + Environment.CurrentManagedThreadId +
                    " fastSingleDelta=" + (Metric("anchorSingleRoutes") - metrics["anchorSingleRoutes"]) +
                    " fastGroupDelta=" + (Metric("anchorPublished") - metrics["anchorPublished"]));
            })) yield return wait;
            int passes = blocked ? 4 : 80;
            for (int i = 0; i < passes; i++)
            {
                yield return run.WaitForTicks(run.CurrentMission, 24);
                bool done = run.InSimulation(run.CurrentMission, () => Observe(blocked));
                Drain(run);
                if (!blocked && done) break;
            }
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            Execute(run, () => {
                Require(error == null, error);
                Require(blocked ? actors.All(a => !a.Moved && a.MoveCalls > 0 && a.LastResult == 0) : actors.All(a => a.Arrived), "Movement did not reach its expected terminal outcome");
                if (!blocked && actors.Any(a => !a.Mounted)) Require(actors.Any(a => !a.Mounted && a.RaisedRoutes > 0), "Foot control never received a structural route");
                log("case phase=" + phase + " individual=" + individual + " blocked=" + blocked + " actors=" + string.Join(",", actors.Select(a => a.Type + ":" + a.Id + ":calls=" + a.MoveCalls + ":accepted=" + a.AcceptedRoutes + ":raised=" + a.RaisedRoutes + ":arrived=" + a.Arrived)));
                capture = false;
                foreach (var actor in actors) KillActor(actor);
                actors.Clear();
            });
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 40);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
        }
        private unsafe void Spawn(eChimps[] types)
        {
            actors.Clear(); capture = false;
            for (int i = 0; i < types.Length; i++)
            {
                int x = cx - 11, y = cy + i - types.Length / 2;
                GameUnit* unit = null;
                long id = GameUnitManagerAPI.Instance.CreateUnitLocal(owner, owner, x, y, elevation, types[i]);
                Require(id > 0 && id <= int.MaxValue && UnitAccess.TryGetById((int)id, out unit, out _), "Unit spawn failed");
                Require(unit->r_GlobalId != 0 && unit->r_UnitChimp == types[i] && unit->r_CurrentTilePositionX == x && unit->r_CurrentTilePositionY == y, "Spawn projected or wrong type");
                actors.Add(new Actor { Id = (int)id, Global = unit->r_GlobalId, Type = types[i], Mounted = Mounted.Contains(types[i]), X = x, Y = y, StartX = x, StartY = y, TargetX = cx + 10, TargetY = cy });
            }
        }
        private unsafe void MoveActor(Actor actor)
        {
            ActorUnit(actor);
            if (APIShared.UnitAccess.TryGetById(actor.Id, out var unit, out _)) GameUnitManagerAPI.Instance.MoveToTile(actor.Id, cx + 10, cy);
            else throw new InvalidOperationException("Actor disappeared before move");
        }
        private unsafe void KillActor(Actor actor)
        {
            ActorUnit(actor);
            if (APIShared.UnitAccess.TryGetById(actor.Id, out var unit, out _)) GameUnitManagerAPI.Instance.KillUnit(actor.Id);
            else throw new InvalidOperationException("Actor disappeared before cleanup");
        }
        private unsafe void RequireFirstMoving() => Require(Walking(ActorUnit(actors[0])), "Individual replan requires an actually moving unit");
        private unsafe bool AllLive() { foreach (var actor in actors) { GameUnit* unit = ActorUnit(actor); if (!UnitAccess.IsReallyAlive(unit)) return false; } return true; }
        private unsafe GameUnit* ActorUnit(Actor actor)
        {
            Require(UnitAccess.TryGetById(actor.Id, out GameUnit* unit, out _) && unit != null && unit->r_GlobalId == actor.Global && unit->r_UnitChimp == actor.Type && unit->r_ControllableForPlayerId == owner, "Fixture identity lost");
            return unit;
        }
        private unsafe bool GroupReady()
        {
            if (!AllLive()) return false;
            int id = ActorUnit(actors[0])->r_TribeId;
            if (id <= 0 || !GameTribeManagerAPI.Instance.TryGetTribeById(id, out GameTribe* group) || group->r_UnitsInGroup != actors.Count) return false;
            if (actors.Any(a => ActorTribe(a) != id)) return false;
            var members = new List<int>();
            if (!GameTribeManagerAPI.Instance.GetUnits(id, members) || !new HashSet<int>(members).SetEquals(actors.Select(a => a.Id))) return false;
            tribe = id; return true;
        }
        private unsafe int ActorTribe(Actor actor) => ActorUnit(actor)->r_TribeId;
        private unsafe void CheckImmediate(bool blocked, bool individual)
        {
            Require(error == null, error);
            foreach (var actor in actors)
            {
                GameUnit* unit = ActorUnit(actor);
                actor.StartX = unit->r_CurrentTilePositionX; actor.StartY = unit->r_CurrentTilePositionY;
                if (!blocked) Require(actor.LastResult > 0 && actor.AcceptedRoutes > 0, "Command did not publish an observed route for " + actor.Type);
                else Require(actor.MoveCalls > 0 && actor.LastResult == 0 && !Walking(unit), "No observed regular failure or unreachable mounted actor retained an active path");
            }
        }
        private void ObserveMove(UnitMoveHereEventArgs args)
        {
            if (!capture || stopped || args.Phase != EventHookPhase.Post) return;
            Actor actor = actors.FirstOrDefault(a => a.Id == args.UnitId);
            if (actor == null) return;
            actor.MoveCalls++; actor.LastResult = (int)args.ReturnValue;
            if (args.ReturnValue <= 0) return;
            actor.AcceptedRoutes++;
            try { AuditRemainingRoute(actor); }
            catch (Exception failure) { error = failure.Message; }
        }
        private IEnumerable<AutomationWait> RunOnSimulationTick(AutomationRun run, Action command)
        {
            Require(!stopped && error == null && queuedCommand == null, error ?? "Simulation command queue unavailable");
            commandError = null; commandCompleted = false;
            queuedCommand = command; // Publish managed state last; the existing tick callback owns native execution.
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return AutomationWait.Until("simulation command completed", () => commandCompleted);
            Require(commandError == null, commandError);
        }
        internal void Tick(int tick)
        {
            if (stopped || error != null || (!capture && queuedCommand == null)) return;
            if (missions?.Current?.SessionId != session)
            {
                error = commandError = "Test mission was replaced"; capture = false;
                queuedCommand = null; commandCompleted = true; return;
            }
            lastTick = tick;
            Action command = queuedCommand;
            if (command != null)
            {
                queuedCommand = null;
                try { command(); }
                catch (Exception failure) { error = commandError = failure.Message; }
                finally { commandCompleted = true; }
            }
            if (!capture || error != null) return;
            try
            {
                foreach (var actor in actors)
                {
                    if (actor.LastResult > 0) AuditRemainingRoute(actor);
                    ObserveActor(actor);
                }
            }
            catch (Exception failure) { error = failure.Message; }
        }
        private unsafe void AuditRemainingRoute(Actor actor)
        {
            GameUnit* unit = ActorUnit(actor);
            if (!Walking(unit)) return;
            Require(GamePathingManagerAPI.Instance.TryGetUnitPathPlanView(actor.Id, out GameUnitPathPlanView path) && path.IsLengthValid, "Invalid packed route");
            int x = unit->r_PathPlanStateBitFlags == 0 && unit->r_MovementSubstep == 8 ? unit->r_CurrentTilePositionX : unit->r_NextTilePositionX2;
            int y = unit->r_PathPlanStateBitFlags == 0 && unit->r_MovementSubstep == 8 ? unit->r_CurrentTilePositionY : unit->r_NextTilePositionY2;
            bool raised = false;
            for (int i = path.CurrentIndex; i < path.Length; i++)
            {
                Require(path.TryGetDirection(i, out PackedPathDirection direction), "Invalid route direction");
                x += Dx[(int)direction]; y += Dy[(int)direction];
                bool high = Raised(x, y); raised |= high;
                Require(!actor.Mounted || !high, "Mounted route entered a structural tile: actor=" + actor.Id + " tile=" + x + "," + y);
            }
            if (raised) actor.RaisedRoutes++;
        }
        private unsafe void ObserveActor(Actor actor)
        {
            GameUnit* unit = ActorUnit(actor);
            Require(UnitAccess.IsReallyAlive(unit), "Actor died during movement");
            int x = unit->r_CurrentTilePositionX, y = unit->r_CurrentTilePositionY;
            Require(!actor.Mounted || !Raised(x, y), "Mounted unit actually entered wall/stairs");
            actor.Moved |= x != actor.StartX || y != actor.StartY;
            actor.X = x; actor.Y = y; actor.Samples++;
            actor.Arrived |= Math.Abs(x - actor.TargetX) <= 3 && Math.Abs(y - actor.TargetY) <= 3 && !Walking(unit);
        }
        private bool Observe(bool blocked)
        {
            Require(error == null, error);
            foreach (var actor in actors) ObserveActor(actor);
            observations.Add("phase=" + phase + " tick=" + lastTick + " ring=" + ring + " blocked=" + blocked + " positions=" + string.Join(";", actors.Select(a => a.Type + ":" + a.X + "," + a.Y + ":result=" + a.LastResult + ":arrived=" + a.Arrived)));
            return actors.All(a => a.Arrived);
        }
        private static unsafe bool Walking(GameUnit* unit) => unit->r_PathPlanStateBitFlags != 0 &&
            (unit->r_CurrentPathPlanIndex < unit->r_PathPlanLength || unit->r_NextTilePositionX2 != unit->r_CurrentTilePositionX || unit->r_NextTilePositionY2 != unit->r_CurrentTilePositionY);
        private bool AnchorMaskReady()
        {
            var progress = maskProgress.GetValue(provider) as int[];
            Require(progress != null && owner >= 1 && owner < progress.Length, "Player mask progress is unavailable");
            int next = progress[owner];
            Require(next != -2, "Current player anchor mask build failed");
            // The native route core's fixed grid covers 800x800 cells, including unused map margin.
            return next >= 800 * 800;
        }
        private void CaptureMetrics() { metrics.Clear(); metrics["anchorSingleRoutes"] = Metric("anchorSingleRoutes"); metrics["anchorPublished"] = Metric("anchorPublished"); }
        private long Metric(string name) => (long)(provider.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(provider) ?? throw new MissingFieldException(name));
        private static object pluginProvider()
        {
            var assembly = BepInEx.Bootstrap.Chainloader.PluginInfos["Pathfinding_Serp"].Instance.GetType().Assembly;
            return assembly.GetType("Pathfinding.Engine.EngineHost", true).GetField("runtime", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null) ?? throw new InvalidOperationException("No active Pathfinding engine");
        }
        private void Drain(AutomationRun run)
        {
            string[] lines = run.InSimulation(run.CurrentMission, () => { var copy = observations.ToArray(); observations.Clear(); return copy; });
            foreach (string line in lines) log(line);
        }
        private static void Set(object settings, string name, bool value)
        {
            PropertyInfo property = settings.GetType().GetProperty(name) ?? throw new MissingMemberException(name);
            property.SetValue(settings, value);
            Require((bool)property.GetValue(settings) == value, "Setting rejected: " + name);
        }
        private static void Execute(AutomationRun run, Action action) => run.InSimulation(run.CurrentMission, () => { action(); return 0; });
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message ?? "Fixture assertion failed"); }
    }
}
