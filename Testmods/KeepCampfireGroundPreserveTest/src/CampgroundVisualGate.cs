using System;
using Iced.Intel;
using RedBird.X64.Extensions;
using static Iced.Intel.AssemblerRegisters;

namespace KeepCampfireGroundPreserveTest
{
    internal static class CampgroundVisualGate
    {
        internal const int FunctionRva = 0x6E620;
        internal const int HookRva = 0x6F0A0;
        internal const int ContinueRva = 0x6F0B0;
        internal const int SkipRva = 0x6F0D8;
        internal const int DisplacedBytes = 16;
        internal const int BuildingTypeOffsetFromImage = 0x64CCCDE;
        internal const int CampgroundType = 0x37;
        // These five adjacent tiles are the dark centre and four stone/fire
        // edges in the installed GM_BUILDINGS1 sprites. This is a game-test
        // candidate; the separate cauldron/flame effects still need checking.
        internal static readonly int[] FirePatchGraphics = {
            0x00060029, 0x0006002A, 0x00060030, 0x00060036, 0x00060037
        };

        internal static readonly byte[] HookBytes = {
            0x42, 0x89, 0x94, 0x87, 0x00, 0x09, 0x14, 0x00,
            0x42, 0x8B, 0x8C, 0x13, 0xB4, 0xCC, 0x4C, 0x06
        };

        // The hook is at the first store of the variant-0x0F generic building
        // branch. Skipping to the common loop tail also skips its AlphaGFX store.
        // All scratch setup and structure/logic changes have already run.
        internal static void Generate(Assembler assembler, ReadOnlySpan<Instruction> original,
            ulong continuation, ulong enableAddress, ulong skipAddress)
        {
            if (original.Length != 2 || original[0].IP + (uint)DisplacedBytes != continuation ||
                original[0].Length != 8 || original[1].Length != 8 ||
                original[0].Mnemonic != Mnemonic.Mov || original[1].Mnemonic != Mnemonic.Mov ||
                original[0].MemoryDisplacement64 != 0x140900 ||
                original[1].MemoryDisplacement64 != BuildingTypeOffsetFromImage - 0x2A)
                throw new InvalidOperationException("Campground graphic-store instruction contract changed.");

            Label vanilla = assembler.CreateLabel("campgroundVanilla");
            Label firePatch = assembler.CreateLabel("campgroundFirePatch");
            assembler.pushfq();
            assembler.push(rax);
            assembler.mov(rax, enableAddress);
            assembler.cmp(__dword_ptr[rax], 0);
            assembler.je(vanilla);
            assembler.cmp(__word_ptr[rbx + r10 + BuildingTypeOffsetFromImage], CampgroundType);
            assembler.jne(vanilla);
            foreach (int graphic in FirePatchGraphics) {
                assembler.cmp(edx, graphic);
                assembler.je(firePatch);
            }
            assembler.inc(__qword_ptr[rax + 8]);
            assembler.pop(rax);
            assembler.popfq();
            assembler.AddUnrestrictedJmp(skipAddress);

            assembler.Label(ref firePatch);
            assembler.inc(__qword_ptr[rax + 16]);
            assembler.jmp(vanilla);
            assembler.Label(ref vanilla);
            assembler.pop(rax);
            assembler.popfq();
            foreach (Instruction instruction in original)
                assembler.AddInstruction(instruction);
            assembler.AddUnrestrictedJmp(continuation);
        }
    }
}
