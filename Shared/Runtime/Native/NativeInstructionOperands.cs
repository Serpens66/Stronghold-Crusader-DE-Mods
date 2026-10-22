using Iced.Intel;
using System;

namespace Shared
{
    // Reads operands from one complete, validated x64 instruction of the loaded image. Callers pass the RVA
    // of an instruction at a fixed position inside a pattern-validated function; the result is then proven by
    // a second, independent evidence before use (see AGENTS/native-analysis.md "Prüfkosten").
    internal static class NativeInstructionOperands
    {
        private const uint ScnCntCode = 0x00000020, ScnCntInitializedData = 0x00000040, ScnCntUninitializedData = 0x00000080;
        private const uint ScnMemExecute = 0x20000000, ScnMemWrite = 0x80000000;

        /// <summary>Decodes the instruction at <paramref name="rva"/>; it must be valid and end inside the image.</summary>
        public static Instruction Decode(ReadOnlySpan<byte> memory, int rva)
        {
            if (rva < 0 || rva >= memory.Length) throw new InvalidOperationException($"Instruction RVA 0x{rva:X} is outside the image.");
            byte[] bytes = memory.Slice(rva, Math.Min(15, memory.Length - rva)).ToArray();
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = (ulong)rva;
            decoder.Decode(out Instruction instruction);
            if (instruction.IsInvalid || decoder.LastError != DecoderError.None)
                throw new InvalidOperationException($"Instruction at RVA 0x{rva:X} is invalid.");
            return instruction;
        }

        /// <summary>RIP-relative memory target of the instruction at <paramref name="rva"/>, which must lie in a data section.</summary>
        /// <param name="writable">True for globals in .data/.bss, false for read-only tables (.rdata).</param>
        public static int RipDataTarget(ReadOnlySpan<byte> memory, int rva, bool writable)
        {
            Instruction instruction = Decode(memory, rva);
            if (!instruction.IsIPRelativeMemoryOperand)
                throw new InvalidOperationException($"Instruction at RVA 0x{rva:X} has no RIP-relative memory operand.");
            ulong target = instruction.IPRelativeMemoryAddress;
            if (target >= (ulong)memory.Length) throw new InvalidOperationException($"RIP target of 0x{rva:X} is outside the image.");
            int result = (int)target;
            if (!InDataSection(memory, result, writable))
                throw new InvalidOperationException($"RIP target 0x{result:X} of 0x{rva:X} is not in a {(writable ? "writable" : "read-only")} data section.");
            return result;
        }

        /// <summary>Signed displacement of a register-based memory operand ([base(+index*scale)+disp]), not RIP-relative.</summary>
        public static int MemoryDisplacement(ReadOnlySpan<byte> memory, int rva, Register expectedBase = Register.None)
        {
            Instruction instruction = Decode(memory, rva);
            bool hasMemory = false;
            for (int operand = 0; operand < instruction.OpCount; operand++)
                if (instruction.GetOpKind(operand) == OpKind.Memory) hasMemory = true;
            if (!hasMemory || instruction.IsIPRelativeMemoryOperand || instruction.MemoryBase == Register.None)
                throw new InvalidOperationException($"Instruction at RVA 0x{rva:X} has no register-based memory operand.");
            if (expectedBase != Register.None && instruction.MemoryBase.GetFullRegister() != expectedBase.GetFullRegister())
                throw new InvalidOperationException($"Instruction at RVA 0x{rva:X} uses base {instruction.MemoryBase}, expected {expectedBase}.");
            return unchecked((int)(long)instruction.MemoryDisplacement64);
        }

        /// <summary>Immediate operand of the instruction at <paramref name="rva"/> (e.g. an imul stride).</summary>
        public static long Immediate(ReadOnlySpan<byte> memory, int rva, int operand)
        {
            Instruction instruction = Decode(memory, rva);
            if (operand < 0 || operand >= instruction.OpCount) throw new InvalidOperationException($"Instruction at RVA 0x{rva:X} has no operand {operand}.");
            switch (instruction.GetOpKind(operand))
            {
                case OpKind.Immediate8: case OpKind.Immediate16: case OpKind.Immediate32: case OpKind.Immediate64:
                case OpKind.Immediate8to16: case OpKind.Immediate8to32: case OpKind.Immediate8to64: case OpKind.Immediate32to64:
                    return (long)instruction.GetImmediate(operand);
                default:
                    throw new InvalidOperationException($"Operand {operand} at RVA 0x{rva:X} is not an immediate.");
            }
        }

        /// <summary>Whether <paramref name="rva"/> lies in a non-executable data section of the requested kind.</summary>
        public static bool InDataSection(ReadOnlySpan<byte> memory, int rva, bool writable)
        {
            if (memory.Length < 0x40 || memory[0] != 0x4D || memory[1] != 0x5A) return false;
            int pe = NativePatternResolver.ReadInt32(memory, 0x3C);
            if (pe < 0 || pe > memory.Length - 24 || memory[pe] != 0x50 || memory[pe + 1] != 0x45) return false;
            int count = memory[pe + 6] | memory[pe + 7] << 8;
            int optional = memory[pe + 20] | memory[pe + 21] << 8;
            int table = pe + 24 + optional;
            if (count <= 0 || count > 96 || table < 0 || table > memory.Length - count * 40) return false;
            for (int index = 0; index < count; index++)
            {
                int header = table + index * 40;
                uint flags = unchecked((uint)NativePatternResolver.ReadInt32(memory, header + 36));
                int size = Math.Max(NativePatternResolver.ReadInt32(memory, header + 8), NativePatternResolver.ReadInt32(memory, header + 16));
                int start = NativePatternResolver.ReadInt32(memory, header + 12);
                if (rva < start || rva >= start + size) continue;
                if ((flags & (ScnMemExecute | ScnCntCode)) != 0) return false;
                if ((flags & (ScnCntInitializedData | ScnCntUninitializedData)) == 0) return false;
                return ((flags & ScnMemWrite) != 0) == writable;
            }
            return false;
        }
    }
}
