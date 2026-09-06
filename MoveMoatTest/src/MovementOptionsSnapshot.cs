namespace MoveMoatTest
{
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

        internal static MovementOptionsSnapshot Capture() => new MovementOptionsSnapshot(
            MoveMoatTestPlugin.Settings.EnableMod,
            MoveMoatTestPlugin.Settings.RouteMode == (int)RouteCalculationMode.RequiredOnly
                ? RouteCalculationMode.RequiredOnly
                : RouteCalculationMode.Exact);
    }
}
