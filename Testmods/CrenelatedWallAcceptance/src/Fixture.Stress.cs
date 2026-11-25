using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using APIShared.Automation;
using CrusaderDE;
using HarmonyLib;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace CrenelatedWallAcceptance
{
    // Load test (scenario crenel-stress): a new 800x800 editor map with 18,000 managed crenels,
    // shelled by spawned catapult stones (target tile via 0x7EB00, splash via event-less 0x7DF60).
    // CrenelatedWallTest's own methods are timed from outside with Harmony, so the measured mod
    // contains no test code. The same load runs with height adjustment on and off.
    internal sealed partial class Fixture
    {
        private const int StressRows = 60, StressLength = 300, StressX = 250, StressY = 250, StressCatapults = 10;
        private const int StressTicks = 30000, StressMinimumHits = 300;

        private IEnumerable<AutomationStep> StressSteps()
        {
            yield return new AutomationStep("stress-on-map", TimeSpan.FromSeconds(240), r => StressMap(r, true));
            yield return new AutomationStep("stress-on-build", TimeSpan.FromSeconds(600), StressBuild, true);
            yield return new AutomationStep("stress-on-siege", TimeSpan.FromSeconds(1200), r => StressSiege(r, "on"), true);
            yield return new AutomationStep("stress-off-map", TimeSpan.FromSeconds(240), r => StressMap(r, false));
            yield return new AutomationStep("stress-off-build", TimeSpan.FromSeconds(600), StressBuild, true);
            yield return new AutomationStep("stress-off-siege", TimeSpan.FromSeconds(1200), r => StressSiege(r, "off"), true);
        }

        // ---------- timing of CrenelatedWallTest from outside ----------
        private static Harmony stressHarmony;
        private static readonly Dictionary<MethodBase, int> stressSlots = new Dictionary<MethodBase, int>();
        private static readonly string[] StressNames = { "Tick", "ShapeChanged", "Damaged", "BuildWall", "AIBuildWall", "GraphicsInvoke", "Normalize" };
        private static readonly long[] stressCalls = new long[StressNames.Length], stressTicks = new long[StressNames.Length], stressMax = new long[StressNames.Length];
        private static long stressTickModTicks, stressTickModMax, stressLastTick, stressIntervalMax, stressIntervals, stressIntervalSum, stressModSum;
        private static readonly List<double> stressIntervalSamples = new List<double>();
        private static bool stressMeasuring;
        private static int stressDepth;

        private static void StressPrefix(out long __state) { __state = Stopwatch.GetTimestamp(); Interlocked.Increment(ref stressDepth); }
        private static void StressPostfix(long __state, MethodBase __originalMethod)
        {
            long elapsed = Stopwatch.GetTimestamp() - __state;
            int depth = Interlocked.Decrement(ref stressDepth);
            if (!stressMeasuring || !stressSlots.TryGetValue(__originalMethod, out int slot)) return;
            Interlocked.Increment(ref stressCalls[slot]);
            Interlocked.Add(ref stressTicks[slot], elapsed);
            long max; while (elapsed > (max = Interlocked.Read(ref stressMax[slot])) && Interlocked.CompareExchange(ref stressMax[slot], elapsed, max) != max) { }
            // Only outermost mod entries count towards the per-tick total (Normalize runs nested).
            if (depth == 0) Interlocked.Add(ref stressTickModTicks, elapsed);
        }
        private static void StressTick(int tick)
        {
            long now = Stopwatch.GetTimestamp();
            if (!stressMeasuring) { stressLastTick = now; return; }
            long mod = Interlocked.Exchange(ref stressTickModTicks, 0);
            stressModSum += mod;
            if (mod > stressTickModMax) stressTickModMax = mod;
            if (stressLastTick != 0)
            {
                long interval = now - stressLastTick;
                stressIntervals++; stressIntervalSum += interval;
                if (interval > stressIntervalMax) stressIntervalMax = interval;
                if (stressIntervalSamples.Count < 200000) stressIntervalSamples.Add(Ms(interval));
            }
            stressLastTick = now;
        }
        private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

        private void StressInstrument()
        {
            if (stressHarmony != null) return;
            Assembly mod = BepInEx.Bootstrap.Chainloader.PluginInfos[WallMod].Instance.GetType().Assembly;
            Type runtime = mod.GetType("CrenelatedWallTest.WallRuntime", true), hook = mod.GetType("CrenelatedWallTest.StraightGraphicsHook", true);
            var targets = new[] {
                AccessTools.Method(runtime, "Tick"), AccessTools.Method(runtime, "ShapeChanged"), AccessTools.Method(runtime, "Damaged"),
                AccessTools.Method(runtime, "BuildWall"), AccessTools.Method(runtime, "AIBuildWall"), AccessTools.Method(hook, "Invoke"),
                AccessTools.Method(runtime, "Normalize") };
            for (int i = 0; i < targets.Length; i++) { Require(targets[i] != null, "mod method missing: " + StressNames[i]); stressSlots[targets[i]] = i; }
            stressHarmony = new Harmony(Plugin.Guid + ".stress");
            var prefix = new HarmonyMethod(typeof(Fixture).GetMethod(nameof(StressPrefix), BindingFlags.Static | BindingFlags.NonPublic));
            var postfix = new HarmonyMethod(typeof(Fixture).GetMethod(nameof(StressPostfix), BindingFlags.Static | BindingFlags.NonPublic));
            foreach (MethodBase target in targets) stressHarmony.Patch(target, prefix: prefix, postfix: postfix);
            GameTimeManagerAPI.Instance.OnTick += StressTick;
            log("STRESS_INSTRUMENTED " + string.Join(",", StressNames));
        }
        private static void StressReset()
        {
            for (int i = 0; i < StressNames.Length; i++) { stressCalls[i] = 0; stressTicks[i] = 0; stressMax[i] = 0; }
            stressTickModTicks = stressTickModMax = stressIntervalMax = stressIntervals = stressIntervalSum = stressModSum = 0;
            stressLastTick = 0; stressIntervalSamples.Clear();
        }
        private static string StressReport(string label)
        {
            var parts = new List<string>();
            for (int i = 0; i < StressNames.Length; i++)
                parts.Add(StressNames[i] + "=" + stressCalls[i] + "x/" + Ms(stressTicks[i]).ToString("0.0") + "ms/max" + Ms(stressMax[i]).ToString("0.000") + "ms");
            var sorted = stressIntervalSamples.OrderBy(v => v).ToList();
            double p50 = sorted.Count == 0 ? 0 : sorted[sorted.Count / 2], p99 = sorted.Count == 0 ? 0 : sorted[Math.Min(sorted.Count - 1, sorted.Count * 99 / 100)];
            return "STRESS_RESULT " + label + " ticks=" + stressIntervals + " modTotal=" + Ms(stressModSum).ToString("0.0") + "ms modPerTickMax=" +
                Ms(stressTickModMax).ToString("0.000") + "ms tickInterval p50=" + p50.ToString("0.00") + "ms p99=" + p99.ToString("0.00") + "ms max=" +
                Ms(stressIntervalMax).ToString("0.00") + "ms | " + string.Join(" ", parts);
        }

        // ---------- scenario ----------
        private IEnumerable<AutomationWait> StressMap(AutomationRun run, bool heights)
        {
            foreach (var w in ToMenu(run)) yield return w;
            SetSwitches(true, heights);
            StressInstrument();
            long before = run.CurrentMission?.SessionId ?? 0;
            // Public sequence of FRONT_EditorSetup's "Start" handler for a new 800x800 build map.
            MainViewModel.Instance.InitNewScene(Enums.SceneIDS.MapEditor);
            EditorDirector.instance.createNewMap(800, Enums.GameModes.BUILD, siege_that: false);
            yield return AutomationWait.Until("new editor mission", () => run.CurrentMission != null && run.CurrentMission.SessionId != before);
            Require(run.CurrentMission.Mode.IsMapEditor, "expected an editor mission");
            yield return run.WaitForPlayState(run.CurrentMission);
            if (MainViewModel.Instance.MEMode != 1) MainViewModel.Instance.MEMode = 1;
            Director.instance.SetEngineFrameRate(250);
            yield return run.WaitForTicks(run.CurrentMission, 20);
            Drain();
            Require(crenelSeen.Any(l => l.Contains("CRENEL_SESSION") && l.Contains("editor=True") && l.Contains("heights=" + heights)),
                "CrenelatedWallTest editor session heights=" + heights + " not reported");
            int size = Engine(() => GameTileManagerAPI.Instance.GetCurrentMapSize());
            Require(size == 800, "new map size " + size);
            log("STRESS_MAP size=" + size + " heights=" + heights + " session=" + run.CurrentMission.SessionId);
        }

        private IEnumerable<AutomationWait> StressBuild(AutomationRun run)
        {
            var m = run.CurrentMission;
            int x1 = StressX + StressLength - 1, y1 = StressY + StressRows * 3;
            Engine(() =>
            {
                var tiles = GameTileManagerAPI.Instance;
                foreach (var (x, y) in new[] { (StressX, StressY), (x1, StressY), (StressX, y1 + 20), (x1, y1 + 20) })
                    Require(tiles.IsTileInsideMapBounds(x, y), "stress area outside the map at " + x + "," + y);
                return 0;
            });
            var sw = Stopwatch.StartNew(); long buildMax = 0;
            for (int r = 0; r < StressRows; r++)
            {
                int wy = StressY + r * 3, cy = wy + 1;
                for (int s = StressX; s <= x1; s += 100)
                {
                    int e = Math.Min(s + 99, x1), sx = s;
                    long t = Stopwatch.GetTimestamp();
                    Engine(() => { GameBuildingManagerAPI.Instance.CreateWall(1, sx, wy, e, wy, eMappers.MAPPER_WALL); GameBuildingManagerAPI.Instance.CreateWall(1, sx, cy, e, cy, eMappers.MAPPER_CRENAL); return 0; });
                    buildMax = Math.Max(buildMax, Stopwatch.GetTimestamp() - t);
                }
                if (r % 10 == 9) yield return run.WaitForTicks(m, 2);
            }
            yield return run.WaitForTicks(m, 200); // render maintenance and redraw reports settle
            int crenels = Engine(() => StressCrenels().Count);
            Require(crenels >= StressRows * StressLength * 9 / 10, "only " + crenels + " crenels were built");
            Drain();
            string managed = crenelSeen.LastOrDefault(l => l.Contains("CRENEL_BUILD"));
            log("STRESS_BUILT crenels=" + crenels + " seconds=" + sw.Elapsed.TotalSeconds.ToString("0.0") + " maxCommandMs=" + Ms(buildMax).ToString("0.0") +
                " last=" + (managed == null ? "none" : managed.Substring(managed.IndexOf("CRENEL_BUILD", StringComparison.Ordinal))));
        }

        private List<int> StressCrenels()
        {
            var ids = new List<int>();
            for (int r = 0; r < StressRows; r++)
                for (int x = StressX; x < StressX + StressLength; x++) { int id = Tile(x, StressY + r * 3 + 1); if (IsCrenel(id)) ids.Add(id); }
            return ids;
        }

        private IEnumerable<AutomationWait> StressSiege(AutomationRun run, string label)
        {
            var m = run.CurrentMission;
            // Catapult stones are spawned directly (SE CreateProjectile): the impact runs the same native
            // path as a catapult unit (0x9C730 -> 0x7EB00 on the hit tile, 0x7DF60 splash), at a known rate.
            // Projectile positions are eighth-tile units (tile * 8 + 4 = tile centre).
            // Units would need engineers, which did not man siege equipment in the editor (run 5bbf59bd).
            var random = new Random(20261125);
            int launchY = StressY + StressRows * 3 + 20, fired = 0;
            int hits = 0, before = Engine(() => StressCrenels().Count);
            var damage = BuildingR3EventHooks.OnBuildingTileTakeDamage.Observable.Subscribe(e => { if (e.Phase == EventHookPhase.Post) Interlocked.Increment(ref hits); });
            try
            {
                StressReset(); stressMeasuring = true;
                for (int t = 0; t < StressTicks; t += 20)
                {
                    // A salvo of StressCatapults stones every 20 ticks, each at a random standing crenel.
                    Engine(() =>
                    {
                        var tiles = GameTileManagerAPI.Instance;
                        for (int i = 0; i < StressCatapults; i++)
                        {
                            int target = StressTarget(StressX + random.Next(StressLength));
                            if (target == 0) continue;
                            XY(target, out int tx, out int ty);
                            int sx = StressX + random.Next(StressLength), source = Tile(sx, launchY);
                            if (GameProjectileManagerAPI.Instance.CreateProjectile(0, 2, sx * 8 + 4, launchY * 8 + 4, tiles.GetTileHeight(source), tx * 8 + 4, ty * 8 + 4, tiles.GetTileHeight(target), ProjectileType.CatapultRocks, 0) > 0) fired++;
                        }
                        return 0;
                    });
                    yield return run.WaitForTicks(m, 20);
                    if (t % 2000 == 1980) log("STRESS_PROGRESS " + label + " ticks=" + (t + 20) + " fired=" + fired + " hits=" + Volatile.Read(ref hits) +
                        " modPerTickMax=" + Ms(stressTickModMax).ToString("0.000") + "ms");
                    if (Volatile.Read(ref hits) >= StressMinimumHits * 3) break;
                }
            }
            finally { stressMeasuring = false; damage.Dispose(); }
            int after = Engine(() => StressCrenels().Count);
            Require(hits >= StressMinimumHits, "only " + hits + " wall hits; the load test did not reach its size");
            Drain();
            Require(!crenelSeen.Any(l => l.Contains("CRENEL_CALLBACK_ERROR")), "runtime error under load");
            log(StressReport(label) + " | fired=" + fired + " hits=" + hits + " crenels " + before + "->" + after);
            if (label == "on")
            {
                // Correctness under load: every standing managed crenel has its target height.
                yield return run.WaitForTicks(m, 400);
                string heights = Engine(() => { humanTiles.Clear(); humanTiles.UnionWith(StressCrenels()); return CheckHeights("stress"); });
                humanTiles.Clear();
                log(heights);
            }
        }

        private int StressTarget(int column)
        {
            for (int r = 0; r < StressRows; r++)
                for (int dx = 0; dx < 12; dx++)
                    foreach (int x in new[] { column + dx, column - dx })
                    {
                        int id = Tile(x, StressY + r * 3 + 1);
                        if (IsCrenel(id)) return id;
                    }
            return 0;
        }
    }
}
