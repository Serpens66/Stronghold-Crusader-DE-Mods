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

        internal static int GetMaximumAiCount(
            int playerCap,
            int lobbyMaxPlayers,
            int selectedMapMaxPlayers,
            bool customCoopGame,
            bool singleplayerSkirmish,
            bool allowFullAiMultiplayerLobby,
            int humanCount)
        {
            if (humanCount < 0)
                return 0;

            int capacity;
            if (customCoopGame)
            {
                if (selectedMapMaxPlayers < 1 || selectedMapMaxPlayers > MaximumPlayerSlots)
                    return 0;
                capacity = selectedMapMaxPlayers;
            }
            else if (playerCap >= 1 && playerCap <= MaximumPlayerSlots &&
                     lobbyMaxPlayers >= 1 && lobbyMaxPlayers <= MaximumPlayerSlots)
            {
                capacity = Math.Min(playerCap, lobbyMaxPlayers);
            }
            else
            {
                capacity = 0;
            }

            // Custom co-op always needs its partner; normal multiplayer does so when the feature is off.
            if (!singleplayerSkirmish && humanCount == 1 &&
                (customCoopGame || !allowFullAiMultiplayerLobby))
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
            if (playerCap < 1 || playerCap > MaximumPlayerSlots ||
                lobbyMaxPlayers < 1 || lobbyMaxPlayers > MaximumPlayerSlots ||
                memberCount < 1 || humanCount < 0 || aiCount < 0)
                return false;

            int capacity = Math.Min(playerCap, lobbyMaxPlayers);
            return memberCount == capacity - 1 &&
                   humanCount == 1 &&
                   aiCount == memberCount - 1;
        }
    }
}
