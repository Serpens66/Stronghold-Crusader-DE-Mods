using System;
using System.IO;
using Iced.Intel;
using RedBird.X64.Extensions;
using SHCDESE.Interop;
using static Iced.Intel.AssemblerRegisters;

namespace AISellThresholdFixTest
{
    internal static class SellAssembly
    {
        internal const string NativeHash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const int PlayerStride = 0x583C, GoodsBase = 0x379B2A0, GoldBase = 0x379B2DC;
        internal const int SaleEpilog = 0x2978B;
        internal static readonly int[] Sites = { 0x3F1F3, 0x29723 };
        internal static readonly string[] OriginalHex = {
            "488D0516D06203448B8C2890F01200",
            "8BD7488D2DE42A6403418BF0488BCD"
        };

        // state+0 is the only functional datum (published activation). No managed callbacks.
        internal static void Generate(Assembler a, ReadOnlySpan<Instruction> original, ulong back,
            int index, ulong image, ulong state)
        {
            if (index < 0 || index >= Sites.Length || back != image + (uint)Sites[index] + 15)
                throw new InvalidOperationException("Unexpected sell hook continuation.");
            var vanilla = a.CreateLabel();
            a.pushfq(); a.push(rax); a.mov(rax, state); a.cmp(__dword_ptr[rax], 0);
            a.je(vanilla); a.pop(rax); a.popfq();
            if (index == 0)
            {
                // EDX is the final reserve, after minimum, demand and emergency branches.
                // The following native CMP replaces flags; retain them anyway.
                var nonnegative = a.CreateLabel();
                a.pushfq(); a.test(edx, edx); a.jns(nonnegative); a.mov(edx, 0);
                a.Label(ref nonnegative); a.popfq();
                Replay(a, original); a.AddUnrestrictedJmp(back);
            }
            else
            {
                var empty = a.CreateLabel(); var amountReady = a.CreateLabel();
                a.pushfq(); a.push(rax); a.push(rcx);
                // RDI is the sign-extended 1-based player, R8D the native goods index.
                a.cmp(edi, 1); a.jl(empty); a.cmp(edi, 8); a.jg(empty);
                a.cmp(r8d, (int)eGoods.STORED_NULL); a.jle(empty);
                a.cmp(r8d, (int)eGoods.Count); a.jae(empty);
                a.cmp(r8d, (int)eGoods.STORED_GOLD); a.je(empty);
                a.imul(rax, rdi, PlayerStride);
                a.mov(ecx, r8d); a.lea(rax, __[rax + rcx * 4]);
                a.mov(rcx, image + GoodsBase); a.mov(eax, __dword_ptr[rcx + rax]);
#if SERP_DEVDIAG
                Capture(a, image, state);
#endif
                a.test(eax, eax); a.jle(empty);
                a.cmp(ebx, eax); a.jle(amountReady); a.mov(ebx, eax);
                a.Label(ref amountReady); a.test(ebx, ebx); a.jle(empty);
                a.mov(r9d, ebx);
#if SERP_DEVDIAG
                a.mov(rcx, state); a.mov(__dword_ptr[rcx + 48], ebx); a.mov(__dword_ptr[rcx + 56], 1);
#endif
                a.pop(rcx); a.pop(rax); a.popfq();
                Replay(a, original); a.AddUnrestrictedJmp(back);
                a.Label(ref empty);
#if SERP_DEVDIAG
                a.mov(rcx, state); a.inc(__qword_ptr[rcx + 24]); a.mov(__dword_ptr[rcx + 56], 0);
#endif
                a.pop(rcx); a.pop(rax); a.popfq();
                // The entire native prologue already ran, including saved RBP.
                a.AddUnrestrictedJmp(image + SaleEpilog);
            }
            // Inactive candidates preserve the complete Vanilla block.
            a.Label(ref vanilla);
            a.pop(rax); a.popfq(); Replay(a, original); a.AddUnrestrictedJmp(back);
        }

#if SERP_DEVDIAG
        private static void Capture(Assembler a, ulong image, ulong state)
        {
            a.mov(rcx, state); a.inc(__qword_ptr[rcx + 16]);
            a.mov(__dword_ptr[rcx + 32], edi); a.mov(__dword_ptr[rcx + 36], r8d);
            a.mov(__dword_ptr[rcx + 40], ebx); a.mov(__dword_ptr[rcx + 44], eax);
            a.push(rax); a.imul(rax, rdi, PlayerStride); a.mov(rcx, image + GoldBase);
            a.mov(eax, __dword_ptr[rcx + rax]); a.mov(rcx, state); a.mov(__dword_ptr[rcx + 52], eax); a.pop(rax);
        }
#endif
        private static void Replay(Assembler a, ReadOnlySpan<Instruction> instructions)
        {
            foreach (var decoded in instructions)
            {
                // Two exclusive paths replay this branch-free block. Keep absolute RIP
                // targets, but remove duplicate instruction identities from BlockEncoder.
                var instruction = decoded; instruction.IP = 0; a.AddInstruction(instruction);
            }
        }
        internal static Instruction[] Decode(byte[] bytes, ulong address)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes)); decoder.IP = address;
            var result = new System.Collections.Generic.List<Instruction>();
            while (decoder.IP < address + (uint)bytes.Length)
            {
                var instruction = decoder.Decode();
                if (instruction.IsInvalid || instruction.NextIP > address + (uint)bytes.Length)
                    throw new InvalidOperationException("Invalid hook instruction boundary.");
                result.Add(instruction);
            }
            return result.ToArray();
        }
        internal static byte[] Assemble(Assembler assembler, ulong address)
        {
            using (var stream = new MemoryStream())
            { assembler.Assemble(new StreamCodeWriter(stream), address); return stream.ToArray(); }
        }
        internal static byte[] Hex(string text)
        {
            var result = new byte[text.Length / 2];
            for (int i = 0; i < result.Length; i++) result[i] = Convert.ToByte(text.Substring(i * 2, 2), 16);
            return result;
        }
    }
}
