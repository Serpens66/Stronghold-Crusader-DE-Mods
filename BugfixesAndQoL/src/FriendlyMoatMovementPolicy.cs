namespace BugfixesAndQoL
{
    internal enum FriendlyMoatMovementMode
    {
        Disabled = 0,
        Exact = 1,
        RequiredOnly = 2
    }

    internal static class FriendlyMoatMovementPolicy
    {
        internal const int DefaultMode = (int)FriendlyMoatMovementMode.RequiredOnly;

        internal static int Normalize(int value) => IsDefined(value)
            ? value
            : (int)FriendlyMoatMovementMode.Disabled;

        internal static bool IsDefined(int value) =>
            value >= (int)FriendlyMoatMovementMode.Disabled &&
            value <= (int)FriendlyMoatMovementMode.RequiredOnly;

        // The slider is ordered by increasing work: Off, Fast, Precise. Keep
        // persisted mode values stable because existing presets already use them.
        internal static int ToSliderValue(int mode)
        {
            switch ((FriendlyMoatMovementMode)Normalize(mode))
            {
                case FriendlyMoatMovementMode.RequiredOnly:
                    return 1;
                case FriendlyMoatMovementMode.Exact:
                    return 2;
                default:
                    return 0;
            }
        }

        internal static int FromSliderValue(int sliderValue)
        {
            switch (sliderValue)
            {
                case 1:
                    return (int)FriendlyMoatMovementMode.RequiredOnly;
                case 2:
                    return (int)FriendlyMoatMovementMode.Exact;
                default:
                    return (int)FriendlyMoatMovementMode.Disabled;
            }
        }
    }
}
