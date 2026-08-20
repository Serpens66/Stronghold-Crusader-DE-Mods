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

            // Network lobbies keep one seat open until a second human has joined.
            if (!singleplayerSkirmish && humanCount == 1)
                capacity--;

            return Math.Max(0, capacity - humanCount);
        }
    }
}
