using SHCDESE.Interop;

namespace BugfixesAndQoL
{
    internal static class SingleBuildingPausePolicy
    {
        internal const int SleepTableLength = 91;
        internal static unsafe bool TryReadTypeSleeping(
            GamePlayerResources* resources, int buildingType, out bool sleeping)
        {
            sleeping = false;
            // The native type-sleep table has 91 byte entries, independently of STRUCT_MAX.
            if (resources == null || buildingType < 0 ||
                buildingType >= SleepTableLength)
                return false;

            sleeping = resources->r_BuildingSleep[buildingType] != 0;
            return true;
        }

        internal static bool TargetSleeping(bool buildingSleeping, bool? manualOverride) =>
            !(manualOverride ?? buildingSleeping);

        internal static bool NeedsTypeToggle(bool typeSleeping, bool targetSleeping) =>
            typeSleeping != targetSleeping;

        internal static short ResolveDisplayedSleeping(
            short vanilla, bool active, bool resolved, bool buildingSleeping, bool? manualOverride) =>
            active && resolved ? (short)((manualOverride ?? buildingSleeping) ? 1 : 0) : vanilla;
    }
}
