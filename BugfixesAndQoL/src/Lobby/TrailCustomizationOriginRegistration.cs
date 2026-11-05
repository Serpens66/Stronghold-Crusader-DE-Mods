using APIShared.GameModes;

namespace BugfixesAndQoL
{
    public static partial class TrailCustomizationLaunchOriginApi
    {
        internal static void RegisterModeProvider() =>
            CustomizedLaunchOrigins.Register(BugfixesAndQoLPlugin.PluginGuid, CaptureModeOrigin);

        private static CustomizedLaunchOrigin CaptureModeOrigin()
        {
            lock (Sync)
                return new CustomizedLaunchOrigin((CustomizedLaunchOriginKind)origin,
                    -1, trailId, missionId, restoredFromSave, launchPending,
                    supportsBuiltInOrigins: false);
        }
    }
}
