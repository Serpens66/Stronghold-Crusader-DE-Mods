using Iced.Intel;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using static Iced.Intel.AssemblerRegisters;

namespace PitchStairsTest
{
    // Variant B of PitchStairs.md: pitch keeps the ground height in HeightGrid. Every RVA, byte
    // sequence and tile-manager offset below is bound to this native hash; see UpdateToNewDLL.md.
    internal static class NativeCode
    {
        internal const string Hash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";

        internal const int TileManagerRva = 0x405EDB0;
        internal const int LogicOffset = 0x898400, StructureOffset = 0xB0BCA0, MiscDisplayOffset = 0xF05F40;
        internal const int HeightOffset = 0xD7E5A0, DefaultHeightOffset = 0xDCCAC0;
        // Screen-height table (int32 per height index) at tile manager +0x7080; Vanilla's pitch
        // render branch reads it at +0x7090, i.e. four indices higher to undo the stored drop.
        internal const int HeightTableOffset = 0x7080, VanillaPitchHeightTableOffset = 0x7090;
        internal const int NeighborHeightRva = 0x6B910;
        internal const int PitchHeightDrop = 4;
        // Packed-grid distance from the misc-display word grid to the structure word grid in 0x804C0.
        internal const int MiscDisplayToStructureDelta = StructureOffset - MiscDisplayOffset;

        // Independent evidence for the data layout, decoded at install time.
        // lea rcx,[rip+disp] in 0x71670 that passes the tile manager to 0x7D510.
        internal const int TileManagerAnchorRva = 0x71956;
        internal static readonly byte[] TileManagerAnchorPrefix = { 0x48, 0x8D, 0x0D };
        // 0x62EB0 (pitch removal): cmp word [rcx+rdx*2+Structure],0 / movzx eax,[rdx+rcx+DefaultHeight].
        internal const int StructureAnchorRva = 0x62F04, DefaultHeightAnchorRva = 0x62F0F;
        // 0x804C0: movsx rax,word [rdx+MiscDisplayToStructureDelta] (rdx walks the misc-display grid).
        internal const int LoadStructureAnchorRva = 0x804FA;

        internal const int PitchTrapFlag = (int)TilePropertyFlag.PitchTrap;
        internal const int WallFlag = (int)TilePropertyFlag.IsWall;
        // Vanilla treats only pitch without this footprint-edge bit (set via 0x6C510) as lowered:
        // see the render (0x43347) and placement (0x68EB2) compensations.
        internal const int FootprintEdgeBit = 0x2000;

        // State block (process lifetime, never freed after publication). Counters are diagnostics only.
        internal const int EnabledOffset = 0, SkippedLoweringsOffset = 8, SkippedAivRestoresOffset = 16;
        internal const int SkippedManualRestoresOffset = 24, LoadPitchTilesOffset = 32, LoadRaisedTilesOffset = 40;
        internal const int FireSourceAdjustedOffset = 48, FireNeighborAdjustedOffset = 56, StateSize = 64;

        internal enum SiteKind { Lowering, AivRestore, ManualRestore, LoadNormalization, Render, Placement, FireSource, FireNeighbor }

        internal sealed class Site
        {
            internal Site(SiteKind kind, string name, int rva, byte[] bytes, int[] lengths, byte[] following, int callIndex = -1)
            {
                Kind = kind; Name = name; Rva = rva; Bytes = bytes; InstructionLengths = lengths; Following = following; CallIndex = callIndex;
            }
            internal SiteKind Kind { get; }
            internal string Name { get; }
            internal int Rva { get; }
            internal byte[] Bytes { get; }
            internal int Length => Bytes.Length;
            internal int ContinuationRva => Rva + Bytes.Length;
            internal int[] InstructionLengths { get; }
            internal byte[] Following { get; }
            internal int CallIndex { get; }
        }

        internal static readonly Site[] Sites =
        {
            // 0x59380 pitch placement: lea eax,[rdx-4]; movzx ecx,al; cmovbe ecx,r9d; mov [r8+rsi+Height],cl
            new Site(SiteKind.Lowering, "pitch-lowering", 0x594C1,
                new byte[] { 0x8D,0x42,0xFC, 0x0F,0xB6,0xC8, 0x41,0x0F,0x46,0xC9, 0x41,0x88,0x8C,0x30,0xA0,0xE5,0xD7,0x00 },
                new[] { 3, 3, 4, 8 }, new byte[] { 0x48,0x8D,0x0D,0xF6,0x11,0x51,0x08 }),
            // 0x6CB20 AIV wall/stair: add byte [rdx+rdi+Height],4; mov eax,[rdi+rdx*4+Logic]
            new Site(SiteKind.AivRestore, "aiv-pitch-restore", 0x6CCED,
                new byte[] { 0x80,0x84,0x3A,0xA0,0xE5,0xD7,0x00,0x04, 0x8B,0x84,0x97,0x00,0x84,0x89,0x00 },
                new[] { 8, 7 }, new byte[] { 0x0F,0xBA,0xE0,0x0C }),
            // 0x6F230 manual wall/stair: test al,8; je 0x6F6C1; and eax,~8; mov [rbx],eax; add byte [r12+r13+Height],4
            new Site(SiteKind.ManualRestore, "manual-pitch-restore", 0x6F6AD,
                new byte[] { 0xA8,0x08, 0x74,0x10, 0x83,0xE0,0xF7, 0x89,0x03, 0x43,0x80,0x84,0x2C,0xA0,0xE5,0xD7,0x00,0x04 },
                new[] { 2, 2, 3, 2, 9 }, new byte[] { 0x8B,0x03 }),
            // 0x804C0 start/load normalization (pitch tile known): movzx eax,[r9+Default-Height]; cmp al,4; jb next; sub al,4; mov [r9],al
            new Site(SiteKind.LoadNormalization, "load-pitch-normalization", 0x80530,
                new byte[] { 0x41,0x0F,0xB6,0x81,0x20,0xE5,0x04,0x00, 0x3C,0x04, 0x72,0x05, 0x2C,0x04, 0x41,0x88,0x01 },
                new[] { 8, 2, 2, 2, 3 }, new byte[] { 0x48,0x83,0xC2,0x02 }),
            // 0x41D60 tile renderer pitch branch: movzx eax,[rbx+r9+Height]; mov eax,[r9+rax*4+0x7090]
            new Site(SiteKind.Render, "render-pitch-height", 0x43357,
                new byte[] { 0x42,0x0F,0xB6,0x84,0x0B,0xA0,0xE5,0xD7,0x00, 0x41,0x8B,0x84,0x81,0x90,0x70,0x00,0x00 },
                new[] { 9, 8 }, new byte[] { 0x89,0x05,0x6A,0xA5,0x3E,0x00 }),
            // 0x676F0 placement height cap: mov eax,0x2000; test word [rbp+r13*2+MiscDisplay],ax; jne 0x68EC0; add esi,4
            new Site(SiteKind.Placement, "placement-pitch-height", 0x68EAD,
                new byte[] { 0xB8,0x00,0x20,0x00,0x00, 0x66,0x42,0x85,0x84,0x6D,0xF0,0x4C,0xF6,0x04, 0x75,0x03, 0x83,0xC6,0x04 },
                new[] { 5, 9, 2, 3 }, new byte[] { 0xC1,0xEF,0x03 }),
            // 0x99420 general fire spread, burning tile: movzx edi,[rcx+r15+Height]; cmp ax,4; jge 0x99952
            new Site(SiteKind.FireSource, "fire-source-height", 0x997B4,
                new byte[] { 0x42,0x0F,0xB6,0xBC,0x39,0xA0,0xE5,0xD7,0x00, 0x66,0x83,0xF8,0x04, 0x0F,0x8D,0x8B,0x01,0x00,0x00 },
                new[] { 9, 4, 6 }, new byte[] { 0x66,0x83,0xBC,0x32,0xCA,0x00 }),
            // 0x99420 neighbor heights via 0x6B910 (four directions).
            new Site(SiteKind.FireNeighbor, "fire-neighbor-west", 0x997DB,
                new byte[] { 0x8D,0x51,0xFF, 0x49,0x8B,0xCF, 0xE8,0x2A,0x21,0xFD,0xFF, 0x2B,0xF8, 0x8B,0xC7 },
                new[] { 3, 3, 5, 2, 2 }, new byte[] { 0x99,0x33,0xC2 }, callIndex: 2),
            new Site(SiteKind.FireNeighbor, "fire-neighbor-east", 0x99824,
                new byte[] { 0x8D,0x51,0x01, 0x49,0x8B,0xCF, 0xE8,0xE1,0x20,0xFD,0xFF, 0x2B,0xF8, 0x8B,0xC7 },
                new[] { 3, 3, 5, 2, 2 }, new byte[] { 0x99,0x33,0xC2 }, callIndex: 2),
            new Site(SiteKind.FireNeighbor, "fire-neighbor-row-a", 0x9987C,
                new byte[] { 0x03,0x94,0x8B,0x38,0xFF,0x02,0x04, 0x49,0x8B,0xCF, 0xE8,0x85,0x20,0xFD,0xFF },
                new[] { 7, 3, 5 }, new byte[] { 0x2B,0xF8,0x8B,0xC7 }, callIndex: 2),
            new Site(SiteKind.FireNeighbor, "fire-neighbor-row-b", 0x998D1,
                new byte[] { 0x03,0x94,0x8B,0x20,0xFF,0x02,0x04, 0x49,0x8B,0xCF, 0xE8,0x30,0x20,0xFD,0xFF },
                new[] { 7, 3, 5 }, new byte[] { 0x2B,0xF8,0x8B,0xC7 }, callIndex: 2),
        };

        internal static Instruction[] Decode(byte[] bytes, ulong ip, int[] expectedLengths)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = ip;
            var result = new List<Instruction>();
            while (decoder.IP < ip + (ulong)bytes.Length)
            {
                Instruction instruction = decoder.Decode();
                if (instruction.IsInvalid || decoder.IP > ip + (ulong)bytes.Length)
                    throw new InvalidOperationException("Invalid pitch-stairs instruction boundary at 0x" + ip.ToString("X"));
                result.Add(instruction);
            }
            if (expectedLengths != null)
            {
                if (result.Count != expectedLengths.Length) throw new InvalidOperationException("Unexpected pitch-stairs instruction count.");
                for (int i = 0; i < result.Count; i++)
                    if (result[i].Length != expectedLengths[i]) throw new InvalidOperationException("Unexpected pitch-stairs instruction length.");
            }
            return result.ToArray();
        }

        internal static byte[] Assemble(Assembler a, ulong ip)
        {
            using (var stream = new MemoryStream())
            {
                if (!a.TryAssemble(new StreamCodeWriter(stream), ip, out string error, out _))
                    throw new InvalidOperationException(error);
                byte[] bytes = stream.ToArray();
                Decode(bytes, ip, null);
                return bytes;
            }
        }

        // Registers, stack and every flag still consumed after the continuation equal Vanilla's.
        // A skipped instruction's own flag result can differ; its next flag consumer is listed in UpdateToNewDLL.md.
        internal static void Emit(Assembler a, Site site, ReadOnlySpan<Instruction> original, ulong state, ulong continuation)
        {
            if (original.Length != site.InstructionLengths.Length)
                throw new InvalidOperationException("Unexpected displaced pitch-stairs block: " + site.Name);
            switch (site.Kind)
            {
                case SiteKind.Lowering: EmitSkipLast(a, original, 3, state, SkippedLoweringsOffset, continuation); break;
                case SiteKind.AivRestore: EmitAivRestore(a, original, state, continuation); break;
                case SiteKind.ManualRestore: EmitSkipLast(a, original, 4, state, SkippedManualRestoresOffset, continuation); break;
                case SiteKind.LoadNormalization: EmitLoadNormalization(a, original, state, continuation); break;
                case SiteKind.Render: EmitRender(a, original, state, continuation); break;
                case SiteKind.Placement: EmitSkipLast(a, original, 3, state, -1, continuation); break;
                case SiteKind.FireSource: EmitFireSource(a, original, state, continuation); break;
                case SiteKind.FireNeighbor: EmitFireNeighbor(a, original, site.CallIndex, state, continuation); break;
                default: throw new ArgumentOutOfRangeException(nameof(site));
            }
        }

        // Runs original[0..keep) unchanged, then skips the last instruction while enabled.
        // Used for the pitch-lowering store, the manual +4 restore and the placement +4 compensation.
        private static void EmitSkipLast(Assembler a, ReadOnlySpan<Instruction> original, int keep, ulong state,
            int counterOffset, ulong continuation)
        {
            if (keep != original.Length - 1) throw new InvalidOperationException("Unexpected skip layout.");
            Label vanilla = a.CreateLabel();
            // Kept instructions (including the cmovbe/je flag consumers) run before any guard.
            Add(a, original.Slice(0, keep));
            a.pushfq(); a.push(rax); a.mov(rax, state);
            a.cmp(__dword_ptr[rax + EnabledOffset], 0); a.je(vanilla);
            if (counterOffset >= 0) a.inc(__qword_ptr[rax + counterOffset]);
            a.pop(rax); a.popfq(); a.jmp(continuation);
            a.Label(ref vanilla);
            a.pop(rax); a.popfq();
            Add(a, original.Slice(keep, 1));
            a.jmp(continuation);
        }

        private static void EmitAivRestore(Assembler a, ReadOnlySpan<Instruction> original, ulong state, ulong continuation)
        {
            Label vanilla = a.CreateLabel(), reload = a.CreateLabel();
            a.pushfq(); a.push(rax); a.mov(rax, state);
            a.cmp(__dword_ptr[rax + EnabledOffset], 0); a.je(vanilla);
            // Height was rebuilt from DefaultHeight plus the mapper offset; the +4 has nothing to undo.
            a.inc(__qword_ptr[rax + SkippedAivRestoresOffset]);
            a.pop(rax); a.popfq(); a.jmp(reload);
            a.Label(ref vanilla);
            a.pop(rax); a.popfq();
            Add(a, original.Slice(0, 1));
            a.Label(ref reload);
            Add(a, original.Slice(1, 1));
            a.jmp(continuation);
        }

        // 0x804C0 visits each PitchTrap tile: r9 = &Height[i], r10 = &Logic[i], rdx = &MiscDisplay[i].
        // Enabled: Height = Default for Default >= 4 (Vanilla writes Default-4). For Default 0..3 Vanilla
        // keeps the placement-clamped 0; only a free, non-wall pitch tile at 0 is raised to its default.
        private static void EmitLoadNormalization(Assembler a, ReadOnlySpan<Instruction> original, ulong state, ulong continuation)
        {
            Label vanilla = a.CreateLabel(), low = a.CreateLabel(), done = a.CreateLabel(), changed = a.CreateLabel();
            a.pushfq(); a.push(rcx); a.mov(rcx, state);
            a.cmp(__dword_ptr[rcx + EnabledOffset], 0); a.je(vanilla);
            a.inc(__qword_ptr[rcx + LoadPitchTilesOffset]);
            Add(a, original.Slice(0, 1)); // movzx eax, Default
            a.cmp(al, PitchHeightDrop); a.jb(low);
            a.cmp(__byte_ptr[r9], al); a.jne(changed);
            a.jmp(done);
            a.Label(ref low);
            a.test(al, al); a.jz(done);
            a.cmp(__byte_ptr[r9], 0); a.jne(done);
            a.test(__dword_ptr[r10], WallFlag); a.jnz(done);
            a.cmp(__word_ptr[rdx + MiscDisplayToStructureDelta], 0); a.jne(done);
            a.Label(ref changed);
            a.mov(__byte_ptr[r9], al);
            a.inc(__qword_ptr[rcx + LoadRaisedTilesOffset]);
            a.Label(ref done);
            a.pop(rcx); a.popfq(); a.jmp(continuation);
            a.Label(ref vanilla);
            a.pop(rcx); a.popfq();
            Add(a, original); // jb keeps its absolute target, the continuation
            a.jmp(continuation);
        }

        // Height is no longer lowered, so the pitch branch must read the ordinary table entry.
        private static void EmitRender(Assembler a, ReadOnlySpan<Instruction> original, ulong state, ulong continuation)
        {
            Label vanilla = a.CreateLabel();
            Add(a, original.Slice(0, 1)); // movzx eax, Height
            a.pushfq(); a.push(rcx); a.mov(rcx, state);
            a.cmp(__dword_ptr[rcx + EnabledOffset], 0);
            a.pop(rcx); a.je(vanilla);
            a.popfq();
            a.mov(eax, __dword_ptr[r9 + rax * 4 + HeightTableOffset]);
            a.jmp(continuation);
            a.Label(ref vanilla);
            a.popfq();
            Add(a, original.Slice(1, 1));
            a.jmp(continuation);
        }

        // Burning tile: give the spread comparison Vanilla's lowered pitch height, max(0, h - 4).
        private static void EmitFireSource(Assembler a, ReadOnlySpan<Instruction> original, ulong state, ulong continuation)
        {
            Label keep = a.CreateLabel();
            Add(a, original.Slice(0, 1)); // movzx edi, Height[rcx] (r15 = tile manager)
            // Incoming flags are dead: the original cmp ax,4 below recomputes them.
            a.push(r11); a.mov(r11, state);
            a.cmp(__dword_ptr[r11 + EnabledOffset], 0); a.je(keep);
            a.test(__dword_ptr[r15 + rcx * 4 + LogicOffset], PitchTrapFlag); a.jz(keep);
            a.test(__word_ptr[r15 + rcx * 2 + MiscDisplayOffset], FootprintEdgeBit); a.jnz(keep);
            a.inc(__qword_ptr[r11 + FireSourceAdjustedOffset]);
            a.sub(edi, PitchHeightDrop); a.jge(keep);
            a.xor(edi, edi);
            a.Label(ref keep);
            a.pop(r11);
            Add(a, original.Slice(1, 2)); // cmp ax,4; jge (absolute target)
            a.jmp(continuation);
        }

        // Neighbor height from 0x6B910 (rcx = tile manager, edx = neighbor tile). Only its raw-height
        // path (no structure) can return a pitch tile; that result gets Vanilla's lowered value.
        private static void EmitFireNeighbor(Assembler a, ReadOnlySpan<Instruction> original, int callIndex, ulong state, ulong continuation)
        {
            Instruction call = original[callIndex];
            if (call.Mnemonic != Mnemonic.Call || call.NearBranchTarget == 0) throw new InvalidOperationException("Fire-neighbor call missing.");
            Label doCall = a.CreateLabel(), noAdjust = a.CreateLabel();
            Add(a, original.Slice(0, callIndex));
            // 0x30 keeps 16-byte call alignment; [rsp..rsp+0x20) is the callee's home area, +0x28 our flag.
            a.sub(rsp, 0x30);
            a.mov(__dword_ptr[rsp + 0x28], 0);
            a.mov(rax, state);
            a.cmp(__dword_ptr[rax + EnabledOffset], 0); a.je(doCall);
            a.movsxd(rax, edx);
            a.test(__dword_ptr[r15 + rax * 4 + LogicOffset], PitchTrapFlag); a.jz(doCall);
            a.test(__word_ptr[r15 + rax * 2 + MiscDisplayOffset], FootprintEdgeBit); a.jnz(doCall);
            a.cmp(__word_ptr[r15 + rax * 2 + StructureOffset], 0); a.jne(doCall);
            a.mov(__dword_ptr[rsp + 0x28], 1);
            a.Label(ref doCall);
            a.call(call.NearBranchTarget);
            a.cmp(__dword_ptr[rsp + 0x28], 0); a.je(noAdjust);
            a.mov(rcx, state);
            a.inc(__qword_ptr[rcx + FireNeighborAdjustedOffset]);
            a.sub(eax, PitchHeightDrop); a.jge(noAdjust);
            a.xor(eax, eax);
            a.Label(ref noAdjust);
            a.add(rsp, 0x30);
            Add(a, original.Slice(callIndex + 1));
            a.jmp(continuation);
        }

        // The hook backend supplies its own decode of the displaced block; it must be the audited one.
        internal static void RequireSameInstructions(ReadOnlySpan<Instruction> actual, Instruction[] expected, string name)
        {
            bool same = actual.Length == expected.Length;
            for (int i = 0; same && i < expected.Length; i++)
                same = actual[i].Code == expected[i].Code && actual[i].Length == expected[i].Length &&
                    actual[i].MemoryDisplacement64 == expected[i].MemoryDisplacement64 &&
                    actual[i].Immediate64 == expected[i].Immediate64 &&
                    (actual[i].FlowControl == FlowControl.Next || actual[i].NearBranchTarget == expected[i].NearBranchTarget);
            if (!same) throw new InvalidOperationException("Hook backend displaced a different block: " + name);
        }

        // Clears the original IP (branch targets stay absolute) so a block may be emitted on two paths.
        private static void Add(Assembler a, ReadOnlySpan<Instruction> instructions)
        {
            foreach (Instruction instruction in instructions)
            {
                Instruction copy = instruction;
                copy.IP = 0;
                a.AddInstruction(copy);
            }
        }

        // Variant-B start/load rule of the 0x804C0 stub, reused for editor starts (the editor never
        // reaches 0x804C0). StubTests checks it against the native stub.
        internal static byte NormalizedPitchHeight(byte height, byte defaultHeight, int logic, ushort structure)
        {
            if ((logic & PitchTrapFlag) == 0) return height;
            if (defaultHeight >= PitchHeightDrop) return defaultHeight;
            return defaultHeight != 0 && height == 0 && (logic & WallFlag) == 0 && structure == 0 ? defaultHeight : height;
        }

        // Reference rule for the diagnostic scan: a pitch tile Vanilla would still keep lowered.
        internal static bool IsVanillaLoweredPitch(byte height, byte defaultHeight, int logic, ushort miscDisplay, ushort structure)
        {
            if ((logic & PitchTrapFlag) == 0 || (miscDisplay & FootprintEdgeBit) != 0) return false;
            if (defaultHeight >= PitchHeightDrop) return height == defaultHeight - PitchHeightDrop;
            return defaultHeight != 0 && height == 0 && (logic & WallFlag) == 0 && structure == 0;
        }
    }
}
