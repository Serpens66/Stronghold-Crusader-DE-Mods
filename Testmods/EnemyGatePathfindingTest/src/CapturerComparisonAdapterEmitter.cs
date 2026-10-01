using Iced.Intel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using static Iced.Intel.AssemblerRegisters;

namespace EnemyGatePathfindingTest
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void CapturerComparisonCallback(IntPtr context);

    // Context layout matches the installed X64SmartCPUContext (144 bytes). R11b
    // carries inequality only while the real R11 is saved on the outer stack.
    // The final TEST is after every call/stack cleanup and POP preserves its ZF.
    internal static class CapturerComparisonAdapterEmitter
    {
        private const int FrameSize = 240; // context + XMM0..5
        private static readonly AssemblerRegister64[] Gprs =
            { rax, rbx, rcx, rdx, rsi, rdi, rbp, rsp, r8, r9, r10, r11, r12, r13, r14, r15 };

        internal static void Emit(Assembler a, ReadOnlySpan<Instruction> original, ulong callback)
        {
            ValidateOriginal(original);
            foreach (Instruction instruction in original) a.AddInstruction(instruction);
            a.push(r11);
            a.setne(r11b);
            a.lea(rsp, __qword_ptr[rsp - FrameSize]);
            for (int i = 0; i < Gprs.Length; i++)
                if (i != 7) a.mov(__qword_ptr[rsp + i * 8], Gprs[i]);
            a.lea(rax, __qword_ptr[rsp + FrameSize]);
            a.mov(__qword_ptr[rsp + 56], rax);
            a.pushfq();
            a.pop(rax);
            a.mov(__qword_ptr[rsp + 128], rax);
            a.movdqu(__xmmword_ptr[rsp + 144], xmm0);
            a.movdqu(__xmmword_ptr[rsp + 160], xmm1);
            a.movdqu(__xmmword_ptr[rsp + 176], xmm2);
            a.movdqu(__xmmword_ptr[rsp + 192], xmm3);
            a.movdqu(__xmmword_ptr[rsp + 208], xmm4);
            a.movdqu(__xmmword_ptr[rsp + 224], xmm5);
            a.mov(rax, rsp);
            a.mov(rcx, rax);
            a.and(rsp, -16);
            a.sub(rsp, 48); // Win64 shadow space and saved context pointer
            a.mov(__qword_ptr[rsp + 32], rax);
            a.mov(rax, callback);
            a.call(rax);
            a.mov(rsp, __qword_ptr[rsp + 32]);
            a.movdqu(xmm0, __xmmword_ptr[rsp + 144]);
            a.movdqu(xmm1, __xmmword_ptr[rsp + 160]);
            a.movdqu(xmm2, __xmmword_ptr[rsp + 176]);
            a.movdqu(xmm3, __xmmword_ptr[rsp + 192]);
            a.movdqu(xmm4, __xmmword_ptr[rsp + 208]);
            a.movdqu(xmm5, __xmmword_ptr[rsp + 224]);
            for (int i = Gprs.Length - 1; i >= 0; i--)
                if (i != 7) a.mov(Gprs[i], __qword_ptr[rsp + i * 8]);
            a.lea(rsp, __qword_ptr[rsp + FrameSize]);
            a.test(r11b, r11b);
            a.pop(r11);
        }

        internal static byte[] AssembleAndValidate(byte[] bytes, ulong originalIp,
            ulong callback, ulong stubIp)
        {
            var d = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            d.IP = originalIp;
            var instructions = new List<Instruction>();
            while (d.IP < originalIp + (ulong)bytes.Length)
            {
                d.Decode(out Instruction instruction);
                if (instruction.Code == Code.INVALID) throw new InvalidOperationException("Invalid capturer block.");
                instructions.Add(instruction);
            }
            if (d.IP != originalIp + 20) throw new InvalidOperationException("Capturer span must be exactly 20 bytes.");
            var a = new Assembler(64);
            Emit(a, instructions.ToArray(), callback);
            using (var stream = new MemoryStream())
            {
                a.Assemble(new StreamCodeWriter(stream), stubIp);
                byte[] encoded = stream.ToArray();
                ValidateStub(encoded, stubIp);
                return encoded;
            }
        }

        private static void ValidateOriginal(ReadOnlySpan<Instruction> original)
        {
            if (original.Length != 3 || original[0].Mnemonic != Mnemonic.Movsxd ||
                original[0].Op0Register != Register.RCX || original[0].MemoryBase != Register.R9 ||
                original[0].MemoryDisplacement64 != unchecked((ulong)-12L) ||
                original[1].Mnemonic != Mnemonic.Imul || original[1].Op0Register != Register.RDX ||
                original[1].Op1Register != Register.RCX || original[1].Immediate32 != 0x32C ||
                original[2].Mnemonic != Mnemonic.Cmp ||
                original[2].MemoryDisplacement64 != EnemyGatePathfindingNativeDefinition.CapturedByPlayerTableDisplacement ||
                original[2].MemoryBase != Register.RDX ||
                (original[2].MemoryIndex != Register.RAX && original[2].MemoryIndex != Register.R13) ||
                original[2].NextIP - original[0].IP != 20)
                throw new InvalidOperationException("Capturer instructions differ from the audited block.");
        }

        private static void ValidateStub(byte[] bytes, ulong ip)
        {
            var d = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            d.IP = ip;
            var instructions = new List<Instruction>();
            while (d.IP < ip + (ulong)bytes.Length)
            {
                d.Decode(out Instruction instruction);
                if (instruction.Code == Code.INVALID || d.IP > ip + (ulong)bytes.Length)
                    throw new InvalidOperationException("Capturer adapter contains invalid code.");
                instructions.Add(instruction);
            }
            int end = instructions.Count;
            if (instructions[end - 2].Mnemonic != Mnemonic.Test ||
                instructions[end - 2].Op0Register != Register.R11L ||
                instructions[end - 1].Mnemonic != Mnemonic.Pop ||
                instructions[end - 1].Op0Register != Register.R11)
                throw new InvalidOperationException("Capturer adapter must end with TEST R11b; POP R11.");
        }
    }
}
