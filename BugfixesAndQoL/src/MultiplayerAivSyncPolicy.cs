using System;

namespace BugfixesAndQoL
{
    internal static class MultiplayerAivSyncPolicy
    {
        public static bool CanAcceptHostPacket(
            bool localIsHost,
            ulong senderSteamId,
            ulong lobbyOwnerSteamId) =>
            !localIsHost && senderSteamId != 0 &&
            senderSteamId == lobbyOwnerSteamId;

        public static bool IsCurrentResponse(
            bool localIsHost,
            bool transferInProgress,
            bool senderExpected,
            int packetGeneration,
            int currentGeneration,
            string packetManifestHash,
            string currentManifestHash,
            string transferRoster,
            string currentRoster) =>
            localIsHost && transferInProgress && senderExpected &&
            packetGeneration == currentGeneration &&
            string.Equals(packetManifestHash, currentManifestHash, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(transferRoster, currentRoster, StringComparison.Ordinal);

        public static bool HasContextChanged(
            string transferRoster,
            string currentRoster,
            string transferSelection,
            string currentSelection) =>
            !string.Equals(transferRoster, currentRoster, StringComparison.Ordinal) ||
            !string.Equals(transferSelection, currentSelection, StringComparison.Ordinal);

        public static bool CanUseConfirmedManifest(
            bool featureActive,
            int coopTrailId,
            bool hasManifest,
            ulong currentLobbyId,
            ulong manifestLobbyId) =>
            featureActive && coopTrailId == 0 && hasManifest && currentLobbyId != 0 &&
            currentLobbyId == manifestLobbyId;

        public static bool IsVanillaChecksumReady(string expected, string current) =>
            !string.IsNullOrEmpty(expected) &&
            string.Equals(expected, current, StringComparison.Ordinal);

        public static bool RequiresTransfer(
            bool hasExtendedCandidates,
            ulong currentLobbyId,
            ulong previousExtendedTransferLobbyId) =>
            hasExtendedCandidates ||
            (currentLobbyId != 0 && currentLobbyId == previousExtendedTransferLobbyId);
    }
}
