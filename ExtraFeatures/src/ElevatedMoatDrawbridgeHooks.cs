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
            ulong imageBase,
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
            Label applyElevatedHeight = assembler.CreateLabel("completedDrawbridgeApplyElevatedHeight");
            EmitEnabledFlagBranch(assembler, featureActiveFlagAddress, vanillaWrite);

            // Vanilla's allocator stored the already calculated building height in the
            // new record. R13 is its building id; RAX/RDX are dead after the state call.
            assembler.mov(rax, r13);
            assembler.imul(rax, rax, ElevatedMoatNativeContract.BuildingRecordStride);
            assembler.mov(rdx, imageBase + ElevatedMoatNativeContract.BuildingHeightAddressRva);
            assembler.movzx(eax, __word_ptr[rdx + rax]);
            assembler.cmp(eax, ElevatedMoatNativeContract.MaximumVanillaTerrainHeight);
            assembler.jbe(vanillaWrite);
            assembler.add(eax, ElevatedMoatNativeContract.DrawbridgeDeckHeightOffset);
            assembler.cmp(eax, ElevatedMoatNativeContract.MaximumTileHeight);
            assembler.jbe(applyElevatedHeight);
            assembler.mov(eax, ElevatedMoatNativeContract.MaximumTileHeight);

            assembler.Label(ref applyElevatedHeight);
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
            Label applyElevatedHeight = assembler.CreateLabel("loweredDrawbridgeApplyElevatedHeight");
            Label restoreImageBase = assembler.CreateLabel("loweredDrawbridgeRestoreImageBase");
            EmitEnabledFlagBranch(assembler, featureActiveFlagAddress, vanillaWrite);

            // R13 is already buildingId * BuildingRecordStride in this function.
            // RAX is volatile and dead here; the untouched INC replaces flags.
            assembler.mov(rax, imageBase + ElevatedMoatNativeContract.BuildingHeightAddressRva);
            assembler.movzx(eax, __word_ptr[rax + r13]);
            assembler.cmp(eax, ElevatedMoatNativeContract.MaximumVanillaTerrainHeight);
            assembler.jbe(vanillaWrite);
            assembler.add(eax, ElevatedMoatNativeContract.DrawbridgeDeckHeightOffset);
            assembler.cmp(eax, ElevatedMoatNativeContract.MaximumTileHeight);
            assembler.jbe(applyElevatedHeight);
            assembler.mov(eax, ElevatedMoatNativeContract.MaximumTileHeight);

            assembler.Label(ref applyElevatedHeight);
            assembler.mov(__byte_ptr[rbx + rdi + ElevatedMoatNativeContract.TileHeightGridOffset], al);
            assembler.jmp(restoreImageBase);

            assembler.Label(ref vanillaWrite);
            assembler.AddInstruction(overwrittenInstructions[0]);

            assembler.Label(ref restoreImageBase);
            assembler.AddInstruction(overwrittenInstructions[1]);
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
            assembler.push(rax);
            assembler.mov(rax, flagAddress);
            assembler.cmp(__byte_ptr[rax], 1);
            assembler.pop(rax);
            assembler.jne(disabledTarget);
        }
    }
}
