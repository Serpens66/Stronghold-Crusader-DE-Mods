#nullable enable
using System;
using System.Collections.Generic;
using APIShared;
using APIShared.GameModes;
using APIShared.Networking;
using BugfixesAndQoL.PitchMoatLines;

// Compile the actual coordinator, formatter and line policy against external API
// boundaries. Native scheduling/ABI is covered separately by the executable fixture.
static class NetworkContractTests
{
    private static int checks;

    private static void Check(bool condition, string name)
    {
        checks++;
        if (!condition) throw new Exception("Network contract: " + name);
    }

    private static int Pack(int x, int y) => x | (y << 16);

    private static PitchLineNetworkPacket Packet(PitchLineOperation operation = PitchLineOperation.Pitch,
        int actor = 2, int request = 1, int version = 2, int[]? tiles = null) =>
        new PitchLineNetworkPacket(version, operation, actor, request, tiles ?? new[] { Pack(160, 271), Pack(161, 271), Pack(162, 271) });

    private static void RejectWire(byte[] bytes, string name)
    {
        try { MessagePack.MessagePackSerializer.Deserialize<PitchLineNetworkPacket>(bytes); }
        catch (MessagePack.MessagePackSerializationException) { Check(true, name); return; }
        throw new Exception("Network contract: accepted invalid wire " + name);
    }

    internal static void Run()
    {
        TestWire();
        TestCoordinator();
        Console.WriteLine("PASS production batch network/protocol: " + checks + " checks.");
    }

    private static void TestWire()
    {
        foreach (PitchLineOperation operation in Enum.GetValues<PitchLineOperation>())
        foreach (int count in new[] { 1, LinePolicy.MaxTiles })
        {
            var packed = new int[count];
            for (int i = 0; i < packed.Length; i++) packed[i] = Pack(799, 799);
            var wire = Packet(operation, 8, int.MaxValue, tiles: packed);
            byte[] encoded = MessagePack.MessagePackSerializer.Serialize(wire);
            var decoded = MessagePack.MessagePackSerializer.Deserialize<PitchLineNetworkPacket>(encoded);
            Check(encoded.Length + sizeof(short) <= 512, "maximum wire body fits configured Chore budget");
            Check(decoded.Version == wire.Version && decoded.Operation == operation &&
                decoded.Actor == 8 && decoded.RequestId == int.MaxValue && decoded.PackedTiles.Count == count,
                "all operations preserve header and count");
            for (int i = 0; i < count; i++)
                Check(decoded.GetTile(i).X == 799 && decoded.GetTile(i).Y == 799, "XY roundtrip at native bounds");
        }

        int[] source = { Pack(1, 2) };
        var frozen = Packet(tiles: source);
        source[0] = Pack(3, 4);
        Check(frozen.GetTile(0).X == 1 && frozen.GetTile(0).Y == 2, "packet copies caller buffer");
        var mutableView = (IList<int>)frozen.PackedTiles;
        try { mutableView[0] = Pack(3, 4); throw new Exception("Packet exposed mutable tiles."); }
        catch (NotSupportedException) { Check(true, "packet exposes read-only tile view"); }

        RejectWire(new byte[] { 0x90 }, "incomplete top-level array");
        RejectWire(MessagePack.MessagePackSerializer.Serialize(Packet(tiles: Array.Empty<int>())), "zero fields");
        RejectWire(MessagePack.MessagePackSerializer.Serialize(Packet(tiles: new int[LinePolicy.MaxTiles + 1])), "oversized line");
        // Header claims a large nested array but has no field bodies. The production
        // formatter must reject its declared bound before trying to read/allocate it.
        RejectWire(new byte[] { 0x95, 0x02, 0x01, 0x02, 0x01, 0xdd, 0x00, 0x10, 0x00, 0x00 },
            "untrusted oversized array header");
    }

    private static void TestCoordinator()
    {
        var api = new ModApiClient();
        bool common = true;
        var native = new NativePlacement();
        var network = new PitchLineNetwork(api, () => common, new BepInEx.Logging.ManualLogSource());
        var channel = (AuthenticatedChoreChannel<PitchLineNetworkPacket>)api.Network.Channel;
        network.SetNative(native);
        var mission = new MissionContext();
        network.SetMission(mission);
        var a = new LineTile(160, 271);
        var b = new LineTile(161, 271);
        var line = new[] { a, b, new LineTile(162, 271) };
        channel.SendBody = packet =>
        {
            native.Live.Add(native.Prepared.CommandId);
            return new NetworkSendResult(NetworkSendStatus.Submitted);
        };

        Check(network.TrySubmit(PitchLineOperation.Pitch, line, 2) == NativeSubmitResult.Submitted, "enqueue whole line");
        int firstCommand = native.Prepared.CommandId;
        var firstPacket = channel.Last!;
        Check(native.Executions.Count == 0 && channel.Sends == 1, "no speculative local execution");
        line[0] = new LineTile(10, 20);
        Check(firstPacket.GetTile(0).X == a.X, "queued packet independent of next input buffer");
        Check(network.TrySubmit(PitchLineOperation.PlannedMoat, new[] { b }, 2) == NativeSubmitResult.Submitted &&
            channel.Sends == 2 && native.Live.Contains(firstCommand), "second line admitted while first remains queued");
        int secondCommand = native.Prepared.CommandId;
        var secondPacket = channel.Last!;
        Check(secondPacket.RequestId != firstPacket.RequestId, "outstanding lines have distinct request identities");
        channel.Deliver(firstPacket, 2, firstCommand);
        channel.Deliver(secondPacket, 2, secondCommand);
        Check(native.Executions.Count == 4, "whole queued lines execute through one receive each");
        Check(native.Executions[0].X == a.X && native.Executions[1].X == b.X &&
            native.Executions[2].X == 162 && native.Executions[3].Operation == PitchLineOperation.PlannedMoat,
            "execution preserves field and request order");

        // A receiver with no local pending operation handles the authenticated issuer.
        native.Executions.Clear();
        channel.Deliver(Packet(actor: 3), 3, 300000099);
        Check(native.Executions.Count == 3 && native.Executions.TrueForAll(e => e.Sender == 3 && e.Actor == 3 &&
            e.Command == 300000099), "remote issuer bound to each field without local state");
        common = false;
        int before = native.Executions.Count;
        channel.Deliver(Packet(actor: 3), 3, 300000100);
        Check(native.Executions.Count == before, "multiplayer common agreement gates receive");
        mission.Mode.IsRealMultiplayer = false;
        channel.Deliver(Packet(actor: 3), 3, 300000101);
        Check(native.Executions.Count == before + 3, "singleplayer uses channel without peer agreement");
        mission.IsEditor = true;
        channel.Deliver(Packet(actor: 1), 2, 200000102);
        Check(native.Executions.Count == before + 6 && native.Executions[^1].Actor == 1 &&
            native.Executions[^1].Sender == 2, "editor pitch preserves captured actor separately from issuer");
        before = native.Executions.Count;
        channel.Deliver(Packet(PitchLineOperation.PlannedMoat, actor: 1), 2, 200000103);
        Check(native.Executions.Count == before, "moat cannot select foreign actor even in editor");
        mission.IsEditor = false;
        channel.Deliver(Packet(actor: 1), 2, 200000104);
        Check(native.Executions.Count == before, "foreign actor rejected outside editor");
        mission.Mode.IsRealMultiplayer = true;
        common = true;

        foreach (var invalid in new[]
        {
            Packet(version: 1), Packet(version: 3), Packet(request: 0), Packet(request: -1),
            Packet(actor: 0), Packet(actor: 9), Packet((PitchLineOperation)0), Packet((PitchLineOperation)4),
            Packet(tiles: Array.Empty<int>()), Packet(tiles: new int[LinePolicy.MaxTiles + 1]),
            Packet(tiles: new[] { Pack(800, 1) }), Packet(tiles: new[] { Pack(1, 800) }),
            Packet(tiles: new[] { -1 })
        })
        {
            before = native.Executions.Count;
            channel.Deliver(invalid, 2, 200000105);
            Check(native.Executions.Count == before, "invalid packet never mutates simulation");
        }

        foreach (PitchLineOperation operation in Enum.GetValues<PitchLineOperation>())
        {
            native.Executions.Clear();
            channel.Deliver(Packet(operation), 2, 200000106);
            Check(native.Executions.Count == 3 && native.Executions.TrueForAll(e => e.Operation == operation),
                "operation delivered for every field");
            var fullLine = new int[LinePolicy.MaxTiles];
            for (int i = 0; i < fullLine.Length; i++) fullLine[i] = Pack(160 + i, 271);
            native.Executions.Clear();
            channel.Deliver(Packet(operation, tiles: fullLine), 2, 200000106);
            Check(native.Executions.Count == LinePolicy.MaxTiles && native.Executions[0].X == 160 &&
                native.Executions[^1].X == 160 + LinePolicy.MaxTiles - 1 &&
                native.Executions.TrueForAll(e => e.Operation == operation && e.Command == 200000106),
                "maximum line executes all fields in one command scope");
        }
        native.Executions.Clear();
        native.Outcomes.Enqueue(LineReceipt.Skipped);
        native.Outcomes.Enqueue(LineReceipt.Accepted);
        native.Outcomes.Enqueue(LineReceipt.Accepted);
        channel.Deliver(Packet(), 2, 200000107);
        Check(native.Executions.Count == 3, "changed obstruction skips field and preserves remaining candidates");
        native.Executions.Clear();
        native.Outcomes.Enqueue(LineReceipt.Accepted);
        native.Outcomes.Enqueue(LineReceipt.Rejected);
        channel.Deliver(Packet(), 2, 200000108);
        Check(native.Executions.Count == 2 && native.Executions[0].X == 160 && native.Executions[1].X == 161,
            "shortage or failed confirmation stops only remaining suffix");
        native.Allowed = false;
        before = native.Executions.Count;
        channel.Deliver(Packet(), 2, 200000109);
        Check(native.Executions.Count == before, "mode/operation mismatch cannot execute");
        native.Allowed = true;

        channel.SendBody = packet =>
        {
            channel.Deliver(packet, 2, native.Prepared.CommandId);
            return new NetworkSendResult(NetworkSendStatus.Submitted);
        };
        native.Executions.Clear();
        Check(network.TrySubmit(PitchLineOperation.CompletedMoat, new[] { a, b }, 2) == NativeSubmitResult.Submitted &&
            native.Executions.Count == 2, "immediate local execution within Send handles whole batch");
        mission.Mode.IsRealMultiplayer = false;
        mission.IsEditor = true;
        native.CapturedActor = 1;
        Check(network.TrySubmit(PitchLineOperation.Pitch, new[] { a }, 2) == NativeSubmitResult.Submitted &&
            channel.Last!.Actor == 1, "submission captures editor actor");
        native.CapturedActor = 3;
        channel.Deliver(channel.Last!, 2, 200000110);
        Check(native.Executions[^1].Actor == 1, "later editor selection cannot rewrite packet actor");
        mission.IsEditor = false;
        mission.Mode.IsRealMultiplayer = true;
        native.CapturedActor = 0;

        channel.SendBody = packet =>
        {
            native.Live.Add(native.Prepared.CommandId);
            return new NetworkSendResult(NetworkSendStatus.TransportFailed);
        };
        int sends = channel.Sends;
        Check(network.TrySubmit(PitchLineOperation.Pitch, new[] { a }, 2) == NativeSubmitResult.Submitted &&
            channel.Sends == sends + 1, "queue identity proves enqueue despite transport exception");
        channel.SendBody = packet => new NetworkSendResult(NetworkSendStatus.TransportFailed);
        sends = channel.Sends;
        Check(network.TrySubmit(PitchLineOperation.Pitch, new[] { a }, 2) == NativeSubmitResult.Rejected &&
            channel.Sends == sends + 1, "ambiguous failure performs exactly one attempt");
        native.CanPrepare = false;
        sends = channel.Sends;
        Check(network.TrySubmit(PitchLineOperation.Pitch, new[] { a }, 2) == NativeSubmitResult.Busy &&
            channel.Sends == sends, "full queue never reaches transport");
        native.CanPrepare = true;
        channel.IsAvailable = false;
        Check(network.TrySubmit(PitchLineOperation.Pitch, new[] { a }, 2) == NativeSubmitResult.Rejected &&
            channel.Sends == sends, "unavailable channel rejects without transport");
        channel.IsAvailable = true;

        Check(network.TrySubmit(PitchLineOperation.Pitch, Array.Empty<LineTile>(), 2) == NativeSubmitResult.Rejected &&
            network.TrySubmit(PitchLineOperation.Pitch, new LineTile[LinePolicy.MaxTiles + 1], 2) == NativeSubmitResult.Rejected,
            "submission rejects empty/oversized lines");
        sends = channel.Sends;
        foreach (var outside in new[] { new LineTile(65536, 1), new LineTile(1, 65536),
            new LineTile(-1, 1), new LineTile(1, -1), new LineTile(800, 1), new LineTile(1, 800) })
            Check(network.TrySubmit(PitchLineOperation.Pitch, new[] { outside }, 2) == NativeSubmitResult.Rejected &&
                channel.Sends == sends, "original coordinates cannot alias a valid packed tile");
        native.CanCapture = false;
        Check(network.TrySubmit(PitchLineOperation.Pitch, new[] { a }, 2) == NativeSubmitResult.Rejected,
            "uncaptured actor prevents enqueue");
        native.CanCapture = true;
        native.BatchReady = false;
        before = native.Executions.Count;
        channel.Deliver(Packet(), 2, 200000111);
        Check(native.Executions.Count == before && !network.NativeReady, "unready native adapter cannot receive");
        native.BatchReady = true;
        network.SetMission(null!);
        before = native.Executions.Count;
        channel.Deliver(Packet(), 2, 200000112);
        Check(native.Executions.Count == before &&
            network.TrySubmit(PitchLineOperation.Pitch, new[] { a }, 2) == NativeSubmitResult.Rejected,
            "mission teardown rejects stale receive and new submissions");
    }
}

namespace APIShared.GameModes
{
    internal sealed class MissionContext { internal readonly ModeSnapshot Mode = new ModeSnapshot(); internal bool IsEditor; }
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
        internal NetworkSendResult Send(T packet)
        {
            if (!options.IsEnabled() || !options.ValidatePayload(packet)) throw new Exception("Invalid fake transport entry.");
            Sends++; Last = packet; return SendBody(packet);
        }
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
namespace SHCDESE.API
{
    internal static class GameTileManagerAPI { internal const int MAX_WIDTH = 800, MAX_HEIGHT = 800; }
    internal static class GamePlayerManagerAPI { internal const int MAX_PLAYERS = 8; }
}
namespace SHCDESE.Interop.Enums { internal enum ChoreType : byte { ScriptExtenderChore = 106 } }
namespace BugfixesAndQoL.PitchMoatLines
{
    internal enum LineReceipt { Pending, Accepted, Rejected, Skipped }
    internal enum NativeSubmitResult { Submitted, Busy, Rejected }
    internal sealed class NativePlacement
    {
        internal bool BatchReady = true, CanPrepare = true, CanCapture = true, Allowed = true;
        internal int CapturedActor;
        internal readonly HashSet<int> Live = new HashSet<int>();
        internal readonly List<Execution> Executions = new List<Execution>();
        internal readonly Queue<LineReceipt> Outcomes = new Queue<LineReceipt>();
        internal PendingNativeCommand Prepared;
        internal readonly record struct Execution(PitchLineOperation Operation, int X, int Sender, int Actor, int Command);
        internal readonly struct PendingNativeCommand
        { internal readonly int CommandId; internal PendingNativeCommand(int id) { CommandId = id; } }
        internal bool TryGetBatchActor(int sender, out int actor) { actor = CapturedActor == 0 ? sender : CapturedActor; return CanCapture; }
        internal bool IsBatchActorAllowed(int sender, int actor, bool isEditor) => sender >= 1 && sender <= 8 &&
            actor >= 1 && actor <= 8 && (isEditor || actor == sender);
        internal bool IsBatchOperationAllowed(PitchLineOperation operation) => Allowed;
        internal bool TryPrepareCommand(byte opcode, int player, out PendingNativeCommand command)
        { command = Prepared = new PendingNativeCommand(Prepared.CommandId + 1); return CanPrepare; }
        internal bool IsCommandPending(PendingNativeCommand command) => Live.Contains(command.CommandId);
        internal LineReceipt ExecuteBatchTile(PitchLineOperation operation, LineTile tile, int sender, int actor, int command)
        {
            Executions.Add(new Execution(operation, tile.X, sender, actor, command));
            return Outcomes.Count > 0 ? Outcomes.Dequeue() : LineReceipt.Accepted;
        }
    }
}
