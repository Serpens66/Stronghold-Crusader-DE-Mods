using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using BugfixesAndQoL;
using BugfixesAndQoL.VanillaPerformance;

internal static class FreezeTests
{
    internal static void Run(Action<bool, string> check, BepInEx.Logging.ManualLogSource log)
    {
        check(GateAttackFreezeRun.Instrumented, "freeze diagnostic installed after functional bridge");
        var direct = new GateAttackFreezeCapture.Run();
        long millisecond = Stopwatch.Frequency / 1000;
        direct.Depth = 2;
        direct.Stack[0] = new GateAttackFreezeCapture.Segment { Token = 1, Start = 0, Stage = GateAttackFreezeCapture.Builder };
        direct.Stack[1] = new GateAttackFreezeCapture.Segment { Token = 2, Start = 10 * millisecond, Stage = GateAttackFreezeCapture.OriginalBuilder };
        GateAttackFreezeCapture.Finish(direct, 2, 3, true, 70 * millisecond);
        GateAttackFreezeCapture.Finish(direct, 1, 3, true, 80 * millisecond);
        check(direct.Metrics[GateAttackFreezeCapture.Builder].Inclusive == 80 * millisecond &&
            direct.Metrics[GateAttackFreezeCapture.Builder].Exclusive == 20 * millisecond && direct.TopLevel == 80 * millisecond,
            "nested native and wrapper exclusive times do not double count");
        direct.Reset();
        for (int i = 0; i < 200; i++)
        {
            direct.Depth = 1;
            direct.Stack[0] = new GateAttackFreezeCapture.Segment { Token = i + 1, Start = 0, Stage = GateAttackFreezeCapture.Builder };
            GateAttackFreezeCapture.Finish(direct, i + 1, i % 2, true, 2 * millisecond);
        }
        check(direct.Metrics[GateAttackFreezeCapture.Builder].Calls == 200 &&
            direct.Metrics[GateAttackFreezeCapture.Builder].Inclusive == 400 * millisecond &&
            direct.Metrics[GateAttackFreezeCapture.Builder].Max == 2 * millisecond && direct.ExampleCount == 8,
            "200 short searches retain their 400ms sum and bounded examples");
        direct.Reset(); direct.Depth = 2;
        direct.Stack[0] = new GateAttackFreezeCapture.Segment { Token = 7, Stage = GateAttackFreezeCapture.Move };
        direct.Stack[1] = new GateAttackFreezeCapture.Segment { Token = 8, Stage = GateAttackFreezeCapture.Builder };
        GateAttackFreezeCapture.Finish(direct, 7, 1, true, millisecond);
        check(direct.PairErrors == 1 && direct.Depth == 0 && direct.Metrics[GateAttackFreezeCapture.Builder].Incomplete == 1,
            "missing nested Post is explicit incomplete evidence and cannot escape to another run");
        var cookie = GateAttackFreezeCapture.Begin(false);
        for (int i = 0; i < 100; i++) GateAttackFreezeCapture.ScopeFailure(new GateAttackFreezeCapture.Mismatch { Reason = "fixture", Tribe = i });
        check(cookie.Value.ScopeFailures == 100 && cookie.Value.MismatchCount == 4, "scope totals exact, detail storage bounded");
        GateAttackFreezeCapture.End(cookie, true);
        var overload = new GateAttackFreezeCapture.Cookie[40];
        for (int i = 0; i < overload.Length; i++) overload[i] = GateAttackFreezeCapture.Begin(false);
        check(overload.Any(x => x.Value == null), "capture pool overload drops evidence instead of allocating or blocking");
        for (int i = overload.Length - 1; i >= 0; i--) GateAttackFreezeCapture.End(overload[i], true);
        check(GateAttackFreezeCapture.Current == null, "pool exhaustion still restores the outer thread scope");
        MemoryBuffers.Available = false;
        int calls = EngineInterface.NativeCalls;
        check(EngineInterface.run(false) == 0 && EngineInterface.NativeCalls == calls && GateAttackFreezeCapture.Current == null,
            "no-buffer run preserves return and performs no fabricated native tick");
        MemoryBuffers.Available = true;
        GateAttackFreezeCapture.Run observed = null;
        EngineInterface.NativeAction = () => observed = GateAttackFreezeCapture.Current;
        check(EngineInterface.run(true) == 19 && observed.NativeEntered && observed.NativeCompleted && observed.Skip && observed.Released > 0,
            "real Harmony run phases preserve skip argument, native result and release");
        EngineInterface.ThrowNative = true;
        Exception error = null;
        try { EngineInterface.run(false); } catch (Exception ex) { error = ex; }
        check(error?.Message == "run-original" && !observed.NativeCompleted && !observed.Completed &&
            observed.Released > 0 && GateAttackFreezeCapture.Current == null && !Monitor.IsEntered(EngineInterface.Lock),
            "native exception unchanged and diagnostic finalizer restores scope after original finally");
        EngineInterface.ThrowNative = false;
        int depth = 0;
        EngineInterface.NativeAction = () =>
        {
            var parent = GateAttackFreezeCapture.Current;
            if (depth++ == 0)
            {
                check(EngineInterface.run(true) == 19, "nested run return unchanged");
                check(ReferenceEquals(parent, GateAttackFreezeCapture.Current), "nested run restores parent correlation");
            }
        };
        check(EngineInterface.run(false) == 23 && GateAttackFreezeCapture.Current == null, "outer scope restored");
        EngineInterface.NativeAction = null;
        GateAttackFreezeCapture.ReadCounters = (_, __) => throw new InvalidOperationException("diagnostic-only");
        check(EngineInterface.run(false) == 23 && GateAttackFreezeCapture.Current == null, "diagnostic counter fault cannot change native result");
        GateAttackFreezeCapture.ReadCounters = null;
        GateAttackFreezeCapture.Drain(10001);
        check(log.Lines.Any(x => x.Contains("GATE_FREEZE") && x.Contains("nativeEntered=")), "reports delivered only by render drain");
        var method = typeof(EngineInterface).GetMethod("run");
        var original = HarmonyLib.PatchProcessor.GetOriginalInstructions(method).ToList();
        original.RemoveAll(x => x.operand is System.Reflection.MethodInfo m && m.Name == "DLL_RunTick");
        var rejected = GateAttackFreezeRun.Transpile(original, method).ToList();
        check(rejected.Count == original.Count && rejected.Zip(original, ReferenceEquals).All(x => x), "unrecognized IL returned untouched");
        GateAttackFreezeRun.Transpile(HarmonyLib.PatchProcessor.GetOriginalInstructions(method), method).ToList();
    }
}
