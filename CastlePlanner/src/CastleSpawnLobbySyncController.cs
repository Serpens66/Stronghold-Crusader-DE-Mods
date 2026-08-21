using BepInEx.Logging;
using MessagePack;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Network;
using Steamworks;
using System;
using UnityEngine;

namespace CastlePlanner
{
    internal sealed class CastleSpawnLobbySyncController
    {
        private const int ProtocolVersion = 1;
        private readonly ManualLogSource log;
        private readonly CastlePlannerSettingsViewModel settings;
        private readonly R3PacketEventHook<CastleSpawnSyncRequestPacket> packetHook;
        private readonly IDisposable packetSubscription;
        private readonly IDisposable joinSubscription;
        private int lastObservedFrame = -1;
        private float nextErrorLogTime;

        private CastleSpawnLobbySyncController(
            ManualLogSource log,
            CastlePlannerSettingsViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            packetHook = GameNetworkAPI.Instance
                .GetPacketEventFor<CastleSpawnSyncRequestPacket>();
            packetSubscription = packetHook.GetBaseHook().Observable
                .Subscribe(OnSyncRequestReceived);
            joinSubscription = NetworkR3EventHooks.OnSendCustomInfoToLobbyMember.Observable
                .Subscribe(OnLobbyMemberJoining);
            Application.onBeforeRender += OnBeforeRender;
        }

        public static CastleSpawnLobbySyncController Create(
            ManualLogSource log,
            CastlePlannerSettingsViewModel settings)
        {
            var controller = new CastleSpawnLobbySyncController(log, settings);
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Persistent CastlePlanner lobby compatibility observer registered; " +
                $"resyncPacketId={controller.packetHook.GetPacketId()}, " +
                $"protocolVersion={ProtocolVersion}.");
            return controller;
        }

        private void OnLobbyMemberJoining(OnSendCustomInfoToLobbyMemberEventArgs args)
        {
            try
            {
                Platform_Multiplayer.MPLobby lobby = Platform_Multiplayer.Instance?.activeLobby;
                if (args?.Phase != EventHookPhase.Post || lobby == null || !lobby.isHost)
                    return;

                // The callback runs outside GameXAMLManagerAPI.ReceiveSettingsUpdate, so the
                // per-player PropertyChanged notifications are not suppressed as sync echoes.
                settings.RequestLobbyCompatibilityBroadcast();
                var packet = new CastleSpawnSyncRequestPacket
                {
                    ProtocolVersion = ProtocolVersion,
                    LobbyId = lobby.id.m_SteamID
                };
                byte[] bytes = MessagePackSerializer.Serialize(packet);
                GameNetworkAPI.SendPacketToAllLobby(new Platform_Multiplayer.MPData
                {
                    packetType = packetHook.GetPacketId(),
                    data = bytes,
                    dataLength = bytes.Length,
                    dataOffset = 0
                });
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"CastlePlanner requested lobby-wide compatibility re-advertisement: " +
                    $"lobby={packet.LobbyId}, joiningMember='{args.Member?.name}'.");
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"CastlePlanner could not request compatibility data for a joining player: {exception}");
            }
        }

        private void OnSyncRequestReceived(
            ReceiveCustomPacketEventArgs<CastleSpawnSyncRequestPacket> args)
        {
            try
            {
                Platform_Multiplayer.MPLobby lobby = Platform_Multiplayer.Instance?.activeLobby;
                CastleSpawnSyncRequestPacket packet = args?.Packet;
                if (lobby == null || packet == null ||
                    packet.ProtocolVersion != ProtocolVersion ||
                    packet.LobbyId != lobby.id.m_SteamID ||
                    !args.SenderSteamId.HasValue ||
                    args.SenderSteamId.Value != SteamMatchmaking.GetLobbyOwner(lobby.id))
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        "CastlePlanner rejected an unauthenticated or stale lobby resync request.");
                    return;
                }

                settings.RequestLobbyCompatibilityBroadcast();
            }
            catch (Exception exception)
            {
                Shared.DebugLogHelper.LogError(
                    log,
                    $"CastlePlanner failed while handling a lobby resync request: {exception}");
            }
        }

        private void OnBeforeRender()
        {
            int frame = Time.frameCount;
            if (lastObservedFrame >= 0 && frame - lastObservedFrame < 15)
                return;
            lastObservedFrame = frame;

            try
            {
                // Lobby menus have no simulation tick. Observe only low-frequency roster
                // state here; file hashing remains change-cached in the settings model.
                settings.ObserveLobbyCompatibilityState();
            }
            catch (Exception exception)
            {
                if (Time.unscaledTime < nextErrorLogTime)
                    return;
                nextErrorLogTime = Time.unscaledTime + 5f;
                Shared.DebugLogHelper.LogError(
                    log,
                    $"CastlePlanner lobby compatibility observer recovered from an error: {exception}");
            }
        }
    }
}
