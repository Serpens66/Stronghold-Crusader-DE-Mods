using System;
using System.Collections.Generic;
using System.IO;
using Iced.Intel;
using RedBird.X64.Extensions;
using static Iced.Intel.AssemblerRegisters;

namespace Spectator
{
    internal static class NativePowerAssembly
    {
        internal static readonly int[] Sites = { 0x105593, 0x8CAF8, 0x8CE20, 0x105627 };
        internal static readonly string[] Hex = {
            "486315D6E77D084869CA3C580000",
            "4C8D15B122FD034C633D6A728508",
            "44893D0D456308E8646BF9FF41B802000000",
            "B277488D0DF0EC4608E85BE3F1FF"
        };
        internal static byte[] Bytes(int site)
        {
            string value = Hex[site]; var bytes = new byte[value.Length / 2];
            for (int i = 0; i < bytes.Length; i++) bytes[i] = Convert.ToByte(value.Substring(i * 2, 2), 16);
            return bytes;
        }
        internal static Instruction[] Decode(byte[] bytes, ulong address)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes)); decoder.IP = address;
            var result = new List<Instruction>();
            while (decoder.IP < address + (ulong)bytes.Length)
            {
                Instruction instruction = decoder.Decode();
                if (instruction.IsInvalid || instruction.NextIP > address + (ulong)bytes.Length)
                    throw new InvalidDataException("Power instruction boundary changed.");
                result.Add(instruction);
            }
            return result.ToArray();
        }
        internal static byte[] Assemble(Assembler a, ulong address)
        {
            using (var stream = new MemoryStream()) { a.Assemble(new StreamCodeWriter(stream), address); return stream.ToArray(); }
        }

        // Same stack/ABI as Vanilla's entry; supply the explicit actor to the first resource read.
        // Only the local-human availability prefix is bypassed. The native pause/cost/selection body remains authoritative.
        internal static void SelectionGateway(Assembler a, ulong module)
        {
            a.push(rbx); a.sub(rsp, 32); a.mov(ebx, ecx); a.mov(eax, edx);
            a.AddUnrestrictedJmp(module + 0x105548);
        }

        internal static void Hook(Assembler a, ReadOnlySpan<Instruction> original, ulong continuation,
            int site, ulong callback)
        {
            Label vanilla = a.CreateLabel("vanilla"), finish = a.CreateLabel("finish");
            a.pushfq(); a.push(rax);
            a.X64FastcallSafe(callback, 1, x => x.mov(ecx, site), preserveRAX: false, preserveXMM: true);
            // The wrapper changes flags; branch only on a freshly computed result.
            a.test(eax, eax); a.je(vanilla);
            if (site == 0) a.mov(edx, eax);
            if (site == 1) a.mov(r15d, eax);
            a.pop(rax); a.popfq();
            if (site < 3)
            {
                Instruction replay = original[site == 0 ? 1 : site == 1 ? 0 : 2];
                replay.IP = 0; // This instruction is also present in the inactive branch; avoid duplicate relocation identities.
                a.AddInstruction(replay);
            }
            // Submission: managed callback captured the accepted Vanilla target; do not also enqueue its Chore.
            a.jmp(finish);
            a.Label(ref vanilla);
            a.pop(rax); a.popfq();
            foreach (Instruction instruction in original) a.AddInstruction(instruction);
            a.Label(ref finish);
            a.AddUnrestrictedJmp(continuation);
        }
    }
}
