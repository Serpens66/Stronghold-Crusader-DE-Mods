namespace Spectator
{
    internal static class SpectatorDefeatPolicy
    {
        internal static bool IsSpectatorObserver(bool originalSpectator, bool confirmedEliminatedParticipant) =>
            originalSpectator || confirmedEliminatedParticipant;
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
            bool eliminationConfirmed,
            int localPlayerId) =>
            featureEnabled &&
            activeMatch &&
            !mapEditor &&
            !alreadySpectator &&
            supportedGameMode &&
            validLocalParticipant &&
            eliminationConfirmed &&
            localPlayerId >= 1 && localPlayerId <= 8;

        internal static int ResolvePresentedGameOverState(
            int state,
            bool spectatorPromotionRequested) =>
            state == 1 && spectatorPromotionRequested ? 2 : state;

        internal static bool IsLocalElimination(long activeSession, long observedSession, int observedPlayer,
            int localPlayer, bool readable, bool eliminationConfirmed) =>
            activeSession > 0 && activeSession == observedSession && observedPlayer >= 1 && observedPlayer <= 8 &&
            observedPlayer == localPlayer && readable && eliminationConfirmed;

        // Roster exclusion: only a readable defeat of the current mission session counts.
        internal static bool IsRosterEliminated(long activeSession, long observedSession, bool readable, bool eliminated) =>
            activeSession > 0 && activeSession == observedSession && readable && eliminated;

    }
}
