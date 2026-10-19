using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.ManagedHooks;

namespace PerfFixTest
{
    // V9: the Script Extender (2.14.3) detours GameMap.interpChimp with
    // ManagedHookManager.GameMap_InterpChimpHook, which allocates a new
    // UnitUnityVisualInterpolateEventArgs for every unit and frame (~450k/s, 10-14 MB/s) before
    // raising OnUnitUnityVisualInterpolate. Audited subscribers (Extender after-images, ExtraFeatures,
    // ImprovedHunters, SkinTest, VirtualUnitsPrototype; none in Fixes) only read args.Chimp
    // synchronously. The transpiler replaces that single allocation with a reused per-thread
    // instance; nested raises (depth > 1) still allocate so an outer event is never overwritten.
    // interpChimp itself is not hooked (no overlapping detour); the Extender keeps its control flow.
    internal static class InterpolateEventFix
    {
        [ThreadStatic] private static UnitUnityVisualInterpolateEventArgs reusable;
        [ThreadStatic] private static int depth;
        private static long reused, nested;
        private static int confirmed;

        internal static void Install()
        {
            try
            {
                MethodInfo target = AccessTools.Method(typeof(ManagedHookManager), "GameMap_InterpChimpHook",
                    new[] { typeof(GameMap), typeof(Chimp), typeof(bool) });
                if (target == null || target.ReturnType != typeof(void))
                    throw new MissingMethodException("ManagedHookManager.GameMap_InterpChimpHook signature differs from the audit.");
                ConstructorInfo constructor = typeof(UnitUnityVisualInterpolateEventArgs).GetConstructor(new[] { typeof(EventHookPhase), typeof(Chimp) });
                if (constructor == null) throw new MissingMethodException("UnitUnityVisualInterpolateEventArgs(EventHookPhase, Chimp) not found.");
                int allocations = PatchProcessor.GetOriginalInstructions(target)
                    .Count(instruction => instruction.opcode == OpCodes.Newobj && Equals(instruction.operand, constructor));
                if (allocations != 1)
                    throw new InvalidOperationException("Expected exactly one event allocation in the Extender hook, found " + allocations + ".");
                PerfFixTestPlugin.Harmony.Patch(target,
                    prefix: new HarmonyMethod(typeof(InterpolateEventFix), nameof(Enter)),
                    transpiler: new HarmonyMethod(typeof(InterpolateEventFix), nameof(Transpiler)),
                    finalizer: new HarmonyMethod(typeof(InterpolateEventFix), nameof(Leave)));
                PerfFixTestPlugin.Note("V9 InterpolateEventReuse installed on ManagedHookManager.GameMap_InterpChimpHook.");
            }
            catch (Exception exception) { PerfFixTestPlugin.Fail("V9 InterpolateEventReuse disabled: " + exception.Message); }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            ConstructorInfo constructor = typeof(UnitUnityVisualInterpolateEventArgs).GetConstructor(new[] { typeof(EventHookPhase), typeof(Chimp) });
            MethodInfo replacement = AccessTools.Method(typeof(InterpolateEventFix), nameof(Rent));
            foreach (CodeInstruction instruction in instructions)
            {
                // Same stack contract: (EventHookPhase, Chimp) -> UnitUnityVisualInterpolateEventArgs.
                if (instruction.opcode == OpCodes.Newobj && Equals(instruction.operand, constructor))
                    yield return new CodeInstruction(OpCodes.Call, replacement).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
                else
                    yield return instruction;
            }
        }

        private static void Enter() => depth++;

        private static Exception Leave(Exception __exception)
        {
            depth--;
            return __exception;
        }

        private static UnitUnityVisualInterpolateEventArgs Rent(EventHookPhase phase, Chimp chimp)
        {
            if (depth > 1)
            {
                nested++;
                return new UnitUnityVisualInterpolateEventArgs(phase, chimp);
            }
            UnitUnityVisualInterpolateEventArgs args = reusable;
            if (args == null || args.Phase != phase) reusable = args = new UnitUnityVisualInterpolateEventArgs(phase, chimp);
            // Reset everything a subscriber could have changed on the shared instance.
            args.Chimp = chimp;
            args.SkipOriginalFunction = false;
            reused++;
            if (confirmed == 0)
            {
                confirmed = 1;
                PerfFixTestPlugin.Note("V9 confirmed: first interpolate event served from the reused instance.");
            }
            else if (reused % 5000000 == 0)
                PerfFixTestPlugin.Note("V9 reused=" + reused + " nestedAllocations=" + nested + ".");
            return args;
        }
    }
}
