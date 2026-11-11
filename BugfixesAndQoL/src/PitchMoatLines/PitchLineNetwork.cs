using System;
using APIShared;
using APIShared.GameModes;
using APIShared.Networking;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop.Enums;

namespace BugfixesAndQoL.PitchMoatLines
{
    // Registered once on every peer, even when local input/native installation is off.
    // The shared compatibility snapshot gates receive, never the personal input option.
    internal sealed class PitchLineNetwork
    {
        private const int Version = 1;
        private readonly AuthenticatedChoreChannel<PitchLineNetworkPacket> channel;
        private readonly Func<bool> receiveEnabled;
        private NativePlacement native;
        private MissionContext mission;
        private Pending pending;
        private int nextRequest;

        private sealed class Pending
        {
            internal readonly LineTile Tile;
            internal readonly int Player, RequestId;
            internal readonly NativePlacement.PendingNativeCommand Command;
            internal LineReceipt Result;
            internal Pending(LineTile tile, int player, int request, NativePlacement.PendingNativeCommand command)
            { Tile = tile; Player = player; RequestId = request; Command = command; Result = LineReceipt.Pending; }
        }

        internal PitchLineNetwork(ModApiClient api, Func<bool> receiveEnabled, ManualLogSource log)
        {
            this.receiveEnabled = receiveEnabled ?? throw new ArgumentNullException(nameof(receiveEnabled));
            channel = api.Network.RegisterAuthenticatedChore<PitchLineNetworkPacket>("pitch-moat-line-field-v1", Receive,
                new AuthenticatedChoreChannelOptions<PitchLineNetworkPacket>
                {
                    MaximumPayloadBytes = 64,
                    IsEnabled = CanReceive,
                    ValidatePayload = ValidPacket,
                    Diagnostic = diagnostic => Shared.DebugLogHelper.LogWarning(log,
                        "PITCH_MOAT_LINE_NETWORK: " + diagnostic.Reason +
                        (diagnostic.Error == null ? string.Empty : ": " + diagnostic.Error))
                });
        }

        internal void SetNative(NativePlacement value) { native = value; }
        internal bool NativeReady => native != null && native.PitchMoatReady;
        // This is deliberately Unity-only, just like the public channel contract.
        internal bool IsAvailable => NativeReady && channel.IsAvailable;

        internal void SetMission(MissionContext context)
        {
            // Only mission teardown retires an enqueued operation unconditionally.
            // Focus, input option, and tool changes do not revoke its synchronized effect.
            mission = context;
            pending = null;
        }

        private bool CanReceive() => mission != null && mission.Mode.IsRealMultiplayer && NativeReady && receiveEnabled();
        private static bool ValidPacket(PitchLineNetworkPacket packet) => packet != null && packet.Version == Version &&
            packet.Operation == PitchLineOperation.Pitch && packet.RequestId > 0 &&
            (uint)packet.X < GameTileManagerAPI.MAX_WIDTH && (uint)packet.Y < GameTileManagerAPI.MAX_HEIGHT;

        // Called from Unity AfterInput while EngineGate is held. No native queue call
        // occurs on a Busy return; Submitted operations must never be sent again.
        internal NativeSubmitResult TrySubmit(LineTile tile, int player)
        {
            if (!CanReceive() || !IsAvailable) return NativeSubmitResult.Rejected;
            if (pending != null)
            {
                if (pending.Result == LineReceipt.Pending && native.IsCommandPending(pending.Command))
                    return NativeSubmitResult.Busy;
                pending = null;
            }
            if (nextRequest == int.MaxValue) return NativeSubmitResult.Rejected;
            if (!native.TryPrepareCommand((byte)ChoreType.ScriptExtenderChore, player, out var command))
                return NativeSubmitResult.Busy;
            var packet = new PitchLineNetworkPacket(Version, PitchLineOperation.Pitch, tile.X, tile.Y, ++nextRequest);
            var candidate = new Pending(tile, player, packet.RequestId, command);
            pending = candidate; // Immediate native dispatch may complete inside Send.
            var result = channel.Send(packet);
            if (result.Submitted || native.IsCommandPending(command)) return NativeSubmitResult.Submitted;
            pending = null;
            // TransportFailed can mean the native void queue was already entered.
            // Stop the gesture rather than risk replaying an uncertain operation.
            return NativeSubmitResult.Rejected;
        }

        internal LineReceipt PollReceipt(LineTile tile, int player)
        {
            var current = pending;
            if (current == null || current.Player != player || current.Tile.X != tile.X || current.Tile.Y != tile.Y)
                return LineReceipt.Rejected;
            if (current.Result != LineReceipt.Pending) return current.Result;
            return native != null && native.IsCommandPending(current.Command)
                ? LineReceipt.Pending : LineReceipt.Rejected;
        }

        private void Receive(AuthenticatedChoreMessage<PitchLineNetworkPacket> message)
        {
            // APIShared has verified exact body, native issuer, command, slot and tick.
            // Execution is synchronous on every peer, including the issuing peer.
            LineReceipt outcome = native.ExecuteNetworkPitch(new LineTile(message.Packet.X, message.Packet.Y),
                message.SenderPlayerId, message.CommandId);
            var current = pending;
            if (current != null && current.Player == message.SenderPlayerId &&
                current.RequestId == message.Packet.RequestId && current.Command.CommandId == message.CommandId &&
                current.Tile.X == message.Packet.X && current.Tile.Y == message.Packet.Y)
                current.Result = outcome;
        }
    }
}
