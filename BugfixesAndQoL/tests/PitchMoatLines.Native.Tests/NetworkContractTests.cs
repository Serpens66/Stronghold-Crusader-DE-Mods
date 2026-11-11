#nullable enable
using System;
using System.Collections.Generic;
using APIShared;
using APIShared.GameModes;
using APIShared.Networking;
using BugfixesAndQoL.PitchMoatLines;

// Compile the actual network coordinator against fake external API/queue boundaries.
// The separate executable fixture loads the production NativePlacement and executes
// its actual machine stub. These doubles do not claim to emulate native scheduling.
static class NetworkContractTests
{
    internal static void Run()
    {
        var wire = new PitchLineNetworkPacket(1, PitchLineOperation.Pitch, 799, 0, int.MaxValue);
        byte[] encoded = MessagePack.MessagePackSerializer.Serialize(wire);
        var decoded = MessagePack.MessagePackSerializer.Deserialize<PitchLineNetworkPacket>(encoded);
        if (encoded.Length > 62 || decoded.Version != wire.Version || decoded.Operation != wire.Operation ||
            decoded.X != wire.X || decoded.Y != wire.Y || decoded.RequestId != wire.RequestId)
            throw new Exception("Production explicit MessagePack formatter roundtrip or body bound.");
        try
        {
            MessagePack.MessagePackSerializer.Deserialize<PitchLineNetworkPacket>(new byte[] { 0x90 });
            throw new Exception("Incomplete packet was accepted.");
        }
        catch (MessagePack.MessagePackSerializationException) { }
        var api = new ModApiClient();
        bool common = true;
        var native = new NativePlacement();
        var network = new PitchLineNetwork(api, () => common, new BepInEx.Logging.ManualLogSource());
        var channel = (AuthenticatedChoreChannel<PitchLineNetworkPacket>)api.Network.Channel;
        network.SetNative(native);
        network.SetMission(new MissionContext());
        var a = new LineTile(160, 271);
        var b = new LineTile(161, 271);
        void Check(bool condition, string name)
        { if (!condition) throw new Exception("Network contract: " + name); }
        void Queue(PitchLineNetworkPacket packet)
        { native.Live.Add(native.Prepared.CommandId); }
        channel.SendBody = packet => { Queue(packet); return new NetworkSendResult(NetworkSendStatus.Submitted); };

        Check(network.TrySubmit(a, 2) == NativeSubmitResult.Submitted, "enqueue first field");
        int first = native.Prepared.CommandId;
        Check(network.PollReceipt(a, 2) == LineReceipt.Pending, "long scheduled wait remains pending");
        Check(network.PollReceipt(b, 2) == LineReceipt.Rejected, "different tile cannot steal receipt");
        Check(network.TrySubmit(b, 2) == NativeSubmitResult.Busy && channel.Sends == 1,
            "new gesture cannot resend or overtake old queued field");
        native.Live.Remove(first);
        Check(network.PollReceipt(a, 2) == LineReceipt.Rejected, "terminal command without receive stops");
        Check(network.TrySubmit(b, 2) == NativeSubmitResult.Submitted && channel.Sends == 2,
            "orphaned terminal command does not block a later clean gesture");
        channel.Deliver(channel.Last!, 2, native.Prepared.CommandId);
        native.Live.Clear();
        Check(network.PollReceipt(b, 2) == LineReceipt.Accepted, "exact authenticated receipt");

        // A receiver on a peer with no locally pending field still executes it.
        network.SetMission(new MissionContext());
        int before = native.Executions;
        channel.Deliver(new PitchLineNetworkPacket(1, PitchLineOperation.Pitch, a.X, a.Y, 99), 3, 300000099);
        Check(native.Executions == before + 1, "remote input executes without personal pending state");
        common = false;
        channel.Deliver(new PitchLineNetworkPacket(1, PitchLineOperation.Pitch, a.X, a.Y, 100), 3, 300000100);
        Check(native.Executions == before + 1, "common capability snapshot gates receiver");
        common = true;
        channel.Deliver(new PitchLineNetworkPacket(2, PitchLineOperation.Pitch, a.X, a.Y, 100), 3, 300000100);
        Check(native.Executions == before + 1, "unknown version rejected");

        channel.SendBody = packet =>
        {
            channel.Deliver(packet, 2, native.Prepared.CommandId);
            return new NetworkSendResult(NetworkSendStatus.Submitted);
        };
        Check(network.TrySubmit(a, 2) == NativeSubmitResult.Submitted &&
            network.PollReceipt(a, 2) == LineReceipt.Accepted, "immediate execute inside Send is retained");
        native.Outcome = LineReceipt.Skipped;
        Check(network.TrySubmit(b, 2) == NativeSubmitResult.Submitted &&
            network.PollReceipt(b, 2) == LineReceipt.Skipped, "known shared obstruction remains a skip");
        native.Outcome = LineReceipt.Rejected;
        Check(network.TrySubmit(a, 2) == NativeSubmitResult.Submitted &&
            network.PollReceipt(a, 2) == LineReceipt.Rejected, "uncertain native attempt remains rejection");
        native.Outcome = LineReceipt.Accepted;

        channel.SendBody = packet => { Queue(packet); return new NetworkSendResult(NetworkSendStatus.TransportFailed); };
        Check(network.TrySubmit(b, 2) == NativeSubmitResult.Submitted,
            "queue identity takes precedence over transport exception after enqueue");
        int sends = channel.Sends;
        Check(network.TrySubmit(b, 2) == NativeSubmitResult.Busy && channel.Sends == sends,
            "queued exception is never retried");
        native.Live.Clear();
        channel.SendBody = packet => new NetworkSendResult(NetworkSendStatus.TransportFailed);
        Check(network.TrySubmit(a, 2) == NativeSubmitResult.Rejected, "ambiguous failure stops rather than retries");

        native.CanPrepare = false;
        sends = channel.Sends;
        Check(network.TrySubmit(a, 2) == NativeSubmitResult.Busy && channel.Sends == sends,
            "full queue never calls send");
        native.CanPrepare = true;
        network.SetMission(null!);
        before = native.Executions;
        channel.Deliver(new PitchLineNetworkPacket(1, PitchLineOperation.Pitch, a.X, a.Y, 101), 3, 300000101);
        Check(native.Executions == before && network.PollReceipt(a, 2) == LineReceipt.Rejected,
            "mission teardown rejects stale receive and receipt");
        Console.WriteLine("PASS production network coordinator: exact receipt, no resend, immediate execute, remote issuer, skip and lifecycle.");
    }
}

namespace APIShared.GameModes
{
    internal sealed class MissionContext { internal readonly ModeSnapshot Mode = new ModeSnapshot(); }
    internal sealed class ModeSnapshot { internal bool IsRealMultiplayer = true; }
}
namespace APIShared
{
    internal sealed class ModApiClient { internal readonly APIShared.Networking.ModNetworkClient Network = new APIShared.Networking.ModNetworkClient(); }
}
namespace APIShared.Networking
{
    internal sealed class ModNetworkClient
    {
        internal object Channel = null!;
        internal AuthenticatedChoreChannel<T> RegisterAuthenticatedChore<T>(string id, Action<AuthenticatedChoreMessage<T>> receive,
            AuthenticatedChoreChannelOptions<T> options) where T : class
        { var value = new AuthenticatedChoreChannel<T>(receive, options); Channel = value; return value; }
    }
    internal sealed class AuthenticatedChoreChannelOptions<T> where T : class
    {
        internal int MaximumPayloadBytes;
        internal Func<bool> IsEnabled = null!;
        internal Func<T, bool> ValidatePayload = null!;
        internal Action<NetworkPacketDiagnostic> Diagnostic = null!;
    }
    internal sealed class AuthenticatedChoreChannel<T> where T : class
    {
        private readonly Action<AuthenticatedChoreMessage<T>> receive;
        private readonly AuthenticatedChoreChannelOptions<T> options;
        internal AuthenticatedChoreChannel(Action<AuthenticatedChoreMessage<T>> receive, AuthenticatedChoreChannelOptions<T> options)
        { this.receive = receive; this.options = options; }
        internal bool IsAvailable = true;
        internal int Sends;
        internal T? Last;
        internal Func<T, NetworkSendResult> SendBody = null!;
        internal NetworkSendResult Send(T packet) { Sends++; Last = packet; return SendBody(packet); }
        internal void Deliver(T packet, int sender, int command)
        { if (options.IsEnabled() && options.ValidatePayload(packet)) receive(new AuthenticatedChoreMessage<T>(packet, sender, command)); }
    }
    internal sealed class AuthenticatedChoreMessage<T> where T : class
    {
        internal T Packet; internal int SenderPlayerId, CommandId;
        internal AuthenticatedChoreMessage(T packet, int sender, int command)
        { Packet = packet; SenderPlayerId = sender; CommandId = command; }
    }
    internal enum NetworkSendStatus { Submitted, TransportFailed }
    internal sealed class NetworkSendResult
    { internal bool Submitted; internal NetworkSendResult(NetworkSendStatus status) { Submitted = status == NetworkSendStatus.Submitted; } }
    internal sealed class NetworkPacketDiagnostic { internal string Reason = string.Empty; internal Exception? Error = null; }
}
namespace BepInEx.Logging { internal sealed class ManualLogSource { } }
namespace Shared { internal static class DebugLogHelper { internal static void LogWarning(object log, string text) { } } }
namespace SHCDESE.API { internal static class GameTileManagerAPI { internal const int MAX_WIDTH = 800, MAX_HEIGHT = 800; } }
namespace SHCDESE.Interop.Enums { internal enum ChoreType : byte { ScriptExtenderChore = 106 } }
namespace BugfixesAndQoL.PitchMoatLines
{
    internal readonly struct LineTile { internal readonly int X, Y; internal LineTile(int x, int y) { X = x; Y = y; } }
    internal enum LineReceipt { Pending, Accepted, Rejected, Skipped }
    internal enum NativeSubmitResult { Submitted, Busy, Rejected }
    internal sealed class NativePlacement
    {
        internal bool PitchMoatReady = true, CanPrepare = true;
        internal readonly HashSet<int> Live = new HashSet<int>();
        internal PendingNativeCommand Prepared;
        internal int Executions;
        internal LineReceipt Outcome = LineReceipt.Accepted;
        internal readonly struct PendingNativeCommand
        { internal readonly int CommandId; internal PendingNativeCommand(int id) { CommandId = id; } }
        internal bool TryPrepareCommand(byte opcode, int player, out PendingNativeCommand command)
        { command = Prepared = new PendingNativeCommand(Prepared.CommandId + 1); return CanPrepare; }
        internal bool IsCommandPending(PendingNativeCommand command) => Live.Contains(command.CommandId);
        internal LineReceipt ExecuteNetworkPitch(LineTile tile, int player, int commandId) { Executions++; return Outcome; }
    }
}
