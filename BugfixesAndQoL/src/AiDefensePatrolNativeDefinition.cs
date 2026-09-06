using Iced.Intel;
using SHCDESE.Interop;
using System;
using System.Runtime.InteropServices;

namespace BugfixesAndQoL
{
    internal static class AiDefensePatrolNativeDefinition
    {
        internal const ulong PreferredImageBase = 0x180000000;
        internal const int RecruitFunctionRva = 0x40740;
        internal const int RecruitFunctionSize = 0x66D;
        internal const int DecisionHookRva = 0x40C97;
        internal const int DecisionHookLength = 15;
        internal const int DecisionCompareRva = 0x40CA6;
        internal const int PatrolCallRva = 0x40CB1;
        internal const int CastleCallRva = 0x40CB8;
        internal const int PatrolAssignmentRva = 0x29430;
        internal const int CastleAssignmentRva = 0x291F0;

        internal const string DecisionPattern =
            "48 8B 44 24 38 48 8B CD 8B 84 28 84 01 00 00 " +
            "39 84 16 ?? ?? ?? ?? 8B D3 7C ?? E8 ?? ?? ?? ?? " +
            "EB ?? E8 ?? ?? ?? ?? EB ?? 83 F8 01";

        private const string HookBytes =
            "48 8B 44 24 38 48 8B CD 8B 84 28 84 01 00 00";
        private const string DecisionTailBytes =
            "39 84 16 B8 DE 79 03 8B D3 7C 07 " +
            "E8 7A 87 FE FF EB 2C E8 33 85 FE FF EB 25";

        internal static void Validate(ReadOnlySpan<byte> memory)
        {
            int resolvedRva = Shared.NativePatternResolver.FindUniquePattern(
                memory,
                DecisionPattern,
                "AI defense patrol assignment decision");
            if (resolvedRva != DecisionHookRva)
            {
                throw new InvalidOperationException(
                    $"AI defense patrol decision resolved to unexpected RVA 0x{resolvedRva:X}.");
            }

            if (!Shared.NativePatternResolver.MatchesPatternAt(memory, DecisionHookRva, HookBytes) ||
                !Shared.NativePatternResolver.MatchesPatternAt(memory, DecisionCompareRva, DecisionTailBytes))
            {
                throw new InvalidOperationException("AI defense patrol decision bytes differ from the audited contract.");
            }

            if (DecisionHookRva + DecisionHookLength != DecisionCompareRva)
                throw new InvalidOperationException("AI defense patrol hook span does not end at the Vanilla compare.");

            int patrolTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                PatrolCallRva + 1,
                PatrolCallRva + 5);
            int castleTarget = Shared.NativePatternResolver.ResolveRelativeTarget(
                memory,
                CastleCallRva + 1,
                CastleCallRva + 5);
            if (patrolTarget != PatrolAssignmentRva || castleTarget != CastleAssignmentRva)
            {
                throw new InvalidOperationException(
                    $"AI defense assignment targets differ: patrol=0x{patrolTarget:X}, castle=0x{castleTarget:X}.");
            }

            ValidateInstructionsAndControlFlow(memory);
        }

        internal static void ValidateManagedLayout()
        {
            if (Marshal.SizeOf(typeof(GameUnit)) != 0x490 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_AliveState)).ToInt32() != 0x88 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_UnitChimp)).ToInt32() != 0x8A ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_ControllableForPlayerId)).ToInt32() != 0x92 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_GlobalId)).ToInt32() != 0x94 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_AITribeRole)).ToInt32() != 0x426)
            {
                throw new InvalidOperationException("GameUnit layout differs from the audited Script Extender 2.0.2 contract.");
            }
        }

        private static void ValidateInstructionsAndControlFlow(ReadOnlySpan<byte> memory)
        {
            int functionEnd = checked(RecruitFunctionRva + RecruitFunctionSize);
            if (RecruitFunctionRva < 0 || functionEnd > memory.Length)
                throw new InvalidOperationException("AI recruit function lies outside the native image.");

            byte[] code = memory.Slice(RecruitFunctionRva, RecruitFunctionSize).ToArray();
            Decoder decoder = Decoder.Create(64, new ByteArrayCodeReader(code), DecoderOptions.None);
            decoder.IP = PreferredImageBase + unchecked((uint)RecruitFunctionRva);

            int hookInstructionCount = 0;
            int hookDecodedLength = 0;
            bool compareValidated = false;
            bool signedBranchValidated = false;
            bool patrolCallValidated = false;
            bool castleCallValidated = false;

            while (decoder.IP < PreferredImageBase + unchecked((uint)functionEnd))
            {
                decoder.Decode(out Instruction instruction);
                if (instruction.Code == Code.INVALID)
                    throw new InvalidOperationException("Invalid instruction encountered in the AI recruit function.");

                int instructionRva = checked((int)(instruction.IP - PreferredImageBase));
                if (instructionRva >= DecisionHookRva && instructionRva < DecisionCompareRva)
                {
                    hookInstructionCount++;
                    hookDecodedLength += instruction.Length;
                    if (instruction.Mnemonic != Mnemonic.Mov)
                        throw new InvalidOperationException("The AI defense hook span contains a non-MOV instruction.");
                }

                if (instructionRva == DecisionCompareRva)
                    compareValidated = instruction.Mnemonic == Mnemonic.Cmp && instruction.Length == 7;
                else if (instructionRva == 0x40CAF)
                {
                    signedBranchValidated = instruction.Mnemonic == Mnemonic.Jl &&
                        instruction.NearBranchTarget == PreferredImageBase + 0x40CB8;
                }
                else if (instructionRva == PatrolCallRva)
                {
                    patrolCallValidated = instruction.Mnemonic == Mnemonic.Call &&
                        instruction.NearBranchTarget == PreferredImageBase + PatrolAssignmentRva;
                }
                else if (instructionRva == CastleCallRva)
                {
                    castleCallValidated = instruction.Mnemonic == Mnemonic.Call &&
                        instruction.NearBranchTarget == PreferredImageBase + CastleAssignmentRva;
                }

                if (IsNearBranch(instruction.Op0Kind))
                {
                    ulong target = instruction.NearBranchTarget;
                    ulong firstInteriorByte = PreferredImageBase + unchecked((uint)DecisionHookRva) + 1;
                    ulong hookEnd = PreferredImageBase + unchecked((uint)DecisionCompareRva);
                    if (target >= firstInteriorByte && target < hookEnd)
                    {
                        throw new InvalidOperationException(
                            $"A branch at RVA 0x{instructionRva:X} enters the middle of the hook span.");
                    }
                }
            }

            if (hookInstructionCount != 3 || hookDecodedLength != DecisionHookLength ||
                !compareValidated || !signedBranchValidated ||
                !patrolCallValidated || !castleCallValidated)
            {
                throw new InvalidOperationException(
                    "AI defense hook instruction, branch, or call-target validation failed.");
            }
        }

        private static bool IsNearBranch(OpKind kind) =>
            kind == OpKind.NearBranch16 ||
            kind == OpKind.NearBranch32 ||
            kind == OpKind.NearBranch64;
    }
}
