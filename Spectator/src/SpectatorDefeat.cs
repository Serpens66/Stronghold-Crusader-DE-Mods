using APIShared.GameModes;
// Host-authorized spectator promotion after a confirmed lord death.
using APIShared;
using BepInEx.Logging;
using CrusaderDE;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Network;
using SHCDESE.ViewModels;
using System;
using Steamworks;
using APIShared.Presentation;

namespace Spectator
{
    internal sealed class SpectatorDefeat
    {
        private readonly ManualLogSource log;
        private readonly SpectatorViewModel settings;
        private R3PacketEventHook<EliminatedPlayerSpectatorPacket> spectatorPacketHook;
        private IDisposable spectatorPacketSubscription;
        private readonly long[] lordDeathSessionIds = new long[9];
        private readonly int[] lordDeathSimulationTicks = new int[9];
        private readonly bool[] spectatorChoreQueuedPlayers = new bool[9], spectatorChoreExecutedPlayers = new bool[9];
        private long activeSessionId;
        private bool initialized, localPlayerLordDeathObserved, spectatorPromotionChoreExpected,
            spectatorPromotionActivated, spectatorPromotionRejectionLogged;
        private int localPlayerLordDeathPlayerId = -1, spectatorPromotionPlayerId = -1;
        private string spectatorPromotionGameMode;
        private static SpectatorDefeat instance;
        private bool promotionConfirmed, promotionIdentityError;
        // Host state is independent of the local toolbar activation setting.
        private bool EliminatedPlayerSpectatorEnabled => settings.Activation.Defeat;
        internal SpectatorDefeat(ManualLogSource log, SpectatorViewModel settings) { this.log = log; this.settings = settings; }
        internal void Initialize()
        {
            instance = this;
            spectatorPacketHook = GameNetworkAPI.Instance.GetPacketEventFor<EliminatedPlayerSpectatorPacket>();
            spectatorPacketSubscription = spectatorPacketHook.GetBaseHook().Observable.Subscribe(OnSpectatorPacketReceived);
            if (!ApiShared.Current.TryGetMissionLifecycle(SpectatorPlugin.PluginGuid, out var lifecycle, out var diagnostic) ||
                !lifecycle.TryRegisterObserver("defeat-spectator", n => Reset(n.Context.SessionId), n => Reset(0), null, out diagnostic))
                throw new InvalidOperationException("Defeat lifecycle unavailable: " + diagnostic?.Reason);
            RegisterPlayerDefeatObserver();
            foreach (var operation in new[] { MissionOverOperation.State, MissionOverOperation.Text })
                if (!MissionOverEvents.TryRegister(operation, SpectatorPlugin.PluginGuid, "eliminated-player-result",
                    args => { if (initialized) args.Result = SpectatorDefeatPolicy.ResolvePresentedGameOverState(args.Result,
                        spectatorPromotionChoreExpected || spectatorPromotionActivated); }, null, out string reason))
                    throw new InvalidOperationException(reason);
            initialized = true;
        }
        private void Reset(long session)
        {
            activeSessionId = session;
            promotionConfirmed = promotionIdentityError = false;
            Array.Clear(lordDeathSessionIds, 0, 9); Array.Clear(lordDeathSimulationTicks, 0, 9);
            Array.Clear(spectatorChoreQueuedPlayers, 0, 9); Array.Clear(spectatorChoreExecutedPlayers, 0, 9);
            localPlayerLordDeathObserved = spectatorPromotionChoreExpected = spectatorPromotionActivated = spectatorPromotionRejectionLogged = false;
            localPlayerLordDeathPlayerId = spectatorPromotionPlayerId = -1;
        }
        internal static void ObservePresentedState()
        {
            var current = instance;
            if (current == null || !current.spectatorPromotionActivated || current.promotionConfirmed ||
                current.promotionIdentityError || !IsStartSpectator()) return;
            int native = PlayerPerspectiveAPI.GetRawNativeViewPlayerId();
            int managed = EditorDirector.instance?.ActivePlayerID ?? -1;
            if (!SpectatorDefeatPolicy.ConfirmIdentity(current.spectatorPromotionPlayerId, native, managed))
            {
                current.promotionIdentityError = true;
                Shared.DebugLogHelper.LogErrorLimited(current.log, "spectator-promotion-identity",
                    $"Spectator promotion identity mismatch: expected={current.spectatorPromotionPlayerId}, native={native}, managed={managed}.");
                return;
            }
            current.promotionConfirmed = true;
            SpectatorRuntime.InvalidateToolbar();
            Shared.DebugLogHelper.LogDebugLimited(current.log, "spectator-promotion-confirmed",
                $"Spectator promotion confirmed: player={managed}; controlled identity preserved.");
        }
        private static bool IsMapEditor() => GameModeHelper.IsMapEditor();
        private static bool IsStartSpectator() => GameData.Instance?.lastGameState != null && GameData.Instance.lastGameState.spectatorMode != 0;
        private static bool IsHumanMember(Platform_Multiplayer.MPGameMember member) => member != null && member.playerID >= 1 &&
            member.playerID <= 8 && !member.kicked && !member.skirmishAI && member.steamID > 1000;
        private void LogPacketWarning(string message) => Shared.DebugLogHelper.LogWarningLimited(log, "spectator-packet", message);
        private void LogPacketInfo(string message) => Shared.DebugLogHelper.LogDebugLimited(log, "spectator-packet", message);
        private void LogPacketError(string message) => Shared.DebugLogHelper.LogErrorLimited(log, "spectator-packet", message);
        private void RegisterPlayerDefeatObserver()
        {
            if (!ApiShared.Current.TryGetPlayerDefeat(
                    SpectatorPlugin.PluginGuid,
                    out IPlayerDefeatCapability capability,
                    out NativeCapabilityDiagnostic diagnostic))
            {
                LogPlayerDefeatRegistrationFailure(diagnostic);
                return;
            }

            if (!capability.TryRegisterObserver(
                    "eliminated-player-spectator",
                    OnPlayerLordDied,
                    null,
                    out diagnostic))
                LogPlayerDefeatRegistrationFailure(diagnostic);
        }

        private void LogPlayerDefeatRegistrationFailure(NativeCapabilityDiagnostic diagnostic) =>
            Shared.DebugLogHelper.LogWarningLimited(log, "spectator-defeat-registration", $"Spectator defeat observer unavailable: {diagnostic?.State}, {diagnostic?.Reason}");

        private void OnPlayerLordDied(PlayerLordDeathNotification notification)
        {
            if (!initialized || notification == null)
                return;

            int playerId = notification.PlayerId;
            if (playerId < 1 || playerId > 8 ||
                activeSessionId <= 0 || notification.SessionId != activeSessionId)
            {
                Shared.DebugLogHelper.LogWarningLimited(log, "Spectator/src/SpectatorDefeat.cs:545", $"Ignored stale or invalid APIShared lord-death notification: activeSessionId={activeSessionId}, notificationSessionId={notification.SessionId}, playerId={playerId}, simulationTick={notification.SimulationTick}.");
                return;
            }

            if (lordDeathSessionIds[playerId] == notification.SessionId)
                return;

            lordDeathSessionIds[playerId] = notification.SessionId;
            lordDeathSimulationTicks[playerId] = notification.SimulationTick;
            SpectatorRuntime.Statistics?.InvalidateMenu(); // Existing defeat notification; no additional simulation observer.

            int localPlayerId = PlayerPerspectiveAPI.GetControlledPlayerId();
            bool localLordDied = SpectatorDefeatPolicy.ShouldLatchPlayerLordDeath(playerId, localPlayerId);
            if (localLordDied)
            {
                localPlayerLordDeathObserved = true;
                localPlayerLordDeathPlayerId = playerId;
            }

            Shared.DebugLogHelper.LogDebugLimited(log, "Spectator/src/SpectatorDefeat.cs:565", $"Observed lord death through APIShared: sessionId={notification.SessionId}, playerId={playerId}, lordUnitId={notification.LordUnitId}, lordGlobalId={notification.LordGlobalId}, simulationTick={notification.SimulationTick}, localPlayerId={localPlayerId}.");

            APIShared.GameModes.GameModeSnapshot gameMode = APIShared.GameModes.GameModeHelper.Capture();
            if (localLordDied && EliminatedPlayerSpectatorEnabled &&
                (gameMode.IsRealMultiplayer || gameMode.IsSingleplayerSkirmishMode))
                spectatorPromotionChoreExpected = true;
            if (!gameMode.IsRealMultiplayer)
            {
                if (localLordDied)
                {
                    // Preserve the pre-tick activation timing. SpectatorMode only sets Vanilla's
                    // local native flag under EngineInterface.threadLock; its return is zero, so
                    // this GameAction has no managed UI branch. Confirmation remains on the UI thread.
                    TryActivateLocalSpectator(playerId, gameMode, "APIShared singleplayer lord-death event");
                }
                return;
            }

            if (GameNetworkAPI.IsLocalHost())
                TryQueueSpectatorChore(notification, gameMode);
        }

        private void TryQueueSpectatorChore(
            PlayerLordDeathNotification notification,
            APIShared.GameModes.GameModeSnapshot gameMode)
        {
            int playerId = notification.PlayerId;
            Platform_Multiplayer.MPGameMember member = Platform_Multiplayer.Instance?.getPlayer(playerId);
            if (!EliminatedPlayerSpectatorEnabled ||
                !IsActiveMatch() || !gameMode.IsRealMultiplayer || IsMapEditor() ||
                notification.SessionId != activeSessionId ||
                !IsHumanMember(member) || spectatorChoreQueuedPlayers[playerId])
            {
                return;
            }

            var packet = new EliminatedPlayerSpectatorPacket { PlayerId = playerId };
            short packetId = spectatorPacketHook?.GetPacketId() ?? (short)0;
            if (!APIShared.Networking.ChoreTransport.TrySend(
                    packet,
                    packetId,
                    initialized && spectatorPacketHook != null,
                    out byte[] body,
                    out string rejectionReason))
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "Spectator/src/SpectatorDefeat.cs:607", $"Eliminated-player spectator Chore was not queued; no unsynchronized fallback was applied: sessionId={notification.SessionId}, playerId={playerId}, lordDeathTick={notification.SimulationTick}, reason={rejectionReason}.");
                return;
            }

            spectatorChoreQueuedPlayers[playerId] = true;
            int queueTick = GameTimeManagerAPI.Instance.GetElapsedMapTicks();
            Shared.DebugLogHelper.LogDebugLimited(log, "Spectator/src/SpectatorDefeat.cs:615", $"Eliminated-player spectator Chore queued: sessionId={notification.SessionId}, playerId={playerId}, lordDeathTick={notification.SimulationTick}, queueTick={queueTick}, packetId={packetId}, bodyBytes={body.Length}, .");
        }

        private void OnSpectatorPacketReceived(
            ReceiveCustomPacketEventArgs<EliminatedPlayerSpectatorPacket> args)
        {
            if (!initialized) return;
            EliminatedPlayerSpectatorPacket packet = args?.Packet;
            try
            {
                int playerId = packet?.PlayerId ?? 0;
                Platform_Multiplayer.MPGameMember member = Platform_Multiplayer.Instance?.getPlayer(playerId);
                if (packet == null || args.SenderSteamId.HasValue ||
                    playerId < 1 || playerId > 8 ||
                    !EliminatedPlayerSpectatorEnabled || !IsActiveMatch() ||
                    !APIShared.GameModes.GameModeHelper.IsRealMultiplayer() || IsMapEditor() ||
                    activeSessionId <= 0 || lordDeathSessionIds[playerId] != activeSessionId ||
                    !IsHumanMember(member))
                {
                    LogPacketWarning(
                        $"Rejected stale or invalid eliminated-player spectator Chore: playerId={playerId}, hasSteamSender={args?.SenderSteamId.HasValue}, activeSessionId={activeSessionId}, observedLordDeathSessionId={(playerId >= 1 && playerId <= 8 ? lordDeathSessionIds[playerId] : 0)}.");
                    return;
                }

                if (spectatorChoreExecutedPlayers[playerId])
                    return;

                spectatorChoreExecutedPlayers[playerId] = true;
                int localPlayerId = GamePlayerManagerAPI.Instance?.GetLocalPlayerId() ?? -1;
                int executionTick = GameTimeManagerAPI.Instance.GetElapsedMapTicks();
                LogPacketInfo(
                    $"Eliminated-player spectator Chore executed: sessionId={activeSessionId}, playerId={playerId}, lordDeathTick={lordDeathSimulationTicks[playerId]}, executionTick={executionTick}, localPlayerId={localPlayerId}.");

                if (localPlayerId != playerId)
                    return;

                long sessionSnapshot = activeSessionId;
                APIShared.GameModes.GameModeSnapshot gameModeSnapshot = APIShared.GameModes.GameModeHelper.Capture();
                Shared.UnityMainThreadDispatch.TryEnqueue(() =>
                {
                    if (activeSessionId != sessionSnapshot ||
                        lordDeathSessionIds[playerId] != sessionSnapshot)
                        return;
                    TryActivateLocalSpectator(
                        playerId,
                        gameModeSnapshot,
                        "synchronized eliminated-player spectator Chore");
                });
            }
            catch (Exception ex)
            {
                LogPacketError($"Eliminated-player spectator Chore failed closed: {ex}");
            }
        }

        private void TryActivateLocalSpectator(
            int localPlayerId,
            APIShared.GameModes.GameModeSnapshot gameMode,
            string source)
        {
            bool supportedGameMode =
                gameMode.IsRealMultiplayer || gameMode.IsSingleplayerSkirmishMode;
            bool validLocalParticipant =
                IsValidLocalSpectatorPromotionParticipant(localPlayerId, gameMode.IsRealMultiplayer);
            bool lordDeathObserved =
                localPlayerLordDeathObserved && localPlayerLordDeathPlayerId == localPlayerId;
            if (spectatorPromotionActivated ||
                !SpectatorDefeatPolicy.CanPromoteEliminatedPlayerToSpectator(
                    EliminatedPlayerSpectatorEnabled,
                    IsActiveMatch(),
                    IsMapEditor(),
                    IsStartSpectator(),
                    supportedGameMode,
                    validLocalParticipant,
                    lordDeathObserved,
                    localPlayerId))
            {
                if (EliminatedPlayerSpectatorEnabled && lordDeathObserved &&
                    (!supportedGameMode || !validLocalParticipant) &&
                    !spectatorPromotionRejectionLogged)
                {
                    spectatorPromotionRejectionLogged = true;
                    Shared.DebugLogHelper.LogWarningLimited(log, "Spectator/src/SpectatorDefeat.cs:699", $"Eliminated-player spectator activation was rejected fail-closed: playerId={localPlayerId}, source={source}, supportedGameMode={supportedGameMode}, validLocalParticipant={validLocalParticipant}, mode={gameMode.ToDiagnosticString()}.");
                }
                return;
            }

            EngineInterface.GameAction(Enums.GameActionCommand.SpectatorMode, 0, 0);
            spectatorPromotionActivated = true;
            spectatorPromotionChoreExpected = false;
            spectatorPromotionPlayerId = localPlayerId;
            spectatorPromotionGameMode = gameMode.ToDiagnosticString();
            Shared.DebugLogHelper.LogDebugLimited(log, "Spectator/src/SpectatorDefeat.cs:710", $"Activated Vanilla spectator mode for eliminated local player {localPlayerId}: source={source}, mode={spectatorPromotionGameMode}.");
        }

        private static bool IsValidLocalSpectatorPromotionParticipant(
            int localPlayerId,
            bool realMultiplayer)
        {
            if (localPlayerId < 1 || localPlayerId > 8 ||
                EditorDirector.instance == null ||
                EditorDirector.instance.ActivePlayerID != localPlayerId)
            {
                return false;
            }

            if (!realMultiplayer)
                return true;

            Platform_Multiplayer multiplayer = Platform_Multiplayer.Instance;
            Platform_Multiplayer.MPGameMember localMember = multiplayer?.getPlayer(localPlayerId);
            CSteamID localSteamId = SteamUser.GetSteamID();
            return IsHumanMember(localMember) &&
                localSteamId.m_SteamID > 1000 &&
                localMember.steamID == localSteamId.m_SteamID;
        }

        private bool IsActiveMatch()
        {
            return FatControler.currentScene == Enums.SceneIDS.ActualMainGame &&
                Director.instance != null &&
                Director.instance.SimRunning &&
                GameData.Instance != null &&
                GameData.Instance.lastGameState != null;
        }
    }
}
