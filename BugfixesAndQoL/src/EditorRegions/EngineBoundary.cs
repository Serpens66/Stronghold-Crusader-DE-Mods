using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;

namespace BugfixesAndQoL.EditorRegions
{
    internal static class EngineBoundary
    {
        internal static MethodInfo ValidateManagedBoundary(out FieldInfo gate)
        {
            gate = typeof(EngineInterface).GetField("threadLock", BindingFlags.Static | BindingFlags.NonPublic);
            var before = typeof(EditorDirector).GetMethod("preDLLCallActions", new[] {
                typeof(int).MakeByRefType(), typeof(int).MakeByRefType() });
            var run = typeof(EngineInterface).GetMethod("run", new[] { typeof(bool) });
            if (gate == null || gate.FieldType != typeof(object) || before == null ||
                !before.IsPublic || before.IsStatic || before.ReturnType != typeof(void) ||
                run == null || !run.IsPublic || !run.IsStatic || run.GetMethodBody() == null)
                throw new InvalidOperationException("Editor engine boundary signature differs.");
            var code = new List<CodeInstruction>(PatchProcessor.GetOriginalInstructions(run));
            int enter = -1, exit = -1, preparation = -1, native = -1, gateLoads = 0;
            foreach (var instruction in code)
                if (instruction.opcode == OpCodes.Ldsfld && Equals(instruction.operand, gate)) gateLoads++;
            for (int i = 0; i < code.Count; i++)
            {
                if (code[i].opcode != OpCodes.Call && code[i].opcode != OpCodes.Callvirt) continue;
                if (!(code[i].operand is MethodInfo call)) continue;
                if (call == before) Unique(ref preparation, i);
                if (call.DeclaringType == typeof(EngineInterface) && call.Name == "DLL_RunTick") Unique(ref native, i);
                if (call.DeclaringType == typeof(Monitor) && call.Name == "Enter") Unique(ref enter, i);
                if (call.DeclaringType == typeof(Monitor) && call.Name == "Exit") Unique(ref exit, i);
            }
            var clauses = run.GetMethodBody().ExceptionHandlingClauses;
            if (gateLoads != 1 || !(0 <= enter && enter < preparation && preparation < native && native < exit) ||
                clauses.Count != 1 || clauses[0].Flags != ExceptionHandlingClauseOptions.Finally)
                throw new InvalidOperationException("Editor preparation is not uniquely inside the run lock before DLL_RunTick.");
            return before;
        }

        private static void Unique(ref int slot, int index)
        {
            if (slot >= 0) throw new InvalidOperationException("Repeated editor engine boundary anchor.");
            slot = index;
        }
    }
}
