using Iced.Intel;
using RedBird.X64.Extensions;
using System;
using static Iced.Intel.AssemblerRegisters;

namespace ExtraFeatures
{
    internal static class ElevatedMoatDrawbridgeHooks
    {
        internal static void GenerateCompleted(
            Assembler assembler,
            ReadOnlySpan<Instruction> overwrittenInstructions,
            ulong returnAddress,
            ulong featureActiveFlagAddress,
            ulong stateUpdateAddress)
        {
            if (overwrittenInstructions.Length != 3 ||
                overwrittenInstructions[0].Length != 3 ||
                overwrittenInstructions[0].Mnemonic != Mnemonic.Mov ||
                overwrittenInstructions[0].Op0Register != Register.RCX ||
                overwrittenInstructions[0].Op1Register != Register.RBX ||
                overwrittenInstructions[1].Length != 5 ||
                overwrittenInstructions[1].Mnemonic != Mnemonic.Call ||
                overwrittenInstructions[1].NearBranchTarget != stateUpdateAddress ||
                overwrittenInstructions[2].Length !=
                    ElevatedMoatNativeContract.CompletedDrawbridgeHeightWriteLength ||
                overwrittenInstructions[2].Mnemonic != Mnemonic.Mov ||
                !HasMemoryOperands(overwrittenInstructions[2], Register.RBX, Register.R14) ||
                overwrittenInstructions[2].MemoryDisplacement64 !=
                    ElevatedMoatNativeContract.TileHeightGridOffset ||
                overwrittenInstructions[2].Immediate8 != 0 ||
                returnAddress != overwrittenInstructions[2].NextIP)
            {
                throw new InvalidOperationException(
                    "The completed-drawbridge hook instruction contract differs.");
            }

            // Preserve Vanilla's structure-state update before changing only the height write.
            assembler.AddInstruction(overwrittenInstructions[0]);
            assembler.AddInstruction(overwrittenInstructions[1]);

            Label vanillaWrite = assembler.CreateLabel("completedDrawbridgeVanillaHeightWrite");
            EmitEnabledFlagBranch(assembler, featureActiveFlagAddress, vanillaWrite);

            // Vanilla keeps the drawbridge tile at the moat floor and applies its
            // fixed eight-unit deck offset separately. Keep Vanilla's zero on ordinary
            // terrain and reproduce that topology above Vanilla's terrain limit.
            // RAX is dead after the state call and following paths do not consume flags.
            assembler.movzx(eax,
                __byte_ptr[rbx + r14 + ElevatedMoatNativeContract.TileDefaultHeightGridOffset]);
            assembler.cmp(eax, ElevatedMoatNativeContract.MaximumVanillaTerrainHeight);
            assembler.jbe(vanillaWrite);

            assembler.sub(eax, ElevatedMoatNativeContract.MoatDepth);
            assembler.mov(__byte_ptr[rbx + r14 + ElevatedMoatNativeContract.TileHeightGridOffset], al);
            assembler.AddUnrestrictedJmp(returnAddress);

            assembler.Label(ref vanillaWrite);
            assembler.AddInstruction(overwrittenInstructions[2]);
        }

        internal static void GenerateLowered(
            Assembler assembler,
            ReadOnlySpan<Instruction> overwrittenInstructions,
            ulong returnAddress,
            ulong featureActiveFlagAddress,
            ulong imageBase)
        {
            if (overwrittenInstructions.Length != 2 ||
                overwrittenInstructions[0].Length !=
                    ElevatedMoatNativeContract.LowerDrawbridgeHeightWriteLength ||
                overwrittenInstructions[0].Mnemonic != Mnemonic.Mov ||
                !HasMemoryOperands(overwrittenInstructions[0], Register.RBX, Register.RDI) ||
                overwrittenInstructions[0].MemoryDisplacement64 !=
                    ElevatedMoatNativeContract.TileHeightGridOffset ||
                overwrittenInstructions[0].Immediate8 != 0 ||
                overwrittenInstructions[1].Length !=
                    ElevatedMoatNativeContract.LowerDrawbridgeImageBaseLeaLength ||
                overwrittenInstructions[1].Mnemonic != Mnemonic.Lea ||
                overwrittenInstructions[1].Op0Register != Register.RDI ||
                overwrittenInstructions[1].MemoryBase != Register.RIP ||
                overwrittenInstructions[1].MemoryDisplacement64 != imageBase ||
                returnAddress != overwrittenInstructions[1].NextIP)
            {
                throw new InvalidOperationException(
                    "The lowered-drawbridge hook instruction contract differs.");
            }

            Label vanillaWrite = assembler.CreateLabel("loweredDrawbridgeVanillaHeightWrite");
            Label restoreImageBase = assembler.CreateLabel("loweredDrawbridgeRestoreImageBase");
            EmitEnabledFlagBranch(assembler, featureActiveFlagAddress, vanillaWrite);

            // RDI is the audited tile ID. Recreate Vanilla's moat-floor/deck separation
            // above the terrain limit. RAX is dead here; Vanilla's following INC
            // replaces flags before they are consumed.
            assembler.movzx(eax,
                __byte_ptr[rbx + rdi + ElevatedMoatNativeContract.TileDefaultHeightGridOffset]);
            assembler.cmp(eax, ElevatedMoatNativeContract.MaximumVanillaTerrainHeight);
            assembler.jbe(vanillaWrite);

            assembler.sub(eax, ElevatedMoatNativeContract.MoatDepth);
            assembler.mov(__byte_ptr[rbx + rdi + ElevatedMoatNativeContract.TileHeightGridOffset], al);
            assembler.jmp(restoreImageBase);

            assembler.Label(ref vanillaWrite);
            assembler.AddInstruction(overwrittenInstructions[0]);

            assembler.Label(ref restoreImageBase);
            assembler.AddInstruction(overwrittenInstructions[1]);
        }

        internal static void GenerateSpecialRenderer(
            Assembler assembler,
            ReadOnlySpan<Instruction> overwrittenInstructions,
            ulong returnAddress,
            ulong featureActiveFlagAddress,
            ulong tileDefaultHeightGridAddress,
            ulong currentTileHeightAddress)
        {
            if (overwrittenInstructions.Length != 6 ||
                overwrittenInstructions[0].Length != 3 ||
                overwrittenInstructions[0].Mnemonic != Mnemonic.Mov ||
                overwrittenInstructions[0].Op0Register != Register.RAX ||
                overwrittenInstructions[0].Op1Register != Register.RSP ||
                overwrittenInstructions[1].Length != 4 ||
                overwrittenInstructions[1].Mnemonic != Mnemonic.Mov ||
                overwrittenInstructions[1].MemoryBase != Register.RAX ||
                overwrittenInstructions[1].MemoryDisplacement64 != 0x20 ||
                overwrittenInstructions[1].Op1Register != Register.RBX ||
                overwrittenInstructions[2].Mnemonic != Mnemonic.Push ||
                overwrittenInstructions[2].Op0Register != Register.RBP ||
                overwrittenInstructions[3].Mnemonic != Mnemonic.Push ||
                overwrittenInstructions[3].Op0Register != Register.R12 ||
                overwrittenInstructions[4].Mnemonic != Mnemonic.Push ||
                overwrittenInstructions[4].Op0Register != Register.R14 ||
                overwrittenInstructions[5].Length != 7 ||
                overwrittenInstructions[5].Mnemonic != Mnemonic.Sub ||
                overwrittenInstructions[5].Op0Register != Register.RSP ||
                overwrittenInstructions[5].Immediate32 != 0x80 ||
                returnAddress != overwrittenInstructions[5].NextIP)
            {
                throw new InvalidOperationException(
                    "The drawbridge special-renderer prologue contract differs.");
            }

            Label vanillaPrologue = assembler.CreateLabel("drawbridgeSpecialRendererVanillaPrologue");
            EmitEnabledFlagBranch(assembler, featureActiveFlagAddress, vanillaPrologue);

            // The two Vanilla call sites omit the tile-height subtraction used by the
            // sibling drawbridge renderer. Argument 5 at entry is the current tile ID.
            // Preserve R10 while using it as the index; RAX is overwritten by the first
            // prologue instruction. Classify through the immutable default grid so
            // elevated moat-floor values at or below 12 are not mistaken for Vanilla.
            assembler.push(r10);
            assembler.mov(r10d, __dword_ptr[rsp + 0x30]);
            assembler.mov(rax, tileDefaultHeightGridAddress);
            assembler.cmp(
                __byte_ptr[rax + r10],
                ElevatedMoatNativeContract.MaximumVanillaTerrainHeight);
            assembler.pop(r10);
            assembler.jbe(vanillaPrologue);

            // R9 and stack argument 7 are the two height-blind coordinates.
            assembler.mov(rax, currentTileHeightAddress);
            assembler.mov(eax, __dword_ptr[rax]);
            assembler.sub(r9d, eax);
            assembler.sub(__dword_ptr[rsp + 0x38], eax);

            assembler.Label(ref vanillaPrologue);
            foreach (Instruction instruction in overwrittenInstructions)
                assembler.AddInstruction(instruction);
        }

        internal static void GenerateAnimatedRendererArguments(
            Assembler assembler,
            ReadOnlySpan<Instruction> overwrittenInstructions,
            ulong returnAddress,
            ulong featureActiveFlagAddress,
            ulong tileDefaultHeightGridAddress,
            ulong currentTileHeightAddress)
        {
            if (overwrittenInstructions.Length != 3 ||
                overwrittenInstructions[0].Length != 8 ||
                overwrittenInstructions[0].Mnemonic != Mnemonic.Mov ||
                overwrittenInstructions[0].Op0Register != Register.RCX ||
                overwrittenInstructions[0].MemoryBase != Register.RSP ||
                overwrittenInstructions[0].MemoryDisplacement64 != 0x140 ||
                overwrittenInstructions[1].Length != 4 ||
                overwrittenInstructions[1].Mnemonic != Mnemonic.Mov ||
                overwrittenInstructions[1].MemoryBase != Register.RSP ||
                overwrittenInstructions[1].MemoryDisplacement64 != 0x28 ||
                overwrittenInstructions[1].Op1Register != Register.ESI ||
                overwrittenInstructions[2].Length != 5 ||
                overwrittenInstructions[2].Mnemonic != Mnemonic.Mov ||
                overwrittenInstructions[2].MemoryBase != Register.RSP ||
                overwrittenInstructions[2].MemoryDisplacement64 != 0x20 ||
                overwrittenInstructions[2].Op1Register != Register.R15D ||
                returnAddress != overwrittenInstructions[2].NextIP)
            {
                throw new InvalidOperationException(
                    "The drawbridge animated-renderer argument contract differs.");
            }

            assembler.mov(rcx, __qword_ptr[rsp + 0x140]);

            Label vanillaHeight = assembler.CreateLabel("drawbridgeAnimatedRendererVanillaHeight");
            EmitEnabledFlagBranch(assembler, featureActiveFlagAddress, vanillaHeight);

            // This call site is reached only for a drawbridge (building type 0x31)
            // on the tile-flags == 4 branch. Vanilla passes zero as argument 6;
            // R15D is argument 5 and therefore the current tile ID. Classify through
            // DefaultHeightGrid, then pass the negative current-tile render offset used
            // by the sibling renderer. The callee remains Vanilla.
            assembler.mov(rax, tileDefaultHeightGridAddress);
            assembler.cmp(
                __byte_ptr[rax + r15],
                ElevatedMoatNativeContract.MaximumVanillaTerrainHeight);
            assembler.jbe(vanillaHeight);

            assembler.mov(rax, currentTileHeightAddress);
            assembler.mov(eax, __dword_ptr[rax]);
            assembler.neg(eax);
            assembler.mov(__dword_ptr[rsp + 0x28], eax);
            assembler.mov(__dword_ptr[rsp + 0x20], r15d);
            assembler.AddUnrestrictedJmp(returnAddress);

            assembler.Label(ref vanillaHeight);
            assembler.mov(__dword_ptr[rsp + 0x28], esi);
            assembler.mov(__dword_ptr[rsp + 0x20], r15d);
        }

        internal static void GenerateUnitHeightCorrection(
            Assembler assembler,
            ReadOnlySpan<Instruction> overwrittenInstructions,
            ulong returnAddress,
            ulong featureActiveFlagAddress)
        {
            if (overwrittenInstructions.Length != 3 ||
                overwrittenInstructions[0].Length != 5 ||
                overwrittenInstructions[0].Mnemonic != Mnemonic.Mov ||
                overwrittenInstructions[0].Op0Register != Register.EAX ||
                overwrittenInstructions[0].Immediate32 != ElevatedMoatNativeContract.MoatDepth ||
                overwrittenInstructions[1].Length != 7 ||
                overwrittenInstructions[1].Mnemonic != Mnemonic.Sub ||
                overwrittenInstructions[1].Op0Register != Register.AX ||
                overwrittenInstructions[1].MemoryBase != Register.RBX ||
                overwrittenInstructions[1].MemoryDisplacement64 !=
                    ElevatedMoatNativeContract.UnitCurrentElevationOffset ||
                overwrittenInstructions[2].Length != 7 ||
                overwrittenInstructions[2].Mnemonic != Mnemonic.Mov ||
                overwrittenInstructions[2].MemoryBase != Register.RBX ||
                overwrittenInstructions[2].MemoryDisplacement64 !=
                    ElevatedMoatNativeContract.UnitVerticalCorrectionOffset ||
                overwrittenInstructions[2].Op1Register != Register.AX ||
                returnAddress != overwrittenInstructions[2].NextIP)
            {
                throw new InvalidOperationException(
                    "The unit drawbridge height-correction instruction contract differs.");
            }

            Label vanillaCorrection = assembler.CreateLabel("unitDrawbridgeVanillaHeightCorrection");
            EmitEnabledFlagBranch(assembler, featureActiveFlagAddress, vanillaCorrection);

            // This hook is already behind Vanilla's building-type == 0x31 branch.
            // RBP is the image base. RBX is the unit-slot anchor formed from the
            // manager base plus game ID * 0x490; its +0x712/+0x714/+0x72C operands
            // correspond to GameUnit record fields +0xB6/+0xB8/+0xD0. RAX, RCX and
            // flags are dead at the common continuation. DefaultHeightGrid classifies
            // the bridge while Vanilla's downstream current-height flow stays intact.
            assembler.mov(ecx,
                __dword_ptr[rbx + ElevatedMoatNativeContract.UnitCurrentTileIdOffset]);
            assembler.movzx(eax,
                __byte_ptr[rbp + rcx + ElevatedMoatNativeContract.TileDefaultHeightGridRva]);
            assembler.cmp(eax, ElevatedMoatNativeContract.MaximumVanillaTerrainHeight);
            assembler.jbe(vanillaCorrection);

            assembler.mov(eax, ElevatedMoatNativeContract.MoatDepth);
            assembler.mov(
                __word_ptr[rbx + ElevatedMoatNativeContract.UnitVerticalCorrectionOffset],
                ax);
            assembler.AddUnrestrictedJmp(returnAddress);

            assembler.Label(ref vanillaCorrection);
            foreach (Instruction instruction in overwrittenInstructions)
                assembler.AddInstruction(instruction);
        }

        private static bool HasMemoryOperands(
            Instruction instruction,
            Register first,
            Register second) =>
            (instruction.MemoryBase == first && instruction.MemoryIndex == second) ||
            (instruction.MemoryBase == second && instruction.MemoryIndex == first);

        private static void EmitEnabledFlagBranch(
            Assembler assembler,
            ulong flagAddress,
            Label disabledTarget)
        {
            assembler.mov(rax, flagAddress);
            assembler.cmp(__byte_ptr[rax], 1);
            assembler.jne(disabledTarget);
        }
    }
}
