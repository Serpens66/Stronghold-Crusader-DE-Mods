// Functional shared callback emitter. Preserve all GPRs, RFLAGS and volatile XMM0..5.
// Required by the descent guard even when temporary diagnostics are removed.
using Iced.Intel;
using static Iced.Intel.AssemblerRegisters;
using System;

namespace EnemyGatePathfindingTest
{
    internal static class NativeCallbackAdapterEmitter
    {
        private const int Frame = 240;
        private static readonly AssemblerRegister64[] Registers =
            { rax, rbx, rcx, rdx, rsi, rdi, rbp, rsp, r8, r9, r10, r11, r12, r13, r14, r15 };
        internal static void EmitExecution(Assembler a, ReadOnlySpan<Instruction> original, ulong before,
            ulong after, ulong enabled, ulong module, bool currentUnitGlobal, int assassinType = 73)
        {
            EmitFilteredCallback(a, before, enabled, module, currentUnitGlobal, assassinType);
            foreach (Instruction instruction in original) a.AddInstruction(instruction);
            if (after != 0) EmitFilteredCallback(a, after, enabled, module, currentUnitGlobal, assassinType);
        }
        private static void EmitFilteredCallback(Assembler a, ulong callback, ulong enabled, ulong module, bool currentUnitGlobal, int assassinType)
        {
            // One shared replay of original instructions; never duplicate their IPs in Iced.
            Label skip = a.CreateLabel(), done = a.CreateLabel();
            a.pushfq(); a.push(rax); a.push(r11);
            a.mov(rax, enabled); a.cmp(__dword_ptr[rax], 0); a.je(skip);
            if (currentUnitGlobal) { a.mov(rax, module + 0x9302C4); a.mov(eax, __dword_ptr[rax]); }
            else a.mov(eax, ebx);
            a.cmp(eax, 1); a.jb(skip); a.cmp(eax, 10000); a.ja(skip);
            a.imul(rax, rax, 0x490); a.mov(r11, module + 0x67E8AE6);
            a.cmp(__word_ptr[r11 + rax], assassinType); a.jne(skip);
            a.pop(r11); a.pop(rax); a.popfq();
            Emit(a, ReadOnlySpan<Instruction>.Empty, callback, enabled);
            a.jmp(done);
            a.Label(ref skip); a.pop(r11); a.pop(rax); a.popfq();
            a.Label(ref done); a.nop();
        }
        internal static void Emit(Assembler a, ReadOnlySpan<Instruction> original, ulong callback, ulong enabled)
        {
            Label inactive = a.CreateLabel(), replay = a.CreateLabel();
            a.pushfq(); a.push(rax); a.mov(rax, enabled); a.cmp(__dword_ptr[rax], 0); a.pop(rax); a.je(inactive);
            a.popfq();
            a.lea(rsp, __qword_ptr[rsp - Frame]);
            for (int i = 0; i < Registers.Length; i++) if (i != 7) a.mov(__qword_ptr[rsp + i * 8], Registers[i]);
            a.lea(rax, __qword_ptr[rsp + Frame]); a.mov(__qword_ptr[rsp + 56], rax);
            a.pushfq(); a.pop(rax); a.mov(__qword_ptr[rsp + 128], rax);
            a.movdqu(__xmmword_ptr[rsp + 144], xmm0); a.movdqu(__xmmword_ptr[rsp + 160], xmm1);
            a.movdqu(__xmmword_ptr[rsp + 176], xmm2); a.movdqu(__xmmword_ptr[rsp + 192], xmm3);
            a.movdqu(__xmmword_ptr[rsp + 208], xmm4); a.movdqu(__xmmword_ptr[rsp + 224], xmm5);
            a.mov(rax, rsp); a.mov(rcx, rax); a.and(rsp, -16); a.sub(rsp, 48);
            a.mov(__qword_ptr[rsp + 32], rax); a.mov(rax, callback); a.call(rax);
            a.mov(rsp, __qword_ptr[rsp + 32]);
            a.movdqu(xmm0, __xmmword_ptr[rsp + 144]); a.movdqu(xmm1, __xmmword_ptr[rsp + 160]);
            a.movdqu(xmm2, __xmmword_ptr[rsp + 176]); a.movdqu(xmm3, __xmmword_ptr[rsp + 192]);
            a.movdqu(xmm4, __xmmword_ptr[rsp + 208]); a.movdqu(xmm5, __xmmword_ptr[rsp + 224]);
            a.push(__qword_ptr[rsp + 128]); a.popfq();
            for (int i = Registers.Length - 1; i >= 0; i--) if (i != 7) a.mov(Registers[i], __qword_ptr[rsp + i * 8]);
            a.lea(rsp, __qword_ptr[rsp + Frame]); a.jmp(replay);
            a.Label(ref inactive); a.popfq();
            a.Label(ref replay);
            foreach (Instruction instruction in original) a.AddInstruction(instruction);
        }
    }
}
