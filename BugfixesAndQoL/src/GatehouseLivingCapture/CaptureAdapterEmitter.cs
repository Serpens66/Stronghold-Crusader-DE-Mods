using Iced.Intel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL.GatehouseLivingCapture
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void CaptureCallback(IntPtr context);

    // The installed context is 136 bytes, padded to 144; another 96 preserve volatile XMM0..5.
    // R11b temporarily carries eligibility. The real R11 stays on the outer stack.
    internal static class CaptureAdapterEmitter
    {
        private const int FrameSize = 240;
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
            a.lea(rax, __qword_ptr[rsp + FrameSize + 8]);
            a.mov(__qword_ptr[rsp + 56], rax);
            a.pushfq(); a.pop(rax);
            a.mov(__qword_ptr[rsp + 128], rax);
            a.movdqu(__xmmword_ptr[rsp + 144], xmm0);
            a.movdqu(__xmmword_ptr[rsp + 160], xmm1);
            a.movdqu(__xmmword_ptr[rsp + 176], xmm2);
            a.movdqu(__xmmword_ptr[rsp + 192], xmm3);
            a.movdqu(__xmmword_ptr[rsp + 208], xmm4);
            a.movdqu(__xmmword_ptr[rsp + 224], xmm5);
            a.mov(rax, rsp); a.mov(rcx, rax);
            a.and(rsp, -16); a.sub(rsp, 48);
            a.mov(__qword_ptr[rsp + 32], rax);
            a.mov(rax, callback); a.call(rax);
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
            // All later flags are overwritten before use, except ZF consumed by the original JE.
            // TEST follows every flag-changing wrapper operation; POP and the return JMP keep ZF.
            a.test(r11b, r11b); a.pop(r11);
        }

        private static void ValidateOriginal(ReadOnlySpan<Instruction> original)
        {
            if (original.Length != 3 || original[0].Mnemonic != Mnemonic.Cdqe ||
                original[1].Mnemonic != Mnemonic.Imul || original[1].Op0Register != Register.RDI ||
                original[1].Op1Register != Register.RAX || original[1].Immediate32 != NativeDefinition.UnitSize ||
                original[2].Mnemonic != Mnemonic.Cmp || original[2].MemorySize != MemorySize.UInt16 ||
                original[2].MemoryBase != Register.RDI || original[2].MemoryIndex != Register.R13 ||
                original[2].MemoryIndexScale != 1 || original[2].Op1Register != Register.R14W ||
                original[2].MemoryDisplacement64 != NativeDefinition.EligibilityDisplacement ||
                original[0].Length != 2 || original[1].Length != 7 || original[2].Length != 9 ||
                original[2].NextIP - original[0].IP != NativeDefinition.HookLength)
                throw new InvalidOperationException("Capture instructions differ from the audited 18-byte block.");
        }

        internal static byte[] AssembleAndValidate(byte[] bytes, ulong originalIp, ulong callback, ulong stubIp)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes)); decoder.IP = originalIp;
            var instructions = new List<Instruction>();
            while (decoder.IP < originalIp + (ulong)bytes.Length)
            {
                decoder.Decode(out Instruction instruction);
                if (instruction.Code == Code.INVALID) throw new InvalidOperationException("Invalid capture block.");
                instructions.Add(instruction);
            }
            if (decoder.IP != originalIp + NativeDefinition.HookLength)
                throw new InvalidOperationException("Capture block does not end at 18 bytes.");
            var assembler = new Assembler(64);
            Emit(assembler, instructions.ToArray(), callback);
            using (var stream = new MemoryStream())
            {
                assembler.Assemble(new StreamCodeWriter(stream), stubIp);
                byte[] result = stream.ToArray();
                ValidateStub(result, stubIp);
                return result;
            }
        }

        internal static void ValidateStub(byte[] bytes, ulong ip)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes)); decoder.IP = ip;
            var instructions = new List<Instruction>();
            while (decoder.IP < ip + (ulong)bytes.Length)
            {
                decoder.Decode(out Instruction instruction);
                if (instruction.Code == Code.INVALID || decoder.IP > ip + (ulong)bytes.Length)
                    throw new InvalidOperationException("Invalid capture adapter code.");
                instructions.Add(instruction);
            }
            int end = instructions.Count;
            if (instructions[end - 2].Mnemonic != Mnemonic.Test ||
                instructions[end - 2].Op0Register != Register.R11L ||
                instructions[end - 1].Mnemonic != Mnemonic.Pop || instructions[end - 1].Op0Register != Register.R11)
                throw new InvalidOperationException("Adapter must end with TEST R11b; POP R11.");
        }
    }
}
