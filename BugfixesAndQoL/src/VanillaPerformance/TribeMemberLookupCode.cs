using Iced.Intel;
using System;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL.VanillaPerformance
{
    // V14 machine code: Vanilla's "unit id of the i-th tribe member" (0x119F90) without its bit-by-bit scan.
    // 0x119F90 (FBCB9319, body 0x8C, SHA-256 CD5DAE0B...) returns 0 unless the tribe word +0x40 == 2 and
    // 0 <= i < (short)+0x5C; otherwise it walks the 625 member words from +0x60 (word ascending, bits 0..15)
    // and returns word*16+bit of the i-th set bit, or 0 when there are fewer set bits. It runs on every
    // member of every group order, several passes each (quadratic in the group size). This version reads the
    // same words as 156 qwords plus the last word and skips set bits with x &= x - 1, so the answer, the
    // order and the zero cases are identical. Registers: it writes only rax, rdx, r9, r10, r11 and flags,
    // a subset of the original's (rax, rcx, rdx, r9, r10, r11; rbx is restored), and keeps r8 like it.
    internal static class TribeMemberLookupCode
    {
        internal const int Rva = 0x119F90, BodyLength = 0x8C;
        internal const string BodySha256 = "CD5DAE0B455BA6CBFC20D0193849591A8250C710C8B77BBDCFFF6908FBD1544F";
        // mov [rsp+8],rbx ; movsxd rax,edx ; xor r9d,r9d ; imul rdx,rax,0x688: four whole instructions,
        // no RIP-relative operand, and no branch of the body targets 0x119F91..0x119FA1.
        internal const int DisplacedLength = 18;
        internal static readonly byte[] DisplacedBytes =
            { 0x48, 0x89, 0x5C, 0x24, 0x08, 0x48, 0x63, 0xC2, 0x45, 0x33, 0xC9, 0x48, 0x69, 0xD0, 0x88, 0x06, 0x00, 0x00 };
        internal const int TribeStride = 0x688, KindOffset = 0x40, CountOffset = 0x5C, WordsOffset = 0x60, WordCount = 0x271;
        private const int FullQwords = WordCount / 4, LastWordOffset = FullQwords * 8;

        // Hook stub at the function entry: an enabled flag (dword != 0) runs the fast lookup and returns to the
        // caller; otherwise the displaced original instructions run and the backend jumps back into the body.
        internal static void Emit(Assembler a, ulong enabledFlag, ReadOnlySpan<Instruction> original)
        {
            if (original.Length != 4) throw new InvalidOperationException("Unexpected 0x119F90 hook boundary.");
            Label vanilla = a.CreateLabel();
            a.mov(rax, enabledFlag);
            a.cmp(__dword_ptr[rax], 0);
            a.je(vanilla);
            EmitLookup(a);
            a.Label(ref vanilla);
            foreach (Instruction instruction in original) a.AddInstruction(instruction);
        }

        // rcx = tribe manager, edx = tribe id, r8d = member ordinal; result in eax, then ret.
        internal static void EmitLookup(Assembler a)
        {
            Label zero = a.CreateLabel(), qword = a.CreateLabel(), last = a.CreateLabel(), bits = a.CreateLabel(),
                next = a.CreateLabel(), advance = a.CreateLabel(), found = a.CreateLabel();
            a.movsxd(rax, edx);
            a.imul(rax, rax, TribeStride);
            a.add(rax, rcx);
            a.cmp(__word_ptr[rax + KindOffset], 2);
            a.jne(zero);
            a.movsx(r9d, __word_ptr[rax + CountOffset]);
            a.cmp(r8d, r9d);
            a.jge(zero);
            a.test(r8d, r8d);
            a.js(zero);
            a.mov(r9d, r8d);
            a.lea(r10, __[rax + WordsOffset]);
            a.xor(r11d, r11d);
            a.Label(ref qword);
            a.cmp(r11d, FullQwords);
            a.je(last);
            a.mov(rax, __qword_ptr[r10 + r11 * 8]);
            a.jmp(bits);
            a.Label(ref last);
            a.movzx(eax, __word_ptr[r10 + LastWordOffset]);
            a.Label(ref bits);
            a.test(rax, rax);
            a.jz(advance);
            a.Label(ref next);
            a.test(r9d, r9d);
            a.jz(found);
            a.dec(r9d);
            a.lea(rdx, __[rax - 1]);
            a.and(rax, rdx);
            a.jnz(next);
            a.Label(ref advance);
            a.inc(r11d);
            a.cmp(r11d, FullQwords + 1);
            a.jb(qword);
            a.Label(ref zero);
            a.xor(eax, eax);
            a.ret();
            a.Label(ref found);
            a.bsf(rax, rax);
            a.shl(r11d, 6);
            a.add(eax, r11d);
            a.ret();
        }
    }
}
