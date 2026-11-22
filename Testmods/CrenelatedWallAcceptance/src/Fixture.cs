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
    internal sealed class Fixture
    {
        private const string WallMod = "CrenelatedWallTest_Serp";
        private readonly string runId;
        private readonly Action<string> log;
        private readonly ConcurrentQueue<string> crenelLog = new ConcurrentQueue<string>();
        private readonly List<string> crenelSeen = new List<string>();
        private readonly HashSet<int> humanTiles = new HashSet<int>();
        private readonly List<int> controlTiles = new List<int>();
        private readonly Dictionary<int, uint> snapshot = new Dictionary<int, uint>();
        private int owner, ai, ox, oy, cx, cy;
        private string save;
        private volatile bool stopped;

        internal Fixture(string runId, Action<string> log)
        {
            this.runId = runId; this.log = log;
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

        internal IEnumerable<AutomationStep> Steps()
        {
            yield return new AutomationStep("skirmish", TimeSpan.FromSeconds(240), Start);
            yield return new AutomationStep("prepare", TimeSpan.FromSeconds(90), Prepare, true);
            yield return new AutomationStep("build-and-reject", TimeSpan.FromSeconds(240), Build, true);
            yield return new AutomationStep("heights", TimeSpan.FromSeconds(60), Heights, true);
            yield return new AutomationStep("assassins", TimeSpan.FromSeconds(240), Assassins, true);
            yield return new AutomationStep("save", TimeSpan.FromSeconds(60), Save, true);
            yield return new AutomationStep("reload", TimeSpan.FromSeconds(180), Reload);
            yield return new AutomationStep("after-load", TimeSpan.FromSeconds(120), AfterLoad, true);
            yield return new AutomationStep("vanilla-control", TimeSpan.FromSeconds(120), Control, true);
        }

        // ---------- steps ----------
        private IEnumerable<AutomationWait> Start(AutomationRun run)
        {
            object settings = WallSettings();
            var property = settings.GetType().GetProperty("EnableMod");
            property.SetValue(settings, true);
            Require((bool)property.GetValue(settings), "CrenelatedWallTest EnableMod could not be set");
            log("settings EnableMod=True map=4spielerkarte.map opponent=SK_RAT peace=none (lobby offers none; short run)");
            foreach (var wait in run.StartSkirmish("4spielerkarte.map", new[] { AILords.SK_RAT })) yield return wait;
        }

        private IEnumerable<AutomationWait> Prepare(AutomationRun run)
        {
            var mission = run.CurrentMission;
            yield return run.WaitForPlayState(mission);
            if (MainViewModel.Instance.Show_HUD_Briefing) MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
            yield return AutomationWait.Until("briefing closed", () => !MainViewModel.Instance.Show_HUD_Briefing);
            foreach (var wait in run.SetPause(mission, false)) yield return wait;
            yield return run.WaitForTicks(mission, 20);
            foreach (var wait in run.SetPause(mission, true)) yield return wait;
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
            // Exploit case: straight rows placed piece by piece, simulation running between pieces.
            for (int i = 1; i <= 7; i++) foreach (var w in Line(run, m, "piece-x", owner, i, 1, i, 1, null)) yield return w;
            for (int i = 1; i <= 7; i++) foreach (var w in Line(run, m, "piece-y", owner, 10, i, 10, i, null)) yield return w;
            foreach (var w in Line(run, m, "drag", owner, 1, 4, 7, 4, null)) yield return w;
            // Double row: the closing fourth tile of a 2x2 is rejected.
            foreach (var w in Line(run, m, "2x2", owner, 1, 8, 2, 8, null)) yield return w;
            foreach (var w in Line(run, m, "2x2", owner, 1, 9, 1, 9, null)) yield return w;
            foreach (var w in Line(run, m, "2x2", owner, 2, 9, 2, 9, "double-row")) yield return w;
            // Diagonal chain: the third isolated diagonal crenel is rejected.
            foreach (var w in Line(run, m, "diagonal", owner, 5, 8, 5, 8, null)) yield return w;
            foreach (var w in Line(run, m, "diagonal", owner, 6, 9, 6, 9, null)) yield return w;
            foreach (var w in Line(run, m, "diagonal", owner, 7, 10, 7, 10, "three diagonal")) yield return w;
            // One-tile stairs: the third connected special piece is rejected.
            foreach (var w in Line(run, m, "stairs", owner, 10, 10, 10, 10, null)) yield return w;
            foreach (var w in Line(run, m, "stairs", owner, 11, 10, 11, 10, null)) yield return w;
            foreach (var w in Line(run, m, "stairs", owner, 11, 11, 11, 11, null)) yield return w;
            foreach (var w in Line(run, m, "stairs", owner, 12, 11, 12, 11, null)) yield return w;
            foreach (var w in Line(run, m, "stairs", owner, 12, 12, 12, 12, "three special")) yield return w;
            // Long L shape stays allowed.
            foreach (var w in Line(run, m, "L", owner, 1, 14, 6, 14, null)) yield return w;
            foreach (var w in Line(run, m, "L", owner, 1, 15, 1, 20, null)) yield return w;
        }

        private IEnumerable<AutomationWait> Heights(AutomationRun run)
        {
            var m = run.CurrentMission;
            foreach (var w in Settle(run, m)) yield return w;
            string result = run.InSimulation(m, () => CheckHeights("heights"));
            log(result);
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
            // Extending a loaded managed row must count the restored set.
            int before = humanTiles.Count;
            foreach (var w in Line(run, m, "after-load", owner, 8, 1, 8, 1, null)) yield return w;
            Drain();
            Require(crenelSeen.Any(l => l.Contains("CRENEL_BUILD") && l.Contains("managed=" + (before + 1) + ".")), "managed set after load did not grow to " + (before + 1));
            foreach (var w in Settle(run, m)) yield return w;
            log(run.InSimulation(m, () => CheckHeights("after-load")));
            log("RELOAD_PASS managed=" + humanTiles.Count + " flagsUnchanged=" + snapshot.Count);
        }

        private IEnumerable<AutomationWait> Control(AutomationRun run)
        {
            var m = run.CurrentMission;
            // The AI's own walls are untouched by the mod: piece by piece they show Vanilla's sticky
            // high bit. This proves the render maintenance ran during the measured windows.
            foreach (var w in Support(run, m, ai, cx, cy + 1, cx + 8, cy + 1, true)) yield return w;
            for (int i = 1; i <= 7; i++)
                foreach (var w in Line(run, m, "vanilla-ai", ai, cx + i, cy, cx + i, cy, null, absolute: true)) yield return w;
            foreach (var w in Settle(run, m)) yield return w;
            string result = run.InSimulation(m, () =>
            {
                var tiles = GameTileManagerAPI.Instance; int mismatch = 0; var cells = new List<string>();
                foreach (int id in controlTiles)
                {
                    XY(id, out int x, out int y);
                    bool high = (Flags(id) & (uint)TilePropertyFlag.CrenelatedHigh) != 0;
                    if (high != Parity(x, y)) mismatch++;
                    cells.Add(x + "," + y + (high ? ":H" : ":L"));
                }
                Require(mismatch > 0, "Vanilla control row shows no sticky high bit; render maintenance not proven");
                return "CONTROL_PASS vanillaStickyHigh=" + mismatch + "/" + controlTiles.Count + " cells=" + string.Join(" ", cells);
            });
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
            int x0, int y0, int x1, int y1, string reject, bool absolute = false)
        {
            if (!absolute) { x0 += ox; y0 += oy; x1 += ox; y1 += oy; }
            // The mod rate-limits rejection logs per 5 s; wait so every rejection is visible.
            if (reject != null) yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(5600));
            foreach (var w in run.SetPause(m, true)) yield return w;
            Drain();
            int fx = x0, fy = y0, tx = x1, ty = y1;
            string result = run.InSimulation(m, () =>
            {
                var cells = Raster(fx, fy, tx, ty);
                var fresh = cells.Where(id => !IsWall(id)).ToList();
                int stone = GamePlayerManagerAPI.Instance.GetGoodAmount(player, eGoods.STORED_STONE_BLOCKS);
                GameBuildingManagerAPI.Instance.CreateWall(player, fx, fy, tx, ty, eMappers.MAPPER_CRENAL);
                if (reject == null)
                {
                    foreach (int id in fresh)
                    {
                        Require(IsCrenel(id) && Owner(id) == player, label + ": tile " + Describe(id) + " was not built for player " + player);
                        if (player == owner) humanTiles.Add(id); else controlTiles.Add(id);
                    }
                    return "BUILD " + label + " line=" + fx + "," + fy + "->" + tx + "," + ty + " added=" + fresh.Count;
                }
                foreach (int id in fresh) Require(!IsWall(id), label + ": rejected tile " + Describe(id) + " was built");
                int after = GamePlayerManagerAPI.Instance.GetGoodAmount(player, eGoods.STORED_STONE_BLOCKS);
                Require(after == stone, label + ": stone changed " + stone + "->" + after);
                return "REJECT " + label + " line=" + fx + "," + fy + "->" + tx + "," + ty + " stone=" + stone;
            });
            Drain();
            if (reject != null)
                Require(crenelSeen.Any(l => l.Contains("CRENEL_REJECT owner=") && l.Contains(reject)), label + ": no CRENEL_REJECT with reason '" + reject + "'");
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
                bool line = mask == 0b0101 || mask == 0b1010 || mask == 1 || mask == 2 || mask == 4 || mask == 8;
                bool expected = line ? Parity(x, y) : true;
                bool high = (Flags(id) & (uint)TilePropertyFlag.CrenelatedHigh) != 0;
                checkedCount++; if (!line) special++; if (!high) low++;
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

        private static unsafe void Kill(int unit)
        {
            if (APIShared.UnitAccess.TryGetById(unit, out GameUnit* _, out _))
                GameUnitManagerAPI.Instance.KillUnit(unit);
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
            Require(!players.IsAIPlayer(owner), "local player is not human");
            GameBuildingManagerAPI.Instance.SetLowWallCostMultiplier(0f);
            GameBuildingManagerAPI.Instance.SetHighWallCostMultiplier(0f);
            // Vanilla refuses wall commands without stone even at zero cost; stock both players.
            bool aiStone = players.TryAddGood(ai, eGoods.STORED_STONE_BLOCKS, 300);
            Require(Area(owner, 14, 21, out ox, out oy), "no free 14x21 area near own keep");
            // The AI door may lie in another path component; the control only needs uniform free land.
            Require(Area(ai, 9, 2, out cx, out cy, anyComponent: true), "no free 9x2 area near AI keep");
            return "owner=" + owner + " ai=" + ai + " area=" + ox + "," + oy + " control=" + cx + "," + cy +
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
                        (tiles.GetTilePropertyFlag(id) & (TilePropertyFlag.IsWall | TilePropertyFlag.IsElevated | TilePropertyFlag.IsBuilding)) != 0) return false;
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
        private static string Describe(int id)
        {
            XY(id, out int x, out int y);
            return x + "," + y + ":f=" + Flags(id).ToString("X") + ":owner=" + Owner(id);
        }
        private void Drain() { while (crenelLog.TryDequeue(out string line)) crenelSeen.Add(line); }
        private static object WallSettings()
        {
            var plugin = BepInEx.Bootstrap.Chainloader.PluginInfos[WallMod].Instance;
            var field = plugin.GetType().GetField("settings", BindingFlags.NonPublic | BindingFlags.Static) ?? throw new MissingFieldException("settings");
            return field.GetValue(null) ?? throw new InvalidOperationException("CrenelatedWallTest settings missing");
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
