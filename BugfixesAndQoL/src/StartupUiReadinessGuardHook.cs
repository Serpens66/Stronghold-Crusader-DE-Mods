// Feature: Keep Vanilla's UI update block dormant until its Noesis roots exist.
using BepInEx.Logging;
using CrusaderDE;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal sealed class StartupUiReadinessGuardHook
    {
        private readonly ILHook hook;

        internal StartupUiReadinessGuardHook(ManualLogSource log)
        {
            if (log == null)
                throw new ArgumentNullException(nameof(log));

            MethodInfo target = typeof(FatControler).GetMethod(
                "Update",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);
            if (target == null || target.ReturnType != typeof(void))
                throw new MissingMethodException(typeof(FatControler).FullName, "Update");

            ILHook candidate = null;
            try
            {
                candidate = new ILHook(target, PatchUiUpdateReadinessBranch);
                hook = candidate;
            }
            catch
            {
                candidate?.Dispose();
                throw;
            }

            Shared.DebugLogHelper.LogDebug(
                log,
                "Bugfixes and QoL Vanilla startup UI-readiness guard installed for the process lifetime.");
        }

        private static void PatchUiUpdateReadinessBranch(ILContext context)
        {
            IList<Instruction> instructions = context.Body.Instructions;
            var matches = new List<int>();

            for (int index = 0; index < instructions.Count - 1; index++)
            {
                if (!MatchesField(
                        instructions[index],
                        OpCodes.Ldsfld,
                        typeof(MainViewModel),
                        nameof(MainViewModel.viewModelLoaded)) ||
                    !IsFalseBranch(instructions[index + 1]) ||
                    !(instructions[index + 1].Operand is Instruction blockEnd))
                {
                    continue;
                }

                int blockEndIndex = instructions.IndexOf(blockEnd);
                if (blockEndIndex <= index + 1)
                    continue;

                int rolloverCalls = CountCalls(
                    instructions,
                    index + 2,
                    blockEndIndex,
                    typeof(MainViewModel),
                    nameof(MainViewModel.CrossThreadRolloverUpdate));
                int frontendUpdateCalls = CountCalls(
                    instructions,
                    index + 2,
                    blockEndIndex,
                    typeof(FrontendMenus),
                    nameof(FrontendMenus.Update));

                if (rolloverCalls == 1 && frontendUpdateCalls == 1)
                    matches.Add(index);
            }

            if (matches.Count != 1)
            {
                throw new InvalidOperationException(
                    "Expected exactly one FatControler.Update UI block guarded by " +
                    $"MainViewModel.viewModelLoaded, found {matches.Count}.");
            }

            var cursor = new ILCursor(context) { Index = matches[0] + 1 };
            cursor.EmitDelegate<Func<bool, bool>>(IsVanillaUiUpdateReady);
        }

        private static bool IsVanillaUiUpdateReady(bool viewModelLoaded)
        {
            if (!viewModelLoaded)
                return false;

            MainViewModel main = MainViewModel.Instance;
            return StartupUiReadinessGuardPolicy.ShouldRunVanillaUiUpdateBlock(
                viewModelLoaded,
                main?.HUDmain != null,
                main?.FrontEndMenu != null);
        }

        private static int CountCalls(
            IList<Instruction> instructions,
            int startIndex,
            int endIndex,
            Type declaringType,
            string methodName)
        {
            int count = 0;
            for (int index = startIndex; index < endIndex; index++)
            {
                if ((instructions[index].OpCode == OpCodes.Call ||
                     instructions[index].OpCode == OpCodes.Callvirt) &&
                    instructions[index].Operand is MethodReference method &&
                    method.DeclaringType.FullName == declaringType.FullName &&
                    method.Name == methodName &&
                    method.Parameters.Count == 0 &&
                    method.ReturnType.MetadataType == MetadataType.Void)
                {
                    count++;
                }
            }
            return count;
        }

        private static bool MatchesField(
            Instruction instruction,
            OpCode opcode,
            Type declaringType,
            string fieldName) =>
            instruction.OpCode == opcode &&
            instruction.Operand is FieldReference field &&
            field.DeclaringType.FullName == declaringType.FullName &&
            field.Name == fieldName;

        private static bool IsFalseBranch(Instruction instruction) =>
            instruction.OpCode == OpCodes.Brfalse || instruction.OpCode == OpCodes.Brfalse_S;
    }
}
