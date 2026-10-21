namespace Spectator
{
    internal static class SpectatorDefeatPolicy
    {
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
