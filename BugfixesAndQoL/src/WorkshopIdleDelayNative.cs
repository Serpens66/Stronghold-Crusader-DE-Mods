using Iced.Intel;
using RedBird.X64.Extensions;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL
{
    // Manager-relative offsets, including the native reserved slot. IDs remain 1-based.
    internal static class WorkshopIdleDelayNative
    {
        internal const string NativeSha256 = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const int UnitManagerRva = 0x67E8400;
        internal const int BuildingManagerRva = 0x64CCBB0;
        internal const int UnitStride = GameUnitManager.UnitRecordByteSize;
        internal const int UnitSlots = GameUnitManager.NativeUnitSlotCount;
        // Native allocation bound; the Extender's NUM_PREALLOC_BUILDINGS is internal.
        internal const int BuildingSlots = 4000;
        internal const int BuildingStride = 0x32C;
        internal const int PoleRva = 0x13AC85, PoleExitRva = 0x13BDAF;
        internal const int TannerRva = 0x13E39F, TannerExitRva = 0x13E6C2;
        internal const int DisplacedLength = 17;
        internal static readonly byte[] PoleBytes = { 0x85,0xFF,0x0F,0x84,0x22,0x11,0,0,0x42,0x0F,0xB7,0x84,0x23,0xD8,0x09,0,0 };
        internal static readonly byte[] TannerBytes = { 0x85,0xED,0x0F,0x84,0x1B,0x03,0,0,0x42,0x0F,0xB7,0x84,0x23,0xD8,0x09,0,0 };

        internal static Instruction[] Decode(byte[] bytes, ulong address)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = address;
            return new[] { decoder.Decode(), decoder.Decode(), decoder.Decode() };
        }

        internal static void ValidateInstructions(ReadOnlySpan<Instruction> instructions, ulong exit, bool tanner)
        {
            Register completion = tanner ? Register.EBP : Register.EDI;
            if (instructions.Length != 3 || instructions[0].Mnemonic != Mnemonic.Test ||
                instructions[0].Op0Register != completion || instructions[0].Op1Register != completion ||
                instructions[1].Mnemonic != Mnemonic.Je || instructions[1].NearBranchTarget != exit ||
                instructions[2].Mnemonic != Mnemonic.Movzx ||
                instructions[2].Op0Register != Register.EAX || instructions[2].Op1Kind != OpKind.Memory ||
                instructions[2].MemoryBase != Register.RBX || instructions[2].MemoryIndex != Register.R12 ||
                instructions[2].MemoryIndexScale != 1 || instructions[2].MemoryDisplacement64 != 0x9D8 ||
                instructions[0].Length + instructions[1].Length + instructions[2].Length != DisplacedLength)
                throw new InvalidOperationException("Workshop idle hook displacement/branch contract changed.");
        }

        internal static void Emit(Assembler a, ReadOnlySpan<Instruction> original,
            ulong enabledFlag, ulong units, ulong buildings, bool tanner)
        {
            var replay = original.CloneInstructionsWithoutIP();
            Label vanilla = a.CreateLabel(), ready = a.CreateLabel(), done = a.CreateLabel();
            Label scan = a.CreateLabel(), next = a.CreateLabel();
            // No calls, SIMD or RedBird Context wrapper: preserve all scratch registers and flags.
            a.pushfq();
            a.push(rax); a.push(rcx); a.push(rdx); a.push(r8); a.push(r9); a.push(r10); a.push(r11);
            a.mov(rax, enabledFlag); a.cmp(__dword_ptr[rax], 0); a.je(vanilla);
            // A completed animation already follows Vanilla's normal path without any extra scan.
            if (tanner) a.test(ebp, ebp); else a.test(edi, edi);
            a.jne(vanilla);
            a.mov(rax, units); a.cmp(r12, rax); a.jne(vanilla);
            a.cmp(__dword_ptr[r12], 1); a.jle(vanilla);
            a.cmp(__dword_ptr[r12], UnitSlots); a.jg(vanilla);
            a.mov(rcx, rbx); a.xor(edx, edx); a.mov(r8, (ulong)UnitStride); a.mov(rax, rcx);
            a.div(r8); a.test(rdx, rdx); a.jne(vanilla);
            a.test(rax, rax); a.je(vanilla); a.cmp(rax, UnitSlots); a.jae(vanilla);
            a.cmp(eax, __dword_ptr[r12]); a.jae(vanilla);
            a.lea(r10, __[rbx + r12]);
            a.cmp(__word_ptr[r10 + 0x6E4], (int)AliveState.IsAlive); a.jne(vanilla);
            // IsReallyAlive: manager-relative low-word death marker (GameUnit+0x29C).
            a.cmp(__word_ptr[r10 + 0x8F8], 0); a.jne(vanilla);
            a.cmp(__word_ptr[r10 + 0x6E6], (int)(tanner ? eChimps.CHIMP_TYPE_TANNER : eChimps.CHIMP_TYPE_POLETURNER)); a.jne(vanilla);
            // State 1 is also used during entertainment. The helper can return zero mid-animation.
            a.cmp(__word_ptr[r10 + 0x918], 1); a.jne(vanilla);
            a.cmp(__word_ptr[r10 + 0x956], 1); a.je(vanilla);
            a.movsx(r11d, __word_ptr[r10 + 0x6EE]);
            a.mov(r9, buildings);
            a.mov(ecx, __dword_ptr[r9 + 0x50]);
            a.cmp(ecx, 1); a.jle(vanilla); a.cmp(ecx, BuildingSlots); a.jg(vanilla);
            // Do not let the bypass introduce an earlier invalid workshop dereference.
            a.movsx(eax, __word_ptr[r10 + 0x990]);
            a.test(eax, eax); a.jle(vanilla); a.cmp(eax, ecx); a.jge(vanilla);
            a.imul(rax, rax, BuildingStride); a.add(rax, r9);
            a.cmp(__word_ptr[rax + 0x12C], (int)AliveState.IsAlive); a.jne(vanilla);
            a.cmp(__word_ptr[rax + 0x12E], (int)(tanner ? eStructs.STRUCT_TANNERS_WORKSHOP : eStructs.STRUCT_POLETURNERS_WORKSHOP)); a.jne(vanilla);
            a.movsx(edx, __word_ptr[rax + 0x132]); a.cmp(edx, r11d); a.jne(vanilla);
            a.mov(edx, __dword_ptr[rax + 0x134]); a.cmp(edx, __dword_ptr[r10 + 0x9C0]); a.jne(vanilla);
            if (tanner)
            {
                // Native signed > 0, not an unsigned inventory approximation.
                a.cmp(__dword_ptr[rax + 0x17C + 4 * (int)eGoods.STORED_COW_HIDES], 0); a.jg(ready);
                a.mov(ecx, __dword_ptr[r12]); a.cmp(ecx, 1); a.jle(vanilla);
                a.cmp(ecx, UnitSlots); a.jg(vanilla);
                a.mov(r9, r12); a.add(r9, UnitStride); a.mov(r8d, 1);
                a.Label(ref scan);
                a.cmp(__word_ptr[r9 + 0x6E4], (int)AliveState.IsAlive); a.jne(next);
                // FUN_18B470 reads the 16-bit death marker r_IsKilledByProjectile (also set by melee).
                a.cmp(__word_ptr[r9 + 0x8F8], 0); a.jne(next);
                a.movsx(edx, __word_ptr[r9 + 0x6EE]); a.cmp(edx, r11d); a.jne(next);
                a.cmp(__word_ptr[r9 + 0x6E6], (int)eChimps.CHIMP_TYPE_COW); a.jne(next);
                a.cmp(__word_ptr[r9 + 0x918], 0); a.jne(next);
                // Native nearest-cow selection compares max(abs(dx), abs(dy)) < 10000.
                // Both axis differences must lie strictly inside that range; no scratch-global writes.
                a.movsx(eax, __word_ptr[r9 + 0x71C]); a.movsx(edx, __word_ptr[r10 + 0x71C]); a.sub(eax, edx);
                a.cmp(eax, -10000); a.jle(next); a.cmp(eax, 10000); a.jge(next);
                a.movsx(eax, __word_ptr[r9 + 0x71E]); a.movsx(edx, __word_ptr[r10 + 0x71E]); a.sub(eax, edx);
                a.cmp(eax, -10000); a.jle(next); a.cmp(eax, 10000); a.jl(ready);
                a.Label(ref next);
                a.add(r9, UnitStride); a.inc(r8d); a.cmp(r8d, ecx); a.jl(scan);
            }
            else
            {
                // Same predicate and reverse scan as FUN_B9280(goods=wood, minimum=1, owner).
                a.dec(ecx); a.imul(rax, rcx, BuildingStride); a.add(r9, rax);
                a.Label(ref scan);
                a.cmp(__word_ptr[r9 + 0x12C], (int)AliveState.IsAlive); a.jne(next);
                a.cmp(__word_ptr[r9 + 0x12E], (int)eStructs.STRUCT_GOODS_YARD); a.jne(next);
                a.movsx(edx, __word_ptr[r9 + 0x132]); a.cmp(edx, r11d); a.jne(next);
                a.cmp(__dword_ptr[r9 + 0x17C + 4 * (int)eGoods.STORED_WOOD_PLANKS], 1); a.jge(ready);
                a.Label(ref next);
                a.sub(r9, BuildingStride); a.dec(ecx); a.jg(scan);
            }
            a.jmp(vanilla);
            a.Label(ref ready);
            Restore(a);
            // Keep the original test/movzx and their flags. Only omit the animation-completion exit.
            a.AddInstruction(replay[0]); a.AddInstruction(replay[2]); a.jmp(done);
            a.Label(ref vanilla);
            Restore(a);
            foreach (Instruction instruction in replay) a.AddInstruction(instruction);
            a.Label(ref done);
            a.nop(); // One label per instruction, including the final continuation.
        }

        private static void Restore(Assembler a)
        {
            a.pop(r11); a.pop(r10); a.pop(r9); a.pop(r8); a.pop(rdx); a.pop(rcx); a.pop(rax); a.popfq();
        }
    }
}
