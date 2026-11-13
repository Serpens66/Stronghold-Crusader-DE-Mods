using Iced.Intel;
using RedBird.X64.Extensions;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using static Iced.Intel.AssemblerRegisters;

namespace ExtraFeatures
{
    internal static class MatureTreeHarvestNative
    {
        internal const string Hash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const int CurrentWorkerRva = 0x9302C4;
        internal const int Stride = 0x9C, ViewOffset = 0x18, Slots = 4000;
        internal const int Alive = 0x68, Species = 0x6A, Health = 0x96, MaxHealth = 0x98, Felled = 0x9A, Stage = 0xA4;
        internal enum Path { Search, Arrival, Strike }

        internal sealed class Site
        {
            internal readonly Path Kind;
            internal readonly int Rva, Reject;
            internal readonly byte[] Original, Following, RejectBytes;

            internal Site(Path kind, int rva, int reject, byte[] original, byte[] following, byte[] rejectBytes)
            {
                Kind = kind;
                Rva = rva;
                Reject = reject;
                Original = original;
                Following = following;
                RejectBytes = rejectBytes;
            }
        }

        internal static readonly Site[] Sites =
        {
            new Site(Path.Search, 0x107EC5, 0x107FEA,
                new byte[] { 0x83,0x7E,0x3C,0x04,0x0F,0x8D,0x1B,0x01,0,0,0x66,0x83,0x3E,0x02,0x0F,0x85,0x11,0x01,0,0 },
                new byte[] { 0x44,0x8B,0x46,0x08 },
                new byte[] { 0x41,0xFF,0xC7 }),
            new Site(Path.Arrival, 0x12BFCF, 0x12C03D,
                new byte[] { 0x85,0xC0,0x48,0x63,0x05,0xEC,0x42,0x80,0,0x74,0x20,0x48,0x69,0xD8,0x90,0x04,0,0 },
                new byte[] { 0x41,0xBF,0x02,0,0,0 },
                new byte[] { 0x48,0x69,0xD8,0x90,0x04,0,0,0x66,0x42,0x89,0xB4,0x23,0x18,0x09,0,0 }),
            new Site(Path.Strike, 0x12C1D8, 0x12C2B3,
                new byte[] { 0x85,0xC0,0x8B,0xD5,0x48,0x63,0x05,0xE1,0x40,0x80,0,0x0F,0x84,0xA8,0,0,0 },
                new byte[] { 0x48,0x69,0xC8,0x90,0x04,0,0 },
                new byte[] { 0x48,0x69,0xC8,0x90,0x04,0,0,0x66,0x89,0xB4,0x19,0x18,0x09,0,0 })
        };

        internal static Instruction[] Decode(byte[] bytes, ulong ip)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = ip;
            ulong end = ip + (uint)bytes.Length;
            var instructions = new List<Instruction>();
            while (decoder.IP < end)
            {
                Instruction instruction = decoder.Decode();
                if (instruction.IsInvalid || decoder.IP > end)
                    throw new InvalidOperationException("Mature tree instruction boundary changed.");
                instructions.Add(instruction);
            }
            return instructions.ToArray();
        }

        internal static Instruction[] DecodeAndValidate(Site site, ulong address, ulong image)
        {
            Instruction[] instructions = Decode(site.Original, address);
            bool valid = site.Kind == Path.Search
                ? instructions[1].NearBranchTarget == image + (uint)site.Reject &&
                  instructions[3].NearBranchTarget == image + (uint)site.Reject
                : instructions[site.Kind == Path.Arrival ? 1 : 2].IPRelativeMemoryAddress == image + CurrentWorkerRva &&
                  instructions[site.Kind == Path.Arrival ? 2 : 3].NearBranchTarget ==
                  image + (site.Kind == Path.Arrival ? 0x12BFFAUL : 0x12C291UL);
            if (!valid)
                throw new InvalidOperationException("Mature tree native branch or operand changed: " + site.Kind);
            return instructions;
        }

        internal static byte[] Assemble(Assembler assembler, ulong ip)
        {
            using (var stream = new MemoryStream())
            {
                if (!assembler.TryAssemble(new StreamCodeWriter(stream), ip, out string error, out _))
                    throw new InvalidOperationException(error);
                return stream.ToArray();
            }
        }

        internal static void Emit(Assembler a, Path path, ulong enabled, ulong currentWorker, ulong reject,
            ReadOnlySpan<Instruction> original)
        {
            var vanilla = a.CreateLabel();
            var replay = a.CreateLabel();

            int offset = 0;
            if (path == Path.Search)
            {
                // RSI points at the candidate's alive field. Dead/old entries take the original exit.
                // Vanilla replay sets the comparison flags again. The reject path overwrites them.
                a.cmp(__word_ptr[rsi], (int)AliveState.IsAlive); a.jne(replay);
                a.cmp(__dword_ptr[rsi + Stage - Alive], (int)TreeGrowthStage.Stage3); a.jae(replay);
                offset = Alive;
            }

            a.pushfq(); a.push(rax); a.push(rcx);
            // Check the flag without overwriting the standing predicate result in EAX.
            a.mov(rcx, enabled); a.cmp(__dword_ptr[rcx], 0); a.je(vanilla);
            if (path != Path.Search)
            {
                // The standing predicate just checked the serial and felled flag. A false result
                // must still reach Vanilla's resource-only fallback for already felled trees.
                a.test(eax, eax); a.je(vanilla);
            }

            if (path == Path.Search)
                a.mov(rcx, rsi);
            else
            {
                a.cmp(ebp, 1); a.jb(vanilla);
                a.cmp(ebp, Slots); a.jae(vanilla);
                a.movsxd(rcx, ebp);
                a.imul(rcx, rcx, Stride);
                a.lea(rcx, __qword_ptr[r14 + rcx]);
                a.cmp(__dword_ptr[rcx + Stage], (int)TreeGrowthStage.Stage3); a.jae(vanilla);
            }
            a.movzx(eax, __word_ptr[rcx + Species - offset]);
            a.cmp(eax, (int)VegetationType.ChestnutTree); a.jb(vanilla);
            a.cmp(eax, (int)VegetationType.BirchTree); a.ja(vanilla);
            a.cmp(__word_ptr[rcx + Felled - offset], 0); a.jne(vanilla);
            // A hit already landed on a living sapling. Vanilla can finish that cut and clear it.
            var protectedTree = a.CreateLabel();
            a.movsx(eax, __word_ptr[rcx + Health - offset]);
            a.test(eax, eax); a.jle(protectedTree);
            a.cmp(ax, __word_ptr[rcx + MaxHealth - offset]); a.jl(vanilla);
            a.Label(ref protectedTree);
            Restore(a);

            if (path != Path.Search)
            {
                // Both idle continuations expect the worker index reloaded in RAX.
                // Strike also replayed MOV EDX,EBP on its original false branch.
                if (path == Path.Strike) a.mov(edx, ebp);
                a.mov(rax, currentWorker); a.movsxd(rax, __dword_ptr[rax]);
            }
            a.jmp(reject);

            a.Label(ref vanilla);
            Restore(a);
            a.Label(ref replay);
            a.AddInstructions(original.CloneInstructionsWithoutIP());
        }

        private static void Restore(Assembler a) { a.pop(rcx); a.pop(rax); a.popfq(); }
    }
}
