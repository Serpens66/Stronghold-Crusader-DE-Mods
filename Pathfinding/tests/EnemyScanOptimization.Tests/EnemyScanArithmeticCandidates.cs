using System;
using Iced.Intel;
using RedBird.X64.Extensions;
using static Iced.Intel.AssemblerRegisters;

namespace Pathfinding.EnemyScanOptimization
{
    // Offline candidates only until the installed-backend measurements justify publication.
    // The live table read is intentionally retained: neither seed range nor table immutability is assumed.
    internal static class EnemyScanArithmetic
    {
        internal const int ScoreSite = 0x18C6D2, ScoreLength = 14;
        internal const int DistanceYMajorSite = 0x18C50C, DistanceXMajorSite = 0x18C533, DistanceLength = 32;
        internal static readonly byte[] ScoreOriginal = Hex("8B8485C019320099F77C243C03C8");
        internal static readonly byte[] DistanceYMajorOriginal = Hex("438D0C00B867666666F7E9D1FA8BC2C1E81F03C2410FAFC09941F7F9418D0C01");
        internal static readonly byte[] DistanceXMajorOriginal = Hex("438D0C09B867666666F7E9D1FA8BC2C1E81F03C2410FAFC19941F7F8418D0C00");

        // The displaced score block ends in ADD ECX,EAX. That instruction restores all defined
        // output flags after the guard, and the following native CMP overwrites them again.
        // R8/R9, every non-output register, stack and the table address are preserved.
        internal static void GenerateScore(Assembler a)
        {
            a.mov(eax, __dword_ptr[rbp + rax * 4 + 0x3219C0]);
            var identity = a.CreateLabel(); var half = a.CreateLabel(); var fifth = a.CreateLabel(); var output = a.CreateLabel();
            a.cmp(__dword_ptr[rsp + 0x3C], 1); a.je(identity);
            a.cmp(__dword_ptr[rsp + 0x3C], 2); a.je(half);
            a.cmp(__dword_ptr[rsp + 0x3C], 5); a.je(fifth);
            // Native list-count construction selects only 1/2/5. Keep IDIV for any other value.
            a.cdq(); a.idiv(__dword_ptr[rsp + 0x3C]); a.jmp(output);
            a.Label(ref identity); a.xor(edx, edx); a.jmp(output);
            a.Label(ref half);
            a.push(r8); a.mov(r8d, eax); a.mov(edx, eax); a.shr(edx, 31); a.add(eax, edx); a.sar(eax, 1);
            a.lea(edx, __[rax + rax]); a.sub(r8d, edx); a.mov(edx, r8d); a.pop(r8); a.jmp(output);
            a.Label(ref fifth);
            a.push(r8); a.mov(r8d, eax);
            a.mov(eax, 0x66666667); a.imul(r8d); a.sar(edx, 1);
            a.mov(eax, edx); a.shr(eax, 31); a.add(eax, edx);
            a.imul(edx, eax, 5); a.sub(r8d, edx); a.mov(edx, r8d); a.pop(r8);
            a.Label(ref output);
            a.add(ecx, eax);
        }

        // Before either native distance branch: |dx| and |dy| are nonnegative and <=65535.
        // The selected major component is nonzero; P=floor(2*m/5)*m <=1717934490.
        // When P<M the quotient is exactly zero and the original remainder is P.
        // Only EAX/EDX/ECX change; the first continuation instruction is CMP ECX,EDI.
        internal static void GenerateDistance(Assembler a, bool yMajor)
        {
            if (yMajor) a.lea(ecx, __[r8 + r8]); else a.lea(ecx, __[r9 + r9]);
            a.mov(eax, 0x66666667); a.imul(ecx); a.sar(edx, 1);
            a.mov(eax, edx); a.shr(eax, 31); a.add(eax, edx);
            if (yMajor) a.imul(eax, r8d); else a.imul(eax, r9d);
            var divide = a.CreateLabel(); var ready = a.CreateLabel();
            if (yMajor) a.cmp(eax, r9d); else a.cmp(eax, r8d);
            a.jae(divide); a.mov(edx, eax); a.xor(eax, eax); a.jmp(ready);
            a.Label(ref divide); a.cdq();
            if (yMajor) a.idiv(r9d); else a.idiv(r8d);
            a.Label(ref ready);
            if (yMajor) a.lea(ecx, __[r9 + rax]); else a.lea(ecx, __[r8 + rax]);
        }

        // All candidates and the real inactive path are measured through this same logical guard.
        // No managed transition, no unpatching; published hooks would remain until process end.
        internal static void GenerateGuarded(Assembler a, ReadOnlySpan<Instruction> original, ulong demand, Action<Assembler> body)
        {
            var vanilla = a.CreateLabel(); var done = a.CreateLabel();
            a.push(rax); a.mov(rax, demand); a.cmp(__byte_ptr[rax], 0); a.pop(rax); a.je(vanilla);
            body(a); a.jmp(done);
            a.Label(ref vanilla); a.AddInstructions(original);
            a.Label(ref done); a.nop();
        }

        private static byte[] Hex(string text)
        {
            var bytes = new byte[text.Length / 2];
            for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(text.Substring(i * 2, 2), 16);
            return bytes;
        }
    }
}
