using Iced.Intel;
using RedBird.X64.Extensions;
using System;
using static Iced.Intel.AssemblerRegisters;

namespace ExtraFeatures
{
    internal static class PlagueApothecarySearchRangeEmitter
    {
        internal static void Emit(
            Assembler assembler,
            ReadOnlySpan<Instruction> overwrittenInstructions,
            ulong distanceResultAddress,
            ulong effectiveMaximumAddress,
            ulong distanceCalculationAddress,
            ulong rejectAddress)
        {
            if (overwrittenInstructions.Length != 3 ||
                overwrittenInstructions[0].Length != 5 ||
                overwrittenInstructions[0].FlowControl != FlowControl.Call ||
                overwrittenInstructions[0].NearBranchTarget != distanceCalculationAddress ||
                overwrittenInstructions[1].Length != 7 ||
                overwrittenInstructions[1].Mnemonic != Mnemonic.Cmp ||
                overwrittenInstructions[2].Length != 2 ||
                overwrittenInstructions[2].Mnemonic != Mnemonic.Jg ||
                overwrittenInstructions[2].NearBranchTarget != rejectAddress)
            {
                throw new InvalidOperationException(
                    "Unexpected apothecary plague-search hook boundary.");
            }

            Instruction[] replay = overwrittenInstructions.CloneInstructionsWithoutIP();
            assembler.AddInstruction(replay[0]);

            // POP does not change the CMP flags. Restore both scratch registers before
            // the signed branch so the accepted and rejected paths see Vanilla's
            // post-distance-call register state.
            assembler.push(rax);
            assembler.push(rcx);
            assembler.mov(rax, effectiveMaximumAddress);
            assembler.mov(eax, __dword_ptr[rax]);
            assembler.mov(rcx, distanceResultAddress);
            assembler.cmp(__dword_ptr[rcx], eax);
            assembler.pop(rcx);
            assembler.pop(rax);
            assembler.jg(rejectAddress);
        }
    }
}
