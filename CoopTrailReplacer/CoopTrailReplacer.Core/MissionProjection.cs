using System.Collections.Generic;
using System.Linq;

namespace CoopTrailReplacer.Core
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
            for (int index = 0; index < players.Count; index++)
            {
                keepOrder[index] = players[index].KeepPosition;
                teams[index] = index < 2 ? humanTeam : players[index].Team;
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
