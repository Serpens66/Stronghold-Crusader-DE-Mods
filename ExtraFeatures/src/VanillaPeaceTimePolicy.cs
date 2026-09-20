using APIShared;
using Shared;

namespace ExtraFeatures
{
    internal enum VanillaPeaceTimeLobbyDirection
    {
        None,
        ModSettingToVanilla,
        VanillaToModSetting
    }

    internal static class VanillaPeaceTimePolicy
    {
        internal const int MinimumMinutes = 0;
        internal const int MaximumMinutes = 60;

        internal static int NormalizeMinutes(int minutes) =>
            minutes < MinimumMinutes ? MinimumMinutes :
            minutes > MaximumMinutes ? MaximumMinutes : minutes;

        internal static bool ShouldOverrideMission(
            MissionContext context,
            bool enableMod,
            bool modeAllowed) =>
            context != null &&
            enableMod &&
            modeAllowed &&
            context.StartKind == MissionStartKind.NewGame &&
            !context.IsEditor &&
            context.MapType != MissionMapType.FreeBuild;

        internal static bool IsMissionModeAllowed(GameModeSnapshot snapshot)
        {
            if (snapshot.HasConflictingCustomizedOrigin)
                return false;

            switch (GameplayModModePolicy.ResolveContext(snapshot))
            {
                case GameplayModAllowedContext.CustomGame:
                case GameplayModAllowedContext.CustomizedVanillaTrail:
                case GameplayModAllowedContext.CustomizedCustomTrail:
                case GameplayModAllowedContext.CustomizedCoopTrail:
                case GameplayModAllowedContext.CustomizedSandsOfTime:
                case GameplayModAllowedContext.CustomTrail:
                case GameplayModAllowedContext.CoopTrail:
                    return true;
                default:
                    return false;
            }
        }

        internal static VanillaPeaceTimeLobbyDirection ResolveLobbyDirection(
            bool hasLobby,
            bool isHost,
            bool enableMod,
            bool initializedForLobby) =>
            !hasLobby || !isHost || !enableMod
                ? VanillaPeaceTimeLobbyDirection.None
                : initializedForLobby
                    ? VanillaPeaceTimeLobbyDirection.VanillaToModSetting
                    : VanillaPeaceTimeLobbyDirection.ModSettingToVanilla;
    }
}
