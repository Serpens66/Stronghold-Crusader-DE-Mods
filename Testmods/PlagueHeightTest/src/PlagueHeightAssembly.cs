using System;
using System.IO;
using Iced.Intel;
using RedBird.X64.Extensions;
using SHCDESE.Interop.Enums;
using static Iced.Intel.AssemblerRegisters;

namespace PlagueHeightTest
{
    // All offsets below are hash-bound native machine contracts, not publicized members.
    internal static class PlagueHeightAssembly
    {
        internal const int Tolerance = 20, VisualOffset = 20;
        internal const int ProjectileBase = 0x2F7A680, HeightGrid = 0x4DDD350;
        internal const int PropertiesGrid = 0x48F71B0, BuildingGrid = 0x4B6AA50;
        internal const int BuildingManager = 0x64CCBB0, BuildingHeight = 0x148;
        internal static readonly int[] Sites = { 0x9FDB8, 0x192809, 0x9CB6F, 0x9CC3D };
        internal static readonly string[] OriginalHex = {
            "4863C24869C8E800000048634419600FB6843050D3DD046683C0146689441950",
            "4A6384062C8B7E0641F78480B0718F04000100100F85AD010000",
            "418BCC2BC88BC19933C22BC283F8787D1D6642837C2B3C027514",
            "418BCC2BC88BC19933C22BC283F8787D1C6642837C333C027513"
        };

        // Windows x64 leaf-style helper: ECX=tile, EAX=standing plane or -1.
        // C07C0 changes only RAX/RCX/RDX and flags; no SIMD state is touched.
        internal static void GenerateSurface(Assembler a, ulong module, int tiles, int buildings)
        {
            var invalid = a.CreateLabel(); var done = a.CreateLabel();
            var stairs = a.CreateLabel(); var wall = a.CreateLabel(); var building = a.CreateLabel();
            a.push(rbx); a.sub(rsp, 32); // alignment and shadow space for native C07C0
            a.cmp(ecx, tiles); a.jae(invalid);
            a.mov(r10, module);
            a.movzx(ebx, __byte_ptr[r10 + rcx + HeightGrid]);
            a.mov(r8d, __dword_ptr[r10 + rcx * 4 + PropertiesGrid]);
            a.test(r8d, (int)TilePropertyFlag.IsStairs); a.jnz(stairs);
            a.test(r8d, (int)TilePropertyFlag.IsWall); a.jnz(wall);
            a.test(r8d, (int)TilePropertyFlag.IsBuilding); a.jnz(building);
            a.test(r8d, (int)TilePropertyFlag.IsElevated); a.jz(done);
            a.movsx(edx, __word_ptr[r10 + rcx * 2 + BuildingGrid]);
            a.cmp(edx, 1); a.jl(invalid); a.cmp(edx, buildings); a.jg(invalid);
            a.imul(rax, rdx, 0x32C);
            a.movzx(ebx, __word_ptr[r10 + rax + BuildingManager + BuildingHeight]);
            a.lea(rcx, __[r10 + BuildingManager]);
            a.AddUnrestrictedCall(module + 0xC07C0);
            a.add(ebx, eax); a.jmp(done);
            a.Label(ref wall);
            a.movsx(edx, __word_ptr[r10 + rcx * 2 + BuildingGrid]);
            a.test(edx, edx); a.jz(done);
            a.cmp(edx, 1); a.jl(invalid); a.cmp(edx, buildings); a.jg(invalid);
            a.add(ebx, 40); a.jmp(done);
            a.Label(ref stairs); a.add(ebx, 12); a.jmp(done);
            a.Label(ref building); a.add(ebx, 2); a.jmp(done);
            a.Label(ref invalid); a.mov(ebx, -1);
            a.Label(ref done); a.mov(eax, ebx); a.add(rsp, 32); a.pop(rbx); a.ret();
        }

        internal static void GenerateHook(Assembler a, ReadOnlySpan<Instruction> original,
            ulong back, int index, ulong module, ulong state, ulong surface)
        {
            if (back != module + (uint)Sites[index] + (uint)(index == 0 ? 32 : 26))
                throw new InvalidOperationException("Unexpected hook continuation.");
            var vanilla = a.CreateLabel();
            a.pushfq(); a.push(rax); a.mov(rax, state); a.cmp(__dword_ptr[rax], 0);
            a.je(vanilla); a.pop(rax); a.popfq();
            if (index == 0) GenerateUpdate(a, original, back, state, surface, ref vanilla);
            else if (index == 1) GenerateDamage(a, module, state, surface);
            else
            {
                // Keep Vanilla's arithmetic/register outputs and its alive gate,
                // but leave the final height decision to the disease damage branch.
                for (int i = 0; i < 6; i++) Replay(a, original[i]);
                Replay(a, original[8]); Replay(a, original[9]);
                a.AddUnrestrictedJmp(back);
            }
            a.Label(ref vanilla); a.pop(rax); a.popfq();
            foreach (var i in original) Replay(a, i);
            a.AddUnrestrictedJmp(back);
        }

        private static void Save(Assembler a)
        {
            a.pushfq(); a.push(rax); a.push(rcx); a.push(rdx);
            a.push(r8); a.push(r9); a.push(r10); a.push(r11); a.sub(rsp, 32);
        }
        private static void Restore(Assembler a)
        {
            a.add(rsp, 32); a.pop(r11); a.pop(r10); a.pop(r9); a.pop(r8);
            a.pop(rdx); a.pop(rcx); a.pop(rax); a.popfq();
        }
        private static void GenerateUpdate(Assembler a, ReadOnlySpan<Instruction> original,
            ulong back, ulong state, ulong surface, ref Label vanilla)
        {
            var fallback = a.CreateLabel();
            Save(a);
            a.movsxd(rax, edx); a.imul(rax, rax, 0xE8);
            a.mov(ecx, __dword_ptr[rbx + rax + 0x60]);
            a.AddUnrestrictedCall(surface); a.test(eax, eax); a.js(fallback);
            a.add(eax, VisualOffset);
            // Saved EDX still carries the slot. Never reuse a clobbered pointer/index.
            a.movsxd(rdx, __dword_ptr[rsp + 64]); a.imul(rcx, rdx, 0xE8);
            a.mov(__word_ptr[rbx + rcx + 0x50], ax);
            a.mov(__qword_ptr[rsp + 80], rax); a.mov(__qword_ptr[rsp + 72], rcx);
            a.mov(r11, state); a.inc(__qword_ptr[r11 + 32]);
            Restore(a); a.AddUnrestrictedJmp(back);
            a.Label(ref fallback); a.mov(r11, state); a.inc(__qword_ptr[r11 + 56]); Restore(a);
            foreach (var i in original) Replay(a, i);
            a.AddUnrestrictedJmp(back);
        }
        private static void GenerateDamage(Assembler a, ulong module, ulong state, ulong surface)
        {
            var reject = a.CreateLabel(); var counted = a.CreateLabel(); var invalid = a.CreateLabel();
            Save(a);
            a.mov(rax, module + ProjectileBase);
            a.mov(ecx, __dword_ptr[rax + r13 + 0x60]);
            a.mov(__dword_ptr[rsp], ecx); // private shadow-space scratch, C07C0 does not use it
            a.AddUnrestrictedCall(surface);
            a.movsx(ecx, __word_ptr[rsi + r15 + 0x712]);
            a.movsx(edx, __word_ptr[rsi + r15 + 0x714]); a.add(ecx, edx);
            a.mov(r11, state);
            a.cmp(__qword_ptr[r11 + 8], 0); a.jne(counted);
            a.mov(edx, __dword_ptr[rsp]); a.mov(__dword_ptr[r11 + 40], edx);
            a.mov(__dword_ptr[r11 + 44], eax); a.mov(__dword_ptr[r11 + 48], ecx);
            a.Label(ref counted); a.inc(__qword_ptr[r11 + 8]);
            a.test(eax, eax); a.js(invalid);
            a.sub(ecx, eax); a.cmp(ecx, -Tolerance); a.jl(reject);
            a.cmp(ecx, Tolerance); a.jg(reject);
            a.inc(__qword_ptr[r11 + 16]); Restore(a);
            // Skip both surface and building immunity; retain type eligibility and all damage semantics.
            a.AddUnrestrictedJmp(module + 0x192861);
            a.Label(ref invalid); a.inc(__qword_ptr[r11 + 56]);
            a.Label(ref reject); a.inc(__qword_ptr[r11 + 24]); Restore(a);
            a.AddUnrestrictedJmp(module + 0x1929D0);
        }

        internal static byte[] Assemble(Assembler a, ulong address)
        {
            using (var stream = new MemoryStream())
            {
                if (!a.TryAssemble(new StreamCodeWriter(stream), address, out string error, out AssemblerResult result))
                    throw new InvalidOperationException("Iced assembly failed: " + error);
                return stream.ToArray();
            }
        }
        private static void Replay(Assembler a, Instruction instruction)
        {
            // Two logical paths replay the same block. Its branches all leave the
            // span, so suppress duplicate BlockEncoder identity while retaining targets.
            instruction.IP = 0;
            a.AddInstruction(instruction);
        }
        internal static Instruction[] Decode(byte[] bytes, ulong address)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes)); decoder.IP = address;
            var result = new System.Collections.Generic.List<Instruction>();
            while (decoder.IP < address + (uint)bytes.Length) { var i = decoder.Decode(); if (i.IsInvalid) throw new InvalidOperationException("Invalid native instruction."); result.Add(i); }
            return result.ToArray();
        }
    }
}
