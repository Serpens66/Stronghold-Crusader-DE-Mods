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
            ulong buildingManagerAddress)
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

            // Argument 2 is the drawbridge building ID. Use Vanilla's common building
            // height for every rigid drawbridge part, including sloped footprints.
            // Preserve R10 while using it as the record index; RAX is overwritten by
            // the first displaced prologue instruction.
            assembler.push(r10);
            assembler.mov(r10d, edx);
            assembler.imul(r10, r10, ElevatedMoatNativeContract.BuildingRecordStride);
            assembler.mov(rax, buildingManagerAddress);
            assembler.movzx(eax,
                __word_ptr[rax + r10 + ElevatedMoatNativeContract.BuildingHeightOffset]);
            assembler.pop(r10);
            assembler.cmp(eax, ElevatedMoatNativeContract.MaximumVanillaTerrainHeight);
            assembler.jbe(vanillaPrologue);

            // R9 and stack argument 7 are the two height-blind coordinates. Vanilla's
            // low-terrain deck reference is eight, so translate both by H - 8.
            assembler.sub(eax, ElevatedMoatNativeContract.MoatDepth);
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
            ulong buildingManagerAddress)
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

            // This call site is reached only for a drawbridge (building type 0x31).
            // EDX still contains the building ID. Preserve R10 while resolving the
            // common Vanilla building height and pass 8 - H as argument 6.
            assembler.push(r10);
            assembler.mov(r10d, edx);
            assembler.imul(r10, r10, ElevatedMoatNativeContract.BuildingRecordStride);
            assembler.mov(rax, buildingManagerAddress);
            assembler.movzx(eax,
                __word_ptr[rax + r10 + ElevatedMoatNativeContract.BuildingHeightOffset]);
            assembler.pop(r10);
            assembler.cmp(eax, ElevatedMoatNativeContract.MaximumVanillaTerrainHeight);
            assembler.jbe(vanillaHeight);

            assembler.sub(eax, ElevatedMoatNativeContract.MoatDepth);
            assembler.neg(eax);
            assembler.mov(__dword_ptr[rsp + 0x28], eax);
            assembler.mov(__dword_ptr[rsp + 0x20], r15d);
            assembler.AddUnrestrictedJmp(returnAddress);

            assembler.Label(ref vanillaHeight);
            assembler.mov(__dword_ptr[rsp + 0x28], esi);
            assembler.mov(__dword_ptr[rsp + 0x20], r15d);
        }

        internal static void GenerateStaticRendererArguments(
            Assembler assembler,
            ReadOnlySpan<Instruction> overwrittenInstructions,
            ulong returnAddress,
            ulong featureActiveFlagAddress,
            ulong buildingManagerAddress)
        {
            if (overwrittenInstructions.Length != 3 ||
                overwrittenInstructions[0].Length != 7 ||
                overwrittenInstructions[0].Mnemonic != Mnemonic.Sub ||
                overwrittenInstructions[0].Op0Register != Register.R10D ||
                overwrittenInstructions[0].MemoryBase != Register.RIP ||
                overwrittenInstructions[0].MemoryDisplacement64 !=
                    buildingManagerAddress - ElevatedMoatNativeContract.BuildingManagerRva +
                        ElevatedMoatNativeContract.CurrentRenderedTileHeightRva ||
                overwrittenInstructions[1].Length != 2 ||
                overwrittenInstructions[1].Mnemonic != Mnemonic.Mov ||
                overwrittenInstructions[1].Op0Register != Register.EDX ||
                overwrittenInstructions[1].Op1Register != Register.EDI ||
                overwrittenInstructions[2].Length != 7 ||
                overwrittenInstructions[2].Mnemonic != Mnemonic.Mov ||
                overwrittenInstructions[2].Op0Register != Register.R9D ||
                overwrittenInstructions[2].MemoryBase != Register.RIP ||
                returnAddress != overwrittenInstructions[2].NextIP)
            {
                throw new InvalidOperationException(
                    "The drawbridge static-renderer argument contract differs.");
            }

            Label vanillaHeight = assembler.CreateLabel("drawbridgeStaticRendererVanillaHeight");
            Label commonArguments = assembler.CreateLabel("drawbridgeStaticRendererCommonArguments");
            EmitEnabledFlagBranch(assembler, featureActiveFlagAddress, vanillaHeight);

            // EDI is the drawbridge building ID. The static sibling renderer expects
            // shadow - height, so elevated bridges subtract the common building height.
            assembler.push(r11);
            assembler.mov(r11d, edi);
            assembler.imul(r11, r11, ElevatedMoatNativeContract.BuildingRecordStride);
            assembler.mov(rax, buildingManagerAddress);
            assembler.movzx(eax,
                __word_ptr[rax + r11 + ElevatedMoatNativeContract.BuildingHeightOffset]);
            assembler.pop(r11);
            assembler.cmp(eax, ElevatedMoatNativeContract.MaximumVanillaTerrainHeight);
            assembler.jbe(vanillaHeight);

            assembler.sub(r10d, eax);
            assembler.jmp(commonArguments);

            assembler.Label(ref vanillaHeight);
            assembler.AddInstruction(overwrittenInstructions[0]);

            assembler.Label(ref commonArguments);
            assembler.AddInstruction(overwrittenInstructions[1]);
            assembler.AddInstruction(overwrittenInstructions[2]);
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

            // This hook is already behind Vanilla's building-type == 0x31 branch and
            // EDI contains its building ID. RBP is the image base. RBX is the unit-slot
            // anchor formed from the manager base plus game ID * 0x490; its +0x712 and
            // +0x714 operands correspond to GameUnit record fields +0xB6 and +0xB8. RAX, RCX and
            // flags are dead at the common continuation. Use the same building height
            // as every drawbridge renderer, then subtract the unit's current elevation.
            assembler.mov(ecx, edi);
            assembler.imul(rcx, rcx, ElevatedMoatNativeContract.BuildingRecordStride);
            assembler.movzx(eax,
                __word_ptr[rbp + rcx + ElevatedMoatNativeContract.BuildingManagerRva +
                    ElevatedMoatNativeContract.BuildingHeightOffset]);
            assembler.cmp(eax, ElevatedMoatNativeContract.MaximumVanillaTerrainHeight);
            assembler.jbe(vanillaCorrection);

            assembler.sub(ax,
                __word_ptr[rbx + ElevatedMoatNativeContract.UnitCurrentElevationOffset]);
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
