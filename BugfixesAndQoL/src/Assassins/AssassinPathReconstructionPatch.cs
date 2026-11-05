// Feature: Register weighted reconstruction policy with the sole APIShared hook owner.
using APIShared;
using BepInEx.Logging;
using RedBird.Core.Memory;
using System;

namespace BugfixesAndQoL
{
    internal sealed class AssassinPathReconstructionPatch
    {
        private bool applied;

        public AssassinPathReconstructionPatch(ManualLogSource log, IntPtr libraryHandle,
            ScanRegion region, ReadOnlySpan<byte> memory, bool referenceHashMatches)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));
            if (libraryHandle == IntPtr.Zero || region == null || !referenceHashMatches)
                throw new InvalidOperationException("Shared Assassin reconstruction requires the validated native library.");
        }

        public bool IsApplied => applied;

        public void SetEnabled(bool enabled)
        {
            AssassinPathAPI.SetWeightedReconstructionEnabled(BugfixesAndQoLPlugin.PluginGuid, enabled);
            applied = enabled;
        }
    }
}
