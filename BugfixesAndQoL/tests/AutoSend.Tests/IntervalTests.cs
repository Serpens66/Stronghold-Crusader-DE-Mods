using System;
using System.IO;
using BugfixesAndQoL.AutoSend;

internal static class IntervalTests
{
    private const int Good = 41, Other = 901;
    private static int checks;
    internal static int Run()
    {
        ExactDeadlineAndEdits(); FairDueRecipients(); IndependentGoods(); SaveRemainingPhase(); RepairAfterDeliveryDoesNotRepeat(); RejectInvalidSavedSchedules(); LegacySchedules();
        WrapAndNoCatchup(); IntervalQueue(); NoDueDoesNotReadOrAllocate(); return checks;
    }
    private static void Check(bool condition) { checks++; if (!condition) throw new Exception("Interval regression " + checks + " failed."); }
    private static AutoSendState State() => new AutoSendState(new[] { Good, Other }, new[] { 100, 1000 });
    private static void Set(AutoSendState state, int recipient, int seconds, int tick, int min = 0, int max = 100, bool on = true, int good = Good)
        => Check(state.TrySetRule(1, recipient, good, min, max, on, seconds, tick, out _));
    private static void ExactDeadlineAndEdits()
    {
        var state = State(); var world = new World(); var planner = new AutoSendPlanner(state);
        Check(state.GetRule(1, 2, Good).IntervalSeconds == 30 && state.GetRule(1, 2, Good).IntervalTicks == 1200);
        Set(state, 2, 1, 17); world.Stock[1, 0] = 100;
        planner.Run(world, 56); Check(world.Reads == 0);
        Set(state, 2, 1, 33, min: 10); Check(state.GetRule(1, 2, Good).LastCheckTick == 17);
        planner.Run(world, 57); Check(world.Calls == 1 && world.Stock[2, 0] == 100);
        Set(state, 2, 2, 60); Check(state.GetRule(1, 2, Good).LastCheckTick == 60);
        world.Stock[1, 0] = 100; world.Stock[2, 0] = 0; int reads = world.Reads;
        planner.Run(world, 139); Check(world.Reads == reads); planner.Run(world, 140); Check(world.Calls == 2);
        Set(state, 2, 2, 141, on: false); Check(!state.GetRule(1, 2, Good).HasCheckTick);
        Set(state, 2, 2, 150); world.Stock[1, 0] = 100; world.Stock[2, 0] = 0;
        planner.Run(world, 229); Check(world.Calls == 2); planner.Run(world, 230); Check(world.Calls == 3);
        long revision = state.GetRevision(1); var old = state.GetRule(1, 2, Good);
        foreach (int seconds in new[] { int.MinValue, -1, 0, 301, int.MaxValue })
        {
            Check(!state.TrySetRule(1, 2, Good, 0, 1, true, seconds, 300, out _));
            Check(state.GetRevision(1) == revision && state.GetRule(1, 2, Good).LastCheckTick == old.LastCheckTick);
        }
        Set(state, 2, 300, 300); Check(state.GetRule(1, 2, Good).IntervalTicks == 12000);
        Check(AutoSendRuleEdits.TryApply(state.GetRule(1, 2, Good), AutoSendEdit.Maximum, 0, false, out var stopped, out _));
        Check(!stopped.Enabled && stopped.IntervalSeconds == 300);
    }
    private static void FairDueRecipients()
    {
        var state = State(); var world = new World(); var planner = new AutoSendPlanner(state);
        Check(state.TrySetReserve(1, Good, 100, out _)); Set(state, 2, 1, 0); Set(state, 3, 1, 0); Set(state, 4, 2, 0);
        world.Stock[1, 0] = 160; planner.Run(world, 40);
        Check(world.Stock[1, 0] == 100 && world.Stock[2, 0] == 30 && world.Stock[3, 0] == 30 && world.Stock[4, 0] == 0);
        world.Stock[1, 0] = 160; world.Stock[2, 0] = world.Stock[3, 0] = 0; planner.Run(world, 80);
        Check(world.Stock[1, 0] == 100 && world.Stock[2, 0] == 20 && world.Stock[3, 0] == 20 && world.Stock[4, 0] == 20);
        state = State(); world = new World(); planner = new AutoSendPlanner(state);
        Check(state.TrySetReserve(1, Good, 100, out _)); Set(state, 2, 1, 0, min: 60); Set(state, 3, 1, 0);
        world.Stock[1, 0] = 160; planner.Run(world, 40); Check(world.Stock[2, 0] == 60 && world.Stock[3, 0] == 0);
    }
    private static void IndependentGoods()
    {
        var state = State(); var world = new World(); var planner = new AutoSendPlanner(state);
        Set(state, 2, 1, 0); Set(state, 2, 3, 0, good: Other);
        world.Stock[1, 0] = world.Stock[1, 1] = 100; planner.Run(world, 40);
        Check(world.Stock[2, 0] == 100 && world.Stock[2, 1] == 0);
        planner.Run(world, 120); Check(world.Stock[2, 1] == 100);
    }
    private static void SaveRemainingPhase()
    {
        var state = State(); Set(state, 2, 3, 500);
        Check(AutoSendSaveCodec.TryDecode(AutoSendSaveCodec.Encode(state), new[] { Other, Good }, new[] { 1000, 100 }, out var loaded, out _));
        loaded.InitializeMissingSchedules(600); Check(loaded.GetRule(1, 2, Good).LastCheckTick == 500);
        var world = new World(); world.Stock[1, 0] = 100; var planner = new AutoSendPlanner(loaded);
        planner.Run(world, 619); Check(world.Reads == 0); planner.Run(world, 620); Check(world.Stock[2, 0] == 100);
        // Cache deadlines are derived and invalidated after an interval change.
        Set(loaded, 2, 1, 625); world.Stock[1, 0] = 100; world.Stock[2, 0] = 0; int reads = world.Reads;
        planner.Run(world, 664); Check(world.Reads == reads); planner.Run(world, 665); Check(world.Stock[2, 0] == 100);
    }
    private static void LegacySchedules()
    {
        foreach (int format in new[] { 1, 2 })
        {
            var state = State(); Set(state, 2, 17, 50);
            Check(AutoSendSaveCodec.TryDecode(EncodeLegacy(state, format), new[] { Good, Other }, new[] { 100, 1000 }, out var loaded, out _));
            var rule = loaded.GetRule(1, 2, Good); Check(rule.IntervalSeconds == 30 && !rule.HasCheckTick);
            loaded.InitializeMissingSchedules(321); Check(loaded.GetRule(1, 2, Good).LastCheckTick == 321);
            var world = new World(); world.Stock[1, 0] = 100; var planner = new AutoSendPlanner(loaded);
            planner.Run(world, 1520); Check(world.Reads == 0); planner.Run(world, 1521); Check(world.Stock[2, 0] == 100);
        }
    }

    private static void RepairAfterDeliveryDoesNotRepeat()
    {
        var host = State(); var client = State();
        Set(host, 2, 1, 0);
        Set(client, 2, 1, 0);
        var hostWorld = new World(); hostWorld.Stock[1, 0] = 200;
        new AutoSendPlanner(host).Run(hostWorld, 40);
        host.MarkProcessedTick(40);
        Check(hostWorld.Calls == 1);
        byte[] snapshot = AutoSendSaveCodec.Encode(host);
        Check(AutoSendSaveCodec.TryDecode(snapshot, new[] { Good, Other }, new[] { 100, 1000 }, out var repaired, out _));
        Check(repaired.LastProcessedTick == 40 && client.GetRule(1, 2, Good).LastCheckTick == 0);
        Check(repaired.GetRevision(1) == host.GetRevision(1) && repaired.GetCursor(1, Good) == host.GetCursor(1, Good));
        var repairedWorld = new World(); repairedWorld.Stock[1, 0] = 100;
        new AutoSendPlanner(repaired).Run(repairedWorld, 41);
        Check(repairedWorld.Calls == 0);
        new AutoSendPlanner(repaired).Run(repairedWorld, 80);
        Check(repairedWorld.Calls == 1);
    }
    private static void RejectInvalidSavedSchedules()
    {
        var state = State(); Set(state, 2, 30, 17);
        byte[] bytes = AutoSendSaveCodec.Encode(state);
        // v4 header: four Int32s, tick flag + tick; eight pause/revision/chat tuples;
        // first good, source reserve/cursor, then the unused self-rule (18 bytes).
        // These byte offsets intentionally exercise the persisted format contract.
        const int intervalOffset = 4 * 4 + 1 + 4 + 8 * (1 + 8 + 1) + 4 + 8 + 18 + 9;
        foreach (int seconds in new[] { 0, 301, int.MaxValue })
        {
            var damaged = (byte[])bytes.Clone(); Array.Copy(BitConverter.GetBytes(seconds), 0, damaged, intervalOffset, 4);
            Check(!AutoSendSaveCodec.TryDecode(damaged, new[] { Good, Other }, new[] { 100, 1000 }, out var rejected, out _));
            Check(rejected == null);
        }
        var missingClock = (byte[])bytes.Clone(); missingClock[intervalOffset + 4] = 0;
        Check(!AutoSendSaveCodec.TryDecode(missingClock, new[] { Good, Other }, new[] { 100, 1000 }, out var missing, out _));
        Check(missing == null); // Nonzero timestamp without its presence flag is invalid.
        var disabledClock = (byte[])bytes.Clone(); disabledClock[intervalOffset - 1] = 0;
        Check(!AutoSendSaveCodec.TryDecode(disabledClock, new[] { Good, Other }, new[] { 100, 1000 }, out var disabled, out _));
        Check(disabled == null); // Stopped rules cannot carry a running clock.
    }
    private static void WrapAndNoCatchup()
    {
        foreach (int start in new[] { int.MaxValue - 20, -20 })
        {
            var state = State(); Set(state, 2, 1, start); var world = new World(); world.Stock[1, 0] = 100;
            var planner = new AutoSendPlanner(state); planner.Run(world, unchecked(start + 39)); Check(world.Reads == 0);
            planner.Run(world, unchecked(start + 40)); Check(world.Stock[2, 0] == 100);
        }
        var lateState = State(); Set(lateState, 2, 1, 0); var lateWorld = new World(); lateWorld.Stock[1, 0] = 10000;
        var latePlanner = new AutoSendPlanner(lateState); latePlanner.Run(lateWorld, 40000); Check(lateWorld.Calls == 1);
        int reads = lateWorld.Reads; latePlanner.Run(lateWorld, 40000); latePlanner.Run(lateWorld, 40039); Check(lateWorld.Reads == reads);
        latePlanner.Run(lateWorld, 40040); Check(lateWorld.Calls == 1); // ceiling already reached
    }
    private static void IntervalQueue()
    {
        var queue = new AutoSendEditQueue();
        queue.Enqueue(new AutoSendEditIntent(1, 1, AutoSendEdit.Interval, 2, Good, 1, false));
        queue.Enqueue(new AutoSendEditIntent(1, 1, AutoSendEdit.Interval, 3, Good, 99, false));
        queue.Enqueue(new AutoSendEditIntent(1, 1, AutoSendEdit.Interval, 2, Good, 2, false));
        Check(queue.WaitingCount == 2); int reserve = 0; var rule = new AutoSendRule(0, 100, true);
        Check(queue.Project(1, 1, 2, Good, ref reserve, ref rule)); Check(rule.IntervalSeconds == 2);
        rule = new AutoSendRule(0, 100, true); queue.Project(1, 1, 3, Good, ref reserve, ref rule); Check(rule.IntervalSeconds == 99);
        rule = new AutoSendRule(0, 100, true); Check(!queue.Project(1, 1, 2, Other, ref reserve, ref rule)); Check(rule.IntervalSeconds == 30);
        Check(!AutoSendRuleEdits.TryApply(rule, AutoSendEdit.Interval, 301, false, out _, out _));
    }
    private static void NoDueDoesNotReadOrAllocate()
    {
        var state = State(); var world = new World(); var planner = new AutoSendPlanner(state);
        for (int recipient = 2; recipient <= 8; recipient++) Set(state, recipient, 300, 0);
        for (int tick = 0; tick < 64; tick++) planner.Run(world, tick);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int tick = 64; tick < 10000; tick++) planner.Run(world, tick);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(world.Reads == 0 && world.Calls == 0); Check(allocated == 0);
    }
    // Exact old byte layouts are the compatibility contract under test. v1/v2
    // contain no interval/clock; v3 adds them but contains no sender chat preference.
    internal static byte[] EncodeLegacy(AutoSendState state, int version)
    {
        using (var stream = new MemoryStream())
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(0x41535431); writer.Write(version); writer.Write(state.GoodCount);
            writer.Write(state.SenderCursor); writer.Write(state.HasProcessedTick); writer.Write(state.LastProcessedTick);
            for (int p = 1; p <= 8; p++) { writer.Write(state.IsPaused(p)); writer.Write(state.GetRevision(p)); }
            for (int g = 0; g < state.GoodCount; g++)
            {
                int good = state.GetGood(g); writer.Write(good);
                for (int sender = 1; sender <= 8; sender++)
                {
                    writer.Write(state.GetReserve(sender, good)); writer.Write(state.GetCursor(sender, good));
                    for (int recipient = 1; recipient <= 8; recipient++)
                    {
                        var rule = state.GetRule(sender, recipient, good);
                        writer.Write(rule.Minimum); writer.Write(rule.Maximum); writer.Write(rule.Enabled);
                        if (version >= 3)
                        { writer.Write(rule.IntervalSeconds); writer.Write(rule.HasCheckTick); writer.Write(rule.LastCheckTick); }
                    }
                }
            }
            writer.Flush(); return stream.ToArray();
        }
    }
    private sealed class World : IAutoSendWorld
    {
        internal readonly int[,] Stock = new int[9, 2];
        internal int Reads, Calls;
        public int GetStock(int player, int good) { Reads++; return Stock[player, good == Good ? 0 : 1]; }
        public int GetFreeCapacity(int player, int good) { Reads++; return 100000; }
        public bool IsAlliedLiving(int sender, int recipient) { Reads++; return sender != recipient; }
        public AutoSendTransferResult Transfer(int sender, int recipient, int good, int amount)
        { Calls++; int g = good == Good ? 0 : 1; Stock[sender, g] -= amount; Stock[recipient, g] += amount; return new AutoSendTransferResult(amount, amount, true); }
    }
}
