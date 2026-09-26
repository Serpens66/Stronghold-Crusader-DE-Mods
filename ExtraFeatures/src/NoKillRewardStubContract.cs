using Iced.Intel;
using RedBird.Abstractions.Hooks;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Context;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using static Iced.Intel.AssemblerRegisters;

namespace ExtraFeatures
{
    internal static class NoKillRewardStubContract
    {
        private static readonly byte[] DecisionBytes = { 0x85, 0xD2 }; // TEST EDX,EDX

        internal static void VerifyLiveSpan(ReadOnlySpan<byte> expected,
            ulong address, int rva)
        {
            var live = new byte[expected.Length];
            Marshal.Copy(new IntPtr(unchecked((long)address)), live, 0, live.Length);
            VerifyLiveSpan(expected, live, rva);
        }

        internal static void VerifyLiveSpan(ReadOnlySpan<byte> expected,
            ReadOnlySpan<byte> actual, int rva)
        {
            if (actual.Length != expected.Length)
                throw new InvalidOperationException("The live defeat-loot hook span has a different length at RVA 0x" + rva.ToString("X") + ".");
            for (int offset = 0; offset < expected.Length; offset++)
                if (actual[offset] != expected[offset])
                    throw new InvalidOperationException("The live defeat-loot hook span changed at RVA 0x" +
                        (rva + offset).ToString("X") + ".");
        }

        internal static IEnumerable<Instruction> SelectRewardInstructions(
            IReadOnlyList<Instruction> original, ulong cleanupAddress)
        {
            if (original.Count != 2 ||
                original[0].Mnemonic != Mnemonic.Imul || original[0].Length != 7 ||
                original[0].Op0Register != Register.R10 || original[0].Op1Register != Register.RBP ||
                original[1].Mnemonic != Mnemonic.Imul || original[1].Length != 7 ||
                original[1].Op0Register != Register.R13 || original[1].Op1Register != Register.RBP)
                throw new InvalidOperationException("The displaced Vanilla reward setup changed.");

            Decoder decoder = Decoder.Create(64, new ByteArrayCodeReader(DecisionBytes));
            Instruction test = decoder.Decode();
            if (test.IsInvalid || test.Length != 2 || test.Mnemonic != Mnemonic.Test ||
                test.Op0Register != Register.EDX || test.Op1Register != Register.EDX)
                throw new InvalidOperationException("Iced did not decode the decision test.");
            return new[] {
                test,
                Instruction.CreateBranch(Code.Je_rel32_64, cleanupAddress),
                original[0],
                original[1]
            };
        }

        internal static void GenerateGoldMessageGuard(
            Assembler assembler, ReadOnlySpan<Instruction> original, ulong returnAddress)
        {
            if (original.Length != 4 ||
                original[0].Mnemonic != Mnemonic.Mov || original[0].Length != 2 ||
                original[1].Mnemonic != Mnemonic.Sub || original[1].Length != 3 ||
                original[2].Mnemonic != Mnemonic.Mov || original[2].Length != 5 ||
                original[3].Mnemonic != Mnemonic.Call || original[3].Length != 5)
                throw new InvalidOperationException("The displaced Vanilla gold-message call changed.");

            // Vanilla's reward is at least 500. R12D=0 uniquely marks our
            // human-winner path; the kill message has already been queued.
            assembler.test(r12d, r12d);
            assembler.je(returnAddress);
            foreach (Instruction instruction in original)
                assembler.AddInstruction(instruction);
        }

        internal static void VerifyReward(X64InlineHook probe, ContextHookDelegate callback,
            ContextHookOptions options, ulong cleanupAddress, ulong returnAddress)
        {
            if (probe.DisplacedByteCount != 14)
                throw new InvalidOperationException("The probe displaced a different reward span.");
            probe.Generate(ContextStub.CreateGenerator(probe, callback, options));
            VerifyRewardEmitted(probe, cleanupAddress, returnAddress);
        }

        internal static void VerifyRewardEmitted(
            X64InlineHook probe, ulong cleanupAddress, ulong returnAddress)
        {
            Instruction[] instructions = DecodeStub(probe);
            for (int i = 0; i < instructions.Length - 4; i++)
            {
                if (!IsTest(instructions[i], Register.EDX))
                    continue;
                if (instructions[i + 1].Mnemonic != Mnemonic.Je ||
                    instructions[i + 1].NearBranchTarget != cleanupAddress ||
                    instructions[i + 2].Mnemonic != Mnemonic.Imul ||
                    instructions[i + 2].Op0Register != Register.R10 ||
                    instructions[i + 3].Mnemonic != Mnemonic.Imul ||
                    instructions[i + 3].Op0Register != Register.R13 ||
                    !IsJumpTo(instructions[i + 4], returnAddress))
                    throw new InvalidOperationException(
                        "The generated reward stub changed its branch or AI continuation: " +
                        Describe(instructions, i, 6) +
                        " expected cleanup=0x" + cleanupAddress.ToString("X") +
                        " return=0x" + returnAddress.ToString("X"));
                return;
            }
            throw new InvalidOperationException("The generated reward stub lacks the post-callback decision.");
        }

        internal static void VerifyGoldMessage(X64InlineHook probe, ulong returnAddress)
        {
            if (probe.DisplacedByteCount != 15)
                throw new InvalidOperationException("The probe displaced a different gold-message span.");
            probe.Generate(GenerateGoldMessageGuard);
            VerifyGoldMessageEmitted(probe, returnAddress);
        }

        internal static void VerifyGoldMessageEmitted(X64InlineHook probe, ulong returnAddress)
        {
            Instruction[] instructions = DecodeStub(probe);
            for (int i = 0; i < instructions.Length - 6; i++)
            {
                if (!IsTest(instructions[i], Register.R12D))
                    continue;
                if (instructions[i + 1].Mnemonic != Mnemonic.Je ||
                    instructions[i + 1].NearBranchTarget != returnAddress ||
                    instructions[i + 2].Mnemonic != Mnemonic.Mov ||
                    instructions[i + 2].Op0Register != Register.EDX ||
                    instructions[i + 3].Mnemonic != Mnemonic.Sub ||
                    instructions[i + 3].Op0Register != Register.R12D ||
                    instructions[i + 4].Mnemonic != Mnemonic.Mov ||
                    instructions[i + 5].Mnemonic != Mnemonic.Call ||
                    instructions[i + 5].NearBranchTarget !=
                        unchecked((ulong)((long)returnAddress - 0x1401A4L)) ||
                    !IsJumpTo(instructions[i + 6], returnAddress))
                    throw new InvalidOperationException(
                        "The generated gold-message stub changed its Vanilla continuation: " +
                        Describe(instructions, i, 8) +
                        " expected return=0x" + returnAddress.ToString("X"));
                return;
            }
            throw new InvalidOperationException("The generated gold-message stub lacks the zero-reward decision.");
        }

        private static bool IsTest(Instruction instruction, Register register) =>
            instruction.Mnemonic == Mnemonic.Test &&
            instruction.Op0Register == register && instruction.Op1Register == register;

        private static bool IsJumpTo(Instruction instruction, ulong target)
        {
            if (instruction.Mnemonic != Mnemonic.Jmp)
                return false;
            if (instruction.NearBranchTarget == target)
                return true;
            if (instruction.Op0Kind != OpKind.Memory || !instruction.IsIPRelativeMemoryOperand)
                return false;
            ulong slot = instruction.IPRelativeMemoryAddress;
            if (slot != instruction.NextIP)
                return false;
            return unchecked((ulong)Marshal.ReadInt64(new IntPtr(unchecked((long)slot)))) == target;
        }

        private static string Describe(Instruction[] instructions, int start, int count)
        {
            var parts = new List<string>();
            for (int i = start; i < instructions.Length && i < start + count; i++)
                parts.Add(instructions[i].Mnemonic + "@" + instructions[i].IP.ToString("X") +
                    " target=" + instructions[i].NearBranchTarget.ToString("X") +
                    " op0=" + instructions[i].Op0Register);
            return string.Join(" | ", parts);
        }

        private static Instruction[] DecodeStub(X64InlineHook probe)
        {
            if (probe.StubAddress == IntPtr.Zero)
                throw new InvalidOperationException("RedBird did not generate a stub.");
            const int scanBytes = 1024;
            byte[] bytes = new byte[scanBytes];
            Marshal.Copy(probe.StubAddress, bytes, 0, scanBytes);
            ulong address = unchecked((ulong)probe.StubAddress.ToInt64());
            Decoder decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes), address);
            var instructions = new List<Instruction>();
            while (decoder.IP < address + scanBytes - 32)
            {
                Instruction instruction = decoder.Decode();
                if (instruction.IsInvalid)
                    break;
                instructions.Add(instruction);
            }
            return instructions.ToArray();
        }
    }
}
