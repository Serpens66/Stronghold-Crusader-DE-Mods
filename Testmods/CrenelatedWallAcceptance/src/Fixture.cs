using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using APIShared;
using APIShared.Automation;
using BepInEx.Logging;
using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace CrenelatedWallAcceptance
{
    // In-game acceptance of CrenelatedWallTest through the real wall entry point (SE CreateWall ->
    // hooked native build -> OnBuildWall Pre/Post). Checks logic heights after Vanilla's render
    // maintenance, rejections, save/load of the managed set and real Assassin climbs.
    internal sealed partial class Fixture
    {
        private const string WallMod = "CrenelatedWallTest_Serp";
        private readonly string runId, scenario;
        private readonly Action<string> log;
        private readonly ConcurrentQueue<string> crenelLog = new ConcurrentQueue<string>();
        private readonly List<string> crenelSeen = new List<string>();
        private readonly HashSet<int> humanTiles = new HashSet<int>();
        private readonly List<int> controlTiles = new List<int>();
        private readonly Dictionary<int, uint> snapshot = new Dictionary<int, uint>();
        private int owner, ai, attacker, ox, oy, cx, cy;
        private string save;
        private volatile bool stopped;

        internal Fixture(string runId, string scenario, Action<string> log)
        {
            this.runId = runId; this.scenario = scenario; this.log = log;
            BepInEx.Logging.Logger.Listeners.Add(new Capture(this));
        }
        internal void Stop() => stopped = true;

        // Captures CrenelatedWallTest's own log lines so the decisions can be asserted.
        private sealed class Capture : ILogListener
        {
            private readonly Fixture owner;
            internal Capture(Fixture owner) { this.owner = owner; }
            public void LogEvent(object sender, LogEventArgs e)
            {
                if (owner.stopped || e?.Data == null) return;
                string text = e.Data.ToString();
                if (text.Contains("CRENEL_") && !text.Contains("CRENEL_ACCEPT")) owner.crenelLog.Enqueue(text);
            }
            public void Dispose() { }
        }

        internal IEnumerable<AutomationStep> Steps() => scenario == Plugin.EditorScenario ? EditorSteps() :
            scenario == Plugin.TopologyScenario ? TopologySteps() : scenario == Plugin.HeightsScenario ? HeightsSteps() : scenario == Plugin.AiScenario ? AiSteps() : scenario == Plugin.StressScenario ? StressSteps() : scenario == Plugin.DiagonalScenario ? DiagonalWalkSteps() : scenario == Plugin.LookScenario ? LookSteps(false) : scenario == Plugin.LookCornersScenario ? LookSteps(true) : scenario == Plugin.LookManualScenario ? LookManualSteps() : scenario == Plugin.LookXScenario ? LookXSteps() : scenario == Plugin.LookDiagonalScenario ? LookDiagonalSteps() : SkirmishSteps();

        private IEnumerable<AutomationStep> SkirmishSteps()
        {
            yield return new AutomationStep("skirmish", TimeSpan.FromSeconds(240), Start);
            yield return new AutomationStep("prepare", TimeSpan.FromSeconds(90), Prepare, true);
            yield return new AutomationStep("build", TimeSpan.FromSeconds(240), Build, true);
            yield return new AutomationStep("heights", TimeSpan.FromSeconds(60), Heights, true);
            yield return new AutomationStep("assassins", TimeSpan.FromSeconds(240), Assassins, true);
            yield return new AutomationStep("siege-tower", TimeSpan.FromSeconds(420), SiegeTower, true);
            yield return new AutomationStep("ladder", TimeSpan.FromSeconds(300), Ladder, true);
            yield return new AutomationStep("siege-summary", TimeSpan.FromSeconds(10), SiegeSummary);
            yield return new AutomationStep("save", TimeSpan.FromSeconds(60), Save, true);
            yield return new AutomationStep("reload", TimeSpan.FromSeconds(180), Reload);
            yield return new AutomationStep("after-load", TimeSpan.FromSeconds(120), AfterLoad, true);
            yield return new AutomationStep("vanilla-control", TimeSpan.FromSeconds(120), Control, true);
        }

        // ---------- steps ----------
        private IEnumerable<AutomationWait> Start(AutomationRun run)
        {
            SetSwitches(true, true);
            SetExisting(false, false, true); // map start: the skirmish map's pre-placed walls (if any) are aligned
            // Peace time keeps the AI from attacking the lord or the test units. The climbing and siege
            // units belong to an unused slot and act on direct orders, so peace does not affect them.
            // Fixed AI castle template and keep order: the control row area depends on the start (run 7270b60a).
            ArmLobby(-1, PeaceMinutes, fixedWorld: true);
            log("settings map=4spielerkarte.map opponent=SK_RAT peace=" + PeaceMinutes + "min (set in MPsetupData; the Skirmish lobby shows no option)");
            foreach (var wait in run.StartSkirmish("4spielerkarte.map", new[] { AILords.SK_RAT })) yield return wait;
        }

        private const int PeaceMinutes = 60;
        private static bool expectPeace;
        private IEnumerable<AutomationWait> Prepare(AutomationRun run)
        {
            var mission = run.CurrentMission;
            yield return run.WaitForPlayState(mission);
            if (MainViewModel.Instance.Show_HUD_Briefing) MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
            yield return AutomationWait.Until("briefing closed", () => !MainViewModel.Instance.Show_HUD_Briefing);
            foreach (var wait in run.SetPause(mission, false)) yield return wait;
            yield return run.WaitForTicks(mission, 20);
            foreach (var wait in run.SetPause(mission, true)) yield return wait;
            // Simulation reads require a mission without briefing.
            string peace = run.InSimulation(mission, () => APIShared.Simulation.PeaceTimeState.TryGetActive(out bool on) ? (on ? "active" : "inactive") : "unavailable");
            log("PEACE " + peace + " expected=" + (expectPeace ? "active" : "any"));
            Require(!expectPeace || peace == "active", "peace time is not active (" + peace + "); the AI could attack the lord or the test units");
            Director.instance.SetEngineFrameRate(250);
            Drain();
            Require(crenelSeen.Any(l => l.Contains("CRENEL_SESSION") && l.Contains("active=True")), "CrenelatedWallTest session is not active");
            string area = run.InSimulation(mission, FindAreas);
            // Some starts have no stockpile: place one away from the test area, then add stone.
            foreach (var wait in Stock(run, mission)) yield return wait;
            log("READY session=" + mission.SessionId + " " + area);
        }

        private IEnumerable<AutomationWait> Build(AutomationRun run)
        {
            var m = run.CurrentMission;
            // Vanilla 0x5C580 accepts a crenel only next to a wall or crenel tile, so every shape
            // gets a supporting stone wall. Wall tiles never count as crenel neighbours.
            foreach (var w in Support(run, m, owner, 0, 2, 8, 2, false)) yield return w;   // piece-x
            foreach (var w in Support(run, m, owner, 11, 0, 11, 8, false)) yield return w; // piece-y
            foreach (var w in Support(run, m, owner, 0, 5, 8, 5, false)) yield return w;   // drag
            foreach (var w in Support(run, m, owner, 0, 7, 5, 7, false)) yield return w;   // 2x2, first diagonal
            foreach (var w in Support(run, m, owner, 7, 9, 7, 9, false)) yield return w;   // diagonal
            foreach (var w in Support(run, m, owner, 10, 9, 12, 9, false)) yield return w; // stairs
            foreach (var w in Support(run, m, owner, 1, 13, 6, 13, false)) yield return w; // L horizontal
            foreach (var w in Support(run, m, owner, 0, 15, 0, 20, false)) yield return w; // L vertical
            // Two separate attack segments: a shared walkway would let one siege path reach the other.
            foreach (var w in Support(run, m, owner, 0, 25, 5, 25, false)) yield return w; // ladder segment
            foreach (var w in Support(run, m, owner, 7, 25, 13, 25, false)) yield return w; // tower segment
            // Exploit case: straight rows placed piece by piece, simulation running between pieces.
            for (int i = 1; i <= 7; i++) foreach (var w in Line(run, m, "piece-x", owner, i, 1, i, 1)) yield return w;
            for (int i = 1; i <= 7; i++) foreach (var w in Line(run, m, "piece-y", owner, 10, i, 10, i)) yield return w;
            foreach (var w in Line(run, m, "drag", owner, 1, 4, 7, 4)) yield return w;
            // Double row: a filled 2x2 is built and becomes a checkerboard.
            foreach (var w in Line(run, m, "2x2", owner, 1, 8, 2, 8)) yield return w;
            foreach (var w in Line(run, m, "2x2", owner, 1, 9, 1, 9)) yield return w;
            foreach (var w in Line(run, m, "2x2", owner, 2, 9, 2, 9)) yield return w;
            // Diagonal row (a screen-horizontal wall): allowed, pieces alternate high/low.
            foreach (var w in Line(run, m, "diagonal", owner, 5, 8, 5, 8)) yield return w;
            foreach (var w in Line(run, m, "diagonal", owner, 6, 9, 6, 9)) yield return w;
            foreach (var w in Line(run, m, "diagonal", owner, 7, 10, 7, 10)) yield return w;
            // One-tile stairs: every piece is a corner and follows the checkerboard.
            foreach (var w in Line(run, m, "stairs", owner, 10, 10, 10, 10)) yield return w;
            foreach (var w in Line(run, m, "stairs", owner, 11, 10, 11, 10)) yield return w;
            foreach (var w in Line(run, m, "stairs", owner, 11, 11, 11, 11)) yield return w;
            foreach (var w in Line(run, m, "stairs", owner, 12, 11, 12, 11)) yield return w;
            foreach (var w in Line(run, m, "stairs", owner, 12, 12, 12, 12)) yield return w;
            // Long L shape stays allowed.
            foreach (var w in Line(run, m, "L", owner, 1, 14, 6, 14)) yield return w;
            foreach (var w in Line(run, m, "L", owner, 1, 15, 1, 20)) yield return w;
            // Attack row for ladders and the siege tower; open ground lies north of it.
            foreach (var w in Line(run, m, "ladder-row", owner, 1, 24, 5, 24)) yield return w;
            foreach (var w in Line(run, m, "tower-row", owner, 7, 24, 12, 24)) yield return w;
        }

        private IEnumerable<AutomationWait> Heights(AutomationRun run)
        {
            var m = run.CurrentMission;
            foreach (var w in Settle(run, m)) yield return w;
            string result = run.InSimulation(m, () => CheckHeights("heights"));
            log(result);
            Drain();
            Require(crenelSeen.Any(l => l.Contains("CRENEL_ADOPT_EXISTING kind=map")), "option align existing on map start did not run");
        }

        private IEnumerable<AutomationWait> Assassins(AutomationRun run)
        {
            var m = run.CurrentMission;
            // A: low endpoint of the piece-by-piece row (the exploit made endpoints high).
            // Both ends of a 7-tile line share one parity; row and column ends have opposite parity.
            var ends = new[] { (1, 1, 1, 0), (7, 1, 7, 0), (10, 1, 9, 1), (10, 7, 9, 7) };
            var end = ends.FirstOrDefault(e => Low(e.Item1, e.Item2));
            Require(end != default, "no low endpoint in the piece-by-piece row or column");
            foreach (var w in Climb(run, m, "low-endpoint", end.Item3, end.Item4, end.Item1, end.Item2, true)) yield return w;
            // B: low middle of the piece-by-piece column.
            int by = Enumerable.Range(2, 5).First(y => Low(10, y));
            foreach (var w in Climb(run, m, "low-middle", 9, by, 10, by, true)) yield return w;
            // C: negative control, a high middle of the dragged row is not enterable.
            int hx = Enumerable.Range(2, 5).First(x => !Low(x, 4));
            foreach (var w in Climb(run, m, "high-middle", hx, 3, hx, 4, false)) yield return w;
        }

        private IEnumerable<AutomationWait> Save(AutomationRun run)
        {
            var m = run.CurrentMission;
            foreach (var w in run.SetPause(m, true)) yield return w;
            run.InSimulation(m, () => { snapshot.Clear(); foreach (int id in humanTiles) snapshot[id] = Flags(id); return 0; });
            save = Path.Combine(ConfigSettings.GetSavesPath(), "SerpAcceptance-" + runId + ".sav");
            Require(!File.Exists(save), "run-owned save already exists");
            yield return AutomationWait.Until("native safe save", () => Director.instance.SafeToSave());
            Director.instance.FinishedSaving();
            var screen = CameraControls2D.instance.getScreenXYForSaveCentring(1, 1); int x = -1, y = -1;
            GameMap.instance.getRotationCentre(ref x, ref y);
            Require(EngineInterface.SaveSaveGame(save, screen.x, screen.y, x, y, false, false, false), "native save writer rejected");
            yield return AutomationWait.Until("save file written", () => File.Exists(save) && new FileInfo(save).Length > 0);
            Drain();
            Require(crenelSeen.Any(l => l.Contains("CRENEL_SAVE tiles=" + humanTiles.Count + ";")), "CRENEL_SAVE does not list " + humanTiles.Count + " managed tiles");
            log("SAVE_PASS path=" + save + " managed=" + humanTiles.Count);
            SetExisting(false, true, false); // the reloaded session aligns the savegame's unmanaged crenels
        }

        private IEnumerable<AutomationWait> Reload(AutomationRun run)
        {
            Drain(); crenelSeen.Clear();
            MainViewModel.Instance.InitNewScene(Enums.SceneIDS.FrontEnd);
            foreach (var w in run.LoadSave(save)) yield return w;
        }

        private IEnumerable<AutomationWait> AfterLoad(AutomationRun run)
        {
            var m = run.CurrentMission;
            yield return run.WaitForPlayState(m);
            if (MainViewModel.Instance.Show_HUD_Briefing) MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
            yield return AutomationWait.Until("briefing closed", () => !MainViewModel.Instance.Show_HUD_Briefing);
            foreach (var w in run.SetPause(m, false)) yield return w;
            yield return run.WaitForTicks(m, 30);
            foreach (var w in run.SetPause(m, true)) yield return w;
            Director.instance.SetEngineFrameRate(250);
            Drain();
            Require(crenelSeen.Any(l => l.Contains("CRENEL_LOAD tiles=" + humanTiles.Count + ";")),
                "CRENEL_LOAD did not restore " + humanTiles.Count + " managed tiles: " + string.Join(" | ", crenelSeen.Where(l => l.Contains("CRENEL_LOAD"))));
            int changed = run.InSimulation(m, () => snapshot.Count(p => Flags(p.Key) != p.Value));
            Require(changed == 0, changed + " managed tiles changed flags across save/load");
            Require(crenelSeen.Any(l => l.Contains("CRENEL_ADOPT_EXISTING kind=save")), "option align existing on savegame load did not run");
            // Settings are locked during a match; the runner restores the original settings afterwards.
            log("ADOPT_SAVE " + string.Join(" | ", crenelSeen.Where(l => l.Contains("CRENEL_ADOPT_EXISTING"))));
            // SE wall cost multipliers are not part of the save; restore the test supply.
            run.InSimulation(m, () => { GameBuildingManagerAPI.Instance.SetLowWallCostMultiplier(0f); GameBuildingManagerAPI.Instance.SetHighWallCostMultiplier(0f); return 0; });
            foreach (var w in Stock(run, m)) yield return w;
            // Extending a loaded managed row must count the restored set.
            int before = humanTiles.Count;
            // Vanilla refuses a tile occupied by a unit; extend a loaded managed row at a free end.
            var ends = new[] { (8, 1), (0, 1), (0, 4), (8, 4) };
            var free = run.InSimulation(m, () => ends.Where(e => GameTileManagerAPI.Instance.IsTileWalkableAndUnoccupied(Tile(e.Item1 + ox, e.Item2 + oy))).ToList());
            log("AFTER_LOAD_CANDIDATES " + string.Join(" ", ends.Select(e => (e.Item1 + ox) + "," + (e.Item2 + oy) + (free.Contains(e) ? ":free" : ":occupied"))));
            Require(free.Count > 0, "after-load: no free end tile to extend a managed row");
            foreach (var w in Line(run, m, "after-load", owner, free[0].Item1, free[0].Item2, free[0].Item1, free[0].Item2)) yield return w;
            Drain();
            Require(crenelSeen.Any(l => l.Contains("CRENEL_BUILD") && l.Contains("managed=" + (before + 1) + ".")), "managed set after load did not grow to " + (before + 1));
            foreach (var w in Settle(run, m)) yield return w;
            log(run.InSimulation(m, () => CheckHeights("after-load")));
            log("RELOAD_PASS managed=" + humanTiles.Count + " flagsUnchanged=" + snapshot.Count);
        }

        private IEnumerable<AutomationWait> Control(AutomationRun run)
        {
            var m = run.CurrentMission;
            // Since the height/blockade split the mod also manages new AI builds. A piece-by-piece AI
            // row would get Vanilla's sticky highs; managed, every piece must end at its parity height.
            // CRENEL_GRAPHICS_CONFIRMED proves that render maintenance ran (low pieces drawn through the
            // hook). Vanilla's sticky behaviour itself is proven by the crenel-vanilla-topology scenario.
            foreach (var w in Support(run, m, ai, cx, cy + 1, cx + 8, cy + 1, true)) yield return w;
            for (int i = 1; i <= 7; i++)
                foreach (var w in Line(run, m, "vanilla-ai", ai, cx + i, cy, cx + i, cy, absolute: true)) yield return w;
            foreach (var w in Settle(run, m)) yield return w;
            var positions = new List<string>();
            string result = run.InSimulation(m, () =>
            {
                var tiles = GameTileManagerAPI.Instance; int mismatch = 0; var cells = new List<string>();
                foreach (int id in controlTiles)
                {
                    XY(id, out int x, out int y); positions.Add(x + "," + y);
                    bool high = (Flags(id) & (uint)TilePropertyFlag.CrenelatedHigh) != 0;
                    if (high != Parity(x, y)) mismatch++;
                    cells.Add(x + "," + y + (high ? ":H" : ":L"));
                }
                Require(mismatch == 0, "managed AI control row kept " + mismatch + " sticky or wrong heights");
                return "CONTROL_PASS aiRowAtParity=" + controlTiles.Count + " cells=" + string.Join(" ", cells);
            });
            Drain();
            Require(graphicsConfirmed, "render maintenance not proven: graphics hook never selected a low piece");
            Require(positions.All(p => crenelSeen.Any(l => l.Contains("CRENEL_BUILD owner=" + ai + "; line=" + p + "->" + p + ";"))), "AI control row was not managed");
            log(result);
        }

        // ---------- helpers ----------
        private IEnumerable<AutomationWait> Support(AutomationRun run, MissionContext m, int player, int x0, int y0, int x1, int y1, bool absolute)
        {
            if (!absolute) { x0 += ox; y0 += oy; x1 += ox; y1 += oy; }
            foreach (var w in run.SetPause(m, true)) yield return w;
            int fx = x0, fy = y0, tx = x1, ty = y1;
            run.InSimulation(m, () =>
            {
                GameBuildingManagerAPI.Instance.CreateWall(player, fx, fy, tx, ty, eMappers.MAPPER_WALL);
                foreach (int id in Raster(fx, fy, tx, ty))
                    Require(IsWall(id) && !IsCrenel(id), "support wall tile " + Describe(id) + " missing for player " + player);
                return 0;
            });
            log("SUPPORT player=" + player + " line=" + fx + "," + fy + "->" + tx + "," + ty);
            foreach (var w in run.SetPause(m, false)) yield return w;
            yield return run.WaitForTicks(m, 4);
        }

        private IEnumerable<AutomationWait> Line(AutomationRun run, MissionContext m, string label, int player,
            int x0, int y0, int x1, int y1, bool absolute = false)
        {
            if (!absolute) { x0 += ox; y0 += oy; x1 += ox; y1 += oy; }
            foreach (var w in run.SetPause(m, true)) yield return w;
            Drain();
            int fx = x0, fy = y0, tx = x1, ty = y1;
            string result = run.InSimulation(m, () =>
            {
                var cells = Raster(fx, fy, tx, ty);
                var fresh = cells.Where(id => !IsWall(id)).ToList();
                GameBuildingManagerAPI.Instance.CreateWall(player, fx, fy, tx, ty, eMappers.MAPPER_CRENAL);
                foreach (int id in fresh)
                {
                    Require(IsCrenel(id) && Owner(id) == player, label + ": tile " + Describe(id) + " was not built for player " + player);
                    if (player == owner) humanTiles.Add(id); else controlTiles.Add(id);
                }
                return "BUILD " + label + " line=" + fx + "," + fy + "->" + tx + "," + ty + " added=" + fresh.Count;
            });
            Drain();
            log(result);
            foreach (var w in run.SetPause(m, false)) yield return w;
            yield return run.WaitForTicks(m, 8); // Vanilla render maintenance between single pieces.
        }

        private IEnumerable<AutomationWait> Settle(AutomationRun run, MissionContext m)
        {
            foreach (var w in run.SetPause(m, false)) yield return w;
            yield return run.WaitForTicks(m, 80);
            foreach (var w in run.SetPause(m, true)) yield return w;
        }

        private string CheckHeights(string stage)
        {
            int checkedCount = 0, special = 0, low = 0; var bad = new List<string>();
            foreach (int id in humanTiles)
            {
                Require(IsCrenel(id), stage + ": managed tile vanished " + Describe(id));
                if (Damaged(id)) continue;
                XY(id, out int x, out int y);
                int mask = Mask(x, y);
                // Independent oracle of CrenelatedWallTest's rule: map checkerboard for every crenel;
                // pieces without axis but with diagonal neighbours alternate along x.
                bool middle = mask == 0b0101 || mask == 0b1010;
                bool expected = mask == 0 && DiagonalMask(x, y) != 0 ? (x & 1) != 0 : Parity(x, y);
                bool high = (Flags(id) & (uint)TilePropertyFlag.CrenelatedHigh) != 0;
                checkedCount++; if (!middle) special++; if (!high) low++;
                if (high != expected) bad.Add(Describe(id) + " mask=" + mask + " expected=" + (expected ? "H" : "L"));
            }
            Require(bad.Count == 0, stage + ": wrong heights " + string.Join(" | ", bad));
            return "HEIGHTS_PASS stage=" + stage + " checked=" + checkedCount + " special=" + special + " low=" + low;
        }

        private IEnumerable<AutomationWait> Climb(AutomationRun run, MissionContext m, string label, int sx, int sy, int tx, int ty, bool expectArrive)
        {
            sx += ox; sy += oy; tx += ox; ty += oy;
            foreach (var w in run.SetPause(m, true)) yield return w;
            int unit = run.InSimulation(m, () => Spawn(sx, sy));
            uint global = run.InSimulation(m, () => Global(unit));
            foreach (var w in run.SetPause(m, false)) yield return w;
            yield return run.WaitForTicks(m, 8);
            foreach (var w in run.SetPause(m, true)) yield return w;
            run.InSimulation(m, () => GamePlayerManagerAPI.Instance.SetSelectedChimps(new List<int> { unit }));
            foreach (var w in run.SetPause(m, false)) yield return w;
            yield return run.WaitForTicks(m, 8);
            foreach (var w in run.SetPause(m, true)) yield return w;
            bool sent = run.InSimulation(m, () => Command(unit, global, tx, ty));
            if (!sent && !expectArrive)
            {
                // Vanilla refuses a move order onto an unenterable high crenel outright.
                log("CLIMB_PASS " + label + " arrived=False commandRejectedByVanilla=True target=" + run.InSimulation(m, () => Describe(Tile(tx, ty))));
                run.InSimulation(m, () => { Kill(unit); return 0; });
                yield break;
            }
            Require(sent, label + ": move command rejected");
            string state = "";
            bool done = false;
            for (int i = 0; i < 60 && !done; i++)
            {
                foreach (var w in run.SetPause(m, false)) yield return w;
                yield return run.WaitForTicks(m, 40);
                foreach (var w in run.SetPause(m, true)) yield return w;
                done = run.InSimulation(m, () => Settled(unit, global, out state)) && (i >= 3 || state.StartsWith(tx + "," + ty + ":"));
            }
            bool arrived = state.StartsWith(tx + "," + ty + ":");
            log("CLIMB " + label + " target=" + tx + "," + ty + " targetTile=" + run.InSimulation(m, () => Describe(Tile(tx, ty))) + " settled=" + done + " unit=" + state);
            Require(done, label + ": assassin never settled: " + state);
            Require(arrived == expectArrive, label + ": arrival=" + arrived + " expected=" + expectArrive);
            log("CLIMB_PASS " + label + " arrived=" + arrived);
            run.InSimulation(m, () => { Kill(unit); return 0; });
        }

        private unsafe int KillAttackers()
        {
            var ids = new List<int>(); var units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
            for (int i = 0; i < units.Length; i++)
                if (UnitAccess.IsReallyAlive(in units[i]) && units[i].r_ControllableForPlayerId == attacker) ids.Add(i + 1);
            foreach (int id in ids) Kill(id);
            return ids.Count;
        }

        private static unsafe void Kill(int unit)
        {
            if (APIShared.UnitAccess.TryGetById(unit, out GameUnit* _, out _))
                GameUnitManagerAPI.Instance.KillUnit(unit);
        }

        // ---------- enemy siege: ladders and siege tower ----------
        // Ladder target choice 0xDC900 only accepts walls of a different owner, so the attackers
        // are AI units in tribes created here; the walls are the human's managed crenels.
        // A failed siege scenario is recorded instead of aborting, so both are always exercised.
        private readonly List<string> siegeFailures = new List<string>();
        private void Fail(string message) { siegeFailures.Add(message); log("SIEGE_FAIL " + message); }

        private IEnumerable<AutomationWait> Ladder(AutomationRun run)
        {
            var m = run.CurrentMission;
            int lx = Enumerable.Range(2, 3).First(x => Low(x, 24));
            int tx = lx + ox, ty = 24 + oy, target = 0;
            foreach (var w in run.SetPause(m, true)) yield return w;
            // Causal proof: the spearman in front of the wall cannot reach the crenel before the
            // ladder exists, and reaches it afterwards. Ladder flags are logged as evidence only.
            int climber = 0; uint climberGlobal = 0;
            run.InSimulation(m, () => { Squad(attacker, eChimps.CHIMP_TYPE_SPEARMAN, 1, lx + 1, 21, out var ids); climber = ids[0]; climberGlobal = Global(climber); target = Tile(tx, ty); return 0; });
            foreach (var w in Initialize(run, m)) yield return w;
            int climberTribe = run.InSimulation(m, () => TribeOf(climber, climberGlobal));
            bool before = run.InSimulation(m, () => GameTribeManagerAPI.Instance.IssueMoveHereCommand(climberTribe, tx, ty));
            log("LADDER_BEFORE moveAccepted=" + before + " target=" + run.InSimulation(m, () => Describe(target)));
            if (before) { Fail("ladder: crenel was reachable before any ladder; proof impossible"); yield break; }
            int man = 0; uint manGlobal = 0;
            int ladderTribe = run.InSimulation(m, () => { int t = Squad(attacker, eChimps.CHIMP_TYPE_LADDERMAN, 1, lx, 22, out var ids); man = ids[0]; manGlobal = Global(man); return t; });
            foreach (var w in Initialize(run, m)) yield return w;
            log("LADDER_START man=" + run.InSimulation(m, () => UnitState(man, manGlobal)));
            bool ordered = false;
            for (int i = 0; i < 90 && !ordered; i++)
            {
                if (i % 20 == 0) log("LADDER_ORDER attach=" + run.InSimulation(m, () => GameTribeManagerAPI.Instance.AttachLadderToWall(ladderTribe, target)) + " i=" + i);
                foreach (var w in run.SetPause(m, false)) yield return w;
                yield return run.WaitForTicks(m, 40);
                foreach (var w in run.SetPause(m, true)) yield return w;
                ordered = run.InSimulation(m, () => GameTribeManagerAPI.Instance.IssueMoveHereCommand(climberTribe, tx, ty));
                if (ordered || i % 10 == 9)
                    log("LADDER_WAIT i=" + i + " climberOrder=" + ordered + " man=" + run.InSimulation(m, () => UnitState(man, manGlobal)) +
                        " ladderFlags=" + (run.InSimulation(m, () => FindFlag(-1, 0, 14, 26, 0x800000u)) ?? "none"));
            }
            if (!ordered) { Fail("ladder: crenel never became reachable after the ladder order; man=" + run.InSimulation(m, () => UnitState(man, manGlobal))); yield break; }
            string state = ""; bool arrived = false;
            for (int i = 0; i < 90 && !arrived; i++)
            {
                foreach (var w in run.SetPause(m, false)) yield return w;
                yield return run.WaitForTicks(m, 40);
                foreach (var w in run.SetPause(m, true)) yield return w;
                arrived = run.InSimulation(m, () => Settled(climber, climberGlobal, out state)) && state.StartsWith(tx + "," + ty + ":");
            }
            log("ENTER ladder target=" + run.InSimulation(m, () => Describe(target)) + " unit=" + state);
            if (!arrived) { Fail("ladder: spearman did not reach the low crenel " + tx + "," + ty + "; unit=" + state); yield break; }
            log("ENTER_PASS ladder arrived=True unreachableBeforeLadder=True");
        }

        private IEnumerable<AutomationWait> SiegeTower(AutomationRun run)
        {
            var m = run.CurrentMission;
            foreach (var w in run.SetPause(m, true)) yield return w;
            int climber = 0; uint climberGlobal = 0;
            run.InSimulation(m, () => { Squad(attacker, eChimps.CHIMP_TYPE_SPEARMAN, 1, 12, 13, out var ids); climber = ids[0]; climberGlobal = Global(climber); return 0; });
            foreach (var w in Initialize(run, m)) yield return w;
            int probeTribe = run.InSimulation(m, () => TribeOf(climber, climberGlobal));
            var segment = Enumerable.Range(7, 6).Where(x => Low(x, 24)).ToList();
            bool before = segment.Any(x => run.InSimulation(m, () => GameTribeManagerAPI.Instance.IssueMoveHereCommand(probeTribe, x + ox, 24 + oy)));
            log("TOWER_BEFORE anyLowReachable=" + before + " lows=" + string.Join(" ", segment.Select(x => (x + ox) + "," + (24 + oy))));
            if (before) { Fail("siege: tower segment was reachable before the tower; proof impossible"); yield break; }
            int tower = 0; uint towerGlobal = 0;
            List<int> crewIds = null; uint crewGlobal = 0;
            int crew = run.InSimulation(m, () =>
            {
                tower = SpawnUnit(attacker, 9, 17, eChimps.CHIMP_TYPE_SIEGE_TOWER); towerGlobal = Global(tower);
                int t = Squad(attacker, eChimps.CHIMP_TYPE_ENGINEER, 4, 9, 15, out crewIds); crewGlobal = Global(crewIds[0]); return t;
            });
            foreach (var w in Initialize(run, m)) yield return w;
            bool manOrder = run.InSimulation(m, () => GameTribeManagerAPI.Instance.ManSiegeEquipment(crew, tower));
            log("TOWER_ORDER man=" + manOrder + " tower=" + run.InSimulation(m, () => UnitState(tower, towerGlobal)));
            if (!manOrder) { Fail("siege: ManSiegeEquipment rejected"); yield break; }
            bool manned = false;
            for (int i = 0; i < 60 && !manned; i++)
            {
                foreach (var w in run.SetPause(m, false)) yield return w;
                yield return run.WaitForTicks(m, 40);
                foreach (var w in run.SetPause(m, true)) yield return w;
                manned = run.InSimulation(m, () => Engineers(tower, towerGlobal) >= 4);
                if (i % 10 == 9) log("TOWER_MAN_WAIT i=" + i + " engineers=" + run.InSimulation(m, () => Engineers(tower, towerGlobal)) +
                    " tower=" + run.InSimulation(m, () => UnitState(tower, towerGlobal)) + " crew0=" + run.InSimulation(m, () => UnitState(crewIds[0], crewGlobal)));
            }
            if (!manned) { Fail("siege: tower was not manned by four engineers; assigned=" + run.InSimulation(m, () => Engineers(tower, towerGlobal))); yield break; }
            log("TOWER_MANNED engineers=" + run.InSimulation(m, () => Engineers(tower, towerGlobal)) + " tower=" + run.InSimulation(m, () => UnitState(tower, towerGlobal)));
            // A manned tower has no tribe of its own; give it one, as a player selection would.
            int towerTribe = run.InSimulation(m, () => TowerTribe(tower, towerGlobal));
            int tx = 8;
            bool attack = run.InSimulation(m, () => GameTribeManagerAPI.Instance.AttackWall(towerTribe, Tile(tx + ox, 24 + oy)));
            log("TOWER_ORDER attackWall=" + attack + " tribe=" + towerTribe);
            if (!attack) { Fail("siege: AttackWall rejected"); yield break; }
            string docked = null;
            for (int i = 0; i < 120 && docked == null; i++)
            {
                foreach (var w in run.SetPause(m, false)) yield return w;
                yield return run.WaitForTicks(m, 40);
                foreach (var w in run.SetPause(m, true)) yield return w;
                docked = run.InSimulation(m, () => FindFlag(0, 17, 13, 24, 0x10000000u));
                if (i % 15 == 14) log("TOWER_WAIT i=" + i + " tower=" + run.InSimulation(m, () => UnitState(tower, towerGlobal)));
            }
            if (docked == null) { Fail("siege: tower never docked (no 0x10000000 tile); tower=" + run.InSimulation(m, () => UnitState(tower, towerGlobal))); yield break; }
            int dockX = run.InSimulation(m, () => FlagCenterX(0, 17, 13, 24, 0x10000000u)) - ox;
            log("TOWER_DOCKED target=" + (tx + ox) + "," + (24 + oy) + " tiles=" + docked + " centerX=" + (dockX + ox));
            // Troops leave the tower onto a non-high tile among the five wall tiles in front of it.
            var front = Enumerable.Range(dockX - 2, 5).Where(x => x >= 7 && x <= 12).ToList();
            log("TOWER_FRONT " + string.Join(" ", front.Select(x => (x + ox) + "," + (24 + oy) + (Low(x, 24) ? ":L" : ":H"))));
            if (!front.Any(x => Low(x, 24))) { Fail("siege: no low crenel among the five wall tiles in front of the tower"); yield break; }
            int target = front.First(x => Low(x, 24));
            foreach (var w in Enter(run, m, "siege-tower", climber, climberGlobal, target, 24)) yield return w;
        }

        // Orders given to a unit that is still NeedsInit are dropped by Vanilla.
        private IEnumerable<AutomationWait> Initialize(AutomationRun run, MissionContext m)
        {
            foreach (var w in run.SetPause(m, false)) yield return w;
            yield return run.WaitForTicks(m, 10);
            foreach (var w in run.SetPause(m, true)) yield return w;
        }

        private IEnumerable<AutomationWait> SiegeSummary(AutomationRun run)
        {
            Require(siegeFailures.Count == 0, "siege scenarios failed: " + string.Join(" | ", siegeFailures));
            log("SIEGE_PASS ladder and siege tower");
            // Vanilla forbids building near enemy troops; remove the attackers before the save/load part.
            var m = run.CurrentMission;
            foreach (var w in run.SetPause(m, true)) yield return w;
            int killed = run.InSimulation(m, KillAttackers);
            foreach (var w in run.SetPause(m, false)) yield return w;
            yield return run.WaitForTicks(m, 40);
            log("SIEGE_CLEANUP attackerUnitsKilled=" + killed);
        }

        private IEnumerable<AutomationWait> Enter(AutomationRun run, MissionContext m, string label, int unit, uint global, int x, int y)
        {
            int tx = x + ox, ty = y + oy;
            foreach (var w in Initialize(run, m)) yield return w;
            log("ENTER_START " + label + " unit=" + run.InSimulation(m, () => UnitState(unit, global)));
            int tribe = run.InSimulation(m, () => TribeOf(unit, global));
            bool ordered = false;
            for (int r = 0; r < 20 && !ordered; r++)
            {
                ordered = run.InSimulation(m, () => GameTribeManagerAPI.Instance.IssueMoveHereCommand(tribe, tx, ty));
                if (ordered) { log("ENTER_ORDER " + label + " attempt=" + r); break; }
                foreach (var w in run.SetPause(m, false)) yield return w;
                yield return run.WaitForTicks(m, 40);
                foreach (var w in run.SetPause(m, true)) yield return w;
            }
            if (!ordered) { Fail(label + ": move onto low crenel rejected 20 times; unit=" + run.InSimulation(m, () => UnitState(unit, global))); yield break; }
            string state = ""; bool arrived = false;
            for (int i = 0; i < 90 && !arrived; i++)
            {
                foreach (var w in run.SetPause(m, false)) yield return w;
                yield return run.WaitForTicks(m, 40);
                foreach (var w in run.SetPause(m, true)) yield return w;
                arrived = run.InSimulation(m, () => Settled(unit, global, out state)) && state.StartsWith(tx + "," + ty + ":");
            }
            log("ENTER " + label + " target=" + run.InSimulation(m, () => Describe(Tile(tx, ty))) + " unit=" + state);
            if (!arrived) { Fail(label + ": spearman did not reach the low crenel " + tx + "," + ty + "; unit=" + state); yield break; }
            log("ENTER_PASS " + label + " arrived=True");
        }

        private int Squad(int player, eChimps type, int count, int x, int y, out List<int> ids)
        {
            ids = new List<int>();
            int tribe = checked((int)GameTribeManagerAPI.Instance.Create(player));
            Require(tribe > 0, "tribe creation failed for player " + player);
            for (int i = 0; i < count; i++)
            {
                int id = SpawnUnit(player, x + i, y, type);
                Require(GameTribeManagerAPI.Instance.AssignUnit(tribe, id), "assigning unit " + id + " to tribe " + tribe + " failed");
                ids.Add(id);
            }
            return tribe;
        }

        private unsafe int SpawnUnit(int player, int x, int y, eChimps type)
        {
            x += ox; y += oy;
            var tiles = GameTileManagerAPI.Instance; int tile = Tile(x, y);
            Require(tiles.IsTileWalkableAndUnoccupied(tile), "spawn tile not free " + Describe(tile));
            long id = GameUnitManagerAPI.Instance.CreateUnitLocal(player, player, x, y, tiles.GetTileHeight(tile), type);
            Require(id > 0 && id <= int.MaxValue && UnitAccess.TryGetById((int)id, out GameUnit* unit, out _) && unit != null, type + " spawn missing");
            return (int)id;
        }

        private unsafe int Engineers(int id, uint global)
        {
            GameUnit* u = Raw(id, global);
            return (u->r_AssignedEngineer1 != 0 ? 1 : 0) + (u->r_AssignedEngineer2 != 0 ? 1 : 0) + (u->r_AssignedEngineer3 != 0 ? 1 : 0) + (u->r_AssignedEngineer4 != 0 ? 1 : 0);
        }
        private unsafe int TowerTribe(int id, uint global)
        {
            GameUnit* u = Raw(id, global);
            if (u->r_TribeId > 0) return u->r_TribeId;
            int tribe = checked((int)GameTribeManagerAPI.Instance.Create(attacker));
            Require(tribe > 0 && GameTribeManagerAPI.Instance.AssignUnit(tribe, id), "siege: tower tribe could not be created");
            return tribe;
        }
        private unsafe int TribeOf(int id, uint global) { GameUnit* u = Raw(id, global); Require(u->r_TribeId > 0, "unit " + id + " has no tribe"); return u->r_TribeId; }
        private unsafe string UnitState(int id, uint global)
        { GameUnit* u = Raw(id, global); return "alive=" + UnitAccess.IsReallyAlive(u) + ":" + u->r_CurrentTilePositionX + "," + u->r_CurrentTilePositionY + ":ai=" + (int)u->r_AIState + ":tribe=" + u->r_TribeId + ":cmd=" + u->r_AI_LastIssuedTribeCommand + ":owner=" + u->r_ControllableForPlayerId; }

        private string FindFlag(int x0, int y0, int x1, int y1, uint flag)
        {
            var found = new List<string>();
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                    if ((Flags(Tile(x + ox, y + oy)) & flag) != 0) found.Add((x + ox) + "," + (y + oy));
            return found.Count == 0 ? null : string.Join(" ", found);
        }
        private int FlagCenterX(int x0, int y0, int x1, int y1, uint flag)
        {
            var xs = new List<int>();
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
                    if ((Flags(Tile(x + ox, y + oy)) & flag) != 0) xs.Add(x + ox);
            return (xs.Min() + xs.Max()) / 2;
        }

        private unsafe int Spawn(int x, int y)
        {
            var tiles = GameTileManagerAPI.Instance; int tile = Tile(x, y);
            Require(tiles.IsTileWalkableAndUnoccupied(tile), "spawn tile not free " + Describe(tile));
            long id = GameUnitManagerAPI.Instance.CreateUnitLocal(owner, owner, x, y, tiles.GetTileHeight(tile), eChimps.CHIMP_TYPE_ARAB_ASSASIN);
            Require(id > 0 && id <= int.MaxValue && UnitAccess.TryGetById((int)id, out GameUnit* unit, out _) && unit != null, "assassin spawn missing");
            return (int)id;
        }
        private unsafe uint Global(int id) { Require(UnitAccess.TryGetById(id, out GameUnit* u, out _) && u != null, "assassin missing"); return u->r_GlobalId; }
        // Identity only: a fresh spawn is still NeedsInit, and a manned tower may not count as alive.
        private unsafe GameUnit* Raw(int id, uint global)
        {
            Require(UnitAccess.TryGetById(id, out GameUnit* u, out _) && u != null && u->r_GlobalId == global, "unit " + id + " identity lost");
            return u;
        }
        private unsafe GameUnit* Unit(int id, uint global)
        {
            Require(UnitAccess.TryGetById(id, out GameUnit* u, out _) && u != null && u->r_GlobalId == global && UnitAccess.IsReallyAlive(u), "assassin identity lost");
            return u;
        }
        private unsafe bool Command(int id, uint global, int tx, int ty)
        {
            GameUnit* u = Unit(id, global); int tribe = u->r_TribeId;
            Require(tribe > 0 && GameTribeManagerAPI.Instance.TryGetTribeById(tribe, out GameTribe* group) && group->r_UnitsInGroup == 1, "selected single-assassin tribe missing");
            return GameTribeManagerAPI.Instance.IssueMoveHereCommand(tribe, tx, ty);
        }
        private unsafe bool Settled(int id, uint global, out string state)
        {
            GameUnit* u = Unit(id, global);
            bool moving = u->r_PathPlanStateBitFlags != 0 && (u->r_CurrentPathPlanIndex < u->r_PathPlanLength ||
                u->r_NextTilePositionX2 != u->r_CurrentTilePositionX || u->r_NextTilePositionY2 != u->r_CurrentTilePositionY);
            state = u->r_CurrentTilePositionX + "," + u->r_CurrentTilePositionY + ":ai=" + (int)u->r_AIState + ":moving=" + moving;
            return !moving && (int)u->r_AIState < 126;
        }

        private IEnumerable<AutomationWait> Stock(AutomationRun run, MissionContext mission)
        {
            var players = GamePlayerManagerAPI.Instance;
            // A stockpile slot takes a bounded amount, so add in small portions.
            int stone = run.InSimulation(mission, () =>
            {
                for (int i = 0; i < 12 && players.GetGoodAmount(owner, eGoods.STORED_STONE_BLOCKS) < 300; i++)
                    if (!players.TryAddGood(owner, eGoods.STORED_STONE_BLOCKS, 25)) break;
                return players.GetGoodAmount(owner, eGoods.STORED_STONE_BLOCKS);
            });
            if (stone < 50) log("STOCK_DIAG " + run.InSimulation(mission, OwnBuildings));
            Require(stone >= 50, "stone could not be stocked: " + stone);
            log("STOCK stone=" + stone);
            yield break;
        }

        private unsafe string OwnBuildings()
        {
            var rows = new List<string>(); var buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            for (int i = 0; i < buildings.Length; i++)
            {
                ref GameBuilding b = ref buildings[i];
                if (b.r_AliveState != AliveState.IsAlive || b.r_PlayerIdOwner != owner) continue;
                rows.Add(b.r_BuildingType + "@" + b.r_TilePositionXBegin + "," + b.r_TilePositionYBegin);
            }
            return "owner=" + owner + " buildings=" + string.Join(" ", rows);
        }
        private string FindAreas()
        {
            var players = GamePlayerManagerAPI.Instance;
            owner = players.GetLocalPlayerId();
            ai = Enumerable.Range(1, 8).FirstOrDefault(p => p != owner && players.IsPlayerIdValid(p) && players.IsAIPlayer(p) && players.GetPlayerKeepId(p) > 0);
            Require(owner > 0 && ai > 0, "local or AI player missing");
            // The AI absorbs freshly spawned units into its own tribes and sends them home, so
            // siege attackers belong to an unused slot without AI control (still a different owner).
            attacker = Enumerable.Range(2, 7).FirstOrDefault(p => p != ai && players.GetPlayerKeepId(p) <= 0);
            Require(attacker > 0, "no unused player slot for the siege attackers");
            Require(!players.IsAIPlayer(owner), "local player is not human");
            GameBuildingManagerAPI.Instance.SetLowWallCostMultiplier(0f);
            GameBuildingManagerAPI.Instance.SetHighWallCostMultiplier(0f);
            // Vanilla refuses wall commands without stone even at zero cost; stock both players.
            bool aiStone = players.TryAddGood(ai, eGoods.STORED_STONE_BLOCKS, 300);
            Require(Area(owner, 14, 26, out ox, out oy), "no free 14x26 area near own keep");
            // The AI door may lie in another path component; the control only needs uniform free land.
            Require(Area(ai, 9, 2, out cx, out cy, anyComponent: true), "no free 9x2 area near AI keep");
            return "owner=" + owner + " ai=" + ai + " area=" + ox + "," + oy + " control=" + cx + "," + cy + " attacker=" + attacker +
                " stone=" + players.GetGoodAmount(owner, eGoods.STORED_STONE_BLOCKS) + " aiStone=" + aiStone + ":" + players.GetGoodAmount(ai, eGoods.STORED_STONE_BLOCKS);
        }

        private bool Area(int player, int w, int h, out int bx, out int by, bool anyComponent = false)
        {
            var door = GamePlayerManagerAPI.Instance.GetPlayerKeepDoorPosition(player);
            var tiles = GameTileManagerAPI.Instance;
            Require(tiles.IsTileInsideMapBounds(door.X, door.Y), "keep door outside map");
            ushort component = GamePathingManagerAPI.Instance.GetPathComponentGrid()[tiles.GetTileId(door.X, door.Y)];
            for (int r = 6; r <= 60; r += 2)
                for (int dy = -r; dy <= r; dy += 2)
                    for (int dx = -r; dx <= r; dx += 2)
                    {
                        if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                        int x0 = door.X + dx, y0 = door.Y + dy;
                        if (Free(x0, y0, w, h, anyComponent ? (ushort)0 : component)) { bx = x0; by = y0; return true; }
                    }
            bx = by = 0; return false;
        }

        private bool Free(int x0, int y0, int w, int h, ushort component)
        {
            var tiles = GameTileManagerAPI.Instance; var grid = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            if (component == 0) { if (!tiles.IsTileInsideMapBounds(x0, y0)) return false; component = grid[tiles.GetTileId(x0, y0)]; if (component == 0) return false; }
            for (int y = y0 - 1; y <= y0 + h; y++)
                for (int x = x0 - 1; x <= x0 + w; x++)
                {
                    if (!tiles.IsTileInsideMapBounds(x, y)) return false;
                    int id = tiles.GetTileId(x, y);
                    if (!tiles.IsTileWalkableAndUnoccupied(id) || tiles.GetTileBuildingId(id) != 0 || grid[id] != component ||
                        (tiles.GetTilePropertyFlag(id) & (TilePropertyFlag.IsWall | TilePropertyFlag.IsElevated | TilePropertyFlag.IsBuilding | TilePropertyFlag.IsSwamp)) != 0) return false; // swamp is walkable but unbuildable
                }
            return true;
        }

        private bool Low(int x, int y) { int id = Tile(x + ox, y + oy); return IsCrenel(id) && (Flags(id) & (uint)TilePropertyFlag.CrenelatedHigh) == 0; }
        private static List<int> Raster(int x, int y, int ex, int ey)
        {
            var result = new List<int>();
            while (true)
            {
                result.Add(Tile(x, y));
                if (x == ex && y == ey) return result;
                x += Math.Sign(ex - x); y += Math.Sign(ey - y);
            }
        }
        // Coordinates of every tile id this fixture touched; ids come only from Tile().
        private static readonly Dictionary<int, (int, int)> coordinates = new Dictionary<int, (int, int)>();
        private static int Tile(int x, int y) { int id = GameTileManagerAPI.Instance.GetTileId(x, y); coordinates[id] = (x, y); return id; }
        private static void XY(int id, out int x, out int y) { var c = coordinates[id]; x = c.Item1; y = c.Item2; }
        private static uint Flags(int id) => (uint)GameTileManagerAPI.Instance.GetTilePropertyFlag(id);
        private static bool IsWall(int id) => (Flags(id) & (uint)TilePropertyFlag.IsWall) != 0;
        private static bool IsCrenel(int id) => IsWall(id) && (Flags(id) & (uint)TilePropertyFlag.CrenelatedLow) != 0;
        private static int Owner(int id) => (GameTileManagerAPI.Instance.GetWallOwnerLayer()[id] & 7) + 1;
        private static bool Damaged(int id) => GameTileManagerAPI.Instance.GetDamageLayer()[id] != 0 || GameTileManagerAPI.Instance.GetStructureLayer()[id] != 0;
        private static bool Parity(int x, int y) => ((x ^ y) & 1) != 0;
        private static int Mask(int x, int y)
        {
            int mask = 0, n = 0;
            foreach (var d in new[] { (0, -1), (1, 0), (0, 1), (-1, 0) })
            {
                int nx = x + d.Item1, ny = y + d.Item2;
                if (GameTileManagerAPI.Instance.IsTileInsideMapBounds(nx, ny) && IsCrenel(Tile(nx, ny))) mask |= 1 << n;
                n++;
            }
            return mask;
        }
        private static int DiagonalMask(int x, int y)
        {
            int mask = 0, n = 0;
            foreach (var d in new[] { (1, -1), (1, 1), (-1, 1), (-1, -1) })
            {
                int nx = x + d.Item1, ny = y + d.Item2;
                if (GameTileManagerAPI.Instance.IsTileInsideMapBounds(nx, ny) && IsCrenel(Tile(nx, ny))) mask |= 1 << n;
                n++;
            }
            return mask;
        }
        private static string Describe(int id)
        {
            XY(id, out int x, out int y);
            return x + "," + y + ":f=" + Flags(id).ToString("X") + ":owner=" + Owner(id);
        }
        // The graphics confirmation is logged once per process; remember it across crenelSeen.Clear().
        private bool graphicsConfirmed;
        private void Drain()
        {
            while (crenelLog.TryDequeue(out string line))
            {
                crenelSeen.Add(line);
                if (line.Contains("CRENEL_GRAPHICS_CONFIRMED")) graphicsConfirmed = true;
            }
        }
        private static object WallSettings()
        {
            var plugin = BepInEx.Bootstrap.Chainloader.PluginInfos[WallMod].Instance;
            var field = plugin.GetType().GetField("settings", BindingFlags.NonPublic | BindingFlags.Static) ?? throw new MissingFieldException("settings");
            return field.GetValue(null) ?? throw new InvalidOperationException("CrenelatedWallTest settings missing");
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
