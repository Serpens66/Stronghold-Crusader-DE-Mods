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
        internal const int SlotDepthOffset = 24;

        internal static Register GetTileRegister(int site)
        {
            switch (site)
            {
                case 0: return Register.R8;
                case 1:
                case 2:
                case 6:
                case 7:
                case 8:
                case 9: return Register.RDI;
                case 3: return Register.R10;
                case 4: return Register.RAX;
                case 5: return Register.R8;
                case 10: return Register.R12;
                default: throw new ArgumentOutOfRangeException(nameof(site));
            }
        }

        internal static bool IsTileIndexInRange(long tileIndex) =>
            unchecked((ulong)tileIndex) <
            EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive;

        internal static string DescribeContracts()
        {
            var descriptions = new string[EnemyGatePathfindingNativeDefinition.DirectionFilterRvas.Length];
            for (int site = 0; site < descriptions.Length; site++)
            {
                descriptions[site] = $"0x{EnemyGatePathfindingNativeDefinition.DirectionFilterRvas[site]:X}:" +
                    $"tile={GetTileRegister(site)}/displaced=" +
                    EnemyGatePathfindingNativeDefinition.DirectionFilterLengths[site];
            }
            return string.Join(";", descriptions);
        }

        internal static void Emit(
            Assembler assembler,
            ReadOnlySpan<Instruction> original,
            int site,
            ulong slots)
        {
            if (assembler == null) throw new ArgumentNullException(nameof(assembler));
            if (original.Length < 2) throw new InvalidOperationException("Direction-filter span is incomplete.");
            if ((uint)site >= (uint)EnemyGatePathfindingNativeDefinition.DirectionFilterRvas.Length)
                throw new ArgumentOutOfRangeException(nameof(site));

            if (site == 1 || site == 2)
                EmitR11Destination(assembler, original, slots);
            else if (site == 4 || site == 5)
                EmitRcxDestination(assembler, original, slots, site == 4);
            else if (site == 10)
                EmitRcxR12Destination(assembler, original, slots);
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
                ValidateDecodedStub(encoded, stubIp, original,
                    originalIp + (ulong)originalBytes.Length, site);
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
            ulong originalEnd,
            int site)
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
            Register expectedMask = GetMaskRegister(site);
            Register expectedTile = GetTileRegister(site);
            Register expectedTile32 = ToRegister32(expectedTile);
            Register expectedIndex = GetZeroExtendedIndexRegister(site);
            Register expectedIndex32 = ToRegister32(expectedIndex);
            int tileAddressAdds = 0;
            int tileIndexCopies = 0;
            int rangeGuards = 0;
            bool rangeGuardNeedsBranch = false;
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(encoded));
            decoder.IP = stubIp;
            ulong end = stubIp + (ulong)encoded.Length;
            while (decoder.IP < end)
            {
                decoder.Decode(out Instruction instruction);
                if (instruction.Code == Code.INVALID || decoder.IP > end)
                    throw new InvalidOperationException("Emitted direction-filter stub contains an invalid instruction.");
                if (rangeGuardNeedsBranch)
                {
                    if (instruction.Mnemonic != Mnemonic.Jae)
                        throw new InvalidOperationException(
                            $"Direction-filter stub {site} does not fail open after its tile-range guard.");
                    rangeGuardNeedsBranch = false;
                }
                if (instruction.Mnemonic == Mnemonic.Cmp &&
                    instruction.Op0Kind == OpKind.Register &&
                    instruction.Op0Register == expectedTile32 &&
                    instruction.Op1Kind == OpKind.Immediate32 &&
                    instruction.Immediate32 ==
                        EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive)
                {
                    rangeGuards++;
                    rangeGuardNeedsBranch = true;
                }
                if (instruction.Mnemonic == Mnemonic.Add &&
                    instruction.Op0Kind == OpKind.Register &&
                    instruction.Op0Register == expectedMask &&
                    instruction.Op1Kind == OpKind.Register)
                {
                    if (instruction.Op1Register != expectedIndex)
                        throw new InvalidOperationException(
                            $"Direction-filter stub {site} adds {instruction.Op1Register} instead of " +
                            $"the zero-extended tile index {expectedIndex} to its policy mask.");
                    tileAddressAdds++;
                }
                if (instruction.Mnemonic == Mnemonic.Mov &&
                    instruction.Op0Kind == OpKind.Register &&
                    instruction.Op0Register == expectedIndex32 &&
                    instruction.Op1Kind == OpKind.Register &&
                    instruction.Op1Register == expectedTile32)
                    tileIndexCopies++;
                if ((instruction.FlowControl == FlowControl.ConditionalBranch ||
                     instruction.FlowControl == FlowControl.UnconditionalBranch) &&
                    requiredExternalTargets.Contains(instruction.NearBranchTarget))
                    observedExternalTargets.Add(instruction.NearBranchTarget);
            }
            if (!requiredExternalTargets.SetEquals(observedExternalTargets))
                throw new InvalidOperationException("Emitted direction-filter stub changed an external Vanilla branch target.");
            if (rangeGuardNeedsBranch || rangeGuards != 1 || tileIndexCopies != 1 ||
                tileAddressAdds != 1)
                throw new InvalidOperationException(
                    $"Direction-filter stub {site} has {rangeGuards} range guards and " +
                    $"{tileIndexCopies} zero-extension copies and {tileAddressAdds} audited " +
                    "tile-address additions; expected one of each.");
        }

        private static Register GetMaskRegister(int site) => site == 1 || site == 2
            ? Register.RAX
            : site == 4 || site == 5 || site == 10 ? Register.R10 : Register.R9;

        internal static Register GetZeroExtendedIndexRegister(int site) => site == 1 || site == 2
            ? Register.R11
            : site == 4 || site == 5 ? Register.R9 : Register.RAX;

        private static Register ToRegister32(Register register)
        {
            switch (register)
            {
                case Register.RAX: return Register.EAX;
                case Register.R8: return Register.R8D;
                case Register.R9: return Register.R9D;
                case Register.R10: return Register.R10D;
                case Register.R11: return Register.R11D;
                case Register.R12: return Register.R12D;
                case Register.RDI: return Register.EDI;
                default: throw new ArgumentOutOfRangeException(nameof(register));
            }
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
            if (site == 0)
            {
                assembler.cmp(r8d, EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive);
                assembler.jae(noContext);
                assembler.mov(eax, r8d);
                assembler.add(r9, rax);
            }
            else if (site == 3)
            {
                assembler.cmp(r10d, EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive);
                assembler.jae(noContext);
                assembler.mov(eax, r10d);
                assembler.add(r9, rax);
            }
            else
            {
                assembler.cmp(edi, EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive);
                assembler.jae(noContext);
                assembler.mov(eax, edi);
                assembler.add(r9, rax);
            }
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
            assembler.cmp(edi, EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive);
            assembler.jae(noContext);
            assembler.mov(r11d, edi);
            assembler.add(rax, r11);
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
            if (tileInRax)
            {
                assembler.cmp(eax, EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive);
                assembler.jae(noContext);
                assembler.mov(r9d, eax);
                assembler.add(r10, r9);
            }
            else
            {
                assembler.cmp(r8d, EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive);
                assembler.jae(noContext);
                assembler.mov(r9d, r8d);
                assembler.add(r10, r9);
            }
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

        private static void EmitRcxR12Destination(
            Assembler assembler,
            ReadOnlySpan<Instruction> original,
            ulong slots)
        {
            Label originalLoad = assembler.CreateLabel();
            Label noContext = assembler.CreateLabel();
            Label unfiltered = assembler.CreateLabel();
            Label unchanged = assembler.CreateLabel();

            // R9 is the native module base in DC3C0 and must remain untouched until
            // its original load has executed. RAX is dead before DC560 and is saved.
            assembler.push(rax);
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
            assembler.cmp(r12d, EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive);
            assembler.jae(noContext);
            assembler.mov(eax, r12d);
            assembler.add(r10, rax);
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
            assembler.pop(rax);
            for (int index = 1; index < original.Length; index++)
                assembler.AddInstruction(original[index]);
        }
    }
}
