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
    //
    // Why: Vanilla stores pitch 4 lower (0x594CB). That drop alone makes the full transition rebuild
    // in 0x71670 -> 0xDAA50 cut stairs behind pitch off, so the AI demolishes reachable buildings.
    // Removing the drop at its source gives pathing, building access and generic height consumers
    // the same real ground plane. Fire comparisons separately retain Vanilla's lowered plane.
    //
    // What is patched (audited 01.-02.11.2026; all writers and compensations of the drop):
    //  - the only -4 store (0x594C1) and the two +4 overbuild restores (AIV 0x6CCED, manual
    //    0x6F6AD), which would otherwise raise stairs and walls over pitch 4 too high;
    //  - the start/load re-lowering 0x804C0. It covers new maps, saves and editor map loads,
    //    because DLL_LoadSaveGame/DLL_LoadMapToPlay -> 0x71670 -> 0x7D510 -> 0x804C0. Only a new,
    //    pitch-free editor map skips it. The same stub raises pitch that old saves or maps still
    //    hold lowered;
    //  - the two +4 compensations for hidden pitch, render 0x43357 and tile pass 0x68EAD, which
    //    would otherwise draw hidden pitch 4 too high;
    //  - fire spread and the pitch chain use Vanilla's virtual lowered height. A native byte map
    //    records the terminal DefaultHeight restore until removal, and is rebuilt at load entry.
    //
    // Deliberately not patched (each checked; reason given):
    //  - visible pitch (MiscDisplay 0x2000) is drawn at ground level instead of as a 4-deep ditch.
    //    This is consistent with unit stand height (0x19B260, 0x184FD0, 0x182B00) and fire-effect
    //    height (0x7D44B, 0x9C730), which also read raw HeightGrid. The user accepted it in game
    //    on 02.11.2026;
    //  - 0x6B910: more than 40 callers; only the fire call sites adjust its result. BFQoL's current
    //    source has a contract entry for this address, not an installed detour;
    //  - pitch removal 0x62EB0, moat digging 0x639C0 and the
    //    pitch-clearing building writers 0x5D740/0x6D580: each writes Default (or 0) itself,
    //    which is identical under B;
    //  - other pitch-record and ignition decisions in 0x9C730, 0x10DDC0 and 0x17CF50;
    //  - stairs that old saves already raised by +4: they no longer carry the pitch flag and cannot
    //    be told apart from a correct build.
    //
    // Multiplayer: every gameplay stub decides only on simulation data, never on MiscDisplay
    // 0x2000, which differs per client.
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
        // Hash-bound native record layout, independently checked against creation, lookup and update.
        // IDs 1..3999: PackedTileId precedes GamePitchDescriptor.GlobalId by four bytes.
        internal const int TileCapacity = 320_800, PitchSlotCapacity = 4_000;
        internal const int PitchRecordsOffset = 0x2038E44, PitchRecordStride = 20;
        internal const int PitchOwnerOffset = 8, PitchStateOffset = 16, PitchLimitOffset = 0x204C6C4;
        internal const int PitchLimitAnchorRva = 0x695C0, PitchRecordsAnchorRva = 0x695D5;
        internal const int PitchOwnerAnchorRva = 0x695E0, PitchStateAnchorRva = 0x7D407;
        internal const int TerminalPitchState = -1;
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
        // MiscDisplay bit that the tile pass 0x65830 sets on pitch visible to the local player (own or
        // allied team, or burning; 0x683D8). The render (0x43347) and placement (0x68EB2) compensations
        // skip it to draw a visible ditch. It differs per client, so gameplay stubs must never test it:
        // stored height is lowered for every pitch tile.
        internal const int PitchVisibleBit = 0x2000;

        // State block (process lifetime, never freed after publication). Counters are diagnostics only.
        internal const int EnabledOffset = 0, SkippedLoweringsOffset = 8, SkippedAivRestoresOffset = 16;
        internal const int SkippedManualRestoresOffset = 24, LoadPitchTilesOffset = 32, LoadRaisedTilesOffset = 40;
        internal const int FireSourceAdjustedOffset = 48, FireNeighborAdjustedOffset = 56;
        // Fire-path counters, written only by stubs emitted with diagCounters (DEVDIAG builds).
        internal const int FireSourceCallsOffset = 64, FireSourcePitchOffset = 72, FireSourcePitchVisibleOffset = 80;
        internal const int FireNeighborCallsOffset = 88, RestoredPitchOffset = 96;
        internal const int StateSize = RestoredPitchOffset + TileCapacity;

        internal enum SiteKind { Lowering, AivRestore, ManualRestore, LoadNormalization, Render, Placement, FireSource, FireNeighbor, LoadBootstrap, BurnoutRestore, PitchChain }

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
            // 0x41D60 tile renderer, hidden-pitch branch (PitchTrap && !0x2000): movzx eax,[rbx+r9+Height];
            // mov eax,[r9+rax*4+0x7090]. The visible-pitch branch reads raw height without compensation
            // and is left alone on purpose (see the class comment).
            new Site(SiteKind.Render, "render-pitch-height", 0x43357,
                new byte[] { 0x42,0x0F,0xB6,0x84,0x0B,0xA0,0xE5,0xD7,0x00, 0x41,0x8B,0x84,0x81,0x90,0x70,0x00,0x00 },
                new[] { 9, 8 }, new byte[] { 0x89,0x05,0x6A,0xA5,0x3E,0x00 }),
            // Simulation tile pass 0x65830 (block from 0x676F0), height cap before cmp esi,0x88:
            // mov eax,0x2000; test word [rbp+r13*2+MiscDisplay],ax; jne 0x68EC0; add esi,4.
            // Only the hidden-pitch branch adds 4; under B it then yields Vanilla's own value
            // (Default = Default-4+4), so this per-client branch stays deterministic.
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
            new Site(SiteKind.LoadBootstrap, "load-pitch-markers", 0x804C0,
                new byte[] { 0x48,0x89,0x5C,0x24,0x08, 0x4C,0x8D,0x0D,0x84,0xCE,0xD5,0x04, 0x41,0xBB,0x20,0xE5,0x04,0x00 },
                new[] { 5, 7, 6 }, new byte[] { 0x4C,0x8D,0x15,0xD7,0x6C,0x87,0x04 }),
            new Site(SiteKind.BurnoutRestore, "pitch-burnout-restore", 0x7D497,
                new byte[] { 0x42,0x0F,0xB6,0x84,0x29,0x70,0xB8,0xE2,0x04, 0x42,0x88,0x84,0x29,0x50,0xD3,0xDD,0x04 },
                new[] { 9, 8 }, new byte[] { 0x66,0x44,0x89,0x5B,0xFE }),
            new Site(SiteKind.PitchChain, "pitch-chain-heights", 0x7D411,
                new byte[] { 0x43,0x0F,0xB6,0x8C,0x2E,0x50,0xD3,0xDD,0x04, 0x41,0x8B,0xC4, 0x2B,0xC1 },
                new[] { 9, 3, 2 }, new byte[] { 0x99,0x33,0xC2,0x2B,0xC2 }),
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
        // diagCounters adds the fire-path counters; only the DEVDIAG runtime passes true.
        internal static void Emit(Assembler a, Site site, ReadOnlySpan<Instruction> original, ulong state, ulong continuation,
            bool diagCounters = false)
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
                case SiteKind.FireSource: EmitFireSource(a, original, state, continuation, diagCounters); break;
                case SiteKind.FireNeighbor: EmitFireNeighbor(a, original, site.CallIndex, state, continuation, diagCounters); break;
                case SiteKind.LoadBootstrap: EmitLoadBootstrap(a, original, state, continuation); break;
                case SiteKind.BurnoutRestore: EmitBurnoutRestore(a, original, state, continuation); break;
                case SiteKind.PitchChain: EmitPitchChain(a, original, state, continuation); break;
                default: throw new ArgumentOutOfRangeException(nameof(site));
            }
        }

        // Runs original[0..keep) unchanged, then skips the last instruction while enabled.
        // Used for the pitch-lowering store, the manual +4 restore and the placement +4 compensation.
        // Manual overbuild: the kept test/je/and/mov clear PitchTrap exactly as Vanilla does; only the
        // +4 that undid the (now absent) drop is skipped.
        // The pitch flag, the pitch record and the tile marking of 0x59380 stay Vanilla; only the height
        // store is skipped.
        private static void EmitSkipLast(Assembler a, ReadOnlySpan<Instruction> original, int keep, ulong state,
            int counterOffset, ulong continuation)
        {
            if (keep != original.Length - 1) throw new InvalidOperationException("Unexpected skip layout.");
            Label vanilla = a.CreateLabel();
            // Kept instructions (including the cmovbe/je flag consumers) run before any guard.
            Add(a, original.Slice(0, keep));
            a.pushfq(); a.push(rax); a.mov(rax, state);
            a.cmp(__dword_ptr[rax + EnabledOffset], 0); a.je(vanilla);
            if (counterOffset == SkippedLoweringsOffset)
            {
                a.cmp(r8d, TileCapacity); a.jae(vanilla);
                a.mov(__byte_ptr[rax + r8 + RestoredPitchOffset], 0);
            }
            if (counterOffset >= 0) a.inc(__qword_ptr[rax + counterOffset]);
            a.pop(rax); a.popfq(); a.jmp(continuation);
            a.Label(ref vanilla);
            a.pop(rax); a.popfq();
            Add(a, original.Slice(keep, 1));
            a.jmp(continuation);
        }

        // 0x6CB20 runs this block only for a tile that still had PitchTrap. It has just set IsWall and
        // cleared PitchTrap (0x6CCE3), so later readers no longer treat the tile as pitch. Under B the
        // ground was never lowered, so the +4 is skipped and nothing else changes.
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
        // Like Vanilla's own write, the Default >= 4 case is unconditional. That repairs lowered pitch
        // in Vanilla saves and maps, and it is a no-op for tiles that already hold B heights.
        // Pathing transitions need no extra work: 0x71670 rebuilds them afterwards (0xDAA50).
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

        // Burning tile: the terminal restore has already raised Vanilla height while PitchTrap can
        // remain set until the next tick. That exact writer is tracked without per-attempt lookups.
        private static void EmitFireSource(Assembler a, ReadOnlySpan<Instruction> original, ulong state, ulong continuation,
            bool diagCounters)
        {
            Label keep = a.CreateLabel(), counted = a.CreateLabel();
            Add(a, original.Slice(0, 1)); // movzx edi, Height[rcx] (r15 = tile manager)
            // Incoming flags are dead: the original cmp ax,4 below recomputes them.
            a.push(r11); a.mov(r11, state);
            if (diagCounters) a.inc(__qword_ptr[r11 + FireSourceCallsOffset]);
            a.cmp(__dword_ptr[r11 + EnabledOffset], 0); a.je(keep);
            a.cmp(ecx, TileCapacity); a.jae(keep);
            a.test(__dword_ptr[r15 + rcx * 4 + LogicOffset], PitchTrapFlag); a.jz(keep);
            if (diagCounters)
            {
                // Observation only: the decision must not depend on the per-client visibility bit.
                a.inc(__qword_ptr[r11 + FireSourcePitchOffset]);
                a.test(__word_ptr[r15 + rcx * 2 + MiscDisplayOffset], PitchVisibleBit); a.jz(counted);
                a.inc(__qword_ptr[r11 + FireSourcePitchVisibleOffset]);
                a.Label(ref counted);
            }
            a.cmp(__byte_ptr[r11 + rcx + RestoredPitchOffset], 0); a.jne(keep);
            a.inc(__qword_ptr[r11 + FireSourceAdjustedOffset]);
            a.sub(edi, PitchHeightDrop); a.jge(keep);
            a.xor(edi, edi);
            a.Label(ref keep);
            a.pop(r11);
            Add(a, original.Slice(1, 2)); // cmp ax,4; jge (absolute target)
            a.jmp(continuation);
        }

        // Neighbor height from 0x6B910 (rcx = tile manager, edx = neighbor tile). Its raw-height path
        // (no structure) returns the stored height, which Vanilla keeps lowered on every pitch tile.
        // Structure tiles are left alone because 0x6B910 returns DefaultHeight or a building correction
        // there; pitch under a structure was not observed. 0x6B910 itself stays unpatched because it
        // has more than 40 callers. The current BFQoL sources contain a contract entry, not a detour.
        private static void EmitFireNeighbor(Assembler a, ReadOnlySpan<Instruction> original, int callIndex, ulong state, ulong continuation,
            bool diagCounters)
        {
            Instruction call = original[callIndex];
            if (call.Mnemonic != Mnemonic.Call || call.NearBranchTarget == 0) throw new InvalidOperationException("Fire-neighbor call missing.");
            Label plain = a.CreateLabel(), adjusted = a.CreateLabel();
            Add(a, original.Slice(0, callIndex));
            // The decision runs before the call (rax and entry flags are dead there). Both paths call
            // with Vanilla's rsp and touch no stack, so the plain path leaves even the callee's flags
            // as Vanilla does; the row variants end with the call itself.
            a.mov(rax, state);
            if (diagCounters) a.inc(__qword_ptr[rax + FireNeighborCallsOffset]);
            a.cmp(__dword_ptr[rax + EnabledOffset], 0); a.je(plain);
            // Native manager grids have padding; the marker array does not. Invalid neighbors must
            // reach the original query unchanged, without touching any of our tile-indexed state.
            a.cmp(edx, TileCapacity); a.jae(plain);
            a.cmp(__byte_ptr[rax + rdx + RestoredPitchOffset], 0); a.jne(plain);
            a.test(__dword_ptr[r15 + rdx * 4 + LogicOffset], PitchTrapFlag); a.jz(plain);
            a.cmp(__word_ptr[r15 + rdx * 2 + StructureOffset], 0); a.jne(plain);
            a.call(call.NearBranchTarget);
            a.mov(rcx, state); // rcx is clobbered by the callee anyway
            a.inc(__qword_ptr[rcx + FireNeighborAdjustedOffset]);
            a.sub(eax, PitchHeightDrop); a.jge(adjusted);
            a.xor(eax, eax);
            a.Label(ref adjusted);
            Add(a, original.Slice(callIndex + 1));
            a.jmp(continuation);
            a.Label(ref plain);
            a.call(call.NearBranchTarget);
            Add(a, original.Slice(callIndex + 1));
            a.jmp(continuation);
        }

        // Rebuild once before the load loop: Vanilla relowers Default>=4 even for state -1, but
        // retains a low terminal height. OwnerId!=0 and the bounded high-water match native lookup.
        private static void EmitLoadBootstrap(Assembler a, ReadOnlySpan<Instruction> original, ulong state, ulong continuation)
        {
            ulong height = original[1].IPRelativeMemoryAddress, manager = height - HeightOffset;
            Add(a, original.Slice(0, 1)); // Home-slot store must run at the original RSP.
            a.mov(r9, height); // Same LEA result without a near-stub RIP-distance requirement.
            Add(a, original.Slice(2, 1));
            Label done = a.CreateLabel(), clear = a.CreateLabel(), bounded = a.CreateLabel();
            Label scan = a.CreateLabel(), next = a.CreateLabel();
            a.pushfq(); a.push(rax); a.push(rcx); a.push(rdx); a.push(r8); a.push(r10);
            a.mov(rax, state); a.cmp(__dword_ptr[rax + EnabledOffset], 0); a.je(done);
            a.lea(rdx, __[rax + RestoredPitchOffset]); a.mov(ecx, TileCapacity / sizeof(long)); a.xor(r8d, r8d);
            a.Label(ref clear);
            a.mov(__qword_ptr[rdx], r8); a.add(rdx, sizeof(long)); a.dec(ecx); a.jne(clear);
            a.mov(r10, manager); a.mov(r8d, __dword_ptr[r10 + PitchLimitOffset]);
            a.cmp(r8d, 1); a.jle(done);
            a.cmp(r8d, PitchSlotCapacity); a.jle(bounded); a.mov(r8d, PitchSlotCapacity);
            a.Label(ref bounded);
            a.dec(r8d); a.mov(rdx, manager + PitchRecordsOffset + PitchRecordStride);
            a.mov(r10, manager + DefaultHeightOffset);
            a.Label(ref scan);
            a.cmp(__word_ptr[rdx + PitchOwnerOffset], 0); a.je(next);
            a.cmp(__word_ptr[rdx + PitchStateOffset], TerminalPitchState); a.jne(next);
            a.mov(ecx, __dword_ptr[rdx]); a.cmp(ecx, TileCapacity); a.jae(next);
            a.cmp(__byte_ptr[r10 + rcx], PitchHeightDrop); a.jae(next);
            a.mov(__byte_ptr[rax + rcx + RestoredPitchOffset], 1);
            a.Label(ref next);
            a.add(rdx, PitchRecordStride); a.dec(r8d); a.jne(scan);
            a.Label(ref done);
            a.pop(r10); a.pop(r8); a.pop(rdx); a.pop(rcx); a.pop(rax); a.popfq(); a.jmp(continuation);
        }

        private static void EmitBurnoutRestore(Assembler a, ReadOnlySpan<Instruction> original, ulong state, ulong continuation)
        {
            Add(a, original);
            Label done = a.CreateLabel();
            a.pushfq(); a.push(rax); a.mov(rax, state);
            a.cmp(__dword_ptr[rax + EnabledOffset], 0); a.je(done);
            a.cmp(ecx, TileCapacity); a.jae(done);
            a.mov(__byte_ptr[rax + rcx + RestoredPitchOffset], 1);
            a.Label(ref done);
            a.pop(rax); a.popfq(); a.jmp(continuation);
        }

        // Source has just moved 2->3, and the target must be dormant (1). Neither has reached the
        // terminal restore. Saturate both comparison operands: low ground is not a uniform +4.
        private static void EmitPitchChain(Assembler a, ReadOnlySpan<Instruction> original, ulong state, ulong continuation)
        {
            Add(a, original.Slice(0, 2));
            Label done = a.CreateLabel(), source = a.CreateLabel();
            a.push(r11); a.mov(r11, state); a.cmp(__dword_ptr[r11 + EnabledOffset], 0); a.je(done);
            a.sub(eax, PitchHeightDrop); a.jge(source); a.xor(eax, eax);
            a.Label(ref source);
            a.sub(ecx, PitchHeightDrop); a.jge(done); a.xor(ecx, ecx);
            a.Label(ref done);
            a.pop(r11); Add(a, original.Slice(2, 1)); a.jmp(continuation);
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

        // Reference rule for the diagnostic scan: a pitch tile still at Vanilla's lowered height.
        internal static bool IsVanillaLoweredPitch(byte height, byte defaultHeight, int logic, ushort structure)
        {
            if ((logic & PitchTrapFlag) == 0) return false;
            if (defaultHeight >= PitchHeightDrop) return height == defaultHeight - PitchHeightDrop;
            return defaultHeight != 0 && height == 0 && (logic & WallFlag) == 0 && structure == 0;
        }
    }
}
