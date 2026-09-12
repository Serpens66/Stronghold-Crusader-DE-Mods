using System;
using System.Globalization;

namespace APIShared
{
    internal sealed class CompiledBytePattern
    {
        private readonly byte[] values;
        private readonly bool[] wildcards;
        private readonly int anchorIndex;

        private CompiledBytePattern(byte[] values, bool[] wildcards, int anchorIndex)
        {
            this.values = values;
            this.wildcards = wildcards;
            this.anchorIndex = anchorIndex;
        }

        internal int Length => values.Length;

        internal static CompiledBytePattern Parse(string pattern)
        {
            if (pattern == null)
                throw new ArgumentNullException(nameof(pattern));

            string[] tokens = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0)
                throw new ArgumentException("Native pattern is empty.", nameof(pattern));

            var values = new byte[tokens.Length];
            var wildcards = new bool[tokens.Length];
            int anchorIndex = -1;
            for (int index = 0; index < tokens.Length; index++)
            {
                bool wildcard = tokens[index] == "?" || tokens[index] == "??";
                wildcards[index] = wildcard;
                if (wildcard)
                    continue;

                values[index] = byte.Parse(tokens[index], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                anchorIndex = index;
            }
            return new CompiledBytePattern(values, wildcards, anchorIndex);
        }

        internal int FindUnique(ReadOnlySpan<byte> memory)
        {
            if (memory.Length < values.Length)
                return -1;

            int found = -1;
            int maximumOffset = memory.Length - values.Length;
            if (anchorIndex < 0)
            {
                for (int offset = 0; offset <= maximumOffset; offset++)
                {
                    if (found >= 0)
                        return -2;
                    found = offset;
                }
                return found;
            }

            int anchorPosition = anchorIndex;
            int maximumAnchorPosition = maximumOffset + anchorIndex;
            while (anchorPosition <= maximumAnchorPosition)
            {
                int relative = memory.Slice(anchorPosition, maximumAnchorPosition - anchorPosition + 1)
                    .IndexOf(values[anchorIndex]);
                if (relative < 0)
                    break;

                anchorPosition += relative;
                int offset = anchorPosition - anchorIndex;
                if (MatchesAt(memory, offset))
                {
                    if (found >= 0)
                        return -2;
                    found = offset;
                }
                anchorPosition++;
            }
            return found;
        }

        private bool MatchesAt(ReadOnlySpan<byte> memory, int offset)
        {
            for (int index = 0; index < values.Length; index++)
            {
                if (!wildcards[index] && memory[offset + index] != values[index])
                    return false;
            }
            return true;
        }
    }
}
