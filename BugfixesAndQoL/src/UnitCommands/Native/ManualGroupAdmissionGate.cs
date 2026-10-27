using System;
using Iced.Intel;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL.UnitCommands
{
    internal static class ManualGroupAdmissionGate
    {
        internal const int Rva = 0x118F50, ContinueRva = 0x118F5E, SkipRva = 0x118FBE;
        internal static readonly byte[] Bytes = { 0x66,0x46,0x89,0xBC,0x29,0x34,0x09,0,0,0xBA,0x65,0,0,0 };

        // Windows x64 NT_TIB.Self, not a guessed managed/thread-pool identity.
        internal static void EmitThreadToken(Assembler asm)
        {
            asm.db(new byte[] { 0x65,0x48,0x8B,0x04,0x25,0x30,0,0,0 }); // mov rax,gs:[30]
            asm.ret();
        }

        // Common-group loop: EAX=id, RCX=id*490, R13=unit manager, RSI=tribe.
        // No calls, allocations, or managed transition. Restore registers/flags on both exits.
        internal static void Emit(Assembler asm, IntPtr slot, ulong reject)
        {
            var pass = asm.CreateLabel();
            asm.pushfq(); asm.push(r10); asm.push(r11);
            asm.mov(r10, unchecked((ulong)slot.ToInt64())); asm.mov(r10, __qword_ptr[r10]);
            asm.test(r10, r10); asm.jz(pass);
            asm.db(new byte[] { 0x65,0x4C,0x8B,0x1C,0x25,0x30,0,0,0 }); // mov r11,gs:[30]
            asm.cmp(__qword_ptr[r10], r11); asm.jne(pass);
            asm.cmp(__qword_ptr[r10 + 8], r13); asm.jne(pass);
            asm.cmp(__qword_ptr[r10 + 16], rsi); asm.jne(pass);
            asm.cmp(eax, 1); asm.jl(pass);
            asm.cmp(eax, ManualRegionAdmissionBatch.Capacity); asm.jg(pass);
            asm.mov(r11d, eax); asm.shl(r11, 4); asm.add(r11, r10);
            asm.mov(r10, __qword_ptr[r10 + 24]);
            asm.cmp(__qword_ptr[r11 + 32], r10); asm.jne(pass);
            asm.cmp(__byte_ptr[r11 + 44], (byte)ManualRegionAdmission.Excluded); asm.jne(pass);
            // Native unit base + 65C + GameUnit.GlobalId(94).
            asm.mov(r10d, __dword_ptr[rcx + r13 + 0x6F0]);
            asm.cmp(__dword_ptr[r11 + 40], r10d); asm.jne(pass);
            asm.pop(r11); asm.pop(r10); asm.popfq(); asm.jmp(reject);
            asm.Label(ref pass);
            asm.pop(r11); asm.pop(r10); asm.popfq();
        }
    }
}
