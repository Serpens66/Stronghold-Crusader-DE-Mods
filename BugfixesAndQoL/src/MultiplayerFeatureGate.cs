using APIShared.GameModes;
// Captures whether synchronized state changes are required for the active map.
using BepInEx.Logging;
using System;

namespace BugfixesAndQoL
{
    internal sealed class MultiplayerFeatureGate
    {
        private readonly ManualLogSource log;
        private bool hasMapSnapshot;
        private bool blocksLocalStateChanges;
        private bool detectionFailureLogged;

        public MultiplayerFeatureGate(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public bool BlocksLocalStateChanges
        {
            get
            {
                if (hasMapSnapshot)
                    return blocksLocalStateChanges;

                try
                {
                    return APIShared.GameModes.GameModeHelper.IsRealMultiplayer();
                }
                catch (Exception ex)
                {
                    if (!detectionFailureLogged)
                    {
                        detectionFailureLogged = true;
                        Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/MultiplayerFeatureGate.cs:36", $"Bugfixes and QoL could not determine the game mode; synchronized state changes remain required as a precaution: {ex}");
                    }

                    return true;
                }
            }
        }

        public void CaptureMapMode(bool multiplayerSave)
        {
            try
            {
                APIShared.GameModes.GameModeSnapshot snapshot = APIShared.GameModes.GameModeHelper.Capture(multiplayerSave);
                blocksLocalStateChanges = snapshot.IsRealMultiplayer;
                hasMapSnapshot = true;
                detectionFailureLogged = false;
                Shared.DebugLogHelper.LogDebugLimited(log, "BugfixesAndQoL/src/MultiplayerFeatureGate.cs:54", $"Bugfixes and QoL multiplayer feature gate captured map mode: {snapshot.ToDiagnosticString()}.");
            }
            catch (Exception ex)
            {
                blocksLocalStateChanges = true;
                hasMapSnapshot = true;
                detectionFailureLogged = true;
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/src/MultiplayerFeatureGate.cs:63", $"Bugfixes and QoL could not capture the map mode; synchronized state changes remain required as a precaution: {ex}");
            }
        }

        public void Reset()
        {
            hasMapSnapshot = false;
            blocksLocalStateChanges = false;
            detectionFailureLogged = false;
        }
    }
}
