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

        // tile -> drawing mode: 1 = every band tile straight, 2 = only low band tiles straight,
        // 3 = low tiles straight with the sprite of direction 0.
        private static readonly Dictionary<int, int> lookMode = new Dictionary<int, int>();
        private static Harmony lookHarmony;
        private static void LookPostfix(int tile, int direction, ref int __result)
        {
            if (__result != 0) return;
            int mode;
            lock (lookMode) if (!lookMode.TryGetValue(tile, out mode)) return;
            bool low = (Flags(tile) & (uint)TilePropertyFlag.CrenelatedHigh) == 0;
            // The rows run along x, so the straight sprite of the east-west axis (direction 2) is used.
            if (mode == 3 ? direction == 0 && low : direction == 2 && (mode == 1 || low)) __result = 1;
        }

        private IEnumerable<AutomationWait> LookStart(AutomationRun run)
        {
            if (lookHarmony == null)
            {
                Assembly mod = BepInEx.Bootstrap.Chainloader.PluginInfos[WallMod].Instance.GetType().Assembly;
                MethodInfo invoke = AccessTools.Method(mod.GetType("CrenelatedWallTest.StraightGraphicsHook", true), "Invoke");
                Require(invoke != null, "straight predicate callback missing");
                lookHarmony = new Harmony(Plugin.Guid + ".look");
                lookHarmony.Patch(invoke, postfix: new HarmonyMethod(typeof(Fixture).GetMethod(nameof(LookPostfix), BindingFlags.Static | BindingFlags.NonPublic)));
            }
            // Mod active (graphics hook) but without height adjustment and blockade.
            foreach (var w in LoadEditor(run, true, null, heights: false, block: false)) yield return w;
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
            yield return (6, y0 + 4, 10, y0 + 4);  // T: bar
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
                    // In the editor a vertical crenel drag built nothing and a drag starting on an
                    // existing crenel turned red, so vertical lines go tile by tile and horizontal ones
                    // only over still empty tiles.
                    var placed = new HashSet<(int, int)>();
                    foreach (var (x0, ya, x1, yb) in CornerLines(y0))
                        for (int y = ya; y <= yb; y++)
                            for (int x = x0; x <= x1; x++)
                            {
                                if (placed.Contains((x, y))) continue;
                                int end = x;
                                while (end < x1 && !placed.Contains((end + 1, y))) end++;
                                foreach (var w in UiWall(run, m, "look-corner", owner, eMappers.MAPPER_CRENAL, x, y, end, y, null)) yield return w;
                                for (int c = x; c <= end; c++) placed.Add((c, y));
                                x = end;
                            }
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
