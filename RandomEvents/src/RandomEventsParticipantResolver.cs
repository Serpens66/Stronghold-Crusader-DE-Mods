using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace RandomEvents
{
    internal static class RandomEventsParticipantResolver
    {
        public static int[] GetLivingEventParticipantIds(bool includeAIPlayers)
        {
            var result = new SortedSet<int>();
            GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
            foreach (int playerId in Shared.ActivePlayerHelper.GetActivePlayerIds())
            {
                if (!players.IsPlayerIdValid(playerId) ||
                    (!includeAIPlayers && players.IsAIPlayer(playerId)) ||
                    !TryGetLivingLord(playerId, out _))
                {
                    continue;
                }

                result.Add(playerId);
            }

            int[] playerIds = new int[result.Count];
            result.CopyTo(playerIds);
            return playerIds;
        }

        public static unsafe bool TryGetLivingLord(int playerId, out string failure)
        {
            failure = string.Empty;
            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(
                    playerId,
                    out GamePlayerResources* resources) ||
                resources == null)
            {
                failure = "player resources unavailable";
                return false;
            }

            uint lordUnitId = resources->r_LordUnitId;
            if (lordUnitId == 0 || lordUnitId > int.MaxValue)
            {
                failure = "no valid Lord unit is registered";
                return false;
            }
            if (!GameUnitManagerAPI.Instance.TryGetUnitById((int)lordUnitId, out GameUnit* lord) || lord == null)
            {
                failure = "registered Lord unit cannot be resolved";
                return false;
            }
            if (lord->r_AliveState != AliveState.IsAlive)
            {
                failure = $"registered Lord unit state is {lord->r_AliveState}";
                return false;
            }
            if (lord->r_UnitChimp != eChimps.CHIMP_TYPE_LORD || lord->r_ControllableForPlayerId != playerId)
            {
                failure = "registered unit is not the target player's Lord";
                return false;
            }

            return true;
        }
    }
}
