using System;
using SHCDESE.Interop;

namespace BugfixesAndQoL
{
    internal static class CoopCustomLordSelectionPolicy
    {
        // FRONT_Multiplayer stores lord types zero-based, while the Extender enum includes SK_NULL.
        internal static int CustomPartnerLordType => checked((int)AILords.SK_X2 - 1);
        internal static ulong SharedProgressId => checked((ulong)CustomPartnerLordType + 1000UL);

        internal static int CalculatePortraitContentHeight(int customLordCount)
        {
            const int portraitColumns = 9;
            const int vanillaLordCount = 29;
            const int portraitTop = 20;
            const int portraitStep = 110;
            const int portraitSize = 100;
            int safeCustomCount = Math.Max(0, customLordCount);
            int rows = Math.Max(
                1,
                (vanillaLordCount + safeCustomCount + portraitColumns - 1) / portraitColumns);
            return portraitTop + (rows - 1) * portraitStep + portraitSize;
        }

        internal static string AppendLordPower(string displayName, int lordPower)
        {
            string name = displayName ?? string.Empty;
            string suffix = " (" + lordPower + ")";
            return name.EndsWith(suffix, StringComparison.Ordinal) ? name : name + suffix;
        }

        internal static string FormatHistoryName(
            string displayName,
            ulong partnerId,
            bool enhancementsEnabled)
        {
            string name = displayName ?? string.Empty;
            const string suffix = " (Custom Lord)";
            if (!enhancementsEnabled || partnerId != SharedProgressId ||
                name.EndsWith(suffix, StringComparison.Ordinal))
            {
                return name;
            }
            return name + suffix;
        }

        internal static bool ShouldShowDeleteButton(bool enhancementsEnabled, ulong partnerId) =>
            enhancementsEnabled && partnerId != 0UL;

        internal static bool CanConfirmProgressDeletion(
            ulong expectedPartnerId,
            ulong currentPartnerId,
            bool recordInDictionary,
            bool recordInOrderedList) =>
            expectedPartnerId != 0UL &&
            currentPartnerId == expectedPartnerId &&
            recordInDictionary &&
            recordInOrderedList;

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
