using Iced.Intel;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL
{
    internal static class AiOutpostRetargetNative
    {
        internal const int HookRva = 0x30675, ContinueRva = 0x3068B;
        internal const int SelectorRva = 0x2A720, ValidLordRva = 0x187E60;
        internal const int AiManagerRva = 0x404C950, UnitManagerRva = 0x67E8400, TribeManagerRva = 0x7CC6720;
        internal const int TribeOrigin = 0x2A, TribeStride = 0x688;
        internal const int TargetPlayerOffset = 0x620, CachedBuildingOffset = 0x622, CachedGlobalOffset = 0x626;
        internal const int LordSlotsRva = 0x3A0F9F0, LordGlobalsRva = 0x3A0FA04;
        internal const int UnitStride = 0x490, UnitOrigin = 0x65C;
        internal const int UnitAlive = UnitOrigin + 0x88, UnitGlobal = UnitOrigin + 0x94, UnitDeath = UnitOrigin + 0x29C;
        internal const int LossSlotZeroRva = 0x379D014, PlayerStride = 0x583C, LossOffset = 0x2244;
        internal static readonly byte[] Original = {
            0x48, 0x89, 0x9C, 0x24, 0x98, 0x00, 0x00, 0x00, 0x48, 0x89, 0x6C, 0x24, 0x58,
            0x41, 0x0F, 0xBF, 0xAC, 0x3E, 0x4A, 0x06, 0x00, 0x00
        };

        // RSP is 16-byte aligned here. Save three volatile registers and flags before
        // inspecting the current target, so the common path never enters managed code.
        internal static void EmitStub(Assembler a, ReadOnlySpan<Instruction> displaced, ulong enabled,
            ulong lossSlotZero, ulong lordSlots, ulong lordGlobals, ulong units, ulong retarget)
        {
            if (displaced.Length != 3 || displaced[0].Length != 8 || displaced[1].Length != 5 ||
                displaced[2].Length != 9 || displaced[2].Mnemonic != Mnemonic.Movsx ||
                displaced[2].Op0Register != Register.EBP || displaced[2].MemoryBase != Register.R14 ||
                displaced[2].MemoryIndex != Register.RDI ||
                displaced[2].MemoryDisplacement64 != TribeOrigin + TargetPlayerOffset)
                throw new InvalidOperationException("Unexpected displaced Outpost target load.");

            var resume = a.CreateLabel();
            var reselect = a.CreateLabel();
            a.pushfq();
            a.push(rax);
            a.push(r10);
            a.push(r11);
            a.mov(rax, enabled);
            a.cmp(__byte_ptr[rax], 0);
            a.je(resume);
            a.movzx(eax, __word_ptr[r14 + rdi + TribeOrigin + TargetPlayerOffset]);
            a.sub(eax, 1);
            a.cmp(eax, GamePlayerManagerAPI.MAX_PLAYERS - 1);
            a.ja(resume);
            a.mov(r11d, eax);
            a.imul(eax, eax, PlayerStride);
            a.mov(r10, lossSlotZero + PlayerStride);
            a.cmp(__dword_ptr[r10 + rax], (int)WinLossState.Loss);
            a.je(reselect);

            // WinLoss is normally assigned only at match end. Inline Vanilla's
            // Lord-identity test so a mid-match loss also reaches the slow path.
            a.mov(r10, lordSlots + sizeof(short));
            a.movzx(eax, __word_ptr[r10 + r11 * 2]);
            a.test(eax, eax);
            a.je(reselect);
            a.cmp(eax, GameUnitManager.LastLiveUnitId);
            a.ja(resume);
            a.imul(eax, eax, UnitStride);
            a.mov(r10, lordGlobals + sizeof(uint));
            a.mov(r11d, __dword_ptr[r10 + r11 * 4]);
            a.mov(r10, units);
            a.cmp(__dword_ptr[r10 + rax + UnitGlobal], r11d);
            a.jne(reselect);
            a.cmp(__word_ptr[r10 + rax + UnitAlive], (int)AliveState.NeedsInit);
            a.je(resume);
            a.cmp(__word_ptr[r10 + rax + UnitAlive], (int)AliveState.IsAlive);
            a.jne(reselect);
            a.cmp(__word_ptr[r10 + rax + UnitDeath], 0);
            a.je(resume);

            a.Label(ref reselect);
            // 32 bytes of Win64 shadow space, then volatile arguments and XMM0..5.
            a.sub(rsp, 0xA0);
            a.mov(__qword_ptr[rsp + 0x20], rcx);
            a.mov(__qword_ptr[rsp + 0x28], rdx);
            a.mov(__qword_ptr[rsp + 0x30], r8);
            a.mov(__qword_ptr[rsp + 0x38], r9);
            a.movdqu(__xmmword_ptr[rsp + 0x40], xmm0);
            a.movdqu(__xmmword_ptr[rsp + 0x50], xmm1);
            a.movdqu(__xmmword_ptr[rsp + 0x60], xmm2);
            a.movdqu(__xmmword_ptr[rsp + 0x70], xmm3);
            a.movdqu(__xmmword_ptr[rsp + 0x80], xmm4);
            a.movdqu(__xmmword_ptr[rsp + 0x90], xmm5);
            a.mov(rcx, r13);
            a.mov(edx, esi);
            a.mov(rax, retarget);
            a.call(rax);
            a.movdqu(xmm0, __xmmword_ptr[rsp + 0x40]);
            a.movdqu(xmm1, __xmmword_ptr[rsp + 0x50]);
            a.movdqu(xmm2, __xmmword_ptr[rsp + 0x60]);
            a.movdqu(xmm3, __xmmword_ptr[rsp + 0x70]);
            a.movdqu(xmm4, __xmmword_ptr[rsp + 0x80]);
            a.movdqu(xmm5, __xmmword_ptr[rsp + 0x90]);
            a.mov(rcx, __qword_ptr[rsp + 0x20]);
            a.mov(rdx, __qword_ptr[rsp + 0x28]);
            a.mov(r8, __qword_ptr[rsp + 0x30]);
            a.mov(r9, __qword_ptr[rsp + 0x38]);
            a.add(rsp, 0xA0);

            a.Label(ref resume);
            a.pop(r11);
            a.pop(r10);
            a.pop(rax);
            a.popfq();
            // Read the target only after the slow path has had a chance to replace it.
            foreach (var instruction in displaced) a.AddInstruction(instruction);
        }
    }
}
