using System;
using System.Collections.Generic;
using System.Globalization;

namespace MultiplayerRestartTest
{
    // Locale selection and file loading stay with the shared game-language helper.
    // These embedded defaults keep the testmod readable if an asset is missing.
    internal static class RestartText
    {
        // The Vanilla confirmation heading is a single line. Keep both questions
        // compact even if an externally edited translation becomes too verbose.
        internal const int MaximumTitleElements = 14;
        private static readonly Dictionary<string, string> english = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Restart.Label"] = "Restart mission",
            ["Restart.Help"] = "Restarts this mission for everyone with the same players and settings. Experimental test mod.",
            ["Restart.NoMission"] = "No supported new multiplayer game has been captured.",
            ["Restart.SavedGame"] = "Saved games are not supported by this test mod.",
            ["Restart.CaptureFailed"] = "The original start data could not be captured. See the log for details.",
            ["Restart.WaitPeers"] = "Waiting for every player to confirm the same test mod and start data.",
            ["Restart.InitializationFailed"] = "The test mod could not initialize.",
            ["Restart.InProgress"] = "A restart is already in progress.",
            ["Restart.CheckpointMissing"] = "The simulation checkpoint is unavailable. Fully restart the game.",
            ["Restart.HostOnly"] = "Only the host can restart the mission.",
            ["Restart.PeersMismatch"] = "Not every player has confirmed matching restart data.",
            ["Restart.NotRunning"] = "The multiplayer simulation has not started.",
            ["Restart.RosterChanged"] = "The player roster has changed.",
            ["Restart.TransitionActive"] = "A mission end or another lobby transition is in progress.",
            ["Restart.SaveBusy"] = "Saving or resynchronizing is in progress.",
            ["Restart.SettingsChanged"] = "Host settings have changed since the mission started.",
            ["Restart.Failed"] = "Restart failed. See the log for details.",
            ["Restart.FailedGuard"] = "Restart failed. Return to the main menu before starting another game.",
            ["Restart.TransitionPending"] = "Restart: lobby transition in progress.",
            ["Restart.WaitStart"] = "Restart: waiting for all start confirmations.",
            ["Restart.LobbyInvalid"] = "The restored lobby does not match the original start data.",
            ["Restart.ConfirmTitle"] = "Restart?",
            ["Restart.RecoveryTitle"] = "Main menu?"
        };

        internal static IEnumerable<string> Keys => english.Keys;
        internal static string Get(string key) => Resolve(key, SerpLocalization.Get);

        // The lookup is the external catalog boundary; tests use the actual shared
        // file reader without asking Steam/Unity to initialize in a desktop fixture.
        internal static string Resolve(string key, Func<string, string> lookup)
        {
            if (!english.TryGetValue(key, out string fallback)) throw new ArgumentOutOfRangeException(nameof(key));
            string value = lookup(key);
            if (string.IsNullOrWhiteSpace(value) || string.Equals(value, key, StringComparison.Ordinal)) return fallback;
            if ((key == "Restart.ConfirmTitle" || key == "Restart.RecoveryTitle") &&
                (value.IndexOfAny(new[] { '\r', '\n' }) >= 0 || new StringInfo(value).LengthInTextElements > MaximumTitleElements))
                return fallback;
            return value;
        }
    }
}
