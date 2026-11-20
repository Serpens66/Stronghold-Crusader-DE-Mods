using System;
using Iced.Intel;
using RedBird.X64.Extensions;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL.Multiplayer
{
    internal static class MoatLandRotationNative
    {
        internal const int Rva = 0x7C8EE;
        internal const int Length = 15;
        internal const int FunctionRva = 0x7C0A0;
        internal const int FunctionLength = 0x1067;
        internal const string FunctionSha256 = "1740D57D654EDDF7EDB08A2089158D41B9CE3D9A54092CD57C5D351DCF5A1276";
        internal const int Height = 0xD7E5A0, Changed = 0xA20D40;
        internal const int RowCount = 800;
        internal const int ReferenceNeighbour = 3;
        internal const int NeighboursPerRow = 8;
        internal const int LowHeightLimit = 8;
        internal const int LandSoilMask = 0xDF;
        private const int OriginalRowStackOffset = 0x20;
        private const int SavedBytes = 6 * sizeof(long);
        private const int LandBitIndex = 15;
        internal static readonly byte[] Entry =
        {
            0x44, 0x8B, 0x6C, 0x24, 0x20, 0x41, 0xFF, 0xC0,
            0x44, 0x8B, 0x7C, 0x24, 0x34, 0xFF, 0xC6
        };

        internal static void Validate(ReadOnlySpan<byte> image)
        {
            if (image.Length < Rva + Length || !image.Slice(Rva, Length).SequenceEqual(Entry) ||
                GameTileManagerView.NativePackedTileCapacity != 320800 ||
                (uint)(TilePropertyFlag.RealityEdge | TilePropertyFlag.MapBorder) != 0x30 ||
                (uint)TilePropertyFlag.IsLand != 1u << LandBitIndex ||
                (uint)TilePropertyFlag.IsMoat != 0x40000000 || (uint)TilePropertyFlag.River != 0x100000)
                throw new InvalidOperationException("Audited moat land rotation span, capacity or flags differ.");
        }

        internal static void Emit(Assembler asm, ulong enabledAddress, ReadOnlySpan<Instruction> original)
        {
            EmitCorrection(asm, enabledAddress);
            asm.AddInstructions(original);
        }

        // The real loop owns RBX (TileManager), R12 (packed ID) and [original RSP+20] (row).
        // The tail is shared with tiles Vanilla skipped, so repeat its border/dirty gates.
        internal static void EmitCorrection(Assembler asm, ulong enabledAddress)
        {
            const int logic = MoatTerrain.NativeDefinition.Logic;
            const int soil = MoatTerrain.NativeDefinition.Soil;
            var land = __dword_ptr[rbx + r12 * 4 + logic];
            var skip = asm.CreateLabel();
            var done = asm.CreateLabel();
            var setLand = asm.CreateLabel();
            var clearLand = asm.CreateLabel();
            var search = asm.CreateLabel();
            var next = asm.CreateLabel();
            var heightDiffers = asm.CreateLabel();
            var comparable = asm.CreateLabel();

            asm.pushfq();
            asm.push(rax);
            asm.mov(rax, enabledAddress);
            asm.cmp(__dword_ptr[rax], 0);
            asm.je(skip);

            // Test the full 64-bit tile ID before using it as an address index.
            asm.cmp(r12, GameTileManagerView.NativePackedTileCapacity);
            asm.jae(skip);
            asm.test(land, (int)TilePropertyFlag.IsMoat);
            asm.je(skip);
            asm.test(land, (int)(TilePropertyFlag.RealityEdge | TilePropertyFlag.MapBorder));
            asm.jne(skip);
            asm.cmp(__byte_ptr[rbx + r12 + Changed], 0);
            asm.je(skip);
            asm.cmp(__byte_ptr[rbx + r12 + Height], 0);
            asm.je(skip);

            asm.push(rcx);
            asm.push(rdx);
            asm.push(r10);
            asm.push(r11);
            asm.movzx(r10d, __byte_ptr[rbx + r12 + Height]);

            // Base store at 0x7C1FF. For positive height its land result depends only on height and soil.
            asm.cmp(r10d, LowHeightLimit);
            asm.jbe(search);
            asm.test(__byte_ptr[rbx + r12 + soil], LandSoilMask);
            asm.je(clearLand);

            // Second store at 0x7C320. Only substitute the camera-dependent neighbour at 0x7C211.
            asm.Label(ref search);
            asm.mov(ecx, __dword_ptr[rsp + OriginalRowStackOffset + SavedBytes]);
            asm.cmp(ecx, RowCount);
            asm.jae(setLand);
            asm.shl(ecx, 5);
            asm.add(rcx, rbx);
            asm.mov(eax, __dword_ptr[rcx + ReferenceNeighbour * sizeof(int)]);
            asm.add(eax, r12d);
            asm.cmp(eax, GameTileManagerView.NativePackedTileCapacity);
            asm.jae(setLand);
            asm.test(__dword_ptr[rbx + rax * 4 + logic], (int)TilePropertyFlag.IsMoat);
            asm.je(setLand);
            asm.xor(r11d, r11d);

            asm.Label(ref heightDiffers);
            asm.mov(eax, __dword_ptr[rcx + r11 * 4]);
            asm.add(eax, r12d);
            asm.cmp(eax, GameTileManagerView.NativePackedTileCapacity);
            asm.jae(next);
            asm.movzx(edx, __byte_ptr[rbx + rax + Height]);
            asm.cmp(r10d, LowHeightLimit);
            asm.ja(comparable);
            asm.test(edx, edx);
            asm.jne(next);
            asm.Label(ref comparable);
            asm.cmp(r10d, edx);
            asm.je(next);
            asm.test(__dword_ptr[rbx + rax * 4 + logic], (int)(TilePropertyFlag.IsMoat | TilePropertyFlag.River));
            asm.jne(setLand);
            asm.jmp(clearLand);

            asm.Label(ref next);
            asm.inc(r11d);
            asm.cmp(r11d, NeighboursPerRow);
            asm.jb(heightDiffers);
            asm.Label(ref setLand);
            asm.bts(land, LandBitIndex);
            asm.jmp(done);
            asm.Label(ref clearLand);
            asm.btr(land, LandBitIndex);
            asm.Label(ref done);
            asm.pop(r11);
            asm.pop(r10);
            asm.pop(rdx);
            asm.pop(rcx);
            asm.Label(ref skip);
            asm.pop(rax);
            asm.popfq();
        }
    }
}
