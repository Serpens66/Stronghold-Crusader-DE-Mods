using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using HarmonyLib;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Units;
using SHCDESE.ManagedHooks;
using UnityEngine;

namespace PerfFixTest
{
    // V9: the Script Extender (2.14.3) detours GameMap.interpChimp with
    // ManagedHookManager.GameMap_InterpChimpHook, which allocates a new
    // UnitUnityVisualInterpolateEventArgs for every unit and frame (~450k/s, 10-14 MB/s) before
    // raising OnUnitUnityVisualInterpolate. Audited subscribers (Extender after-images, ExtraFeatures,
    // ImprovedHunters, SkinTest, VirtualUnitsPrototype; none in Fixes) only read args.Chimp
    // synchronously. A transpiler replaces that single allocation with a reused instance and
    // releases it right after Raise. The hook runs ~3000 times per frame, so no Harmony prefix or
    // finalizer is added (their try/finally wrapper measurably slowed the hook):
    // - only the outermost raise on the main thread reuses the instance; nested raises (a subscriber
    //   re-entering interpChimp) and other threads allocate as before, so no event is overwritten;
    // - if a subscriber throws, the Extender's catch skips the release; the depth then stays raised
    //   (Vanilla-like allocation) until the next frame resets it in onBeforeRender.
    // interpChimp itself is not hooked (no overlapping detour); the Extender keeps its control flow.
    internal static class InterpolateEventFix
    {
        private static UnitUnityVisualInterpolateEventArgs reusable;
        private static int depth, mainThreadId = -1, confirmed;
        private static long reused, fresh;

        internal static void Install()
        {
            try
            {
                MethodInfo target = AccessTools.Method(typeof(ManagedHookManager), "GameMap_InterpChimpHook",
                    new[] { typeof(GameMap), typeof(Chimp), typeof(bool) });
                if (target == null || target.ReturnType != typeof(void))
                    throw new MissingMethodException("ManagedHookManager.GameMap_InterpChimpHook signature differs from the audit.");
                List<CodeInstruction> original = PatchProcessor.GetOriginalInstructions(target);
                ConstructorInfo constructor = Constructor();
                int allocations = original.Count(i => i.opcode == OpCodes.Newobj && Equals(i.operand, constructor));
                int raises = original.Count(IsRaise);
                if (allocations != 1 || raises != 1)
                    throw new InvalidOperationException("Expected one event allocation and one Raise in the Extender hook, found " +
                        allocations + "/" + raises + ".");
                PerfFixTestPlugin.Harmony.Patch(target, transpiler: new HarmonyMethod(typeof(InterpolateEventFix), nameof(Transpiler)));
                Application.onBeforeRender += ResetPerFrame;
                PerfFixTestPlugin.Note("V9 InterpolateEventReuse installed on ManagedHookManager.GameMap_InterpChimpHook (transpiler only).");
            }
            catch (Exception exception) { PerfFixTestPlugin.Fail("V9 InterpolateEventReuse disabled: " + exception.Message); }
        }

        private static ConstructorInfo Constructor() =>
            typeof(UnitUnityVisualInterpolateEventArgs).GetConstructor(new[] { typeof(EventHookPhase), typeof(Chimp) });

        private static bool IsRaise(CodeInstruction instruction) =>
            (instruction.opcode == OpCodes.Call || instruction.opcode == OpCodes.Callvirt) && instruction.operand is MethodInfo method &&
            method.Name == "Raise" && method.DeclaringType == typeof(R3EventHook<UnitUnityVisualInterpolateEventArgs>);

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            ConstructorInfo constructor = Constructor();
            MethodInfo rent = AccessTools.Method(typeof(InterpolateEventFix), nameof(Rent));
            MethodInfo release = AccessTools.Method(typeof(InterpolateEventFix), nameof(Release));
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Newobj && Equals(instruction.operand, constructor))
                {
                    // Same stack contract: (EventHookPhase, Chimp) -> UnitUnityVisualInterpolateEventArgs.
                    yield return new CodeInstruction(OpCodes.Call, rent).MoveLabelsFrom(instruction).MoveBlocksFrom(instruction);
                    continue;
                }
                yield return instruction;
                // Raise returns void and Release takes nothing: the evaluation stack is unchanged.
                if (IsRaise(instruction)) yield return new CodeInstruction(OpCodes.Call, release);
            }
        }

        private static bool OnMainThread => mainThreadId >= 0 && Thread.CurrentThread.ManagedThreadId == mainThreadId;

        private static UnitUnityVisualInterpolateEventArgs Rent(EventHookPhase phase, Chimp chimp)
        {
            if (!OnMainThread)
            {
                fresh++;
                return new UnitUnityVisualInterpolateEventArgs(phase, chimp);
            }
            if (depth++ > 0)
            {
                fresh++;
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
                PerfFixTestPlugin.Note("V9 reused=" + reused + " freshAllocations=" + fresh + ".");
            return args;
        }

        private static void Release()
        {
            if (OnMainThread && depth > 0) depth--;
        }

        private static void ResetPerFrame()
        {
            if (mainThreadId < 0) mainThreadId = Thread.CurrentThread.ManagedThreadId;
            depth = 0;
        }
    }
}
