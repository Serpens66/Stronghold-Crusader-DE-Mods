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
    // Only the two audited wrappers are rewritten. Monitor's original overload, ref flag,
    // exception/finally edges and reentrancy remain unchanged; no extra lock acquisition.
    internal static class PerfProbeLockTiming
    {
        [ThreadStatic] private static long waitTicks, heldTicks, heldSince, acquisitions;
        [ThreadStatic] private static int depth;

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
        {
            long started = Stopwatch.GetTimestamp();
            // No TryEnter probe: even an uncontended probe could change lock scheduling.
            Monitor.Enter(gate, ref taken);
            long acquired = Stopwatch.GetTimestamp();
            waitTicks += acquired - started;
            acquisitions++;
            if (depth++ == 0) heldSince = acquired;
        }

        internal static void Exit(object gate)
        {
            // Release first, exactly as Vanilla does. Only successful exits update counters.
            long ended = Stopwatch.GetTimestamp();
            Monitor.Exit(gate);
            if (--depth == 0) heldTicks += ended - heldSince;
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
            if ((__originalMethod.Name != "GetMeritData" && __originalMethod.Name != "SetAchData") ||
                gate == null || gate.FieldType != typeof(object) || gates != 1 || enters != 1 || exits != 1)
                throw new InvalidOperationException("Audited single threadLock scope differs: " + __originalMethod.Name);
            // Replacing operands in place preserves labels and exception block markers.
            foreach (CodeInstruction instruction in code)
            {
                if (instruction.Calls(enter)) instruction.operand = typeof(PerfProbeLockTiming).GetMethod(nameof(Enter), BindingFlags.Static | BindingFlags.NonPublic);
                else if (instruction.Calls(exit)) instruction.operand = typeof(PerfProbeLockTiming).GetMethod(nameof(Exit), BindingFlags.Static | BindingFlags.NonPublic);
            }
            return code;
        }

        // Windows reports kernel/user CPU in 100 ns units, independent of wall-clock waits.
        internal static long ThreadCpuTime()
        {
            return GetThreadTimes(GetCurrentThread(), out _, out _, out long kernel, out long user) ? kernel + user : -1;
        }

        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentThread();
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetThreadTimes(IntPtr thread, out long created, out long exited, out long kernel, out long user);
    }
}
