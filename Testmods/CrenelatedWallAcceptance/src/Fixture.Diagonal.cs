using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using APIShared;
using APIShared.Automation;
using HarmonyLib;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace CrenelatedWallAcceptance
{
    // Evidence only (scenario crenel-diagonal-walk): can units walk diagonally from a low crenel to
    // the next low crenel when both tiles between them are high crenels? A 4x4 block and a 2x5 band
    // are forced into a checkerboard. The mod's height adjustment is off; a Harmony postfix on the
    // mod's straight predicate keeps the forced low tiles drawn as straight pieces, so the renderer
    // does not raise them again. Interior low tiles of the block are reachable only diagonally.
    internal sealed partial class Fixture
    {
        private IEnumerable<AutomationStep> DiagonalWalkSteps()
        {
            yield return new AutomationStep("skirmish", TimeSpan.FromSeconds(240), DiagonalStart);
            yield return new AutomationStep("prepare", TimeSpan.FromSeconds(90), Prepare, true);
            yield return new AutomationStep("diag-build", TimeSpan.FromSeconds(240), DiagonalBuild, true);
            yield return new AutomationStep("diag-assassin", TimeSpan.FromSeconds(240), DiagonalAssassin, true);
            yield return new AutomationStep("diag-ladder", TimeSpan.FromSeconds(420), DiagonalLadder, true);
        }

        private static readonly HashSet<int> forcedLow = new HashSet<int>();
        private static Harmony diagonalHarmony;
        private static void DiagonalPostfix(int tile, int direction, ref int __result)
        {
            if (__result != 0 || direction != 0) return;
            lock (forcedLow) if (!forcedLow.Contains(tile)) return;
            if ((Flags(tile) & (uint)TilePropertyFlag.CrenelatedHigh) == 0) __result = 1;
        }

        private IEnumerable<AutomationWait> DiagonalStart(AutomationRun run)
        {
            SetSwitches(true, false, false); // height adjustment and blockade off: the mod writes nothing
            ArmLobby(-1, PeaceMinutes, fixedWorld: true);
            if (diagonalHarmony == null)
            {
                Assembly mod = BepInEx.Bootstrap.Chainloader.PluginInfos[WallMod].Instance.GetType().Assembly;
                MethodInfo invoke = AccessTools.Method(mod.GetType("CrenelatedWallTest.StraightGraphicsHook", true), "Invoke");
                Require(invoke != null, "straight predicate callback missing");
                diagonalHarmony = new Harmony(Plugin.Guid + ".diagonal");
                diagonalHarmony.Patch(invoke, postfix: new HarmonyMethod(typeof(Fixture).GetMethod(nameof(DiagonalPostfix), BindingFlags.Static | BindingFlags.NonPublic)));
            }
            log("DIAG settings heights=off block=off; predicate postfix keeps forced lows straight");
            foreach (var wait in run.StartSkirmish("4spielerkarte.map", new[] { AILords.SK_RAT })) yield return wait;
        }

        // Block x 3..6, y 3..6; band x 8..12, y 3..4; support wall y 2; open ground from y 7.
        private const int BlockX0 = 3, BlockX1 = 6, BlockY0 = 3, BlockY1 = 6, BandX0 = 8, BandX1 = 12;

        private IEnumerable<AutomationWait> DiagonalBuild(AutomationRun run)
        {
            var m = run.CurrentMission;
            foreach (var w in Support(run, m, owner, 1, 2, 6, 2, false)) yield return w;
            foreach (var w in Support(run, m, owner, 7, 2, 12, 2, false)) yield return w;
            for (int y = BlockY0; y <= BlockY1; y++) foreach (var w in Line(run, m, "diag-block", owner, BlockX0, y, BlockX1, y, null)) yield return w;
            for (int y = 3; y <= 4; y++) foreach (var w in Line(run, m, "diag-band", owner, BandX0, y, BandX1, y, null)) yield return w;
            foreach (var w in run.SetPause(m, false)) yield return w;
            yield return run.WaitForTicks(m, 60);
            foreach (var w in run.SetPause(m, true)) yield return w;
            log("DIAG_VANILLA " + run.InSimulation(m, () => DiagonalGrid()));
            // Force the checkerboard: low where the map parity is low, then rebuild path edges.
            run.InSimulation(m, () =>
            {
                var tiles = GameTileManagerAPI.Instance;
                foreach (var (x, y) in DiagonalCells())
                {
                    int id = Tile(x, y);
                    Require(IsCrenel(id), "test crenel missing at " + x + "," + y);
                    TilePropertyFlag flags = tiles.GetTilePropertyFlag(id);
                    bool high = Parity(x, y);
                    if (!high) lock (forcedLow) forcedLow.Add(id);
                    tiles.SetTilePropertyFlag(id, high ? flags | TilePropertyFlag.CrenelatedHigh : flags & ~TilePropertyFlag.CrenelatedHigh);
                }
                foreach (var (x, y) in DiagonalCells())
                {
                    Require(GamePathingManagerAPI.Instance.RecalculateTileAndNeighbours(x, y), "path refresh failed");
                    tiles.RefreshTileAreaVisuals(x, y);
                }
                return 0;
            });
            foreach (var w in run.SetPause(m, false)) yield return w;
            yield return run.WaitForTicks(m, 200); // render maintenance must keep the forced lows
            foreach (var w in run.SetPause(m, true)) yield return w;
            string grid = run.InSimulation(m, () => DiagonalGrid());
            log("DIAG_FORCED " + grid);
            Require(run.InSimulation(m, () => DiagonalCells().All(c => ((Flags(Tile(c.Item1, c.Item2)) & (uint)TilePropertyFlag.CrenelatedHigh) != 0) == Parity(c.Item1, c.Item2))),
                "the forced checkerboard did not survive render maintenance");
        }

        private IEnumerable<(int, int)> DiagonalCells()
        {
            for (int y = BlockY0; y <= BlockY1; y++) for (int x = BlockX0; x <= BlockX1; x++) yield return (x + ox, y + oy);
            for (int y = 3; y <= 4; y++) for (int x = BandX0; x <= BandX1; x++) yield return (x + ox, y + oy);
        }

        // H/L grid per row plus the native edge mask (bit k = direction k: 0=N,1=NE,...,7=NW).
        private string DiagonalGrid()
        {
            var edges = GamePathingManagerAPI.Instance.GetPathEdgeMaskGrid();
            var rows = new List<string>();
            for (int y = 2; y <= 7; y++)
            {
                var row = new System.Text.StringBuilder("y" + (y + oy) + ":");
                for (int x = BlockX0 - 1; x <= BandX1 + 1; x++)
                {
                    int id = Tile(x + ox, y + oy); uint f = Flags(id);
                    row.Append(IsCrenel(id) ? ((f & (uint)TilePropertyFlag.CrenelatedHigh) != 0 ? "H" : "L") : IsWall(id) ? "W" : ".");
                    row.Append(edges[id].ToString("X2")).Append(' ');
                }
                rows.Add(row.ToString());
            }
            return string.Join(" | ", rows);
        }

        // Interior low tile of the block: all four axis neighbours are high crenels.
        private (int, int) InteriorLow()
        {
            for (int y = BlockY0 + 1; y < BlockY1; y++) for (int x = BlockX0 + 1; x < BlockX1; x++)
                    if (!Parity(x + ox, y + oy)) return (x + ox, y + oy);
            throw new InvalidOperationException("no interior low tile");
        }

        private IEnumerable<AutomationWait> DiagonalAssassin(AutomationRun run)
        {
            var m = run.CurrentMission;
            var (tx, ty) = InteriorLow();
            foreach (var w in DiagonalMove(run, m, "assassin-interior", BlockX0 + 1 + ox, BlockY1 + 2 + oy, tx, ty)) yield return w;
            // The band's far row (y 3) is reachable from the ground only through the near row.
            int bx = Enumerable.Range(BandX0, BandX1 - BandX0 + 1).First(x => !Parity(x + ox, 3 + oy)) + ox;
            foreach (var w in DiagonalMove(run, m, "assassin-band", bx, 6 + oy, bx, 3 + oy)) yield return w;
            // Control: a high tile must be refused or not reached.
            int hx = Enumerable.Range(BlockX0, 4).First(x => Parity(x + ox, BlockY0 + 1 + oy)) + ox;
            foreach (var w in DiagonalMove(run, m, "assassin-high-control", BlockX0 + 1 + ox, BlockY1 + 2 + oy, hx, BlockY0 + 1 + oy)) yield return w;
        }

        private IEnumerable<AutomationWait> DiagonalMove(AutomationRun run, MissionContext m, string label, int sx, int sy, int tx, int ty)
        {
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
            var path = new List<string>(); string state = ""; bool done = false;
            for (int i = 0; i < 60 && sent && !done; i++)
            {
                foreach (var w in run.SetPause(m, false)) yield return w;
                yield return run.WaitForTicks(m, 20);
                foreach (var w in run.SetPause(m, true)) yield return w;
                done = run.InSimulation(m, () => Settled(unit, global, out state)) && (i >= 6 || state.StartsWith(tx + "," + ty + ":"));
                string at = state.Split(':')[0];
                if (path.Count == 0 || path[path.Count - 1] != at) path.Add(at);
            }
            bool arrived = state.StartsWith(tx + "," + ty + ":");
            log("DIAG_WALK " + label + " from=" + sx + "," + sy + " target=" + tx + "," + ty + " commandAccepted=" + sent + " arrived=" + arrived +
                " path=" + string.Join(">", path) + " unit=" + state);
            run.InSimulation(m, () => { Kill(unit); return 0; });
        }

        private IEnumerable<AutomationWait> DiagonalLadder(AutomationRun run)
        {
            var m = run.CurrentMission;
            var (tx, ty) = InteriorLow();
            // Ladder on a low tile of the block's south row, from the open ground in front of it.
            int lx = Enumerable.Range(BlockX0, 4).First(x => !Parity(x + ox, BlockY1 + oy));
            int ladderTile = 0, climber = 0; uint climberGlobal = 0;
            foreach (var w in run.SetPause(m, true)) yield return w;
            run.InSimulation(m, () => { Squad(attacker, eChimps.CHIMP_TYPE_SPEARMAN, 1, lx + 1, BlockY1 + 4, out var ids); climber = ids[0]; climberGlobal = Global(climber); ladderTile = Tile(lx + ox, BlockY1 + oy); return 0; });
            foreach (var w in Initialize(run, m)) yield return w;
            int climberTribe = run.InSimulation(m, () => TribeOf(climber, climberGlobal));
            log("DIAG_LADDER before interiorOrder=" + run.InSimulation(m, () => GameTribeManagerAPI.Instance.IssueMoveHereCommand(climberTribe, tx, ty)) +
                " edgeOrder=" + run.InSimulation(m, () => GameTribeManagerAPI.Instance.IssueMoveHereCommand(climberTribe, lx + ox, BlockY1 + oy)));
            int man = 0; uint manGlobal = 0;
            int ladderTribe = run.InSimulation(m, () => { int t = Squad(attacker, eChimps.CHIMP_TYPE_LADDERMAN, 1, lx, BlockY1 + 5, out var ids); man = ids[0]; manGlobal = Global(man); return t; });
            foreach (var w in Initialize(run, m)) yield return w;
            bool edgeReachable = false;
            for (int i = 0; i < 90 && !edgeReachable; i++)
            {
                if (i % 20 == 0) run.InSimulation(m, () => GameTribeManagerAPI.Instance.AttachLadderToWall(ladderTribe, ladderTile));
                foreach (var w in run.SetPause(m, false)) yield return w;
                yield return run.WaitForTicks(m, 40);
                foreach (var w in run.SetPause(m, true)) yield return w;
                edgeReachable = run.InSimulation(m, () => GameTribeManagerAPI.Instance.IssueMoveHereCommand(climberTribe, lx + ox, BlockY1 + oy));
            }
            log("DIAG_LADDER placed edgeReachable=" + edgeReachable + " man=" + run.InSimulation(m, () => UnitState(man, manGlobal)) +
                " ladderFlags=" + (run.InSimulation(m, () => FindFlag(0, 0, 14, 26, 0x800000u)) ?? "none"));
            if (!edgeReachable) { log("DIAG_WALK ladder-interior result=no-ladder"); yield break; }
            // Step 1: up the ladder onto the low edge crenel. Step 2: diagonally to the interior low.
            var path = new List<string>(); string state = "";
            foreach (var (gx, gy, label) in new[] { (lx + ox, BlockY1 + oy, "ladder-edge"), (tx, ty, "ladder-interior") })
            {
                bool accepted = run.InSimulation(m, () => GameTribeManagerAPI.Instance.IssueMoveHereCommand(climberTribe, gx, gy));
                bool arrived = false;
                for (int i = 0; i < 150 && accepted && !arrived; i++)
                {
                    foreach (var w in run.SetPause(m, false)) yield return w;
                    yield return run.WaitForTicks(m, 20);
                    foreach (var w in run.SetPause(m, true)) yield return w;
                    run.InSimulation(m, () => Settled(climber, climberGlobal, out state));
                    string at = state.Split(':')[0];
                    if (path.Count == 0 || path[path.Count - 1] != at) path.Add(at);
                    arrived = state.StartsWith(gx + "," + gy + ":");
                    if (i % 30 == 29 && !arrived) accepted = run.InSimulation(m, () => GameTribeManagerAPI.Instance.IssueMoveHereCommand(climberTribe, gx, gy));
                }
                log("DIAG_WALK " + label + " target=" + gx + "," + gy + " commandAccepted=" + accepted + " arrived=" + arrived +
                    " path=" + string.Join(">", path) + " unit=" + state);
                if (!arrived) break;
            }
            log("DIAG_FINAL " + run.InSimulation(m, () => DiagonalGrid()));
        }
    }
}
