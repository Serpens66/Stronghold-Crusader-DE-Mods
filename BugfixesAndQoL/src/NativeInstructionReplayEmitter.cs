using Iced.Intel;
using RedBird.X64.Extensions;
using System;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL
{
    internal static class NativeInstructionReplayEmitter
    {
        internal static void EmitConditionalSkip(
            Assembler assembler,
            ReadOnlySpan<Instruction> overwrittenInstructions,
            ulong enabledFlagAddress,
            int skippedInstructionIndex,
            int skippedInstructionCount,
            string label)
        {
            if (skippedInstructionIndex < 0 || skippedInstructionCount <= 0 ||
                skippedInstructionIndex > overwrittenInstructions.Length - skippedInstructionCount ||
                overwrittenInstructions.Length == skippedInstructionCount)
            {
                throw new InvalidOperationException(
                    $"Permanent native skip hook '{label}' has an invalid instruction skip range.");
            }

            Instruction[] enabledReplay = overwrittenInstructions.CloneInstructionsWithoutIP();
            Label vanilla = assembler.CreateLabel(label + "Vanilla");
            Label done = assembler.CreateLabel(label + "Done");

            assembler.pushfq();
            assembler.push(rax);
            assembler.mov(rax, enabledFlagAddress);
            assembler.cmp(__dword_ptr[rax], 0);
            assembler.je(vanilla);
            assembler.pop(rax);
            assembler.popfq();
            int skippedInstructionEnd = skippedInstructionIndex + skippedInstructionCount;
            for (int index = 0; index < enabledReplay.Length; index++)
                if (index < skippedInstructionIndex || index >= skippedInstructionEnd)
                    assembler.AddInstruction(enabledReplay[index]);
            assembler.jmp(done);

            assembler.Label(ref vanilla);
            assembler.pop(rax);
            assembler.popfq();
            foreach (Instruction instruction in overwrittenInstructions)
                assembler.AddInstruction(instruction);

            assembler.Label(ref done);
            assembler.nop();
        }

        internal static void EmitClassifier(
            Assembler assembler,
            ReadOnlySpan<Instruction> overwrittenInstructions,
            ulong enabledFlagAddress,
            int prefixInstructionCount,
            int sourceIndex,
            int replacementIndex)
        {
            if (overwrittenInstructions.Length < prefixInstructionCount)
                throw new InvalidOperationException("Unexpected classifier hook boundary.");

            Instruction[] mappedReplay = overwrittenInstructions.CloneInstructionsWithoutIP();
            Label vanilla = assembler.CreateLabel("classifierVanilla");
            Label mapped = assembler.CreateLabel("classifierMapped");
            Label done = assembler.CreateLabel("classifierDone");
            assembler.pushfq();
            assembler.push(rax);
            assembler.mov(rax, enabledFlagAddress);
            assembler.cmp(__dword_ptr[rax], 0);
            assembler.je(vanilla);
            assembler.pop(rax);
            assembler.popfq();

            for (int index = 0; index < prefixInstructionCount; index++)
                assembler.AddInstruction(mappedReplay[index]);
            assembler.cmp(eax, sourceIndex);
            assembler.jne(mapped);
            assembler.mov(eax, replacementIndex);

            assembler.Label(ref mapped);
            for (int index = prefixInstructionCount; index < mappedReplay.Length; index++)
                assembler.AddInstruction(mappedReplay[index]);
            assembler.jmp(done);

            assembler.Label(ref vanilla);
            assembler.pop(rax);
            assembler.popfq();
            foreach (Instruction instruction in overwrittenInstructions)
                assembler.AddInstruction(instruction);

            assembler.Label(ref done);
            assembler.nop();
        }
    }
}
