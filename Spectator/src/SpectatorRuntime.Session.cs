using APIShared.GameModes;
using APIShared;
using CrusaderDE;
using SHCDESE.Interop.Enums;
using System;
using System.Threading;

namespace Spectator
{
    internal static partial class SpectatorRuntime
    {
        private static long announcedSessionId;
        private static GameModeSnapshot? sessionMode;
        private static long CurrentSessionId => Interlocked.Read(ref activeSessionId);
        internal static bool HasMission => CurrentSessionId > 0;

        private static bool IsCurrentSession(long sessionId) =>
            sessionId > 0 && sessionId == CurrentSessionId && sessionId == Interlocked.Read(ref announcedSessionId);

        private static void OnMissionStart(MissionLifecycleNotification notification)
        {
            long sessionId = notification?.Context?.SessionId ?? 0;
            if (sessionId <= 0) return;
            Interlocked.Exchange(ref announcedSessionId, sessionId);
            var mode = notification.Context.Mode;
            if (!log.IsMainThread) { PostPresentation(() => BeginPresentedSession(sessionId, mode)); return; }
            BeginPresentedSession(sessionId, mode);
        }

        private static void BeginPresentedSession(long sessionId, GameModeSnapshot mode)
        {
            if (Interlocked.Read(ref announcedSessionId) != sessionId) return;
            if (!initialized || sessionId <= 0 || sessionId == CurrentSessionId) return;
            ResetSessionState();
            sessionMode = mode;
            Interlocked.Exchange(ref activeSessionId, sessionId);
            actionLedger.Reset(sessionId);
            log?.LogInfo("SPECTATOR_EDITOR_SESSION_START id=" + sessionId);
            RequestRender();
        }

        private static void OnMissionEnd(MissionLifecycleNotification notification)
        {
            long sessionId = notification?.Context?.SessionId ?? 0;
            if (sessionId <= 0) return;
            // Invalidate gameplay immediately on the publishing thread, before deferred UI cleanup.
            Interlocked.CompareExchange(ref announcedSessionId, 0, sessionId);
            Interlocked.CompareExchange(ref activeSessionId, 0, sessionId);
            if (!log.IsMainThread) { PostPresentation(() => EndPresentedSession(sessionId)); return; }
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
            ResetPresentationScheduling();
            ResetTestActions(0);
            ResetSpriteColourLease();
            SetRevealHidden(0);
            DetachHumanControls();
            sessionMode = null;
            humanPlacementMode = false;
            toolbarMoved = false;
            toolbarDesiredX = toolbarDesiredY = 0;
            RestorePlacementFreeze();
            ApplyHumanAi(-1);
            initialSelectionChosen = false;
            toolbarDirty = true;
            spectatorSelectionLinked = false;
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
            moatInputDiagnostics = 0;
            DiagResetSession();
            pendingBuilding = null;
            buildingClickScope = null;
            buildingWall = null;
            DiagPlacingUnit(0);
            rawMouseReleasePending = false;
            activeHud = null;
            initialLayoutLoggedHud = null;
            lastOwner = int.MinValue;
            lastScreen = int.MinValue;
            DiagForgetIcons();
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
                    state.game_type == (int)eGameTypeModes.GAMETYPE_MULTIPLAYER && state.spectatorMode != 0 &&
                    SpectatorPerspectiveRuntime.IsSpectatorObserver() && !view.IsMapEditorMode &&
                    director.SkirmishModeGame && !director.MultiplayerGame &&
                    !APIShared.GameModes.GameModeHelper.IsRealMultiplayer();
            }
            catch { return false; }
        }

        private static bool IsHumanSingleplayerSession() => IsToolsSession();

        private static bool IsPlacementSession() => humanPlacementMode && IsToolsSession();

        private static bool IsSelectableAi(int owner) => IsToolsPlayer(owner, true);

        private static int CurrentAiOwner() => IsPlacementSession() && IsPlacementPlayer(humanSelectedOwner)
            ? humanSelectedOwner : -1;

    }
}
