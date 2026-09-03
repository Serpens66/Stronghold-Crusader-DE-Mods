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
                Shared.GameModeSnapshot snapshot = Shared.GameplayModActivationGate.Snapshot;
                return snapshot.Kind == Shared.GameModeKind.Unknown || snapshot.IsRealMultiplayer;
            }
        }

        public void CaptureMapMode(bool multiplayerSave)
        {
            try
            {
                Shared.GameModeSnapshot snapshot = Shared.GameplayModActivationGate.Snapshot;
                blocksLocalStateChanges = snapshot.Kind == Shared.GameModeKind.Unknown || snapshot.IsRealMultiplayer;
                hasMapSnapshot = snapshot.Kind != Shared.GameModeKind.Unknown;
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Extra Features multiplayer feature gate captured map mode: {snapshot.ToDiagnosticString()}.");
            }
            catch (Exception ex)
            {
                blocksLocalStateChanges = true;
                hasMapSnapshot = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Extra Features could not capture the map mode; desync-prone local state changes remain blocked as a precaution: {ex}");
            }
        }

        public void Reset()
        {
            hasMapSnapshot = false;
            blocksLocalStateChanges = false;
        }
    }
}
