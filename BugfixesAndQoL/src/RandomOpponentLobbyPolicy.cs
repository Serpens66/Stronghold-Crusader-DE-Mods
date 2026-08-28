using System;

namespace BugfixesAndQoL
{
    internal static class RandomOpponentLobbyPolicy
    {
        internal static bool IsRemovableAi(bool isSkirmishMember, bool isHuman) =>
            isSkirmishMember && !isHuman;

        internal static int GetMaximumAiCount(
            int playerCap,
            int lobbyMaxPlayers,
            int selectedMapMaxPlayers,
            bool customCoopGame,
            bool singleplayerSkirmish,
            int humanCount)
        {
            if (humanCount < 0)
                return 0;

            int capacity;
            if (customCoopGame)
            {
                capacity = selectedMapMaxPlayers;
            }
            else if (playerCap > 0 && lobbyMaxPlayers > 0)
            {
                capacity = Math.Min(playerCap, lobbyMaxPlayers);
            }
            else
            {
                capacity = 0;
            }

            // Custom co-op missions still require their human partner slot.
            if (customCoopGame && !singleplayerSkirmish && humanCount == 1)
                capacity--;

            return Math.Max(0, capacity - humanCount);
        }

        internal static bool ShouldReleaseFinalAiSeat(
            bool modEnabled,
            bool isHost,
            bool singleplayerSkirmish,
            bool coopGame,
            bool customCoopGame,
            int playerCap,
            int lobbyMaxPlayers,
            int memberCount,
            int humanCount,
            int aiCount)
        {
            if (!modEnabled || !isHost || singleplayerSkirmish || coopGame || customCoopGame)
                return false;
            if (playerCap <= 0 || lobbyMaxPlayers <= 0 || memberCount < 1)
                return false;

            int capacity = Math.Min(playerCap, lobbyMaxPlayers);
            return memberCount == capacity - 1 &&
                   humanCount == 1 &&
                   aiCount == memberCount - 1;
        }
    }
}
