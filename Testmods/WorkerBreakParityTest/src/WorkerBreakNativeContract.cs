using Iced.Intel;
using System;

namespace WorkerBreakParityTest
{
    internal sealed class WorkerBreakPatchSite
    {
        internal WorkerBreakPatchSite(string name, int rva, int targetRva, byte[] expected,
            byte[] replacement, byte[] signature, int signatureOffset, int functionStart, int functionEnd)
        {
            Name = name;
            Rva = rva;
            TargetRva = targetRva;
            Expected = expected;
            Replacement = replacement;
            Signature = signature;
            SignatureOffset = signatureOffset;
            FunctionStart = functionStart;
            FunctionEnd = functionEnd;
        }

        internal string Name { get; }
        internal int Rva { get; }
        internal int TargetRva { get; }
        internal byte[] Expected { get; }
        internal byte[] Replacement { get; }
        internal byte[] Signature { get; }
        internal int SignatureOffset { get; }
        internal int FunctionStart { get; }
        internal int FunctionEnd { get; }
    }

    internal static class WorkerBreakNativeContract
    {
        internal const string ReferenceSha256 =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const int TextStart = 0x1000;
        internal const int TextEnd = 0x20A200;

        internal static readonly WorkerBreakPatchSite[] Sites =
        {
            new WorkerBreakPatchSite("miller", 0x1383F3, 0x138467,
                new byte[] { 0x74, 0x72 }, new byte[] { 0xEB, 0x72 },
                new byte[] { 0x85, 0xC0, 0x74, 0x72, 0x8B, 0x15 }, 2,
                0x1377C0, 0x138850),
            new WorkerBreakPatchSite("baker", 0x139596, 0x139651,
                new byte[] { 0x0F, 0x84, 0xB5, 0x00, 0x00, 0x00 },
                new byte[] { 0xE9, 0xB6, 0x00, 0x00, 0x00, 0x90 },
                new byte[] { 0x85, 0xC0, 0x0F, 0x84, 0xB5, 0x00, 0x00, 0x00,
                    0x48, 0x69, 0xFD, 0x2C, 0x03, 0x00, 0x00 }, 2,
                0x138850, 0x139950)
        };

        internal static int Resolve(ReadOnlySpan<byte> memory, ulong imageBase,
            WorkerBreakPatchSite site)
        {
            if (memory.Length < TextEnd)
                throw new InvalidOperationException("Native .text is shorter than the audited range.");
            if (memory.Slice(site.Rva, site.Expected.Length).SequenceEqual(site.Expected))
                return ValidateResolved(memory, imageBase, site, site.Rva);

            int count = 0;
            int candidate = -1;
            for (int offset = TextStart; offset <= TextEnd - site.Signature.Length; offset++)
            {
                if (!memory.Slice(offset, site.Signature.Length).SequenceEqual(site.Signature))
                    continue;
                candidate = offset + site.SignatureOffset;
                count++;
            }
            if (count != 1 || candidate != site.Rva)
                throw new InvalidOperationException(site.Name +
                    " patch bytes changed and its unique Vanilla signature was not found at the audited RVA; matches=" + count);
            return ValidateResolved(memory, imageBase, site, candidate);
        }

        private static int ValidateResolved(ReadOnlySpan<byte> memory, ulong imageBase,
            WorkerBreakPatchSite site, int rva)
        {
            if (rva < site.FunctionStart || rva + site.Expected.Length > site.FunctionEnd ||
                !memory.Slice(rva, site.Expected.Length).SequenceEqual(site.Expected))
                throw new InvalidOperationException(site.Name + " patch site is outside the audited function or modified.");

            ValidateInstruction(site.Expected, imageBase + (uint)rva,
                FlowControl.ConditionalBranch, site.Expected.Length, imageBase + (uint)site.TargetRva);
            ValidateInstruction(site.Replacement, imageBase + (uint)rva,
                FlowControl.UnconditionalBranch, site.Expected.Length == 2 ? 2 : 5,
                imageBase + (uint)site.TargetRva);
            if (site.Replacement.Length != site.Expected.Length)
                throw new InvalidOperationException(site.Name + " patch changes instruction span.");
            return rva;
        }

        private static void ValidateInstruction(byte[] bytes, ulong ip, FlowControl flow,
            int length, ulong target)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = ip;
            Instruction instruction = decoder.Decode();
            if (instruction.IsInvalid || instruction.Length != length ||
                instruction.FlowControl != flow || instruction.NearBranchTarget != target)
                throw new InvalidOperationException(
                    $"Unexpected native branch at 0x{ip:X}: {instruction}; target=0x{target:X}.");
            if (bytes.Length == 6 && flow == FlowControl.UnconditionalBranch)
            {
                Instruction padding = decoder.Decode();
                if (padding.IsInvalid || padding.Mnemonic != Mnemonic.Nop || padding.Length != 1)
                    throw new InvalidOperationException("Baker patch padding is not one NOP instruction.");
            }
        }

        internal static void Emit(Assembler assembler, WorkerBreakPatchSite site, ulong imageBase)
        {
            assembler.jmp(imageBase + (uint)site.TargetRva);
            if (site.Expected.Length == 6)
                assembler.nop();
        }
    }
}
