// Shared helper: resolve native signatures for plague fixes after the fixed layout is validated.
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
            // These callbacks use GameUnit/GameProjectile fields and, for popularity,
            // fixed player-manager offsets that are not completely encoded by any one
            // code signature. A unique pattern alone therefore cannot prove the full
            // callback contract on an unknown binary.
            if (!referenceHashMatches)
            {
                throw new InvalidOperationException(
                    $"The native {name} remains inactive because its fixed unit, projectile, and player layouts are not validated for this CrusaderDE.dll.");
            }

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
