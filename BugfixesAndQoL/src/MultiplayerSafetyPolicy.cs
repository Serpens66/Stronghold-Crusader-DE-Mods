// Pure policy for multiplayer speed limits and connection-recovery eligibility.
using System;

namespace BugfixesAndQoL
{
    internal static class MultiplayerSafetyPolicy
    {
        internal const int MinimumConfiguredMultiplayerSpeed = 40;
        internal const int MaximumConfiguredMultiplayerSpeed = 200;
        internal const int DefaultConfiguredMultiplayerSpeed = 90;
        internal const string CompatibilityToken = "connection-recovery-v1";
        internal const int RecoveryTimeoutSeconds = 30;
        internal const string RecoverySaveFileName = "Connection Recovery.msv";

        internal static bool IsConnectedHuman(ulong steamId, bool skirmishAi, bool kicked) =>
            steamId > 1000 && !skirmishAi && !kicked;

        internal static int ResolveMaximumSpeed(
            int scriptExtenderMaximum,
            int multiplayerMaximum,
            bool realMultiplayer,
            int connectedHumanCount)
        {
            int normalizedMaximum = Math.Max(
                MultiplayerGameSpeedPolicy.MinimumSpeed,
                scriptExtenderMaximum);
            return realMultiplayer && connectedHumanCount >= 2
                ? Math.Min(NormalizeConfiguredMultiplayerSpeed(multiplayerMaximum), normalizedMaximum)
                : normalizedMaximum;
        }

        internal static int NormalizeConfiguredMultiplayerSpeed(int value)
        {
            int clamped = Math.Max(
                MinimumConfiguredMultiplayerSpeed,
                Math.Min(MaximumConfiguredMultiplayerSpeed, value));
            int steps = (int)Math.Round(
                (double)clamped / MultiplayerGameSpeedPolicy.SpeedStep,
                MidpointRounding.AwayFromZero);
            return steps * MultiplayerGameSpeedPolicy.SpeedStep;
        }

        // True only while the host executes a kick the user confirmed. Such a kick never waits
        // for a recovery save. Every kick path runs on the Unity main thread.
        internal static bool ExplicitHostKickActive { get; private set; }

        internal static void RunExplicitHostKick(Action kick)
        {
            ExplicitHostKickActive = true;
            try { kick(); }
            finally { ExplicitHostKickActive = false; }
        }

        internal static bool ShouldDelayNativeLagKick(
            bool mainModEnabled,
            bool recoverySaveEnabled,
            bool realMultiplayer,
            bool multiplayerSimulationRunning,
            bool participantsCompatible,
            bool forceKickFromHost,
            ulong targetSteamId,
            bool targetSkirmishAi,
            bool targetKicked,
            int connectedHumanCount) =>
            mainModEnabled &&
            recoverySaveEnabled &&
            realMultiplayer &&
            multiplayerSimulationRunning &&
            participantsCompatible &&
            forceKickFromHost &&
            IsConnectedHuman(targetSteamId, targetSkirmishAi, targetKicked) &&
            connectedHumanCount >= 2;
    }
}
