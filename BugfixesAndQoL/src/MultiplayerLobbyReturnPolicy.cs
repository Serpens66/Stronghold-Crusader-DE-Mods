// Feature: Pure eligibility and timeout rules for the post-game lobby handoff.
using System;

namespace BugfixesAndQoL
{
    internal static class MultiplayerLobbyReturnPolicy
    {
        internal const int ExitWaitTimeoutSeconds = 15;

        internal static bool IsSupportedSession(
            bool modEnabled,
            bool settingEnabled,
            bool realMultiplayer,
            int coopTrailId) =>
            modEnabled && settingEnabled && realMultiplayer && coopTrailId <= 0;

        internal static bool ShouldCreateLobby(
            bool supportedSession,
            int gameOverState,
            bool localPlayerIsHost,
            bool alreadyRequested) =>
            supportedSession && gameOverState > 0 && localPlayerIsHost && !alreadyRequested;

        internal static bool ShouldAnnounceToMember(
            bool isSelf,
            bool kicked,
            bool skirmishAi,
            bool stillConnected,
            ulong steamId) =>
            !isSelf && !kicked && !skirmishAi && stillConnected && steamId > 1000UL;

        internal static bool HasTimedOut(long startedAt, long now, long frequency)
        {
            if (startedAt <= 0 || now < startedAt || frequency <= 0)
                return false;

            long timeoutTicks = checked(frequency * ExitWaitTimeoutSeconds);
            return now - startedAt >= timeoutTicks;
        }
    }
}
