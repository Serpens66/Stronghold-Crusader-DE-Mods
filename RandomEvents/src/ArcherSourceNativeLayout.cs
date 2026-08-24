using Shared;
using System;
using System.Collections.Generic;

namespace RandomEvents
{
    internal readonly struct ArcherSourceNativeResolution
    {
        public ArcherSourceNativeResolution(int rva, int sourceXOffset, string method)
        {
            Rva = rva;
            SourceXOffset = sourceXOffset;
            Method = method;
        }

        public int Rva { get; }
        public int SourceXOffset { get; }
        public int SourceYOffset => SourceXOffset + sizeof(int);
        public string Method { get; }
    }

    internal static class ArcherSourceNativeLayout
    {
        internal const int ReferenceLoadRva = 0x104E13;
        internal const int ReferenceSignpostIdsOffset = 0x18388C;
        internal const int SourceRecordStride = 0x10;
        private const int ValidationWindow = 0x80;
        internal const string Pattern =
            "48 63 C8 48 8D 1D ?? ?? ?? ?? 8B 05 ?? ?? ?? ?? 48 03 C9 89 44 24 30 BA 03 00 00 00 " +
            "8B 05 ?? ?? ?? ?? C7 44 24 28 16 00 00 00 49 8D 34 CE 89 44 24 20 " +
            "44 8B 8E ?? ?? ?? ?? 49 8D 3C CE 44 8B 87 ?? ?? ?? ??";

        public static ArcherSourceNativeResolution Resolve(
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches,
            int signpostIdsOffset)
        {
            int rva;
            string method;
            if (referenceHashMatches)
            {
                rva = ReferenceLoadRva;
                method = "reference-rva";
            }
            else
            {
                rva = NativePatternResolver.FindUniquePattern(memory, Pattern, "archer signpost source");
                method = "signature-fallback";
            }

            if (!TryValidate(memory, rva, signpostIdsOffset, out int sourceXOffset, out string failure))
            {
                throw new InvalidOperationException(
                    $"archer source {method} candidate 0x{rva:X} failed validation: {failure}");
            }
            return new ArcherSourceNativeResolution(rva, sourceXOffset, method);
        }

        internal static bool TryValidate(
            ReadOnlySpan<byte> memory,
            int candidateRva,
            int signpostIdsOffset,
            out int sourceXOffset,
            out string failure)
        {
            sourceXOffset = -1;
            failure = string.Empty;
            if (candidateRva < 0 || candidateRva > memory.Length - 3 ||
                memory[candidateRva] != 0x48 || memory[candidateRva + 1] != 0x63 || memory[candidateRva + 2] != 0xC8)
            {
                failure = "candidate does not begin with the selected-slot sign extension.";
                return false;
            }

            int end = Math.Min(memory.Length - 7, candidateRva + ValidationWindow);
            List<int> xOffsets = new List<int>();
            List<int> yOffsets = new List<int>();
            for (int offset = candidateRva; offset <= end; offset++)
            {
                if (memory[offset] != 0x44 || memory[offset + 1] != 0x8B)
                    continue;
                if (memory[offset + 2] == 0x87)
                    xOffsets.Add(NativePatternResolver.ReadInt32(memory, offset + 3));
                else if (memory[offset + 2] == 0x8E)
                    yOffsets.Add(NativePatternResolver.ReadInt32(memory, offset + 3));
            }

            if (xOffsets.Count != 2 || yOffsets.Count != 2 ||
                xOffsets[0] != xOffsets[1] || yOffsets[0] != yOffsets[1])
            {
                failure = $"expected two matching X/Y loads but found X=[{string.Join(",", xOffsets)}], " +
                    $"Y=[{string.Join(",", yOffsets)}].";
                return false;
            }
            if (yOffsets[0] != xOffsets[0] + sizeof(int))
            {
                failure = $"source Y offset 0x{yOffsets[0]:X} does not immediately follow X offset 0x{xOffsets[0]:X}.";
                return false;
            }
            if (xOffsets[0] != signpostIdsOffset + 0x40)
            {
                failure = $"source X offset 0x{xOffsets[0]:X} is not signpost slots 0x{signpostIdsOffset:X} + 0x40.";
                return false;
            }
            if (!ContainsBytes(memory, candidateRva, end, new byte[] { 0x48, 0x03, 0xC9 }) ||
                !ContainsBytes(memory, candidateRva, end, new byte[] { 0x49, 0x8D, 0x34, 0xCE }) ||
                !ContainsBytes(memory, candidateRva, end, new byte[] { 0x49, 0x8D, 0x3C, 0xCE }))
            {
                failure = $"selected-slot scaling does not prove the expected 0x{SourceRecordStride:X}-byte coordinate-record stride.";
                return false;
            }

            sourceXOffset = xOffsets[0];
            return true;
        }

        private static bool ContainsBytes(ReadOnlySpan<byte> memory, int start, int end, byte[] needle)
        {
            for (int offset = start; offset <= end - needle.Length; offset++)
            {
                int index = 0;
                while (index < needle.Length && memory[offset + index] == needle[index])
                    index++;
                if (index == needle.Length)
                    return true;
            }
            return false;
        }
    }
}
