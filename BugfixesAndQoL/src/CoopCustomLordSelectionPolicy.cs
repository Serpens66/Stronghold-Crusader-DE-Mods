using SHCDESE.Interop;

namespace BugfixesAndQoL
{
    internal static class CoopCustomLordSelectionPolicy
    {
        // FRONT_Multiplayer stores lord types zero-based, while the Extender enum includes SK_NULL.
        internal static int CustomPartnerLordType => checked((int)AILords.SK_X2 - 1);
        internal static ulong SharedProgressId => checked((ulong)CustomPartnerLordType + 1000UL);

        internal static bool CanSelect(
            bool settingEnabled,
            bool coopGame,
            bool customCoopGame,
            bool panelVisible,
            bool isHost,
            int humanPlayers) =>
            settingEnabled && coopGame && !customCoopGame && panelVisible && isHost && humanPlayers == 1;

        internal static bool ShouldReplaceDefaultAiv(
            bool coopGame,
            bool customCoopGame,
            bool singlePlayerCoop,
            bool customSelectionActive,
            int selectedAllyLordType,
            int lordType,
            int playerId) =>
            coopGame && !customCoopGame && singlePlayerCoop && customSelectionActive &&
            selectedAllyLordType == CustomPartnerLordType &&
            lordType == CustomPartnerLordType && playerId == 2;
    }
}
