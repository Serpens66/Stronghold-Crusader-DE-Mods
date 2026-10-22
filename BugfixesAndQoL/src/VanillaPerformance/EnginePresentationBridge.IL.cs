using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace BugfixesAndQoL.VanillaPerformance
{
    internal static partial class EnginePresentationBridge
    {
        // Inspect real managed IL, not a publicized compile view. Runtime copies
        // are independently validated because other transpilers may have run.
        private static void ValidateOriginal(MethodInfo method)
        {
            if (method == null || !method.IsPublic || !method.IsStatic || method.GetMethodBody() == null)
                throw new InvalidOperationException("Managed engine bridge signature unavailable");
            var original = PatchProcessor.GetOriginalInstructions(method);
            Validate(new List<CodeInstruction>(original), method);
        }
        private static int Unique(List<CodeInstruction> code, string type, string name)
        {
            int found = -1;
            for (int i = 0; i < code.Count; i++)
                if ((code[i].opcode == OpCodes.Call || code[i].opcode == OpCodes.Callvirt) &&
                    code[i].operand is MethodInfo call && call.DeclaringType == AnchorType(type) && call.Name == name)
                { if (found >= 0) throw new InvalidOperationException("Repeated IL anchor " + name); found = i; }
            if (found < 0) throw new InvalidOperationException("Missing IL anchor " + name);
            return found;
        }
        private static Type AnchorType(string name) => name == "Monitor" ? typeof(System.Threading.Monitor) :
            name == "MemoryBuffers" ? typeof(MemoryBuffers) : typeof(EngineInterface);
        internal static void ValidateLockedCall(MethodInfo method, string nativeName)
        {
            if (method == null || !method.IsPublic || !method.IsStatic || method.GetMethodBody() == null)
                throw new InvalidOperationException("Engine accessor body unavailable");
            var code = new List<CodeInstruction>(PatchProcessor.GetOriginalInstructions(method));
            ValidateLock(code, method, out int enter, out int exit);
            int native = Unique(code, "EngineInterface", nativeName);
            if (!(enter < native && native < exit)) throw new InvalidOperationException("Engine accessor lock scope differs");
        }
        private static void Validate(List<CodeInstruction> code, MethodBase method)
        {
            ValidateLock(code, method, out int enter, out int exit);
            ValidatePhases(code, method, enter, exit);
        }
        private static void ValidateLock(List<CodeInstruction> code, MethodBase method, out int enter, out int exit)
        {
            enter = Unique(code, "Monitor", "Enter"); exit = Unique(code, "Monitor", "Exit");
            var monitor = (MethodInfo)code[enter].operand;
            var parameters = monitor.GetParameters();
            if (parameters.Length != 2 || parameters[0].ParameterType != typeof(object) ||
                parameters[1].ParameterType != typeof(bool).MakeByRefType() || enter >= exit ||
                method.GetMethodBody().ExceptionHandlingClauses.Count != 1 ||
                method.GetMethodBody().ExceptionHandlingClauses[0].Flags != ExceptionHandlingClauseOptions.Finally)
                throw new InvalidOperationException("Engine Monitor/finally contract differs");
            var gate = typeof(EngineInterface).GetField("threadLock", BindingFlags.Static | BindingFlags.NonPublic);
            if (code.Take(enter).Count(i => i.opcode == OpCodes.Ldsfld && Equals(i.operand, gate)) != 1 ||
                ((MethodInfo)code[exit].operand).GetParameters().Length != 1)
                throw new InvalidOperationException("Engine lock identity differs");
        }
        private static void ValidatePhases(List<CodeInstruction> code, MethodBase method, int enter, int exit)
        {
            if (method.Name == "run")
            {
                if (((MethodInfo)method).ReturnType != typeof(int) || method.GetParameters().Length != 1 ||
                    method.GetParameters()[0].ParameterType != typeof(bool)) throw new InvalidOperationException("run signature");
                int buffer = Unique(code, "MemoryBuffers", "getFreeBuffer");
                int native = Unique(code, "EngineInterface", "DLL_RunTick");
                int publish = Unique(code, "MemoryBuffers", "returnBuffer");
                if (!(buffer < enter && enter < native && native < publish && publish < exit))
                    throw new InvalidOperationException("run phase order differs");
            }
            else if (method.Name != "SaveSaveGame" || ((MethodInfo)method).ReturnType != typeof(bool) ||
                !method.GetParameters().Select(p => p.ParameterType).SequenceEqual(new[] {typeof(string), typeof(int),
                    typeof(int), typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(bool)}) ||
                Unique(code, "EngineInterface", "DLL_SaveSaveGame") <= enter ||
                Unique(code, "EngineInterface", "DLL_SaveSaveGame") >= exit)
                throw new InvalidOperationException("save phase order differs");
        }
        internal static IEnumerable<CodeInstruction> Transpile(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var original = new List<CodeInstruction>(instructions);
            try
            {
                var code = original.Select(i => new CodeInstruction(i)).ToList();
                Validate(code, __originalMethod);
                bool run = __originalMethod.Name == "run";
                int entered = Unique(code, "Monitor", "Enter");
                int publish = run ? Unique(code, "MemoryBuffers", "returnBuffer") : -1;
                int save = run ? -1 : Unique(code, "EngineInterface", "DLL_SaveSaveGame");
                var result = new List<CodeInstruction>();
                for (int i = 0; i < code.Count; i++)
                {
                    if (i == publish || i == save)
                    {
                        var callback = Call(run ? nameof(BeforePublish) : nameof(BeforeSave));
                        // Incoming branches and try boundaries must execute the new
                        // callback too. Stack arguments below a void call are retained.
                        callback.labels.AddRange(code[i].labels); code[i].labels.Clear();
                        callback.blocks.AddRange(code[i].blocks); code[i].blocks.Clear(); result.Add(callback);
                    }
                    result.Add(code[i]);
                    if (run && i == entered) result.Add(Call(nameof(EnteredRun)));
                }
                if (run) runValid = true; else saveValid = true;
                return result;
            }
            catch (Exception error)
            {
                if (__originalMethod.Name == "run") runValid = false; else saveValid = false;
                Report(error); return original;
            }
        }
        private static CodeInstruction Call(string name) => new CodeInstruction(OpCodes.Call,
            typeof(EnginePresentationBridge).GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic));
    }
}
