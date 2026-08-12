// Temporarily blocks local state-changing features that are not deterministic in multiplayer.
using BepInEx.Logging;
using System;

namespace ExtraFeatures
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
                    return Shared.GameModeHelper.IsRealMultiplayer();
                }
                catch (Exception ex)
                {
                    if (!detectionFailureLogged)
                    {
                        detectionFailureLogged = true;
                        Shared.DebugLogHelper.LogError(
                            log,
                            $"Extra Features could not determine the game mode; desync-prone local state changes remain blocked as a precaution: {ex}");
                    }

                    return true;
                }
            }
        }

        public void CaptureMapMode(bool multiplayerSave)
        {
            try
            {
                Shared.GameModeSnapshot snapshot = Shared.GameModeHelper.Capture(multiplayerSave);
                blocksLocalStateChanges = snapshot.IsRealMultiplayer;
                hasMapSnapshot = true;
                detectionFailureLogged = false;
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Extra Features multiplayer feature gate captured map mode: {snapshot.ToDiagnosticString()}.");
            }
            catch (Exception ex)
            {
                blocksLocalStateChanges = true;
                hasMapSnapshot = true;
                detectionFailureLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Extra Features could not capture the map mode; desync-prone local state changes remain blocked as a precaution: {ex}");
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
