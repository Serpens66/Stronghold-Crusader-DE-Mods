using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using BepInEx.Logging;
using HarmonyLib;

namespace BugfixesAndQoL.VanillaPerformance
{
    internal static partial class EnginePresentationBridge
    {
        static partial void DiagInstallFreeze(ManualLogSource logger) => GateAttackFreezeRun.Install(logger);
    }

    // Managed probes only. Arguments/results, Monitor overload and finally are preserved.
    internal static class GateAttackFreezeRun
    {
        private static readonly Harmony harmony = new Harmony("BugfixesAndQoL_Serp.GateFreeze");
        internal static bool Instrumented;
        internal static void Install(ManualLogSource logger)
        {
            Shared.DevDiag.Attach(logger); GateAttackFreezeCapture.Initialize();
            try
            {
                var target = typeof(EngineInterface).GetMethod(nameof(EngineInterface.run), new[] { typeof(bool) });
                harmony.Patch(target,
                    prefix: new HarmonyMethod(typeof(GateAttackFreezeRun), nameof(Before)),
                    transpiler: new HarmonyMethod(typeof(GateAttackFreezeRun), nameof(Transpile)) { priority = Priority.Last },
                    finalizer: new HarmonyMethod(typeof(GateAttackFreezeRun), nameof(After)));
                Shared.DevDiag.Log(() => "GATE_FREEZE runPhasesInstalled=" + Instrumented + " additionalNativeCalls=0");
            }
            catch (Exception ex) { Shared.DevDiag.Log(() => "GATE_FREEZE runPhasesUnavailable=" + ex.GetType().Name + ":" + ex.Message); }
        }
        private static void Before(bool __0, out GateAttackFreezeCapture.Cookie __state) => __state = GateAttackFreezeCapture.Begin(__0);
        private static Exception After(GateAttackFreezeCapture.Cookie __state, Exception __exception)
        { GateAttackFreezeCapture.End(__state, __exception == null); return __exception; }
        internal static void Enter(object gate, ref bool taken)
        {
            var run = GateAttackFreezeCapture.Current;
            if (run != null) { run.LockStart = Stopwatch.GetTimestamp(); run.LockObserved = true; }
            Monitor.Enter(gate, ref taken);
            if (run != null) run.Acquired = Stopwatch.GetTimestamp();
        }
        internal static void Exit(object gate)
        {
            long stamp = Stopwatch.GetTimestamp();
            Monitor.Exit(gate);
            var run = GateAttackFreezeCapture.Current;
            if (run != null) run.Released = stamp;
        }
        private static void BeforeNative()
        {
            var run = GateAttackFreezeCapture.Current; if (run == null) return;
            GateAttackFreezeCapture.Counters(true);
            run.NativeStart = Stopwatch.GetTimestamp(); run.NativeEntered = true;
        }
        private static void AfterNative()
        {
            var run = GateAttackFreezeCapture.Current; if (run == null) return;
            run.NativeEnd = Stopwatch.GetTimestamp(); run.NativeCompleted = true;
            GateAttackFreezeCapture.Counters(false);
        }
        internal static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var original = new List<CodeInstruction>(instructions);
            try
            {
                if (__originalMethod.DeclaringType != typeof(EngineInterface) || __originalMethod.Name != "run" ||
                    ((MethodInfo)__originalMethod).ReturnType != typeof(int) || __originalMethod.GetParameters().Length != 1 ||
                    __originalMethod.GetParameters()[0].ParameterType != typeof(bool)) throw new InvalidOperationException("run signature differs");
                var code = original.ConvertAll(i => new CodeInstruction(i));
                int enter = -1, exit = -1, native = -1, buffer = -1, publish = -1, gates = 0;
                var gate = typeof(EngineInterface).GetField("threadLock", BindingFlags.NonPublic | BindingFlags.Static);
                var monitorEnter = typeof(Monitor).GetMethod(nameof(Monitor.Enter), new[] { typeof(object), typeof(bool).MakeByRefType() });
                var monitorExit = typeof(Monitor).GetMethod(nameof(Monitor.Exit), new[] { typeof(object) });
                for (int i = 0; i < code.Count; i++)
                {
                    if (code[i].opcode == OpCodes.Ldsfld && Equals(code[i].operand, gate)) gates++;
                    if (!(code[i].operand is MethodInfo m) || (code[i].opcode != OpCodes.Call && code[i].opcode != OpCodes.Callvirt)) continue;
                    void Unique(ref int slot) { if (slot >= 0) throw new InvalidOperationException("Repeated run phase " + m.Name); slot = i; }
                    if (m == monitorEnter) Unique(ref enter);
                    else if (m == monitorExit) Unique(ref exit);
                    else if (m.DeclaringType == typeof(EngineInterface) && m.Name == nameof(EngineInterface.DLL_RunTick) && m.ReturnType == typeof(int)) Unique(ref native);
                    else if (m.DeclaringType == typeof(MemoryBuffers) && m.Name == "getFreeBuffer") Unique(ref buffer);
                    else if (m.DeclaringType == typeof(MemoryBuffers) && m.Name == "returnBuffer") Unique(ref publish);
                }
                if (gate == null || gate.FieldType != typeof(object) || gates != 1 ||
                    !(buffer >= 0 && buffer < enter && enter < native && native < publish && publish < exit) ||
                    __originalMethod.GetMethodBody().ExceptionHandlingClauses.Count != 1 ||
                    __originalMethod.GetMethodBody().ExceptionHandlingClauses[0].Flags != ExceptionHandlingClauseOptions.Finally)
                    throw new InvalidOperationException("run buffer/lock/native/publication/finally differs");
                code[enter].operand = Method(nameof(Enter)); code[exit].operand = Method(nameof(Exit));
                var result = new List<CodeInstruction>(code.Count + 2);
                for (int i = 0; i < code.Count; i++)
                {
                    if (i == native)
                    {
                        var probe = new CodeInstruction(OpCodes.Call, Method(nameof(BeforeNative)));
                        probe.labels.AddRange(code[i].labels); code[i].labels.Clear();
                        probe.blocks.AddRange(code[i].blocks); code[i].blocks.Clear(); result.Add(probe);
                    }
                    result.Add(code[i]);
                    if (i == native) result.Add(new CodeInstruction(OpCodes.Call, Method(nameof(AfterNative))));
                }
                Instrumented = true; return result;
            }
            catch (Exception)
            { Instrumented = false; GateAttackFreezeCapture.Fault(); return original; }
        }
        private static MethodInfo Method(string name) => typeof(GateAttackFreezeRun).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic);
    }
}
