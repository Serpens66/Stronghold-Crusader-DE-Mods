using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;

// Only the external log sink is replaced in this helper assembly.
namespace Shared { internal static class DevDiag { internal static void Log(Func<string> text) { } } }

namespace PerfProbe
{
    // Tests the actual production helpers in an in-memory test assembly, not a mod build.
    public static class DiagnosticHelperTests
    {
        private static readonly object threadLock = new object();
        private static void SetAchData() { }
        private static void OtherMethod() { }
        private static void Check(bool condition, string reason)
        {
            if (!condition) throw new InvalidOperationException(reason);
        }

        public static string Run()
        {
            object gate = new object();
            var before = PerfProbeLockTiming.Capture();
            bool taken = false, nested = false;
            PerfProbeLockTiming.Enter(gate, ref taken);
            try
            {
                Check(taken && Monitor.IsEntered(gate), "Original Monitor acquisition/ref flag lost");
                PerfProbeLockTiming.Enter(gate, ref nested);
                try { Check(nested, "Reentrant acquisition failed"); }
                finally { PerfProbeLockTiming.Exit(gate); }
            }
            finally { PerfProbeLockTiming.Exit(gate); }
            var after = PerfProbeLockTiming.Capture();
            Check(after.Acquisitions - before.Acquisitions == 2 && after.Wait >= before.Wait && after.Held >= before.Held,
                "Reentrant accounting failed");
            Check(!Monitor.IsEntered(gate), "Monitor leaked");

            bool invalidTaken = true;
            bool rejected = false;
            try { PerfProbeLockTiming.Enter(gate, ref invalidTaken); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected && invalidTaken && !Monitor.IsEntered(gate), "Monitor.Enter exception/ref contract changed");
            rejected = false;
            try { PerfProbeLockTiming.Exit(gate); }
            catch (SynchronizationLockException) { rejected = true; }
            Check(rejected && PerfProbeLockTiming.Capture().Acquisitions == after.Acquisitions, "Invalid Exit changed counters");

            var expected = new ApplicationException("original");
            taken = false;
            try
            {
                PerfProbeLockTiming.Enter(gate, ref taken);
                try { throw expected; }
                finally { if (taken) PerfProbeLockTiming.Exit(gate); }
            }
            catch (ApplicationException actual) { Check(ReferenceEquals(expected, actual), "Original exception replaced"); }
            Check(!Monitor.IsEntered(gate), "Exceptional finally failed to release");

            using (var ready = new ManualResetEventSlim())
            using (var release = new ManualResetEventSlim())
            {
                var holder = new Thread(() => { lock (gate) { ready.Set(); release.Wait(); } });
                holder.Start();
                Check(ready.Wait(10000), "Fixture holder did not start");
                before = PerfProbeLockTiming.Capture();
                using (var timer = new Timer(_ => release.Set(), null, 120, Timeout.Infinite))
                {
                    taken = false;
                    PerfProbeLockTiming.Enter(gate, ref taken);
                    try { Check(taken, "Contended acquisition failed"); }
                    finally { PerfProbeLockTiming.Exit(gate); }
                }
                Check(holder.Join(10000), "Fixture holder did not exit");
                after = PerfProbeLockTiming.Capture();
                Check(after.Acquisitions == before.Acquisitions + 1 && after.Wait > before.Wait,
                    "Real Monitor contention not accounted for");
            }

            var code = MakeScope();
            var label = default(Label);
            code[1].labels.Add(label);
            code[1].blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
            var rewritten = new List<CodeInstruction>(PerfProbeLockTiming.Transpile(code, FixtureMethod("SetAchData")));
            Check(rewritten.Count == code.Count && ReferenceEquals(rewritten[1], code[1]) &&
                rewritten[1].labels.Count == 1 && rewritten[1].blocks.Count == 1, "IL control-flow metadata changed");
            Check(((MethodInfo)rewritten[1].operand).DeclaringType == typeof(PerfProbeLockTiming) &&
                ((MethodInfo)rewritten[2].operand).DeclaringType == typeof(PerfProbeLockTiming), "Monitor operands not rewritten");
            rejected = false;
            try { PerfProbeLockTiming.Transpile(MakeScope(), FixtureMethod("OtherMethod")); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "Unknown wrapper must fail closed");
            code = MakeScope(); code.Add(code[1]); rejected = false;
            try { PerfProbeLockTiming.Transpile(code, FixtureMethod("SetAchData")); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected, "Changed lock scope must fail closed");
            code = MakeScope(); code[0].labels.Add(label); var originalOperand = code[1].operand;
            rewritten = new List<CodeInstruction>(PerfProbeLockTiming.SafeTranspile(code, FixtureMethod("OtherMethod")));
            Check(ReferenceEquals(rewritten[1], code[1]) && Equals(code[1].operand, originalOperand) && code[0].labels.Count == 1,
                "Rejected optional lock probe mutated original IL");
            rewritten = new List<CodeInstruction>(PerfProbeRunTiming.SafeTranspile(code, FixtureMethod("OtherMethod")));
            Check(!PerfProbeRunTiming.Instrumented && ReferenceEquals(rewritten[0], code[0]) && Equals(code[1].operand, originalOperand),
                "Rejected run anchors mutated original IL or claimed measured phases");

            var history = new PerfProbeSampleHistory(3, 2);
            var raw = new ulong[] { 11, 12 };
            history.Add(10, raw, 0, 2); raw[0] = 21;
            history.Add(20, raw, 0, 1); raw[0] = 31;
            history.Add(30, raw, 0, 2); raw[0] = 41;
            history.Add(40, raw, 0, 1); raw[0] = 99;
            var times = new List<long>(); var leaves = new List<ulong>();
            int matches = history.Visit(20, 30, (addresses, offset, depth, stamp) =>
            { times.Add(stamp); leaves.Add(addresses[offset]); Check(depth > 0, "Missing raw depth"); });
            Check(history.Oldest == 20 && matches == 2 && times[0] == 20 && times[1] == 30 &&
                leaves[0] == 21 && leaves[1] == 31, "History wrap, copy or exact interval boundaries failed");
            Check(history.Visit(1, 9, (a, b, c, d) => { throw new Exception("Outside event"); }) == 0, "Outside-event sample included");

            long cpu = PerfProbeLockTiming.ThreadCpuTime();
            Thread.SpinWait(100000);
            long laterCpu = PerfProbeLockTiming.ThreadCpuTime();
            Check(cpu < 0 || laterCpu >= cpu, "Thread CPU clock moved backwards");
            TestRunPhases();
            TestTransfer();
            var ownerHistory = new PerfProbeLockHistory(2);
            ownerHistory.Add(new PerfProbeLockHistory.Span { Started = 1, Acquired = 2, Ended = 4 });
            ownerHistory.Add(new PerfProbeLockHistory.Span { Started = 5, Acquired = 6, Ended = 8 });
            ownerHistory.Add(new PerfProbeLockHistory.Span { Started = 9, Acquired = 10, Ended = 12 });
            int intervals = ownerHistory.Visit(1, 20, _ => { }, out bool clipped, out int lost);
            Check(intervals == 2 && clipped && lost == 0, "Overwritten lock owner history must be explicit");
            TestConcurrentLockHistory();
            return "PASS: production Monitor and transformed run IL (no buffer/native/skip/nesting/exception/finally); unchanged rejected IL; bounded history and 10,000 concurrent batch transfers; CPU clock.";
        }

        private static void TestRunPhases()
        {
            long native = PerfProbeRunTiming.NativeCallsStarted, empty = PerfProbeRunTiming.BuffersUnavailable;
            var parent = PerfProbeRunTiming.Begin(true);
            PerfProbeRunTiming.BeforeBuffer(); PerfProbeRunTiming.BufferResult(null);
            var result = PerfProbeRunTiming.Complete(parent);
            Check(result.Skip && result.NoBuffer && !result.NativeEntered && result.NativeCpu == -1 &&
                PerfProbeRunTiming.NativeCallsStarted == native && PerfProbeRunTiming.BuffersUnavailable == empty + 1,
                "No-buffer run must not become a native call or CPU measurement");
            parent = PerfProbeRunTiming.Begin(false);
            PerfProbeRunTiming.BeforeBuffer(); PerfProbeRunTiming.BufferResult(new object());
            PerfProbeRunTiming.Acquired(Stopwatch.GetTimestamp());
            var nested = PerfProbeRunTiming.Begin(true);
            PerfProbeRunTiming.BeforeBuffer(); PerfProbeRunTiming.BufferResult(null);
            PerfProbeRunTiming.Complete(nested);
            PerfProbeRunTiming.BeforeNative(); PerfProbeRunTiming.AfterNative();
            PerfProbeRunTiming.Released(Stopwatch.GetTimestamp());
            result = PerfProbeRunTiming.Complete(parent);
            Check(!result.Skip && !result.NoBuffer && result.NativeEntered && result.NativeCompleted &&
                result.Acquired <= result.NativeStarted && result.NativeEnded <= result.Released, "Nested run phase restoration failed");
            long completed = PerfProbeRunTiming.NativeCallsCompleted;
            parent = PerfProbeRunTiming.Begin(false);
            PerfProbeRunTiming.BeforeNative(); result = PerfProbeRunTiming.Complete(parent);
            Check(result.NativeEntered && !result.NativeCompleted && PerfProbeRunTiming.NativeCallsCompleted == completed,
                "Failed native call must remain incomplete");

            Func<bool, int> run = RunIlFixture.Create();
            RunFixture.BufferAvailable = false;
            parent = PerfProbeRunTiming.Begin(false);
            Check(run(false) == 0, "No buffer result changed");
            result = PerfProbeRunTiming.Complete(parent);
            Check(result.NoBuffer && !result.NativeEntered && RunFixture.NativeCalls == 0, "Executed no-buffer IL path changed");
            RunFixture.BufferAvailable = true;
            parent = PerfProbeRunTiming.Begin(true);
            Check(run(true) == 37 && RunFixture.LastSkip, "Native result/skip argument changed");
            result = PerfProbeRunTiming.Complete(parent);
            Check(result.NativeEntered && result.NativeCompleted && RunFixture.NativeCalls == 1 && !Monitor.IsEntered(RunFixture.threadLock),
                "Executed phase/lock/original counts failed");
            RunFixture.Fail = true; parent = PerfProbeRunTiming.Begin(false);
            try { run(false); Check(false, "Missing original exception"); }
            catch (ApplicationException error) { Check(ReferenceEquals(error, RunFixture.Error), "Transpiler changed original exception"); }
            result = PerfProbeRunTiming.Complete(parent);
            Check(!result.NativeCompleted && !Monitor.IsEntered(RunFixture.threadLock), "Exceptional transformed path leaked lock");
            RunFixture.Fail = false;
            RunFixture.NativeAction = () =>
            {
                RunFixture.NativeAction = null;
                var nestedParent = PerfProbeRunTiming.Begin(true);
                Check(run(true) == 37, "Nested native result changed");
                var child = PerfProbeRunTiming.Complete(nestedParent);
                Check(child.Acquired > 0 && child.Released >= child.Acquired && child.NativeCompleted,
                    "Reentrant run lost its own lock/native phases");
            };
            parent = PerfProbeRunTiming.Begin(false); Check(run(false) == 37, "Reentrant outer return changed");
            result = PerfProbeRunTiming.Complete(parent);
            Check(!result.Skip && result.NativeCompleted && result.Released >= result.Acquired && !Monitor.IsEntered(RunFixture.threadLock),
                "Reentrant run corrupted parent phases or leaked its gate");
            for (int stage = 1; stage <= 3; stage++)
            {
                RunFixture.FailStage = stage; parent = PerfProbeRunTiming.Begin(false);
                try { run(false); Check(false, "Missing buffer/copy/publication exception"); }
                catch (ApplicationException error) { Check(ReferenceEquals(error, RunFixture.Error), "Boundary exception was replaced"); }
                result = PerfProbeRunTiming.Complete(parent);
                Check(!Monitor.IsEntered(RunFixture.threadLock) && (stage == 1 ? !result.NativeEntered : result.NativeCompleted && result.Released > 0),
                    "Exceptional buffer/copy/publication path lost phase semantics or leaked its gate");
            }
            RunFixture.FailStage = 0;
        }

        private static void TestTransfer()
        {
            var job = new PerfProbeAnalysisJob(false, 8, 2);
            Exception failure = null;
            var consumer = new Thread(() =>
            {
                try
                {
                    for (int i = 1; i <= 10000; i++)
                    {
                        while (!job.TryRead()) Thread.Yield();
                        Check(job.Sequence == i && job.Main.Count == 1 && job.Main.Frames[0] == (ulong)i, "Torn/reordered analysis batch");
                        Check(!job.TryClaim(), "Producer acquired a consumer-owned batch");
                        job.Release();
                    }
                }
                catch (Exception error) { failure = error; }
            });
            consumer.Start();
            for (int i = 1; i <= 10000; i++)
            {
                while (!job.TryClaim()) { if (failure != null) throw failure; Thread.Yield(); }
                job.Main.Reset(); job.Sequence = i; job.Main.Add(new[] { (ulong)i }, 0, 1, i);
                Check(!job.TryRead(), "Consumer saw an unpublished batch"); job.Publish();
            }
            Check(consumer.Join(10000), "Analysis handoff fixture stalled"); if (failure != null) throw failure;
            var batch = new PerfProbeSampleBatch(1, 2); var history = new PerfProbeSampleHistory(3, 2);
            history.Add(1, new ulong[] { 1, 2 }, 0, 2); history.Add(2, new ulong[] { 3, 4 }, 0, 2);
            history.CopyTo(batch, 0, 2);
            Check(batch.Count == 1 && batch.Dropped == 1 && batch.Frames[0] == 1, "Bounded batch overflow not reported");
        }

        private static void TestConcurrentLockHistory()
        {
            var history = new PerfProbeLockHistory(32);
            int done = 0; Exception failure = null;
            var reader = new Thread(() =>
            {
                try
                {
                    do
                    {
                        history.Visit(0, long.MaxValue, value => Check(value.Acquired == value.Started + 1 &&
                            value.Ended == value.Started + 2 && value.Thread == value.Started,
                            "Concurrent history reader accepted a torn owner span"), out _, out _);
                    } while (Volatile.Read(ref done) == 0);
                }
                catch (Exception error) { failure = error; }
            });
            reader.Start();
            for (int i = 1; i <= 20000; i++)
                history.Add(new PerfProbeLockHistory.Span { Started = i, Acquired = i + 1, Ended = i + 2, Thread = i });
            Volatile.Write(ref done, 1);
            Check(reader.Join(10000), "Concurrent owner-history reader stalled");
            if (failure != null) throw failure;
        }

        public sealed class MemoryBuffers
        {
            public object getFreeBuffer(bool fill) { if (RunFixture.FailStage == 1) throw RunFixture.Error; return RunFixture.BufferAvailable ? this : null; }
            public void returnBuffer(object buffer) { if (RunFixture.FailStage == 3) throw RunFixture.Error; }
        }
        public sealed class EditorDirector { public void preDLLCallActions(ref int a, ref int b) { } }
        public static class RunFixture
        {
            internal static readonly object threadLock = new object();
            public static bool BufferAvailable, LastSkip, Fail;
            public static Action NativeAction;
            public static int NativeCalls, FailStage;
            public static readonly ApplicationException Error = new ApplicationException("original-native-failure");
            private static readonly MemoryBuffers buffers = new MemoryBuffers();
            private static readonly EditorDirector editor = new EditorDirector();
            public static int DLL_RunTick(bool skip) { NativeCalls++; LastSkip = skip; NativeAction?.Invoke(); if (Fail) throw Error; return 37; }
            public static object CopyPlayStateStruct() { if (FailStage == 2) throw Error; return null; }
            [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
            public static int run(bool skip)
            {
                object buffer = buffers.getFreeBuffer(true); if (buffer == null) return 0;
                lock (threadLock)
                {
                    int a = 0, b = 0; editor.preDLLCallActions(ref a, ref b);
                    int value = DLL_RunTick(skip); CopyPlayStateStruct(); buffers.returnBuffer(buffer); return value;
                }
            }
        }

        private static MethodInfo FixtureMethod(string name) => typeof(DiagnosticHelperTests).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);
        private static List<CodeInstruction> MakeScope() => new List<CodeInstruction>
        {
            new CodeInstruction(OpCodes.Ldsfld, typeof(DiagnosticHelperTests).GetField("threadLock", BindingFlags.NonPublic | BindingFlags.Static)),
            new CodeInstruction(OpCodes.Call, typeof(Monitor).GetMethod(nameof(Monitor.Enter), new[] { typeof(object), typeof(bool).MakeByRefType() })),
            new CodeInstruction(OpCodes.Call, typeof(Monitor).GetMethod(nameof(Monitor.Exit), new[] { typeof(object) }))
        };
    }
}
