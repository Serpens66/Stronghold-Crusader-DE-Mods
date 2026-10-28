using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;

namespace PerfProbe
{
    // All clocks are monotonic. A run call is NOT a game tick: buffer exhaustion returns
    // before the lock, and even DLL_RunTick(skip) need not advance simulation time.
    internal static class PerfProbeRunTiming
    {
        internal struct State
        {
            internal long Started, BufferStarted, BufferTicks, LockStarted, Acquired, Released, ReleaseEnded, NativeStarted, NativeEnded, CpuStarted, NativeCpu;
            internal bool Active, BufferObserved, NoBuffer, NativeEntered, NativeCompleted, Skip;
        }
        [ThreadStatic] private static State current;
        // SkipCalls: run(true) from Vanilla's low-FPS compensation or multiplayer frame skipping.
        internal static long CallsStarted, CallsCompleted, BuffersUnavailable, NativeCallsStarted, NativeCallsCompleted, SkipCalls;
        internal static volatile bool Instrumented;

        internal static State Begin(bool skip)
        {
            State parent = current;
            current = new State { Started = Stopwatch.GetTimestamp(), Active = true, Skip = skip, NativeCpu = -1 };
            Interlocked.Increment(ref CallsStarted);
            if (skip) Interlocked.Increment(ref SkipCalls);
            return parent;
        }
        internal static State Complete(State parent)
        {
            State result = current;
            current = parent;
            Interlocked.Increment(ref CallsCompleted);
            if (result.BufferObserved && result.NoBuffer) Interlocked.Increment(ref BuffersUnavailable);
            return result;
        }
        internal static void BeforeBuffer() { if (current.Active) current.BufferStarted = Stopwatch.GetTimestamp(); }
        internal static void BufferResult(object buffer)
        {
            if (!current.Active) return;
            current.BufferTicks = Stopwatch.GetTimestamp() - current.BufferStarted;
            current.BufferObserved = true; current.NoBuffer = buffer == null;
        }
        internal static void Acquired(long stamp) { if (current.Active) current.Acquired = stamp; }
        internal static void Waiting(long stamp) { if (current.Active) current.LockStarted = stamp; }
        internal static void Released(long stamp) => Released(stamp, stamp);
        internal static void Released(long stamp, long ended) { if (current.Active) { current.Released = stamp; current.ReleaseEnded = ended; } }
        internal static bool NativeEntered => current.NativeEntered;
        internal static void BeforeNative()
        {
            if (!current.Active) return;
            current.NativeStarted = Stopwatch.GetTimestamp();
            current.NativeEntered = true;
            // Never called on the no-buffer path. Native time includes SE/mod callbacks.
            current.CpuStarted = PerfProbeLockTiming.ThreadCpuTime();
            Interlocked.Increment(ref NativeCallsStarted);
        }
        internal static void AfterNative()
        {
            if (!current.Active) return;
            current.NativeEnded = Stopwatch.GetTimestamp();
            long cpu = PerfProbeLockTiming.ThreadCpuTime();
            current.NativeCpu = cpu < 0 || current.CpuStarted < 0 ? -1 : cpu - current.CpuStarted;
            current.NativeCompleted = true;
            Interlocked.Increment(ref NativeCallsCompleted);
        }
        internal static string Detail(State value)
        {
            double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;
            return string.Format(CultureInfo.InvariantCulture,
                "runPhasesMeasured={0} bufferObserved={1} noBuffer={2} skipArgument={3} bufferMs={4:F3} " +
                "lockPreNativeMs={5:F3} nativeInclusiveMs={6:F3} nativeEntered={7} nativeCompleted={8} " +
                "postNativeCopyPublishMs={9:F3} nativeThreadCpuMs={10:F3}",
                Instrumented, value.BufferObserved, value.NoBuffer, value.Skip,
                value.BufferObserved ? Ms(value.BufferTicks) : -1,
                value.Acquired != 0 && value.NativeStarted != 0 ? Ms(value.NativeStarted - value.Acquired) : -1,
                value.NativeCompleted ? Ms(value.NativeEnded - value.NativeStarted) : -1,
                value.NativeEntered, value.NativeCompleted,
                value.NativeCompleted && value.Released != 0 ? Ms(value.Released - value.NativeEnded) : -1,
                value.NativeCpu < 0 ? -1 : value.NativeCpu / 10000.0) +
                // Time before the original body: other mods' prefixes (PerfProbe's prefix runs first).
                " preBodyMs=" + (value.BufferObserved ? Ms(value.BufferStarted - value.Started) : -1).ToString("F3", CultureInfo.InvariantCulture) +
                " lockWaitMs=" + (value.LockStarted != 0 && value.Acquired != 0 ? Ms(value.Acquired - value.LockStarted) : -1).ToString("F3", CultureInfo.InvariantCulture) +
                " lockReleaseMs=" + (value.ReleaseEnded != 0 ? Ms(value.ReleaseEnded - value.Released) : -1).ToString("F3", CultureInfo.InvariantCulture);
        }

        internal static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var code = new List<CodeInstruction>(instructions);
            int buffer = -1, native = -1, enter = -1, exit = -1, before = -1, copy = -1, publish = -1;
            for (int i = 0; i < code.Count; i++)
            {
                if (!(code[i].operand is MethodInfo method) || (code[i].opcode != OpCodes.Call && code[i].opcode != OpCodes.Callvirt)) continue;
                void Unique(ref int slot) { if (slot >= 0) throw new InvalidOperationException("Ambiguous run anchor: " + method.Name); slot = i; }
                if (method.DeclaringType?.Name == "MemoryBuffers" && method.Name == "getFreeBuffer" &&
                    method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(bool) && !method.ReturnType.IsValueType) Unique(ref buffer);
                if (method.DeclaringType == __originalMethod.DeclaringType && method.Name == "DLL_RunTick" && method.ReturnType == typeof(int)) Unique(ref native);
                if (method.DeclaringType == typeof(Monitor) && method.Name == "Enter") Unique(ref enter);
                if (method.DeclaringType == typeof(Monitor) && method.Name == "Exit") Unique(ref exit);
                if (method.DeclaringType?.Name == "EditorDirector" && method.Name == "preDLLCallActions") Unique(ref before);
                if (method.DeclaringType == __originalMethod.DeclaringType && method.Name == "CopyPlayStateStruct") Unique(ref copy);
                if (method.DeclaringType?.Name == "MemoryBuffers" && method.Name == "returnBuffer") Unique(ref publish);
            }
            if (__originalMethod.Name != "run" || !(buffer >= 0 && buffer < enter && enter < before && before < native && native < copy && copy < publish && publish < exit))
                throw new InvalidOperationException("Audited run buffer/lock/native/copy/publication chain differs.");
            // First validate EVERYTHING, then replace Monitor operands and add stack-neutral
            // probes. Call arguments/results remain on the evaluation stack untouched.
            code = new List<CodeInstruction>(PerfProbeLockTiming.Transpile(code, __originalMethod));
            var result = new List<CodeInstruction>(code.Count + 7);
            for (int i = 0; i < code.Count; i++)
            {
                if (i == buffer || i == native)
                {
                    var probe = Call(i == buffer ? nameof(BeforeBuffer) : nameof(BeforeNative));
                    probe.labels.AddRange(code[i].labels); code[i].labels.Clear();
                    probe.blocks.AddRange(code[i].blocks); code[i].blocks.Clear();
                    result.Add(probe);
                }
                result.Add(code[i]);
                if (i == buffer) { result.Add(new CodeInstruction(OpCodes.Dup)); result.Add(Call(nameof(BufferResult))); }
                if (i == native) result.Add(Call(nameof(AfterNative)));
            }
            return result;
        }
        internal static IEnumerable<CodeInstruction> SafeTranspile(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var original = new List<CodeInstruction>(instructions);
            try
            {
                var copy = new List<CodeInstruction>();
                foreach (var instruction in original) copy.Add(new CodeInstruction(instruction));
                var transformed = Transpile(copy, __originalMethod);
                Instrumented = true;
                return transformed;
            }
            catch (Exception exception)
            {
                Instrumented = false;
                Shared.DevDiag.Log(() => "PERFPROBE_RUN_PHASES unavailable: " + exception.Message);
                return original;
            }
        }
        private static CodeInstruction Call(string name) => new CodeInstruction(OpCodes.Call,
            typeof(PerfProbeRunTiming).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic));
    }
}
