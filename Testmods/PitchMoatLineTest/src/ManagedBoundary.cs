using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using BepInEx.Logging;
using HarmonyLib;

namespace PitchMoatLineTest
{
    internal static class ManagedBoundary
    {
        private static readonly Harmony Harmony = new Harmony(Plugin.Guid + ".Placement");
        private static ManualLogSource log;
        private static volatile bool inputReady, mapperReady, engineReady;

        internal static object EngineGate { get; private set; }
        internal static bool IsReady => inputReady && mapperReady && engineReady;

        internal static void Install(ManualLogSource logger)
        {
            log = logger;
            var update = typeof(EditorDirector).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
            var mapper = typeof(EditorDirector).GetMethod(nameof(EditorDirector.preDLLCallActions),
                new[] { typeof(int).MakeByRefType(), typeof(int).MakeByRefType() });
            var run = typeof(EngineInterface).GetMethod(nameof(EngineInterface.run), new[] { typeof(bool) });
            var gate = typeof(EngineInterface).GetField("threadLock", BindingFlags.Static | BindingFlags.NonPublic);
            if (gate?.FieldType != typeof(object) || gate.GetValue(null) is not object engineGate)
                throw new InvalidOperationException("Missing EngineInterface monitor.");
            var clauses = run?.GetMethodBody()?.ExceptionHandlingClauses;
            if (update == null || mapper == null || clauses?.Count != 1 ||
                clauses[0].Flags != ExceptionHandlingClauseOptions.Finally)
                throw new InvalidOperationException("Missing original placement boundary or engine monitor scope.");
            EngineGate = engineGate;

            // Harmony composes these with the installed prefixes, postfixes and
            // transpilers before emitting the shared wrapper. Hooking the emitted
            // wrapper directly changes the IL shape and can fail on a repatch.
            Patch(update, nameof(TransformInput));
            Patch(mapper, nameof(TransformMapper));
            Patch(run, nameof(TransformEngine));
            if (!IsReady)
                throw new InvalidOperationException("The managed placement boundaries did not all validate.");
            log.LogInfo(Plugin.Stamp("Verified mapper input and locked engine boundaries."));
        }

        private static void Patch(MethodInfo target, string method) =>
            Harmony.Patch(target, transpiler: new HarmonyMethod(typeof(ManagedBoundary), method) { priority = Priority.Last });

        private static IEnumerable<CodeInstruction> TransformInput(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var edges = code.Where(i => IsCall(i, typeof(EditorDirector),
                nameof(EditorDirector.updateLeftMouseStateForEngine))).ToArray();
            var modifiers = code.Where(i => i.opcode == OpCodes.Stfld &&
                i.operand is FieldInfo field && field.DeclaringType == typeof(EditorDirector) &&
                field.Name == nameof(EditorDirector.altPressed)).ToArray();
            int[] states = { 1, 2, 3, 3, 0 };
            inputReady = edges.Length == states.Length && modifiers.Length == 1;
            for (int i = 0; inputReady && i < edges.Length; i++)
            {
                int index = code.IndexOf(edges[i]);
                inputReady = index > 0 && code[index - 1].LoadsConstant(states[i]);
            }
            if (!inputReady)
                return Unavailable(code, "primary-button input");

            foreach (var edge in edges)
                ReplaceCall(edge, nameof(PlacementRuntime.ForwardButton));
            code.InsertRange(code.IndexOf(modifiers[0]) + 1, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Call, RuntimeMethod(nameof(PlacementRuntime.AfterInput)))
            });
            return code;
        }

        private static IEnumerable<CodeInstruction> TransformMapper(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            var calls = code.Where(i => IsCall(i, typeof(EngineInterface),
                nameof(EngineInterface.PlaceMapperItem))).ToArray();
            int cursor = calls.Length == 7 ? code.IndexOf(calls[0]) : -1;
            mapperReady = cursor >= 2 && cursor + 1 < code.Count &&
                code[cursor - 2].LoadsConstant(1L) && code[cursor - 1].IsLdloc() &&
                code[cursor + 1].opcode == OpCodes.Ret &&
                ((MethodInfo)calls[0].operand).GetParameters().Length == 8;
            if (!mapperReady)
                return Unavailable(code, "wall and brush cursor placement");

            ReplaceCall(calls[0], nameof(PlacementRuntime.ForwardMapper));
            return code;
        }

        private static IEnumerable<CodeInstruction> TransformEngine(IEnumerable<CodeInstruction> instructions)
        {
            var code = instructions.ToList();
            int enter = FindUniqueCall(code, typeof(Monitor), nameof(Monitor.Enter));
            int exit = FindUniqueCall(code, typeof(Monitor), nameof(Monitor.Exit));
            int prepare = FindUniqueCall(code, typeof(EditorDirector), nameof(EditorDirector.preDLLCallActions));
            int run = FindUniqueCall(code, typeof(EngineInterface), "DLL_RunTick");
            engineReady = enter >= 0 && enter < prepare && prepare < run && run < exit &&
                code.Count(i => i.opcode == OpCodes.Ldsfld && i.operand is FieldInfo field &&
                    field.DeclaringType == typeof(EngineInterface) && field.Name == "threadLock") == 1;
            if (!engineReady)
                return Unavailable(code, "locked engine tick");

            code.Insert(prepare + 1, new CodeInstruction(OpCodes.Call, RuntimeMethod(nameof(PlacementRuntime.BeforeNativeTick))));
            return code;
        }

        private static int FindUniqueCall(List<CodeInstruction> code, Type type, string name)
        {
            int first = code.FindIndex(i => IsCall(i, type, name));
            return first >= 0 && first == code.FindLastIndex(i => IsCall(i, type, name)) ? first : -1;
        }

        private static bool IsCall(CodeInstruction instruction, Type type, string name) =>
            (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) &&
            instruction.operand is MethodInfo method && method.DeclaringType == type && method.Name == name;

        private static MethodInfo RuntimeMethod(string name) =>
            typeof(PlacementRuntime).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static);

        private static void ReplaceCall(CodeInstruction instruction, string name)
        {
            instruction.opcode = OpCodes.Call;
            instruction.operand = RuntimeMethod(name);
        }

        private static IEnumerable<CodeInstruction> Unavailable(List<CodeInstruction> code, string boundary)
        {
            log?.LogError(Plugin.Stamp("PITCH_MOAT_LINE_DISABLED: incompatible " + boundary + " after managed patch composition."));
            return code;
        }
    }
}
