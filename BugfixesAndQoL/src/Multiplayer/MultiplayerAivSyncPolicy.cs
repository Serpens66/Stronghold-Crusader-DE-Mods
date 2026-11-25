using System;

namespace BugfixesAndQoL
{
    internal sealed class MultiplayerAivMissionManifest
    {
        private long session;
        private ulong lobby;
        private byte[] payload;
        private bool ready;
        private bool replacement;

        internal void RecordLoad(long sessionId, ulong lobbyId, byte[] confirmedPayload)
        {
            if (confirmedPayload == null) return;
            session = sessionId;
            lobby = lobbyId;
            payload = confirmedPayload;
            ready = replacement = false;
        }

        internal void RecordStart(long sessionId)
        {
            if (session != sessionId) return;
            if (replacement) Clear();
            else ready = true;
        }

        internal void RecordEnd(long sessionId, long? linkedReplacement)
        {
            if (session != sessionId) return;
            if (ready && linkedReplacement.HasValue)
            {
                session = linkedReplacement.Value;
                ready = false;
                replacement = true;
                return;
            }
            Clear();
        }

        internal byte[] TakeReplacement(ulong lobbyId, bool explicitRestart)
        {
            if (!replacement || !explicitRestart || lobby != lobbyId) return null;
            byte[] result = payload;
            Clear();
            return result;
        }

        private void Clear()
        {
            session = 0;
            lobby = 0;
            payload = null;
            ready = replacement = false;
        }
    }

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
            bool customCoopGame,
            bool hasManifest,
            ulong currentLobbyId,
            ulong manifestLobbyId) =>
            featureActive && (coopTrailId >= 0 || customCoopGame) &&
            hasManifest && currentLobbyId != 0 &&
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
