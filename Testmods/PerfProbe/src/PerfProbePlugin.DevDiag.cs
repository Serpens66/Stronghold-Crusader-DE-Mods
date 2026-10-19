using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading;
using BepInEx.Logging;
using HarmonyLib;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace PerfProbe
{
    public sealed partial class PerfProbePlugin
    {
        static partial void DiagIsActive(ref bool active) => active = true;

        // Read once at startup; values are copied into static fields, so the destroyed plugin
        // component is not needed later. The original zoom/render purpose stays the default.
        static partial void DiagBindConfig(BepInEx.Configuration.ConfigFile config)
        {
            if (config == null) return;
            PerfProbeRuntime.RenderSnapshots = config.Bind("Zoom", "RenderSnapshots", true,
                "Write the 5 s render/zoom snapshot lines (original purpose of PerfProbe).").Value;
            PerfProbeRuntime.SpikeLines = config.Bind("Spikes", "Enabled", true,
                "Write immediate DEVDIAG spike lines for single slow simulation ticks, main-thread calls and frame gaps.").Value;
            PerfProbeRuntime.SetThresholds(
                config.Bind("Spikes", "SimulationMs", 200, "Minimum EngineInterface.run duration for a spike line.").Value,
                config.Bind("Spikes", "MainThreadMs", 200, "Minimum Director.Update / GUI-check duration for a spike line.").Value,
                config.Bind("Spikes", "FrameGapMs", 200, "Minimum gap between two rendered frames for a spike line.").Value,
                config.Bind("Spikes", "FreezeMs", 400, "Spike lines at or above this duration are marked freeze=true.").Value);
            PerfProbeExtras.BindConfig(config);
        }

        static partial void DiagInstall(ManualLogSource logger)
        {
            Shared.DevDiag.Attach(logger);
            PerfProbeRuntime.Install(logger);
        }
    }

    // Timing-only observers on audited Vanilla methods (Assembly-CSharp BC8B6A39). Prefix/postfix
    // never alter arguments or results; the original chain always runs. Patches stay published
    // for the process lifetime (statically rooted Harmony instance, no unpatch path).
    internal static class PerfProbeRuntime
    {
        private static readonly Harmony harmony = new Harmony("PerfProbe_Serp.Timing");
        private static readonly long SnapshotInterval = Stopwatch.Frequency * 5;
        private static ManualLogSource log;
        private static FieldInfo lowFpsField;
        private static int lastFrame = -1;
        private static long lastFrameStamp, nextSnapshot;
        private static float zoomMin = float.MaxValue, zoomMax;
        private static bool recordersStarted, firstSampleLogged, snapshotErrorLogged, frameExtrasErrorLogged;
        private static ProfilerRecorder batches, setPass, drawCalls, vertices, mainThread, renderThread;

        internal static void Install(ManualLogSource logger)
        {
            log = logger;
            Patch(typeof(Director), "Update", Type.EmptyTypes, nameof(EndDirectorUpdate), nameof(BeginMainSpikeWatch));
            Patch(typeof(GameMap), nameof(GameMap.processTestMap),
                new[] { typeof(short[]), typeof(int), typeof(EngineInterface.PlayState), typeof(byte[]) },
                nameof(EndProcessTestMap), nameof(BeginProcessTestMap));
            Patch(typeof(GameMap), nameof(GameMap.experimentalRowManager), Type.EmptyTypes, nameof(EndRowManager));
            Patch(typeof(GameMap), nameof(GameMap.ApplyRadarMap), Type.EmptyTypes, nameof(EndApplyRadarMap));
            Patch(typeof(TilemapManager), nameof(TilemapManager.endTileRefresh), new[] { typeof(bool) }, nameof(EndTileRefresh));
            Patch(typeof(FatControler), nameof(FatControler.NoesisGUIUpdateChecksInGame), Type.EmptyTypes, nameof(EndGuiChecks), nameof(BeginMainSpikeWatch));
            // Runs on Director's simulation thread; DevDiag aggregation is thread-safe.
            Patch(typeof(EngineInterface), nameof(EngineInterface.run), new[] { typeof(bool) }, nameof(EndEngineRun), nameof(BeginEngineRun));
            PerfProbeExtras.Install(log, harmony);
            lowFpsField = AccessTools.Field(typeof(Director), "lowFPSCompensationLevel");
            if (lowFpsField != null && lowFpsField.FieldType != typeof(int)) lowFpsField = null;
            // Statically rooted per-frame sampler; render/camera bound, also while paused.
            Application.onBeforeRender += OnBeforeRender;
            Shared.DevDiag.Log(() => string.Format(CultureInfo.InvariantCulture,
                "PERFPROBE_READY: timing patches published; frame sampler attached; renderSnapshots={0} " +
                "spikeLines={1} simMs={2} mainMs={3} frameGapMs={4} freezeMs={5}.", RenderSnapshots, SpikeLines,
                SimSpikeTicks * 1000 / Stopwatch.Frequency, MainSpikeTicks * 1000 / Stopwatch.Frequency,
                FrameSpikeTicks * 1000 / Stopwatch.Frequency, FreezeTicks * 1000 / Stopwatch.Frequency));
        }

        private static void Patch(Type type, string name, Type[] signature, string postfix, string prefix = nameof(Begin))
        {
            try
            {
                MethodInfo target = AccessTools.Method(type, name, signature);
                if (target == null) throw new MissingMethodException(type.Name + "." + name + " signature differs from the audit.");
                harmony.Patch(target, prefix: new HarmonyMethod(typeof(PerfProbeRuntime), prefix),
                    postfix: new HarmonyMethod(typeof(PerfProbeRuntime), postfix));
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(log, "PERFPROBE patch skipped for " + type.Name + "." + name + ": " + exception.Message);
            }
        }

        private static void Begin(out long __state) { __state = 0; Shared.DevDiag.Start(ref __state); }

        private static void BeginProcessTestMap(short[] sourceMap, int numElements, out long __state)
        {
            // Negative means "scan for terminator"; only explicit counts are aggregated.
            if (numElements > 0) Shared.DevDiag.Count("records/buffer(sum)", numElements);
            PerfProbeExtras.CountRecordTypes(sourceMap, numElements);
            __state = 0; Shared.DevDiag.Start(ref __state);
        }

        // Immediate spike lines (single calls/gaps at or above these limits) with cross-thread
        // context: framesDuring=0 on a sim spike means the main thread rendered nothing meanwhile;
        // simBusyMs on a main-thread spike is the overlap with a running EngineInterface.run.
        // Configured in DiagBindConfig (BepInEx config); defaults apply if binding failed.
        internal static bool RenderSnapshots = true, SpikeLines = true;
        private static long SimSpikeTicks = Stopwatch.Frequency / 5;      // 200 ms
        private static long MainSpikeTicks = Stopwatch.Frequency / 5;     // 200 ms
        private static long FrameSpikeTicks = Stopwatch.Frequency / 5;    // 200 ms
        private static long FreezeTicks = Stopwatch.Frequency * 2 / 5;    // 400 ms
        private static long simRunStarted, simRunsCompleted;
        private static int framesRendered;

        internal static void SetThresholds(int simMs, int mainMs, int frameMs, int freezeMs)
        {
            SimSpikeTicks = Ticks(simMs); MainSpikeTicks = Ticks(mainMs);
            FrameSpikeTicks = Ticks(frameMs); FreezeTicks = Ticks(freezeMs);
        }

        private static long Ticks(int ms) => Math.Max(1, ms) * Stopwatch.Frequency / 1000;

        private static string Freeze(long elapsed) => elapsed >= FreezeTicks ? " freeze=true" : "";

        private static void BeginEngineRun(out long __state)
        {
            simRunFramesAtStart = Volatile.Read(ref framesRendered);
            simRunAllocAtStart = PerfProbeExtras.ThreadAllocated();
            __state = Stopwatch.GetTimestamp();
            Volatile.Write(ref simRunStarted, __state);
        }

        private static void EndEngineRun(long __state)
        {
            long now = Stopwatch.GetTimestamp();
            Volatile.Write(ref simRunStarted, 0);
            Interlocked.Increment(ref simRunsCompleted);
            long elapsed = now - __state;
            if (simRunAllocAtStart >= 0) PerfProbeExtras.AddSimAlloc(PerfProbeExtras.ThreadAllocated() - simRunAllocAtStart);
            Shared.DevDiagRuntime.Add("sim:EngineInterface.run", 1, elapsed);
            if (!SpikeLines || elapsed < SimSpikeTicks) return;
            int frames = Volatile.Read(ref framesRendered) - simRunFramesAtStart;
            Shared.DevDiagRuntime.Spike("sim:EngineInterface.run", elapsed,
                "framesDuring=" + frames.ToString(CultureInfo.InvariantCulture) + Freeze(elapsed));
        }

        [ThreadStatic] private static int simRunFramesAtStart;
        [ThreadStatic] private static long simRunAllocAtStart;

        private static void BeginMainSpikeWatch(out MainWatch __state)
        {
            __state = new MainWatch
            {
                Started = Stopwatch.GetTimestamp(),
                SimRunsAtStart = Interlocked.Read(ref simRunsCompleted)
            };
        }

        private static void EndMainSpike(string name, MainWatch state)
        {
            long now = Stopwatch.GetTimestamp();
            long elapsed = now - state.Started;
            Shared.DevDiagRuntime.Add(name, 1, elapsed);
            if (!SpikeLines || elapsed < MainSpikeTicks) return;
            long simStart = Volatile.Read(ref simRunStarted);
            long simRuns = Interlocked.Read(ref simRunsCompleted) - state.SimRunsAtStart;
            double busyMs = simStart == 0 ? 0 : (now - Math.Max(simStart, state.Started)) * 1000.0 / Stopwatch.Frequency;
            Shared.DevDiagRuntime.Spike(name, elapsed, string.Format(CultureInfo.InvariantCulture,
                "simRunActiveAtEnd={0} simBusyMs={1:F1} simRunsCompletedDuring={2}", simStart != 0, busyMs, simRuns) +
                Freeze(elapsed));
        }

        private struct MainWatch { public long Started, SimRunsAtStart; }

        private static void EndDirectorUpdate(MainWatch __state) => EndMainSpike("main:Director.Update(total)", __state);
        private static void EndProcessTestMap(long __state) => Shared.DevDiag.Stop(ref __state, "main:GameMap.processTestMap");
        private static void EndRowManager(long __state) => Shared.DevDiag.Stop(ref __state, "main:GameMap.experimentalRowManager");
        private static void EndApplyRadarMap(long __state) => Shared.DevDiag.Stop(ref __state, "main:GameMap.ApplyRadarMap");
        private static void EndTileRefresh(long __state) => Shared.DevDiag.Stop(ref __state, "main:TilemapManager.endTileRefresh");
        private static void EndGuiChecks(MainWatch __state) => EndMainSpike("main:FatControler.NoesisGUIUpdateChecksInGame", __state);

        // True while EngineInterface.run (and thus EngineInterface.threadLock) is held by the sim thread.
        internal static bool SimTickActive => Volatile.Read(ref simRunStarted) != 0;
        internal static long MainThreadSpikeTicks => MainSpikeTicks;
        internal static long SimRunsCompleted => Interlocked.Read(ref simRunsCompleted);

        private static void LogFrameExtrasError(Exception exception)
        {
            if (frameExtrasErrorLogged) return;
            frameExtrasErrorLogged = true;
            Shared.DebugLogHelper.LogError(log, "PERFPROBE frame extras failed (logged once): " + exception);
        }

        private static void OnBeforeRender()
        {
            int frame = Time.frameCount;
            if (frame == lastFrame) return;
            lastFrame = frame;
            long now = Stopwatch.GetTimestamp();
            // Frame interval = Unity's full frame cost including render thread/GPU waits.
            long started = lastFrameStamp;
            Interlocked.Increment(ref framesRendered);
            if (started != 0)
            {
                long gap = now - started;
                Shared.DevDiagRuntime.Add("frame:interval", 1, gap);
                if (SpikeLines && gap >= FrameSpikeTicks)
                {
                    // Context of the frame that just ended (GC, top callbacks); built only for spikes.
                    string frameContext = "";
                    try { frameContext = PerfProbeExtras.FrameContext(); }
                    catch (Exception exception) { LogFrameExtrasError(exception); }
                    long simStart = Volatile.Read(ref simRunStarted);
                    Shared.DevDiagRuntime.Spike("frame:interval", gap, string.Format(CultureInfo.InvariantCulture,
                        "simRunActiveNow={0} simRunMsSoFar={1:F1} paused={2}", simStart != 0,
                        simStart == 0 ? 0 : (now - simStart) * 1000.0 / Stopwatch.Frequency,
                        Director.instance != null && !Director.instance.SimRunning) + frameContext + Freeze(gap));
                }
            }
            try { PerfProbeExtras.EndFrame(); }
            catch (Exception exception) { LogFrameExtrasError(exception); }
            lastFrameStamp = now;
            if (!RenderSnapshots) return;
            if (!recordersStarted) StartRecorders();
            try
            {
                if (PerfectPixelWithZoom.instance != null)
                {
                    float zoom = PerfectPixelWithZoom.instance.getZoom();
                    if (zoom < zoomMin) zoomMin = zoom;
                    if (zoom > zoomMax) zoomMax = zoom;
                }
                if (nextSnapshot == 0) nextSnapshot = now + SnapshotInterval;
                if (now < nextSnapshot) return;
                nextSnapshot = now + SnapshotInterval;
                string snapshot = BuildSnapshot();
                zoomMin = float.MaxValue; zoomMax = 0f;
                if (snapshot == null) return;
                if (!firstSampleLogged)
                {
                    firstSampleLogged = true;
                    Shared.DevDiag.Log(() => "recorders valid: batches=" + batches.Valid + " setPass=" + setPass.Valid +
                        " drawCalls=" + drawCalls.Valid + " vertices=" + vertices.Valid +
                        " mainThread=" + mainThread.Valid + " renderThread=" + renderThread.Valid);
                }
                Shared.DevDiag.Log(() => snapshot);
                PerfProbeExtras.Snapshot();
            }
            catch (Exception exception)
            {
                if (snapshotErrorLogged) return;
                snapshotErrorLogged = true;
                Shared.DebugLogHelper.LogError(log, "PERFPROBE snapshot failed (logged once): " + exception);
            }
        }

        private static void StartRecorders()
        {
            recordersStarted = true;
            try
            {
                // Availability in a release player is part of the measurement; validity is logged.
                batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
                setPass = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
                drawCalls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
                vertices = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count");
                mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
                renderThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Render Thread", 15);
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(log, "PERFPROBE profiler recorders unavailable: " + exception.Message);
            }
        }

        private static string BuildSnapshot()
        {
            Director director = Director.instance;
            if (director == null || !director.SimRunning || GameMap.instance == null) return null;
            int activeRows = 0, rowMaps = 0;
            Tilemap[] rows = TilemapManager.instance != null ? TilemapManager.instance.rowTileMaps : null;
            if (rows != null)
            {
                rowMaps = rows.Length;
                for (int index = 0; index < rows.Length; index++)
                    if (rows[index] != null && rows[index].gameObject.activeSelf) activeRows++;
            }
            int generalVisible = -1, orgVisible = -1, generalUsed = -1, orgUsed = -1;
            if (ObjectPool.instance != null && ObjectPool.poolsCreated)
            {
                generalVisible = ObjectPool.instance.getPoolCount(0, true);
                orgVisible = ObjectPool.instance.getPoolCount(1, true);
                generalUsed = ObjectPool.instance.getPoolCount(0, false);
                orgUsed = ObjectPool.instance.getPoolCount(1, false);
            }
            CultureInfo c = CultureInfo.InvariantCulture;
            return string.Format(c,
                "snapshot zoom={0:0.###}..{1:0.###} screen={2}x{3} tilemapSize={4} rowTilemapsActive={5}/{6} " +
                "spritesVisible(general/org)={7}/{8} spritesUsed(general/org)={9}/{10} batches={11} setPass={12} " +
                "drawCalls={13} vertices={14} mainThreadMs={15:0.00} renderThreadMs={16:0.00} lowFpsLevel={17} " +
                "simAvgMs={18} simMaxMs={19} vSync={20} targetFps={21}",
                zoomMin == float.MaxValue ? 0f : zoomMin, zoomMax, Screen.width, Screen.height, GameMap.tilemapSize,
                activeRows, rowMaps, generalVisible, orgVisible, generalUsed, orgUsed,
                Last(batches), Last(setPass), Last(drawCalls), Last(vertices),
                AverageMs(mainThread), AverageMs(renderThread),
                lowFpsField != null ? (int)lowFpsField.GetValue(director) : -1,
                director.getAverageCalcTime(), director.getHighestCalcTime(),
                QualitySettings.vSyncCount, Application.targetFrameRate);
        }

        private static long Last(ProfilerRecorder recorder) => recorder.Valid ? recorder.LastValue : -1;

        // Timing recorders report nanoseconds; average the retained samples.
        private static double AverageMs(ProfilerRecorder recorder)
        {
            if (!recorder.Valid || recorder.Count == 0) return -1;
            double sum = 0;
            for (int index = 0; index < recorder.Count; index++) sum += recorder.GetSample(index).Value;
            return sum / recorder.Count / 1e6;
        }
    }
}
