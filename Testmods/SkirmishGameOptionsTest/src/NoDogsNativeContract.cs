using Iced.Intel;
using System;

namespace SkirmishGameOptionsTest
{
    internal static class NoDogsNativeContract
    {
        internal const string ReferenceSha256 =
            "FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2";
        internal const int PatchRva = 0xFFD98;
        internal const ulong PreferredImageBase = 0x180000000;

        internal static readonly byte[] ExpectedBytes = { 0x74, 0x31 };
        internal static readonly byte[] ReplacementBytes = { 0x90, 0x90 };

        internal static void Validate(
            ReadOnlySpan<byte> memory,
            ulong imageBase,
            bool referenceHashMatches)
        {
            if (!referenceHashMatches)
                throw new InvalidOperationException(
                    "No Dogs Skirmish enforcement is available only for the audited CrusaderDE.dll.");
            if (PatchRva < 0 || PatchRva + ExpectedBytes.Length > memory.Length)
                throw new InvalidOperationException("No Dogs patch RVA is outside the native image.");
            if (!memory.Slice(PatchRva, ExpectedBytes.Length).SequenceEqual(ExpectedBytes))
                throw new InvalidOperationException("No Dogs patch bytes do not match Vanilla.");

            ValidateInstruction(ExpectedBytes, imageBase + PatchRva, FlowControl.ConditionalBranch, 2);
            ValidateInstruction(ReplacementBytes, imageBase + PatchRva, FlowControl.Next, 1);
        }

        internal static void ValidateInstruction(
            byte[] bytes,
            ulong instructionPointer,
            FlowControl expectedFlowControl,
            int expectedFirstLength)
        {
            var decoder = Decoder.Create(64, new ByteArrayCodeReader(bytes));
            decoder.IP = instructionPointer;
            Instruction instruction = decoder.Decode();
            if (instruction.IsInvalid ||
                instruction.FlowControl != expectedFlowControl ||
                instruction.Length != expectedFirstLength)
            {
                throw new InvalidOperationException(
                    $"Unexpected instruction at 0x{instructionPointer:X}: {instruction}, " +
                    $"flow={instruction.FlowControl}, length={instruction.Length}.");
            }
        }
    }
}
