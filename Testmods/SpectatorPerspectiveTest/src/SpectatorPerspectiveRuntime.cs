using System;
using BepInEx.Logging;
using CrusaderDE;
using Noesis;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
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
        private static bool resetHudPending;
        private static bool spectatorActive;
        private static bool awaitingFreshState;
        private static EngineInterface.PlayState stateBeforeLoad;
        private static int selectedPlayer;
        private static int lastFrame = -1;
        private static bool readyLogged;
        private static bool failureLogged;
        private static EngineInterface.PlayState reportStateBeforeSwitch;
        private static int freshReportFrame = -1;
        private static Grid hiddenReportPanel;
        private static Visibility reportPanelVisibility;
        private static Grid guardedFoodPanel;
        private static readonly Button[] guardedFoodButtons = new Button[4];
        private static readonly bool[] foodButtonEnabledBeforeGuard = new bool[4];
        private static readonly string[] foodButtonNames = { "EatingMeat", "EatingCheese", "EatingBread", "EatingApples" };

        internal static void Initialize(ManualLogSource logger)
        {
            if (initialized) return;
            initialized = true;
            log = logger;
            hud = new SpectatorPerspectiveHud(SelectPlayer);
            try
            {
                SpectatorReportHooks.Install();
                log.LogInfo("SPECTATOR_REPORT_HOOKS_READY: Vanilla report navigation and food action guard installed.");
            }
            catch (Exception error) { log.LogError("SPECTATOR_REPORT_HOOKS_FAILED: " + error); }
            postLoadSubscription = MapLoaderR3EventHooks.OnPostLoad.Observable.Subscribe(OnPostLoad);
            startSubscription = MapLoaderR3EventHooks.OnStartMap.Observable.Subscribe(OnStartMap);
            unloadSubscription = MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(OnUnloadMap);
            Application.onBeforeRender += OnBeforeRender;
            log.LogInfo("SPECTATOR_PERSPECTIVE_INITIALIZED: static publisher roots installed; waiting for post-load gameplay render.");
        }

        private static void OnStartMap(MapStartEventArgs args)
        {
            if (args.Phase == EventHookPhase.Pre) BeginMapLoad();
        }

        private static void OnPostLoad(MapPostLoadEventArgs args)
        {
            if (args.Phase != EventHookPhase.Post) return;
            BeginMapLoad();
            stateBeforeLoad = GameData.Instance?.lastGameState;
            mapReady = true;
        }

        private static void OnUnloadMap(MapUnloadEventArgs args)
        {
            if (args.Phase != EventHookPhase.Pre) return;
            RestoreReportPanel();
            RestoreFoodControls();
            mapReady = false;
            spectatorActive = false;
            awaitingFreshState = true;
            selectedPlayer = 0;
            stateBeforeLoad = null;
            resetHudPending = true;
            log.LogInfo("SPECTATOR_PERSPECTIVE_MAP_UNLOAD: perspective and HUD position reset.");
        }

        private static void BeginMapLoad()
        {
            RestoreReportPanel();
            RestoreFoodControls();
            mapReady = false;
            spectatorActive = false;
            awaitingFreshState = true;
            selectedPlayer = 0;
            stateBeforeLoad = null;
            resetHudPending = true;
        }

        private static void OnBeforeRender()
        {
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            if (!readyLogged && mapReady && GameData.Instance?.lastGameState != null)
            {
                readyLogged = true;
                log.LogInfo("SPECTATOR_PERSPECTIVE_RUNTIME_ALIVE: application render publisher executed after startup initialization.");
            }
            try
            {
                if (resetHudPending)
                {
                    resetHudPending = false;
                    hud.ResetForSession();
                }
                Refresh();
            }
            catch (Exception error)
            {
                spectatorActive = false;
                RestoreReportPanel();
                RestoreFoodControls();
                hud.Hide();
                if (failureLogged) return;
                failureLogged = true;
                log.LogError("SPECTATOR_PERSPECTIVE_ERROR: " + error);
            }
        }

        private static void Refresh()
        {
            var gameData = GameData.Instance;
            var director = EditorDirector.instance;
            var state = gameData?.lastGameState;
            if (!mapReady || state == null || director == null || state.game_type != 3 || state.spectatorMode == 0 || director.ActivePlayerID > 0)
            {
                spectatorActive = false;
                RestoreReportPanel();
                RestoreFoodControls();
                hud.Hide();
                return;
            }
            if (awaitingFreshState)
            {
                if (ReferenceEquals(state, stateBeforeLoad)) { hud.Hide(); return; }
                awaitingFreshState = false;
                stateBeforeLoad = null;
            }
            bool[] occupied = new bool[9];
            int first = 0;
            for (int player = 1; player <= 8; player++)
            {
                occupied[player] = state.is_human_or_skirmish_player(player);
                if (occupied[player] && first == 0) first = player;
            }
            if (first == 0)
            {
                spectatorActive = false;
                RestoreReportPanel();
                RestoreFoodControls();
                hud.Hide();
                return;
            }
            if (!spectatorActive)
            {
                selectedPlayer = first;
                spectatorActive = true;
                EngineInterface.SetEditorPlayer(selectedPlayer);
                WaitForFreshReport(state);
                log.LogInfo($"SPECTATOR_PERSPECTIVE_ACTIVE: selected={selectedPlayer}, occupied={OccupiedList(occupied)}.");
            }
            else if (!occupied[selectedPlayer])
            {
                selectedPlayer = first;
                EngineInterface.SetEditorPlayer(selectedPlayer);
                WaitForFreshReport(state);
                log.LogInfo($"SPECTATOR_PERSPECTIVE_SLOT_CHANGED: selected={selectedPlayer}.");
            }
            hud.Show(occupied, selectedPlayer);
            RefreshReport(state);
        }

        internal static bool IsActiveSpectator()
        {
            var state = GameData.Instance?.lastGameState;
            return mapReady && spectatorActive && selectedPlayer > 0 && state != null &&
                   state.game_type == 3 && state.spectatorMode != 0 &&
                   EditorDirector.instance != null && EditorDirector.instance.ActivePlayerID <= 0;
        }

        private static void WaitForFreshReport(EngineInterface.PlayState oldState)
        {
            if (hiddenReportPanel != null && !ReferenceEquals(hiddenReportPanel, MainViewModel.Instance?.HUDBuildingPanel?.RefBuildingPanel))
                RestoreReportPanel();
            reportStateBeforeSwitch = oldState;
            freshReportFrame = -1;
            var viewModel = MainViewModel.Instance;
            if (GameData.Instance?.app_mode != 16 || viewModel?.HUDBuildingPanel?.RefBuildingPanel == null) return;
            var panel = viewModel.HUDBuildingPanel.RefBuildingPanel;
            if (!ReferenceEquals(panel, hiddenReportPanel))
            {
                hiddenReportPanel = panel;
                reportPanelVisibility = panel.Visibility;
            }
            panel.Visibility = Visibility.Hidden;
        }

        private static void RestoreReportPanel()
        {
            if (hiddenReportPanel != null) hiddenReportPanel.Visibility = reportPanelVisibility;
            hiddenReportPanel = null;
            reportStateBeforeSwitch = null;
            freshReportFrame = -1;
        }

        private static void RefreshReport(EngineInterface.PlayState state)
        {
            if (reportStateBeforeSwitch != null)
            {
                if (GameData.Instance.app_mode != 16)
                    RestoreReportPanel();
                else if (!ReferenceEquals(state, reportStateBeforeSwitch))
                {
                    if (freshReportFrame < 0) freshReportFrame = Time.frameCount;
                    if (Time.frameCount > freshReportFrame) RestoreReportPanel();
                    else if (hiddenReportPanel != null) hiddenReportPanel.Visibility = Visibility.Hidden;
                }
                else if (hiddenReportPanel != null) hiddenReportPanel.Visibility = Visibility.Hidden;
            }

            if (GameData.Instance.app_mode != 16)
            {
                RestoreFoodControls();
                return;
            }
            var viewModel = MainViewModel.Instance;
            if (viewModel == null) return;
            if (GameData.Instance.app_sub_mode == 71 && reportStateBeforeSwitch == null)
            {
                string name = Platform_Multiplayer.Instance?.getSkirmishName(selectedPlayer);
                viewModel.PlayerNameText = string.IsNullOrWhiteSpace(name) ? "-" : name;
            }
            var foodPanel = viewModel.HUDBuildingPanel?.RefReportsFoodPanel;
            if (foodPanel == null) return;
            if (ReferenceEquals(foodPanel, guardedFoodPanel)) return;
            RestoreFoodControls();
            guardedFoodPanel = foodPanel;
            for (int index = 0; index < foodButtonNames.Length; index++)
            {
                var button = GameXAMLManagerAPI.Instance?.FindElementByName(foodPanel, foodButtonNames[index]) as Button;
                if (button == null) continue;
                guardedFoodButtons[index] = button;
                foodButtonEnabledBeforeGuard[index] = button.IsEnabled;
                button.IsEnabled = false;
            }
        }

        private static void RestoreFoodControls()
        {
            for (int index = 0; index < guardedFoodButtons.Length; index++)
            {
                if (guardedFoodButtons[index] != null)
                    guardedFoodButtons[index].IsEnabled = foodButtonEnabledBeforeGuard[index];
                guardedFoodButtons[index] = null;
            }
            guardedFoodPanel = null;
        }

        private static void SelectPlayer(int player)
        {
            if (!spectatorActive || player < 1 || player > 8) return;
            var state = GameData.Instance?.lastGameState;
            if (state == null || state.spectatorMode == 0 || state.game_type != 3 ||
                EditorDirector.instance == null || EditorDirector.instance.ActivePlayerID > 0 ||
                !state.is_human_or_skirmish_player(player)) return;
            if (selectedPlayer == player) return;
            try { EngineInterface.SetEditorPlayer(player); }
            catch (Exception error)
            {
                log.LogError($"SPECTATOR_PERSPECTIVE_SELECT_FAILED: player={player}, error={error}.");
                return;
            }
            selectedPlayer = player;
            WaitForFreshReport(state);
            log.LogInfo($"SPECTATOR_PERSPECTIVE_SELECTED: player={player}; future Vanilla reports and messages use this index.");
            hud.SetSelected(player);
        }

        private static string OccupiedList(bool[] occupied)
        {
            string result = "";
            for (int player = 1; player <= 8; player++)
                if (occupied[player]) result += (result.Length == 0 ? "" : ",") + player;
            return result;
        }
    }
}
