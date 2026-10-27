using System;
using Iced.Intel;
using RedBird.X64.Extensions;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL.MoatTerrain
{
    internal enum TerrainSiteKind
    {
        TaskCreated, Excavated, Filled, DirectPrepare, BrushFinished, BridgeRemoved,
        BridgePurged, AreaRemoved, Paint, BuildingPrepare, Replaced, Erased, Vegetation, VegetationUpdate
    }

    internal static class NativeCode
    {
        // Six pushes below the original RSP; there are no calls while this frame exists.
        private const int SavedBytes = 48;

        internal static void Emit(Assembler a, TerrainSiteKind kind, ulong state, ulong tileManager,
            ReadOnlySpan<Instruction> original)
        {
            EmitOperation(a, kind, state, tileManager);
            // In particular the brush's native refresh CALL sees its original RSP and arguments.
            a.AddInstructions(original);
        }

        // Shared by production hooks and executable fixtures, including the dormant guard.
        internal static void EmitOperation(Assembler a, TerrainSiteKind kind, ulong state, ulong tileManager)
        {
            Label dormant = a.CreateLabel(), done = a.CreateLabel(), invalid = a.CreateLabel();
            a.pushfq(); a.push(r10);
            a.mov(r10, state);
            a.cmp(__byte_ptr[r10], 0); a.je(dormant);
            a.push(rax); a.push(rcx); a.push(rdx); a.push(r11);
            LoadTile(a, kind);
            a.cmp(r11d, TerrainState.Capacity); a.jae(invalid);
            a.mov(rcx, tileManager);
            a.movzx(eax, __word_ptr[r10 + r11 * 2 + TerrainState.DataOffset]);
            switch (kind)
            {
                case TerrainSiteKind.TaskCreated: EmitTask(a, done); break;
                case TerrainSiteKind.Excavated:
                    a.test(__dword_ptr[rcx + r11 * 4 + NativeDefinition.Logic], (int)TilePropertyFlag.IsMoat);
                    a.jnz(done); // A late/repeated call cannot invent history for an existing moat.
                    Capture(a, TerrainState.Dug); break;
                case TerrainSiteKind.DirectPrepare:
                    a.cmp(r15d, 2); a.jne(done); // Native operation number, not a mapper ID.
                    Capture(a, TerrainState.Preparing); break;
                case TerrainSiteKind.BrushFinished: EmitBrush(a, done); break;
                case TerrainSiteKind.BuildingPrepare: EmitBuilding(a, done); break;
                case TerrainSiteKind.Filled:
                    a.test(__dword_ptr[rcx + r11 * 4 + NativeDefinition.Logic], (int)TilePropertyFlag.IsMoat);
                    a.jnz(done);
                    // Filling a raised bridge's moat does not remove its footprint. A later
                    // bridge demolition still executes an OR-2 soil writer and needs this origin.
                    Label ordinaryFill = a.CreateLabel();
                    a.test(eax, TerrainState.Bridge); a.jz(ordinaryFill);
                    a.cmp(__word_ptr[rcx + r11 * 2 + NativeDefinition.Structure], 0); a.je(ordinaryFill);
                    a.test(eax, TerrainState.Dug); a.jz(done); // Bridge-only means the original soil is unknown.
                    WriteSoil(a); a.jmp(done);
                    a.Label(ref ordinaryFill);
                    Restore(a, done, false); break;
                case TerrainSiteKind.BridgeRemoved:
                case TerrainSiteKind.BridgePurged:
                case TerrainSiteKind.AreaRemoved:
                    a.test(__dword_ptr[rcx + r11 * 4 + NativeDefinition.Logic], (int)TilePropertyFlag.IsMoat);
                    a.jnz(done); // A surviving moat under a destroyed raised bridge retains its origin.
                    Restore(a, done, false); break;
                default: Clear(a); break; // Explicit replacement owns the resulting terrain.
            }
            a.jmp(done);
            a.Label(ref invalid);
            a.mov(__dword_ptr[r10 + 12], 1); // Reported outside the tile loop; never dereference an invalid tile.
            a.Label(ref done);
            a.pop(r11); a.pop(rdx); a.pop(rcx); a.pop(rax);
            a.Label(ref dormant);
            a.pop(r10); a.popfq();
        }

        private static void LoadTile(Assembler a, TerrainSiteKind kind)
        {
            switch (kind)
            {
                case TerrainSiteKind.TaskCreated: a.mov(r11d, r9d); break;
                case TerrainSiteKind.Excavated: a.mov(r11d, edi); break;
                case TerrainSiteKind.Filled: a.mov(r11d, __dword_ptr[rbx]); break;
                case TerrainSiteKind.DirectPrepare:
                case TerrainSiteKind.BrushFinished:
                case TerrainSiteKind.Paint: a.mov(r11d, ebx); break;
                case TerrainSiteKind.BridgeRemoved:
                case TerrainSiteKind.BridgePurged:
                case TerrainSiteKind.BuildingPrepare: a.mov(r11d, esi); break;
                case TerrainSiteKind.AreaRemoved: a.mov(r11d, r14d); break;
                case TerrainSiteKind.Replaced: a.mov(r11d, edi); break;
                case TerrainSiteKind.Erased: a.mov(r11d, ebp); break;
                case TerrainSiteKind.Vegetation: a.mov(r11d, __dword_ptr[rbx + 0x8C]); break;
                case TerrainSiteKind.VegetationUpdate: a.mov(r11d, __dword_ptr[rbx + 0x24]); break;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        private static void Capture(Assembler a, int stage)
        {
            a.movzx(eax, __byte_ptr[rcx + r11 + NativeDefinition.Soil]);
            a.or(eax, stage);
            Store(a);
            a.mov(__dword_ptr[r10 + 4], 1);
        }

        private static void Store(Assembler a) => a.mov(__word_ptr[r10 + r11 * 2 + TerrainState.DataOffset], ax);
        private static void Clear(Assembler a) => a.mov(__word_ptr[r10 + r11 * 2 + TerrainState.DataOffset], 0);

        private static void EmitTask(Assembler a, Label done)
        {
            Label remember = a.CreateLabel(), active = a.CreateLabel();
            a.test(eax, TerrainState.Bridge); a.jnz(done);
            a.test(eax, TerrainState.Preparing); a.jz(remember);
            a.and(eax, 0xFF); a.or(eax, TerrainState.Dug); Store(a); a.jmp(done);
            a.Label(ref remember);
            a.test(__dword_ptr[rcx + r11 * 4 + NativeDefinition.Logic], (int)TilePropertyFlag.IsMoat);
            a.jnz(done); // A new work record on an old, untracked moat supplies no original soil.
            a.cmp(__word_ptr[rcx + r11 * 2 + NativeDefinition.Structure], 0);
            a.jne(done); // Old bridge decks without our construction capture also remain untracked.
            a.cmp(__dword_ptr[rsp + SavedBytes + 0x50], 0); a.jne(active);
            Capture(a, TerrainState.Planned); a.jmp(done);
            a.Label(ref active);
            Capture(a, TerrainState.Dug);
        }

        private static void EmitBuilding(Assembler a, Label done)
        {
            Label fresh = a.CreateLabel();
            a.cmp(r12d, (int)eStructs.STRUCT_DRAWBRIDGE); a.je(fresh);
            Clear(a); a.jmp(done);
            a.Label(ref fresh);
            Label reuse = a.CreateLabel();
            a.mov(edx, __dword_ptr[rcx + r11 * 4 + NativeDefinition.Logic]);
            a.test(edx, (int)(TilePropertyFlag.IsMoat | TilePropertyFlag.PlannedMoat));
            Label capture = a.CreateLabel();
            a.jz(capture);
            a.test(eax, TerrainState.Planned | TerrainState.Dug); a.jnz(reuse);
            a.test(edx, (int)TilePropertyFlag.IsMoat); a.jz(capture);
            // A bridge on an untracked old moat must not turn its current dirt into an
            // alleged historical origin. The bridge-only marker also suppresses task capture.
            a.mov(eax, TerrainState.Bridge); Store(a); a.jmp(done);
            a.Label(ref capture);
            Capture(a, TerrainState.Dug | TerrainState.Bridge); a.jmp(done);
            a.Label(ref reuse);
            a.and(eax, 0xFF); a.or(eax, TerrainState.Dug | TerrainState.Bridge); Store(a);
        }

        private static void EmitBrush(Assembler a, Label done)
        {
            Label moat = a.CreateLabel(), placed = a.CreateLabel(), removal = a.CreateLabel();
            a.cmp(r13d, unchecked((int)TilePropertyFlag.IsMoat)); a.je(moat);
            Clear(a); a.jmp(done);
            a.Label(ref moat);
            a.cmp(r15d, 3); a.je(removal);
            a.cmp(r15d, 1); a.jne(placed);
            Clear(a); a.jmp(done); // Cancelling only a plan does not rewrite the current soil.
            a.Label(ref placed);
            a.cmp(r15d, 2); a.jne(done);
            Label failed = a.CreateLabel();
            a.test(__dword_ptr[rcx + r11 * 4 + NativeDefinition.Logic], (int)TilePropertyFlag.IsMoat);
            a.jz(failed);
            a.test(eax, TerrainState.Preparing); a.jz(done);
            a.and(eax, 0xFF); a.or(eax, TerrainState.Dug); Store(a); a.jmp(done);
            a.Label(ref failed);
            a.test(eax, TerrainState.Preparing); a.jz(done);
            WriteSoil(a); Clear(a); a.jmp(done);
            a.Label(ref removal);
            Restore(a, done, true);
        }

        private static void Restore(Assembler a, Label done, bool includePlan)
        {
            a.test(eax, includePlan ? TerrainState.Planned | TerrainState.Dug : TerrainState.Dug);
            Label clear = a.CreateLabel();
            a.jz(clear);
            WriteSoil(a);
            a.Label(ref clear);
            Clear(a);
        }

        private static void WriteSoil(Assembler a)
        {
            a.mov(__byte_ptr[rcx + r11 + NativeDefinition.Soil], al);
            a.mov(__dword_ptr[r10 + 8], 1);
        }
    }
}
