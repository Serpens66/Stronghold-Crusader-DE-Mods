using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using APIShared.Automation;
using APIShared.Simulation;
using CrusaderDE;
using SHCDESE.API;

namespace PathfindingLoadTest
{
    // Invokes the installed production method in a paused, owned test mission. No copied
    // watcher, substitute runtime, native writes, timing hooks or OS input are used.
    internal static class WatchBenchmark
    {
        private const int CallsPerBatch = 2048, Batches = 5;
        private static readonly BindingFlags Fields = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Static | BindingFlags.Instance;
        // Mono's Process.ProcessorAffinity returned zero after a successful-looking setter.
        // Verify the owned Windows process directly rather than timing unpinned runs.
        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetProcessAffinityMask(IntPtr process, UIntPtr mask);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetProcessAffinityMask(IntPtr process, out UIntPtr mask, out UIntPtr systemMask);

        internal static IEnumerable<AutomationStep> Steps(Action<string> log)
        {
            yield return new AutomationStep("load-watch-save", TimeSpan.FromSeconds(150),
                run => run.LoadSave("test_stresstest5.sav"));
            yield return new AutomationStep("prepare-watch-benchmark", TimeSpan.FromSeconds(60),
                run => Prepare(run, log), true);
            yield return new AutomationStep("production-watch-benchmark", TimeSpan.FromSeconds(60),
                run => Measure(run, log), true);
        }

        private static IEnumerable<AutomationWait> Prepare(AutomationRun run, Action<string> log)
        {
            yield return run.WaitForPlayState(run.CurrentMission);
            var process = GetCurrentProcess();
            if (!SetProcessAffinityMask(process, new UIntPtr(0xFFFFu)) ||
                !GetProcessAffinityMask(process, out UIntPtr affinity, out _) || affinity.ToUInt64() != 0xFFFF)
                throw new InvalidOperationException("Windows did not retain benchmark CPU affinity: " + Marshal.GetLastWin32Error());
            if (MainViewModel.Instance.Show_HUD_Briefing)
                MainViewModel.Instance.ButtonBriefingResumeCommand.Execute(null);
            yield return AutomationWait.Until("briefing closed", () => !MainViewModel.Instance.Show_HUD_Briefing);
            Director.instance.SetEngineFrameRate(250);
            log("WATCH_PREP before " + Snapshot(run));
            // Exercise both ordinary pause transitions and record their actual state. A save's
            // initial local/published pause state is an observation, not a readiness assumption.
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            log("WATCH_PREP paused " + Snapshot(run));
            foreach (var wait in run.SetPause(run.CurrentMission, false)) yield return wait;
            log("WATCH_PREP resumed " + Snapshot(run));
            yield return run.WaitForTicks(run.CurrentMission, 64);
            foreach (var wait in run.SetPause(run.CurrentMission, true)) yield return wait;
            yield return AutomationWait.Delay(TimeSpan.FromSeconds(1));
        }

        private static string Snapshot(AutomationRun run) => run.InSimulation(run.CurrentMission, () => {
            bool scoped = SimulationClock.TryGetCurrentTick(run.CurrentMission, out int tick);
            return "raw=" + GameTimeManagerAPI.Instance.GetElapsedMapTicks() + " scopedAvailable=" + scoped +
                " scoped=" + tick + " localPaused=" + GamePlayerManagerAPI.Instance.IsLocalPaused() +
                " publishedPaused=" + GameData.Instance.lastGameState.game_paused +
                " directorPaused=" + Director.instance.Paused + " simRunning=" + Director.instance.SimRunning;
        });

        private static IEnumerable<AutomationWait> Measure(AutomationRun run, Action<string> log)
        {
            var assembly = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Pathfinding");
            var host = assembly.GetType("Pathfinding.Engine.EngineHost", true);
            var provider = RequiredField(host, "runtime").GetValue(null) ??
                throw new InvalidOperationException("Pathfinding engine provider unavailable.");
            var type = provider.GetType();
            var method = type.GetMethod("WatchNativeDirections", Fields, null, Type.EmptyTypes, null) ??
                throw new MissingMethodException(type.FullName, "WatchNativeDirections");
            var watch = (Action)Delegate.CreateDelegate(typeof(Action), provider, method);
            var cursor = RequiredField(type, "anchorWatchCursor");
            var primed = RequiredField(type, "anchorWatchPrimed");
            var changes = RequiredField(type, "anchorWatchChanges");
            var tiles = RequiredField(type, "anchorWatchTiles");
            var bytes = RequiredField(type, "anchorWatchBytes");
            var layout = assembly.GetType("Pathfinding.UnitCommands.UnitCommandPathRuntime", true);
            var width = (int)RequiredField(layout, "MapWidth").GetRawConstantValue();
            var budget = (int)RequiredField(type, "AnchorWatchNodesPerTick").GetRawConstantValue();
            int total = checked(width * width);
            if (total % budget != 0 || CallsPerBatch % (total / budget) != 0)
                throw new InvalidOperationException("Benchmark calls must contain complete watcher rounds.");
            run.InSimulation(run.CurrentMission, () => {
                if (!(RequiredField(type, "anchorAvailable").GetValue(provider) is bool available) || !available ||
                    (int)primed.GetValue(provider) <= 0)
                    throw new InvalidOperationException("Normally scheduled watcher has not initialized.");
                // Warm JIT/current shadows; this preparation is excluded from timed samples.
                for (int i = 0; i < 32; i++) watch();
                if ((int)primed.GetValue(provider) != total ||
                    !((tiles.GetValue(provider) as int[])?.Length == total) ||
                    !((bytes.GetValue(provider) as byte[])?.Length == total))
                    throw new InvalidOperationException("Watcher priming is incomplete.");
                return 0;
            });
            string ilHash;
            using (var sha = SHA256.Create())
                ilHash = BitConverter.ToString(sha.ComputeHash(method.GetMethodBody().GetILAsByteArray())).Replace("-", "");
            log("WATCH_BENCH_READY mvid=" + assembly.ManifestModule.ModuleVersionId + " ilSHA256=" + ilHash +
                " calls=" + CallsPerBatch + " coordinateNodesPerCall=" + budget +
                " includesPreparation=False worldPaused=True affinity=0xFFFF delegates=actual-installed-method");
            for (int batch = 0; batch < Batches; batch++)
            {
                int ordinal = batch;
                string result = run.InSimulation(run.CurrentMission, () => {
                    int beforeCursor = (int)cursor.GetValue(provider);
                    long beforeChanges = (long)changes.GetValue(provider);
                    long started = Stopwatch.GetTimestamp();
                    for (int i = 0; i < CallsPerBatch; i++) watch();
                    long elapsed = Stopwatch.GetTimestamp() - started;
                    long changed = (long)changes.GetValue(provider) - beforeChanges;
                    if (changed != 0 || (int)cursor.GetValue(provider) != beforeCursor ||
                        (int)primed.GetValue(provider) != total)
                        throw new InvalidOperationException("Benchmark input changed or a watcher round was incomplete: changes=" + changed);
                    return "WATCH_BENCH batch=" + ordinal + " calls=" + CallsPerBatch + " ms=" +
                        (elapsed * 1000.0 / Stopwatch.Frequency).ToString("F3", CultureInfo.InvariantCulture) +
                        " usPerCall=" + (elapsed * 1000000.0 / Stopwatch.Frequency / CallsPerBatch).ToString("F3", CultureInfo.InvariantCulture) +
                        " changes=0 cursor=" + beforeCursor + " primed=" + total;
                });
                log(result);
                yield return AutomationWait.NextFrame;
            }
        }

        private static FieldInfo RequiredField(Type type, string name) => type.GetField(name, Fields) ??
            throw new MissingFieldException(type.FullName, name);
    }
}
