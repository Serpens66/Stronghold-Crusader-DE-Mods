using System.Collections.Generic;

namespace BugfixesAndQoL
{
    internal readonly struct MultiplayerTimeControlMember
    {
        internal MultiplayerTimeControlMember(
            ulong steamId, int playerId, bool isSelf, bool isHost,
            bool skirmishAi, bool kicked, bool pendingKick)
        {
            SteamId = steamId;
            PlayerId = playerId;
            IsSelf = isSelf;
            IsHost = isHost;
            SkirmishAi = skirmishAi;
            Kicked = kicked;
            PendingKick = pendingKick;
        }

        internal ulong SteamId { get; }
        internal int PlayerId { get; }
        internal bool IsSelf { get; }
        internal bool IsHost { get; }
        internal bool SkirmishAi { get; }
        internal bool Kicked { get; }
        internal bool PendingKick { get; }

        internal bool IsActiveHuman =>
            SteamId > 1000 && PlayerId > 0 && PlayerId <= 8 &&
            !SkirmishAi && !Kicked && !PendingKick;
    }

    internal static class MultiplayerTimeControlRosterPolicy
    {
        // The unique active host's current slot. It can differ from slot 1 after a host
        // migration or a multiplayer save remap.
        internal static bool TryGetActiveHostPlayerId(
            IEnumerable<MultiplayerTimeControlMember> members,
            out int hostPlayerId)
        {
            hostPlayerId = 0;
            if (members == null)
                return false;

            int activeHosts = 0;
            foreach (MultiplayerTimeControlMember member in members)
            {
                if (member.IsActiveHuman && member.IsHost)
                {
                    activeHosts++;
                    hostPlayerId = member.PlayerId;
                }
            }

            if (activeHosts == 1)
                return true;
            hostPlayerId = 0;
            return false;
        }

        internal static bool TryGetLocalSendAuthorization(
            IEnumerable<MultiplayerTimeControlMember> members,
            out bool isLocalHost,
            out bool hostChanged)
        {
            isLocalHost = false;
            hostChanged = false;
            if (members == null)
                return false;

            int localCount = 0;
            int activeHostCount = 0;
            ulong activeHostSteamId = 0;
            int activeHostPlayerId = 0;
            bool departedHost = false;
            foreach (MultiplayerTimeControlMember member in members)
            {
                if (member.IsHost && member.Kicked && member.SteamId > 1000 &&
                    member.PlayerId > 0 && member.PlayerId <= 8 && !member.SkirmishAi)
                    departedHost = true;

                if (!member.IsActiveHuman)
                    continue;

                if (member.IsSelf)
                {
                    localCount++;
                    isLocalHost = member.IsHost;
                }

                if (member.IsHost)
                {
                    activeHostCount++;
                    activeHostSteamId = member.SteamId;
                    activeHostPlayerId = member.PlayerId;
                }
            }

            if (localCount != 1 || activeHostCount != 1)
                return false;

            if (departedHost)
            {
                foreach (MultiplayerTimeControlMember member in members)
                {
                    if (member.IsHost && member.Kicked &&
                        member.SteamId > 1000 && member.PlayerId > 0 &&
                        member.PlayerId <= 8 && !member.SkirmishAi &&
                        member.SteamId != activeHostSteamId &&
                        member.PlayerId != activeHostPlayerId)
                    {
                        hostChanged = true;
                        break;
                    }
                }
            }

            return true;
        }

        internal static bool IsActiveHumanSender(
            IEnumerable<MultiplayerTimeControlMember> members, ulong senderSteamId)
        {
            if (members == null || senderSteamId <= 1000)
                return false;

            int matches = 0;
            foreach (MultiplayerTimeControlMember member in members)
            {
                if (member.SteamId != senderSteamId)
                    continue;
                if (!member.IsActiveHuman)
                    return false;
                matches++;
            }
            return matches == 1;
        }
    }
}
