using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;

namespace BugfixesAndQoL
{
    // Development-only, bounded numeric capture. The simulation never formats or writes a report.
    internal static class GateAttackFreezeCapture
    {
        internal const int Approach = 0, Consumer = 1, Move = 2, Builder = 3,
            OriginalApproach = 4, OriginalConsumer = 5, OriginalBuilder = 6, ManualProbe = 7, Stages = 8;
        private static readonly string[] Names = { "buildingApproach", "buildingConsumer", "unitMoveEvent", "builder",
            "originalApproach.filtered", "originalConsumer.filtered", "originalBuilder.filtered", "manualProbe" };
        internal struct Context
        {
            internal int Unit, Type, Tribe, Player, Command, Building, StartX, StartY, TargetX, TargetY;
            internal uint Global;
        }
        internal struct Metric { internal long Calls, Inclusive, Exclusive, Max, Positive, Zero, Unknown, Incomplete; }
        internal struct Segment { internal long Token, Start, Children; internal int Stage; internal Context Context; }
        internal struct Example { internal long Elapsed, Result; internal int Stage; internal bool Completed; internal Context Context; }
        internal struct Mismatch
        {
            internal string Reason, Role;
            internal int Tribe, RawPlayer, SnapshotOwner, LiveOwner, Leader, Control, Planning;
            internal uint SnapshotGlobal, LiveGlobal, LeaderGlobal;
        }
        internal sealed class Run
        {
            internal Run Parent;
            internal long Id, ParentId, Start, End, LockStart, Acquired, Released, NativeStart, NativeEnd, TopLevel,
                RejectedEdges, ScopeFailures;
            internal DateTime EndUtc;
            internal int Thread, Depth, ExampleCount, MismatchCount, PairErrors, CounterErrors,
                RepairBefore, RepairAfter, RebuildBefore, RebuildAfter, GcBefore, GcAfter;
            internal bool Skip, NativeEntered, NativeCompleted, LockObserved, CountersBefore, CountersAfter, Completed, FirstNative;
            internal readonly Metric[] Metrics = new Metric[Stages];
            internal readonly Segment[] Stack = new Segment[64];
            internal readonly Example[] Examples = new Example[8];
            internal readonly Mismatch[] Mismatches = new Mismatch[4];
            internal readonly ManualProbeCapture.Record Probes = new ManualProbeCapture.Record();
            internal void Reset()
            {
                Parent = null; Id = ParentId = Start = End = LockStart = Acquired = Released = NativeStart = NativeEnd = TopLevel =
                    RejectedEdges = ScopeFailures = 0;
                Thread = Depth = ExampleCount = MismatchCount = PairErrors = CounterErrors = 0;
                Skip = NativeEntered = NativeCompleted = LockObserved = CountersBefore = CountersAfter = Completed = FirstNative = false;
                Array.Clear(Metrics, 0, Metrics.Length); Array.Clear(Stack, 0, Stack.Length);
                Array.Clear(Examples, 0, Examples.Length); Array.Clear(Mismatches, 0, Mismatches.Length);
                Probes.Reset();
            }
        }
        internal struct Cookie { internal Run Value, Parent; }
        [ThreadStatic] private static Run active;
        [ThreadStatic] private static long nextToken;
        private static readonly ConcurrentQueue<Run> free = new ConcurrentQueue<Run>();
        private static readonly ConcurrentQueue<Run> ready = new ConcurrentQueue<Run>();
        private static readonly ConcurrentQueue<Mismatch> outsideScopes = new ConcurrentQueue<Mismatch>();
        private static readonly long[] calls = new long[Stages], ticks = new long[Stages];
        private static long nextRun, dropped, faults, normalRuns, noNative, nativeRuns, totalTicks,
            lastSummary, lastFrame, lastGap, lastScopeReport, summaryEdges, summaryScopes, reportWindow, reportsSuppressed;
        private static int outsideCount, windowReports;
        private static int initialized, firstNative, firstCallback, frameNumber = -1;
        internal static Action<Run, bool> ReadCounters;
        internal static Run Current => active;
        internal static long Faults => Interlocked.Read(ref faults);

        internal static void Initialize()
        {
            if (Interlocked.Exchange(ref initialized, 1) != 0) return;
            for (int i = 0; i < 32; i++) free.Enqueue(new Run());
            ManualProbeCapture.Initialize();
            lastSummary = Stopwatch.GetTimestamp();
            Shared.DevDiag.Log(() => "GATE_FREEZE ready thresholdMs=100 pool=32 examples=8 mismatchExamples=4 nativeOriginalsIncludeActiveHooks=true");
        }
        internal static void Fault() => Interlocked.Increment(ref faults);
        internal static Cookie Begin(bool skip)
        {
            var cookie = new Cookie { Parent = active };
            try
            {
                if (!free.TryDequeue(out Run run)) { Interlocked.Increment(ref dropped); active = null; return cookie; }
                run.Reset(); run.Parent = active; run.ParentId = active?.Id ?? 0; run.Id = Interlocked.Increment(ref nextRun);
                run.Thread = Thread.CurrentThread.ManagedThreadId; run.Skip = skip;
                run.Probes.Id = run.Probes.EngineRun = run.Id; run.Probes.Thread = run.Thread;
                run.Start = Stopwatch.GetTimestamp(); run.GcBefore = GC.CollectionCount(0);
                active = run; cookie.Value = run;
            }
            catch (Exception) { Fault(); active = null; }
            return cookie;
        }
        internal static void End(Cookie cookie, bool completed)
        {
            Run run = cookie.Value;
            try
            {
                if (run == null) return;
                run.End = Stopwatch.GetTimestamp(); run.EndUtc = DateTime.UtcNow;
                run.Completed = completed; run.GcAfter = GC.CollectionCount(0);
                while (run.Depth > 0) Finish(run, run.Stack[run.Depth - 1].Token, 0, false, run.End);
                Interlocked.Increment(ref normalRuns); Interlocked.Add(ref totalTicks, run.End - run.Start);
                if (run.NativeEntered) Interlocked.Increment(ref nativeRuns); else Interlocked.Increment(ref noNative);
                Interlocked.Add(ref summaryEdges, run.RejectedEdges); Interlocked.Add(ref summaryScopes, run.ScopeFailures);
                for (int i = 0; i < Stages; i++)
                { Interlocked.Add(ref calls[i], run.Metrics[i].Calls); Interlocked.Add(ref ticks[i], run.Metrics[i].Exclusive); }
                bool first = run.NativeCompleted && Interlocked.CompareExchange(ref firstNative, 1, 0) == 0;
                run.FirstNative = first;
                long previousScopeReport = Interlocked.Read(ref lastScopeReport);
                bool scopeExample = run.ScopeFailures != 0 && run.End - previousScopeReport >= Stopwatch.Frequency &&
                    Interlocked.CompareExchange(ref lastScopeReport, run.End, previousScopeReport) == previousScopeReport;
                bool report = first || scopeExample || run.End - run.Start >= Stopwatch.Frequency / 10 || run.PairErrors != 0 || !completed;
                long window = Interlocked.Read(ref reportWindow);
                if (run.End - window >= Stopwatch.Frequency && Interlocked.CompareExchange(ref reportWindow, run.End, window) == window)
                    Interlocked.Exchange(ref windowReports, 0);
                if (report && (first || Interlocked.Increment(ref windowReports) <= 4)) ready.Enqueue(run);
                else if (report) { Interlocked.Increment(ref reportsSuppressed); free.Enqueue(run); }
                else free.Enqueue(run);
            }
            catch (Exception) { Fault(); }
            finally { active = cookie.Parent; }
        }
        internal static long Enter(int stage, Context context)
        {
            try
            {
                Run run = active;
                if (run == null) return 0;
                if ((uint)stage >= Stages || run.Depth == run.Stack.Length) { run.PairErrors++; return 0; }
                if (context.Unit == 0 && context.Tribe == 0 && run.Depth > 0) context = run.Stack[run.Depth - 1].Context;
                long token = ++nextToken;
                run.Stack[run.Depth++] = new Segment { Token = token, Start = Stopwatch.GetTimestamp(), Stage = stage, Context = context };
                return token;
            }
            catch (Exception) { Fault(); return 0; }
        }
        internal static void Leave(long token, long result, bool completed)
        {
            try { if (token != 0 && active != null) Finish(active, token, result, completed, Stopwatch.GetTimestamp()); }
            catch (Exception) { Fault(); }
        }
        // Also used by deterministic tests; clock values, rather than sleeps, exercise accounting.
        internal static void Finish(Run run, long token, long result, bool completed, long ended)
        {
            int index = run.Depth - 1;
            while (index >= 0 && run.Stack[index].Token != token) index--;
            if (index < 0) { run.PairErrors++; return; }
            while (run.Depth - 1 > index)
            { run.PairErrors++; Finish(run, run.Stack[run.Depth - 1].Token, 0, false, ended); }
            Segment segment = run.Stack[--run.Depth]; run.Stack[run.Depth] = default;
            long elapsed = Math.Max(0, ended - segment.Start);
            ref Metric metric = ref run.Metrics[segment.Stage];
            metric.Calls++; metric.Inclusive += elapsed;
            metric.Exclusive += Math.Max(0, elapsed - segment.Children); metric.Max = Math.Max(metric.Max, elapsed);
            if (!completed) metric.Incomplete++;
            else if (result == int.MinValue) metric.Unknown++;
            else if (result > 0) metric.Positive++; else metric.Zero++;
            if (run.Depth > 0) run.Stack[run.Depth - 1].Children += elapsed;
            else run.TopLevel += elapsed;
            int example = run.ExampleCount;
            if (example == run.Examples.Length)
            {
                example = 0;
                for (int i = 1; i < run.Examples.Length; i++) if (run.Examples[i].Elapsed < run.Examples[example].Elapsed) example = i;
                if (run.Examples[example].Elapsed >= elapsed) return;
            }
            else run.ExampleCount++;
            run.Examples[example] = new Example { Stage = segment.Stage, Elapsed = elapsed, Context = segment.Context, Result = result, Completed = completed };
        }
        internal static void GateEdges(long amount)
        { try { if (active != null) active.RejectedEdges += amount; } catch (Exception) { Fault(); } }
        internal static void ScopeFailure(Mismatch value)
        {
            try
            {
                Run run = active;
                if (run == null)
                {
                    Interlocked.Increment(ref summaryScopes);
                    if (Interlocked.Increment(ref outsideCount) <= 16) outsideScopes.Enqueue(value);
                    else { Interlocked.Decrement(ref outsideCount); Interlocked.Increment(ref dropped); }
                    return;
                }
                run.ScopeFailures++;
                if (run.MismatchCount < run.Mismatches.Length) run.Mismatches[run.MismatchCount++] = value;
            }
            catch (Exception) { Fault(); }
        }
        internal static void Counters(bool before)
        { try { if (active != null) ReadCounters?.Invoke(active, before); } catch (Exception) { if (active != null) active.CounterErrors++; Fault(); } }
        private static double Ms(long value) => value * 1000.0 / Stopwatch.Frequency;
        internal static string Format(Run run)
        {
            var text = new StringBuilder(1800); var c = CultureInfo.InvariantCulture;
            void Time(string key, long value) => text.Append(' ').Append(key).Append('=').Append(value < 0 ? "unknown" : Ms(value).ToString("F3", c));
            text.Append("GATE_FREEZE run=").Append(run.Id).Append(" parentRun=").Append(run.ParentId).Append(" thread=").Append(run.Thread)
                .Append(" endUtc=").Append(run.EndUtc.ToString("o", c)).Append(" skipArgument=").Append(run.Skip)
                .Append(" completed=").Append(run.Completed).Append(" nativeEntered=").Append(run.NativeEntered)
                .Append(" nativeCompleted=").Append(run.NativeCompleted).Append(" firstCompletedNativeCall=").Append(run.FirstNative)
                .Append(" originalTimesIncludeActiveHooks=true contextsAreEntrySnapshots=true");
            Time("totalMs", run.End - run.Start);
            Time("lockWaitMs", run.LockObserved && run.Acquired != 0 ? run.Acquired - run.LockStart : -1);
            Time("preLockMs", run.LockObserved ? run.LockStart - run.Start : -1);
            Time("lockedPreNativeMs", run.NativeEntered ? run.NativeStart - run.Acquired : -1);
            Time("nativeInclusiveMs", run.NativeCompleted ? run.NativeEnd - run.NativeStart : -1);
            Time("postNativeMs", run.NativeCompleted && run.Released != 0 ? run.Released - run.NativeEnd : -1);
            Time("outsideMeasuredScopesMs", Math.Max(0, run.End - run.Start - run.TopLevel));
            text.Append(" gcCollectionsDuring=").Append(run.GcAfter - run.GcBefore).Append(" gcPauseMeasured=false")
                .Append(" rejectedGateEdges=").Append(run.RejectedEdges).Append(" scopeFailures=").Append(run.ScopeFailures)
                .Append(" pairingErrors=").Append(run.PairErrors).Append(" counterErrors=").Append(run.CounterErrors)
                .Append(" reconstructFailuresDelta=").Append(run.CountersBefore && run.CountersAfter ? unchecked((uint)(run.RepairAfter - run.RepairBefore)).ToString(c) : "unknown")
                .Append(" pclRebuildsDelta=").Append(run.CountersBefore && run.CountersAfter ? unchecked((uint)(run.RebuildAfter - run.RebuildBefore)).ToString(c) : "unknown");
            for (int i = 0; i < Stages; i++)
            {
                Metric m = run.Metrics[i]; if (m.Calls == 0) continue;
                text.Append(" | ").Append(Names[i]).Append(" calls=").Append(m.Calls);
                Time("inclusiveMs", m.Inclusive); Time("exclusiveMs", m.Exclusive); Time("maxMs", m.Max);
                text.Append(" positive=").Append(m.Positive).Append(" nonpositive=").Append(m.Zero)
                    .Append(" noResultContract=").Append(m.Unknown).Append(" incomplete=").Append(m.Incomplete);
            }
            for (int i = 0; i < run.ExampleCount; i++)
            {
                Example e = run.Examples[i]; Context x = e.Context;
                text.Append(" | sample=").Append(Names[e.Stage]); Time("ms", e.Elapsed);
                text.Append(" unit=").Append(x.Unit).Append("/g").Append(x.Global).Append(" type=").Append(x.Type)
                    .Append(" tribe=").Append(x.Tribe).Append(" player=").Append(x.Player).Append(" command=").Append(x.Command)
                    .Append(" building=").Append(x.Building).Append(" start=").Append(x.StartX).Append(',').Append(x.StartY)
                    .Append(" target=").Append(x.TargetX).Append(',').Append(x.TargetY).Append(" result=")
                    .Append(e.Result == int.MinValue ? "none" : e.Result.ToString(c)).Append(" completed=").Append(e.Completed);
            }
            for (int i = 0; i < run.MismatchCount; i++)
            {
                Mismatch m = run.Mismatches[i]; text.Append(" | scopeFailure=").Append(m.Reason).Append(" role=").Append(m.Role)
                    .Append(" tribe=").Append(m.Tribe).Append(" snapshot=").Append(m.SnapshotOwner).Append("/g").Append(m.SnapshotGlobal)
                    .Append(" live=").Append(m.LiveOwner).Append("/g").Append(m.LiveGlobal).Append(" leader=").Append(m.Leader)
                    .Append("/g").Append(m.LeaderGlobal).Append(" control=").Append(m.Control).Append(" rawPlayer=").Append(m.RawPlayer)
                    .Append(" planning=").Append(m.Planning);
            }
            if (run.Probes.Attempts != 0 || run.Probes.Hits != 0 || run.Probes.NativeAvoided != 0) text.Append(" | ").Append(ManualProbeCapture.Format(run.Probes, false, true));
            return text.ToString();
        }
        internal static void Drain(int frame)
        {
            try
            {
                if (frameNumber == frame) return; frameNumber = frame;
                long now = Stopwatch.GetTimestamp();
                if (Interlocked.Exchange(ref firstCallback, 1) == 0) Shared.DevDiag.Log(() => "GATE_FREEZE renderCallbackConfirmed=true");
                if (lastFrame != 0 && now - lastFrame >= Stopwatch.Frequency / 10 && now - lastGap >= Stopwatch.Frequency)
                {
                    long gap = now - lastFrame; lastGap = now;
                    Shared.DevDiag.Log(() => "GATE_FREEZE renderCallbackGapMs=" + Ms(gap).ToString("F3", CultureInfo.InvariantCulture) + " cause=unattributed");
                }
                lastFrame = now;
                ManualProbeCapture.Drain();
                if (outsideScopes.TryDequeue(out Mismatch outside))
                {
                    Interlocked.Decrement(ref outsideCount);
                    var scope = new Run { MismatchCount = 1 }; scope.Mismatches[0] = outside;
                    Shared.DevDiag.Log(() => "GATE_FREEZE outsideEngineRun=true " + Format(scope));
                }
                for (int i = 0; i < 8 && Stopwatch.GetTimestamp() - now < Stopwatch.Frequency / 1000; i++)
                {
                    if (!ready.TryDequeue(out Run run)) break;
                    try { Shared.DevDiag.Log(() => Format(run)); } finally { free.Enqueue(run); }
                }
                if (now - lastSummary < Stopwatch.Frequency * 5) return;
                lastSummary = now;
                var summary = new StringBuilder("GATE_FREEZE summary");
                summary.Append(" runs=").Append(Interlocked.Exchange(ref normalRuns, 0)).Append(" nativeCalls=").Append(Interlocked.Exchange(ref nativeRuns, 0))
                    .Append(" noNativeCalls=").Append(Interlocked.Exchange(ref noNative, 0)).Append(" totalMs=").Append(Ms(Interlocked.Exchange(ref totalTicks, 0)).ToString("F3", CultureInfo.InvariantCulture))
                    .Append(" captureDropped=").Append(Interlocked.Exchange(ref dropped, 0)).Append(" diagnosticFaultsTotal=").Append(Faults)
                    .Append(" detailReportsSuppressed=").Append(Interlocked.Exchange(ref reportsSuppressed, 0))
                    .Append(" rejectedEdges=").Append(Interlocked.Exchange(ref summaryEdges, 0)).Append(" scopeFailures=").Append(Interlocked.Exchange(ref summaryScopes, 0));
                for (int i = 0; i < Stages; i++) summary.Append(" | ").Append(Names[i]).Append(" calls=").Append(Interlocked.Exchange(ref calls[i], 0))
                    .Append(" exclusiveMs=").Append(Ms(Interlocked.Exchange(ref ticks[i], 0)).ToString("F3", CultureInfo.InvariantCulture));
                Shared.DevDiag.Log(() => summary.ToString());
            }
            catch (Exception) { Fault(); }
        }
    }
}
