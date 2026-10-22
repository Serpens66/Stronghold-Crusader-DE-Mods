namespace BugfixesAndQoL.UnitCommands
{
    // Only the settings/provider boundary is doubled. Capture itself is linked from
    // production: independent traversal must survive H=false, without enabling own rules.
    internal interface IUnitCommandSettings
    {
        bool EnableMod { get; }
        bool EnableImprovedManualUnitCommands { get; }
    }
    internal sealed class TraversalSettings : IUnitCommandSettings
    {
        public bool EnableMod { get; set; }
        public bool EnableImprovedManualUnitCommands { get; set; }
    }
    internal sealed class UnitCommandTraversalProvider
    {
        internal bool Enabled, RequiredOnly;
    }
    internal static class UnitCommandPathAPI
    {
        internal static UnitCommandTraversalProvider Traversal;
    }
}
