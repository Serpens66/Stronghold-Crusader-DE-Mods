using System;
using APIShared;
using CrusaderDE;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private static bool spectatorSelectionLinked;
        private static IDisposable perspectiveSubscription;
        private static long lastPerspectiveSequence;
        private static bool toolbarDirty = true;
        private static PlayerSelectionController selection;
        internal static SpectatorStatistics Statistics;
        internal static void PostPresentation(Action action) => presentationCallbacks.Enqueue(action);
        internal static bool ToolbarReady => humanPanel != null;
        internal static void InvalidateToolbar() => toolbarDirty = true;
        internal static void AcceptPerspective(int player) { ApplyHumanAi(player); toolbarDirty = true; }

        private static void RegisterPerspectiveSelection()
        {
            selection = new PlayerSelectionController(CancelSelectionActions, PublishSelectedPlayer);
            perspectiveSubscription = PlayerPerspectiveAPI.SubscribeChanges(Guid, "toolbar", change =>
                presentationCallbacks.Enqueue(() => {
                    if (change.Sequence <= lastPerspectiveSequence) return;
                    lastPerspectiveSequence = change.Sequence;
                    if (change.Reason == PlayerPerspectiveChangeReason.Cleared) { ApplyHumanAi(-1); return; }
                    SpectatorPerspectiveRuntime.ObservePerspective(change.CurrentPlayerId);
                    if (SpectatorPerspectiveRuntime.IsActiveSpectator()) AcceptPerspective(change.CurrentPlayerId);
                }));
        }

        private static bool IsToolbarSession() => settings.EnableClientFeatures && IsCurrentSession(CurrentSessionId) &&
            MainViewModel.viewModelLoaded && GameData.Instance?.lastGameState != null &&
            (IsToolsSession() || SpectatorPerspectiveRuntime.IsActiveSpectator() || Statistics?.Available == true);

        private static bool IsSelectableView(int player) => player >= 1 && player <= 8 &&
            (SpectatorPerspectiveRuntime.IsActiveSpectator()
                ? GameData.Instance.lastGameState.is_human_or_skirmish_player(player)
                : IsSelectableAi(player));

        private static void SyncSpectatorSelection()
        {
            spectatorSelectionLinked = SpectatorPerspectiveRuntime.IsActiveSpectator();
            if (!spectatorSelectionLinked) return;
            int player = PlayerPerspectiveAPI.GetViewedPlayerId();
            ApplyHumanAi(IsSelectableView(player) ? player : -1);
        }

        private static void ApplyHumanAi(int owner) => selection?.Select(owner);
        private static void CancelSelectionActions()
        {
            CancelPowerSelection();
            ClearGateSelection();
            MainControls.instance?.StopAllPlacement();
            activeWall = null;
            activeSpecial = null;
        }
        private static void PublishSelectedPlayer(int owner)
        {
            humanSelectedOwner = owner;
            toolbarDirty = true;
            if (IsSelectableAi(owner) && humanPlacementMode && MainViewModel.viewModelLoaded && MainViewModel.Instance.HUDmain != null)
                RefreshBuildIcons(MainViewModel.Instance.HUDmain, owner, "selection");
            log.LogInfo("SPECTATOR_SELECTION player=" + owner);
        }
    }
}
