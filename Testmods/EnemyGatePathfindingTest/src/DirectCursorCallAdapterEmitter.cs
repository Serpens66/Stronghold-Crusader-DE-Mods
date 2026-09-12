using Iced.Intel;
using System;
using System.Collections.Generic;
using System.IO;
using static Iced.Intel.AssemblerRegisters;

namespace EnemyGatePathfindingTest
{
    // Re-emits the three cursor argument loads and replaces only the audited direct
    // DB650 call. The wrapper binds a player mask and then calls the same Vanilla search.
    internal static class DirectCursorCallAdapterEmitter
    {
        internal static void Emit(
            Assembler assembler,
            ReadOnlySpan<Instruction> original,
            ulong wrapperAddress)
        {
            if (assembler == null) throw new ArgumentNullException(nameof(assembler));
            ValidateOriginal(original);
            assembler.AddInstruction(original[0]);
            assembler.AddInstruction(original[1]);
            assembler.AddInstruction(original[2]);
            assembler.mov(r11, wrapperAddress);
            assembler.call(r11);
        }

        internal static byte[] AssembleAndValidate(
            byte[] originalBytes,
            ulong originalIp,
            ulong wrapperAddress,
            ulong stubIp)
        {
            Instruction[] original = DecodeExact(originalBytes, originalIp);
            ValidateOriginal(original);
            var assembler = new Assembler(64);
            Emit(assembler, original, wrapperAddress);
            using (var stream = new MemoryStream())
            {
                assembler.Assemble(new StreamCodeWriter(stream), stubIp);
                byte[] encoded = stream.ToArray();
                ValidateStub(encoded, stubIp, wrapperAddress);
                return encoded;
            }
        }

        private static Instruction[] DecodeExact(byte[] bytes, ulong ip)
        {
            if (bytes == null || bytes.Length !=
                EnemyGatePathfindingNativeDefinition.DirectCursorSearchBlockLength)
                throw new InvalidOperationException("Direct cursor block has an unexpected length.");
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = ip;
            var result = new List<Instruction>();
            ulong end = ip + (ulong)bytes.Length;
            while (decoder.IP < end)
            {
                decoder.Decode(out Instruction instruction);
                if (instruction.Code == Code.INVALID || decoder.IP > end)
                    throw new InvalidOperationException("Direct cursor block is not instruction aligned.");
                result.Add(instruction);
            }
            if (decoder.IP != end) throw new InvalidOperationException("Direct cursor block was not consumed exactly.");
            return result.ToArray();
        }

        private static void ValidateOriginal(ReadOnlySpan<Instruction> original)
        {
            if (original.Length != 4 ||
                original[0].IP != original[1].IP - 9 ||
                original[1].IP != original[2].IP - 8 ||
                original[2].IP != original[3].IP - 7 ||
                original[3].IP - original[0].IP != 24)
                throw new InvalidOperationException("Direct cursor block does not contain its four audited instructions.");
            if (original[0].Mnemonic != Mnemonic.Movsx ||
                original[1].Mnemonic != Mnemonic.Movsx ||
                original[2].Mnemonic != Mnemonic.Lea ||
                original[3].Mnemonic != Mnemonic.Call ||
                original[3].NearBranchTarget != original[0].IP -
                    (ulong)EnemyGatePathfindingNativeDefinition.DirectCursorSearchBlockRva +
                    (ulong)EnemyGatePathfindingNativeDefinition.DirectTileSearchRva)
                throw new InvalidOperationException("Direct cursor call target or argument preparation differs from baseline.");
        }

        private static void ValidateStub(byte[] bytes, ulong ip, ulong wrapperAddress)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = ip;
            int count = 0;
            bool wrapperPointer = false;
            bool wrapperCall = false;
            ulong end = ip + (ulong)bytes.Length;
            while (decoder.IP < end)
            {
                decoder.Decode(out Instruction instruction);
                if (instruction.Code == Code.INVALID || decoder.IP > end)
                    throw new InvalidOperationException("Direct cursor adapter contains invalid code.");
                count++;
                if (instruction.Mnemonic == Mnemonic.Mov &&
                    instruction.Op0Register == Register.R11 &&
                    instruction.Immediate64 == wrapperAddress)
                    wrapperPointer = true;
                if (instruction.Mnemonic == Mnemonic.Call &&
                    instruction.Op0Kind == OpKind.Register &&
                    instruction.Op0Register == Register.R11)
                    wrapperCall = true;
                if (instruction.Mnemonic == Mnemonic.Call &&
                    instruction.Op0Kind == OpKind.NearBranch64)
                    throw new InvalidOperationException("Direct cursor adapter retained the original direct call.");
            }
            if (count != 5 || !wrapperPointer || !wrapperCall)
                throw new InvalidOperationException("Direct cursor adapter did not emit the audited five-instruction form.");
        }
    }

    // Replaces the normal cursor's E2610 call while the original ABI arguments and
    // representative unit in R14 are still live. The wrapper receives a private,
    // correctly aligned Win64 call frame; Vanilla's TEST and RIP-relative LEA are
    // then replayed exactly once.
    internal static class CursorPclCallAdapterEmitter
    {
        private const int PrivateCallFrameSize = 0x30;

        internal static void Emit(
            Assembler assembler,
            ReadOnlySpan<Instruction> original,
            ulong wrapperAddress)
        {
            if (assembler == null) throw new ArgumentNullException(nameof(assembler));
            ValidateOriginal(original);

            assembler.mov(r10d, __dword_ptr[rsp + 0x20]);
            assembler.sub(rsp, PrivateCallFrameSize);
            assembler.mov(__dword_ptr[rsp + 0x20], r10d);
            assembler.mov(__dword_ptr[rsp + 0x28], r14d);
            assembler.mov(r11, wrapperAddress);
            assembler.call(r11);
            assembler.add(rsp, PrivateCallFrameSize);
            assembler.AddInstruction(original[1]);
            assembler.AddInstruction(original[2]);
        }

        internal static byte[] AssembleAndValidate(
            byte[] originalBytes,
            ulong originalIp,
            ulong wrapperAddress,
            ulong stubIp)
        {
            Instruction[] original = DecodeExact(originalBytes, originalIp);
            ValidateOriginal(original);
            var assembler = new Assembler(64);
            Emit(assembler, original, wrapperAddress);
            using (var stream = new MemoryStream())
            {
                assembler.Assemble(new StreamCodeWriter(stream), stubIp);
                byte[] encoded = stream.ToArray();
                ValidateStub(encoded, stubIp, wrapperAddress,
                    original[2].IPRelativeMemoryAddress);
                return encoded;
            }
        }

        private static Instruction[] DecodeExact(byte[] bytes, ulong ip)
        {
            if (bytes == null || bytes.Length !=
                EnemyGatePathfindingNativeDefinition.CursorPclDecisionLength)
                throw new InvalidOperationException("Cursor PCL block has an unexpected length.");
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = ip;
            var result = new List<Instruction>();
            ulong end = ip + (ulong)bytes.Length;
            while (decoder.IP < end)
            {
                decoder.Decode(out Instruction instruction);
                if (instruction.Code == Code.INVALID || decoder.IP > end)
                    throw new InvalidOperationException("Cursor PCL block is not instruction aligned.");
                result.Add(instruction);
            }
            if (decoder.IP != end)
                throw new InvalidOperationException("Cursor PCL block was not consumed exactly.");
            return result.ToArray();
        }

        private static void ValidateOriginal(ReadOnlySpan<Instruction> original)
        {
            if (original.Length != 3 ||
                original[0].Mnemonic != Mnemonic.Call ||
                original[1].Mnemonic != Mnemonic.Test ||
                original[1].Op0Register != Register.EAX ||
                original[1].Op1Register != Register.EAX ||
                original[2].Mnemonic != Mnemonic.Lea ||
                original[2].Op0Register != Register.RDI ||
                original[0].NearBranchTarget != original[0].IP -
                    (ulong)EnemyGatePathfindingNativeDefinition.CursorPclDecisionRva +
                    (ulong)EnemyGatePathfindingNativeDefinition.PclReachabilityRva ||
                original[2].IPRelativeMemoryAddress != original[0].IP -
                    (ulong)EnemyGatePathfindingNativeDefinition.CursorPclDecisionRva +
                    (ulong)EnemyGatePathfindingNativeDefinition.NativeDirectionGridRva)
                throw new InvalidOperationException(
                    "Normal cursor PCL call, TEST, or grid-base LEA differs from baseline.");
        }

        private static void ValidateStub(
            byte[] bytes,
            ulong ip,
            ulong wrapperAddress,
            ulong expectedLeaTarget)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = ip;
            int stackDelta = 0;
            int wrapperPointers = 0, wrapperCalls = 0, tests = 0, leas = 0;
            int modeCopies = 0, unitCopies = 0;
            ulong end = ip + (ulong)bytes.Length;
            while (decoder.IP < end)
            {
                decoder.Decode(out Instruction instruction);
                if (instruction.Code == Code.INVALID || decoder.IP > end)
                    throw new InvalidOperationException("Cursor PCL adapter contains invalid code.");
                if (instruction.Mnemonic == Mnemonic.Sub &&
                    instruction.Op0Register == Register.RSP)
                    stackDelta += unchecked((int)instruction.Immediate32);
                if (instruction.Mnemonic == Mnemonic.Add &&
                    instruction.Op0Register == Register.RSP)
                    stackDelta -= unchecked((int)instruction.Immediate32);
                if (instruction.Mnemonic == Mnemonic.Mov &&
                    instruction.Op0Register == Register.R11 &&
                    instruction.Immediate64 == wrapperAddress) wrapperPointers++;
                if (instruction.Mnemonic == Mnemonic.Call &&
                    instruction.Op0Kind == OpKind.Register &&
                    instruction.Op0Register == Register.R11) wrapperCalls++;
                if (instruction.Mnemonic == Mnemonic.Call &&
                    instruction.Op0Kind == OpKind.NearBranch64)
                    throw new InvalidOperationException(
                        "Cursor PCL adapter retained the original direct call.");
                if (instruction.Mnemonic == Mnemonic.Test &&
                    instruction.Op0Register == Register.EAX &&
                    instruction.Op1Register == Register.EAX) tests++;
                if (instruction.Mnemonic == Mnemonic.Lea &&
                    instruction.Op0Register == Register.RDI &&
                    instruction.IPRelativeMemoryAddress == expectedLeaTarget) leas++;
                if (instruction.Mnemonic == Mnemonic.Mov &&
                    instruction.Op0Kind == OpKind.Memory &&
                    instruction.MemoryBase == Register.RSP &&
                    instruction.MemoryDisplacement64 == 0x20 &&
                    instruction.Op1Register == Register.R10D) modeCopies++;
                if (instruction.Mnemonic == Mnemonic.Mov &&
                    instruction.Op0Kind == OpKind.Memory &&
                    instruction.MemoryBase == Register.RSP &&
                    instruction.MemoryDisplacement64 == 0x28 &&
                    instruction.Op1Register == Register.R14D) unitCopies++;
            }
            if (stackDelta != 0 || wrapperPointers != 1 || wrapperCalls != 1 ||
                tests != 1 || leas != 1 || modeCopies != 1 || unitCopies != 1)
                throw new InvalidOperationException(
                    "Cursor PCL adapter violated its stack, ABI, or replay contract.");
        }
    }
}
