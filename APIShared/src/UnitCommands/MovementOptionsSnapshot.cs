namespace APIShared.UnitCommands
{
    internal enum RouteCalculationMode
    {
        Exact = 0,
        RequiredOnly = 1
    }

    internal readonly struct MovementOptionsSnapshot
    {
        internal MovementOptionsSnapshot(bool enabled, RouteCalculationMode routeMode, bool traversalEnabled = false)
        {
            Enabled = enabled;
            TraversalEnabled = traversalEnabled;
            RouteMode = routeMode;
        }

        internal bool Enabled { get; }
        internal bool TraversalEnabled { get; }
        internal RouteCalculationMode RouteMode { get; }
        internal bool RequiredOnly => RouteMode == RouteCalculationMode.RequiredOnly;

        internal static MovementOptionsSnapshot Capture(IUnitCommandSettings settings)
        {
            UnitCommandTraversalProvider provider = UnitCommandPathAPI.Traversal;
            bool traversal = settings.EnableMod && provider?.Enabled == true;
            return new MovementOptionsSnapshot(
                settings.EnableMod && (settings.EnableImprovedManualUnitCommands || traversal),
                provider?.RequiredOnly == true ? RouteCalculationMode.RequiredOnly : RouteCalculationMode.Exact,
                traversal);
        }
    }
}
