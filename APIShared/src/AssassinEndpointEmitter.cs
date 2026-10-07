using Iced.Intel;
using RedBird.X64.Extensions;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using static Iced.Intel.AssemblerRegisters;

namespace APIShared
{
    internal static class AssassinEndpointEmitter
    {
        internal static void Emit(Assembler assembler, ReadOnlySpan<Instruction> original,
            ulong module, ulong flagsAddress, int site)
        {
            if (site < 0 || site > 3 || original.Length < 2 ||
                original[0].Mnemonic != Mnemonic.Jne)
                throw new InvalidOperationException("Unexpected Assassin endpoint instructions.");
            Instruction[] replay = original.CloneInstructionsWithoutIP();
            Label vanilla = assembler.CreateLabel("vanilla");
            Label accept = assembler.CreateLabel("accept");
            Label reject = assembler.CreateLabel("reject");
            Label done = assembler.CreateLabel("done");
            assembler.pushfq();
            assembler.push(rax);
            assembler.push(r10);
            assembler.push(r11);
            assembler.mov(r11, flagsAddress);
            assembler.mov(r10d, __dword_ptr[r11]);
            assembler.test(r10d, site >= 2 ? 3 : 2);
            assembler.je(vanilla);
            // Weighted reconstruction already used this broad skip. Search remains gate-specific.
            if (site >= 2)
            {
                assembler.test(r10d, 1);
                assembler.jne(accept);
            }
            // Native start/neighbor tile registers, audited independently at each site.
            assembler.mov(r10, site == 1 ? r13 : site == 3 ? rcx : r8);
            assembler.cmp(r10, AssassinPathAPI.TileCount);
            assembler.jae(reject);
            assembler.mov(r11, module + AssassinPathAPI.BuildingGridRva);
            assembler.movzx(eax, __word_ptr[r11 + r10 * 2]);
            assembler.test(eax, eax);
            assembler.je(accept);
            assembler.cmp(eax, 3999);
            assembler.ja(reject);
            assembler.mov(r11, module + AssassinPathAPI.TileFlagsRva);
            assembler.test(__dword_ptr[r11 + r10 * 4], (int)TilePropertyFlag.IsWall);
            assembler.je(reject);
            assembler.imul(eax, eax, AssassinPathAPI.BuildingStride);
            assembler.mov(r11, module + AssassinPathAPI.BuildingAliveBaseRva);
            assembler.cmp(__word_ptr[r11 + rax], (int)AliveState.None);
            assembler.je(reject);
            assembler.cmp(__word_ptr[r11 + rax], (int)AliveState.MarkedForDeletion);
            assembler.je(reject);
            assembler.mov(r11, module + AssassinPathAPI.BuildingTypeBaseRva);
            assembler.movzx(eax, __word_ptr[r11 + rax]);
            assembler.cmp(eax, (int)eStructs.STRUCT_GATE_MAIN);
            assembler.je(accept);
            assembler.cmp(eax, (int)eStructs.STRUCT_GATE_INNER);
            assembler.jne(reject);
            assembler.Label(ref accept);
            Restore(assembler);
            // Drop only the building rejection branch. Every subsequent wall test is replayed.
            for (int index = 1; index < replay.Length; index++) assembler.AddInstruction(replay[index]);
            assembler.jmp(done);
            assembler.Label(ref reject);
            Restore(assembler);
            assembler.jmp(original[0].NearBranchTarget);
            assembler.Label(ref vanilla);
            Restore(assembler);
            foreach (Instruction instruction in replay) assembler.AddInstruction(instruction);
            assembler.Label(ref done);
            assembler.nop();
        }

        private static void Restore(Assembler assembler)
        {
            assembler.pop(r11);
            assembler.pop(r10);
            assembler.pop(rax);
            assembler.popfq();
        }
    }
}
