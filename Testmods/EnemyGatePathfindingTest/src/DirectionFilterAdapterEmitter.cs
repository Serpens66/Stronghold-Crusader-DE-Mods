using Iced.Intel;
using System;
using System.Collections.Generic;
using System.IO;
using static Iced.Intel.AssemblerRegisters;

namespace EnemyGatePathfindingTest
{
    // Pure native adapter emitter shared by runtime and machine-contract tests.
    // Every decoded Vanilla instruction is appended exactly once; inactive contexts
    // join the same original load/continuation path as active contexts.
    internal static class DirectionFilterAdapterEmitter
    {
        internal const int ThreadSlotCount = 64;
        internal const int ThreadSlotStrideShift = 5;
        internal const int SlotOwnerOffset = 0;
        internal const int SlotMaskOffset = 8;
        internal const int SlotTouchedOffset = 16;

        internal static void Emit(
            Assembler assembler,
            ReadOnlySpan<Instruction> original,
            int site,
            ulong slots)
        {
            if (assembler == null) throw new ArgumentNullException(nameof(assembler));
            if (original.Length < 2) throw new InvalidOperationException("Direction-filter span is incomplete.");
            if ((uint)site >= 10) throw new ArgumentOutOfRangeException(nameof(site));

            if (site == 1 || site == 2)
                EmitR11Destination(assembler, original, slots);
            else if (site == 4 || site == 5)
                EmitRcxDestination(assembler, original, slots, site == 4);
            else
                EmitRaxDestination(assembler, original, slots, site);
        }

        internal static byte[] AssembleAndValidate(
            byte[] originalBytes,
            ulong originalIp,
            int site,
            ulong slots,
            ulong stubIp)
        {
            if (originalBytes == null || originalBytes.Length == 0)
                throw new ArgumentException("Original direction-filter bytes are required.", nameof(originalBytes));

            Instruction[] original = DecodeExact(originalBytes, originalIp);
            var assembler = new Assembler(64);
            Emit(assembler, original, site, slots);
            using (var stream = new MemoryStream())
            {
                assembler.Assemble(new StreamCodeWriter(stream), stubIp);
                byte[] encoded = stream.ToArray();
                ValidateDecodedStub(encoded, stubIp, original, originalIp + (ulong)originalBytes.Length);
                return encoded;
            }
        }

        private static Instruction[] DecodeExact(byte[] bytes, ulong ip)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = ip;
            var result = new List<Instruction>();
            ulong end = ip + (ulong)bytes.Length;
            while (decoder.IP < end)
            {
                decoder.Decode(out Instruction instruction);
                if (instruction.Code == Code.INVALID || decoder.IP > end)
                    throw new InvalidOperationException("Direction-filter bytes do not end on an instruction boundary.");
                result.Add(instruction);
            }
            if (decoder.IP != end || result.Count < 2)
                throw new InvalidOperationException("Direction-filter decoder did not consume the exact span.");
            return result.ToArray();
        }

        private static void ValidateDecodedStub(
            byte[] encoded,
            ulong stubIp,
            Instruction[] original,
            ulong originalEnd)
        {
            var requiredExternalTargets = new HashSet<ulong>();
            for (int index = 0; index < original.Length; index++)
            {
                Instruction instruction = original[index];
                if ((instruction.FlowControl == FlowControl.ConditionalBranch ||
                     instruction.FlowControl == FlowControl.UnconditionalBranch) &&
                    (instruction.NearBranchTarget < original[0].IP || instruction.NearBranchTarget >= originalEnd))
                    requiredExternalTargets.Add(instruction.NearBranchTarget);
            }

            var observedExternalTargets = new HashSet<ulong>();
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(encoded));
            decoder.IP = stubIp;
            ulong end = stubIp + (ulong)encoded.Length;
            while (decoder.IP < end)
            {
                decoder.Decode(out Instruction instruction);
                if (instruction.Code == Code.INVALID || decoder.IP > end)
                    throw new InvalidOperationException("Emitted direction-filter stub contains an invalid instruction.");
                if ((instruction.FlowControl == FlowControl.ConditionalBranch ||
                     instruction.FlowControl == FlowControl.UnconditionalBranch) &&
                    requiredExternalTargets.Contains(instruction.NearBranchTarget))
                    observedExternalTargets.Add(instruction.NearBranchTarget);
            }
            if (!requiredExternalTargets.SetEquals(observedExternalTargets))
                throw new InvalidOperationException("Emitted direction-filter stub changed an external Vanilla branch target.");
        }

        private static void EmitRaxDestination(
            Assembler assembler,
            ReadOnlySpan<Instruction> original,
            ulong slots,
            int site)
        {
            Label originalLoad = assembler.CreateLabel();
            Label noContext = assembler.CreateLabel();
            Label unfiltered = assembler.CreateLabel();
            Label unchanged = assembler.CreateLabel();

            assembler.push(r9);
            assembler.push(r11);
            assembler.mov(r11d, __dword_ptr.gs[0x48]);
            assembler.and(r11d, ThreadSlotCount - 1);
            assembler.shl(r11, ThreadSlotStrideShift);
            assembler.mov(r9, slots);
            assembler.add(r11, r9);
            assembler.mov(r9d, __dword_ptr.gs[0x48]);
            assembler.cmp(__dword_ptr[r11 + SlotOwnerOffset], r9d);
            assembler.jne(noContext);
            assembler.mov(r9, __qword_ptr[r11 + SlotMaskOffset]);
            assembler.test(r9, r9);
            assembler.je(originalLoad);
            if (site == 0) assembler.add(r9, rdx);
            else if (site == 3) assembler.add(r9, rbx);
            else assembler.add(r9, rdi);
            assembler.jmp(originalLoad);

            assembler.Label(ref noContext);
            assembler.xor(r9d, r9d); // Null selects the unfiltered shared path.

            assembler.Label(ref originalLoad);
            assembler.AddInstruction(original[0]);
            assembler.test(r9, r9);
            assembler.je(unfiltered);
            assembler.push(rax);
            assembler.and(al, __byte_ptr[r9]);
            assembler.cmp(al, __byte_ptr[rsp]);
            assembler.je(unchanged);
            assembler.inc(__qword_ptr[r11 + SlotTouchedOffset]);
            assembler.Label(ref unchanged);
            assembler.add(rsp, 8);
            assembler.Label(ref unfiltered);
            assembler.pop(r11);
            assembler.pop(r9);
            for (int index = 1; index < original.Length; index++)
                assembler.AddInstruction(original[index]);
        }

        private static void EmitR11Destination(
            Assembler assembler,
            ReadOnlySpan<Instruction> original,
            ulong slots)
        {
            Label originalLoad = assembler.CreateLabel();
            Label noContext = assembler.CreateLabel();
            Label unfiltered = assembler.CreateLabel();
            Label unchanged = assembler.CreateLabel();

            assembler.push(rax);
            assembler.push(r9);
            assembler.push(r10);
            assembler.mov(r10d, __dword_ptr.gs[0x48]);
            assembler.and(r10d, ThreadSlotCount - 1);
            assembler.shl(r10, ThreadSlotStrideShift);
            assembler.mov(rax, slots);
            assembler.add(r10, rax);
            assembler.mov(eax, __dword_ptr.gs[0x48]);
            assembler.cmp(__dword_ptr[r10 + SlotOwnerOffset], eax);
            assembler.jne(noContext);
            assembler.mov(rax, __qword_ptr[r10 + SlotMaskOffset]);
            assembler.test(rax, rax);
            assembler.je(originalLoad);
            assembler.add(rax, rdi);
            assembler.jmp(originalLoad);

            assembler.Label(ref noContext);
            assembler.xor(eax, eax);

            assembler.Label(ref originalLoad);
            assembler.AddInstruction(original[0]);
            assembler.test(rax, rax);
            assembler.je(unfiltered);
            assembler.push(r11);
            assembler.and(r11b, __byte_ptr[rax]);
            assembler.cmp(r11b, __byte_ptr[rsp]);
            assembler.je(unchanged);
            assembler.inc(__qword_ptr[r10 + SlotTouchedOffset]);
            assembler.Label(ref unchanged);
            assembler.add(rsp, 8);
            assembler.Label(ref unfiltered);
            assembler.pop(r10);
            assembler.pop(r9);
            assembler.pop(rax);
            for (int index = 1; index < original.Length; index++)
                assembler.AddInstruction(original[index]);
        }

        private static void EmitRcxDestination(
            Assembler assembler,
            ReadOnlySpan<Instruction> original,
            ulong slots,
            bool tileInRax)
        {
            Label originalLoad = assembler.CreateLabel();
            Label noContext = assembler.CreateLabel();
            Label unfiltered = assembler.CreateLabel();
            Label unchanged = assembler.CreateLabel();

            assembler.push(r9);
            assembler.push(r10);
            assembler.push(r11);
            assembler.mov(r11d, __dword_ptr.gs[0x48]);
            assembler.and(r11d, ThreadSlotCount - 1);
            assembler.shl(r11, ThreadSlotStrideShift);
            assembler.mov(r10, slots);
            assembler.add(r11, r10);
            assembler.mov(r10d, __dword_ptr.gs[0x48]);
            assembler.cmp(__dword_ptr[r11 + SlotOwnerOffset], r10d);
            assembler.jne(noContext);
            assembler.mov(r10, __qword_ptr[r11 + SlotMaskOffset]);
            assembler.test(r10, r10);
            assembler.je(originalLoad);
            if (tileInRax) assembler.add(r10, rax);
            else assembler.add(r10, rcx);
            assembler.jmp(originalLoad);

            assembler.Label(ref noContext);
            assembler.xor(r10d, r10d);

            assembler.Label(ref originalLoad);
            assembler.AddInstruction(original[0]);
            assembler.test(r10, r10);
            assembler.je(unfiltered);
            assembler.push(rcx);
            assembler.and(cl, __byte_ptr[r10]);
            assembler.cmp(cl, __byte_ptr[rsp]);
            assembler.je(unchanged);
            assembler.inc(__qword_ptr[r11 + SlotTouchedOffset]);
            assembler.Label(ref unchanged);
            assembler.add(rsp, 8);
            assembler.Label(ref unfiltered);
            assembler.pop(r11);
            assembler.pop(r10);
            assembler.pop(r9);
            for (int index = 1; index < original.Length; index++)
                assembler.AddInstruction(original[index]);
        }
    }
}
