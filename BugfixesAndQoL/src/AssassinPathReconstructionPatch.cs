// Feature: Let Vanilla reconstruct a validated Assassin climb route through a walkable reservation.
using BepInEx.Logging;
using RedBird.Core.Memory;
using System;

namespace BugfixesAndQoL
{
    internal sealed class AssassinPathReconstructionPatch
    {
        private const int CurrentMinimumHookSize = 6;
        private const int CurrentExpectedDisplacedBytes = 18;
        private const int NeighborMinimumHookSize = 6;
        private const int NeighborExpectedDisplacedBytes = 23;

        private readonly PermanentInstructionSkipPatch patch;
        private bool applied;

        public AssassinPathReconstructionPatch(
            ManualLogSource log,
            IntPtr libraryHandle,
            ScanRegion region,
            ReadOnlySpan<byte> memory,
            bool referenceHashMatches)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));
            if (libraryHandle == IntPtr.Zero) throw new ArgumentException("native library handle is null", nameof(libraryHandle));
            if (region == null) throw new ArgumentNullException(nameof(region));

            Shared.NativeResolution resolution = Shared.NativePatternResolver.ResolveUnique(
                memory,
                AssassinPathReconstructionNativeDefinition.EndpointBuildingGuardsPattern,
                AssassinPathReconstructionNativeDefinition.EndpointBuildingGuardsPatternRva,
                referenceHashMatches,
                "Assassin path reconstruction endpoint-building guards",
                log);
            int currentRva = ValidateSite(
                memory,
                resolution.Rva,
                AssassinPathReconstructionNativeDefinition.CurrentTileRejectJumpOffset,
                AssassinPathReconstructionNativeDefinition.OriginalCurrentTileRejectJump,
                "current route tile building guard");
            int neighborRva = ValidateSite(
                memory,
                resolution.Rva,
                AssassinPathReconstructionNativeDefinition.NeighborTileRejectJumpOffset,
                AssassinPathReconstructionNativeDefinition.OriginalNeighborTileRejectJump,
                "neighbor route tile building guard");
            ulong libraryBase = unchecked((ulong)libraryHandle.ToInt64());
            patch = new PermanentInstructionSkipPatch(
                region,
                new PermanentInstructionSkipPatch.Site(
                    libraryBase + unchecked((ulong)currentRva),
                    CurrentMinimumHookSize,
                    CurrentExpectedDisplacedBytes,
                    1,
                    "Assassin current-tile building rejection"),
                new PermanentInstructionSkipPatch.Site(
                    libraryBase + unchecked((ulong)neighborRva),
                    NeighborMinimumHookSize,
                    NeighborExpectedDisplacedBytes,
                    1,
                    "Assassin neighbor-tile building rejection"));

            Shared.DebugLogHelper.LogDebug(
                log,
                $"Assassin path reconstruction permanent hooks installed: " +
                $"currentRva=0x{currentRva:X}, currentDisplaced={CurrentExpectedDisplacedBytes}, " +
                $"neighborRva=0x{neighborRva:X}, neighborDisplaced={NeighborExpectedDisplacedBytes}.");
        }

        public bool IsApplied => applied;

        public void SetEnabled(bool enabled)
        {
            patch.SetEnabled(enabled);
            applied = enabled;
        }

        private static int ValidateSite(
            ReadOnlySpan<byte> memory,
            int patternRva,
            int jumpOffset,
            byte[] originalBytes,
            string label)
        {
            int patchRva = checked(patternRva + jumpOffset);
            if (patchRva < 0 || patchRva + originalBytes.Length > memory.Length ||
                !memory.Slice(patchRva, originalBytes.Length).SequenceEqual(originalBytes))
            {
                throw new InvalidOperationException(
                    $"Assassin path reconstruction {label} did not match the validated Vanilla bytes.");
            }
            return patchRva;
        }
    }
}
