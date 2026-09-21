// Feature: Pure policy for the local high-contrast yellow presentation.
namespace BugfixesAndQoL
{
    internal enum LobbyYellowResourceState
    {
        Baseline,
        Gold,
        Foreign,
    }

    internal static class LobbyYellowContrastPolicy
    {
        internal const byte TeamBrushAlpha = 184;
        internal const byte GoldRed = 184;
        internal const byte GoldGreen = 134;
        internal const byte GoldBlue = 11;

        internal static bool IsEnabled(
            bool enableMod,
            bool enableClientFeatures,
            bool improveYellowLobbyContrast) =>
            enableMod && enableClientFeatures && improveYellowLobbyContrast;

        internal static LobbyYellowResourceState ClassifyResourceState(
            bool sourceIsBaseline,
            bool sourceRectIsBaseline,
            bool sourceIsGold,
            bool sourceRectIsGold)
        {
            if (sourceIsBaseline && sourceRectIsBaseline)
                return LobbyYellowResourceState.Baseline;
            if (sourceIsGold && sourceRectIsGold)
                return LobbyYellowResourceState.Gold;
            return LobbyYellowResourceState.Foreign;
        }

        internal static bool CanApplyGold(LobbyYellowResourceState state) =>
            state == LobbyYellowResourceState.Baseline ||
            state == LobbyYellowResourceState.Gold;

        internal static bool ShouldRestoreBaseline(
            LobbyYellowResourceState state) =>
            state == LobbyYellowResourceState.Gold;
    }
}
