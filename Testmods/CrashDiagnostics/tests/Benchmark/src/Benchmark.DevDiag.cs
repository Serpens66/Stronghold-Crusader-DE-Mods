using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Threading;
using APIShared;
using APIShared.Automation;
using APIShared.Simulation;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace CrashDiagnosticsBenchmark
{
    public sealed partial class Plugin
    {
        private static BenchmarkRuntime runtime;
        partial void InitializeBenchmark(ManualLogSource log)
        {
            if (runtime != null) return;
            runtime = new BenchmarkRuntime(log);
            runtime.Initialize();
        }
    }

    internal sealed class BenchmarkRuntime
    {
        private readonly ManualLogSource log;
        private ModApiClient api;
        private Guid runId;
        private string requestPath, fixture;
        private volatile bool apiReady;
        private bool requested, started;
        private long frames, tickEvents;
        private int lastFrame = -1;

        internal BenchmarkRuntime(ManualLogSource logger) { log = logger; }

        internal void Initialize()
        {
            requestPath = Path.Combine(Paths.ConfigPath, "SerpAutomation", Plugin.Guid + ".run");
            if (!File.Exists(requestPath)) return;
            if (new FileInfo(requestPath).Length > 4096) throw new InvalidDataException("Benchmark request is oversized.");
            string[] lines = File.ReadAllLines(requestPath);
            if (lines.Length < 2 || lines[1] != "benchmark") return;
            if (!Guid.TryParseExact(lines[0], "N", out runId) || runId == Guid.Empty)
                throw new InvalidDataException("Benchmark request has an invalid run ID.");
            fixture = lines.Length > 2 && !string.IsNullOrWhiteSpace(lines[2]) ? lines[2] :
                Path.Combine(Paths.BepInExRootPath, "SerpAutomationRollback", "fixture.sav");
            api = ApiShared.ForMod(Plugin.Guid);
            string reason;
            if (!SimulationTickEvents.TryRegister(Plugin.Guid, "benchmark.ticks", Tick, out reason))
                throw new InvalidOperationException("Benchmark tick observer unavailable: " + reason);
            requested = true;
            api.WhenReady(_ => apiReady = true);
            // Static publisher owns this runtime after SHCDE startup component cleanup.
            Application.onBeforeRender += BeforeRender;
            Stamp("BENCHMARK_READY request=" + runId.ToString("N"));
        }

        private void Tick(int tick) { Interlocked.Increment(ref tickEvents); }

        private void BeforeRender()
        {
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            long count = Interlocked.Increment(ref frames);
            if (started || !requested || !apiReady || count < 150) return;
            started = true;
            api.Automation.Start(runId, Steps(), log,
                result => Stamp("BENCHMARK_FINISHED status=" + result.Status + "; detail=" + result.Detail),
                quitWhenDone: true);
            File.Delete(requestPath);
        }

        private IEnumerable<AutomationStep> Steps()
        {
            yield return new AutomationStep("load shared benchmark fixture", TimeSpan.FromSeconds(180), Load, false);
            yield return new AutomationStep("warmup and 20-second performance window", TimeSpan.FromSeconds(60), Measure, true);
        }

        private IEnumerable<AutomationWait> Load(AutomationRun run)
        {
            foreach (var wait in run.LoadSave(fixture)) yield return wait;
            yield return run.WaitForPlayState(run.CurrentMission);
        }

        private IEnumerable<AutomationWait> Measure(AutomationRun run)
        {
            var mission = run.CurrentMission;
            foreach (var wait in run.SetPause(mission, false)) yield return wait;
            yield return run.WaitForTicks(mission, 5);
            yield return AutomationWait.Delay(TimeSpan.FromSeconds(2));
            long beforeFrames = Interlocked.Read(ref frames), beforeTicks = Interlocked.Read(ref tickEvents);
            double beforeCpu = CpuSeconds();
            long began = Stopwatch.GetTimestamp();
            yield return AutomationWait.Delay(TimeSpan.FromSeconds(20));
            double elapsed = (Stopwatch.GetTimestamp() - began) / (double)Stopwatch.Frequency;
            double cpu = CpuSeconds() - beforeCpu;
            long actualFrames = Interlocked.Read(ref frames) - beforeFrames;
            long actualTicks = Interlocked.Read(ref tickEvents) - beforeTicks;
            if (elapsed < 20 || cpu < 0 || actualFrames <= 0 || actualTicks <= 0)
                throw new InvalidOperationException("The benchmark window did not observe valid elapsed CPU/frame/tick data.");
            Stamp(string.Format(CultureInfo.InvariantCulture,
                "BENCHMARK elapsedSeconds={0:F6} cpuSeconds={1:F6} frames={2} tickEvents={3}",
                elapsed, cpu, actualFrames, actualTicks));
        }

        private static double CpuSeconds()
        {
            using (Process process = Process.GetCurrentProcess()) return process.TotalProcessorTime.TotalSeconds;
        }

        private void Stamp(string message) => log.LogInfo("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " + message);
    }
}
