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
    }
}
