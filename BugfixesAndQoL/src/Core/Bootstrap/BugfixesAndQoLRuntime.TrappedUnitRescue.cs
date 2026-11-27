using System.Runtime.CompilerServices;
using SHCDESE.API.LowLevel;
using BugfixesAndQoL.TrappedUnitRescue;

// Separate acceptance automation uses the real production objects, never a second kernel.
[assembly: InternalsVisibleTo("TrappedUnitRescueTest")]

namespace BugfixesAndQoL
{
    public sealed partial class BugfixesAndQoLRuntime
    {
        private TrappedUnitRescueFeature trappedUnitRescueFeature;

        private TrappedUnitRescueFeature EnsureTrappedUnitRescueFeature() =>
            trappedUnitRescueFeature ?? (trappedUnitRescueFeature = new TrappedUnitRescueFeature(
                BugfixesAndQoLPlugin.PluginGuid, log,
                () => settings.EnableMod && settings.EnableTrappedUnitRescue));

        private void InitializeTrappedUnitRescue(CrusaderLibraryLoadContext context)
        {
            var feature = EnsureTrappedUnitRescueFeature();
            feature.Initialize(context);
            // A lifecycle replay may have arrived before native initialization.
            feature.ApplySettings();
        }
    }
}
