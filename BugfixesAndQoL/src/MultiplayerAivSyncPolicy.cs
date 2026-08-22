using System;

namespace BugfixesAndQoL
{
    internal static class MultiplayerAivSyncPolicy
    {
        public static bool CanAcceptHostPacket(
            bool localIsHost,
            bool featureActive,
            ulong senderSteamId,
            ulong lobbyOwnerSteamId) =>
            !localIsHost && featureActive && senderSteamId != 0 &&
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
    }
}
