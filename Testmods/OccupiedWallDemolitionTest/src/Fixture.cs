using System;
using System.Collections.Generic;
using APIShared;
using APIShared.Automation;
using APIShared.Simulation;
using CrusaderDE;
using R3;
using SHCDESE.API;
using SHCDESE.Detours;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace OccupiedWallDemolitionTest
{
    internal sealed partial class Fixture
    {
        private const string MapName = "test AI.map";
        private readonly Action<string> log;
        private readonly WallHooks hooks;
        private int owner, siteX, siteY;
        private IDisposable wallSubscription;
        private DemolitionObservation demolition;

        private sealed class DemolitionObservation
        {
            internal string Name;
            internal int Tile, Unit, PostCount;
            internal Exception Error;
        }

        internal Fixture(Action<string> log, WallHooks hooks)
        {
            this.log = log;
            this.hooks = hooks;
        }

        internal IEnumerable<AutomationStep> Steps()
        {
            yield return new AutomationStep("start-skirmish", TimeSpan.FromSeconds(180), Start);
            yield return new AutomationStep("prepare", TimeSpan.FromSeconds(60), Prepare, true);
            yield return new AutomationStep("empty-and-occupied-controls", TimeSpan.FromSeconds(60), Controls, true);
            yield return new AutomationStep("stationary-spearman", TimeSpan.FromSeconds(60),
                run => Stationary(run, "stationary-spearman", eChimps.CHIMP_TYPE_SPEARMAN, 2), true);
            yield return new AutomationStep("moving-spearman", TimeSpan.FromSeconds(90), MovingSpearman, true);
            yield return new AutomationStep("chicken", TimeSpan.FromSeconds(60),
                run => Stationary(run, "chicken", eChimps.CHIMP_TYPE_CHICKEN, 4), true);
            yield return new AutomationStep("free-civilian", TimeSpan.FromSeconds(60),
                run => Stationary(run, "free-civilian", eChimps.CHIMP_TYPE_PEASANT, 6), true);
            yield return new AutomationStep("apple-workworld", TimeSpan.FromSeconds(45), PrepareAppleWorld, true);
            yield return new AutomationStep("apple-worker-outbound", TimeSpan.FromSeconds(220),
                run => AppleWorker(run, false), true);
            yield return new AutomationStep("apple-worker-carrying", TimeSpan.FromSeconds(160),
                run => AppleWorker(run, true), true);
            yield return new AutomationStep("coverage", TimeSpan.FromSeconds(10), VerifyCoverage, true);
        }

        internal void Stop()
        {
            hooks.Disarm();
            demolition = null;
            wallSubscription?.Dispose();
            wallSubscription = null;
        }

        private IEnumerable<AutomationWait> Start(AutomationRun run)
        {
            Require(hooks.Ready, "Both native wall hooks must be available");
            foreach (var wait in run.StartSkirmish(MapName, new[] { AILords.SK_RAT })) yield return wait;
        }

        private IEnumerable<AutomationWait> Prepare(AutomationRun run)
        {
            yield return run.WaitForPlayState(run.CurrentMission);
            if (MainViewModel.Instance.Show_HUD_Briefing)
                MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
            yield return AutomationWait.Until("briefing closed", () => !MainViewModel.Instance.Show_HUD_Briefing);
            Director.instance.SetEngineFrameRate(100);
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, 20);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            InSimulation(run, () => {
                var players = GamePlayerManagerAPI.Instance;
                owner = players.GetLocalPlayerId();
                Require(owner > 0 && !players.IsAIPlayer(owner), "No human fixture player");
                FindSite();
                wallSubscription = BuildingR3EventHooks.OnWallBulldoze.Observable.Subscribe(OnWallBulldoze);
                log($"WALL_FIXTURE map={MapName} player={owner} center={siteX},{siteY}");
            });
        }

        private IEnumerable<AutomationWait> Controls(AutomationRun run)
        {
            int empty = 0, occupied = 0, unit = 0;
            RefundBalance before = default;
            InSimulation(run, () => {
                hooks.Disarm();
                empty = BuildEmptyWall(siteX, siteY);
                occupied = BuildEmptyWall(siteX + 2, siteY);
                unit = Spawn(eChimps.CHIMP_TYPE_SPEARMAN, siteX + 2, siteY, Tiles.GetTileHeight(occupied));
                before = ReadRefund();
                Require(Tiles.GetTileUnitId(occupied) != 0, "Control wall does not have a native occupant");
                demolition = new DemolitionObservation { Name = "occupied-control", Tile = occupied, Unit = unit };
            });
            // Vanilla refuses to dispatch a demolition while the native game is paused.
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            InSimulation(run, () => {
                Require(Tiles.GetTileUnitId(occupied) == unit, "Occupied control occupant left before the click");
                hooks.Delete(owner, siteX + 2, siteY, occupied);
            });
            foreach (var wait in Advance(run, 16)) yield return wait;
            InSimulation(run, () => {
                var after = ReadRefund();
                Require(IsWall(occupied) && before.Same(after), "Disabled occupied control was demolished or refunded");
                Require(Tiles.GetTileUnitId(occupied) == unit, "Occupied control occupant left the wall");
                Require(demolition.PostCount == 0, "Occupied control reached wall execution with the feature disabled");
                log($"WALL_CONTROL occupied blocked=true refund={before}->{after}");
                before = after;
                demolition = new DemolitionObservation { Name = "empty-control", Tile = empty };
            });
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            InSimulation(run, () => {
                Require(Tiles.GetTileUnitId(empty) == 0, "Empty control became occupied before the click");
                hooks.Delete(owner, siteX, siteY, empty);
            });
            foreach (var wait in Advance(run, 16)) yield return wait;
            InSimulation(run, () => {
                Require(Removed(empty) && demolition.PostCount == 1 && demolition.Error == null,
                    $"Empty control did not complete native demolition: tile={empty} flags={Tiles.GetTilePropertyFlag(empty)} " +
                    $"height={Tiles.GetTileHeight(empty)} default={Tiles.GetTileDefaultHeight(empty)} " +
                    $"posts={demolition.PostCount} callbackError={demolition.Error}");
                var after = ReadRefund();
                log($"WALL_CONTROL empty removed=true refund={before}->{after}");
                demolition = null;
                RetireControlUnit(unit);
            });
            // The unit first has to leave its native occupancy chain.
            foreach (var wait in Advance(run, 16)) yield return wait;
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            InSimulation(run, () => {
                if (IsWall(occupied) && Tiles.GetTileUnitId(occupied) == 0)
                    hooks.Delete(owner, siteX + 2, siteY, occupied);
            });
            foreach (var wait in Advance(run, 16)) yield return wait;
        }

        private IEnumerable<AutomationWait> Stationary(AutomationRun run, string name, eChimps type, int yOffset)
        {
            int x = siteX, y = siteY + yOffset;
            int unitId = 0, tile = 0;
            int chickenControl = 0, chickenControlTile = 0;
            InSimulation(run, () => {
                tile = BuildEmptyWall(x, y);
                unitId = Spawn(type, x, y, Tiles.GetTileHeight(tile));
                if (type == eChimps.CHIMP_TYPE_CHICKEN)
                {
                    chickenControlTile = BuildEmptyWall(x + 4, y);
                    chickenControl = Spawn(type, x + 4, y, Tiles.GetTileHeight(chickenControlTile));
                }
            });
            foreach (var wait in Advance(run, 3)) yield return wait;
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            Sample before = null, chickenControlBefore = null;
            InSimulation(run, () => {
                before = ReadUnit(unitId, tile);
                if (chickenControl != 0)
                {
                    chickenControlBefore = ReadUnit(chickenControl, chickenControlTile);
                    log($"WALL_CHICKEN_CONTROL phase=before wall={IsWall(chickenControlTile)} " +
                        Format(chickenControlBefore));
                }
                if (!CanObserve(before, tile))
                {
                    log($"WALL_COVERAGE case={name} status=unavailable initial={before}");
                    return;
                }
                Request(name, "native-wall-built-empty", before, tile);
            });
            if (!CanObserve(before, tile))
            {
                foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
                yield break;
            }
            foreach (var wait in Observe(run, name, before, tile, 96)) yield return wait;
            if (chickenControl != 0)
            {
                InSimulation(run, () => log($"WALL_CHICKEN_CONTROL phase=after wall={IsWall(chickenControlTile)} " +
                    Format(ReadUnit(chickenControl, chickenControlTile))));
                // The vanilla population updater may retire only one excess chicken per 200-tick cycle.
                foreach (var wait in Advance(run, 430)) yield return wait;
                InSimulation(run, () => {
                    var after = ReadUnit(chickenControl, chickenControlTile);
                    bool same = after != null && chickenControlBefore != null && after.Global == chickenControlBefore.Global;
                    log($"WALL_CHICKEN_CONTROL phase=population-window wall={IsWall(chickenControlTile)} " +
                        $"sameIdentity={same} alive={same && after.Alive} afterTicks>=430 {Format(after)}");
                });
            }
        }

        private IEnumerable<AutomationWait> MovingSpearman(AutomationRun run)
        {
            const string name = "moving-spearman";
            int x = siteX - 7, y = siteY + 7, destination = siteX + 7, id = 0;
            bool issued = false;
            InSimulation(run, () => id = Spawn(eChimps.CHIMP_TYPE_SPEARMAN, x, y,
                Tiles.GetTileHeight(Tiles.GetTileId(x, y))));
            foreach (var wait in Advance(run, 4)) yield return wait;
            InSimulation(run, () => issued = IssueMove(id, destination, y));
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            Sample before = null;
            int tile = 0;
            for (int attempt = 0; issued && attempt < 24 && before == null; attempt++)
            {
                yield return run.WaitForTicks(run.CurrentMission, 4);
                InSimulation(run, () => {
                    var unit = ReadUnit(id);
                    if (unit == null || unit.Path == 0 || unit.Substep >= 8 ||
                        !CanObserve(unit, unit.Tile) || !CanPaint(unit.Tile)) return;
                    tile = unit.Tile;
                    PaintWall(unit);
                    before = ReadUnit(id, tile);
                    Request(name, "synthetic-wall-under-real-active-route", before, tile);
                });
            }
            if (before == null)
            {
                foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
                log($"WALL_COVERAGE case={name} status=unavailable orderAccepted={issued}");
                yield break;
            }
            foreach (var wait in Observe(run, name, before, tile, 160, destination, y)) yield return wait;
        }

        private void Request(string name, string fixture, Sample before, int source)
        {
            Require(CanObserve(before, source) && IsWall(source), "Source is not an occupied wall");
            log($"WALL_BEFORE case={name} setup={fixture} height={Tiles.GetTileHeight(source)} " +
                $"default={Tiles.GetTileDefaultHeight(source)} refund={ReadRefund()} {before}");
            demolition = new DemolitionObservation { Name = name, Tile = source, Unit = before.Id };
            hooks.Arm(source);
            try { hooks.Delete(owner, before.X, before.Y, source); }
            catch { hooks.Disarm(); throw; }
        }

        private void OnWallBulldoze(WallBulldozeEventArgs args)
        {
            var current = demolition;
            if (current == null || args.TileId != current.Tile || args.PlayerId != owner ||
                args.Phase != EventHookPhase.Post) return;
            try
            {
                current.PostCount++;
                string tick = SimulationTickEvents.TryGetCurrentTick(out int mapTick) ? mapTick.ToString() : "unknown";
                log($"WALL_NATIVE_POST case={current.Name} mapTick={tick} removed={Removed(current.Tile)} " +
                    $"height={Tiles.GetTileHeight(current.Tile)} " +
                    $"{(current.Unit == 0 ? "unit=none" : Format(ReadUnit(current.Unit, current.Tile)))}");
            }
            catch (Exception error) { current.Error = error; }
        }

        private IEnumerable<AutomationWait> Observe(AutomationRun run, string name, Sample before, int source,
            int ticks, int destinationX = -1, int destinationY = -1, AppleObservation work = null)
        {
            bool removed = false, changedTile = false, reached = false, delivered = false;
            Sample latest = before;
            try
            {
                foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
                for (int elapsed = 0; elapsed < ticks;)
                {
                    int delta = elapsed < 16 ? 4 : 12;
                    yield return run.WaitForTicks(run.CurrentMission, delta);
                    elapsed += delta;
                    int tickSample = elapsed;
                    InSimulation(run, () => {
                        latest = ReadUnit(before.Id, source);
                        if (!removed && Removed(source))
                        {
                            removed = true;
                            hooks.Disarm();
                            log($"WALL_REMOVED case={name} sampleTick={tickSample} tile={source} " +
                                $"height={Tiles.GetTileHeight(source)} refund={ReadRefund()} unit={Format(latest)}");
                        }
                        if (latest == null || latest.Global != before.Global || !latest.Alive) return;
                        changedTile |= latest.Tile != source;
                        if (destinationX >= 0 && latest.Tile != source)
                            reached |= Math.Abs(latest.X - destinationX) <= 1 && Math.Abs(latest.Y - destinationY) <= 1;
                        if (work != null)
                        {
                            SampleApple(work, latest, ReadGranary());
                            delivered = work.Delivered(latest);
                        }
                    });
                    if (removed && (latest == null || latest.Global != before.Global || !latest.Alive)) break;
                    if (removed && delivered) break;
                }
                foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            }
            finally { hooks.Disarm(); }
            InSimulation(run, () => {
                Require(removed, $"No physical wall removal for {name}, tile {source}");
                var native = demolition;
                demolition = null;
                Require(native != null && native.PostCount == 1 && native.Error == null,
                    $"Unexpected native event evidence for {name}: posts={native?.PostCount} error={native?.Error}");
                bool same = latest != null && latest.Global == before.Global && latest.Type == before.Type
                    && latest.Owner == before.Owner;
                bool occupancyIntact = same && latest.Alive && latest.CurrentOccupancy == 1
                    && latest.SourceOccupancy == (latest.Tile == source ? 1 : 0);
                log($"WALL_OUTCOME case={name} removed=true sameIdentity={same} moved={changedTile} " +
                    $"sameHealth={same && latest.Health == before.Health} occupancyIntact={occupancyIntact} " +
                    $"reachedOriginalTarget={reached} last={Format(latest)}");
                if (work != null) LogAppleOutcome(name, work, before, latest, reached);
            });
        }

        private IEnumerable<AutomationWait> Advance(AutomationRun run, int ticks)
        {
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            yield return run.WaitForTicks(run.CurrentMission, ticks);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
        }

        private static GameTileManagerAPI Tiles => GameTileManagerAPI.Instance;
        private static bool IsWall(int tile) => (Tiles.GetTilePropertyFlag(tile) & TilePropertyFlag.IsWall) != 0;
        private static bool Removed(int tile) => !IsWall(tile) && Tiles.GetTileHeight(tile) == Tiles.GetTileDefaultHeight(tile);
        private static bool CanObserve(Sample unit, int tile) => unit != null && unit.Alive
            && unit.Tile == tile && unit.CurrentOccupancy == 1;

        private void FindSite()
        {
            var keep = GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(owner);
            for (int radius = 12; radius < Tiles.GetCurrentMapSize(); radius += 8)
                for (int dy = -radius; dy <= radius; dy += 8)
                    for (int dx = -radius; dx <= radius; dx += 8)
                    {
                        int x = keep.X + dx, y = keep.Y + dy;
                        if (!FlatVacantSquare(x, y)) continue;
                        siteX = x;
                        siteY = y;
                        return;
                    }
            throw new InvalidOperationException("No flat, empty 25x25 fixture site");
        }

        private static bool FlatVacantSquare(int x, int y)
        {
            const int radius = 12;
            if (!Tiles.IsTileInsideMapBounds(x, y)) return false;
            byte height = Tiles.GetTileHeight(Tiles.GetTileId(x, y));
            for (int yy = y - radius; yy <= y + radius; yy++)
                for (int xx = x - radius; xx <= x + radius; xx++)
                {
                    if (!Tiles.IsTileInsideMapBounds(xx, yy)) return false;
                    int tile = Tiles.GetTileId(xx, yy);
                    if (!CanPaint(tile) || Tiles.GetTileUnitId(tile) != 0 || !Tiles.IsTileWalkableAndUnoccupied(tile) ||
                        Tiles.GetTileHeight(tile) != height)
                        return false;
                }
            return true;
        }

        private static bool CanPaint(int tile)
        {
            return Tiles.IsValidTileId(tile) && Tiles.GetTileBuildingId(tile) == 0
                && Tiles.GetTileDefaultHeight(tile) <= byte.MaxValue - 80
                && (Tiles.GetTilePropertyFlag(tile) & (TilePropertyFlag.IsWall | TilePropertyFlag.IsMoat
                    | TilePropertyFlag.PlannedMoat | TilePropertyFlag.IsTree)) == 0;
        }

        private int BuildEmptyWall(int x, int y)
        {
            int tile = Tiles.GetTileId(x, y);
            Require(CanPaint(tile) && Tiles.GetTileUnitId(tile) == 0, "Real wall fixture requires empty buildable terrain");
            BulkAIDetours.c_game_ai_build_wall_hook_impl(Tiles.GetTileManager(), owner, x, y, eMappers.MAPPER_WALL);
            Require(IsWall(tile) && ((Tiles.GetTilePlayerOwnerId(tile) & 7) + 1) == owner,
                "Native builder did not create a wall owned by the human fixture player");
            return tile;
        }

        private void PaintWall(Sample unit)
        {
            Require(CanPaint(unit.Tile), "Synthetic wall source is blocked by other terrain");
            var ownerByte = Tiles.GetTilePlayerOwnerId(unit.Tile);
            Tiles.SetTilePlayerOwnerId(unit.Tile, (byte)((ownerByte & ~7) | (owner - 1)));
            Tiles.SetTilePropertyFlag(unit.Tile, Tiles.GetTilePropertyFlag(unit.Tile) | TilePropertyFlag.IsWall);
            // Match a regular full-height wall while leaving the unit and its native order untouched.
            Tiles.SetTileHeight(unit.Tile, checked((byte)(Tiles.GetTileDefaultHeight(unit.Tile) + 80)));
        }

        private int Spawn(eChimps type, int x, int y, byte height)
        {
            int unit = checked((int)GameUnitManagerAPI.Instance.CreateUnitLocal(owner, owner, x, y, height, type));
            Require(unit > 0, $"Native unit spawn failed for {type}");
            return unit;
        }

        private static unsafe void RetireControlUnit(int id)
        {
            bool retired = APIShared.UnitAccess.TryGetById(id, out GameUnit* unit, out _)
                && GameUnitManagerAPI.Instance.DeleteUnitSafe(id);
            Require(retired, "Occupied control could not retire its fixture unit");
        }

        private bool IssueMove(int id, int x, int y)
        {
            var tribes = GameTribeManagerAPI.Instance;
            int tribe = checked((int)tribes.Create(owner));
            return tribe > 0 && tribes.AssignUnit(tribe, id) && tribes.IssueMoveHereCommand(tribe, x, y);
        }

        private readonly struct RefundBalance
        {
            private readonly int stone;
            private readonly uint partial;
            internal RefundBalance(int stone, uint partial) { this.stone = stone; this.partial = partial; }
            internal bool Same(RefundBalance other) => stone == other.stone && partial == other.partial;
            public override string ToString() => $"{stone}+accumulator:{partial}";
        }

        private unsafe RefundBalance ReadRefund()
        {
            var players = GamePlayerManagerAPI.Instance;
            Require(players.TryGetPlayerResourcesById(owner, out var resources) && resources != null, "Missing fixture resources");
            return new RefundBalance(players.GetGoodAmount(owner, eGoods.STORED_STONE_BLOCKS), resources->r_WallRefundStoneAccumulator);
        }

        private sealed class Sample
        {
            internal int Id, Owner, Tile, X, Y, WorldX, WorldY, BaseHeight, HeightAdd, State, Path, Substep;
            internal int Workplace, Cargo, CurrentOccupancy, SourceOccupancy;
            internal uint Global, Health;
            internal eChimps Type;
            internal bool Alive;
            public override string ToString() => $"id={Id}/{Global} type={Type} owner={Owner} alive={Alive} hp={Health} " +
                $"tile={Tile}@{X},{Y} world={WorldX},{WorldY} elevation={BaseHeight}+{HeightAdd} " +
                $"state={State} path={Path} substep={Substep} work={Workplace} cargo={Cargo} " +
                $"currentOccupancy={CurrentOccupancy} sourceOccupancy={SourceOccupancy}";
        }

        private static unsafe Sample ReadUnit(int id, int source = -1)
        {
            if (!UnitAccess.TryGetById(id, out GameUnit* unit, out _) || unit == null) return null;
            int tile = unchecked((int)unit->r_CurrentPositionTileId);
            return new Sample {
                Id = id, Global = unit->r_GlobalId, Type = unit->r_UnitChimp, Owner = unit->r_ControllableForPlayerId,
                Alive = UnitAccess.IsReallyAlive(unit), Health = unit->r_CurrentHealth, Tile = tile,
                X = unit->r_CurrentTilePositionX, Y = unit->r_CurrentTilePositionY,
                WorldX = unit->r_CurrentWorldPositionX, WorldY = unit->r_CurrentWorldPositionY,
                BaseHeight = unit->r_HeightElevation, HeightAdd = unit->N0000006A, State = unit->r_AIState,
                Path = unit->r_PathPlanStateBitFlags, Substep = unit->r_MovementSubstep,
                Workplace = unit->r_LinkedProductionBuildingId, Cargo = unit->r_CarryGoodAmount,
                CurrentOccupancy = CountOccupancy(tile, id), SourceOccupancy = CountOccupancy(source, id)
            };
        }

        private static unsafe int CountOccupancy(int tile, int unit)
        {
            if (!Tiles.IsValidTileId(tile)) return 0;
            int current = Tiles.GetTileUnitId(tile), count = 0;
            var seen = new HashSet<int>();
            while (current != 0)
            {
                if (!seen.Add(current) || !UnitAccess.TryGetById(current, out GameUnit* member, out _) || member == null)
                    return -1;
                if (current == unit) count++;
                current = member->r_InterestingTodo4MaybeRandomPathing;
            }
            return count;
        }

        private static string Format(Sample unit) => unit?.ToString() ?? "missing";
        private static void InSimulation(AutomationRun run, Action action) => run.InSimulation(run.CurrentMission,
            () => { action(); return 0; });
        private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
    }
}
