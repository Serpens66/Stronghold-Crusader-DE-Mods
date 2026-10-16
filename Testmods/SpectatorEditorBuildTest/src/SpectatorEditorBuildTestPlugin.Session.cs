using APIShared.GameModes;
using APIShared;
using BepInEx;
using BepInEx.Logging;
using CrusaderDE;
using HarmonyLib;
using Iced.Intel;
using Noesis;
using R3;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Detours;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Player;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using Path = System.IO.Path;
using System.Reflection;
using System.Runtime.InteropServices;
using Marshal = System.Runtime.InteropServices.Marshal;
using System.Security.Cryptography;
using System.Threading;

namespace SpectatorEditorBuildTest
{
    public sealed partial class SpectatorEditorBuildTestPlugin
    {
        private static long announcedSessionId;
        private static long CurrentSessionId => Interlocked.Read(ref activeSessionId);

        private static bool IsCurrentSession(long sessionId) =>
            sessionId > 0 && sessionId == CurrentSessionId && sessionId == Interlocked.Read(ref announcedSessionId);

        private static void OnMissionStart(MissionLifecycleNotification notification)
        {
            long sessionId = notification?.Context?.SessionId ?? 0;
            if (sessionId <= 0) return;
            Interlocked.Exchange(ref announcedSessionId, sessionId);
            if (!log.IsMainThread) { presentationCallbacks.Enqueue(() => BeginPresentedSession(sessionId)); return; }
            BeginPresentedSession(sessionId);
        }

        private static void BeginPresentedSession(long sessionId)
        {
            if (Interlocked.Read(ref announcedSessionId) != sessionId) return;
            if (sessionId <= 0 || sessionId == CurrentSessionId) return;
            ResetSessionState();
            Interlocked.Exchange(ref activeSessionId, sessionId);
            actionLedger.Reset(sessionId);
            log?.LogInfo("SPECTATOR_EDITOR_SESSION_START id=" + sessionId);
        }

        private static void OnMissionEnd(MissionLifecycleNotification notification)
        {
            long sessionId = notification?.Context?.SessionId ?? 0;
            if (sessionId <= 0) return;
            // Invalidate gameplay immediately on the publishing thread, before deferred UI cleanup.
            Interlocked.CompareExchange(ref announcedSessionId, 0, sessionId);
            Interlocked.CompareExchange(ref activeSessionId, 0, sessionId);
            if (!log.IsMainThread) { presentationCallbacks.Enqueue(() => EndPresentedSession(sessionId)); return; }
            EndPresentedSession(sessionId);
        }

        private static void EndPresentedSession(long sessionId)
        {
            if (CurrentSessionId != 0 || Interlocked.Read(ref announcedSessionId) != 0) return;
            ResetSessionState();
            log?.LogInfo("SPECTATOR_EDITOR_SESSION_END id=" + sessionId);
        }

        private static void ResetSessionState()
        {
            ResetTestActions(0);
            Interlocked.Exchange(ref revealHiddenActive, 0);
            DetachHumanControls();
            humanPlacementMode = false;
            humanSelectedOwner = -1;
            humanSavedScreen = 1;
            humanSavedSubMode = 0;
            humanSavedTabs = null;
            humanCameraFailureLogged = false;
            activeWall = null;
            pendingWall = null;
            activeSpecial = null;
            lock (moatLock) pendingMoat.Clear();
            lock (pitchLock) pendingPitch.Clear();
            pitchClickScope = null;
            wallPreviewDiagnostics = 0;
            wallInputDiagnostics = 0;
            wallPressDiagnostics = wallReleaseDiagnostics = wallQueueDiagnostics = 0;
            wallOrderDiagnostics = 0;
            wallCostDiagnostics = 0;
            specialInputDiagnostics = pitchCostDiagnostics = 0;
            moatInputDiagnostics = moatOutcomeDiagnostics = deleteDiagnostics = 0;
            pendingBuilding = null;
            buildingClickScope = null;
            buildingWall = null;
            placingUnitOwner = 0;
            rawMouseReleasePending = false;
            RestoreDateLayout();
            activeHud = null;
            initialLayoutLoggedHud = null;
            lastOwner = int.MinValue;
            lastScreen = int.MinValue;
            pendingVisibilityHud = null;
            pendingVisibilityAt = DateTime.MaxValue;
        }

        private static bool IsSpectatorSession()
        {
            try
            {
                if (!MainViewModel.viewModelLoaded) return false;
                var state = GameData.Instance?.lastGameState;
                var director = Director.instance;
                var editor = EditorDirector.instance;
                var view = MainViewModel.Instance;
                return CurrentSessionId > 0 && state != null && director != null &&
                    editor != null && view != null &&
                    state.game_type == 3 && state.spectatorMode != 0 &&
                    editor.ActivePlayerID <= 0 && !view.IsMapEditorMode &&
                    director.SkirmishModeGame && !director.MultiplayerGame &&
                    !APIShared.GameModes.GameModeHelper.IsRealMultiplayer();
            }
            catch { return false; }
        }

        private static bool IsHumanSingleplayerSession() => IsToolsSession();

        private static bool IsPlacementSession() => humanPlacementMode && IsToolsSession();

        private static bool IsSelectableAi(int owner) => IsToolsPlayer(owner, true);

        private static int CurrentAiOwner() => IsPlacementSession() && IsSelectableAi(humanSelectedOwner)
            ? humanSelectedOwner : -1;

    }
}
