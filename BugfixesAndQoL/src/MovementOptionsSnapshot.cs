namespace BugfixesAndQoL
{
    internal enum RouteCalculationMode
    {
        Exact = 0,
        RequiredOnly = 1
    }

    internal readonly struct MovementOptionsSnapshot
    {
        internal MovementOptionsSnapshot(bool enabled, RouteCalculationMode routeMode)
        {
            Enabled = enabled;
            RouteMode = routeMode;
        }

        internal bool Enabled { get; }
        internal RouteCalculationMode RouteMode { get; }
        internal bool RequiredOnly => RouteMode == RouteCalculationMode.RequiredOnly;

        internal static MovementOptionsSnapshot Capture(BugfixesAndQoLViewModel settings)
        {
            FriendlyMoatMovementMode mode = settings.GetFriendlyMoatMovementMode();
            return new MovementOptionsSnapshot(
            settings.EnableMod && mode != FriendlyMoatMovementMode.Disabled,
            mode == FriendlyMoatMovementMode.RequiredOnly
                ? RouteCalculationMode.RequiredOnly
                : RouteCalculationMode.Exact);
        }
    }
}
