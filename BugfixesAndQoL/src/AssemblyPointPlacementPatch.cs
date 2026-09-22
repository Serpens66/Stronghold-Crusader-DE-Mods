// Feature: Permit barracks and keep rally points despite native reachability rejection.
using BepInEx.Logging;
using RedBird.Core.Memory;
using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    internal sealed class AssemblyPointPlacementPatch : IDisposable
    {
        private const string ConstructingFailureStatusPattern =
            "45 84 ED 74 3D 85 C9 BA AC 00 00 00 B8 0D 00 18 00 BB 0D 00 00 00 0F 44 D8";
        private const string EuropeanPlacementRejectPattern = "85 C9 0F 84 ?? ?? ?? ?? 8B 05 ?? ?? ?? ?? 05 B4 FE FF FF";
        private const string MercenaryPlacementRejectPattern = "85 C9 0F 84 ?? ?? ?? ?? 8B 05 ?? ?? ?? ?? 05 A2 FE FF FF";
        private const string EngineerPlacementRejectPattern = "85 C9 0F 84 ?? ?? ?? ?? 8B 05 ?? ?? ?? ?? 05 A5 FE FF FF";
        private const string TunnelerPlacementRejectPattern = "85 C9 0F 84 ?? ?? ?? ?? C7 05 ?? ?? ?? ?? 1E 00 00 00 E9";
        private const string KnightPlacementRejectPattern = "85 D2 0F 84 ?? ?? ?? ?? 8B 05 ?? ?? ?? ?? 89 05";
        private const string BedouinPlacementRejectPattern = "85 C9 0F 84 ?? ?? ?? ?? 8B 05 ?? ?? ?? ?? 05 AB FE FF FF";

        private const int ConstructingFailureStatusRva = 0x9129E;
        private const int EuropeanPlacementRejectRva = 0x929D3;
        private const int MercenaryPlacementRejectRva = 0x928E0;
        private const int EngineerPlacementRejectRva = 0x926FA;
        private const int TunnelerPlacementRejectRva = 0x912E0;
        private const int KnightPlacementRejectRva = 0x913CF;
        private const int BedouinPlacementRejectRva = 0x927ED;

        private readonly PermanentInstructionSkipPatch patch;
        private bool disposed;

        public AssemblyPointPlacementPatch(
            ManualLogSource log,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            bool referenceHashMatches)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));
            var sites = new List<PermanentInstructionSkipPatch.Site>
            {
                ResolveSite(memory, libraryBase, ConstructingFailureStatusPattern,
                    ConstructingFailureStatusRva, referenceHashMatches, 17,
                    new byte[] { 0xBB, 0x0D, 0x00, 0x00, 0x00, 0x0F, 0x44, 0xD8 },
                    16, 16, "shared preview failure status", log, 1),
                ResolveSite(memory, libraryBase, EuropeanPlacementRejectPattern,
                    EuropeanPlacementRejectRva, referenceHashMatches, 2,
                    new byte[] { 0x0F, 0x84 }, 6, 17, "European troop placement rejection", log),
                ResolveSite(memory, libraryBase, MercenaryPlacementRejectPattern,
                    MercenaryPlacementRejectRva, referenceHashMatches, 2,
                    new byte[] { 0x0F, 0x84 }, 6, 17, "mercenary troop placement rejection", log),
                ResolveSite(memory, libraryBase, EngineerPlacementRejectPattern,
                    EngineerPlacementRejectRva, referenceHashMatches, 2,
                    new byte[] { 0x0F, 0x84 }, 6, 17, "engineer placement rejection", log),
                ResolveSite(memory, libraryBase, TunnelerPlacementRejectPattern,
                    TunnelerPlacementRejectRva, referenceHashMatches, 2,
                    new byte[] { 0x0F, 0x84 }, 6, 16, "tunneler placement rejection", log),
                ResolveSite(memory, libraryBase, KnightPlacementRejectPattern,
                    KnightPlacementRejectRva, referenceHashMatches, 2,
                    new byte[] { 0x0F, 0x84 }, 6, 18, "knight placement rejection", log),
                ResolveSite(memory, libraryBase, BedouinPlacementRejectPattern,
                    BedouinPlacementRejectRva, referenceHashMatches, 2,
                    new byte[] { 0x0F, 0x84 }, 6, 17, "Bedouin troop placement rejection", log)
            };
            patch = new PermanentInstructionSkipPatch(region, sites.ToArray());
            patch.SetEnabled(true);
            Shared.DebugLogHelper.LogDebug(log, $"Assembly-point permanent hooks installed; sites={sites.Count}.");
        }

        internal void SetEnabled(bool enabled)
        {
            if (!disposed) patch.SetEnabled(enabled);
        }

        public void Dispose()
        {
            if (disposed) return;
            patch.SetEnabled(false);
            disposed = true;
        }

        private static PermanentInstructionSkipPatch.Site ResolveSite(
            ReadOnlySpan<byte> memory,
            ulong libraryBase,
            string pattern,
            int referenceRva,
            bool referenceHashMatches,
            int patternOffset,
            byte[] expectedOpcode,
            int minimumHookSize,
            int expectedDisplacedBytes,
            string label,
            ManualLogSource log,
            int skippedInstructionIndex = 0)
        {
            int resolvedRva = Shared.NativePatternResolver.ResolveUnique(
                memory, pattern, referenceRva, referenceHashMatches, label, log).Rva;
            int siteRva = checked(resolvedRva + patternOffset);
            if (siteRva < 0 || siteRva + expectedOpcode.Length > memory.Length ||
                !memory.Slice(siteRva, expectedOpcode.Length).SequenceEqual(expectedOpcode))
            {
                throw new InvalidOperationException($"The native {label} opcode did not match expectations.");
            }
            return new PermanentInstructionSkipPatch.Site(
                libraryBase + unchecked((ulong)siteRva), minimumHookSize,
                expectedDisplacedBytes, skippedInstructionIndex, 1, label);
        }
    }
}
