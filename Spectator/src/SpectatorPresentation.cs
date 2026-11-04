using System;
using System.ComponentModel;
using APIShared;
using APIShared.GameModes;
using APIShared.Presentation;
using CrusaderDE;
using Noesis;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private static IUiFrameWorkRegistration renderWork;
        private static readonly TimeSpan presentationStatusInterval = TimeSpan.FromMilliseconds(100);
        private static readonly PresentationRefreshState presentationRefresh = new PresentationRefreshState();
        private static bool rosterPending { get => presentationRefresh.RosterPending; set => presentationRefresh.RosterPending = value; }
        private static bool presentationStatePending { get => presentationRefresh.StatePending; set => presentationRefresh.StatePending = value; }
        private static bool toolbarStatusDirty { get => presentationRefresh.StatusPending; set => presentationRefresh.StatusPending = value; }
        private static MainViewModel presentationView;
        private static readonly int[] presentationColours = new int[9];
        private static bool presentationColoursKnown;
        private static bool presentedToolsEligible, presentedToolbarEligible;

        private static void RequestRender() => renderWork?.Request();

        private static void EnableRender()
        {
            if (!ApiShared.ForMod(Guid).Ui.TryRegisterBeforeRender("presentation", OnBeforeRender,
                out renderWork, out string reason, order: 100))
                throw new InvalidOperationException("Spectator UI scheduling unavailable: " + reason);
            settings.SettingsChanged += InvalidatePresentationState;
            RequestRender();
        }

        private static void InvalidatePresentationState()
        {
            presentationStatePending = true;
            RequestRender();
        }

        private static void RequestToolbarLayout()
        {
            toolbarLayoutDirty = true;
            RequestRender();
        }

        private static void RequestToolbarStatus()
        {
            toolbarStatusDirty = true;
            RequestRender();
        }

        // Existing PlayState publication is the source for roster/order updates. Explicit defeat,
        // perspective and UI events also invalidate their consumers without waiting for a new state.
        private static void ObserveRosterChange()
        {
            rosterPending = true;
            // PlayState also changes for unrelated simulation data. Coalesce those publications
            // into the allowed 100 ms status interval; explicit selection/defeat remains immediate.
            renderWork?.RequestAfter(presentationStatusInterval);
        }

        private static bool SessionNeedsPresentation()
        {
            if (settings == null || !IsCurrentSession(CurrentSessionId) || !(sessionMode is GameModeSnapshot mode) ||
                mode.IsMapEditor) return false;
            var activation = settings.Activation;
            if (!mode.IsRealMultiplayer && (activation.TestTools || activation.SingleplayerTest)) return true;
            if (!activation.Perspective && !activation.Statistics && !activation.Defeat) return false;
            var state = GameData.Instance?.lastGameState;
            return state != null && state.spectatorMode != 0 || SpectatorDefeat.NeedsPresentation;
        }

        private static bool SessionAwaitingPresentationReadiness()
        {
            if (settings == null || !IsCurrentSession(CurrentSessionId) || !(sessionMode is GameModeSnapshot mode) ||
                mode.IsMapEditor) return false;
            var activation = settings.Activation;
            bool configured = activation.Perspective || activation.Statistics || activation.Defeat ||
                !mode.IsRealMultiplayer && (activation.TestTools || activation.SingleplayerTest);
            // A successful mission notification can precede the first view/PlayState. Keep a
            // bounded readiness retry; no later simulation tick or property event is assumed.
            return configured && (!MainViewModel.viewModelLoaded || GameData.Instance?.lastGameState == null);
        }

        private static bool TakePresentationStatusCheck()
        {
            return presentationRefresh.TakeStatusCheck(System.Diagnostics.Stopwatch.GetTimestamp(),
                System.Diagnostics.Stopwatch.Frequency);
        }

        private static void ObservePresentationView(MainViewModel view)
        {
            if (ReferenceEquals(view, presentationView)) return;
            if (presentationView != null) presentationView.PropertyChanged -= OnPresentationPropertyChanged;
            presentationView = view;
            if (view != null) view.PropertyChanged += OnPresentationPropertyChanged;
            InvalidateToolbar();
            presentationStatePending = true;
        }

        private static void OnPresentationPropertyChanged(object sender, PropertyChangedEventArgs args)
        {
            switch (args.PropertyName)
            {
                case null: case "": case "UIVisible": case "Show_InGameUI": case "Show_HUD_Extras":
                case "Show_HUD_Briefing": case "Show_HUD_MissionOver": case "Show_HUD_IngameMenu":
                case "Show_HUD_Main": case "Show_HUD_Building": case "Show_HUD_Troops":
                case "AlliesPanelVisible": case "MeritPanelVisible": case "MPChatVisible":
                case "FreezeMainControls": case "SubMode":
                    RequestToolbarLayout();
                    InvalidatePresentationState();
                    break;
            }
        }

        private static void ObservePresentedPerspective()
        {
            bool active = SpectatorPerspectiveRuntime.IsActiveSpectator();
            int viewed = active ? PlayerPerspectiveAPI.GetViewedPlayerId() : -1;
            // Native/external perspective writers need not publish SubscribeChanges. Keep this
            // small active-render guard; full roster validation only runs on a change or explicit dirt.
            if (presentationRefresh.ObservePerspective(active, viewed)) RequestRender();
        }

        private static void ObservePresentationColours()
        {
            int[] colours = SpriteMapping.remapColours;
            bool changed = !presentationColoursKnown;
            for (int player = 1; player < presentationColours.Length; player++)
            {
                int colour = colours != null && player < colours.Length ? colours[player] : 0;
                if (presentationColours[player] != colour) changed = true;
                presentationColours[player] = colour;
            }
            presentationColoursKnown = true;
            if (changed) InvalidateToolbar();
        }

        private static void ResetPresentationScheduling()
        {
            renderWork?.SetContinuous(false);
            renderWork?.CancelPending();
            presentationRefresh.Reset();
            presentationColoursKnown = false;
            presentedToolsEligible = presentedToolbarEligible = false;
            ObservePresentationView(null);
        }

        private static void OnBeforeRender()
        {
            long diagnosticStarted = 0;
            Shared.DevDiag.Start(ref diagnosticStarted);
            try { RenderFrame(); }
            finally
            {
                Shared.DevDiag.StopSpike(ref diagnosticStarted, "spectator.render", 20000);
                TimeSpan retry = presentationRefresh.RetryDelay(System.Diagnostics.Stopwatch.GetTimestamp(),
                    System.Diagnostics.Stopwatch.Frequency);
                if (retry > TimeSpan.Zero)
                {
                    renderWork.SetContinuous(false);
                    renderWork.RequestAfter(retry);
                }
                else
                {
                    bool applicable = MainViewModel.viewModelLoaded && SessionNeedsPresentation();
                    // Geometry can move without SizeChanged; leased controls and active cursor/input
                    // work also retain their original render phase. Hidden idle content is sampled.
                    bool continuous = applicable && (humanPanel?.Visibility == Visibility.Visible && humanCanvas?.IsVisible == true ||
                        humanPlacementMode || IsControlledBuildSession() || powerToolbarOwned || pendingPower != null ||
                        gateHudLifecycle.Pending || selectedGate != null || observedGate != null);
                    renderWork.SetContinuous(continuous);
                    if (!continuous && (applicable || SessionAwaitingPresentationReadiness()))
                        renderWork.RequestAfter(presentationStatusInterval);
                    if (!presentationCallbacks.IsEmpty || log.HasPending || !testActions.IsEmpty) RequestRender();
                }
            }
        }
    }
}
