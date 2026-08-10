// Shared helper: Require update-safe unique native signatures for plague fixes.
using System;
using System.Globalization;


namespace BugfixesAndQoL
{
    internal static class PlagueNativePatternValidator
    {
        public static void ValidateUnique(ReadOnlySpan<byte> memory, string pattern, string name)
        {
            string[] tokens = pattern.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            int[] expected = new int[tokens.Length];
            for (int index = 0; index < tokens.Length; index++)
            {
                if (tokens[index] == "??")
                {
                    expected[index] = -1;
                    continue;
                }

                if (!byte.TryParse(
                        tokens[index],
                        NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture,
                        out byte value))
                {
                    throw new InvalidOperationException($"Invalid AOB token '{tokens[index]}' in {name}.");
                }
                expected[index] = value;
            }

            int matchCount = 0;
            for (int offset = 0; offset <= memory.Length - expected.Length; offset++)
            {
                bool matches = true;
                for (int index = 0; index < expected.Length; index++)
                {
                    if (expected[index] >= 0 && memory[offset + index] != expected[index])
                    {
                        matches = false;
                        break;
                    }
                }

                if (!matches)
                    continue;

                matchCount++;
                if (matchCount > 1)
                    break;
            }

            if (matchCount != 1)
            {
                throw new InvalidOperationException(
                    $"The {name} signature matched {matchCount} times instead of exactly once.");
            }
        }
    }
}
