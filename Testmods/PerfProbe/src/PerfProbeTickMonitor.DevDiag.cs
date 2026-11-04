using System;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using BepInEx.Configuration;
using HarmonyLib;
using SHCDESE.API;
using SHCDESE.GameGlobals;

namespace PerfProbe
{
    // Game-time stalls ("tick:gap"): the world can stand still while frames and even run calls
    // continue (trigger 2026-10-28: editor, 1350 units, smooth frames, no spike line). Two
    // independent progress signals, both on the simulation thread:
    //   - Extender OnTick: native hook in the tick loop inside DLL_RunTick, not raised while the
    //     local player is paused;
    //   - native elapsed-map-tick counter (Extender API), read after every completed run call.
    // Pauses/loading come from the main thread once per rendered frame (PerfProbeTickGap.Classify).
    // A gap is handed to the main thread, which writes the spike line and requests main/sim stack
    // slices for exactly that window. Run calls, native calls and sim busy time inside the window
    // separate "simulation busy" from "simulation idle" (Vanilla's sim thread only sleeps between runs).
    // Rooted by the Extender's tick publisher (subscribed once from the static render callback).
    internal static class PerfProbeTickMonitor
    {
        private static readonly CultureInfo C = CultureInfo.InvariantCulture;
        private static readonly string[] HaltNames = { "none", "notRunning", "paused", "localPaused" };
        private static readonly PerfProbeGapQueue queue = new PerfProbeGapQueue(32);
        private static long threshold = Stopwatch.Frequency / 10;
        private static bool subscribeAttempted, mapTicksAvailable, localPauseAvailable;
        private static volatile bool subscribed;
        private static IntPtr mapTickAddress;

        private struct Counters
        {
            internal long Runs, Native, Skip, NoBuffer, Busy, Timer;
            internal int Frames, Map;
        }

        // Simulation thread only (OnTick and run postfix).
        private static long lastTickStamp, lastMapStamp, ticks;
        private static int lastTick = -1, lastMap = -1;
        private static Counters atTick, atMap;
        // Main thread writes, simulation thread reads.
        private static long haltStamp, expectedTicks;
        private static int haltKind;
        private static float lastFrameTime = -1f;
        // Window counters, reset by the 5 s summary (main thread).
        private static long windowTicks, windowGaps, windowHalted, windowMaxActive, windowMapTicks;
        private static long runsNoTick, runsMultiTick, maxTicksPerRun, runsNoMapTick, runsCounted;

        internal static bool Enabled => threshold > 0;
        internal static long Ticks => Interlocked.Read(ref ticks);
        internal static bool TicksObserved => subscribed && Interlocked.Read(ref ticks) > 0;
        internal static string Configuration => "tickGapMs:" + (threshold * 1000 / Stopwatch.Frequency) +
            ",subscribed:" + subscribed + ",mapTicks:" + mapTicksAvailable + ",localPause:" + localPauseAvailable + ",timerProbe:" + TimerProbe;

        // ---------- Vanilla simulation pacing (2026-10-28 finding: no run call during a stall) ----------
        // Director.MyFixedUpdate is a System.Threading.Timer callback (thread pool) that adds one
        // FramesToProcess per engine frame unless paused; the sim thread runs one run call per entry.
        // Missing callbacks = timer/thread-pool starvation; a growing FramesToProcess without run
        // calls = sim thread held before run. Private members are validated at runtime (Assembly-CSharp
        // BC8B6A39); on a mismatch only this probe is skipped.
        private static AccessTools.FieldRef<Director, int> framesToProcess;
        private static long timerCalls, timerLastStamp, timerMaxInterval, framesToProcessMax, windowTimerCalls, windowTimerMaxInterval;
        internal static bool TimerProbe => framesToProcess != null;

        internal static void InstallTimerProbe(Harmony harmony)
        {
            if (threshold == 0) return;
            try
            {
                MethodInfo target = AccessTools.Method(typeof(Director), "MyFixedUpdate", new[] { typeof(object) });
                if (target == null || target.ReturnType != typeof(void) || target.IsStatic)
                    throw new MissingMethodException("Director.MyFixedUpdate(object) differs from the audit.");
                FieldInfo field = AccessTools.Field(typeof(Director), "FramesToProcess");
                if (field == null || field.FieldType != typeof(int) || field.IsStatic)
                    throw new MissingFieldException("Director.FramesToProcess differs from the audit.");
                framesToProcess = AccessTools.FieldRefAccess<Director, int>(field);
                harmony.Patch(target, prefix: new HarmonyMethod(typeof(PerfProbeTickMonitor), nameof(BeforeFixedTimer)));
            }
            catch (Exception exception)
            {
                framesToProcess = null;
                string detail = exception.GetType().Name + " " + exception.Message;
                Shared.DevDiag.Log(() => "timerProbe unavailable: " + detail);
            }
        }

        // Thread-pool thread; value before Vanilla's own increment. Observation only.
        private static void BeforeFixedTimer(Director __instance)
        {
            long now = Stopwatch.GetTimestamp();
            long previous = Interlocked.Exchange(ref timerLastStamp, now);
            Interlocked.Increment(ref timerCalls);
            Interlocked.Increment(ref windowTimerCalls);
            if (previous != 0)
            {
                Max(ref timerMaxInterval, now - previous);
                Max(ref windowTimerMaxInterval, now - previous);
            }
            var access = framesToProcess;
            if (access != null && __instance != null) Max(ref framesToProcessMax, access(__instance));
        }

        private static void Max(ref long slot, long value)
        {
            long current;
            while (value > (current = Interlocked.Read(ref slot)) && Interlocked.CompareExchange(ref slot, value, current) != current) { }
        }

        internal static void BindConfig(ConfigFile config)
        {
            int ms = config.Bind("Spikes", "TickGapMs", 100,
                "Game-time stall: minimum time without a game tick, beyond the expected tick interval of the game speed, " +
                "for a tick:gap spike line with stack slices. Pauses and loading are excluded. 0 disables the tick monitor.").Value;
            threshold = ms <= 0 ? 0 : ms * Stopwatch.Frequency / 1000;
        }

        // Main thread, once per rendered frame (also while paused or in menus).
        internal static void ObserveFrame(long now)
        {
            if (threshold == 0) return;
            if (!subscribeAttempted) Subscribe();
            Director director = Director.instance;
            int kind = director == null || !director.SimRunning ? 1 : director.Paused ? 2 : LocalPaused() ? 3 : 0;
            if (kind != 0) { Volatile.Write(ref haltKind, kind); Volatile.Write(ref haltStamp, now); }
            if (director != null)
            {
                float frameTime = director.EngineFrameTime;
                if (frameTime != lastFrameTime)
                {
                    lastFrameTime = frameTime;
                    Volatile.Write(ref expectedTicks, (long)(Math.Max(0f, frameTime) * Stopwatch.Frequency));
                }
            }
            while (queue.TryDequeue(out PerfProbeTickGap gap)) Emit(gap);
        }

        // The native pause flag is a global found by the Extender; read only inside a running mission.
        private static bool LocalPaused()
        {
            if (!localPauseAvailable) return false;
            try { return GamePlayerManagerAPI.Instance.IsLocalPaused(); }
            catch (Exception) { localPauseAvailable = false; return false; }
        }

        // After plugin startup (first render callback): the Extender's globals and tick publisher exist.
        private static void Subscribe()
        {
            subscribeAttempted = true;
            try
            {
                localPauseAvailable = GameGlobalsManager.Instance.GamePausedVA != 0;
                ulong mapTickVa = GameGlobalsManager.Instance.CurrentGameTickVA;
                mapTickAddress = new IntPtr((long)mapTickVa);
                mapTicksAvailable = mapTickVa != 0;
                GameTimeManagerAPI.Instance.OnTick += OnTick;
                subscribed = true;
            }
            catch (Exception exception)
            {
                string detail = exception.GetType().Name + " " + exception.Message;
                Shared.DevDiag.Log(() => "tickMonitor unavailable: " + detail);
                return;
            }
            Shared.DevDiag.Log(() => "tickMonitor ready " + Configuration);
        }

        private static Counters Capture() => new Counters
        {
            Runs = Interlocked.Read(ref PerfProbeRunTiming.CallsCompleted),
            Native = Interlocked.Read(ref PerfProbeRunTiming.NativeCallsCompleted),
            Skip = Interlocked.Read(ref PerfProbeRunTiming.SkipCalls),
            NoBuffer = Interlocked.Read(ref PerfProbeRunTiming.BuffersUnavailable),
            Busy = PerfProbeRuntime.SimBusyTicks,
            Frames = PerfProbeRuntime.FramesRendered,
            Map = Volatile.Read(ref lastMap),
            Timer = Interlocked.Read(ref timerCalls)
        };

        // Simulation thread, inside DLL_RunTick: keep it to a few reads and writes per tick.
        private static void OnTick(int tick)
        {
            long now = Stopwatch.GetTimestamp();
            Interlocked.Increment(ref ticks);
            Interlocked.Increment(ref windowTicks);
            Counters current = Capture();
            Check(0, lastTickStamp, now, lastTick, tick, atTick, current);
            Volatile.Write(ref lastTickStamp, now); Volatile.Write(ref lastTick, tick); atTick = current;
            // Pacing maxima are per tick interval; a missing OnTick lets them span the map-tick gap.
            if (framesToProcess != null) { Interlocked.Exchange(ref timerMaxInterval, 0); Interlocked.Exchange(ref framesToProcessMax, 0); }
        }

        // Simulation thread, after every run call (also when it advanced no game tick).
        internal static void OnRunCompleted(long now, long ticksDuring, bool nativeCompleted)
        {
            if (!subscribed) return;
            if (nativeCompleted)
            {
                Interlocked.Increment(ref runsCounted);
                if (ticksDuring == 0) Interlocked.Increment(ref runsNoTick);
                else if (ticksDuring > 1) Interlocked.Increment(ref runsMultiTick);
                if (ticksDuring > Interlocked.Read(ref maxTicksPerRun)) Interlocked.Exchange(ref maxTicksPerRun, ticksDuring);
            }
            if (!mapTicksAvailable) return;
            // Same global as GameTimeManagerAPI.GetElapsedMapTicks (low 32 bits), read directly: the API
            // logs an error per call if its pointer was captured before the Extender found the global.
            int map = Marshal.ReadInt32(mapTickAddress);
            if (map == lastMap)
            {
                if (nativeCompleted) Interlocked.Increment(ref runsNoMapTick);
                return;
            }
            if (map > lastMap && lastMap >= 0) Interlocked.Add(ref windowMapTicks, map - lastMap);
            int before = lastMap;
            Volatile.Write(ref lastMap, map);
            Counters current = Capture();
            Check(1, lastMapStamp, now, before, map, atMap, current);
            lastMapStamp = now; atMap = current;
        }

        private static void Check(int source, long previous, long now, int before, int after, Counters start, Counters end)
        {
            long halt = Volatile.Read(ref haltStamp), expected = Volatile.Read(ref expectedTicks);
            if (!PerfProbeTickGap.Classify(previous, now, halt, expected, threshold, before, after, out long active))
            {
                if (source == 0 && PerfProbeTickGap.ExcludedByHalt(previous, now, halt, expected, threshold)) Interlocked.Increment(ref windowHalted);
                return;
            }
            if (source == 0)
            {
                Interlocked.Increment(ref windowGaps);
                if (now - active > Interlocked.Read(ref windowMaxActive)) Interlocked.Exchange(ref windowMaxActive, now - active);
            }
            // Pacing state only on the (rare) gap path; the open timer interval counts as well.
            var access = framesToProcess;
            Director director = Director.instance;
            long timerOpen = Interlocked.Read(ref timerLastStamp);
            queue.TryEnqueue(new PerfProbeTickGap
            {
                Source = source, Previous = previous, ActiveStart = active, End = now, Expected = expected,
                HaltStamp = halt, HaltKind = halt > previous ? Volatile.Read(ref haltKind) : 0, TickBefore = before, TickAfter = after,
                RunCalls = end.Runs - start.Runs, NativeCalls = end.Native - start.Native, SkipCalls = end.Skip - start.Skip,
                NoBuffer = end.NoBuffer - start.NoBuffer, BusyTicks = end.Busy - start.Busy, Frames = end.Frames - start.Frames,
                MapTicks = start.Map < 0 || end.Map < start.Map ? -1 : end.Map - start.Map,
                TimerCalls = access == null ? -1 : end.Timer - start.Timer,
                TimerMaxInterval = access == null ? -1 : Math.Max(Interlocked.Read(ref timerMaxInterval), timerOpen == 0 ? 0 : now - timerOpen),
                FramesToProcessMax = access == null ? -1 : Interlocked.Read(ref framesToProcessMax),
                FramesToProcessEnd = access == null || director == null ? -1 : access(director)
            });
        }

        private static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

        private static void Emit(PerfProbeTickGap gap)
        {
            long whole = gap.End - gap.Previous;
            // Busy time includes the run call that contained the interval's first tick. The line is
            // written one frame later on the main thread: its wall-clock start/end are shifted by
            // emittedAfterMs; startStamp/endStamp are exact.
            string detail = string.Format(C,
                "source={0} tickBefore={1} tickAfter={2} gapMs={3:F1} haltedMs={4:F1} afterHalt={5} haltKind={6} expectedTickMs={7:F1} " +
                "runCallsDuring={8} nativeCallsDuring={9} skipRunsDuring={10} noBufferDuring={11} simBusyMs={12:F1} simBusyShare={13:F2} " +
                "framesDuring={14} mapTicksDuring={15} lowFpsLevel={16} emittedAfterMs={17:F1}",
                gap.Source == 0 ? "onTick" : "mapTick", gap.TickBefore, gap.TickAfter, Ms(whole), Ms(gap.ActiveStart - gap.Previous),
                gap.ActiveStart > gap.Previous, HaltNames[Math.Max(0, Math.Min(3, gap.HaltKind))], Ms(gap.Expected),
                gap.RunCalls, gap.NativeCalls, gap.SkipCalls, gap.NoBuffer, Ms(gap.BusyTicks), whole <= 0 ? 0 : gap.BusyTicks / (double)whole,
                gap.Frames, gap.MapTicks, PerfProbeRuntime.LowFpsLevel, Ms(Stopwatch.GetTimestamp() - gap.End)) +
                Pacing(gap) + PerfProbeRuntime.FreezeMark(gap.End - gap.ActiveStart);
            PerfProbeRuntime.ReportSpike(gap.Source == 0 ? "tick:gap" : "tick:mapGap", gap.ActiveStart, gap.End, detail);
        }

        // Expected timer period = engine frame time / low-FPS level (Vanilla ResetFrameRate). Thread-pool
        // occupancy is read when the line is written, not during the gap.
        private static string Pacing(PerfProbeTickGap gap)
        {
            if (gap.TimerCalls < 0) return " timerProbe=False";
            int level = Math.Max(1, PerfProbeRuntime.LowFpsLevel);
            ThreadPool.GetMaxThreads(out int maxWorkers, out int maxIo);
            ThreadPool.GetAvailableThreads(out int freeWorkers, out int freeIo);
            return string.Format(C,
                " timerProbe=True timerCallsDuring={0} timerMaxIntervalMs={1:F1} expectedTimerMs={2:F1} framesToProcessMax={3} " +
                "framesToProcessAtEnd={4} threadPoolBusyWorkersAtEmit={5} threadPoolBusyIoAtEmit={6}",
                gap.TimerCalls, Ms(gap.TimerMaxInterval), Ms(gap.Expected) / level, gap.FramesToProcessMax, gap.FramesToProcessEnd,
                maxWorkers - freeWorkers, maxIo - freeIo);
        }

        // 5 s summary line (main thread). Runs are counted only after native work completed.
        internal static string Summary(double seconds)
        {
            if (threshold == 0) return "tickCounters disabled=true";
            long counted = Interlocked.Exchange(ref runsCounted, 0), tickCount = Interlocked.Exchange(ref windowTicks, 0);
            return string.Format(C,
                "tickCounters windowS={0:F1} subscribed={1} ticks={2} ticksPerS={3:F1} gapsOverThreshold={4} maxActiveGapMs={5:F1} " +
                "haltedGapsExcluded={6} mapTicks={7} mapTicksAvailable={8} nativeRunsCounted={9} runsWithoutTick={10} runsMultiTick={11} " +
                "maxTicksPerRun={12} runsWithoutMapTick={13} gapQueueDropped={14} lastTick={15} timerProbe={16} timerCalls={17} maxTimerIntervalMs={18:F1}",
                seconds, subscribed, tickCount, tickCount / Math.Max(0.001, seconds),
                Interlocked.Exchange(ref windowGaps, 0), Ms(Interlocked.Exchange(ref windowMaxActive, 0)), Interlocked.Exchange(ref windowHalted, 0),
                Interlocked.Exchange(ref windowMapTicks, 0), mapTicksAvailable, counted, Interlocked.Exchange(ref runsNoTick, 0),
                Interlocked.Exchange(ref runsMultiTick, 0), Interlocked.Exchange(ref maxTicksPerRun, 0), Interlocked.Exchange(ref runsNoMapTick, 0),
                queue.Dropped, Volatile.Read(ref lastTick), TimerProbe, Interlocked.Exchange(ref windowTimerCalls, 0),
                Ms(Interlocked.Exchange(ref windowTimerMaxInterval, 0)));
        }

        // Age of the last game tick, for frame spike lines (-1 = no tick yet).
        internal static double LastTickAgeMs(long now)
        {
            long last = Volatile.Read(ref lastTickStamp);
            return last == 0 ? -1 : Ms(now - last);
        }
    }
}
