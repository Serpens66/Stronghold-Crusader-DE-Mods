using APIShared.GameModes;

namespace ExtendedData
{
    public static partial class ExtendedDataLaunchOriginApi
    {
        internal static void RegisterModeProvider() =>
            CustomizedLaunchOrigins.Register(ExtendedDataPlugin.PluginGuid, CaptureModeOrigin);

        private static CustomizedLaunchOrigin CaptureModeOrigin()
        {
            lock (Sync)
                return new CustomizedLaunchOrigin((CustomizedLaunchOriginKind)origin,
                    trailType, trailId, missionId, restoredFromSave, launchPending,
                    supportsBuiltInOrigins: true);
        }
    }
}
