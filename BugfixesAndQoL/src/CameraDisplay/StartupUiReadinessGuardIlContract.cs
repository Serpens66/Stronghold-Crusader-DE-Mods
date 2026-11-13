// Feature: Fail-closed IL contract for Vanilla's startup UI and radar update tail.
using CrusaderDE;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.Utils;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal static class StartupUiReadinessGuardIlContract
    {
        internal const string RadarScrollMapMethodName = "RadarScrollMap";

        internal sealed class PatchSite
        {
            internal PatchSite(
                int insertionIndex,
                Instruction branch,
                Instruction radarCall,
                Instruction finalReturn)
            {
                InsertionIndex = insertionIndex;
                Branch = branch;
                RadarCall = radarCall;
                FinalReturn = finalReturn;
            }

            internal int InsertionIndex { get; }
            internal Instruction Branch { get; }
            internal Instruction RadarCall { get; }
            internal Instruction FinalReturn { get; }
        }

        internal static void ApplyPatch(ILContext context, Func<bool, bool> readinessGuard)
        {
            if (readinessGuard == null)
                throw new ArgumentNullException(nameof(readinessGuard));

            ApplyPatchCore(context, cursor => cursor.EmitDelegate(readinessGuard));
        }

        internal static void ApplyPatchCore(ILContext context, Action<ILCursor> emitGuard)
        {
            if (emitGuard == null)
                throw new ArgumentNullException(nameof(emitGuard));

            PatchSite site = FindUniquePatchSite(context);
            object finalReturnTarget = site.Branch.Operand is ILLabel
                ? (object)context.DefineLabel(site.FinalReturn)
                : site.FinalReturn;

            var cursor = new ILCursor(context) { Index = site.InsertionIndex };
            emitGuard(cursor);
            site.Branch.Operand = finalReturnTarget;
        }

        internal static PatchSite FindUniquePatchSite(ILContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            IList<Instruction> instructions = context.Body.Instructions;
            var matches = new List<PatchSite>();

            for (int index = 0; index < instructions.Count - 1; index++)
            {
                Instruction branch = instructions[index + 1];
                if (!MatchesField(
                        instructions[index],
                        OpCodes.Ldsfld,
                        typeof(MainViewModel),
                        nameof(MainViewModel.viewModelLoaded),
                        MetadataType.Boolean) ||
                    !IsFalseBranch(branch) ||
                    !TryGetBranchTarget(branch.Operand, out Instruction blockEnd))
                {
                    continue;
                }

                int blockEndIndex = instructions.IndexOf(blockEnd);
                if (blockEndIndex <= index + 1 ||
                    !TryGetExpectedPostBlockTail(
                        instructions,
                        blockEndIndex,
                        out Instruction radarCall,
                        out Instruction finalReturn))
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
                    matches.Add(new PatchSite(index + 1, branch, radarCall, finalReturn));
            }

            if (matches.Count != 1)
            {
                throw new InvalidOperationException(
                    "Expected exactly one complete FatControler.Update startup UI block guarded by " +
                    $"MainViewModel.viewModelLoaded, found {matches.Count}.");
            }

            return matches[0];
        }

        internal static void ValidateVulnerableRadarScrollMap(MethodInfo radarScrollMap)
        {
            if (radarScrollMap == null ||
                radarScrollMap.IsStatic ||
                radarScrollMap.ReturnType != typeof(void) ||
                radarScrollMap.GetParameters().Length != 0)
            {
                throw new InvalidOperationException(
                    "FatControler.RadarScrollMap no longer has the expected instance void signature.");
            }

            using (var definition = new DynamicMethodDefinition(radarScrollMap))
            using (var context = new ILContext(definition.Definition))
            {
                bool matched = false;
                context.Invoke(il => matched = HasVulnerableRadarPrefix(il));
                if (!matched)
                {
                    throw new InvalidOperationException(
                        "FatControler.RadarScrollMap no longer contains the known unguarded " +
                        "MainControls.instance.IsUIVisible startup access.");
                }
            }
        }

        internal static bool HasVulnerableRadarPrefix(ILContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            IList<Instruction> instructions = context.Body.Instructions;
            if (instructions.Count < 11 ||
                !MatchesField(
                    instructions[0],
                    OpCodes.Ldsfld,
                    typeof(MainViewModel),
                    nameof(MainViewModel.viewModelLoaded),
                    MetadataType.Boolean) ||
                !IsFalseBranch(instructions[1]) ||
                !MatchesMethod(
                    instructions[2],
                    OpCodes.Call,
                    typeof(MainViewModel),
                    "get_Instance",
                    MetadataType.Class) ||
                !MatchesMethod(
                    instructions[3],
                    OpCodes.Callvirt,
                    typeof(MainViewModel),
                    "get_Show_HUD_Briefing",
                    MetadataType.Boolean) ||
                !IsFalseBranch(instructions[4]) ||
                instructions[5].OpCode != OpCodes.Ret ||
                !MatchesField(
                    instructions[6],
                    OpCodes.Ldsfld,
                    typeof(MainControls),
                    nameof(MainControls.instance),
                    MetadataType.Class) ||
                !MatchesMethod(
                    instructions[7],
                    OpCodes.Callvirt,
                    typeof(MainControls),
                    "get_IsUIVisible",
                    MetadataType.Boolean) ||
                !IsTrueBranch(instructions[8]) ||
                instructions[9].OpCode != OpCodes.Ret)
            {
                return false;
            }

            return TryGetBranchTarget(instructions[1].Operand, out Instruction unloadedReturn) &&
                ReferenceEquals(unloadedReturn, instructions[5]) &&
                TryGetBranchTarget(instructions[4].Operand, out Instruction controlsLoad) &&
                ReferenceEquals(controlsLoad, instructions[6]) &&
                TryGetBranchTarget(instructions[8].Operand, out Instruction readyContinuation) &&
                ReferenceEquals(readyContinuation, instructions[10]);
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

        private static bool TryGetExpectedPostBlockTail(
            IList<Instruction> instructions,
            int blockEndIndex,
            out Instruction radarCall,
            out Instruction finalReturn)
        {
            radarCall = null;
            finalReturn = null;
            if (blockEndIndex + 2 >= instructions.Count ||
                instructions[blockEndIndex].OpCode != OpCodes.Ldarg_0 ||
                !MatchesMethod(
                    instructions[blockEndIndex + 1],
                    OpCodes.Call,
                    typeof(FatControler),
                    RadarScrollMapMethodName,
                    MetadataType.Void) ||
                instructions[blockEndIndex + 2].OpCode != OpCodes.Ret)
            {
                return false;
            }

            radarCall = instructions[blockEndIndex + 1];
            finalReturn = instructions[blockEndIndex + 2];
            return true;
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
                if (MatchesMethod(
                        instructions[index],
                        OpCodes.Call,
                        declaringType,
                        methodName,
                        MetadataType.Void) ||
                    MatchesMethod(
                        instructions[index],
                        OpCodes.Callvirt,
                        declaringType,
                        methodName,
                        MetadataType.Void))
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
            string methodName,
            MetadataType returnType) =>
            instruction.OpCode == opcode &&
            instruction.Operand is MethodReference method &&
            method.DeclaringType.FullName == declaringType.FullName &&
            method.Name == methodName &&
            method.Parameters.Count == 0 &&
            method.ReturnType.MetadataType == returnType;

        private static bool MatchesField(
            Instruction instruction,
            OpCode opcode,
            Type declaringType,
            string fieldName,
            MetadataType fieldType) =>
            instruction.OpCode == opcode &&
            instruction.Operand is FieldReference field &&
            field.DeclaringType.FullName == declaringType.FullName &&
            field.Name == fieldName &&
            field.FieldType.MetadataType == fieldType;

        private static bool IsFalseBranch(Instruction instruction) =>
            instruction.OpCode == OpCodes.Brfalse || instruction.OpCode == OpCodes.Brfalse_S;

        private static bool IsTrueBranch(Instruction instruction) =>
            instruction.OpCode == OpCodes.Brtrue || instruction.OpCode == OpCodes.Brtrue_S;
    }
}
