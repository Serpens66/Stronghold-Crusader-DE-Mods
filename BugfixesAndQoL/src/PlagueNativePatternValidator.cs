// Shared helper: resolve update-safe native signatures for plague fixes.
using BepInEx.Logging;
using System;

namespace BugfixesAndQoL
{
    internal static class PlagueNativePatternValidator
    {
        public static int Resolve(
            ManualLogSource log,
            ReadOnlySpan<byte> memory,
            string pattern,
            int referenceRva,
            bool referenceHashMatches,
            string name)
        {
            return Shared.NativePatternResolver.ResolveUnique(
                memory,
                pattern,
                referenceRva,
                referenceHashMatches,
                name,
                log).Rva;
        }
    }
}
