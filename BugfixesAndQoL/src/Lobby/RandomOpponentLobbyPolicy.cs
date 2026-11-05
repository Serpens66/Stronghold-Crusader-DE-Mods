using System;

namespace BugfixesAndQoL
{
    internal static class RandomOpponentLobbyPolicy
    {
        internal const int MaximumPlayerSlots = 8;
        internal const int MaximumRandomOpponents = MaximumPlayerSlots - 1;

        internal static bool IsRemovableAi(bool isSkirmishMember, bool isHuman) =>
            isSkirmishMember && !isHuman;

        internal static bool TryGetRandomOpponentCount(string value, out int count)
        {
            count = 0;
            if (!int.TryParse(value, out int parsed) ||
                parsed >= 0 || parsed < -MaximumRandomOpponents)
                return false;

            count = -parsed;
            return true;
        }

        internal static bool TryGetMaximumAiCount(
            int playerCap,
            int lobbyMaxPlayers,
            int selectedMapMaxPlayers,
            bool customCoopGame,
            bool singleplayerSkirmish,
            bool allowFullAiMultiplayerLobby,
            int humanCount,
            out int maximumAiCount)
        {
            maximumAiCount = 0;
            if (humanCount < 1 || humanCount > MaximumPlayerSlots)
                return false;

            if (playerCap < 1 || playerCap > MaximumPlayerSlots ||
                lobbyMaxPlayers < 1 || lobbyMaxPlayers > MaximumPlayerSlots ||
                selectedMapMaxPlayers < 1 || selectedMapMaxPlayers > MaximumPlayerSlots)
                return false;

            int capacity = Math.Min(
                selectedMapMaxPlayers,
                Math.Min(playerCap, lobbyMaxPlayers));

            if (humanCount > capacity)
                return false;

            // Custom co-op always needs its partner; normal multiplayer does so when the feature is off.
            if (!singleplayerSkirmish && humanCount == 1 &&
                (customCoopGame || !allowFullAiMultiplayerLobby))
                capacity--;

            maximumAiCount = Math.Max(0, capacity - humanCount);
            return true;
        }

        internal static bool ShouldReleaseFinalAiSeat(
            bool modEnabled,
            bool isHost,
            bool singleplayerSkirmish,
            bool coopGame,
            bool customCoopGame,
            int playerCap,
            int lobbyMaxPlayers,
            int selectedMapMaxPlayers,
            int memberCount,
            int humanCount,
            int aiCount)
        {
            if (!modEnabled || !isHost || singleplayerSkirmish || coopGame || customCoopGame)
                return false;
            if (playerCap < 1 || playerCap > MaximumPlayerSlots ||
                lobbyMaxPlayers < 1 || lobbyMaxPlayers > MaximumPlayerSlots ||
                selectedMapMaxPlayers < 1 || selectedMapMaxPlayers > MaximumPlayerSlots ||
                memberCount < 1 || memberCount > MaximumPlayerSlots ||
                humanCount < 1 || humanCount > MaximumPlayerSlots ||
                aiCount < 0 || aiCount > MaximumRandomOpponents)
                return false;

            int capacity = Math.Min(
                selectedMapMaxPlayers,
                Math.Min(playerCap, lobbyMaxPlayers));
            if (humanCount > capacity || memberCount > capacity)
                return false;

            return memberCount == capacity - 1 &&
                   humanCount == 1 &&
                   aiCount == memberCount - 1;
        }
    }

    internal static class TemporaryBooleanStateGuard
    {
        internal static void Execute(
            Func<bool> readState,
            Action<bool> writeState,
            bool temporaryState,
            Action action,
            Action afterRestore,
            Action<Exception> reportCleanupFailure)
        {
            if (readState == null)
                throw new ArgumentNullException(nameof(readState));
            if (writeState == null)
                throw new ArgumentNullException(nameof(writeState));
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            bool originalState = readState();
            try
            {
                writeState(temporaryState);
                action();
            }
            finally
            {
                TryCleanup(() => writeState(originalState), reportCleanupFailure);
                TryCleanup(afterRestore, reportCleanupFailure);
            }
        }

        private static void TryCleanup(Action cleanup, Action<Exception> reportCleanupFailure)
        {
            if (cleanup == null)
                return;

            try
            {
                cleanup();
            }
            catch (Exception ex)
            {
                try
                {
                    reportCleanupFailure?.Invoke(ex);
                }
                catch
                {
                    // Cleanup diagnostics must never replace the original action exception.
                }
            }
        }
    }
}
