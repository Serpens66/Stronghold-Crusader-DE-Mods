using APIShared.GameModes;
using CrusaderDE;

namespace BugfixesAndQoL
{
    public static partial class TrailCustomizationLaunchOriginApi
    {
        internal static void RegisterModeProvider() =>
            CustomizedLaunchOrigins.Register(BugfixesAndQoLPlugin.PluginGuid, CaptureModeOrigin);

        private static CustomizedLaunchOrigin CaptureModeOrigin()
        {
            bool abandoned;
            lock (Sync)
            {
                // A pending Coop origin belongs to the Coop setup that announced it. Vanilla keeps
                // FRONT_Multiplayer.coopGame for that whole launch and resets it only when another
                // setup opens or a non-Coop lobby is joined. A start that failed or a lobby that
                // closed must not hand the origin to the next plain game. A restored save is loaded
                // from any screen and is therefore exempt.
                abandoned = origin == TrailCustomizationLaunchOriginKind.CustomizedCoopTrail &&
                    launchPending && !restoredFromSave && !FRONT_Multiplayer.coopGame;
                if (!abandoned)
                    return new CustomizedLaunchOrigin((CustomizedLaunchOriginKind)origin,
                        -1, trailId, missionId, restoredFromSave, launchPending,
                        supportsBuiltInOrigins: false);
            }
            Clear();
            return new CustomizedLaunchOrigin(CustomizedLaunchOriginKind.None,
                -1, -1, -1, false, false, supportsBuiltInOrigins: false);
        }
    }
}
