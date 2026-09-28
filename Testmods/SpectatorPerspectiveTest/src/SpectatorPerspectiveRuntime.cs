using System;
using APIShared;
using BepInEx.Logging;
using CrusaderDE;
using Noesis;
using R3;
using Shared;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using UnityEngine;

namespace SpectatorPerspectiveTest
{
    internal static class SpectatorPerspectiveRuntime
    {
        private static ManualLogSource log;
        private static SpectatorPerspectiveHud hud;
        private static IDisposable postLoadSubscription;
        private static IDisposable startSubscription;
        private static IDisposable unloadSubscription;
        private static bool initialized;
        private static bool mapReady;
        private static bool loadedFromSave;
        private static bool spectatorActive;
        private static bool initializationPending;
        private static bool hudPending;
        private static bool resetHudPending;
        private static bool renderSubscribed;
        private static bool readyLogged;
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
        private static Grid hiddenReportPanel;
        private static Visibility reportPanelVisibility;
        private static HUD_AlliesPanel hiddenAlliesPanel;
        private static Visibility alliesPanelVisibility;
        private static Grid guardedFoodPanel;
        private static readonly Button[] guardedFoodButtons = new Button[4];
        private static readonly bool[] foodButtonHitTestBeforeGuard = new bool[4];
        private static readonly string[] foodButtonNames = { "EatingMeat", "EatingCheese", "EatingBread", "EatingApples" };

        internal static void Initialize(ManualLogSource logger)
        {
            if (initialized) return;
            initialized = true;
            log = logger;
            hud = new SpectatorPerspectiveHud(SelectPlayer, JumpToPlayer, OnHudUnavailable,
                OnHudAvailable, OnBriefingChanged);
            try
            {
                SpectatorReportHooks.Install();
            }
            catch (Exception error) { log.LogError("SPECTATOR_REPORT_HOOKS_FAILED: " + error); }
            try
            {
                SpectatorAllyHooks.Install();
            }
            catch (Exception error) { log.LogError("SPECTATOR_ALLY_HOOKS_FAILED: " + error); }
            postLoadSubscription = MapLoaderR3EventHooks.OnPostLoad.Observable.Subscribe(OnPostLoad);
            startSubscription = MapLoaderR3EventHooks.OnStartMap.Observable.Subscribe(OnStartMap);
            unloadSubscription = MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(OnUnloadMap);
        }

        private static void OnStartMap(MapStartEventArgs args)
        {
            if (args.Phase != EventHookPhase.Pre) return;
            var previousState = GameData.Instance?.lastGameState;
            BeginMapLoad();
            stateBeforeLoad = previousState;
        }

        private static void OnPostLoad(MapPostLoadEventArgs args)
        {
            if (args.Phase != EventHookPhase.Post) return;
            var previousState = stateBeforeLoad;
            BeginMapLoad();
            stateBeforeLoad = previousState;
            mapReady = true;
            loadedFromSave = args.FromSaveGame;
            initializationPending = GameData.Instance?.game_type == 3;
            pendingSince = Time.realtimeSinceStartup;
            ArmRenderIfNeeded();
        }

        private static void OnUnloadMap(MapUnloadEventArgs args)
        {
            if (args.Phase != EventHookPhase.Pre) return;
            BeginMapLoad();
        }

        private static void BeginMapLoad()
        {
            PlayerPerspectiveAPI.ClearSpectatorView();
            RestoreReportPanel();
            RestoreAllyPanel();
            RestoreFoodControls();
            SpectatorAllyHooks.RestoreControls();
            mapReady = false;
            loadedFromSave = false;
            spectatorActive = false;
            initializationPending = false;
            hudPending = false;
            selectedPlayer = 0;
            selectedName = null;
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
                        if (!readyLogged)
                        {
                            readyLogged = true;
                            log.LogInfo("SPECTATOR_PERSPECTIVE_RUNTIME_ALIVE: temporary render readiness callback executed after startup cleanup.");
                        }
                        if (state.game_type != 3 || state.spectatorMode == 0)
                            initializationPending = false;
                        else if (EditorDirector.instance != null)
                        {
                            bool originalSpectatorSave = loadedFromSave && IsOriginalSpectatorSave(state);
                            if (EditorDirector.instance.ActivePlayerID <= 0 || originalSpectatorSave)
                            {
                                // Vanilla restores a saved spectator's native view as a positive local player.
                                // Restore only the managed control identity; APIShared owns the native view.
                                if (originalSpectatorSave && EditorDirector.instance.ActivePlayerID > 0)
                                    EditorDirector.instance.SetLocalPlayer(-1);
                                InitializePerspective(state, originalSpectatorSave ?
                                    PlayerPerspectiveAPI.GetRawNativeViewPlayerId() : 0);
                                initializationPending = false;
                            }
                        }
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
                    string pending = initializationPending ? "PlayState" : hudPending ? "IngameUI" :
                        reportStateBeforeSwitch != null ? "report PlayState" : "allies PlayState";
                    log.LogWarning("SPECTATOR_PERSPECTIVE_READY_TIMEOUT: " + pending +
                        " did not become ready within 15 seconds.");
                    initializationPending = false;
                    hudPending = false;
                    RestoreReportPanel();
                    ClosePendingAllies();
                }
                StopRenderIfIdle();
            }
            catch (Exception error)
            {
                PlayerPerspectiveAPI.ClearSpectatorView();
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

        private static bool IsOriginalSpectatorSave(EngineInterface.PlayState state)
        {
            if (state == null || state.game_type != (int)eGameTypeModes.GAMETYPE_MULTIPLAYER ||
                state.spectatorMode == 0 || state.player_register == null ||
                state.player_register.Length < 9 || state.computer_register == null ||
                state.computer_register.Length < 9 ||
                GameModeHelper.IsRealMultiplayer()) return false;
            bool hasCpu = false;
            for (int player = 1; player <= 8; player++)
            {
                if (state.is_valid_player(player)) return false;
                hasCpu |= state.is_skirmish_player(player);
            }
            return hasCpu;
        }

        private static void InitializePerspective(EngineInterface.PlayState state, int savedView)
        {
            if (!mapReady) return;
            occupiedSlots = new bool[9];
            int first = 0;
            for (int player = 1; player <= 8; player++)
            {
                occupiedSlots[player] = state.is_human_or_skirmish_player(player);
                if (occupiedSlots[player] && first == 0) first = player;
            }
            if (first == 0) return;
            int initialView = savedView >= 1 && savedView <= 8 && occupiedSlots[savedView]
                ? savedView : first;
            if (!PlayerPerspectiveAPI.TrySetSpectatorView(initialView))
            {
                log.LogError("SPECTATOR_PERSPECTIVE_SELECT_FAILED: APIShared rejected the initial spectator view.");
                return;
            }
            selectedPlayer = initialView;
            spectatorActive = true;
            hudPending = true;
        }

        internal static bool IsActiveSpectator()
        {
            var state = GameData.Instance?.lastGameState;
            return mapReady && spectatorActive && selectedPlayer > 0 && state != null &&
                   state.game_type == 3 && state.spectatorMode != 0 &&
                   EditorDirector.instance != null && EditorDirector.instance.ActivePlayerID <= 0;
        }

        internal static int GetAllyViewPlayerId(GameData data)
        {
            return IsActiveSpectator() ? selectedPlayer : data.playerID;
        }

        // The future host setting replaces this one constant decision.
        private static bool AllyInteractionAllowed => true;

        internal static bool IsNetworkSpectator()
        {
            var state = GameData.Instance?.lastGameState;
            return state != null && state.game_type == 3 && state.spectatorMode != 0 &&
                   GameModeHelper.IsRealMultiplayer();
        }

        internal static bool CanInteractWithAllies()
        {
            var state = GameData.Instance?.lastGameState;
            return IsActiveSpectator() && AllyInteractionAllowed && !GameModeHelper.IsRealMultiplayer() && state != null &&
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
            return mapReady && spectatorActive && name != null && state != null &&
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
                        !units.TryGetUnitById(lordId, out GameUnit* lord) || lord == null ||
                        lord->r_AliveState != AliveState.IsAlive ||
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
