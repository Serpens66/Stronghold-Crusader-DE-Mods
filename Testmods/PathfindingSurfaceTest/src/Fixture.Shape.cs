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
    internal sealed partial class Fixture
    {
        // Specific player report: the former raster alternated on almost every
        // step for this slope (44 turns). Keep the route in its one-tile corridor
        // while requiring substantially fewer turns, not a particular byte string.
        private const int ShapeDeltaX = 45, ShapeDeltaY = 22, ShapeMaximumTurns = 15;
        private string shapeRegionFailure;

        private IEnumerable<AutomationWait> PrepareShape(AutomationRun run)
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
            provider = pluginProvider();
            maskProgress = provider.GetType().GetField("anchorMaskNext", BindingFlags.Instance | BindingFlags.NonPublic) ??
                throw new MissingFieldException("anchorMaskNext");
            moves = UnitR3EventHooks.OnUnitMoveHere.Observable.Subscribe(ObserveMove);
            if (!SimulationTickEvents.TryRegister(Plugin.Guid, "surface-observer", Tick, out string reason, 750))
                throw new InvalidOperationException(reason);
            log("shape-ready owner=" + owner + " session=" + session);
        }

        // The preceding case can leave an ordinary corpse or attract a world actor.
        // Find a genuinely empty region for every newly spawned case rather than
        // deleting world objects or relaxing the empty-ground acceptance contract.
        private IEnumerable<AutomationWait> FindShapeRegion(AutomationRun run)
        {
            var keep = run.InSimulation(run.CurrentMission, () => GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(owner));
            bool found = false; int rejected = 0;
            for (int dy = -96; dy <= 64 && !found; dy += 8)
                for (int dx = -96; dx <= 64 && !found; dx += 8)
                {
                    int x = keep.X + dx, y = keep.Y + dy;
                    found = run.InSimulation(run.CurrentMission, () => FlatShapeRegion(x, y));
                    if (found) { cx = x; cy = y; }
                    else rejected++;
                    yield return AutomationWait.NextFrame;
                }
            Require(found, "No vacant flat 50x27 ground region for the 45/22 route-shape scenario; last rejection=" + shapeRegionFailure);
            log("shape-region start=" + cx + "," + cy + " target=" + (cx + ShapeDeltaX) + "," +
                (cy + ShapeDeltaY) + " rejectedCandidates=" + rejected);
        }

        private bool FlatShapeRegion(int x, int y, bool allowActorAtStart = false)
        {
            var tiles = GameTileManagerAPI.Instance;
            shapeRegionFailure = null;
            int firstHeight = -1;
            for (int yy = y - 2; yy <= y + ShapeDeltaY + 2; yy++)
                for (int xx = x - 2; xx <= x + ShapeDeltaX + 2; xx++)
                {
                    if (!tiles.IsTileInsideMapBounds(xx, yy)) { shapeRegionFailure = "outside-map tile=" + xx + "," + yy; return false; }
                    int tile = tiles.GetTileId(xx, yy);
                    if (!tiles.IsValidTileId(tile)) { shapeRegionFailure = "invalid tile=" + xx + "," + yy; return false; }
                    bool walkable = tiles.IsTileWalkableAndUnoccupied(tile);
                    var chimp = tiles.GetChimpLayer()[tile];
                    var flags = tiles.GetTilePropertyFlag(tile);
                    if (
                        (!(allowActorAtStart && xx == x && yy == y) &&
                         (!walkable || chimp != 0)) ||
                        (flags & (TilePropertyFlag.IsWall | TilePropertyFlag.IsBuilding | TilePropertyFlag.IsElevated)) != 0)
                    {
                        shapeRegionFailure = "occupied-or-structural tile=" + xx + "," + yy + " walkable=" + walkable + " chimp=" + chimp + " flags=" + flags;
                        return false;
                    }
                    int h = tiles.GetTileHeight(tile);
                    if (firstHeight >= 0 && h != firstHeight) { shapeRegionFailure = "height tile=" + xx + "," + yy + " height=" + h + " expected=" + firstHeight; return false; }
                    firstHeight = h;
                }
            elevation = firstHeight; return true;
        }

        private unsafe void SpawnShape()
        {
            actors.Clear(); capture = false;
            GameUnit* unit = null;
            long id = GameUnitManagerAPI.Instance.CreateUnitLocal(owner, owner, cx, cy, elevation, eChimps.CHIMP_TYPE_SPEARMAN);
            Require(id > 0 && id <= int.MaxValue && UnitAccess.TryGetById((int)id, out unit, out _), "Shape actor spawn failed");
            Require(unit->r_CurrentTilePositionX == cx && unit->r_CurrentTilePositionY == cy && unit->r_GlobalId != 0,
                "Shape actor was projected away from the verified start");
            actors.Add(new Actor { Id = (int)id, Global = unit->r_GlobalId, Type = eChimps.CHIMP_TYPE_SPEARMAN,
                X = cx, Y = cy, StartX = cx, StartY = cy, TargetX = cx + ShapeDeltaX, TargetY = cy + ShapeDeltaY });
        }

        private IEnumerable<AutomationWait> ExerciseShape(AutomationRun run, bool group)
        {
            foreach (var wait in FindShapeRegion(run)) yield return wait;
            Execute(run, SpawnShape);
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 12);
            yield return AutomationWait.Until("live shape actor", () => run.InSimulation(run.CurrentMission, AllLive));
            yield return AutomationWait.Until("shape owner mask ready", () => run.InSimulation(run.CurrentMission, AnchorMaskReady));
            Execute(run, () => GamePlayerManagerAPI.Instance.SetSelectedChimps(actors.Select(a => a.Id).ToList()));
            yield return run.WaitForTicks(run.CurrentMission, 8);
            yield return AutomationWait.Until("shape group membership", () => run.InSimulation(run.CurrentMission, GroupReady));
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            foreach (var wait in RunOnSimulationTick(run, () => IssueShape(group))) yield return wait;
            for (int i = 0; i < 180; i++)
            {
                yield return run.WaitForTicks(run.CurrentMission, 32);
                if (run.InSimulation(run.CurrentMission, () => { Require(error == null, error); ObserveActor(actors[0]); return actors[0].Arrived; })) break;
            }
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            Execute(run, () => {
                Require(error == null, error); Require(actors[0].Arrived, "Shape actor did not arrive at the requested endpoint");
                log("shape-arrived kind=" + (group ? "group" : "individual") + " actor=" + actors[0].Id + " tile=" + actors[0].X + "," + actors[0].Y);
                capture = false; KillActor(actors[0]); actors.Clear();
            });
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 40);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
        }

        private unsafe void IssueShape(bool group)
        {
            Actor actor = actors[0]; GameUnit* unit = ActorUnit(actor);
            Require(unit->r_CurrentTilePositionX == cx && unit->r_CurrentTilePositionY == cy && !Walking(unit),
                "New shape order requires a stationary actor at the original start");
            Require(FlatShapeRegion(cx, cy, true), "Shape setup changed: another actor or obstacle entered the empty ground region; " + shapeRegionFailure);
            capture = true; phase++; CaptureMetrics();
            actor.MoveCalls = actor.AcceptedRoutes = 0; actor.LastResult = -1;
            if (group) Require(GameTribeManagerAPI.Instance.IssueMoveHereCommand(tribe, actor.TargetX, actor.TargetY), "Shape group command rejected");
            else
            {
                if (APIShared.UnitAccess.TryGetById(actor.Id, out var movingUnit, out _))
                    GameUnitManagerAPI.Instance.MoveToTile(actor.Id, actor.TargetX, actor.TargetY);
                else throw new InvalidOperationException("Shape actor disappeared before the individual command");
            }
            Require(error == null, error);
            Require(actor.LastResult > 0 && actor.AcceptedRoutes > 0, "No observed fresh shape route was published");
            Require(Metric(group ? "anchorPublished" : "anchorSingleRoutes") > metrics[group ? "anchorPublished" : "anchorSingleRoutes"],
                "Shape command did not use the tested accelerated route publisher");
            Require(GamePathingManagerAPI.Instance.TryGetUnitPathPlanView(actor.Id, out GameUnitPathPlanView path) &&
                path.IsLengthValid && path.CurrentIndex == 0 && path.Length == ShapeDeltaX,
                "Fresh 45/22 route has the wrong length or was already consumed");
            int x = cx, y = cy, turns = 0, previous = -1, maximumCross = 0;
            var runs = new System.Text.StringBuilder(); int runDirection = -1, runLength = 0;
            for (int i = 0; i < path.Length; i++)
            {
                Require(path.TryGetDirection(i, out PackedPathDirection direction), "Shape route contains an invalid direction");
                int d = (int)direction;
                Require(GamePathingManagerAPI.Instance.HasPathEdge(x, y, direction), "Shape route invents a directed edge");
                x += Dx[d]; y += Dy[d];
                int cross = Math.Abs((x - cx) * ShapeDeltaY - (y - cy) * ShapeDeltaX);
                maximumCross = Math.Max(maximumCross, cross);
                Require(cross <= ShapeDeltaX, "Shape route left its one-tile direct corridor");
                if (previous >= 0 && d != previous) turns++;
                previous = d;
                if (runDirection == d) runLength++;
                else
                {
                    if (runLength != 0) runs.Append(runDirection).Append('x').Append(runLength).Append(' ');
                    runDirection = d; runLength = 1;
                }
            }
            runs.Append(runDirection).Append('x').Append(runLength);
            Require(x == actor.TargetX && y == actor.TargetY && x == unit->r_TargetTilePositionX && y == unit->r_TargetTilePositionY,
                "Published shape route misses its requested or unit-owned endpoint");
            Require(turns <= ShapeMaximumTurns, "Fresh 45/22 route still zigzags: turns=" + turns + " maximum=" + ShapeMaximumTurns);
            log("shape-route kind=" + (group ? "group" : "individual") + " actor=" + actor.Id + " steps=" + path.Length +
                " turns=" + turns + " maximumCross=" + maximumCross + " oldRasterTurns=44 runs=" + runs);
        }
    }
}
