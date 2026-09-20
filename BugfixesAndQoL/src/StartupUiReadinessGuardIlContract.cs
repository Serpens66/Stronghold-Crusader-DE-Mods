// Feature: Fail-closed IL contract for Vanilla's startup UI update block.
using CrusaderDE;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    internal static class StartupUiReadinessGuardIlContract
    {
        internal const string RadarScrollMapMethodName = "RadarScrollMap";

        internal static int FindUniqueInsertionIndex(ILContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

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
                    !TryGetBranchTarget(instructions[index + 1].Operand, out Instruction blockEnd))
                {
                    continue;
                }

                int blockEndIndex = instructions.IndexOf(blockEnd);
                if (blockEndIndex <= index + 1 ||
                    !HasExpectedPostBlockTail(instructions, blockEndIndex))
                {
                    continue;
                }

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
                    matches.Add(index + 1);
            }

            if (matches.Count != 1)
            {
                throw new InvalidOperationException(
                    "Expected exactly one complete FatControler.Update startup UI block guarded by " +
                    $"MainViewModel.viewModelLoaded, found {matches.Count}.");
            }

            return matches[0];
        }

        private static bool TryGetBranchTarget(object operand, out Instruction target)
        {
            if (operand is ILLabel label)
            {
                target = label.Target;
                return target != null;
            }

            target = operand as Instruction;
            return target != null;
        }

        private static bool HasExpectedPostBlockTail(
            IList<Instruction> instructions,
            int blockEndIndex) =>
            blockEndIndex + 2 < instructions.Count &&
            instructions[blockEndIndex].OpCode == OpCodes.Ldarg_0 &&
            MatchesMethod(
                instructions[blockEndIndex + 1],
                OpCodes.Call,
                typeof(FatControler),
                RadarScrollMapMethodName) &&
            instructions[blockEndIndex + 2].OpCode == OpCodes.Ret;

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
                if (MatchesMethod(instructions[index], OpCodes.Call, declaringType, methodName) ||
                    MatchesMethod(instructions[index], OpCodes.Callvirt, declaringType, methodName))
                {
                    count++;
                }
            }
            return count;
        }

        private static bool MatchesMethod(
            Instruction instruction,
            OpCode opcode,
            Type declaringType,
            string methodName) =>
            instruction.OpCode == opcode &&
            instruction.Operand is MethodReference method &&
            method.DeclaringType.FullName == declaringType.FullName &&
            method.Name == methodName &&
            method.Parameters.Count == 0 &&
            method.ReturnType.MetadataType == MetadataType.Void;

        private static bool MatchesField(
            Instruction instruction,
            OpCode opcode,
            Type declaringType,
            string fieldName) =>
            instruction.OpCode == opcode &&
            instruction.Operand is FieldReference field &&
            field.DeclaringType.FullName == declaringType.FullName &&
            field.Name == fieldName &&
            field.FieldType.MetadataType == MetadataType.Boolean;

        private static bool IsFalseBranch(Instruction instruction) =>
            instruction.OpCode == OpCodes.Brfalse || instruction.OpCode == OpCodes.Brfalse_S;
    }
}
