using System;
using System.Collections.Generic;
using APIShared;
using APIShared.GameModes;
using APIShared.Networking;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop.Enums;

namespace BugfixesAndQoL.PitchMoatLines
{
    // Each native Chore owns its whole immutable line in every mode.
    // No input-owned pending field and no per-field acknowledgement.
    internal sealed class PitchLineNetwork
    {
        private const int Version = 2;
        private readonly AuthenticatedChoreChannel<PitchLineNetworkPacket> channel;
        private readonly Func<bool> receiveEnabled;
        private NativePlacement native;
        private MissionContext mission;
        private int nextRequest;

        internal PitchLineNetwork(ModApiClient api, Func<bool> receiveEnabled, ManualLogSource log)
        {
            this.receiveEnabled = receiveEnabled ?? throw new ArgumentNullException(nameof(receiveEnabled));
            channel = api.Network.RegisterAuthenticatedChore<PitchLineNetworkPacket>("pitch-moat-line-batch-v2", Receive,
                new AuthenticatedChoreChannelOptions<PitchLineNetworkPacket>
                {
                    MaximumPayloadBytes = 512,
                    IsEnabled = CanReceive,
                    ValidatePayload = ValidPacket,
                    Diagnostic = diagnostic => Shared.DebugLogHelper.LogWarning(log,
                        "PITCH_MOAT_LINE_NETWORK: " + diagnostic.Reason +
                        (diagnostic.Error == null ? string.Empty : ": " + diagnostic.Error))
                });
        }

        internal void SetNative(NativePlacement value) { native = value; }
        internal bool NativeReady => native != null && native.BatchReady;
        // Availability queries and Send remain Unity-only by the public contract.
        internal bool IsAvailable => NativeReady && channel.IsAvailable;
        internal void SetMission(MissionContext context) { mission = context; }
        private bool CanReceive() => mission != null && NativeReady &&
            (!mission.Mode.IsRealMultiplayer || receiveEnabled());

        private static bool ValidPacket(PitchLineNetworkPacket packet)
        {
            if (packet == null || packet.Version != Version || packet.RequestId <= 0 ||
                packet.Actor < 1 || packet.Actor > GamePlayerManagerAPI.MAX_PLAYERS ||
                packet.Operation < PitchLineOperation.Pitch || packet.Operation > PitchLineOperation.CompletedMoat ||
                packet.PackedTiles.Count < 1 || packet.PackedTiles.Count > LinePolicy.MaxTiles)
                return false;
            for (int i = 0; i < packet.PackedTiles.Count; i++)
            {
                var tile = packet.GetTile(i);
                if ((uint)tile.X >= GameTileManagerAPI.MAX_WIDTH || (uint)tile.Y >= GameTileManagerAPI.MAX_HEIGHT)
                    return false;
            }
            return true;
        }

        internal NativeSubmitResult TrySubmit(PitchLineOperation operation, IReadOnlyList<LineTile> tiles, int player)
        {
            if (!CanReceive() || !IsAvailable || !native.IsBatchOperationAllowed(operation) ||
                tiles == null || tiles.Count < 1 || tiles.Count > LinePolicy.MaxTiles || nextRequest == int.MaxValue)
                return NativeSubmitResult.Rejected;
            int actor = player;
            if (operation == PitchLineOperation.Pitch && !native.TryGetBatchActor(player, out actor))
                return NativeSubmitResult.Rejected;
            if (!native.IsBatchActorAllowed(player, actor, mission.IsEditor))
                return NativeSubmitResult.Rejected;
            var packed = new int[tiles.Count];
            for (int i = 0; i < tiles.Count; i++)
            {
                if ((uint)tiles[i].X >= GameTileManagerAPI.MAX_WIDTH || (uint)tiles[i].Y >= GameTileManagerAPI.MAX_HEIGHT)
                    return NativeSubmitResult.Rejected;
                packed[i] = tiles[i].X | (tiles[i].Y << 16);
            }
            var packet = new PitchLineNetworkPacket(Version, operation, actor, ++nextRequest, packed);
            if (!ValidPacket(packet)) return NativeSubmitResult.Rejected;
            if (!native.TryPrepareCommand((byte)ChoreType.ScriptExtenderChore, player, out var command))
                return NativeSubmitResult.Busy;
            var result = channel.Send(packet);
            // Local execution can finish inside Send. An exception after enqueue
            // must never cause the same operation to be submitted again.
            return result.Submitted || native.IsCommandPending(command)
                ? NativeSubmitResult.Submitted : NativeSubmitResult.Rejected;
        }

        private void Receive(AuthenticatedChoreMessage<PitchLineNetworkPacket> message)
        {
            var packet = message.Packet;
            if (!native.IsBatchOperationAllowed(packet.Operation) ||
                !native.IsBatchActorAllowed(message.SenderPlayerId, packet.Actor, mission.IsEditor) ||
                (packet.Operation != PitchLineOperation.Pitch && packet.Actor != message.SenderPlayerId))
                return;
            // The entire body is bound to one executing native slot. Every peer
            // uses the same live per-field build path, including the sender.
            for (int i = 0; i < packet.PackedTiles.Count; i++)
                if (native.ExecuteBatchTile(packet.Operation, packet.GetTile(i), message.SenderPlayerId,
                    packet.Actor, message.CommandId) == LineReceipt.Rejected)
                    break;
        }
    }
}
