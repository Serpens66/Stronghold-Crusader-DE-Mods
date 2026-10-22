using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;

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
            return "PASS: production Monitor contention/reentrancy/finally/exception contracts; IL labels and fail-closed validation; bounded raw sample history and exact event filtering; CPU clock.";
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
