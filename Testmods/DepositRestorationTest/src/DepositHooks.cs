using System;
using Iced.Intel;
using RedBird.X64.Extensions;
using SHCDESE.Interop.Enums;
using static Iced.Intel.AssemblerRegisters;

namespace DepositRestorationTest
{
    internal static class DepositHooks
    {
        internal const string NativeHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const int TileManager = 0x405EDB0;
        internal const int Logic = 0x898400;
        internal const int Capacity = 320800;
        internal const int HistoryOffset = 16;
        internal const int FlagShift = 16;
        internal const int Deposits = (int)(TilePropertyFlag.HasStone | TilePropertyFlag.HasIron);
        internal const int StoredMask = Deposits >> FlagShift;

        internal enum Operation { CaptureManual, CaptureAi, Restore, Undo, ReplaceTerrain }

        internal sealed class Site
        {
            internal readonly string Name;
            internal readonly int Rva;
            internal readonly AssemblerRegister32 Tile;
            internal readonly Operation Action;
            internal readonly byte[] Bytes;

            internal Site(string name, int rva, AssemblerRegister32 tile, Operation action, string hex)
            {
                Name = name;
                Rva = rva;
                Tile = tile;
                Action = action;
                Bytes = new byte[hex.Length / 2];
                for (int i = 0; i < Bytes.Length; i++)
                    Bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
        }

        internal static readonly Site[] Sites =
        {
            new Site("ManualPlacement", 0x6F55D, r12d, Operation.CaptureManual, "25FFFFF0FF0FBAE8088903430FBF8C65006CBF00"),
            // After accepted validation, before any wall mutation or the Fixes stair hook.
            new Site("AiPlacement", 0x6CB85, edx, Operation.CaptureAi, "0FB6843AC0CADC0088843AA0E5D700"),
            new Site("Bulldozer", 0x63775, edi, Operation.Restore, "81A4BE00848900FFF4BEFF448BDF"),
            new Site("PlayerDefeat", 0x71F0D, ebx, Operation.Restore, "816500FFF4BEFF8BFB410FB606448BC3"),
            new Site("DirectClear", 0x71FF3, r10d, Operation.Restore, "4281A49100848900FFF4BEFF410FB6840AC0CADC00"),
            new Site("AreaDamage", 0x7E15A, edi, Operation.Restore, "4181A4BE00848900FFF4BEFF488D0DF3F40206"),
            new Site("AttackDamage", 0x7ED20, r12d, Operation.Restore, "4181A700848900FFF4BEFF488D0D2EE90206"),
            new Site("AreaClear", 0xED990, r14d, Operation.Restore, "4281A4B100848900FFF4BEFF41FFC3"),
            new Site("BuildingPreparation", 0x6D711, esi, Operation.Restore, "81A4B300848900F7F4BEFF420FB6840670B8E204"),
            new Site("BuildingFootprintReplacement", 0x5D78A, edi, Operation.Restore, "C6840F8029FA00008B8CB900848900"),
            new Site("StructureFootprintClear", 0x638D3, ebx, Operation.Restore, "81A49F00848900FFF0BEEF0FB7845F405FF000"),
            new Site("WallUndo", 0x63F25, r8d, Operation.Undo, "488D0C7F438B8486B0718F040B4604"),
            new Site("RockPlacement", 0x6D4B1, ecx, Operation.ReplaceTerrain, "4088AC39C0CADC0081A48F00848900B6FFC11F"),
        };

        internal static void Emit(Assembler asm, Site site, ulong state, ulong logic,
            ReadOnlySpan<Instruction> original)
        {
            Label done = asm.CreateLabel();
            asm.pushfq();
            asm.push(r11);
            asm.push(r10);
            asm.push(rcx);
            asm.push(rax);
            // Read the original r10/rcx/eax before using any of them as scratch.
            asm.mov(r11d, site.Tile);
            asm.mov(r10, state);
            asm.cmp(__byte_ptr[r10], 0);
            asm.je(done);
            asm.cmp(r11d, Capacity);
            asm.jae(done);
            asm.mov(rcx, logic);

            if (site.Action == Operation.CaptureManual || site.Action == Operation.CaptureAi)
            {
                if (site.Action == Operation.CaptureAi) asm.mov(eax, __dword_ptr[rcx + r11 * 4]);
                asm.test(eax, Deposits);
                asm.jz(done);
                asm.shr(eax, FlagShift);
                asm.and(eax, StoredMask);
                asm.mov(__byte_ptr[r10 + r11 + HistoryOffset], al);
            }
            else if (site.Action == Operation.ReplaceTerrain)
            {
                // Explicit rock placement owns the tile even if Vanilla retains a wall bit.
                asm.mov(__byte_ptr[r10 + r11 + HistoryOffset], 0);
            }
            else
            {
                if (site.Action == Operation.Undo)
                {
                    // Undo can revert a repair while retaining the wall.
                    asm.test(__dword_ptr[rsi + 4], (int)TilePropertyFlag.IsWall);
                    asm.jnz(done);
                }
                asm.movzx(eax, __byte_ptr[r10 + r11 + HistoryOffset]);
                asm.test(eax, eax);
                asm.jz(done);
                asm.mov(__byte_ptr[r10 + r11 + HistoryOffset], 0);
                asm.shl(eax, FlagShift);
                asm.or(__dword_ptr[rcx + r11 * 4], eax);
            }

            asm.Label(ref done);
            asm.pop(rax);
            asm.pop(rcx);
            asm.pop(r10);
            asm.pop(r11);
            asm.popfq();
            asm.AddInstructions(original);
        }
    }
}
