using Iced.Intel;
using System;
using static Iced.Intel.AssemblerRegisters;

namespace ImprovedHunters
{
    internal static class DispatchIndexStub
    {
        internal static void Generate(
            Assembler assembler,
            ReadOnlySpan<Instruction> instructions,
            ulong flagAddress,
            int sourceType,
            byte replacementIndex,
            bool sourceTypeInR9)
        {
            if (instructions.Length != 2 ||
                instructions[0].Mnemonic != Mnemonic.Movzx ||
                instructions[0].Op0Register != Register.EAX ||
                instructions[1].Mnemonic != Mnemonic.Mov ||
                instructions[1].Op0Register != Register.ECX)
                throw new InvalidOperationException("Unexpected dispatch-reader hook boundary.");

            Label vanilla = assembler.CreateLabel("dispatchOverrideVanilla");
            Label readOriginalIndex = assembler.CreateLabel("dispatchOverrideReadOriginalIndex");
            Label loadDispatchTarget = assembler.CreateLabel("dispatchOverrideLoadDispatchTarget");
            assembler.pushfq();
            assembler.push(rax);
            assembler.mov(rax, flagAddress);
            assembler.cmp(__dword_ptr[rax], 0);
            assembler.je(vanilla);
            assembler.pop(rax);
            assembler.popfq();
            if (sourceTypeInR9) assembler.cmp(r9d, sourceType);
            else assembler.cmp(r11d, sourceType);
            assembler.jne(readOriginalIndex);
            assembler.mov(eax, replacementIndex);
            assembler.jmp(loadDispatchTarget);
            assembler.Label(ref vanilla);
            assembler.pop(rax);
            assembler.popfq();
            assembler.Label(ref readOriginalIndex);
            assembler.AddInstruction(instructions[0]);
            assembler.Label(ref loadDispatchTarget);
            assembler.AddInstruction(instructions[1]);
        }
    }
}
