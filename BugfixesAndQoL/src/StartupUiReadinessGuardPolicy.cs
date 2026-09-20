namespace BugfixesAndQoL
{
    internal static class StartupUiReadinessGuardPolicy
    {
        internal static bool ShouldRunVanillaUiUpdateBlock(
            bool viewModelLoaded,
            bool hudMainAvailable,
            bool frontEndMenuAvailable) =>
            viewModelLoaded && hudMainAvailable && frontEndMenuAvailable;
    }
}
