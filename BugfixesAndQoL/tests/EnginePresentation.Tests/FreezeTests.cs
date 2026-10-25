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
        ProbeTests(check, log);
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

    private static void ProbeTests(Action<bool, string> check, BepInEx.Logging.ManualLogSource log)
    {
        long ms = Stopwatch.Frequency / 1000;
        var record = new ManualProbeCapture.Record();
        for (int i = 0; i < 200; i++)
        {
            var probe = new ManualProbeCapture.Probe {
                Enabled = true, Seen = 15, Saved = ms, Planned = 2 * ms, Restored = 3 * ms,
                Origin = ManualProbeCapture.TileCursor, Result = i % 2, Completed = true, RestoreCompleted = true,
                Context = new ManualProbeCapture.Context { Unit = new GateAttackFreezeCapture.Context { Unit = i + 1 } } };
            ManualProbeCapture.Account(record, probe, 7 * ms);
        }
        check(record.Probes == 200 && record.Positive == 100 && record.Negative == 100 && record.Phases.Sum(x => x.Sum) == 1400 * ms &&
            record.Phases.All(x => x.Calls == 200) && record.Phases[ManualProbeCapture.Administration].Sum == 200 * ms,
            "many short probes sum disjoint phases with explicit administration remainder");
        check(record.Phases[ManualProbeCapture.Restore].Max == 3 * ms && record.Phases[ManualProbeCapture.Restore].Context.Unit.Unit == 200,
            "sub-threshold phase maximum retains its matching context");
        var merge = new ManualProbeCapture.Record(); ManualProbeCapture.Merge(merge, record);
        check(merge.Probes == 200 && merge.Phases.Sum(x => x.Sum) == 1400 * ms && merge.Phases.Length == 4 && merge.Reasons.Length == 7,
            "summary merge retains sums and fixed bounded context storage");
        ManualProbeCapture.ExchangeSummary();
        var scope = ManualProbeCapture.BeginOperation(ManualProbeCapture.TileCursor);
        check(scope.Value != null && scope.Value.EngineRun == 0 && scope.Value.Id > 0, "cursor operation outside engine has independent identity");
        using (ManualProbeCapture.BeginOperation(ManualProbeCapture.BuildingCursor))
            check(ManualProbeCapture.Current.Origin == ManualProbeCapture.BuildingCursor, "nested caller attribution uses actual probe origin");
        check(ManualProbeCapture.Current.Origin == ManualProbeCapture.TileCursor, "nested origin restores enclosing operation");
        ManualProbeCapture.Cache(true); ManualProbeCapture.Cache(false); ManualProbeCapture.Invalidate(true, true, false);
        ManualProbeCapture.RegionReject(0); ManualProbeCapture.RegionReject(1); ManualProbeCapture.RegionReject(2);
        var p = ManualProbeCapture.BeginProbe(default, true);
        ManualProbeCapture.Copied(ref p, false, 0, 0, 1168);
        ManualProbeCapture.Mark(ref p, ManualProbeCapture.Native);
        ManualProbeCapture.Mark(ref p, ManualProbeCapture.Restore);
        ManualProbeCapture.Copied(ref p, true, 3967084, 1283200, 1224);
        p.RestoreCompleted = true; ManualProbeCapture.Result(ref p, -1); ManualProbeCapture.EndProbe(ref p);
        check(scope.Value.Hits == 1 && scope.Value.Misses == 1 && scope.Value.Probes == 1 && scope.Value.Reused == 1 &&
            scope.Value.SaveManager == 0 && scope.Value.SaveSmall == 1168 && scope.Value.RestoreManager == 3967084,
            "cache events separate from actual reused probe and exact copied bytes");
        scope.Dispose();
        var summary = ManualProbeCapture.ExchangeSummary();
        check(summary.RejectCandidates == 1 && summary.RejectLearned == 1 && summary.RejectHits == 1 && summary.NativeAvoided == 1 && summary.Probes == 1,
            "region proof counters aggregate separately from actual native probes and cursor cache hits");
        check(summary.Probes == 1 && summary.MaxContext.Operation > 0 && summary.MaxContext.EngineRun == 0 && summary.Phases[0].Calls == 1,
            "five-second summary retains even a small probe and its operation context");
        var run = GateAttackFreezeCapture.Begin(false);
        using (ManualProbeCapture.BeginOperation(ManualProbeCapture.GroupFallback))
        {
            check(ManualProbeCapture.Current.EngineRun == run.Value.Id, "probe operation correlates with actual engine run");
            ManualProbeCapture.RegionReject(0); ManualProbeCapture.RegionReject(2);
            check(run.Value.Probes.RejectCandidates == 1 && run.Value.Probes.NativeAvoided == 1, "avoided probes retain actual engine correlation");
            ManualProbeCapture.GroundReject(0, Stopwatch.GetTimestamp(), 0, 0);
            ManualProbeCapture.GroundReject(1, 0, 0, 0);
            ManualProbeCapture.GroundReject(2, 0, 0, 0);
            ManualProbeCapture.GroundReject(7, 0, 12, 12);
            check(run.Value.Probes.GroundReasons[1] == 1 && run.Value.Probes.GroundReasons[2] == 1 && run.Value.Probes.NativeAvoided == 2 &&
                ManualProbeCapture.Format(run.Value.Probes, false, true).Contains("regionSample=12->12"),
                "ground proof cost, hits, region context and avoided copies retain engine correlation");
            var failure = ManualProbeCapture.BeginProbe(default, false);
            ManualProbeCapture.Mark(ref failure, ManualProbeCapture.Native);
            ManualProbeCapture.Mark(ref failure, ManualProbeCapture.Restore);
            failure.RestoreCompleted = true; ManualProbeCapture.EndProbe(ref failure);
            check(ManualProbeCapture.Current.Incomplete == 1, "native exception has incomplete result and still records restoration phase");
            var nestedRun = GateAttackFreezeCapture.Begin(false);
            var nestedProbe = ManualProbeCapture.BeginProbe(default, false);
            ManualProbeCapture.Mark(ref nestedProbe, ManualProbeCapture.Native);
            ManualProbeCapture.Result(ref nestedProbe, 1);
            ManualProbeCapture.Mark(ref nestedProbe, ManualProbeCapture.Restore);
            nestedProbe.RestoreCompleted = true;
            ManualProbeCapture.EndProbe(ref nestedProbe);
            check(nestedRun.Value.Probes.Probes == 1 && nestedRun.Value.Probes.MaxContext.EngineRun == nestedRun.Value.Id &&
                ManualProbeCapture.Current.EngineRun == run.Value.Id,
                "nested engine run gets its own probe phases while enclosing operation retains its start identity");
            GateAttackFreezeCapture.End(nestedRun, true);
        }
        check(run.Value.Metrics[GateAttackFreezeCapture.ManualProbe].Calls == 1 && run.Value.Probes.Probes == 1 &&
            run.Value.Probes.Phases[ManualProbeCapture.Restore].Calls == 1,
            "probe participates in nested engine accounting including disjoint phases below operation threshold");
        GateAttackFreezeCapture.End(run, true);
        int lines = log.Lines.Count;
        var overload = new ManualProbeCapture.Scope[100];
        // Keep completed slow records in the bounded queue without draining it.
        for (int i = 0; i < overload.Length; i++)
        {
            overload[i] = ManualProbeCapture.BeginOperation(ManualProbeCapture.Unknown);
            if (overload[i].Value != null) { overload[i].Value.Start -= Stopwatch.Frequency; overload[i].Value.Probes = 1; }
            overload[i].Dispose();
        }
        check(log.Lines.Count == lines && ManualProbeCapture.Current == null, "hot-path capture never logs and throttled records restore operation state");
        ManualProbeCapture.Drain();
        check(log.Lines.Count - lines <= 5 && log.Lines.Any(x => x.Contains("MANUAL_PROBE operation=")), "render delivery is bounded and reports 20ms operation evidence");
    }
}
