using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Shared
{
    public static unsafe class ActivePlayerHelper
    {
        /// <summary>
        /// Returns the sorted, one-based IDs from the synchronized in-game member roster,
        /// excluding kicked and defeated players.
        /// </summary>
        /// <remarks>
        /// No simulation-derived fallback is used. During multiplayer startup, entity arrays,
        /// Lord IDs, and lastGameState can become ready at different times on different peers.
        /// Returning an empty list is safer than constructing a divergent participant roster.
        /// </remarks>
        public static int[] GetActivePlayerIds()
        {
            Platform_Multiplayer.MPGameMember[] gameMembers =
                Platform_Multiplayer.Instance?.gameMembers?.ToArray();
            if (gameMembers == null || gameMembers.Length == 0)
                return Array.Empty<int>();

            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            HashSet<int> activePlayerIds = new HashSet<int>();
            foreach (Platform_Multiplayer.MPGameMember member in gameMembers)
            {
                if (member == null || member.kicked ||
                    !playerApi.IsPlayerIdValid(member.playerID) ||
                    HasPlayerLost(member.playerID, playerApi))
                {
                    continue;
                }

                activePlayerIds.Add(member.playerID);
            }

            int[] results = new int[activePlayerIds.Count];
            activePlayerIds.CopyTo(results);
            Array.Sort(results);
            return results;
        }

        private static bool HasPlayerLost(int playerId, GamePlayerManagerAPI playerApi)
        {
            return !playerApi.TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources) ||
                   resources == null ||
                   resources->r_WinLossState == WinLossState.Loss;
        }
    }
}
