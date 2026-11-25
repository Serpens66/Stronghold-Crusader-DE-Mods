using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using APIShared;
using APIShared.Automation;
using CrusaderDE;
using HarmonyLib;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using UnityEngine;

namespace CrenelatedWallAcceptance
{
    // Screenshots for a design decision (scenario crenel-look): a double row and a 4x4 block in
    // three variants. Set 0 Vanilla (all high), set 1 checkerboard with every band tile drawn as a
    // straight piece, set 2 checkerboard with only the low tiles drawn straight. The mod's height
    // adjustment is off; a test-only postfix on the mod's straight predicate chooses the drawing.
    // Scenario crenel-look-corners: thin L corner, T junction and cross instead, with checkerboard
    // heights placed so that every junction is low. Set 0 Vanilla, set 1 lows drawn with the
    // straight sprite of direction 0, set 2 with that of direction 2.
    internal sealed partial class Fixture
    {
        // Runs in the map editor: free placement without territory, keep or stone limits.
        private bool lookCorners;
        private IEnumerable<AutomationStep> LookSteps(bool corners)
        {
            lookCorners = corners;
            yield return new AutomationStep("editor-load", TimeSpan.FromSeconds(180), LookStart);
            yield return new AutomationStep("look-build", TimeSpan.FromSeconds(300), LookBuild, true);
            yield return new AutomationStep("look-shots", TimeSpan.FromSeconds(180), LookShots, true);
        }

        // Scenario crenel-look-manual (foreground): both layouts in one editor session for a manual
        // look with free camera and rotation. Ends with PASS when the user leaves the editor.
        private IEnumerable<AutomationStep> LookManualSteps()
        {
            lookCorners = false;
            yield return new AutomationStep("editor-load", TimeSpan.FromSeconds(180), LookStart);
            yield return new AutomationStep("look-build-bands", TimeSpan.FromSeconds(300), LookBuild, true);
            yield return new AutomationStep("look-build-corners", TimeSpan.FromSeconds(300), r => { lookCorners = true; return LookBuild(r); }, true);
            yield return new AutomationStep("look-manual", TimeSpan.FromHours(4), LookManualWait);
        }

        // Scenario crenel-look-x: an "X" (centre plus four diagonal crenels) in front of a support wall,
        // first without the mod's height adjustment, then with it; four camera rotations each.
        private IEnumerable<AutomationStep> LookXSteps()
        {
            yield return new AutomationStep("editor-vanilla", TimeSpan.FromSeconds(180), r => LoadEditor(r, true, null, heights: false));
            yield return new AutomationStep("x-vanilla", TimeSpan.FromSeconds(300), r => LookX(r, "vanilla"), true);
            yield return new AutomationStep("editor-mod", TimeSpan.FromSeconds(180), r => LoadEditor(r, true, null, heights: true));
            yield return new AutomationStep("x-mod", TimeSpan.FromSeconds(300), r => LookX(r, "mod"), true);
        }

        private IEnumerable<AutomationWait> LookX(AutomationRun run, string variant)
        {
            var m = run.CurrentMission;
            foreach (var w in WaitCameraRest()) yield return w;
            int sx = GameMap.instance.ScreenCentreTileX, sy = GameMap.instance.ScreenCentreTileY;
            owner = 1;
            bool found = Engine(() =>
            {
                for (int r = 0; r <= 30; r++)
                    for (int dy = -r; dy <= r; dy++)
                        for (int dx = -r; dx <= r; dx++)
                            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r && Free(sx - 3 + dx, sy - 2 + dy, 7, 5, 0)) { ox = ex = sx - 3 + dx; oy = ey = sy - 2 + dy; return true; }
                return false;
            });
            Require(found, "no free area for the X");
            log("LOOK_X " + variant + " origin=" + ox + "," + oy + " centre=" + sx + "," + sy);
            foreach (var w in UiWall(run, m, "x-support", owner, eMappers.MAPPER_WALL, 0, 0, 6, 0, null)) yield return w;
            // A crenel needs an axis neighbour wall or crenel (0x5C580), which the X centre and its lower
            // arms lack; a temporary plain wall below the centre carries them and is bulldozed afterwards.
            foreach (var w in UiWall(run, m, "x-helper", owner, eMappers.MAPPER_WALL, 3, 3, 3, 3, null)) yield return w;
            foreach (var (x, y) in new[] { (2, 1), (4, 1), (3, 2), (2, 3), (4, 3) })
                foreach (var w in UiWall(run, m, "x-piece", owner, eMappers.MAPPER_CRENAL, x, y, x, y, false, unmanaged: variant == "vanilla")) yield return w;
            int helper = Engine(() => Tile(3 + ex, 3 + ey));
            EditorDirector.instance.SetEditorPlayerID(1);
            MainControls.instance.CurrentAction = 6; // bulldozer tool, as in EditorDemolish
            foreach (var w in MouseClick(3 + ex, 3 + ey, 1, 2, 2, 3)) yield return w;
            MainControls.instance.StopAllPlacement();
            bool gone = false;
            for (int i = 0; i < 30 && !gone; i++) { yield return run.WaitForTicks(m, 10); gone = Engine(() => !IsWall(helper)); }
            Require(gone, "helper wall below the X centre was not removed");
            yield return run.WaitForTicks(m, 200);
            log("LOOK_X_GRID " + variant + " " + Engine(() => string.Join("|", Enumerable.Range(0, 5).Select(y => new string(Enumerable.Range(0, 7).Select(x =>
            {
                int id = Tile(x + ox, y + oy);
                return IsCrenel(id) ? ((Flags(id) & (uint)TilePropertyFlag.CrenelatedHigh) != 0 ? 'H' : 'L') : IsWall(id) ? 'W' : '.';
            }).ToArray())))));
            string directory = Path.Combine(BepInEx.Paths.BepInExRootPath, "CrenelLook_shots");
            Directory.CreateDirectory(directory);
            PerfectPixelWithZoom.instance.SetZoomImmediate(0.5f);
            for (int rotation = 0; rotation < 4; rotation++)
            {
                yield return run.WaitForTicks(m, 8);
                yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(1200));
                string file = Path.Combine(directory, runId + "_x-" + variant + "-r" + rotation + ".png");
                ScreenCapture.CaptureScreenshot(file);
                yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(1500));
                log("LOOK_SHOT x-" + variant + "-r" + rotation + " file=" + file + " exists=" + File.Exists(file));
                GameMap.instance.RotateMapRight();
            }
            PerfectPixelWithZoom.instance.SetZoomImmediate(1f);
        }

        // Scenario crenel-look-diagonal (user's comparison): two diagonal rows of 10 plain walls
        // (screen-horizontal), each with a crenel row in front; heights on. The plain walls of row B
        // are bulldozed. Shots: current drawing, then row B's high crenels drawn with the straight
        // sprite of direction 0, then of direction 2 (test postfix only).
        private IEnumerable<AutomationStep> LookDiagonalSteps()
        {
            yield return new AutomationStep("editor-load", TimeSpan.FromSeconds(180), r => LookStart(r, true));
            yield return new AutomationStep("diagonal-rows", TimeSpan.FromSeconds(900), LookDiagonal, true);
        }

        private const int DiagonalLength = 10, DiagonalGap = 8;
        // Row 0 walls at (i, i), row 1 walls at (gap + i, -gap + i); crenels one tile further in +x,
        // which is the screen-front side at the editor start rotation (+1,-1 points down on screen).
        private static IEnumerable<(int, int)> DiagonalRow(int row, bool crenels)
        {
            for (int i = 0; i < DiagonalLength; i++) yield return (row * DiagonalGap + i + (crenels ? 1 : 0), -row * DiagonalGap + i);
        }

        private IEnumerable<AutomationWait> LookDiagonal(AutomationRun run)
        {
            var m = run.CurrentMission;
            foreach (var w in WaitCameraRest()) yield return w;
            int sx = GameMap.instance.ScreenCentreTileX, sy = GameMap.instance.ScreenCentreTileY;
            owner = 1;
            var used = DiagonalRow(0, false).Concat(DiagonalRow(0, true)).Concat(DiagonalRow(1, false)).Concat(DiagonalRow(1, true)).ToList();
            bool found = Engine(() =>
            {
                for (int r = 0; r <= 40; r++)
                    for (int dy = -r; dy <= r; dy++)
                        for (int dx = -r; dx <= r; dx++)
                        {
                            int x0 = sx - 9 + dx, y0 = sy - 1 + dy;
                            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r && used.All(t => Free(x0 + t.Item1, y0 + t.Item2, 1, 1, 0))) { ox = ex = x0; oy = ey = y0; return true; }
                        }
                return false;
            });
            Require(found, "no free area for the diagonal rows");
            log("LOOK_DIAG origin=" + ox + "," + oy + " centre=" + sx + "," + sy);
            for (int row = 0; row < 2; row++)
            {
                // Editor drags turn red from the seventh tile on: two drags of five per row.
                var walls = DiagonalRow(row, false).ToList(); var crenels = DiagonalRow(row, true).ToList();
                foreach (var (a, b) in new[] { (0, 4), (5, 9) })
                    foreach (var w in UiWall(run, m, "diag-wall", owner, eMappers.MAPPER_WALL, walls[a].Item1, walls[a].Item2, walls[b].Item1, walls[b].Item2, null)) yield return w;
                foreach (var (a, b) in new[] { (0, 4), (5, 9) })
                    foreach (var w in UiWall(run, m, "diag-crenels", owner, eMappers.MAPPER_CRENAL, crenels[a].Item1, crenels[a].Item2, crenels[b].Item1, crenels[b].Item2, false)) yield return w;
            }
            EditorDirector.instance.SetEditorPlayerID(1);
            foreach (var (x, y) in DiagonalRow(1, false))
            {
                int wall = Engine(() => Tile(x + ex, y + ey));
                MainControls.instance.CurrentAction = 6; // bulldozer tool, as in EditorDemolish
                foreach (var w in MouseClick(x + ex, y + ey, 1, 2, 2, 3)) yield return w;
                MainControls.instance.StopAllPlacement();
                bool gone = false;
                for (int t = 0; t < 30 && !gone; t++) { yield return run.WaitForTicks(m, 10); gone = Engine(() => !IsWall(wall)); }
                Require(gone, "plain wall of row B not removed at " + x + "," + y);
            }
            yield return run.WaitForTicks(m, 200);
            log("LOOK_DIAG_HEIGHTS " + Engine(() => string.Join(" | ", Enumerable.Range(0, 2).Select(row =>
                new string(DiagonalRow(row, true).Select(t => { int id = Tile(t.Item1 + ex, t.Item2 + ey); return !IsCrenel(id) ? '.' : (Flags(id) & (uint)TilePropertyFlag.CrenelatedHigh) != 0 ? 'H' : 'L'; }).ToArray())))));
            string directory = Path.Combine(BepInEx.Paths.BepInExRootPath, "CrenelLook_shots");
            Directory.CreateDirectory(directory);
            PerfectPixelWithZoom.instance.SetZoomImmediate(0.5f);
            // Straight-sprite combinations for row B (low direction, high direction; 9 = Vanilla special).
            foreach (var (name, mode) in new[] { ("current", 0), ("l0-h0", 100), ("l0-h2", 102), ("l2-hs", 129), ("l2-h0", 120), ("l2-h2", 122) })
            {
                Engine(() =>
                {
                    foreach (var (x, y) in DiagonalRow(1, true))
                    {
                        int id = Tile(x + ex, y + ey);
                        lock (lookMode) { if (mode == 0) lookMode.Remove(id); else lookMode[id] = mode; }
                        GameTileManagerAPI.Instance.RefreshTileAreaVisuals(x + ex, y + ey);
                    }
                    return 0;
                });
                yield return run.WaitForTicks(m, 20);
                yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(1200));
                string file = Path.Combine(directory, runId + "_diag-" + name + ".png");
                ScreenCapture.CaptureScreenshot(file);
                yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(1500));
                log("LOOK_SHOT diag-" + name + " file=" + file + " exists=" + File.Exists(file) + " heights=" + Engine(() =>
                    new string(DiagonalRow(1, true).Select(t => (Flags(Tile(t.Item1 + ex, t.Item2 + ey)) & (uint)TilePropertyFlag.CrenelatedHigh) != 0 ? 'H' : 'L').ToArray())));
            }
            PerfectPixelWithZoom.instance.SetZoomImmediate(1f);
        }

        // The start camera still glides to its start position; wait until it rests.
        private IEnumerable<AutomationWait> WaitCameraRest()
        {
            float lastX = float.NaN, lastY = float.NaN;
            for (int wait = 0; wait < 40; wait++)
            {
                yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(500));
                float camX = CameraControls2D.instance.getCameraXPos(), camY = CameraControls2D.instance.getCameraYPos();
                if (camX == lastX && camY == lastY && wait >= 4) yield break;
                lastX = camX; lastY = camY;
            }
        }

        private IEnumerable<AutomationWait> LookManualWait(AutomationRun run)
        {
            long session = run.CurrentMission.SessionId;
            log("LOOK_MANUAL ready; leave the editor to end the run");
            yield return AutomationWait.Until("user leaves the editor", () => run.CurrentMission == null || run.CurrentMission.SessionId != session);
            log("LOOK_MANUAL editor left");
        }

        // tile -> drawing mode: 1 = every band tile straight, 2 = only low band tiles straight,
        // 3 = low tiles straight with the sprite of direction 0.
        private static readonly Dictionary<int, int> lookMode = new Dictionary<int, int>();
        private static Harmony lookHarmony;
        private static void LookPostfix(int tile, int direction, ref int __result)
        {
            int mode;
            lock (lookMode) if (!lookMode.TryGetValue(tile, out mode)) return;
            bool low = (Flags(tile) & (uint)TilePropertyFlag.CrenelatedHigh) == 0;
            // The rows run along x, so the straight sprite of the east-west axis (direction 2) is used.
            // 100 + low*10 + high: diagonal pieces drawn straight in direction 0/2, 9 = high keeps the
            // Vanilla special sprite (crenel-look-diagonal combinations). Overrides the mod's answer.
            if (mode >= 100)
            {
                int target = low ? (mode - 100) / 10 : (mode - 100) % 10;
                if (target == 9) return;
                __result = direction == target ? 1 : 0;
                return;
            }
            if (__result != 0) return;
            // 4/5: high diagonal pieces with the straight sprite of direction 0 / 2 (crenel-look-diagonal).
            if (mode == 4 || mode == 5) { if (!low && direction == (mode == 4 ? 0 : 2)) __result = 1; return; }
            if (mode == 3 ? direction == 0 && low : direction == 2 && (mode == 1 || low)) __result = 1;
        }

        private IEnumerable<AutomationWait> LookStart(AutomationRun run) => LookStart(run, false);
        private IEnumerable<AutomationWait> LookStart(AutomationRun run, bool heights)
        {
            if (lookHarmony == null)
            {
                Assembly mod = BepInEx.Bootstrap.Chainloader.PluginInfos[WallMod].Instance.GetType().Assembly;
                MethodInfo invoke = AccessTools.Method(mod.GetType("CrenelatedWallTest.StraightGraphicsHook", true), "Invoke");
                Require(invoke != null, "straight predicate callback missing");
                lookHarmony = new Harmony(Plugin.Guid + ".look");
                lookHarmony.Patch(invoke, postfix: new HarmonyMethod(typeof(Fixture).GetMethod(nameof(LookPostfix), BindingFlags.Static | BindingFlags.NonPublic)));
            }
            // Mod active (graphics hook); height adjustment only where the scenario needs it.
            foreach (var w in LoadEditor(run, true, null, heights: heights)) yield return w;
        }

        private static int LookY(int set) => 2 + set * 8;
        private IEnumerable<(int, int)> LookCells(int set)
        {
            int y0 = LookY(set);
            if (lookCorners)
            {
                foreach (var (x, y) in CornerCells(y0)) yield return (x + ox, y + oy);
                yield break;
            }
            for (int y = y0 + 1; y <= y0 + 4; y++) for (int x = 3; x <= 6; x++) yield return (x + ox, y + oy);
            for (int y = y0 + 1; y <= y0 + 2; y++) for (int x = 8; x <= 12; x++) yield return (x + ox, y + oy);
        }

        // Relative crenel cells: L corner at (1,y0+3), T junction at (8,y0+4), cross at (12,y0+2).
        // All three junctions share one parity, so a single origin parity makes them all low.
        private static IEnumerable<(int, int)> CornerCells(int y0)
        {
            var cells = new HashSet<(int, int)>();
            foreach (var (x0, ya, x1, yb) in CornerLines(y0))
                for (int y = ya; y <= yb; y++) for (int x = x0; x <= x1; x++) cells.Add((x, y));
            return cells;
        }

        private static IEnumerable<(int, int, int, int)> CornerLines(int y0)
        {
            yield return (1, y0 + 1, 1, y0 + 3);   // L: stem from the support wall
            yield return (1, y0 + 3, 4, y0 + 3);   // L: arm
            yield return (8, y0 + 1, 8, y0 + 4);   // T: stem
            yield return (9, y0 + 4, 10, y0 + 4);  // T: bar, built outwards from the stem
            yield return (7, y0 + 4, 7, y0 + 4);
            yield return (6, y0 + 4, 6, y0 + 4);
            yield return (12, y0 + 1, 12, y0 + 4); // cross: vertical
            yield return (11, y0 + 2, 13, y0 + 2); // cross: horizontal
        }

        private IEnumerable<AutomationWait> LookBuild(AutomationRun run)
        {
            var m = run.CurrentMission;
            // Camera positioning from game tiles proved unreliable, so the sets go where the camera
            // already looks: the free area nearest to the game's own screen-centre tile.
            // The start camera still glides to the keep; wait until it rests before reading the centre.
            float lastX = float.NaN, lastY = float.NaN;
            for (int wait = 0; wait < 40; wait++)
            {
                yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(500));
                float camX = CameraControls2D.instance.getCameraXPos(), camY = CameraControls2D.instance.getCameraYPos();
                log("LOOK_CAMERA wait=" + wait + " camera=" + camX.ToString("0.00") + "," + camY.ToString("0.00") +
                    " centre=" + GameMap.instance.ScreenCentreTileX + "," + GameMap.instance.ScreenCentreTileY);
                if (camX == lastX && camY == lastY && wait >= 4) break;
                lastX = camX; lastY = camY;
            }
            int sx = GameMap.instance.ScreenCentreTileX, sy = GameMap.instance.ScreenCentreTileY;
            owner = 1;
            bool found = Engine(() =>
            {
                ushort component = 0;
                for (int r = 0; r <= 40; r++)
                    for (int dy = -r; dy <= r; dy++)
                        for (int dx = -r; dx <= r; dx++)
                        {
                            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                            int x0 = sx - 7 + dx, y0 = sy - 13 + dy;
                            // Corner layout: junctions sit at even relative x+y and must be low (Parity false).
                            if (lookCorners && Parity(x0, y0)) continue;
                            if (Free(x0, y0, 14, 26, component)) { ox = ex = x0; oy = ey = y0; return true; }
                        }
                return false;
            });
            log("LOOK_AREA screenCentre=" + sx + "," + sy + " origin=" + ox + "," + oy + " found=" + found);
            Require(found, "no free area near the screen centre");
            for (int set = 0; set < 3; set++)
            {
                int y0 = LookY(set);
                // Editor drags turn red from the seventh tile on, so every drag stays at six tiles.
                foreach (var w in UiWall(run, m, "look-support", owner, eMappers.MAPPER_WALL, 1, y0, 6, y0, null)) yield return w;
                foreach (var w in UiWall(run, m, "look-support", owner, eMappers.MAPPER_WALL, 7, y0, 12, y0, null)) yield return w;
                if (lookCorners)
                {
                    // In the editor a crenel drag only builds tiles next to an already built wall or
                    // crenel (the preview turned red from the second free-standing tile), so the thin
                    // shapes go tile by tile in line order.
                    var placed = new HashSet<(int, int)>();
                    foreach (var (x0, ya, x1, yb) in CornerLines(y0))
                        for (int y = ya; y <= yb; y++)
                            for (int x = x0; x <= x1; x++)
                                if (placed.Add((x, y)))
                                    foreach (var w in UiWall(run, m, "look-corner", owner, eMappers.MAPPER_CRENAL, x, y, x, y, null)) yield return w;
                    continue;
                }
                for (int y = y0 + 1; y <= y0 + 4; y++) foreach (var w in UiWall(run, m, "look-block", owner, eMappers.MAPPER_CRENAL, 3, y, 6, y, null)) yield return w;
                for (int y = y0 + 1; y <= y0 + 2; y++) foreach (var w in UiWall(run, m, "look-band", owner, eMappers.MAPPER_CRENAL, 8, y, 12, y, null)) yield return w;
            }
            yield return run.WaitForTicks(m, 60);
            Engine(() =>
            {
                var tiles = GameTileManagerAPI.Instance;
                for (int set = 1; set < 3; set++)
                    foreach (var (x, y) in LookCells(set))
                    {
                        int id = Tile(x, y);
                        Require(IsCrenel(id), "look crenel missing at " + x + "," + y);
                        lock (lookMode) lookMode[id] = lookCorners ? (set == 1 ? 3 : 2) : set;
                        TilePropertyFlag flags = tiles.GetTilePropertyFlag(id);
                        tiles.SetTilePropertyFlag(id, Parity(x, y) ? flags | TilePropertyFlag.CrenelatedHigh : flags & ~TilePropertyFlag.CrenelatedHigh);
                    }
                for (int set = 1; set < 3; set++)
                    foreach (var (x, y) in LookCells(set))
                    {
                        Require(GamePathingManagerAPI.Instance.RecalculateTileAndNeighbours(x, y), "path refresh failed");
                        tiles.RefreshTileAreaVisuals(x, y);
                    }
                return 0;
            });
            yield return run.WaitForTicks(m, 200);
            for (int set = 0; set < 3; set++) log("LOOK_SET " + set + " " + Engine(() => LookGrid(set)));
        }

        private string LookGrid(int set)
        {
            var rows = new List<string>();
            int y0 = LookY(set);
            for (int y = y0; y <= y0 + 5; y++)
            {
                var row = new System.Text.StringBuilder();
                for (int x = 0; x <= 14; x++)
                {
                    int id = Tile(x + ox, y + oy);
                    row.Append(IsCrenel(id) ? ((Flags(id) & (uint)TilePropertyFlag.CrenelatedHigh) != 0 ? 'H' : 'L') : IsWall(id) ? 'W' : '.');
                }
                rows.Add(row.ToString());
            }
            return string.Join("|", rows);
        }

        private IEnumerable<AutomationWait> LookShots(AutomationRun run)
        {
            var m = run.CurrentMission;
            string directory = Path.Combine(BepInEx.Paths.BepInExRootPath, "CrenelLook_shots");
            Directory.CreateDirectory(directory);
            // The sets were built around the screen centre (see LookBuild); the camera stays where it is.
            foreach (var (name, zoom) in new[] { ("wide", 0.25f), ("half", 0.5f), ("overview", 1f) })
            {
                PerfectPixelWithZoom.instance.SetZoomImmediate(zoom);
                yield return run.WaitForTicks(m, 4);
                yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(1000));
                string file = Path.Combine(directory, runId + "_" + name + ".png");
                ScreenCapture.CaptureScreenshot(file);
                yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(1500));
                log("LOOK_SHOT " + name + " zoom=" + PerfectPixelWithZoom.instance.getZoom().ToString("0.00") + " file=" + file + " exists=" + File.Exists(file));
            }
        }
    }
}
