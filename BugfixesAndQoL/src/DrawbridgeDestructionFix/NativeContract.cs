using Iced.Intel;
using RedBird.X64.Extensions;
using SHCDESE.Interop;
using System;
using System.IO;
using System.Runtime.InteropServices;
using static Iced.Intel.AssemblerRegisters;

namespace BugfixesAndQoL.DrawbridgeDestructionFix
{
    internal static class NativeContract
    {
        internal static bool IsEnabled(bool enableMod, bool enableFix) => enableMod && enableFix;
        internal const string Hash = "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const int HookRva = 0x7EE6B, SpanLength = 15, SkipRva = 0x7EF87;
        internal const int FunctionRva = 0x7EB00, FunctionSize = 0xD8A;
        // Native manager slot includes a 0x5C header before the public GameBuilding view.
        internal const int TypeOffset = 0x12E, HealthOffset = 0x168;
        internal static readonly byte[] Signature = {
            0x41,0x83,0xFD,0x01,0x0F,0x85,0x12,0x01,0x00,0x00,
            0x66,0x41,0x83,0xE8,0x2D,0x66,0x45,0x3B,0xC5,0x0F,0x87,0x03,0x01,0x00,0x00 };

        internal static void ValidateLayouts()
        {
            if (Marshal.SizeOf(typeof(GameBuilding)) != 0x32C ||
                Marshal.OffsetOf(typeof(GameBuilding), "r_CurrentHealth").ToInt32() + 0x5C != HealthOffset ||
                Marshal.OffsetOf(typeof(GameBuilding), "r_BuildingType").ToInt32() + 0x5C != TypeOffset ||
                (int)eStructs.STRUCT_GATE_MAIN != 0x2D || (int)eStructs.STRUCT_GATE_INNER != 0x2E ||
                (int)eStructs.STRUCT_DRAWBRIDGE != 0x31)
                throw new InvalidOperationException("Native/public field or enum contract differs.");
        }

        internal static bool Matches(ReadOnlySpan<byte> image, int offset)
        {
            return offset >= 0 && offset <= image.Length - Signature.Length &&
                image.Slice(offset, Signature.Length).SequenceEqual(Signature);
        }

        internal static int Resolve(ReadOnlySpan<byte> image, bool referenceHashMatches, out string method)
        {
            if (referenceHashMatches && Matches(image, HookRva)) { method = "reference-rva"; return HookRva; }
            if (image.Length < 0x100 || image[0] != 'M' || image[1] != 'Z') throw new InvalidOperationException("Invalid PE image.");
            int pe = BitConverter.ToInt32(image.Slice(0x3C, 4).ToArray(), 0);
            if (pe < 0 || pe > image.Length - 24 || image[pe] != 'P' || image[pe + 1] != 'E') throw new InvalidOperationException("Invalid PE header.");
            int sections = BitConverter.ToUInt16(image.Slice(pe + 6, 2).ToArray(), 0);
            int table = pe + 24 + BitConverter.ToUInt16(image.Slice(pe + 20, 2).ToArray(), 0);
            int found = -1;
            for (int section = 0; section < sections; section++)
            {
                int h = checked(table + section * 40);
                if (h > image.Length - 40) throw new InvalidOperationException("Invalid section table.");
                uint flags = BitConverter.ToUInt32(image.Slice(h + 36, 4).ToArray(), 0);
                if ((flags & 0x20000000) == 0) continue;
                int begin = BitConverter.ToInt32(image.Slice(h + 12, 4).ToArray(), 0);
                int size = BitConverter.ToInt32(image.Slice(h + 8, 4).ToArray(), 0);
                if (begin < 0 || size < 0 || begin > image.Length - size) throw new InvalidOperationException("Invalid executable section.");
                for (int offset = begin; offset <= begin + size - Signature.Length; offset++)
                    if (Matches(image, offset))
                    {
                        if (found != -1) throw new InvalidOperationException("Ambiguous executable signature.");
                        found = offset;
                    }
            }
            if (found < 0) throw new InvalidOperationException("Signature absent, including fallback after RVA failure.");
            method = "executable-pattern-fallback";
            return found;
        }

        // fileSha256 is the caller's uppercase SHA-256 of the file at path (the runtime passes the
        // process-wide cached hash instead of hashing the 3.4 MB DLL again).
        internal static byte[] ReadReferenceFunction(string path, string fileSha256)
        {
            if (!string.Equals(fileSha256, Hash, StringComparison.Ordinal))
                throw new InvalidOperationException("Unknown native hash; fixed structure offsets are hash-bound.");
            byte[] raw = File.ReadAllBytes(path);
            int pe = BitConverter.ToInt32(raw, 0x3C);
            int table = pe + 24 + BitConverter.ToUInt16(raw, pe + 20);
            for (int n = 0; n < BitConverter.ToUInt16(raw, pe + 6); n++)
            {
                int h = table + n * 40, rva = BitConverter.ToInt32(raw, h + 12), size = BitConverter.ToInt32(raw, h + 16);
                if (FunctionRva >= rva && FunctionRva + FunctionSize <= rva + size)
                {
                    byte[] result = new byte[FunctionSize];
                    Buffer.BlockCopy(raw, BitConverter.ToInt32(raw, h + 20) + FunctionRva - rva, result, 0, result.Length);
                    return result;
                }
            }
            throw new InvalidOperationException("Audited function is outside raw PE sections.");
        }

        internal static Instruction[] DecodeSpan(ulong address)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(Signature));
            decoder.IP = address;
            var instructions = new System.Collections.Generic.List<Instruction>();
            int bytes = 0;
            while (bytes < SpanLength)
            {
                decoder.Decode(out Instruction instruction);
                if (instruction.IsInvalid) throw new InvalidOperationException("Invalid hook instruction.");
                instructions.Add(instruction); bytes += instruction.Length;
            }
            if (bytes != SpanLength || instructions.Count != 3) throw new InvalidOperationException("Unexpected displaced span.");
            return instructions.ToArray();
        }

        // No managed call: preserve every register and restore flags before original instructions/skip.
        // State is data-only: enabled, entered, suppressed (three Int32 words).
        internal static void Emit(Assembler asm, ReadOnlySpan<Instruction> original, ulong continuation, ulong skip, ulong state)
        {
            Label vanilla = asm.CreateLabel();
            asm.pushfq(); asm.push(rax); asm.mov(rax, state);
            asm.inc(__dword_ptr[rax + 4]);
            asm.cmp(__dword_ptr[rax], 0); asm.je(vanilla);
            asm.cmp(r13d, 1); asm.jne(vanilla);
            asm.cmp(__word_ptr[r15 + rbp + TypeOffset], (int)eStructs.STRUCT_GATE_MAIN); asm.jb(vanilla);
            asm.cmp(__word_ptr[r15 + rbp + TypeOffset], (int)eStructs.STRUCT_GATE_INNER); asm.ja(vanilla);
            asm.cmp(__word_ptr[r15 + rbp + HealthOffset], 0); asm.jle(vanilla);
            asm.inc(__dword_ptr[rax + 8]);
            asm.pop(rax); asm.popfq(); asm.jmp(skip);
            asm.Label(ref vanilla);
            asm.pop(rax); asm.popfq(); asm.AddInstructions(original); asm.jmp(continuation);
        }
    }
}
