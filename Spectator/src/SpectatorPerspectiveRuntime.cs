using APIShared.GameModes;
using System;
using APIShared;
using BepInEx.Logging;
using CrusaderDE;
using Noesis;
using Shared;
using SHCDESE.API;
using SHCDESE.API.Components.SaveData;
using SHCDESE.Interop.Enums;
using UnityEngine;

namespace Spectator
{
    internal static partial class SpectatorPerspectiveRuntime
    {
        private static ManualLogSource log;
        private static SpectatorViewModel settings;
        private static bool initialized;
        private static bool featureReady;
        private static bool sessionEnabled;
        // Spectator input control for this mission; settings only change between missions.
        private static bool sessionControl;
        private static bool controlFailureLogged;
        private static long preparedSessionId;
        private static long announcedSessionId;
        private static bool mapReady;
        private static bool loadedFromSave;
        private static int saveRecoveryStage;
        private static int savedView;
        private static bool recoveryIdentityApplied;
        private static bool spectatorActive;
        private static bool perspectiveUncertain;
        private static bool initializationPending;
        private static bool hudPending;
        private static bool resetHudPending;
        private static bool renderSubscribed;
        private static bool failureLogged;
        private static int selectedPlayer;
        private static int lastPendingFrame = -1;
        private static float pendingSince;
        private static string selectedName;
        private static bool[] occupiedSlots;
        private static EngineInterface.PlayState stateBeforeLoad;
        private static EngineInterface.PlayState alliesStateBeforeSwitch;
        private static Noesis.Grid hiddenReportPanel;
        private static PanelInputLease reportPanelLease;
        private static HUD_AlliesPanel hiddenAlliesPanel;
        private static readonly PanelRefreshState alliesRefresh = new PanelRefreshState();
        internal static bool AlliesRefreshing => alliesRefresh.Pending;
        private static Noesis.Grid guardedFoodPanel;
        private static readonly Button[] guardedFoodButtons = new Button[4];
        private static readonly bool[] foodButtonHitTestBeforeGuard = new bool[4];
        private static readonly string[] foodButtonNames = { "EatingMeat", "EatingCheese", "EatingBread", "EatingApples" };

        internal static void Initialize(ManualLogSource logger, SpectatorViewModel currentSettings)
        {
            if (initialized) return;
            initialized = true;
            log = logger;
            settings = currentSettings ?? throw new ArgumentNullException(nameof(currentSettings));
            try
            {
                if (!ModSaveDataAPI.Instance.RegisterModDataHandler(SpectatorSaveMarker.Identifier,
                    SaveSpectatorMarker, IgnoreLoadedMarker))
                    throw new InvalidOperationException("Spectator save-data handler already registered.");
                SpectatorReportHooks.Install();
                InitializeReportRefresh();
                SpectatorAllyHooks.Install();
                if (!ApiShared.Current.TryGetMissionLifecycle(SpectatorPlugin.PluginGuid,
                    out IMissionLifecycleCapability lifecycle, out NativeCapabilityDiagnostic diagnostic))
                    throw new InvalidOperationException("Mission lifecycle unavailable: " + diagnostic?.Reason);
                if (!lifecycle.TryRegisterObserver("Spectator.SpectatorPerspective",
                    OnMissionStart, OnMissionEnd, OnMissionInitialization, out diagnostic))
                    throw new InvalidOperationException("Mission lifecycle registration failed: " + diagnostic?.Reason);
                featureReady = true;
            }
            catch (Exception error)
            {
                log.LogError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] SPECTATOR_PERSPECTIVE_INITIALIZATION_FAILED: {error}");
            }
            // Installs APIShared's input boundaries only when the option is used: now (LibraryLoaded,
            // no thread suspension) or when it is enabled later between missions.
            EnsureControlRegistration();
            settings.SettingsChanged += EnsureControlRegistration;
        }

        private static bool controlRegistered, controlRegistrationLogged;

        private static void EnsureControlRegistration()
        {
            // APIShared installs only between missions; a refusal inside one is no failure to report.
            if (controlRegistered || !featureReady || !settings.Activation.Control || mapReady) return;
            try
            {
                controlRegistered = PlayerPerspectiveAPI.TryRegisterSpectatorControl(SpectatorPlugin.PluginGuid, out string reason);
                if (controlRegistered || controlRegistrationLogged) return;
                controlRegistrationLogged = true;
                Shared.DebugLogHelper.LogWarning(log, "SPECTATOR_CONTROL_UNAVAILABLE: " + reason +
                    " Selecting a player keeps the view only.");
            }
            catch (Exception error)
            {
                if (controlRegistrationLogged) return;
                controlRegistrationLogged = true;
                Shared.DebugLogHelper.LogError(log, "SPECTATOR_CONTROL_REGISTRATION_FAILED: " + error);
            }
        }

        private static void OnMissionInitialization(MissionLifecycleNotification notification)
        {
            if (!notification.IsBeforeInitialization ||
                preparedSessionId == notification.Context.SessionId) return;
            long session = notification.Context.SessionId;
            System.Threading.Interlocked.Exchange(ref announcedSessionId, session);
            SpectatorRuntime.PostPresentation(() => { if (System.Threading.Interlocked.Read(ref announcedSessionId) == session) PrepareSession(session); });
        }

        private static void PrepareSession(long sessionId)
        {
            var previousState = GameData.Instance?.lastGameState;
            BeginMapLoad();
            stateBeforeLoad = previousState;
            preparedSessionId = sessionId;
        }

        private static void OnMissionStart(MissionLifecycleNotification notification)
        {
            long session = notification.Context.SessionId;
            System.Threading.Interlocked.Exchange(ref announcedSessionId, session);
            SpectatorRuntime.PostPresentation(() => {
                if (System.Threading.Interlocked.Read(ref announcedSessionId) != session) return;
                if (preparedSessionId != session) PrepareSession(session);
                sessionEnabled = settings.Activation.Perspective;
                sessionControl = settings.Activation.Control;
                mapReady = true;
                loadedFromSave = notification.Context.IsSave;
                initializationPending = true;
                pendingSince = Time.realtimeSinceStartup;
                ArmRenderIfNeeded();
            });
        }

        private static void OnMissionEnd(MissionLifecycleNotification notification)
        {
            long session = notification.Context.SessionId;
            System.Threading.Interlocked.CompareExchange(ref announcedSessionId, 0, session);
            SpectatorRuntime.PostPresentation(() => {
                if (System.Threading.Interlocked.Read(ref announcedSessionId) != 0 || preparedSessionId != session) return;
                BeginMapLoad(); preparedSessionId = 0;
            });
        }

        private static void BeginMapLoad()
        {
            PlayerPerspectiveAPI.ClearEliminatedPlayerView();
            PlayerPerspectiveAPI.ClearSpectatorView();
            ClearReportCache();
            RestoreAllyPanel();
            RestoreFoodControls();
            SpectatorAllyHooks.RestoreControls();
            mapReady = false;
            sessionEnabled = false;
            sessionControl = false;
            controlFailureLogged = false;
            loadedFromSave = false;
            saveRecoveryStage = 0;
            savedView = 0;
            recoveryIdentityApplied = false;
            spectatorActive = false;
            perspectiveUncertain = false;
            initializationPending = false;
            hudPending = false;
            selectedPlayer = 0;
            selectedName = null;
            failureLogged = false;
            occupiedSlots = null;
            stateBeforeLoad = null;
            resetHudPending = true;
            ArmRenderIfNeeded();
            // An option enabled during a mission can only install between missions.
            EnsureControlRegistration();
        }

        private static void ArmRenderIfNeeded()
        {
            if (renderSubscribed) return;
            pendingSince = Time.realtimeSinceStartup;
            renderSubscribed = true;
            Application.onBeforeRender += OnPendingRender;
        }

        private static void StopRenderIfIdle()
        {
            if (!renderSubscribed || resetHudPending || initializationPending || hudPending ||
                reportRefresh.Pending || alliesStateBeforeSwitch != null) return;
            Application.onBeforeRender -= OnPendingRender;
            renderSubscribed = false;
        }

        private static void OnPendingRender()
        {
            if (lastPendingFrame == Time.frameCount) return;
            lastPendingFrame = Time.frameCount;
            try
            {
                if (resetHudPending)
                {
                    resetHudPending = false;
                    SpectatorRuntime.InvalidateToolbar();
                }
                if (initializationPending)
                {
                    var state = GameData.Instance?.lastGameState;
                    if (state != null && (stateBeforeLoad == null || !ReferenceEquals(state, stateBeforeLoad)))
                    {
                        AdvanceInitialization(state);
                        if (!initializationPending) stateBeforeLoad = null;
                    }
                }
                if (hudPending && spectatorActive)
                {
                    if (SpectatorRuntime.ToolbarReady)
                    {
                        hudPending = false;
                        if (selectedName == null) CacheSelectedName();
                        ApplyControlPresentation();
                    }
                    else if ((MainViewModel.viewModelLoaded && MainViewModel.Instance.Show_HUD_Briefing))
                        hudPending = false;
                }
                RefreshPendingReport();
                RefreshPendingAllies(GameData.Instance?.lastGameState);
                if ((initializationPending || hudPending || reportRefresh.Pending || alliesStateBeforeSwitch != null) &&
                    Time.realtimeSinceStartup - pendingSince > 15f)
                {
                    string pending = initializationPending ? saveRecoveryStage == 1 ? "restored spectator PlayState" :
                        saveRecoveryStage == 2 ? "selected-player PlayState" : "PlayState" : hudPending ? "IngameUI" :
                        reportRefresh.Pending ? "report PlayState" : "allies PlayState";
                    Shared.DebugLogHelper.LogWarning(log, "SPECTATOR_PERSPECTIVE_READY_TIMEOUT: " + pending +
                        " did not become ready within 15 seconds.");
                    initializationPending = false;
                    saveRecoveryStage = 0;
                    hudPending = false;
                    RestoreReportPanel();
                    ClosePendingAllies();
                }
                StopRenderIfIdle();
            }
            catch (Exception error)
            {
                if (!recoveryIdentityApplied) PlayerPerspectiveAPI.ClearSpectatorView();
                initializationPending = false;
                hudPending = false;
                resetHudPending = false;
                spectatorActive = false;
                RestoreReportPanel();
                ClosePendingAllies();
                RestoreFoodControls();
                SpectatorRuntime.InvalidateToolbar();
                StopRenderIfIdle();
                if (failureLogged) return;
                failureLogged = true;
                Shared.DebugLogHelper.LogError(log, "SPECTATOR_PERSPECTIVE_ERROR: " + error);
            }
        }

        private static void AdvanceInitialization(EngineInterface.PlayState state)
        {
            if (state.game_type != (int)eGameTypeModes.GAMETYPE_MULTIPLAYER)
            {
                initializationPending = false;
                saveRecoveryStage = 0;
                return;
            }
            if (EditorDirector.instance == null) return;
            if (saveRecoveryStage == 1)
            {
                stateBeforeLoad = state;
                if (state.spectatorMode == 0) return;
                if (sessionEnabled ? !InitializePerspective(state, savedView, true) :
                    !RestoreSavedViewWithoutExtensions(savedView))
                {
                    initializationPending = false;
                    saveRecoveryStage = 0;
                    return;
                }
                saveRecoveryStage = 2;
                return;
            }
            if (saveRecoveryStage == 2)
            {
                stateBeforeLoad = state;
                if (state.spectatorMode == 0 || PlayerPerspectiveAPI.GetRawNativeViewPlayerId() != selectedPlayer)
                    return;
                saveRecoveryStage = 0;
                initializationPending = false;
                if (!sessionEnabled) return;
                spectatorActive = true;
                hudPending = true;
                CacheSelectedName();
                SpectatorAllyHooks.RefreshOpenPanel();
                return;
            }
            if (loadedFromSave && TryRecognizeSavedSpectator(state, out int view))
            {
                // The load path clears the native spectator flag and gives the view a positive managed ID.
                // Lock managed input first, then restore Vanilla's local-only spectator action once.
                recoveryIdentityApplied = true;
                EditorDirector.instance.SetLocalPlayer(-1);
                EngineInterface.GameAction(Enums.GameActionCommand.SpectatorMode, 0, 0);
                savedView = view;
                stateBeforeLoad = state;
                saveRecoveryStage = 1;
                pendingSince = Time.realtimeSinceStartup;
                return;
            }
            if (sessionEnabled && state.spectatorMode != 0 && IsSpectatorObserver())
            {
                int initialView = SpectatorDefeat.IsEliminatedSpectator ? PlayerPerspectiveAPI.GetControlledPlayerId() : 0;
                if (InitializePerspective(state, initialView, false))
                {
                    spectatorActive = true;
                    hudPending = true;
                }
            }
            initializationPending = false;
        }

        private static bool TryRecognizeSavedSpectator(EngineInterface.PlayState state, out int view)
        {
            view = 0;
            bool realMultiplayer = GameModeHelper.IsRealMultiplayer();
            int rawView = PlayerPerspectiveAPI.GetRawNativeViewPlayerId();
            byte[] marker = GameMapArchiveManagerAPI.Instance.TryReadBinaryFile(SpectatorSaveMarker.EntryName) ??
                GameMapArchiveManagerAPI.Instance.TryReadBinaryFile(SpectatorSaveMarker.LegacyEntryName);
            bool recognized;
            string reason;
            if (marker != null)
            {
                if (!SpectatorSaveMarker.TryDecode(marker, out int markedView))
                {
                    reason = "invalid spectator save marker";
                    recognized = false;
                }
                else if (markedView == 0)
                    return false;
                else
                    recognized = SpectatorSavePolicy.TryRecognizeMarked(state.player_register,
                        state.computer_register, realMultiplayer, rawView, markedView,
                        out view, out reason);
            }
            else
                recognized = SpectatorSavePolicy.TryRecognize(state.player_register, state.computer_register,
                    realMultiplayer, rawView, out view, out reason);
            return recognized;
        }

        private static bool RestoreSavedViewWithoutExtensions(int view)
        {
            if (PlayerPerspectiveAPI.TrySetSpectatorView(view))
            {
                selectedPlayer = view;
                return true;
            }
            Shared.DebugLogHelper.LogError(log, "SPECTATOR_SAVE_RESTORE_FAILED: APIShared rejected the saved CPU view.");
            return false;
        }

        private static byte[] SaveSpectatorMarker(SaveContext context)
        {
            if (context == null || !context.IsSaveFile || context.IsMapEditorSave) return null;
            var state = GameData.Instance?.lastGameState;
            int candidate = IsActiveSpectator() ? selectedPlayer : PlayerPerspectiveAPI.GetRawNativeViewPlayerId();
            int view = featureReady && !GameModeHelper.IsRealMultiplayer() &&
                state != null && state.spectatorMode != 0 &&
                EditorDirector.instance != null && EditorDirector.instance.ActivePlayerID <= 0 &&
                candidate >= 1 && candidate <= 8 && state.is_skirmish_player(candidate) &&
                !state.is_valid_player(candidate) ? candidate : 0;
            // Write an explicit non-spectator marker when an existing save archive is overwritten.
            return SpectatorSaveMarker.Encode(view);
        }

        private static void IgnoreLoadedMarker(byte[] bytes, LoadContext context)
        {
            // OnPostLoad reads the active archive, after both of the extender's load passes.
        }

        private static bool InitializePerspective(EngineInterface.PlayState state, int requestedView, bool cpuOnly)
        {
            if (!mapReady) return false;
            occupiedSlots = new bool[9];
            int first = 0;
            for (int player = 1; player <= 8; player++)
            {
                occupiedSlots[player] = cpuOnly ? state.is_skirmish_player(player) :
                    state.is_human_or_skirmish_player(player);
                if (occupiedSlots[player] && first == 0 && !SpectatorDefeat.IsPlayerEliminated(player)) first = player;
            }
            if (first == 0) return false;
            // An eliminated participant starts on its own view (no foreign-view lease needed) even
            // though defeated players are not listed; every other request must be an undefeated slot.
            bool ownEliminatedView = SpectatorDefeat.IsEliminatedSpectator && requestedView == PlayerPerspectiveAPI.GetControlledPlayerId();
            int initialView = requestedView >= 1 && requestedView <= 8 && occupiedSlots[requestedView] &&
                (ownEliminatedView || !SpectatorDefeat.IsPlayerEliminated(requestedView)) ? requestedView : first;
            if (!TrySetView(initialView))
            {
                Shared.DebugLogHelper.LogError(log, "SPECTATOR_PERSPECTIVE_SELECT_FAILED: APIShared rejected the initial spectator view.");
                return false;
            }
            selectedPlayer = initialView;
            SpectatorRuntime.AcceptPerspective(initialView);
            return true;
        }

        // The controlled player's own Vanilla actions are not spectator actions.
        internal static bool IsSpectatorActionRestricted() => featureReady &&
            (recoveryIdentityApplied || IsActiveSpectator() || IsSpectatorObserver()) && !IsInputControlled();

        // True only while APIShared confirms control of exactly the selected player in this mission.
        // Cheap field checks first: without the registered option no APIShared call happens per frame.
        internal static bool IsInputControlled() => controlRegistered && sessionControl && featureReady && mapReady &&
            selectedPlayer > 0 && PlayerPerspectiveAPI.GetInputControlledPlayerId() == selectedPlayer;

        internal static int InputControlledPlayer() => IsInputControlled() ? selectedPlayer : 0;

        // Spectator restrictions follow the actual control state after every selection.
        private static void ApplyControlPresentation()
        {
            if (IsInputControlled()) RestoreFoodControls();
            else if (spectatorActive && SpectatorRuntime.ToolbarReady) GuardFoodControls();
            if (MainViewModel.viewModelLoaded) SpectatorAllyHooks.RefreshControlState();
            SpectatorRuntime.InvalidateToolbar();
        }

        // Information and offline tool permissions must not depend on a nonpositive participant ID.
        internal static bool IsSpectatorObserver() =>
            SpectatorDefeatPolicy.IsSpectatorObserver(IsOriginalSpectator(), SpectatorDefeat.IsEliminatedSpectator);

        internal static void OnEliminatedSpectatorConfirmed()
        {
            if (!featureReady || !mapReady || !sessionEnabled || spectatorActive) return;
            // Promotion can happen long after the one-time mission initialization has completed.
            stateBeforeLoad = null;
            initializationPending = true;
            pendingSince = Time.realtimeSinceStartup;
            ArmRenderIfNeeded();
        }

        private static bool TrySetView(int player)
        {
            if (SpectatorDefeat.IsEliminatedSpectator) return PlayerPerspectiveAPI.TrySetEliminatedPlayerView(player);
            // Original offline spectators with the option control the selected player. If APIShared
            // rejects control (unsupported image or state), the plain report view stays available.
            if (sessionControl && controlRegistered && IsOriginalSpectator() && !GameModeHelper.IsRealMultiplayer())
            {
                if (PlayerPerspectiveAPI.TrySetSpectatorControl(player)) return true;
                if (!controlFailureLogged)
                {
                    controlFailureLogged = true;
                    Shared.DebugLogHelper.LogWarning(log, "SPECTATOR_CONTROL_UNAVAILABLE: player=" + player +
                        "; APIShared rejected spectator input control, selecting the view only.");
                }
            }
            return PlayerPerspectiveAPI.TrySetSpectatorView(player);
        }

        internal static bool IsOriginalSpectator()
        {
            var state = GameData.Instance?.lastGameState;
            return mapReady && state != null && state.game_type == (int)eGameTypeModes.GAMETYPE_MULTIPLAYER && state.spectatorMode != 0 &&
                   EditorDirector.instance != null && EditorDirector.instance.ActivePlayerID <= 0;
        }

        // The spectator roster is shown as soon as the new mission's PlayState is present, before
        // perspective initialization completes; selection itself still requires IsActiveSpectator.
        internal static bool IsSpectatorRoster()
        {
            if (IsActiveSpectator()) return true;
            var state = GameData.Instance?.lastGameState;
            return featureReady && sessionEnabled && mapReady && !perspectiveUncertain && state != null &&
                (stateBeforeLoad == null || !ReferenceEquals(state, stateBeforeLoad)) &&
                state.game_type == (int)eGameTypeModes.GAMETYPE_MULTIPLAYER && state.spectatorMode != 0 &&
                EditorDirector.instance != null && IsSpectatorObserver();
        }

        // Presentation thread, after an APIShared defeat-state change: a defeated selected player is
        // replaced by the next listed, undefeated occupied slot.
        internal static void OnDefeatStateChanged()
        {
            if (!spectatorActive || selectedPlayer < 1 || occupiedSlots == null ||
                !SpectatorDefeat.IsPlayerEliminated(selectedPlayer)) return;
            // An eliminated participant's deliberate own view is kept.
            if (SpectatorDefeat.IsEliminatedSpectator && selectedPlayer == PlayerPerspectiveAPI.GetControlledPlayerId()) return;
            for (int step = 1; step < 8; step++)
            {
                int candidate = (selectedPlayer - 1 + step) % 8 + 1;
                if (!occupiedSlots[candidate] || SpectatorDefeat.IsPlayerEliminated(candidate)) continue;
                SelectPlayer(candidate);
                return;
            }
        }

        internal static bool IsActiveSpectator()
        {
            var state = GameData.Instance?.lastGameState;
            return preparedSessionId > 0 && preparedSessionId == System.Threading.Interlocked.Read(ref announcedSessionId) && featureReady && sessionEnabled && mapReady && spectatorActive && !perspectiveUncertain && selectedPlayer > 0 && PlayerPerspectiveAPI.GetViewedPlayerId() >= 0 && state != null &&
                   state.game_type == (int)eGameTypeModes.GAMETYPE_MULTIPLAYER && state.spectatorMode != 0 &&
                   EditorDirector.instance != null && IsSpectatorObserver();
        }

        internal static int GetAllyViewPlayerId(GameData data)
        {
            return IsActiveSpectator() ? selectedPlayer : data.playerID;
        }

        internal static bool IsNetworkSpectator()
        {
            var state = GameData.Instance?.lastGameState;
            return featureReady && state != null && state.game_type == (int)eGameTypeModes.GAMETYPE_MULTIPLAYER && state.spectatorMode != 0 &&
                   GameModeHelper.IsRealMultiplayer();
        }

        internal static bool CanInteractWithAllies()
        {
            var state = GameData.Instance?.lastGameState;
            return !AlliesRefreshing && IsActiveSpectator() && !GameModeHelper.IsRealMultiplayer() && state != null &&
                   state.is_skirmish_player(selectedPlayer) && !state.is_valid_player(selectedPlayer) &&
                   EngineInterface.GetMeritData()[selectedPlayer, 1] >= 0;
        }

        internal static bool CanIssueAllyAction(Enums.GameActionCommand command, int structureID, int stateValue, int value2)
        {
            if (!CanInteractWithAllies()) return false;
            var state = GameData.Instance.lastGameState;
            if (command == Enums.GameActionCommand.Ally_Orders &&
                (structureID < 1 || structureID > 6) && structureID != 10 && structureID != 11)
                return false;
            int target = command == Enums.GameActionCommand.Ally_Orders ? stateValue : structureID;
            if (target < 1 || target > 8 || target == selectedPlayer ||
                !state.is_human_or_skirmish_player(target) || state.teams[target] != state.teams[selectedPlayer])
                return false;
            if (command == Enums.GameActionCommand.Ally_Orders && structureID >= 1 && structureID <= 6 &&
                (value2 < 1 || value2 > 8 || !state.is_human_or_skirmish_player(value2) ||
                 state.teams[value2] == state.teams[selectedPlayer]))
                return false;
            return EngineInterface.GetMeritData()[target, 1] >= 0;
        }

        internal static bool TryGetSelectedReportName(out string name)
        {
            name = selectedName;
            var state = GameData.Instance?.lastGameState;
            return IsActiveSpectator() && name != null && state != null &&
                   state.app_mode == 16 && state.app_sub_mode == 71;
        }

        private static void CacheSelectedName()
        {
            string name = Platform_Multiplayer.Instance?.getSkirmishName(selectedPlayer);
            selectedName = string.IsNullOrWhiteSpace(name) ? "-" : name;
            if (!reportRefresh.Pending && GameData.Instance?.app_mode == 16 && GameData.Instance.app_sub_mode == 71 && MainViewModel.Instance != null)
                MainViewModel.Instance.PlayerNameText = selectedName;
        }

        private static void WaitForFreshReport()
        {
            if (GameData.Instance?.app_mode != 16) return;
            var panel = MainViewModel.Instance?.HUDBuildingPanel?.RefBuildingPanel;
            if (panel == null) return;
            if (hiddenReportPanel != null && !ReferenceEquals(hiddenReportPanel, panel)) RestoreReportPanel();
            try
            {
                int boundary = SpectatorRuntime.CaptureReportFrame(reportClock);
                reportRefresh.Begin(preparedSessionId, panel, selectedPlayer, GameData.Instance.app_sub_mode, boundary);
                pendingSince = Time.realtimeSinceStartup;
                if (hiddenReportPanel == null)
                {
                    hiddenReportPanel = panel;
                    reportPanelLease = new PanelInputLease(panel);
                    HoldReportValues();
                }
                ArmRenderIfNeeded();
            }
            catch (Exception error)
            {
                RestoreReportPanel();
                Shared.DebugLogHelper.LogError(log, "SPECTATOR_REPORT_REFRESH_FAILED: " + error);
            }
        }

        private static void RefreshPendingReport()
        {
            if (reportRefresh.Pending && !ReportContextCurrent()) RestoreReportPanel();
            // Completion is handled after Vanilla's GUI writes, never on an arbitrary render frame.
        }

        private static void RestoreReportPanel()
        {
            reportRefresh.Cancel();
            var display = reportDisplay; reportDisplay = null;
            var input = reportPanelLease; reportPanelLease = null;
            hiddenReportPanel = null;
            Exception failure = display?.Restore();
            try { input?.Restore(); } catch (Exception error) { if (failure == null) failure = error; }
            if (failure != null) Shared.DebugLogHelper.LogError(log, "SPECTATOR_REPORT_RESTORE_FAILED: " + failure);
        }

        private static void WaitForFreshAllies(EngineInterface.PlayState oldState)
        {
            if (MainViewModel.Instance?.AlliesPanelVisible != true) return;
            var panel = MainViewModel.Instance.HUDAlliesPanel;
            if (panel == null) return;
            if (hiddenAlliesPanel != null && !ReferenceEquals(hiddenAlliesPanel, panel)) RestoreAllyPanel();
            alliesStateBeforeSwitch = oldState;
            pendingSince = Time.realtimeSinceStartup;
            if (hiddenAlliesPanel == null)
            {
                hiddenAlliesPanel = panel;
                panel.IsVisibleChanged += OnPendingAllyVisibilityChanged;
            }
            alliesRefresh.Begin(preparedSessionId, panel, oldState);
            SpectatorAllyHooks.RefreshControlState();
            ArmRenderIfNeeded();
        }

        private static void RefreshPendingAllies(EngineInterface.PlayState state)
        {
            if (alliesStateBeforeSwitch == null) return;
            if (!mapReady || !alliesRefresh.IsCurrent(preparedSessionId,
                MainViewModel.Instance?.HUDAlliesPanel, MainViewModel.Instance?.AlliesPanelVisible == true))
            {
                RestoreAllyPanel();
                return;
            }
            if (!alliesRefresh.HasFreshState(state)) return;
            try { SpectatorAllyHooks.RefreshOpenPanel(); }
            finally { RestoreAllyPanel(); }
        }

        private static void ClosePendingAllies()
        {
            if (alliesStateBeforeSwitch != null && MainViewModel.Instance != null)
                MainViewModel.Instance.AlliesPanelVisible = false;
            RestoreAllyPanel();
        }

        private static void OnPendingAllyVisibilityChanged(object sender, DependencyPropertyChangedEventArgs args)
        {
            if (hiddenAlliesPanel?.IsVisible != true) RestoreAllyPanel();
        }
        private static void RestoreAllyPanel()
        {
            if (hiddenAlliesPanel != null) hiddenAlliesPanel.IsVisibleChanged -= OnPendingAllyVisibilityChanged;
            alliesRefresh.Cancel();
            hiddenAlliesPanel = null;
            alliesStateBeforeSwitch = null;
            if (MainViewModel.viewModelLoaded) SpectatorAllyHooks.RefreshControlState();
        }

        private static void GuardFoodControls()
        {
            var foodPanel = MainViewModel.Instance?.HUDBuildingPanel?.RefReportsFoodPanel;
            if (foodPanel == null || ReferenceEquals(foodPanel, guardedFoodPanel)) return;
            RestoreFoodControls();
            guardedFoodPanel = foodPanel;
            for (int index = 0; index < foodButtonNames.Length; index++)
            {
                var button = GameXAMLManagerAPI.Instance?.FindElementByName(foodPanel, foodButtonNames[index]) as Button;
                if (button == null) continue;
                guardedFoodButtons[index] = button;
                foodButtonHitTestBeforeGuard[index] = button.IsHitTestVisible;
                button.IsHitTestVisible = false;
            }
        }

        private static void RestoreFoodControls()
        {
            for (int index = 0; index < guardedFoodButtons.Length; index++)
            {
                if (guardedFoodButtons[index] != null)
                    guardedFoodButtons[index].IsHitTestVisible = foodButtonHitTestBeforeGuard[index];
                guardedFoodButtons[index] = null;
            }
            guardedFoodPanel = null;
        }

        internal static void OnHudUnavailable()
        {
            RestoreFoodControls();
            ClearReportCache();
            RestoreAllyPanel();
            if (!mapReady || !spectatorActive) return;
            hudPending = true;
            ArmRenderIfNeeded();
        }

        internal static void OnHudAvailable()
        {
            CacheReportValues(MainViewModel.Instance.HUDBuildingPanel);
            if (!mapReady || !spectatorActive) return;
            hudPending = true;
            ArmRenderIfNeeded();
        }

        internal static void ObservePerspective(int player)
        {
            if (player < 1 || player > 8)
            {
                if (!mapReady || (!spectatorActive && !perspectiveUncertain)) return;
                spectatorActive = false;
                perspectiveUncertain = true;
                selectedPlayer = 0;
                selectedName = null;
                initializationPending = hudPending = false;
                ClearReportCache();
                ClosePendingAllies();
                CloseUncertainPresentation();
                SpectatorRuntime.InvalidateToolbar();
                return;
            }
            if ((!spectatorActive && !perspectiveUncertain) || player == selectedPlayer) return;
            var state = GameData.Instance?.lastGameState;
            if (state == null || !state.is_human_or_skirmish_player(player) ||
                !sessionEnabled || !IsSpectatorObserver()) return;
            perspectiveUncertain = false;
            spectatorActive = true;
            selectedPlayer = player;
            WaitForFreshReport();
            CacheSelectedName();
            WaitForFreshAllies(state);
        }

        internal static bool CanOpenReports() => SpectatorFeatureActivation.CanOpenReports(IsSpectatorObserver(),
            perspectiveUncertain || PlayerPerspectiveAPI.GetViewedPlayerId() < 0);

        private static void CloseUncertainPresentation()
        {
            if (!MainViewModel.viewModelLoaded) return;
            var view = MainViewModel.Instance;
            view.AlliesPanelVisible = false;
            // This public Vanilla helper only changes HUD flags. Preserve unrelated modal overlays.
            if (GameData.Instance?.app_mode == (int)Enums.AppModes.APP_MODE_IN_BUILDING && view.Show_HUD_Building &&
                !view.Show_HUD_MissionOver && !view.Show_HUD_Briefing)
                view.DefaultGameUIGameAction();
        }

        internal static void SelectPlayer(int player)
        {
            if (!spectatorActive || player < 1 || player > 8 || occupiedSlots == null || !occupiedSlots[player] ||
                SpectatorDefeat.IsPlayerEliminated(player)) return;
            var state = GameData.Instance?.lastGameState;
            if (state == null || state.spectatorMode == 0 || state.game_type != (int)eGameTypeModes.GAMETYPE_MULTIPLAYER ||
                EditorDirector.instance == null || !IsSpectatorObserver() ||
                !state.is_human_or_skirmish_player(player) || selectedPlayer == player) return;
            try
            {
                if (!TrySetView(player)) return;
            }
            catch (Exception error)
            {
                Shared.DebugLogHelper.LogError(log, $"SPECTATOR_PERSPECTIVE_SELECT_FAILED: player={player}, error={error}.");
                return;
            }
            selectedPlayer = player;
            WaitForFreshReport();
            CacheSelectedName();
            SpectatorRuntime.AcceptPerspective(player);
            WaitForFreshAllies(state);
            ApplyControlPresentation();
        }

    }
}
