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
        private static bool spectatorActive;
        private static bool initializationPending;
        private static bool hudPending;
        private static bool resetHudPending;
        private static bool renderSubscribed;
        private static bool readyLogged;
        private static bool failureLogged;
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
            hud = new SpectatorPerspectiveHud(SelectPlayer, OnHudUnavailable);
            try
            {
                SpectatorReportHooks.Install();
                log.LogInfo("SPECTATOR_REPORT_HOOKS_READY: Vanilla report navigation, food guard and stable report name installed.");
            }
            catch (Exception error) { log.LogError("SPECTATOR_REPORT_HOOKS_FAILED: " + error); }
            try
            {
                SpectatorAllyHooks.Install();
                log.LogInfo("SPECTATOR_ALLY_HOOKS_READY: selected-player ally view and CPU-only action guard installed.");
            }
            catch (Exception error) { log.LogError("SPECTATOR_ALLY_HOOKS_FAILED: " + error); }
            postLoadSubscription = MapLoaderR3EventHooks.OnPostLoad.Observable.Subscribe(OnPostLoad);
            startSubscription = MapLoaderR3EventHooks.OnStartMap.Observable.Subscribe(OnStartMap);
            unloadSubscription = MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(OnUnloadMap);
            log.LogInfo("SPECTATOR_PERSPECTIVE_INITIALIZED: static map publishers installed; render callback is armed only while a session needs setup.");
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
            initializationPending = GameData.Instance?.game_type == 3;
            pendingSince = Time.realtimeSinceStartup;
            ArmRenderIfNeeded();
        }

        private static void OnUnloadMap(MapUnloadEventArgs args)
        {
            if (args.Phase != EventHookPhase.Pre) return;
            BeginMapLoad();
            log.LogInfo("SPECTATOR_PERSPECTIVE_MAP_UNLOAD: perspective and HUD position reset.");
        }

        private static void BeginMapLoad()
        {
            RestoreReportPanel();
            RestoreAllyPanel();
            RestoreFoodControls();
            SpectatorAllyHooks.RestoreControls();
            mapReady = false;
            spectatorActive = false;
            initializationPending = false;
            hudPending = false;
            selectedPlayer = 0;
            selectedName = null;
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
                    if (state != null && !ReferenceEquals(state, stateBeforeLoad))
                    {
                        if (!readyLogged)
                        {
                            readyLogged = true;
                            log.LogInfo("SPECTATOR_PERSPECTIVE_RUNTIME_ALIVE: temporary render readiness callback executed after startup cleanup.");
                        }
                        if (state.game_type != 3 || state.spectatorMode == 0)
                            initializationPending = false;
                        else if (EditorDirector.instance != null && EditorDirector.instance.ActivePlayerID <= 0)
                        {
                            InitializePerspective(state);
                            initializationPending = false;
                        }
                        if (!initializationPending) stateBeforeLoad = null;
                    }
                }
                if (hudPending && spectatorActive && hud.TryShow(occupiedSlots, selectedPlayer))
                {
                    hudPending = false;
                    if (selectedName == null) CacheSelectedName();
                    GuardFoodControls();
                }
                RefreshPendingReport(GameData.Instance?.lastGameState);
                RefreshPendingAllies(GameData.Instance?.lastGameState);
                if ((initializationPending || hudPending || reportStateBeforeSwitch != null || alliesStateBeforeSwitch != null) &&
                    Time.realtimeSinceStartup - pendingSince > 15f)
                {
                    log.LogWarning("SPECTATOR_PERSPECTIVE_READY_TIMEOUT: pending UI or PlayState did not become ready within 15 seconds.");
                    initializationPending = false;
                    hudPending = false;
                    RestoreReportPanel();
                    ClosePendingAllies();
                }
                StopRenderIfIdle();
            }
            catch (Exception error)
            {
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

        private static void InitializePerspective(EngineInterface.PlayState state)
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
            EngineInterface.SetEditorPlayer(first);
            selectedPlayer = first;
            spectatorActive = true;
            hudPending = true;
            log.LogInfo($"SPECTATOR_PERSPECTIVE_ACTIVE: selected={selectedPlayer}, occupied={OccupiedList(occupiedSlots)}.");
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

        internal static bool CanInteractWithAllies()
        {
            var state = GameData.Instance?.lastGameState;
            return IsActiveSpectator() && AllyInteractionAllowed && state != null &&
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

        internal static void LogAllyAction(Enums.GameActionCommand command, int target, bool allowed)
        {
            log.LogInfo($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] SPECTATOR_ALLY_ACTION: viewer={selectedPlayer}, command={command}, target={target}, allowed={allowed}.");
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

        private static void SelectPlayer(int player)
        {
            if (!spectatorActive || player < 1 || player > 8 || occupiedSlots == null || !occupiedSlots[player]) return;
            var state = GameData.Instance?.lastGameState;
            if (state == null || state.spectatorMode == 0 || state.game_type != 3 ||
                EditorDirector.instance == null || EditorDirector.instance.ActivePlayerID > 0 ||
                !state.is_human_or_skirmish_player(player) || selectedPlayer == player) return;
            try { EngineInterface.SetEditorPlayer(player); }
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
            log.LogInfo($"SPECTATOR_PERSPECTIVE_SELECTED: player={player}; future Vanilla reports and messages use this index.");
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
