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
        private long exitWaitStartedAt;
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

        internal void OnGameOverPresentation()
        {
            // Vanilla starts its Coop continuation lobby from this same on-screen signal.
            // It occurs early enough that packet type 10 can arrive before ManageGameOver
            // releases the clients' gameMembers receive path.
            TryCreateReplacementLobby("post-game-over");
        }

        internal void OnGameOverState(int state)
        {
            if (!supportedSession || state <= 0 || gameOverObserved)
                return;

            gameOverObserved = true;
            if (!TryCreateReplacementLobby("set-game-over-fallback"))
            {
                Platform_Multiplayer multiplayer = Platform_Multiplayer.Instance;
                bool isHost = multiplayer?.IsGameMemberHost() == true;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Post-game lobby handoff armed: host={isHost}, creationRequested={creationRequested}, snapshotReady={snapshot != null}, receivedLobbyId={multiplayer?.CoopContinuationLobbyID ?? 0UL}.");
            }
        }

        private bool TryCreateReplacementLobby(string trigger)
        {
            Platform_Multiplayer multiplayer = Platform_Multiplayer.Instance;
            bool isHost = multiplayer?.IsGameMemberHost() == true;
            if (!MultiplayerLobbyReturnPolicy.ShouldCreateLobby(
                    supportedSession,
                    true,
                    isHost,
                    creationRequested))
            {
                return false;
            }

            creationRequested = true;
            if (snapshot == null)
            {
                creationFailed = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    "Post-game lobby creation was skipped because the original lobby metadata was not captured.");
                return false;
            }

            try
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Creating post-game lobby: trigger={trigger}, name='{snapshot.GameName}', map='{snapshot.MapFileName}', maxPlayers={snapshot.MaxPlayers}, lobbyMode={snapshot.LobbyMode}.");
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
                return true;
            }
            catch (Exception ex)
            {
                creationFailed = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Post-game lobby creation failed before Steam accepted the request; Vanilla exit remains available: {ex}");
                return false;
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

            if (pendingVanillaExit == null)
            {
                pendingVanillaExit = vanillaExit;
                exitWaitStartedAt = Stopwatch.GetTimestamp();
                Shared.DebugLogHelper.LogInfo(log, "Post-game Exit is waiting for the host's replacement lobby ID.");
            }
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
                int eligiblePeers = CountEligibleGamePeers(multiplayer);
                // Use the exact Vanilla Coop transport. SendPacketToAll already excludes self
                // and AI members, while SendGameData rejects kicked recipients.
                multiplayer.SendCoopContinuationLobby(created.identifier);
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Post-game lobby created and announced to connected peers: lobbyId={created.identifier}, steamMembers={created.numLobbyMembers}, eligibleGamePeers={eligiblePeers}.");
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

        private static int CountEligibleGamePeers(Platform_Multiplayer multiplayer)
        {
            if (multiplayer?.gameMembers == null)
                return 0;

            int count = 0;
            foreach (Platform_Multiplayer.MPGameMember member in multiplayer.gameMembers)
            {
                if (member == null || member.isSelf || member.kicked || member.skirmishAI ||
                    member.steamID <= 1000UL)
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private void OpenHostLobby(
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
            RestoreHostMapPresentation(front);
            RefreshHostLobbyRows(front);
            viewModel.Show_FrontMenus_Background_Main = false;
            viewModel.Show_Frontend_MainMenu = false;
        }

        private void RestoreHostMapPresentation(FRONT_Multiplayer front)
        {
            try
            {
                FileHeader header = MapFileManager.Instance?.GetHeaderFromFileNameMP(
                    snapshot.MapFileName,
                    snapshot.Crc);
                if (header == null)
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Post-game host lobby map presentation could not be restored because the exact map header was not found: map='{snapshot.MapFileName}', crc={snapshot.Crc}.");
                    return;
                }

                // doOpen(false) preserves the replacement Steam lobby but clears the map
                // presentation. Re-select through Vanilla so every bound detail is rebuilt.
                front.ButtonClicked("ClearFilter");
                FindFrontMethod(
                    "populateMapList",
                    new[] { typeof(FileHeader), typeof(bool) })
                    .Invoke(front, new object[] { header, false });

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Post-game host lobby map presentation restored through Vanilla selection: map='{snapshot.MapFileName}', crc={snapshot.Crc}.");
            }
            catch (Exception ex)
            {
                // The replacement Steam lobby is already valid; presentation recovery is cosmetic.
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Post-game host lobby opened, but its map presentation could not be restored: map='{snapshot?.MapFileName ?? string.Empty}', crc={snapshot?.Crc ?? 0}, error={ex}");
            }
        }

        private void RefreshHostLobbyRows(FRONT_Multiplayer front)
        {
            try
            {
                // doOpen(false) preserves the new Steam lobby and therefore skips the normal
                // frontend reset. Clear only transient match UI, then let Vanilla render the
                // actual members of the replacement lobby.
                SetFrontField(front, "MPGameLoading", false);
                SetFrontField(front, "MPLocalReady", false);
                SetFrontField(front, "MPLocalReadyLocked", false);
                SetFrontField(front, "humanPlayerCount", -1);

                FieldInfo rowsField = FindFrontField("playerRows");
                Array rows = rowsField.GetValue(front) as Array;
                if (rows == null)
                    throw new InvalidOperationException("The multiplayer player-row array is unavailable.");

                foreach (object row in rows)
                {
                    MethodInfo clearMethod = row?.GetType().GetMethod(
                        "Clear",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null,
                        Type.EmptyTypes,
                        null);
                    if (clearMethod == null)
                        throw new MissingMethodException(row?.GetType().FullName, "Clear");
                    clearMethod.Invoke(row, null);
                }

                front.Update();
            }
            catch (Exception ex)
            {
                // The Steam lobby is already valid. A cosmetic refresh failure must not leave it.
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Post-game host lobby opened, but its preserved player rows could not be refreshed immediately: {ex}");
            }
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
                exitWaitStartedAt,
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
            exitWaitStartedAt = 0;
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

        private static FieldInfo FindFrontField(string name)
        {
            FieldInfo field = typeof(FRONT_Multiplayer).GetField(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
                throw new MissingFieldException(typeof(FRONT_Multiplayer).FullName, name);
            return field;
        }

        private static void SetFrontField(FRONT_Multiplayer front, string name, object value)
        {
            FindFrontField(name).SetValue(front, value);
        }
    }
}
