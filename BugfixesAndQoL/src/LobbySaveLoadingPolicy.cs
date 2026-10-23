// Feature: Add solo host loading without changing Vanilla save selection or player remapping.
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    internal static class LobbySaveLoadingPolicy
    {
        internal static bool IsNormalMultiplayer(bool skirmish, bool coopTrail, bool customCoop)
        {
            return !skirmish && !coopTrail && !customCoop;
        }

        internal static bool IsNormalLobby(ulong steamLobbyId, string coopType, bool coopTrail,
            bool localCoop, bool trailMaker, bool customizedTrail, bool extremeCustomized)
        {
            // Frontend flags and gameMembers can outlive a scene. Require current Steam-lobby evidence.
            return steamLobbyId != 0 && string.Equals(coopType, "0", System.StringComparison.Ordinal) &&
                !coopTrail && !localCoop && !trailMaker && !customizedTrail && !extremeCustomized;
        }

        internal static bool IsSoloReadyRoster(IReadOnlyList<Platform_Multiplayer.MPLobbyMember> roster,
            int memberCount, ulong localSteamId)
        {
            if (localSteamId <= 1000 || roster == null || roster.Count != memberCount) return false;
            int humans = 0;
            bool localReady = false;
            foreach (var member in roster)
            {
                // Vanilla uses IDs below 1000 for AI. The boundary value is not a resolved identity.
                if (member == null || member.dummyToBeKicked || member.id.m_SteamID == 1000) return false;
                if (member.id.m_SteamID > 1000)
                {
                    if (member.SkirmishMember || member.SkirmishHumanMember) return false;
                    humans++;
                    if (member.id.m_SteamID == localSteamId) localReady = member.ready;
                }
                else if (!member.SkirmishMember || member.SkirmishHumanMember) return false;
            }
            return humans == 1 && localReady;
        }

        internal static bool ResolveSoloLoad(bool vanilla, bool enabled, bool skirmish,
            bool coop, bool host, bool ready, bool validRoster, int members, int maximum, int humans)
        {
            return vanilla || (enabled && !skirmish && !coop && host && ready && validRoster &&
                members >= 1 && members <= maximum && humans == 1);
        }

        internal static bool ShouldRestoreVisibility(bool enabled, bool skirmish, bool coop)
        {
            return enabled && skirmish && !coop;
        }
    }
}
