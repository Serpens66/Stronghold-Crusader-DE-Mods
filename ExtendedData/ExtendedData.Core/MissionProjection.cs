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
    }
}
