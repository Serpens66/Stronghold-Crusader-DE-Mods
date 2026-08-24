using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Shared
{
    internal enum NativePatternSearchScope
    {
        ExecutableSections,
        EntireImage
    }

    internal static class NativePatternResolver
    {
        private const uint ImageScnMemExecute = 0x20000000;

        public static NativeResolution ResolveUnique(
            ReadOnlySpan<byte> memory,
            string pattern,
            int referenceRva,
            bool referenceHashMatches,
            string name,
            ManualLogSource log = null,
            NativePatternSearchScope searchScope = NativePatternSearchScope.ExecutableSections)
        {
            PatternByte[] bytes = ParsePattern(pattern);
            int rva;
            string method;

            if (referenceHashMatches && MatchesPatternAt(memory, referenceRva, bytes))
            {
                rva = referenceRva;
                method = "reference-rva";
            }
            else
            {
                rva = FindUniquePattern(memory, bytes, name, searchScope);
                method = referenceHashMatches
                    ? "signature-fallback-after-rva-validation-failure"
                    : "signature-fallback";
            }

            DebugLogHelper.LogInfo(log, $"Native address resolved: name={name}, method={method}, rva=0x{rva:X}.");
            return new NativeResolution(rva, method);
        }

        public static NativeResolution ResolveUnique(
            ReadOnlySpan<byte> memory,
            byte[] pattern,
            int referenceRva,
            bool referenceHashMatches,
            string name,
            ManualLogSource log = null,
            NativePatternSearchScope searchScope = NativePatternSearchScope.EntireImage)
        {
            if (pattern == null || pattern.Length == 0)
                throw new ArgumentException("Native byte pattern is empty.", nameof(pattern));

            int rva;
            string method;
            if (referenceHashMatches && MatchesBytesAt(memory, referenceRva, pattern))
            {
                rva = referenceRva;
                method = "reference-rva";
            }
            else
            {
                rva = FindUniqueBytes(memory, pattern, name, searchScope);
                method = referenceHashMatches
                    ? "signature-fallback-after-rva-validation-failure"
                    : "signature-fallback";
            }

            DebugLogHelper.LogInfo(log, $"Native address resolved: name={name}, method={method}, rva=0x{rva:X}.");
            return new NativeResolution(rva, method);
        }

        public static int FindUniquePattern(
            ReadOnlySpan<byte> memory,
            string pattern,
            string name,
            NativePatternSearchScope searchScope = NativePatternSearchScope.ExecutableSections) =>
            FindUniquePattern(memory, ParsePattern(pattern), name, searchScope);

        public static bool MatchesPatternAt(ReadOnlySpan<byte> memory, int offset, string pattern) =>
            MatchesPatternAt(memory, offset, ParsePattern(pattern));

        public static NativeCodeRange[] GetExecutableCodeRanges(ReadOnlySpan<byte> memory)
        {
            if (memory.Length < 0x40 || memory[0] != 0x4D || memory[1] != 0x5A)
                throw new InvalidOperationException("native module has no valid DOS header.");

            int peOffset = ReadInt32(memory, 0x3C);
            if (peOffset < 0 || peOffset > memory.Length - 24 ||
                memory[peOffset] != 0x50 || memory[peOffset + 1] != 0x45 ||
                memory[peOffset + 2] != 0 || memory[peOffset + 3] != 0)
            {
                throw new InvalidOperationException("native module has no valid PE header.");
            }

            int sectionCount = ReadUInt16(memory, peOffset + 6);
            int optionalHeaderSize = ReadUInt16(memory, peOffset + 20);
            if (sectionCount <= 0 || sectionCount > 96)
                throw new InvalidOperationException($"native module section count {sectionCount} is implausible.");

            int sectionTable = checked(peOffset + 24 + optionalHeaderSize);
            if (sectionTable < 0 || sectionTable > memory.Length - sectionCount * 40)
                throw new InvalidOperationException("native module section table is outside the loaded image.");

            List<NativeCodeRange> ranges = new List<NativeCodeRange>();
            for (int index = 0; index < sectionCount; index++)
            {
                int header = sectionTable + index * 40;
                uint characteristics = ReadUInt32(memory, header + 36);
                if ((characteristics & ImageScnMemExecute) == 0)
                    continue;

                int virtualSize = checked((int)ReadUInt32(memory, header + 8));
                int virtualAddress = checked((int)ReadUInt32(memory, header + 12));
                int rawSize = checked((int)ReadUInt32(memory, header + 16));
                int length = Math.Max(virtualSize, rawSize);
                if (virtualAddress < 0 || virtualAddress >= memory.Length || length <= 0)
                    continue;

                ranges.Add(new NativeCodeRange(virtualAddress, Math.Min(length, memory.Length - virtualAddress)));
            }

            if (ranges.Count == 0)
                throw new InvalidOperationException("native module contains no executable PE section.");
            return ranges.ToArray();
        }

        public static int ResolveRelativeTarget(ReadOnlySpan<byte> memory, int displacementRva, int nextInstructionRva)
        {
            if (displacementRva < 0 || displacementRva > memory.Length - sizeof(int))
                throw new InvalidOperationException("relative native target displacement is outside the module image.");
            return checked(nextInstructionRva + ReadInt32(memory, displacementRva));
        }

        public static int ReadInt32(ReadOnlySpan<byte> memory, int offset)
        {
            if (offset < 0 || offset > memory.Length - sizeof(int))
                throw new ArgumentOutOfRangeException(nameof(offset));
            return memory[offset] |
                memory[offset + 1] << 8 |
                memory[offset + 2] << 16 |
                memory[offset + 3] << 24;
        }

        private static int FindUniquePattern(
            ReadOnlySpan<byte> memory,
            PatternByte[] pattern,
            string name,
            NativePatternSearchScope searchScope)
        {
            int match = -1;
            int count = 0;
            foreach (NativeCodeRange range in GetSearchRanges(memory, searchScope))
            {
                int end = range.Offset + range.Length - pattern.Length;
                for (int offset = range.Offset; offset <= end; offset++)
                {
                    if (!MatchesPatternAt(memory, offset, pattern))
                        continue;
                    match = offset;
                    if (++count > 1)
                        throw new InvalidOperationException($"{name} semantic AOB matched more than once.");
                }
            }

            if (count != 1)
                throw new InvalidOperationException($"{name} semantic AOB was not found.");
            return match;
        }

        private static int FindUniqueBytes(
            ReadOnlySpan<byte> memory,
            byte[] pattern,
            string name,
            NativePatternSearchScope searchScope)
        {
            int match = -1;
            int count = 0;
            foreach (NativeCodeRange range in GetSearchRanges(memory, searchScope))
            {
                int end = range.Offset + range.Length - pattern.Length;
                for (int offset = range.Offset; offset <= end; offset++)
                {
                    if (!MatchesBytesAt(memory, offset, pattern))
                        continue;
                    match = offset;
                    if (++count > 1)
                        throw new InvalidOperationException($"{name} byte pattern matched more than once.");
                }
            }

            if (count != 1)
                throw new InvalidOperationException($"{name} byte pattern was not found.");
            return match;
        }

        private static NativeCodeRange[] GetSearchRanges(
            ReadOnlySpan<byte> memory,
            NativePatternSearchScope searchScope) =>
            searchScope == NativePatternSearchScope.ExecutableSections
                ? GetExecutableCodeRanges(memory)
                : new[] { new NativeCodeRange(0, memory.Length) };

        private static bool MatchesPatternAt(ReadOnlySpan<byte> memory, int offset, PatternByte[] pattern)
        {
            if (offset < 0 || offset > memory.Length - pattern.Length)
                return false;
            for (int index = 0; index < pattern.Length; index++)
            {
                if (!pattern[index].Wildcard && memory[offset + index] != pattern[index].Value)
                    return false;
            }
            return true;
        }

        private static bool MatchesBytesAt(ReadOnlySpan<byte> memory, int offset, byte[] pattern)
        {
            if (offset < 0 || offset > memory.Length - pattern.Length)
                return false;
            for (int index = 0; index < pattern.Length; index++)
            {
                if (memory[offset + index] != pattern[index])
                    return false;
            }
            return true;
        }

        private static PatternByte[] ParsePattern(string pattern)
        {
            string[] tokens = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            PatternByte[] result = new PatternByte[tokens.Length];
            for (int index = 0; index < tokens.Length; index++)
            {
                bool wildcard = tokens[index] == "?" || tokens[index] == "??";
                result[index] = new PatternByte(
                    wildcard ? (byte)0 : byte.Parse(tokens[index], NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    wildcard);
            }
            return result;
        }

        private static int ReadUInt16(ReadOnlySpan<byte> memory, int offset) =>
            memory[offset] | memory[offset + 1] << 8;

        private static uint ReadUInt32(ReadOnlySpan<byte> memory, int offset) =>
            (uint)(memory[offset] |
                memory[offset + 1] << 8 |
                memory[offset + 2] << 16 |
                memory[offset + 3] << 24);

        private readonly struct PatternByte
        {
            public PatternByte(byte value, bool wildcard)
            {
                Value = value;
                Wildcard = wildcard;
            }

            public byte Value { get; }
            public bool Wildcard { get; }
        }
    }

    internal readonly struct NativeResolution
    {
        public NativeResolution(int rva, string method)
        {
            Rva = rva;
            Method = method;
        }

        public int Rva { get; }
        public string Method { get; }
    }

    internal readonly struct NativeCodeRange
    {
        public NativeCodeRange(int offset, int length)
        {
            Offset = offset;
            Length = length;
        }

        public int Offset { get; }
        public int Length { get; }
    }
}
