using System.Collections.Generic;
using System.Linq;

namespace ExtendedData.Core
{
    public sealed class MissionProjection
    {
        public IReadOnlyList<PlayerDefinition> ActivePlayers { get; private set; }
        public int[] KeepOrder { get; private set; }
        public int[] Teams { get; private set; }
        public int[] AiBaseLordIds { get; private set; }

        public static MissionProjection Create(CoopMissionDefinition definition)
        {
            List<PlayerDefinition> players = definition.Players.Where(player => player != null && player.Active).ToList();
            int[] keepOrder = Enumerable.Repeat(-1, 8).ToArray();
            int[] teams = new int[8];
            int humanTeam = players[0].Team;
            var normalizedTeams = new Dictionary<int, int>();
            normalizedTeams[humanTeam] = 1;
            int nextTeam = 2;
            for (int index = 0; index < players.Count; index++)
            {
                keepOrder[index] = players[index].KeepPosition;
                if (index < 2 || players[index].Team == humanTeam)
                {
                    teams[index] = 1;
                    continue;
                }
                if (!normalizedTeams.TryGetValue(players[index].Team, out int normalizedTeam))
                {
                    normalizedTeam = nextTeam++;
                    normalizedTeams[players[index].Team] = normalizedTeam;
                }
                teams[index] = normalizedTeam;
            }

            return new MissionProjection
            {
                ActivePlayers = players,
                KeepOrder = keepOrder,
                Teams = teams,
                AiBaseLordIds = players.Skip(2).Select(GetBaseLordId).ToArray(),
            };
        }

        public static int GetBaseLordId(PlayerDefinition player) =>
            player.Lord.Id ?? player.Lord.BaseLordId;

        // Vanilla numbers a custom Lord by the lobby slot it occupies (MPLobbyMember.SetValidCustomLordType:
        // first custom type + zero-based player index); portrait, name and Lord media are looked up through
        // that slot. MPAIVInfo.lordType keeps the slot in which the Lord was added and is not updated when it
        // is moved, so a stored custom type is never trusted: it is derived from the mission slot. Built-in
        // types are Lord identities and stay unchanged.
        public static int RuntimeLordType(int storedLordType, int playerIndex, int firstCustomLordType)
        {
            if (storedLordType < firstCustomLordType)
                return storedLordType;
            if (playerIndex < 0 || playerIndex > 7)
                throw new System.ArgumentOutOfRangeException(nameof(playerIndex), playerIndex,
                    "A custom Lord needs a mission player index from 0 to 7.");
            return firstCustomLordType + playerIndex;
        }
    }
}
