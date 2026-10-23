// Feature: Native-only fear-factor stubs. Both sites run in hot simulation paths
// (every melee/projectile hit and every marked visible unit per tick), so the
// disabled default must not enter managed code; one native flag byte decides.
using Iced.Intel;
using System;
using static Iced.Intel.AssemblerRegisters;

namespace ExtraFeatures
{
    internal static class FearFactorNativeStubs
    {
        // Function entry 0x180560: MOVSXD RAX,R8D; IMUL RCX,RAX,0x583C; LEA RAX,[fear table].
        // RAX is dead at entry and entry flags are not live, so the flag test needs no save.
        internal static void GenerateDamage(
            Assembler assembler,
            ReadOnlySpan<Instruction> overwrittenInstructions,
            ulong returnAddress,
            ulong enabledFlagAddress,
            ulong fearTableAddress)
        {
            if (overwrittenInstructions.Length != 3 ||
                overwrittenInstructions[0].Length != 3 ||
                overwrittenInstructions[0].Mnemonic != Mnemonic.Movsxd ||
                overwrittenInstructions[0].Op0Register != Register.RAX ||
                overwrittenInstructions[0].Op1Register != Register.R8D ||
                overwrittenInstructions[1].Length != 7 ||
                overwrittenInstructions[1].Mnemonic != Mnemonic.Imul ||
                overwrittenInstructions[1].Op0Register != Register.RCX ||
                overwrittenInstructions[1].Op1Register != Register.RAX ||
                overwrittenInstructions[1].Immediate32 != FearFactorNativeDefinition.PlayerRecordStride ||
                overwrittenInstructions[2].Length != 7 ||
                overwrittenInstructions[2].Mnemonic != Mnemonic.Lea ||
                overwrittenInstructions[2].Op0Register != Register.RAX ||
                !overwrittenInstructions[2].IsIPRelativeMemoryOperand ||
                overwrittenInstructions[2].IPRelativeMemoryAddress != fearTableAddress ||
                returnAddress != overwrittenInstructions[2].NextIP)
            {
                throw new InvalidOperationException("The fear-factor damage entry contract differs.");
            }

            Label vanilla = assembler.CreateLabel("fearDamageVanilla");
            assembler.mov(rax, enabledFlagAddress);
            assembler.cmp(__byte_ptr[rax], 1);
            assembler.jne(vanilla);
            // Neutral fear returns the unchanged base damage to the caller.
            assembler.mov(eax, edx);
            assembler.ret();

            assembler.Label(ref vanilla);
            foreach (Instruction instruction in overwrittenInstructions)
                assembler.AddInstruction(instruction);
        }

        // Overlay 0x1A19F2: IMUL RCX,R10,0x583C; MOV EAX,[RCX+RDI+fear table]; next TEST EAX,EAX.
        // RAX is dead before the load and the following TEST regenerates the consumed flags.
        internal static void GenerateOverlay(
            Assembler assembler,
            ReadOnlySpan<Instruction> overwrittenInstructions,
            ulong returnAddress,
            ulong enabledFlagAddress)
        {
            if (overwrittenInstructions.Length != 2 ||
                overwrittenInstructions[0].Length != 7 ||
                overwrittenInstructions[0].Mnemonic != Mnemonic.Imul ||
                overwrittenInstructions[0].Op0Register != Register.RCX ||
                overwrittenInstructions[0].Op1Register != Register.R10 ||
                overwrittenInstructions[0].Immediate32 != FearFactorNativeDefinition.PlayerRecordStride ||
                overwrittenInstructions[1].Length != FearFactorNativeDefinition.UiFearLoadLength ||
                overwrittenInstructions[1].Mnemonic != Mnemonic.Mov ||
                overwrittenInstructions[1].Op0Register != Register.EAX ||
                overwrittenInstructions[1].MemoryBase != Register.RCX ||
                overwrittenInstructions[1].MemoryIndex != Register.RDI ||
                overwrittenInstructions[1].MemoryDisplacement64 != (ulong)FearFactorNativeDefinition.FearTableRva ||
                returnAddress != overwrittenInstructions[1].NextIP)
            {
                throw new InvalidOperationException("The fear-factor overlay load contract differs.");
            }

            Label done = assembler.CreateLabel("fearOverlayDone");
            assembler.AddInstruction(overwrittenInstructions[0]);
            // IMUL changes flags, so test the switch after it; MOV preserves the CMP result.
            assembler.mov(rax, enabledFlagAddress);
            assembler.cmp(__byte_ptr[rax], 1);
            assembler.AddInstruction(overwrittenInstructions[1]);
            assembler.jne(done);
            // Hide only the overlay: Vanilla's fear value itself stays unchanged.
            assembler.xor(eax, eax);
            assembler.Label(ref done);
            assembler.nop();
        }
    }
}
