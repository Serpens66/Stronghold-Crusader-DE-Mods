using Iced.Intel;
using RedBird.X64.Extensions;
using SHCDESE.Interop.Enums;
using System;
using System.IO;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL.TreeBlightDecay
{
    // All offsets describe the hash-bound native manager/record, not the SE vegetation ID view.
    internal static class NativeCode
    {
        internal const string Hash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const int EventRva = 0x107A6C, GrowthRva = 0x10838E, Span = 14;
        internal const int GrowthSkip = 0x1083B0, GrowthExit = 0x108645;
        internal const int TableRva = 0x31E220, ModeRva = 0x3665F28, ScreenRva = 0x3665ED8;
        internal const int TypeOffset = 0x6A, AliveOffset = 0x68, StageOffset = 0xA4, ProgressOffset = 0xA8;
        internal static readonly byte[] EventBytes = { 0x41,0xFF,0xC1,0x49,0x81,0xC0,0x9C,0,0,0,0x45,0x3B,0x4A,0x10 };
        internal static readonly byte[] GrowthBytes = { 0x66,0x83,0xBB,0x9A,0,0,0,0,0x0F,0x85,0xA9,0x02,0,0 };
        internal static readonly byte[] EventFollowing = { 0x7C,0x9A };
        internal static readonly byte[] GrowthFollowing = { 0x0F,0xB7,0x83,0x98,0,0,0 };

        internal static Instruction[] Decode(byte[] bytes, ulong ip)
        {
            var d = Decoder.Create(64, new ByteArrayCodeReader(bytes)); d.IP = ip;
            var result = new System.Collections.Generic.List<Instruction>();
            while (d.IP < ip + (ulong)bytes.Length)
            {
                var instruction = d.Decode();
                if (instruction.IsInvalid || d.IP > ip + (ulong)bytes.Length) throw new InvalidOperationException("Invalid tree-blight instruction boundary.");
                result.Add(instruction);
            }
            return result.ToArray();
        }

        internal static byte[] Assemble(Assembler a, ulong ip)
        {
            using (var s = new MemoryStream())
            {
                if (!a.TryAssemble(new StreamCodeWriter(s), ip, out string error, out _)) throw new InvalidOperationException(error);
                byte[] bytes = s.ToArray(); Decode(bytes, ip); return bytes;
            }
        }

        // state: enabled Int32 at 0; table pointer at 8; native game-mode pointer at 16;
        // frontend-state pointer at 24. No calls, SIMD, allocation or RNG in either stub.
        internal static void Emit(Assembler a, bool growth, ulong state, ulong skipHealth, ReadOnlySpan<Instruction> original)
        {
            var vanilla = a.CreateLabel(); var restoreVanilla = a.CreateLabel(); var keepProgress = a.CreateLabel();
            a.pushfq(); a.push(rax); a.push(rcx);
            a.mov(rax, state); a.cmp(__dword_ptr[rax], 0); a.je(restoreVanilla);
            a.mov(rcx, __qword_ptr[rax + 16]);
            a.cmp(__dword_ptr[rcx], (int)eGameTypeModes.GAMETYPE_BUILDER); a.je(restoreVanilla);
            a.cmp(__dword_ptr[rcx], (int)eGameTypeModes.GAMETYPE_SIEGE_THAT_BUILDER); a.je(restoreVanilla);
            if (growth)
            {
                a.cmp(__word_ptr[rbx + AliveOffset], (int)AliveState.IsAlive); a.jne(restoreVanilla);
                a.movzx(ecx, __word_ptr[rbx + TypeOffset]);
            }
            else
            {
                // R8 points at this slot's vegetation type; examine it before Vanilla advances R8.
                a.cmp(__word_ptr[r8 - 2], 0); a.je(restoreVanilla);
                a.movzx(ecx, __word_ptr[r8]);
            }
            a.cmp(ecx, (int)VegetationType.ChestnutTree); a.jb(restoreVanilla);
            a.cmp(ecx, (int)VegetationType.BirchTree); a.ja(restoreVanilla);
            if (growth)
            {
                a.cmp(__dword_ptr[rbx + StageOffset], (int)TreeGrowthStage.Stage5); a.jne(restoreVanilla);
                // Do not repair a saved counter while a remaining Vanilla gate blocks growth.
                // Pause/scenario and random-bucket gates precede this interception site.
                a.cmp(__dword_ptr[rdi + 4], 0); a.je(restoreVanilla);
                a.mov(rcx, __qword_ptr[rax + 24]); a.cmp(__dword_ptr[rcx], 0xC); a.je(restoreVanilla);
                a.movzx(ecx, __word_ptr[rbx + TypeOffset]);
            }
            else
            {
                a.cmp(__dword_ptr[r8 + StageOffset - TypeOffset], (int)TreeGrowthStage.Stage5); a.jne(restoreVanilla);
            }
            a.mov(rax, __qword_ptr[rax + 8]);
            a.imul(ecx, ecx, 7 * sizeof(int));
            a.mov(ecx, __dword_ptr[rax + rcx + (int)TreeGrowthStage.Stage5 * sizeof(int)]);
            if (growth)
            {
                a.cmp(__dword_ptr[rbx + ProgressOffset], ecx); a.jge(keepProgress);
                a.mov(__dword_ptr[rbx + ProgressOffset], ecx);
            }
            else
            {
                // Newly blighted Vanilla stages 2..4 lie below this floor. Old stage-5
                // trees in the same existing loop retain any later decay progress.
                a.cmp(__dword_ptr[r8 + ProgressOffset - TypeOffset], ecx); a.jge(keepProgress);
                a.mov(__dword_ptr[r8 + ProgressOffset - TypeOffset], ecx);
            }
            a.Label(ref keepProgress);
            Restore(a);
            if (growth) a.jmp(skipHealth); else a.jmp(vanilla);
            a.Label(ref restoreVanilla);
            Restore(a);
            a.Label(ref vanilla);
            a.AddInstructions(original.CloneInstructionsWithoutIP());
        }

        private static void Restore(Assembler a) { a.pop(rcx); a.pop(rax); a.popfq(); }
    }
}
