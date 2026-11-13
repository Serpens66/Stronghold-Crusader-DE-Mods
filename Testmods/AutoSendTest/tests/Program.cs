using System;
using System.IO;
using AutoSendTest;

internal static class Program
{
    // Arbitrary sparse fixture keys verify this domain never assumes native enum offsets.
    private const int Resource = 41, OtherResource = 901;
    private static int checks;
    private static void Main()
    {
        EqualShareAndCappedRedistribution(); EmergencyTier(); RemainderRotation(); ReserveAndCapacity();
        PauseAndInvalidPlayers(); CycleAdmission(); MultipleSenders(); KnownPartialAndVeto(); UncertaintyStopsEntireRun();
        RechecksBeforeExecution(); SaveRoundTrip(); MalformedSaves(); ForgedRuleSaves(); SavedBundleIsAtomic(); EpochRequiresAuthenticatedLocalIdentity(); RendezvousRequiresEveryExpectedPeer(); InitialRosterDoesNotUseLaterLossState(); BoundedWork(); DormantRulesDoNotReadWorld();
        checks += ProbeTests.Run();
        checks += EditTests.Run();
        Console.WriteLine("AutoSend policy: " + checks + " checks passed.");
    }
    private static AutoSendState State(int batch = 100) => new AutoSendState(new[] { Resource, OtherResource }, new[] { batch, 1000 });
    private static void Rule(AutoSendState state, int sender, int recipient, int minimum, int maximum, int good = Resource)
    { Check(state.TrySetRule(sender, recipient, good, minimum, maximum, true, out string reason), reason); }
    private static void EqualShareAndCappedRedistribution()
    {
        AutoSendState state = State(); var world = new World();
        world.Stock[1] = 120; Rule(state, 1, 2, 0, 100); Rule(state, 1, 3, 0, 100); Rule(state, 1, 4, 0, 100);
        AutoSendRunResult result = new AutoSendPlanner(state).Run(world);
        Equal(40, world.Stock[2]); Equal(40, world.Stock[3]); Equal(40, world.Stock[4]); Equal(3, result.Transfers); Equal(120L, result.Units);
        state = State(); world = new World(); world.Stock[1] = 120;
        Rule(state, 1, 2, 0, 10); Rule(state, 1, 3, 0, 100); Rule(state, 1, 4, 0, 100);
        new AutoSendPlanner(state).Run(world);
        Equal(10, world.Stock[2]); Equal(55, world.Stock[3]); Equal(55, world.Stock[4]);
    }
    private static void EmergencyTier()
    {
        AutoSendState state = State(); var world = new World(); world.Stock[1] = 40;
        Rule(state, 1, 2, 30, 100); Rule(state, 1, 3, 10, 100); Rule(state, 1, 4, 0, 100);
        new AutoSendPlanner(state).Run(world);
        Equal(30, world.Stock[2]); Equal(10, world.Stock[3]); Equal(0, world.Stock[4]);
        world.Stock[1] = 30; new AutoSendPlanner(state).Run(world);
        Equal(40, world.Stock[2]); Equal(20, world.Stock[3]); Equal(10, world.Stock[4]);
    }
    private static void RemainderRotation()
    {
        AutoSendState state = State(); var world = new World();
        Rule(state, 1, 2, 0, 100); Rule(state, 1, 3, 0, 100); Rule(state, 1, 4, 0, 100);
        var planner = new AutoSendPlanner(state);
        for (int round = 0; round < 6; round++) { world.Stock[1] = 1; planner.Run(world); }
        Equal(2, world.Stock[2]); Equal(2, world.Stock[3]); Equal(2, world.Stock[4]);
    }
    private static void ReserveAndCapacity()
    {
        AutoSendState state = State(); var world = new World(); world.Stock[1] = 200;
        Check(state.TrySetReserve(1, Resource, 150, out _));
        Rule(state, 1, 2, 0, 100); Rule(state, 1, 3, 0, 100); world.Free[2] = 5;
        new AutoSendPlanner(state).Run(world);
        Equal(150, world.Stock[1]); Equal(5, world.Stock[2]); Equal(45, world.Stock[3]);
        world.Stock[1] = 200; world.Free[2] = -1; world.Free[3] = 0;
        new AutoSendPlanner(state).Run(world); Equal(200, world.Stock[1]);
        Check(!state.TrySetReserve(1, Resource, -1, out _)); Equal(150, state.GetReserve(1, Resource));
        Check(!state.TrySetRule(1, 2, Resource, 101, 100, true, out _)); Equal(100, state.GetRule(1, 2, Resource).Maximum);
        Check(!state.TrySetRule(1, 2, Resource, 0, 0, true, out _));
    }
    private static void PauseAndInvalidPlayers()
    {
        AutoSendState state = State(); var world = new World(); world.Stock[1] = 100;
        Rule(state, 1, 2, 0, 100); Check(state.TrySetPaused(1, true, out _));
        new AutoSendPlanner(state).Run(world); Equal(0, world.Calls);
        Check(state.TrySetPaused(1, false, out _)); world.Allied[2] = false;
        new AutoSendPlanner(state).Run(world); Equal(0, world.Calls);
        Check(!state.TrySetRule(1, 1, Resource, 0, 100, true, out _));
        Check(!state.TrySetRule(0, 2, Resource, 0, 100, true, out _));
        Check(!state.TrySetRule(1, 9, Resource, 0, 100, true, out _));
        Check(!state.TrySetReserve(1, 17, 5, out _));
    }
    private static void CycleAdmission()
    {
        AutoSendState state = State(); Rule(state, 1, 2, 0, 100); Rule(state, 2, 3, 0, 100);
        long revision = state.GetRevision(3);
        Check(!state.TrySetRule(3, 1, Resource, 0, 100, true, out _));
        Equal(revision, state.GetRevision(3)); Check(!state.GetRule(3, 1, Resource).Enabled);
        Check(state.TrySetPaused(1, true, out _));
        Check(!state.TrySetRule(2, 1, Resource, 0, 100, true, out _));
        Rule(state, 3, 1, 0, 100, OtherResource);
        Check(state.TrySetRule(2, 3, Resource, 0, 100, false, out _)); Rule(state, 3, 1, 0, 100);
    }
    private static void MultipleSenders()
    {
        AutoSendState state = State(); var world = new World();
        world.Stock[1] = 100; world.Stock[2] = 100; Rule(state, 1, 3, 0, 100); Rule(state, 2, 3, 0, 100);
        new AutoSendPlanner(state).Run(world); Equal(100, world.Stock[3]); Equal(100, world.Stock[1] + world.Stock[2]);
        world.Stock[3] = 0; world.Stock[1] = 100; world.Stock[2] = 100;
        new AutoSendPlanner(state).Run(world); Equal(100, world.Stock[1]); Equal(0, world.Stock[2]);
    }
    private static void KnownPartialAndVeto()
    {
        AutoSendState state = State(); var world = new World(); world.Stock[1] = 90; world.Partial = true;
        Rule(state, 1, 2, 0, 100); Rule(state, 1, 3, 0, 100); Rule(state, 1, 4, 0, 100);
        AutoSendRunResult result = new AutoSendPlanner(state).Run(world);
        Equal(3, world.Calls); Equal(0, result.UncertainTransfers); Equal(3, result.Transfers); Equal(45, result.Units);
        state = State(); world = new World(); world.Stock[1] = 90; world.Veto = true;
        Rule(state, 1, 2, 0, 100); Rule(state, 1, 3, 0, 100);
        result = new AutoSendPlanner(state).Run(world);
        Equal(2, world.Calls); Equal(0, result.UncertainTransfers); Equal(0, result.Transfers); Equal(90, world.Stock[1]);
    }
    private static void UncertaintyStopsEntireRun()
    {
        var invalid = new[] {
            new AutoSendTransferResult(45, 45, false),
            new AutoSendTransferResult(-1, -1, true),
            new AutoSendTransferResult(46, 46, true),
            new AutoSendTransferResult(45, 44, true)
        };
        foreach (AutoSendTransferResult reported in invalid)
        {
            AutoSendState state = State(); var world = new World();
            world.Stock[1] = 90; world.Stock[2] = 90; world.Reported = reported;
            Rule(state, 1, 3, 0, 100); Rule(state, 1, 4, 0, 100); Rule(state, 2, 5, 0, 100);
            AutoSendRunResult result = new AutoSendPlanner(state).Run(world);
            Equal(1, world.Calls); Equal(1, result.UncertainTransfers); Equal(0, result.Transfers);
        }
    }
    private static void RechecksBeforeExecution()
    {
        AutoSendState state = State(); var world = new World(); world.Stock[1] = 100;
        Check(state.TrySetReserve(1, Resource, 25, out _));
        Rule(state, 1, 2, 0, 100); Rule(state, 1, 3, 0, 100);
        world.AfterFirst = () => { world.Stock[1] = 30; if (world.Stock[2] > 0) world.Stock[3] = 99; else world.Stock[2] = 99; };
        new AutoSendPlanner(state).Run(world); Equal(29, world.Stock[1]); Check(world.Stock[2] == 100 || world.Stock[3] == 100);
    }
    private static void SaveRoundTrip()
    {
        AutoSendState state = State(); Rule(state, 1, 2, 31, 123); Rule(state, 2, 3, 10, 500, OtherResource);
        Check(state.TrySetReserve(1, Resource, 987, out _)); Check(state.TrySetPaused(2, true, out _));
        state.SetCursor(1, Resource, 5); state.SenderCursor = 6; state.MarkProcessedTick(int.MinValue + 8);
        byte[] bytes = AutoSendSaveCodec.Encode(state);
        Check(AutoSendSaveCodec.TryDecode(bytes, new[] { OtherResource, Resource }, new[] { 1000, 100 }, out AutoSendState restored, out string reason), reason);
        Equal(987, restored.GetReserve(1, Resource)); Equal(31, restored.GetRule(1, 2, Resource).Minimum);
        Check(restored.IsPaused(2)); Equal(state.GetRevision(1), restored.GetRevision(1)); Equal(5, restored.GetCursor(1, Resource));
        Equal(6, restored.SenderCursor); Check(restored.HasProcessedTick); Equal(state.LastProcessedTick, restored.LastProcessedTick);
        restored.RestoreMeta(1, false, long.MaxValue);
        Check(!restored.TrySetReserve(1, Resource, 1, out _)); Equal(987, restored.GetReserve(1, Resource));
    }
    private static void MalformedSaves()
    {
        byte[] bytes = AutoSendSaveCodec.Encode(State());
        for (int length = 0; length < bytes.Length; length++)
        {
            var truncated = new byte[length]; Array.Copy(bytes, truncated, length);
            Check(!AutoSendSaveCodec.TryDecode(truncated, new[] { Resource, OtherResource }, new[] { 100, 1000 }, out _, out _));
        }
        byte[] corrupt = (byte[])bytes.Clone(); corrupt[4] = 9;
        Check(!Decode(corrupt));
        corrupt = (byte[])bytes.Clone(); corrupt[12] = 9; Check(!Decode(corrupt));
        corrupt = (byte[])bytes.Clone(); corrupt[16] = 2; Check(!Decode(corrupt));
        corrupt = new byte[bytes.Length + 1]; Array.Copy(bytes, corrupt, bytes.Length); Check(!Decode(corrupt));
        Check(!AutoSendSaveCodec.TryDecode(bytes, new[] { 41, 902 }, new[] { 100, 1000 }, out _, out _));
    }
    private static bool Decode(byte[] bytes) => AutoSendSaveCodec.TryDecode(bytes,
        new[] { Resource, OtherResource }, new[] { 100, 1000 }, out _, out _);
    private static void EpochRequiresAuthenticatedLocalIdentity()
    {
        var epochs = new AutoSendEpochs(); const long localEpoch = 987654321;
        Check(epochs.Bind(2, localEpoch, 1, localEpoch, out bool acknowledged)); Check(!acknowledged);
        Check(epochs.Bind(1, localEpoch, 1, localEpoch, out acknowledged)); Check(acknowledged);
        Check(!epochs.Bind(1, localEpoch + 1, 1, localEpoch, out acknowledged)); Check(!acknowledged);
        Equal(localEpoch, epochs.Get(1));
        Check(epochs.Bind(1, localEpoch, 1, localEpoch, out acknowledged)); Check(acknowledged);
        Check(!epochs.Bind(2, localEpoch + 1, 1, localEpoch, out acknowledged)); Check(!acknowledged);
        Equal(localEpoch, epochs.Get(2));
        Check(epochs.Bind(3, localEpoch, -1, localEpoch, out acknowledged)); Check(!acknowledged);
        Check(epochs.Bind(4, localEpoch, 4, localEpoch + 1, out acknowledged)); Check(!acknowledged);
        Check(epochs.Bind(5, long.MinValue, 1, localEpoch, out acknowledged)); Check(!acknowledged);
        Equal(long.MinValue, epochs.Get(5));
        foreach (int invalid in new[] { int.MinValue, -1, 0, 9, int.MaxValue })
        {
            Check(!epochs.Bind(invalid, localEpoch, invalid, localEpoch, out acknowledged)); Check(!acknowledged);
            Equal(0, epochs.Get(invalid));
        }
        Check(!epochs.Bind(6, 0, 6, 0, out acknowledged)); Check(!acknowledged); Equal(0, epochs.Get(6));
        epochs.Reset();
        for (int sender = 1; sender <= AutoSendState.PlayerCount; sender++) Equal(0, epochs.Get(sender));
        Check(epochs.Bind(1, localEpoch + 1, 1, localEpoch + 1, out acknowledged)); Check(acknowledged);
    }
    private static void RendezvousRequiresEveryExpectedPeer()
    {
        const int mask = (1 << 1) | (1 << 2) | (1 << 4);
        var first = new AutoSendEpochs(); var second = new AutoSendEpochs();
        Check(!first.AllKnown(mask)); Equal(0, first.Fingerprint(mask)); Check(!first.IsReady(mask));
        Check(first.Bind(1, 11, 1, 11, out _)); Check(first.Bind(4, 44, 1, 11, out _));
        Check(!first.AcknowledgeReady(1, 11, mask, 1)); Check(!first.AllKnown(mask));
        Check(first.Bind(2, -22, 1, 11, out _));
        Check(second.Bind(2, -22, 2, -22, out _)); Check(second.Bind(1, 11, 2, -22, out _)); Check(second.Bind(4, 44, 2, -22, out _));
        Check(first.AllKnown(mask) && second.AllKnown(mask));
        long digest = first.Fingerprint(mask); Check(digest > 0); Equal(digest, second.Fingerprint(mask));
        Check(first.Bind(3, 333, 1, 11, out _)); Equal(digest, first.Fingerprint(mask));
        Check(!first.AcknowledgeReady(1, 12, mask, digest));
        Check(!first.AcknowledgeReady(1, 11, mask, digest == long.MaxValue ? digest - 1 : digest + 1));
        Check(!first.AcknowledgeReady(1, 11, mask, 0)); Check(!first.AcknowledgeReady(1, 11, mask, -1));
        Check(!first.AcknowledgeReady(3, 333, mask, digest));
        Check(first.AcknowledgeReady(1, 11, mask, digest)); Check(!first.IsReady(mask));
        int differentRoster = (1 << 1) | (1 << 2);
        Check(!first.AcknowledgeReady(2, -22, differentRoster, first.Fingerprint(differentRoster)));
        Check(!first.IsReady(differentRoster));
        Check(first.AcknowledgeReady(2, -22, mask, digest)); Check(!first.IsReady(mask));
        Check(first.AcknowledgeReady(4, 44, mask, digest)); Check(first.IsReady(mask));
        Check(first.AcknowledgeReady(4, 44, mask, digest)); Check(first.IsReady(mask));
        Check(second.AcknowledgeReady(4, 44, mask, digest)); Check(second.AcknowledgeReady(2, -22, mask, digest));
        Check(!second.IsReady(mask)); Check(second.AcknowledgeReady(1, 11, mask, digest)); Check(second.IsReady(mask));
        foreach (int invalid in new[] { 0, 1, mask | 1, mask | (1 << 9), -1, int.MaxValue })
        {
            Check(!first.AllKnown(invalid)); Equal(0, first.Fingerprint(invalid)); Check(!first.IsReady(invalid));
            Check(!first.AcknowledgeReady(1, 11, invalid, digest));
        }
        first.Reset(); Check(!first.AllKnown(mask)); Equal(0, first.Fingerprint(mask)); Check(!first.IsReady(mask));
        Check(first.Bind(1, 11, 1, 11, out _)); Check(first.Bind(2, -22, 1, 11, out _)); Check(first.Bind(4, 44, 1, 11, out _));
        Equal(digest, first.Fingerprint(mask)); Check(!first.IsReady(mask));
    }
    private static void InitialRosterDoesNotUseLaterLossState()
    {
        var firstPeer = new AutoSendInitialRoster(); var secondPeer = new AutoSendInitialRoster();
        var humans = new ulong[9]; humans[1] = 17; humans[2] = ulong.MaxValue;
        Check(!firstPeer.TryGet(100, humans, out int mask)); Equal(0, mask);
        Check(!firstPeer.Capture(100, 0));
        firstPeer.Reset(100); secondPeer.Reset(100);
        Check(firstPeer.Capture(100, 0)); Check(secondPeer.Capture(100, 0));
        Check(firstPeer.TryGet(100, humans, out int initialMask)); Equal((1 << 1) | (1 << 2), initialMask);
        // A later observation of player two's defeat cannot replace the frozen
        // initialization snapshot, even on a peer whose first tick arrives later.
        Check(!secondPeer.Capture(100, 1 << 2));
        Check(secondPeer.TryGet(100, humans, out int delayedPeerMask)); Equal(initialMask, delayedPeerMask);
        Check(secondPeer.Capture(100, 0));
        Check(!secondPeer.Capture(99, 0)); Check(!secondPeer.TryGet(99, humans, out mask)); Equal(0, mask);
        secondPeer.Reset(101); Check(!secondPeer.TryGet(100, humans, out mask)); Equal(0, mask);
        Check(!secondPeer.TryGet(101, humans, out mask)); Equal(0, mask);
        Check(!secondPeer.Capture(100, 0)); Check(secondPeer.Capture(101, 1 << 2));
        Check(secondPeer.TryGet(101, humans, out mask)); Equal(1 << 1, mask);
        Check(!secondPeer.TryGet(101, null, out mask)); Equal(0, mask);
        Check(!secondPeer.TryGet(101, new ulong[8], out mask)); Equal(0, mask);
        var invalid = (ulong[])humans.Clone(); invalid[0] = 1;
        Check(!secondPeer.TryGet(101, invalid, out mask)); Equal(0, mask);
        foreach (int invalidLosses in new[] { 1, 1 << 9, -1, int.MaxValue })
        {
            firstPeer.Reset(102); Check(!firstPeer.Capture(102, invalidLosses));
            Check(!firstPeer.TryGet(102, humans, out mask)); Equal(0, mask);
        }
        firstPeer.Reset(103); Check(firstPeer.Capture(103, (1 << 1) | (1 << 2)));
        Check(firstPeer.TryGet(103, humans, out mask)); Equal(0, mask);
    }
    private static void SavedBundleIsAtomic()
    {
        AutoSendState original = State(); Rule(original, 1, 2, 20, 100);
        byte[] rules = AutoSendSaveCodec.Encode(original);
        var lords = new uint[AutoSendState.PlayerCount + 1]; var humans = new ulong[AutoSendState.PlayerCount + 1];
        lords[1] = uint.MaxValue; lords[8] = 17; humans[1] = ulong.MaxValue;
        byte[] players = AutoSendSavedPlayers.EncodePlayers(lords, humans);
        Check(AutoSendSavedPlayers.TryRead(rules, players, new[] { Resource, OtherResource }, new[] { 100, 1000 },
            out AutoSendState state, out uint[] restoredLords, out ulong[] restoredHumans, out _));
        Equal(20, state.GetRule(1, 2, Resource).Minimum);
        Check(restoredLords[1] == uint.MaxValue && restoredHumans[1] == ulong.MaxValue);
        Check(restoredLords[8] == 17 && restoredLords[0] == 0 && restoredHumans[0] == 0);
        // Read the encoded contract itself, not just symmetric round-trip code.
        Check(players.Length == 9 * (sizeof(uint) + sizeof(ulong)));
        Check(players[12] == 255 && players[13] == 255 && players[14] == 255 && players[15] == 255);
        Check(players[96] == 17 && players[97] == 0 && players[98] == 0 && players[99] == 0);
        CheckBundleRejected(null, players); CheckBundleRejected(Array.Empty<byte>(), players);
        byte[] badRules = (byte[])rules.Clone(); badRules[4] = 9; CheckBundleRejected(badRules, players);
        CheckBundleRejected(rules, null);
        for (int length = 0; length < players.Length; length++)
        { var shorter = new byte[length]; Array.Copy(players, shorter, length); CheckBundleRejected(rules, shorter); }
        var longer = new byte[players.Length + 1]; Array.Copy(players, longer, players.Length); CheckBundleRejected(rules, longer);
        byte[] badPlayers = (byte[])players.Clone(); badPlayers[0] = 1; CheckBundleRejected(rules, badPlayers);
        badPlayers = (byte[])players.Clone(); badPlayers[4] = 1; CheckBundleRejected(rules, badPlayers);
        bool threw = false;
        try { AutoSendSavedPlayers.EncodePlayers(new uint[8], humans); } catch (ArgumentException) { threw = true; }
        Check(threw);
        threw = false; lords[0] = 1;
        try { AutoSendSavedPlayers.EncodePlayers(lords, humans); } catch (ArgumentException) { threw = true; }
        Check(threw);
    }
    private static void CheckBundleRejected(byte[] rules, byte[] players)
    {
        Check(!AutoSendSavedPlayers.TryRead(rules, players, new[] { Resource, OtherResource }, new[] { 100, 1000 },
            out AutoSendState state, out uint[] lords, out ulong[] humans, out string reason));
        Check(state == null && lords == null && humans == null && !string.IsNullOrEmpty(reason));
    }
    private static void ForgedRuleSaves()
    {
        AutoSendState state = State(); Rule(state, 1, 2, 0, 100); Rule(state, 2, 3, 0, 100);
        Check(state.TrySetPaused(1, true, out _));
        byte[] bytes = AutoSendSaveCodec.Encode(state);
        Check(!Decode(RewriteRule(bytes, 3, 1, 0, 100, true)));
        Check(!Decode(RewriteRule(bytes, 1, 1, 0, 100, true)));
        Check(!Decode(RewriteRule(bytes, 1, 2, 101, 100, true)));
        Check(!Decode(RewriteRule(bytes, 1, 2, 0, 0, true)));
    }
    // Modify an actual serialized fixture to verify untrusted data is validated
    // independently of the public state setters, including the complete graph.
    private static byte[] RewriteRule(byte[] encoded, int wantedSender, int wantedRecipient, int minimum, int maximum, bool enabled)
    {
        var changed = (byte[])encoded.Clone();
        using (var stream = new MemoryStream(changed, true))
        using (var reader = new BinaryReader(stream))
        {
            reader.ReadInt32(); reader.ReadInt32(); int goodsCount = reader.ReadInt32(); reader.ReadInt32(); reader.ReadBoolean(); reader.ReadInt32();
            for (int player = 1; player <= AutoSendState.PlayerCount; player++) { reader.ReadBoolean(); reader.ReadInt64(); }
            for (int g = 0; g < goodsCount; g++)
            {
                int good = reader.ReadInt32();
                for (int sender = 1; sender <= AutoSendState.PlayerCount; sender++)
                {
                    reader.ReadInt32(); reader.ReadInt32();
                    for (int recipient = 1; recipient <= AutoSendState.PlayerCount; recipient++)
                    {
                        long position = stream.Position;
                        reader.ReadInt32(); reader.ReadInt32(); reader.ReadBoolean();
                        if (good != Resource || sender != wantedSender || recipient != wantedRecipient) continue;
                        stream.Position = position;
                        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
                        { writer.Write(minimum); writer.Write(maximum); writer.Write(enabled); }
                        return changed;
                    }
                }
            }
        }
        throw new InvalidOperationException("Fixture rule not found.");
    }
    private static void BoundedWork()
    {
        AutoSendState state = State(); var world = new World(); world.Stock[1] = int.MaxValue;
        for (int p = 2; p <= AutoSendState.PlayerCount; p++) Rule(state, 1, p, int.MaxValue, int.MaxValue);
        new AutoSendPlanner(state).Run(world);
        Equal(7, world.Calls); Equal(int.MaxValue - 700, world.Stock[1]);
        for (int p = 2; p <= AutoSendState.PlayerCount; p++) Equal(100, world.Stock[p]);
    }
    private static void DormantRulesDoNotReadWorld()
    {
        AutoSendState state = State(); var world = new World();
        new AutoSendPlanner(state).Run(world); Equal(0, world.StockReads);
        Rule(state, 1, 2, 0, 100); Check(state.HasActiveRules(1, Resource));
        Check(state.TrySetRule(1, 2, Resource, 0, 100, false, out _)); Check(!state.HasActiveRules(1, Resource));
        new AutoSendPlanner(state).Run(world); Equal(0, world.StockReads);
    }
    private static void Check(bool success, string message = null)
    { checks++; if (!success) throw new InvalidOperationException(message ?? "Assertion failed."); }
    private static void Equal(long wanted, long actual)
    { Check(wanted == actual, "Expected " + wanted + ", got " + actual + "."); }
    private sealed class World : IAutoSendWorld
    {
        internal readonly int[] Stock = new int[AutoSendState.PlayerCount + 1];
        internal readonly int[] Free = new int[AutoSendState.PlayerCount + 1];
        internal readonly bool[] Allied = new bool[AutoSendState.PlayerCount + 1];
        internal bool Partial, Veto;
        internal AutoSendTransferResult? Reported;
        internal int Calls, StockReads;
        internal Action AfterFirst;
        internal World() { for (int p = 1; p <= AutoSendState.PlayerCount; p++) { Free[p] = int.MaxValue; Allied[p] = true; } }
        public int GetStock(int player, int good) { StockReads++; return good == Resource ? Stock[player] : 0; }
        public int GetFreeCapacity(int player, int good) => Free[player];
        public bool IsAlliedLiving(int sender, int recipient) => sender != recipient && Allied[sender] && Allied[recipient];
        public AutoSendTransferResult Transfer(int sender, int recipient, int good, int amount)
        {
            Calls++; int applied = Veto ? 0 : Partial ? amount / 2 : amount;
            Stock[sender] -= applied; Stock[recipient] += applied; Free[recipient] -= applied;
            if (Calls == 1) AfterFirst?.Invoke();
            return Reported ?? new AutoSendTransferResult(applied, applied, true);
        }
    }
}
