using System;
using BepInEx.Logging;
using CrusaderDE;
using R3;
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

        internal static void Initialize(ManualLogSource logger)
        {
            if (initialized) return;
            initialized = true;
            log = logger;
            hud = new SpectatorPerspectiveHud(SelectPlayer);
            postLoadSubscription = MapLoaderR3EventHooks.OnPostLoad.Observable.Subscribe(OnPostLoad);
            startSubscription = MapLoaderR3EventHooks.OnStartMap.Observable.Subscribe(OnStartMap);
            unloadSubscription = MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(OnUnloadMap);
            Application.onBeforeRender += OnBeforeRender;
            log.LogInfo("SPECTATOR_PERSPECTIVE_INITIALIZED: static publisher roots installed; waiting for gameplay render after startup cleanup.");
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
            if (!readyLogged)
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
                hud.Hide();
                return;
            }
            if (!spectatorActive)
            {
                selectedPlayer = first;
                spectatorActive = true;
                EngineInterface.SetEditorPlayer(selectedPlayer);
                log.LogInfo($"SPECTATOR_PERSPECTIVE_ACTIVE: selected={selectedPlayer}, occupied={OccupiedList(occupied)}.");
            }
            else if (!occupied[selectedPlayer])
            {
                selectedPlayer = first;
                EngineInterface.SetEditorPlayer(selectedPlayer);
                log.LogInfo($"SPECTATOR_PERSPECTIVE_SLOT_CHANGED: selected={selectedPlayer}.");
            }
            hud.Show(occupied, selectedPlayer);
        }

        private static void SelectPlayer(int player)
        {
            if (!spectatorActive || player < 1 || player > 8) return;
            var state = GameData.Instance?.lastGameState;
            if (state == null || state.spectatorMode == 0 || state.game_type != 3 ||
                EditorDirector.instance == null || EditorDirector.instance.ActivePlayerID > 0 ||
                !state.is_human_or_skirmish_player(player)) return;
            if (selectedPlayer == player) return;
            selectedPlayer = player;
            EngineInterface.SetEditorPlayer(player);
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
