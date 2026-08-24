namespace BugfixesAndQoL
{
    internal static class StartupDiagnosticPolicy
    {
        // Settings can be applied before CrusaderDE.dll is available. That state is an
        // expected startup deferral, not evidence that the current native layout is invalid.
        internal static bool ShouldReportFixedLayoutFailure(
            bool nativeLibraryAvailable,
            bool fixedLayoutHashValidated,
            bool settingEnabled,
            bool alreadyReported)
        {
            return nativeLibraryAvailable &&
                settingEnabled &&
                !fixedLayoutHashValidated &&
                !alreadyReported;
        }
    }
}
