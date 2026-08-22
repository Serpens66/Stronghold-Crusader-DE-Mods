// Feature: Recreate a normal multiplayer lobby before the final statistics release the peers.
using BepInEx.Logging;
using CrusaderDE;
using R3;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace BugfixesAndQoL
{
    internal sealed class MultiplayerLobbyReturnFeature : IDisposable
    {
        private sealed class LobbySnapshot
        {
            internal string GameName;
            internal string MapName;
            internal string MapFileName;
            internal int MaxPlayers;
            internal string Settings;
            internal string SetTeams;
            internal int Crc;
            internal int LobbyMode;
            internal string AivDataPlayer2;
            internal string AivDataPlayer3;
            internal string AivDataPlayer4;
            internal string AivDataPlayer5;
            internal string AivDataPlayer6;
            internal string AivDataPlayer7;
            internal string AivDataPlayer8;
        }

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();

        private LobbySnapshot snapshot;
        private Platform_Multiplayer.MPLobby hostLobby;
        private Action pendingVanillaExit;
        private bool supportedSession;
        private bool gameOverObserved;
        private bool creationRequested;
        private bool creationFailed;
        private bool transitionStarted;
        private bool disposed;
        private long gameOverStartedAt;
        private int lastFrame = -1;

        internal MultiplayerLobbyReturnFeature(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        internal void Initialize()
        {
            subscriptions.Add(MapLoaderR3EventHooks.OnStartMap.Observable.Subscribe(OnMapStart));
            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(args =>
            {
                if (args.Phase == EventHookPhase.Post)
                    Reset("map-unload", clearContinuationId: false);
            }));
        }

        internal void OnGameOverState(int state)
        {
            if (!supportedSession || state <= 0 || gameOverObserved)
                return;

            gameOverObserved = true;
            gameOverStartedAt = Stopwatch.GetTimestamp();

            Platform_Multiplayer multiplayer = Platform_Multiplayer.Instance;
            bool isHost = multiplayer?.IsGameMemberHost() == true;
            if (!MultiplayerLobbyReturnPolicy.ShouldCreateLobby(
                    supportedSession,
                    state,
                    isHost,
                    creationRequested))
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Post-game lobby handoff armed for a client: host={isHost}, snapshotReady={snapshot != null}.");
                return;
            }

            creationRequested = true;
            if (snapshot == null)
            {
                creationFailed = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    "Post-game lobby creation was skipped because the original lobby metadata was not captured.");
                return;
            }

            try
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Creating post-game lobby: name='{snapshot.GameName}', map='{snapshot.MapFileName}', maxPlayers={snapshot.MaxPlayers}, lobbyMode={snapshot.LobbyMode}.");
                multiplayer.CreateLobby(
                    snapshot.GameName,
                    snapshot.MapName,
                    snapshot.MapFileName,
                    snapshot.MaxPlayers,
                    0,
                    snapshot.LobbyMode,
                    snapshot.Settings,
                    snapshot.Crc,
                    OnHostLobbyCreated,
                    (_, __, ___) => { },
                    -1,
                    clearGameMembers: false);
                ApplySnapshotToPendingLobby(multiplayer.activeLobby, snapshot);
            }
            catch (Exception ex)
            {
                creationFailed = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Post-game lobby creation failed before Steam accepted the request; Vanilla exit remains available: {ex}");
            }
        }

        internal bool TryHandleMissionOverExit(Action vanillaExit)
        {
            if (!supportedSession || !gameOverObserved || transitionStarted)
                return false;

            if (vanillaExit == null)
                throw new ArgumentNullException(nameof(vanillaExit));

            if (TryTransitionToLobby())
                return true;

            if (creationFailed || IsTimedOut())
            {
                FallbackToVanilla(vanillaExit, creationFailed ? "creation-failed" : "timeout");
                return true;
            }

            pendingVanillaExit = vanillaExit;
            Shared.DebugLogHelper.LogInfo(log, "Post-game Exit is waiting for the host's replacement lobby ID.");
            return true;
        }

        internal void OnBeforeRender()
        {
            if (disposed || pendingVanillaExit == null || lastFrame == UnityEngine.Time.frameCount)
                return;

            lastFrame = UnityEngine.Time.frameCount;
            try
            {
                if (TryTransitionToLobby())
                {
                    ClearPendingExit();
                    return;
                }

                if (creationFailed || IsTimedOut())
                {
                    Action fallback = pendingVanillaExit;
                    ClearPendingExit();
                    FallbackToVanilla(fallback, creationFailed ? "creation-failed" : "timeout");
                }
            }
            catch (Exception ex)
            {
                Action fallback = pendingVanillaExit;
                ClearPendingExit();
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Post-game lobby handoff failed while waiting; Vanilla exit is used: {ex}");
                FallbackToVanilla(fallback, "wait-error");
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();
            subscriptions.Clear();
            ClearPendingExit();
        }

        private void OnMapStart(MapStartEventArgs args)
        {
            int coopTrailId = GameData.Instance?.coopTrailID ?? 0;
            if (args.Phase == EventHookPhase.Pre)
                Reset("map-start", clearContinuationId: coopTrailId <= 0);

            if (args.Phase != EventHookPhase.Pre && args.Phase != EventHookPhase.Post)
                return;

            Shared.GameModeSnapshot mode = Shared.GameModeHelper.Capture(args.bMultiplayerSave != 0);
            supportedSession = MultiplayerLobbyReturnPolicy.IsSupportedSession(
                settings.EnableMod,
                settings.EnableReturnToMultiplayerLobby,
                mode.IsRealMultiplayer,
                coopTrailId);
            if (!supportedSession || snapshot != null)
                return;

            snapshot = CaptureLobbySnapshot();
            if (snapshot != null)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Captured multiplayer lobby for post-game return: phase={args.Phase}, name='{snapshot.GameName}', map='{snapshot.MapFileName}', maxPlayers={snapshot.MaxPlayers}, lobbyMode={snapshot.LobbyMode}, mode={mode.ToDiagnosticString()}.");
            }
            else if (args.Phase == EventHookPhase.Post)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Post-game lobby return was enabled, but no valid original lobby snapshot was available after map start: mode={mode.ToDiagnosticString()}.");
            }
        }

        private LobbySnapshot CaptureLobbySnapshot()
        {
            Platform_Multiplayer multiplayer = Platform_Multiplayer.Instance;
            FRONT_Multiplayer front = MainViewModel.viewModelLoaded
                ? MainViewModel.Instance?.FRONTMultiplayer
                : null;
            Platform_Multiplayer.MPLobby lobby = multiplayer?.activeLobby ?? front?.currentLobby;
            if (lobby == null)
                return null;

            return new LobbySnapshot
            {
                GameName = lobby.gameName ?? string.Empty,
                MapName = lobby.mapName ?? string.Empty,
                MapFileName = lobby.mapFileName ?? string.Empty,
                MaxPlayers = ParseBoundedInt(lobby.maxPlayers, 8, 2, 8),
                Settings = lobby.settings ?? string.Empty,
                SetTeams = lobby.setTeams ?? string.Empty,
                Crc = ParseBoundedInt(lobby.crc, 0, int.MinValue, int.MaxValue),
                LobbyMode = multiplayer.lastLobbyMode,
                AivDataPlayer2 = lobby.AIVDataPlayer2 ?? string.Empty,
                AivDataPlayer3 = lobby.AIVDataPlayer3 ?? string.Empty,
                AivDataPlayer4 = lobby.AIVDataPlayer4 ?? string.Empty,
                AivDataPlayer5 = lobby.AIVDataPlayer5 ?? string.Empty,
                AivDataPlayer6 = lobby.AIVDataPlayer6 ?? string.Empty,
                AivDataPlayer7 = lobby.AIVDataPlayer7 ?? string.Empty,
                AivDataPlayer8 = lobby.AIVDataPlayer8 ?? string.Empty,
            };
        }

        private static void ApplySnapshotToPendingLobby(
            Platform_Multiplayer.MPLobby lobby,
            LobbySnapshot source)
        {
            if (lobby == null || source == null)
                return;

            // CreateLobby publishes these fields asynchronously, so restore them before its
            // Steam callback serializes the replacement lobby metadata.
            lobby.setTeams = source.SetTeams;
            lobby.AIVDataPlayer2 = source.AivDataPlayer2;
            lobby.AIVDataPlayer3 = source.AivDataPlayer3;
            lobby.AIVDataPlayer4 = source.AivDataPlayer4;
            lobby.AIVDataPlayer5 = source.AivDataPlayer5;
            lobby.AIVDataPlayer6 = source.AivDataPlayer6;
            lobby.AIVDataPlayer7 = source.AivDataPlayer7;
            lobby.AIVDataPlayer8 = source.AivDataPlayer8;
        }

        private void OnHostLobbyCreated()
        {
            try
            {
                Platform_Multiplayer multiplayer = Platform_Multiplayer.Instance;
                Platform_Multiplayer.MPLobby created = multiplayer?.GetActiveLobby();
                if (created == null || !created.isHost || created.identifier == 0)
                    throw new InvalidOperationException("Steam did not expose the created host lobby.");

                hostLobby = created;
                multiplayer.CoopContinuationLobbyID = created.identifier;
                SendContinuationLobbyToConnectedPeers(multiplayer, created.identifier);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Post-game lobby created and announced to connected peers: lobbyId={created.identifier}, members={created.numLobbyMembers}.");
            }
            catch (Exception ex)
            {
                creationFailed = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Post-game lobby was created but could not be announced; Vanilla exit remains available: {ex}");
            }
        }

        private bool TryTransitionToLobby()
        {
            Platform_Multiplayer multiplayer = Platform_Multiplayer.Instance;
            ulong lobbyId = hostLobby?.identifier ?? multiplayer?.CoopContinuationLobbyID ?? 0UL;
            if (lobbyId == 0)
                return false;

            transitionStarted = true;
            try
            {
                if (hostLobby != null && hostLobby.isHost)
                    OpenHostLobby(multiplayer, hostLobby);
                else
                    JoinClientLobby(multiplayer, lobbyId);

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Post-game lobby transition started: role={(hostLobby != null && hostLobby.isHost ? "host" : "client")}, lobbyId={lobbyId}.");
                return true;
            }
            catch (Exception ex)
            {
                transitionStarted = false;
                creationFailed = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Post-game lobby transition failed; Vanilla exit remains available: {ex}");
                return false;
            }
        }

        private static void JoinClientLobby(Platform_Multiplayer multiplayer, ulong lobbyId)
        {
            if (multiplayer == null)
                throw new InvalidOperationException("Platform multiplayer is unavailable.");

            multiplayer.SetInviteLobbyID(lobbyId);
            multiplayer.ResumeInvite();
        }

        private static void SendContinuationLobbyToConnectedPeers(
            Platform_Multiplayer multiplayer,
            ulong lobbyId)
        {
            if (multiplayer?.gameMembers == null)
                return;

            Platform_Multiplayer.MPData packet = new Platform_Multiplayer.MPData
            {
                packetType = 10,
                dataLength = 8,
                data = BitConverter.GetBytes(lobbyId),
            };
            foreach (Platform_Multiplayer.MPGameMember member in multiplayer.gameMembers)
            {
                if (member == null ||
                    !MultiplayerLobbyReturnPolicy.ShouldAnnounceToMember(
                        member.isSelf,
                        member.kicked,
                        member.skirmishAI,
                        member.stillWithSteamConnection,
                        member.steamID))
                {
                    continue;
                }

                multiplayer.SendPacketToPlayerID(member.playerID, packet);
            }
        }

        private static void OpenHostLobby(
            Platform_Multiplayer multiplayer,
            Platform_Multiplayer.MPLobby lobby)
        {
            MainViewModel viewModel = MainViewModel.Instance;
            FRONT_Multiplayer front = viewModel?.FRONTMultiplayer;
            if (front == null || multiplayer == null)
                throw new InvalidOperationException("The multiplayer frontend is unavailable.");

            viewModel.Show_HUD_MissionOver = false;
            FatControler.instance.NewScene(Enums.SceneIDS.FrontEnd);

            front.currentLobby = lobby;
            multiplayer.SetActiveLobby(lobby);
            front.doOpen(skirmishSetup: false, fromNew: false);
            front.currentLobby = lobby;
            multiplayer.SetActiveLobby(lobby);
            multiplayer.initFastFollowOn();
            // Resolve private frontend helpers only when the host actually leaves the statistics.
            // A future game rename can then fall back to Vanilla without disabling surrender itself.
            FindFrontMethod("updateSteamIDMappings", Type.EmptyTypes).Invoke(front, null);
            FindFrontMethod("UpdateRadarShieldPositions", Type.EmptyTypes).Invoke(front, null);
            FindFrontMethod("UpdateHostInfo", new[] { typeof(bool) }).Invoke(front, new object[] { false });
            FindFrontMethod("ShowSetupScreen", Type.EmptyTypes).Invoke(front, null);
            viewModel.Show_FrontMenus_Background_Main = false;
            viewModel.Show_Frontend_MainMenu = false;
        }

        private void FallbackToVanilla(Action vanillaExit, string reason)
        {
            transitionStarted = true;
            Shared.DebugLogHelper.LogError(
                log,
                $"Post-game lobby handoff fell back to Vanilla Exit: reason={reason}, requested={creationRequested}, failed={creationFailed}.");
            vanillaExit?.Invoke();
        }

        private bool IsTimedOut() =>
            MultiplayerLobbyReturnPolicy.HasTimedOut(
                gameOverStartedAt,
                Stopwatch.GetTimestamp(),
                Stopwatch.Frequency);

        private void Reset(string reason, bool clearContinuationId)
        {
            snapshot = null;
            hostLobby = null;
            supportedSession = false;
            gameOverObserved = false;
            creationRequested = false;
            creationFailed = false;
            transitionStarted = false;
            gameOverStartedAt = 0;
            lastFrame = -1;
            ClearPendingExit();

            if (clearContinuationId && Platform_Multiplayer.Instance != null)
                Platform_Multiplayer.Instance.CoopContinuationLobbyID = 0UL;

            Shared.DebugLogHelper.LogDebug(log, $"Post-game lobby handoff state reset: reason={reason}.");
        }

        private void ClearPendingExit()
        {
            pendingVanillaExit = null;
        }

        private static int ParseBoundedInt(string text, int fallback, int minimum, int maximum)
        {
            return int.TryParse(text, out int value) && value >= minimum && value <= maximum
                ? value
                : fallback;
        }

        private static MethodInfo FindFrontMethod(string name, Type[] parameterTypes)
        {
            MethodInfo method = typeof(FRONT_Multiplayer).GetMethod(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                parameterTypes,
                null);
            if (method == null)
                throw new MissingMethodException(typeof(FRONT_Multiplayer).FullName, name);
            return method;
        }
    }
}
