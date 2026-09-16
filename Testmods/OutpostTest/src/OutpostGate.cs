using System;
using Iced.Intel;
using RedBird.X64.Extensions;
using static Iced.Intel.AssemblerRegisters;

namespace OutpostTest
{
    internal static class OutpostGate
    {
        internal const int Rva = 0xABC78, Length = 16, ExitRva = 0xACDDB;
        internal static readonly byte[] Bytes = { 0x44,0x39,0x1D,0xA9,0xA2,0x5B,0x03,0x0F,0x84,0x56,0x11,0,0,0x45,0x85,0xFF };

        // Pure assembly: no managed callback, no volatile/XMM clobbering. The
        // enabled branch uses the existing epilogue before RBP/RDI/R14 spills.
        internal static void Generate(Assembler a, ReadOnlySpan<Instruction> original,
            ulong continuation, ulong flag, ulong exit)
        {
            if (original.Length != 3 || original[0].Length != 7 || original[1].Length != 6 ||
                original[2].Length != 3 || original[0].Mnemonic != Mnemonic.Cmp ||
                original[1].Mnemonic != Mnemonic.Je || original[2].Mnemonic != Mnemonic.Test ||
                original[1].NearBranchTarget != exit || original[2].NextIP != continuation)
                throw new InvalidOperationException("Outpost gate instruction contract changed.");
            var vanilla = a.CreateLabel();
            a.pushfq();
            a.push(rax);
            a.mov(rax, flag);
            a.cmp(__dword_ptr[rax], 0);
            a.je(vanilla);
            a.inc(__qword_ptr[rax + 8]); // simulation-thread-only confirmation counter
            a.pop(rax);
            a.popfq();
            a.AddUnrestrictedJmp(exit);
            a.Label(ref vanilla);
            a.pop(rax);
            a.popfq();
            foreach (var instruction in original) a.AddInstruction(instruction);
            a.AddUnrestrictedJmp(continuation);
        }
    }
}
