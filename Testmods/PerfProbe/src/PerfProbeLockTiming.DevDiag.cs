using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Threading;
using HarmonyLib;

namespace PerfProbe
{
    // Only the three audited wrappers are rewritten. Monitor's original overload, ref flag,
    // exception/finally edges and reentrancy remain unchanged; no extra lock acquisition.
    internal static class PerfProbeLockTiming
    {
        [ThreadStatic] private static long waitTicks, heldTicks, heldSince, acquisitions;
        [ThreadStatic] private static int depth;
        [ThreadStatic] private static long rootStarted;
        [ThreadStatic] private static int rootKind;

        internal struct Snapshot
        {
            internal long Wait, Held, Acquisitions;
        }

        internal static Snapshot Capture() => new Snapshot
        {
            Wait = waitTicks,
            Held = heldTicks + (depth == 0 ? 0 : Stopwatch.GetTimestamp() - heldSince),
            Acquisitions = acquisitions
        };

        internal static void Enter(object gate, ref bool taken)
            => EnterCore(gate, ref taken, 0);
        internal static void EnterRun(object gate, ref bool taken) => EnterCore(gate, ref taken, 1);
        internal static void EnterMerit(object gate, ref bool taken) => EnterCore(gate, ref taken, 2);
        internal static void EnterAchievement(object gate, ref bool taken) => EnterCore(gate, ref taken, 3);

        private static void EnterCore(object gate, ref bool taken, int kind)
        {
            long started = Stopwatch.GetTimestamp();
            if (kind == 1) PerfProbeRunTiming.Waiting(started);
            // No TryEnter probe: even an uncontended probe could change lock scheduling.
            Monitor.Enter(gate, ref taken);
            long acquired = Stopwatch.GetTimestamp();
            if (kind == 1) PerfProbeRunTiming.Acquired(acquired);
            waitTicks += acquired - started;
            acquisitions++;
            if (depth++ == 0)
            {
                heldSince = acquired; rootStarted = started; rootKind = kind;
            }
        }

        internal static void Exit(object gate) => ExitCore(gate, false);
        internal static void ExitRun(object gate) => ExitCore(gate, true);
        private static void ExitCore(object gate, bool run)
        {
            // Release first, exactly as Vanilla does. Only successful exits update counters.
            long ended = Stopwatch.GetTimestamp();
            Monitor.Exit(gate);
            if (run) PerfProbeRunTiming.Released(ended, Stopwatch.GetTimestamp());
            if (--depth == 0)
            {
                heldTicks += ended - heldSince;
                PerfProbeLockOwners.Record(rootStarted, heldSince, ended, rootKind,
                    rootKind == 1 && PerfProbeRunTiming.NativeEntered);
            }
        }

        internal static IEnumerable<CodeInstruction> Transpile(
            IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var code = new List<CodeInstruction>(instructions);
            MethodInfo enter = typeof(Monitor).GetMethod(nameof(Monitor.Enter), new[] { typeof(object), typeof(bool).MakeByRefType() });
            MethodInfo exit = typeof(Monitor).GetMethod(nameof(Monitor.Exit), new[] { typeof(object) });
            FieldInfo gate = __originalMethod.DeclaringType.GetField("threadLock", BindingFlags.NonPublic | BindingFlags.Static);
            int enters = 0, exits = 0, gates = 0;
            foreach (CodeInstruction instruction in code)
            {
                if (instruction.Calls(enter)) enters++;
                if (instruction.Calls(exit)) exits++;
                if (instruction.opcode == OpCodes.Ldsfld && Equals(instruction.operand, gate)) gates++;
            }
            if ((__originalMethod.Name != "GetMeritData" && __originalMethod.Name != "SetAchData" && __originalMethod.Name != "run") ||
                gate == null || gate.FieldType != typeof(object) || gates != 1 || enters != 1 || exits != 1)
                throw new InvalidOperationException("Audited single threadLock scope differs: " + __originalMethod.Name);
            // Replacing operands in place preserves labels and exception block markers.
            foreach (CodeInstruction instruction in code)
            {
                if (instruction.Calls(enter)) instruction.operand = typeof(PerfProbeLockTiming).GetMethod(
                    __originalMethod.Name == "run" ? nameof(EnterRun) : __originalMethod.Name == "GetMeritData" ? nameof(EnterMerit) : nameof(EnterAchievement), BindingFlags.Static | BindingFlags.NonPublic);
                else if (instruction.Calls(exit)) instruction.operand = typeof(PerfProbeLockTiming).GetMethod(
                    __originalMethod.Name == "run" ? nameof(ExitRun) : nameof(Exit), BindingFlags.Static | BindingFlags.NonPublic);
            }
            return code;
        }

        private static volatile bool meritInstrumented, achievementInstrumented;
        internal static bool IsInstrumented(string name) => name == "EngineInterface.GetMeritData" ? meritInstrumented :
            name == "EngineInterface.SetAchData" && achievementInstrumented;
        internal static IEnumerable<CodeInstruction> SafeTranspile(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var original = new List<CodeInstruction>(instructions);
            bool measured = false;
            try
            {
                var copy = new List<CodeInstruction>();
                foreach (var instruction in original) copy.Add(new CodeInstruction(instruction));
                var result = Transpile(copy, __originalMethod);
                measured = true;
                return result;
            }
            catch (Exception exception)
            {
                Shared.DevDiag.Log(() => "PERFPROBE_LOCK_TIMING unavailable method=" + __originalMethod.Name + " reason=" + exception.Message);
                return original;
            }
            finally
            {
                if (__originalMethod.Name == "GetMeritData") meritInstrumented = measured;
                else if (__originalMethod.Name == "SetAchData") achievementInstrumented = measured;
            }
        }

        // Windows reports kernel/user CPU in 100 ns units, independent of wall-clock waits.
        internal static long ThreadCpuTime()
        {
            try { return GetThreadTimes(GetCurrentThread(), out _, out _, out long kernel, out long user) ? kernel + user : -1; }
            catch (Exception) { return -1; }
        }

        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentThread();
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetThreadTimes(IntPtr thread, out long created, out long exited, out long kernel, out long user);
    }
}
