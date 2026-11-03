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
        private static bool toolbarDirty { get => presentationRefresh.ContentPending; set => presentationRefresh.ContentPending = value; }
        private static PlayerSelectionController selection;
        internal static SpectatorStatistics Statistics;
        internal static void PostPresentation(Action action) { presentationCallbacks.Enqueue(action); RequestRender(); }
        internal static bool ToolbarReady => humanPanel != null;
        internal static void InvalidateToolbar()
        {
            toolbarDirty = toolbarStatusDirty = true;
            InvalidatePresentationState();
            RequestToolbarLayout();
        }
        internal static void AcceptPerspective(int player) { ApplyHumanAi(player); InvalidateToolbar(); }

        private static void RegisterPerspectiveSelection()
        {
            selection = new PlayerSelectionController(CancelSelectionActions, PublishSelectedPlayer);
            perspectiveSubscription = PlayerPerspectiveAPI.SubscribeChanges(Guid, "toolbar", change =>
                PostPresentation(() => {
                    if (change.Sequence <= lastPerspectiveSequence) return;
                    lastPerspectiveSequence = change.Sequence;
                    if (change.Reason == PlayerPerspectiveChangeReason.Cleared)
                    { SpectatorPerspectiveRuntime.ObservePerspective(-1); ApplyHumanAi(-1); return; }
                    SpectatorPerspectiveRuntime.ObservePerspective(change.CurrentPlayerId);
                    if (SpectatorPerspectiveRuntime.IsActiveSpectator()) AcceptPerspective(change.CurrentPlayerId);
                }));
        }

        private static bool IsToolbarSession() => IsCurrentSession(CurrentSessionId) &&
            MainViewModel.viewModelLoaded && GameData.Instance?.lastGameState != null &&
            SpectatorFeatureActivation.HasToolbar(IsToolsSession(),
                SpectatorPerspectiveRuntime.IsSpectatorRoster(), Statistics?.Available == true,
                APIShared.GameModes.GameModeHelper.IsMapEditor());

        // Listed from mission start: occupied slots from the lobby registers, minus players APIShared
        // reports as defeated. A spawned Lord is only required for actions (IsToolsPlayer), not listing.
        private static bool IsSelectableView(int player)
        {
            if (player < 1 || player > 8) return false;
            var state = GameData.Instance.lastGameState;
            bool occupied = state.is_human_or_skirmish_player(player) && !SpectatorDefeat.IsPlayerEliminated(player);
            return ToolSessionPolicy.CanSelect(SpectatorPerspectiveRuntime.IsSpectatorRoster(), IsToolsSession(),
                occupied, state.is_skirmish_player(player),
                IsActivePlayerTestSession() && player == PlayerPerspectiveAPI.GetControlledPlayerId());
        }

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
            InvalidateToolbar();
            if (IsPlacementPlayer(owner) && humanPlacementMode && MainViewModel.viewModelLoaded && MainViewModel.Instance.HUDmain != null)
                RefreshBuildIcons(MainViewModel.Instance.HUDmain, owner, "selection");
            log.LogInfo("SPECTATOR_SELECTION player=" + owner);
        }
    }
}
