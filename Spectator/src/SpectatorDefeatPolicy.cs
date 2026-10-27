namespace Spectator
{
    internal static class SpectatorDefeatPolicy
    {
        internal static bool IsStatisticsViewer(bool spectator, int player, bool validLord) =>
            spectator || (player >= 1 && player <= 8 && !validLord);
        internal static bool ConfirmIdentity(int expected, int nativePlayer, int managedPlayer) =>
            expected >= 1 && expected <= 8 && expected == nativePlayer && expected == managedPlayer;

        internal static bool CanPromoteEliminatedPlayerToSpectator(
            bool featureEnabled,
            bool activeMatch,
            bool mapEditor,
            bool alreadySpectator,
            bool supportedGameMode,
            bool validLocalParticipant,
            bool lordDeathObserved,
            int localPlayerId) =>
            featureEnabled &&
            activeMatch &&
            !mapEditor &&
            !alreadySpectator &&
            supportedGameMode &&
            validLocalParticipant &&
            lordDeathObserved &&
            localPlayerId >= 1 && localPlayerId <= 8;

        internal static int ResolvePresentedGameOverState(
            int state,
            bool spectatorPromotionRequested) =>
            state == 1 && spectatorPromotionRequested ? 2 : state;

        internal static bool ShouldLatchPlayerLordDeath(int notificationPlayerId, int localPlayerId) =>
            notificationPlayerId >= 1 &&
            notificationPlayerId <= 8 &&
            notificationPlayerId == localPlayerId;

    }
}
