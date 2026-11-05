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
            bool terminalSignalObserved,
            bool localPlayerIsHost,
            bool alreadyRequested) =>
            supportedSession && terminalSignalObserved && localPlayerIsHost && !alreadyRequested;

        internal static bool HasTimedOut(long startedAt, long now, long frequency)
        {
            if (startedAt <= 0 || now < startedAt || frequency <= 0)
                return false;

            long timeoutTicks = checked(frequency * ExitWaitTimeoutSeconds);
            return now - startedAt >= timeoutTicks;
        }
    }
}
