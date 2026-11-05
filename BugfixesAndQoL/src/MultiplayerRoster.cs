// In-game member list access. GameNetworkAPI.IsLocalHost reads Platform_Multiplayer.IsHost, which
// Vanilla's promoteNewHost never updates. The member flags used here follow a host migration.
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    internal static class MultiplayerRoster
    {
        internal static bool IsLocalHost() =>
            Platform_Multiplayer.Instance?.IsGameMemberHost() == true;

        internal static List<MultiplayerTimeControlMember> Snapshot()
        {
            var members = Platform_Multiplayer.Instance?.gameMembers;
            if (members == null)
                return null;

            var snapshot = new List<MultiplayerTimeControlMember>(members.Count);
            foreach (Platform_Multiplayer.MPGameMember member in members)
            {
                if (member == null)
                    continue;
                snapshot.Add(new MultiplayerTimeControlMember(
                    member.steamID, member.playerID, member.isSelf, member.isHost,
                    member.skirmishAI, member.kicked, member.pendingKick));
            }
            return snapshot;
        }
    }
}
