using Iced.Intel;
using System;
using System.Collections.Generic;
using System.IO;
using static Iced.Intel.AssemblerRegisters;

namespace EnemyGatePathfindingTest
{
    // Native-only edge filter for Vanilla's three tactical target floods. The
    // managed detour binds one immutable player mask per complete 113BC0 query;
    // these adapters perform no calls, allocation, logging or global atomics.
    internal static class AiTacticalTargetAdapterEmitter
    {
        internal const int ThreadSlotCount = 64;
        internal const int ThreadSlotStrideShift = 7;
        internal const int ThreadSlotStride = 1 << ThreadSlotStrideShift;
        internal const int SlotOwnerOffset = 0;
        internal const int SlotMaskOffset = 8;
        internal const int SlotBuildingTouchedOffset = 16;
        internal const int SlotUnitTouchedOffset = 24;
        internal const int SlotFallbackTouchedOffset = 32;
        internal const int SlotDepthOffset = 40;
        internal const int SlotBuildingSourceOffset = 48;
        internal const int SlotBuildingTargetOffset = 52;
        internal const int SlotBuildingDirectionOffset = 56;
        internal const int SlotUnitSourceOffset = 60;
        internal const int SlotUnitTargetOffset = 64;
        internal const int SlotUnitDirectionOffset = 68;
        internal const int SlotFallbackSourceOffset = 72;
        internal const int SlotFallbackTargetOffset = 76;
        internal const int SlotFallbackDirectionOffset = 80;

        internal static Register GetSourceRegister(int site)
        {
            switch (site)
            {
                case 0: return Register.R14;
                case 1: return Register.R11;
                case 2: return Register.RDI;
                default: throw new ArgumentOutOfRangeException(nameof(site));
            }
        }

        internal static Register GetTargetRegister(int site)
        {
            switch (site)
            {
                case 0: return Register.R8;
                case 1: return Register.R10;
                case 2: return Register.None;
                default: throw new ArgumentOutOfRangeException(nameof(site));
            }
        }

        internal static Register GetDirectionRegister(int site)
        {
            switch (site)
            {
                case 0: return Register.RSI;
                case 1: return Register.R9;
                case 2: return Register.AL;
                default: throw new ArgumentOutOfRangeException(nameof(site));
            }
        }

        internal static bool IsTileIndexInRange(long tileIndex) =>
            unchecked((ulong)tileIndex) <
            EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive;

        internal static string DescribeContracts()
        {
            var values = new string[EnemyGatePathfindingNativeDefinition.AiTacticalFilterRvas.Length];
            for (int site = 0; site < values.Length; site++)
                values[site] = $"0x{EnemyGatePathfindingNativeDefinition.AiTacticalFilterRvas[site]:X}:" +
                    $"source={GetSourceRegister(site)}/target={GetTargetRegister(site)}/" +
                    $"direction={GetDirectionRegister(site)}/displaced=" +
                    EnemyGatePathfindingNativeDefinition.AiTacticalFilterLengths[site] +
                    $"/reject=0x{EnemyGatePathfindingNativeDefinition.AiTacticalRejectRvas[site]:X}";
            return string.Join(";", values);
        }

        internal static void Emit(Assembler assembler, ReadOnlySpan<Instruction> original,
            int site, ulong slots, ulong rejectTarget)
        {
            if (assembler == null) throw new ArgumentNullException(nameof(assembler));
            if ((uint)site >= (uint)EnemyGatePathfindingNativeDefinition.AiTacticalFilterRvas.Length)
                throw new ArgumentOutOfRangeException(nameof(site));
            if (original.Length < 2)
                throw new InvalidOperationException("AI tactical-filter span is incomplete.");
            if (site == 0) EmitBuilding(assembler, original, slots, rejectTarget);
            else if (site == 1) EmitUnit(assembler, original, slots, rejectTarget);
            else EmitFallback(assembler, original, slots);
        }

        internal static byte[] AssembleAndValidate(byte[] originalBytes, ulong originalIp,
            int site, ulong slots, ulong rejectTarget, ulong stubIp)
        {
            Instruction[] original = DecodeExact(originalBytes, originalIp);
            var assembler = new Assembler(64);
            Emit(assembler, original, site, slots, rejectTarget);
            using (var stream = new MemoryStream())
            {
                assembler.Assemble(new StreamCodeWriter(stream), stubIp);
                byte[] encoded = stream.ToArray();
                ValidateDecodedStub(encoded, stubIp, site, rejectTarget);
                return encoded;
            }
        }

        private static void EmitBuilding(Assembler a, ReadOnlySpan<Instruction> original,
            ulong slots, ulong rejectTarget)
        {
            for (int index = 0; index < original.Length; index++) a.AddInstruction(original[index]);
            Label allowed = a.CreateLabel();
            Label first = a.CreateLabel();
            Label rejected = a.CreateLabel();
            a.push(rax); a.push(rcx); a.push(rdx); a.push(r9);
            EmitSlotLookup(a, slots, ref allowed);
            a.cmp(r14d, EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive); a.jae(allowed);
            a.cmp(r8d, EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive); a.jae(allowed);
            a.mov(rax, __qword_ptr[rdx + SlotMaskOffset]); a.test(rax, rax); a.je(allowed);
            a.mov(ecx, esi); a.and(ecx, 7); a.mov(r9d, 1); a.shl(r9b, cl);
            a.test(__byte_ptr[rax + r14], r9b); a.jne(allowed);
            a.cmp(__qword_ptr[rdx + SlotBuildingTouchedOffset], 0); a.je(first); a.jmp(rejected);
            a.Label(ref first);
            a.mov(__dword_ptr[rdx + SlotBuildingSourceOffset], r14d);
            a.mov(__dword_ptr[rdx + SlotBuildingTargetOffset], r8d);
            a.mov(__dword_ptr[rdx + SlotBuildingDirectionOffset], r9d);
            a.Label(ref rejected);
            a.inc(__qword_ptr[rdx + SlotBuildingTouchedOffset]);
            a.pop(r9); a.pop(rdx); a.pop(rcx); a.pop(rax); a.jmp(rejectTarget);
            a.Label(ref allowed);
            a.pop(r9); a.pop(rdx); a.pop(rcx); a.pop(rax);
        }

        private static void EmitUnit(Assembler a, ReadOnlySpan<Instruction> original,
            ulong slots, ulong rejectTarget)
        {
            for (int index = 0; index < original.Length; index++) a.AddInstruction(original[index]);
            Label allowed = a.CreateLabel();
            Label first = a.CreateLabel();
            Label rejected = a.CreateLabel();
            a.push(rax); a.push(rcx); a.push(rdx); a.push(r8);
            EmitSlotLookup(a, slots, ref allowed);
            a.cmp(r11d, EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive); a.jae(allowed);
            a.cmp(r10d, EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive); a.jae(allowed);
            a.mov(rax, __qword_ptr[rdx + SlotMaskOffset]); a.test(rax, rax); a.je(allowed);
            a.mov(ecx, r9d); a.and(ecx, 7); a.mov(r8d, 1); a.shl(r8b, cl);
            a.test(__byte_ptr[rax + r11], r8b); a.jne(allowed);
            a.cmp(__qword_ptr[rdx + SlotUnitTouchedOffset], 0); a.je(first); a.jmp(rejected);
            a.Label(ref first);
            a.mov(__dword_ptr[rdx + SlotUnitSourceOffset], r11d);
            a.mov(__dword_ptr[rdx + SlotUnitTargetOffset], r10d);
            a.mov(__dword_ptr[rdx + SlotUnitDirectionOffset], r8d);
            a.Label(ref rejected);
            a.inc(__qword_ptr[rdx + SlotUnitTouchedOffset]);
            a.pop(r8); a.pop(rdx); a.pop(rcx); a.pop(rax); a.jmp(rejectTarget);
            a.Label(ref allowed);
            a.pop(r8); a.pop(rdx); a.pop(rcx); a.pop(rax);
        }

        private static void EmitFallback(Assembler a, ReadOnlySpan<Instruction> original, ulong slots)
        {
            a.AddInstruction(original[0]);
            Label unfiltered = a.CreateLabel();
            Label first = a.CreateLabel();
            Label counted = a.CreateLabel();
            a.push(rax); a.push(rcx); a.push(rdx); a.push(r8); a.push(r9);
            EmitSlotLookup(a, slots, ref unfiltered);
            a.cmp(edi, EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive); a.jae(unfiltered);
            a.mov(r9, __qword_ptr[rdx + SlotMaskOffset]); a.test(r9, r9); a.je(unfiltered);
            a.mov(r8b, __byte_ptr[rsp + 32]);
            a.and(r8b, __byte_ptr[r9 + rdi]);
            a.cmp(r8b, __byte_ptr[rsp + 32]);
            a.mov(__byte_ptr[rsp + 32], r8b);
            a.je(unfiltered);
            a.cmp(__qword_ptr[rdx + SlotFallbackTouchedOffset], 0); a.je(first); a.jmp(counted);
            a.Label(ref first);
            a.mov(__dword_ptr[rdx + SlotFallbackSourceOffset], edi);
            a.mov(__dword_ptr[rdx + SlotFallbackTargetOffset], -1);
            a.movzx(ecx, r8b);
            a.mov(__dword_ptr[rdx + SlotFallbackDirectionOffset], ecx);
            a.Label(ref counted);
            a.inc(__qword_ptr[rdx + SlotFallbackTouchedOffset]);
            a.Label(ref unfiltered);
            a.pop(r9); a.pop(r8); a.pop(rdx); a.pop(rcx); a.pop(rax);
            for (int index = 1; index < original.Length; index++) a.AddInstruction(original[index]);
        }

        private static void EmitSlotLookup(Assembler a, ulong slots, ref Label failOpen)
        {
            a.mov(edx, __dword_ptr.gs[0x48]);
            a.and(edx, ThreadSlotCount - 1);
            a.shl(rdx, ThreadSlotStrideShift);
            a.mov(rax, slots);
            a.add(rdx, rax);
            a.mov(eax, __dword_ptr.gs[0x48]);
            a.cmp(__dword_ptr[rdx + SlotOwnerOffset], eax);
            a.jne(failOpen);
        }

        private static Instruction[] DecodeExact(byte[] bytes, ulong ip)
        {
            if (bytes == null || bytes.Length == 0)
                throw new ArgumentException("Original AI tactical-filter bytes are required.", nameof(bytes));
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes)); decoder.IP = ip;
            var result = new List<Instruction>(); ulong end = ip + (ulong)bytes.Length;
            while (decoder.IP < end)
            {
                decoder.Decode(out Instruction instruction);
                if (instruction.Code == Code.INVALID || decoder.IP > end)
                    throw new InvalidOperationException("AI tactical-filter bytes do not end on an instruction boundary.");
                result.Add(instruction);
            }
            if (decoder.IP != end) throw new InvalidOperationException("AI tactical-filter decoder did not consume the exact span.");
            return result.ToArray();
        }

        private static void ValidateDecodedStub(byte[] encoded, ulong stubIp, int site, ulong rejectTarget)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(encoded)); decoder.IP = stubIp;
            ulong end = stubIp + (ulong)encoded.Length; int bounds = 0, policyReads = 0, rejects = 0;
            while (decoder.IP < end)
            {
                decoder.Decode(out Instruction instruction);
                if (instruction.Code == Code.INVALID || decoder.IP > end)
                    throw new InvalidOperationException("Emitted AI tactical-filter stub contains an invalid instruction.");
                if (instruction.Mnemonic == Mnemonic.Cmp && instruction.Op1Kind == OpKind.Immediate32 &&
                    instruction.Immediate32 == EnemyGatePathfindingNativeDefinition.MaximumTileIdExclusive)
                    bounds++;
                if ((site == 2 && instruction.Mnemonic == Mnemonic.And &&
                     instruction.Op1Kind == OpKind.Memory) ||
                    (site != 2 && instruction.Mnemonic == Mnemonic.Test &&
                     instruction.Op0Kind == OpKind.Memory))
                    policyReads++;
                if (instruction.Mnemonic == Mnemonic.Jmp && instruction.NearBranchTarget == rejectTarget)
                    rejects++;
                if (site == 2 && instruction.Mnemonic == Mnemonic.Je &&
                    instruction.NearBranchTarget == rejectTarget) rejects++;
            }
            int expectedBounds = site == 2 ? 1 : 2;
            if (bounds != expectedBounds || policyReads != 1 || rejects != 1)
                throw new InvalidOperationException($"AI tactical-filter stub {site} failed validation: " +
                    $"bounds={bounds}/{expectedBounds}, policyReads={policyReads}/1, rejects={rejects}/1.");
        }
    }
}
