using System;
using APIShared;
using BepInEx.Logging;
using CrusaderDE;
using Noesis;
using Shared;
using SHCDESE.API;
using SHCDESE.API.Components.SaveData;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using UnityEngine;

namespace BugfixesAndQoL
{
    internal static class SpectatorPerspectiveRuntime
    {
        private static ManualLogSource log;
        private static BugfixesAndQoLViewModel settings;
        private static SpectatorPerspectiveHud hud;
        private static bool initialized;
        private static bool featureReady;
        private static bool sessionEnabled;
        private static long preparedSessionId;
        private static bool mapReady;
        private static bool loadedFromSave;
        private static int saveRecoveryStage;
        private static int savedView;
        private static bool recoveryIdentityApplied;
        private static bool spectatorActive;
        private static bool initializationPending;
        private static bool hudPending;
        private static bool resetHudPending;
        private static bool renderSubscribed;
        private static bool failureLogged;
        private static bool cameraFailureLogged;
        private static int selectedPlayer;
        private static int lastPendingFrame = -1;
        private static float pendingSince;
        private static string selectedName;
        private static bool[] occupiedSlots;
        private static EngineInterface.PlayState stateBeforeLoad;
        private static EngineInterface.PlayState reportStateBeforeSwitch;
        private static EngineInterface.PlayState alliesStateBeforeSwitch;
        private static Noesis.Grid hiddenReportPanel;
        private static Visibility reportPanelVisibility;
        private static HUD_AlliesPanel hiddenAlliesPanel;
        private static Visibility alliesPanelVisibility;
        private static Noesis.Grid guardedFoodPanel;
        private static readonly Button[] guardedFoodButtons = new Button[4];
        private static readonly bool[] foodButtonHitTestBeforeGuard = new bool[4];
        private static readonly string[] foodButtonNames = { "EatingMeat", "EatingCheese", "EatingBread", "EatingApples" };

        internal static void Initialize(ManualLogSource logger, BugfixesAndQoLViewModel currentSettings)
        {
            if (initialized) return;
            initialized = true;
            log = logger;
            settings = currentSettings ?? throw new ArgumentNullException(nameof(currentSettings));
            hud = new SpectatorPerspectiveHud(SelectPlayer, JumpToPlayer, OnHudUnavailable,
                OnHudAvailable, OnBriefingChanged);
            try
            {
                if (!ModSaveDataAPI.Instance.RegisterModDataHandler(SpectatorSaveMarker.Identifier,
                    SaveSpectatorMarker, IgnoreLoadedMarker))
                    throw new InvalidOperationException("Spectator save-data handler already registered.");
                SpectatorReportHooks.Install();
                SpectatorAllyHooks.Install();
                if (!ApiShared.Current.TryGetMissionLifecycle(BugfixesAndQoLPlugin.PluginGuid,
                    out IMissionLifecycleCapability lifecycle, out NativeCapabilityDiagnostic diagnostic))
                    throw new InvalidOperationException("Mission lifecycle unavailable: " + diagnostic?.Reason);
                if (!lifecycle.TryRegisterObserver("BugfixesAndQoL.SpectatorPerspective",
                    OnMissionStart, OnMissionEnd, OnMissionInitialization, out diagnostic))
                    throw new InvalidOperationException("Mission lifecycle registration failed: " + diagnostic?.Reason);
                featureReady = true;
            }
            catch (Exception error)
            {
                log.LogError($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] SPECTATOR_PERSPECTIVE_INITIALIZATION_FAILED: {error}");
            }
        }

        private static void OnMissionInitialization(MissionLifecycleNotification notification)
        {
            if (!notification.IsBeforeInitialization ||
                preparedSessionId == notification.Context.SessionId) return;
            PrepareSession(notification.Context.SessionId);
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
            if (preparedSessionId != notification.Context.SessionId)
                PrepareSession(notification.Context.SessionId);
            sessionEnabled = settings.EnableMod && settings.EnableSpectatorPerspective;
            mapReady = true;
            loadedFromSave = notification.Context.IsSave;
            // The managed game type can still be stale here. Decide from the first fresh PlayState.
            initializationPending = true;
            pendingSince = Time.realtimeSinceStartup;
            ArmRenderIfNeeded();
        }

        private static void OnMissionEnd(MissionLifecycleNotification notification)
        {
            if (preparedSessionId != notification.Context.SessionId) return;
            BeginMapLoad();
            preparedSessionId = 0;
        }

        private static void BeginMapLoad()
        {
            PlayerPerspectiveAPI.ClearSpectatorView();
            RestoreReportPanel();
            RestoreAllyPanel();
            RestoreFoodControls();
            SpectatorAllyHooks.RestoreControls();
            mapReady = false;
            sessionEnabled = false;
            loadedFromSave = false;
            saveRecoveryStage = 0;
            savedView = 0;
            recoveryIdentityApplied = false;
            spectatorActive = false;
            initializationPending = false;
            hudPending = false;
            selectedPlayer = 0;
            selectedName = null;
            failureLogged = false;
            cameraFailureLogged = false;
            occupiedSlots = null;
            stateBeforeLoad = null;
            resetHudPending = true;
            ArmRenderIfNeeded();
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
                reportStateBeforeSwitch != null || alliesStateBeforeSwitch != null) return;
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
                    hud.ResetForSession();
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
                    if (hud.TryShow(occupiedSlots, selectedPlayer))
                    {
                        hudPending = false;
                        if (selectedName == null) CacheSelectedName();
                        GuardFoodControls();
                    }
                    else if (hud.IsBriefingVisible)
                        hudPending = false;
                }
                RefreshPendingReport(GameData.Instance?.lastGameState);
                RefreshPendingAllies(GameData.Instance?.lastGameState);
                if ((initializationPending || hudPending || reportStateBeforeSwitch != null || alliesStateBeforeSwitch != null) &&
                    Time.realtimeSinceStartup - pendingSince > 15f)
                {
                    string pending = initializationPending ? saveRecoveryStage == 1 ? "restored spectator PlayState" :
                        saveRecoveryStage == 2 ? "selected-player PlayState" : "PlayState" : hudPending ? "IngameUI" :
                        reportStateBeforeSwitch != null ? "report PlayState" : "allies PlayState";
                    log.LogWarning("SPECTATOR_PERSPECTIVE_READY_TIMEOUT: " + pending +
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
                hud.Hide();
                StopRenderIfIdle();
                if (failureLogged) return;
                failureLogged = true;
                log.LogError("SPECTATOR_PERSPECTIVE_ERROR: " + error);
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
            if (sessionEnabled && state.spectatorMode != 0 && EditorDirector.instance.ActivePlayerID <= 0)
            {
                if (InitializePerspective(state, 0, false))
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
            byte[] marker = GameMapArchiveManagerAPI.Instance.TryReadBinaryFile(SpectatorSaveMarker.EntryName);
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
            log.LogError("SPECTATOR_SAVE_RESTORE_FAILED: APIShared rejected the saved CPU view.");
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
                if (occupiedSlots[player] && first == 0) first = player;
            }
            if (first == 0) return false;
            int initialView = requestedView >= 1 && requestedView <= 8 && occupiedSlots[requestedView]
                ? requestedView : first;
            if (!PlayerPerspectiveAPI.TrySetSpectatorView(initialView))
            {
                log.LogError("SPECTATOR_PERSPECTIVE_SELECT_FAILED: APIShared rejected the initial spectator view.");
                return false;
            }
            selectedPlayer = initialView;
            return true;
        }

        internal static bool IsSpectatorActionRestricted() => featureReady &&
            (recoveryIdentityApplied || IsActiveSpectator() || IsOriginalSpectator());

        private static bool IsOriginalSpectator()
        {
            var state = GameData.Instance?.lastGameState;
            return mapReady && state != null && state.game_type == 3 && state.spectatorMode != 0 &&
                   EditorDirector.instance != null && EditorDirector.instance.ActivePlayerID <= 0;
        }

        internal static bool IsActiveSpectator()
        {
            var state = GameData.Instance?.lastGameState;
            return featureReady && sessionEnabled && mapReady && spectatorActive && selectedPlayer > 0 && state != null &&
                   state.game_type == 3 && state.spectatorMode != 0 &&
                   EditorDirector.instance != null && EditorDirector.instance.ActivePlayerID <= 0;
        }

        internal static int GetAllyViewPlayerId(GameData data)
        {
            return IsActiveSpectator() ? selectedPlayer : data.playerID;
        }

        internal static bool IsNetworkSpectator()
        {
            var state = GameData.Instance?.lastGameState;
            return featureReady && state != null && state.game_type == 3 && state.spectatorMode != 0 &&
                   GameModeHelper.IsRealMultiplayer();
        }

        internal static bool CanInteractWithAllies()
        {
            var state = GameData.Instance?.lastGameState;
            return IsActiveSpectator() && !GameModeHelper.IsRealMultiplayer() && state != null &&
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
            if (GameData.Instance?.app_mode == 16 && GameData.Instance.app_sub_mode == 71 && MainViewModel.Instance != null)
                MainViewModel.Instance.PlayerNameText = selectedName;
        }

        private static void WaitForFreshReport(EngineInterface.PlayState oldState)
        {
            if (GameData.Instance?.app_mode != 16) return;
            var panel = MainViewModel.Instance?.HUDBuildingPanel?.RefBuildingPanel;
            if (panel == null) return;
            if (hiddenReportPanel != null && !ReferenceEquals(hiddenReportPanel, panel)) RestoreReportPanel();
            reportStateBeforeSwitch = oldState;
            pendingSince = Time.realtimeSinceStartup;
            if (hiddenReportPanel == null)
            {
                hiddenReportPanel = panel;
                reportPanelVisibility = panel.Visibility;
            }
            panel.Visibility = Visibility.Hidden;
            ArmRenderIfNeeded();
        }

        private static void RefreshPendingReport(EngineInterface.PlayState state)
        {
            if (reportStateBeforeSwitch == null) return;
            if (!mapReady || GameData.Instance?.app_mode != 16)
            {
                RestoreReportPanel();
                return;
            }
            if (ReferenceEquals(state, reportStateBeforeSwitch)) return;
            RestoreReportPanel();
        }

        private static void RestoreReportPanel()
        {
            if (hiddenReportPanel != null) hiddenReportPanel.Visibility = reportPanelVisibility;
            hiddenReportPanel = null;
            reportStateBeforeSwitch = null;
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
                alliesPanelVisibility = panel.Visibility;
            }
            panel.Visibility = Visibility.Hidden;
            ArmRenderIfNeeded();
        }

        private static void RefreshPendingAllies(EngineInterface.PlayState state)
        {
            if (alliesStateBeforeSwitch == null) return;
            if (!mapReady || MainViewModel.Instance?.AlliesPanelVisible != true)
            {
                RestoreAllyPanel();
                return;
            }
            if (ReferenceEquals(state, alliesStateBeforeSwitch)) return;
            try { SpectatorAllyHooks.RefreshOpenPanel(); }
            finally { RestoreAllyPanel(); }
        }

        private static void ClosePendingAllies()
        {
            if (alliesStateBeforeSwitch != null && MainViewModel.Instance != null)
                MainViewModel.Instance.AlliesPanelVisible = false;
            RestoreAllyPanel();
        }

        private static void RestoreAllyPanel()
        {
            if (hiddenAlliesPanel != null) hiddenAlliesPanel.Visibility = alliesPanelVisibility;
            hiddenAlliesPanel = null;
            alliesStateBeforeSwitch = null;
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

        private static void OnHudUnavailable()
        {
            RestoreFoodControls();
            RestoreReportPanel();
            RestoreAllyPanel();
            if (!mapReady || !spectatorActive) return;
            hudPending = true;
            ArmRenderIfNeeded();
        }

        private static void OnHudAvailable()
        {
            if (!mapReady || !spectatorActive) return;
            hudPending = true;
            ArmRenderIfNeeded();
        }

        private static void OnBriefingChanged(bool visible)
        {
            if (!mapReady || !spectatorActive) return;
            if (visible)
            {
                hud.Hide();
                hudPending = false;
                StopRenderIfIdle();
            }
            else
            {
                hudPending = true;
                ArmRenderIfNeeded();
            }
        }

        private static void SelectPlayer(int player)
        {
            if (!spectatorActive || player < 1 || player > 8 || occupiedSlots == null || !occupiedSlots[player]) return;
            var state = GameData.Instance?.lastGameState;
            if (state == null || state.spectatorMode == 0 || state.game_type != 3 ||
                EditorDirector.instance == null || EditorDirector.instance.ActivePlayerID > 0 ||
                !state.is_human_or_skirmish_player(player) || selectedPlayer == player) return;
            try
            {
                if (!PlayerPerspectiveAPI.TrySetSpectatorView(player)) return;
            }
            catch (Exception error)
            {
                log.LogError($"SPECTATOR_PERSPECTIVE_SELECT_FAILED: player={player}, error={error}.");
                return;
            }
            selectedPlayer = player;
            CacheSelectedName();
            hud.SetSelected(player);
            WaitForFreshReport(state);
            WaitForFreshAllies(state);
        }

        private static unsafe void JumpToPlayer(int player, MouseButton button)
        {
            if (player < 1 || player > 8 || !IsActiveSpectator() ||
                occupiedSlots == null || !occupiedSlots[player] ||
                !GameData.Instance.lastGameState.is_human_or_skirmish_player(player)) return;

            try
            {
                GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
                if (players == null) return;

                if (button == MouseButton.Right)
                {
                    GameBuildingManagerAPI buildings = GameBuildingManagerAPI.Instance;
                    if (buildings == null) return;
                    int keepId = players.GetPlayerKeepId(player);
                    if (keepId <= 0 || !buildings.TryGetBuildingById(keepId, out GameBuilding* keep) ||
                        keep == null || keep->r_AliveState != AliveState.IsAlive ||
                        keep->r_PlayerIdOwner != player || keep->r_GlobalId == 0 ||
                        keep->r_BuildingType < eStructs.STRUCT_KEEP_ONE ||
                        keep->r_BuildingType > eStructs.STRUCT_KEEP_FIVE) return;
                    players.SetScreenCenterToBuilding(keepId);
                }
                else if (button == MouseButton.Middle)
                {
                    GameUnitManagerAPI units = GameUnitManagerAPI.Instance;
                    if (units == null) return;
                    int lordId = players.GetLordUnitId(player);
                    int lordGlobalId = players.GetLordUnitGlobalId(player);
                    if (lordId <= 0 || lordGlobalId == 0 ||
                        !APIShared.UnitAccess.TryGetById(units, lordId, out GameUnit* lord, out _) || lord == null ||
                        !APIShared.UnitAccess.IsReallyAlive(lord) ||
                        lord->r_ControllableForPlayerId != player ||
                        lord->r_UnitChimp != eChimps.CHIMP_TYPE_LORD ||
                        lord->r_CurrentHealth == 0 || lord->r_GlobalId != unchecked((uint)lordGlobalId)) return;
                    players.SetScreenCenterToUnit(lordId);
                }
            }
            catch (Exception error)
            {
                if (cameraFailureLogged) return;
                cameraFailureLogged = true;
                log.LogError($"SPECTATOR_PERSPECTIVE_CAMERA_FAILED: player={player}, button={button}, error={error}.");
            }
        }

    }
}
