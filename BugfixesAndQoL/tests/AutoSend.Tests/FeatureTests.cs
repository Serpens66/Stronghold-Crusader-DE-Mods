using System;
using BugfixesAndQoL.AutoSend;

internal static class FeatureTests
{
    private const int Good = 41;
    private static int checks;

    internal static int Run()
    {
        DisabledSendersKeepRulesDormant();
        PersonalChoicesAreIndependent();
        ReenableDoesNotCatchUp();
        DisabledParticipantStillAcknowledgesSession();
        PreferenceIsBoundToEpoch();
        return checks;
    }

    private static void Check(bool condition)
    { checks++; if (!condition) throw new Exception("AutoSend feature regression " + checks + " failed."); }

    private static AutoSendState State()
    {
        var state = new AutoSendState(new[] { Good }, new[] { 100 });
        Check(state.TrySetRule(1, 3, Good, 12, 1000, true, 1, 0, out _));
        Check(state.TrySetRule(2, 1, Good, 9, 1000, true, 1, 0, out _));
        return state;
    }

    private static void DisabledSendersKeepRulesDormant()
    {
        var state = State();
        var world = new World();
        byte[] before = AutoSendSaveCodec.Encode(state);
        var planner = new AutoSendPlanner(state);
        planner.Run(world, 40000, 0);
        planner.Run(world, 80000, 0);
        Check(world.Reads == 0 && world.Calls == 0);
        byte[] after = AutoSendSaveCodec.Encode(state);
        Check(before.AsSpan().SequenceEqual(after));
        Check(state.GetRule(1, 3, Good).Enabled && state.GetRule(2, 1, Good).Enabled);
        Check(!state.IsPaused(1) && !state.IsPaused(2));
    }

    private static void PersonalChoicesAreIndependent()
    {
        var original = State();
        Check(AutoSendSaveCodec.TryDecode(AutoSendSaveCodec.Encode(original), new[] { Good }, new[] { 100 },
            out var restored, out _));
        var world = new World();
        var result = new AutoSendPlanner(restored).Run(world, 40, 1 << 2);
        Check(result.Transfers == 1 && result.Units == 100);
        Check(world.Stock[1] == 200 && world.Stock[2] == 0 && world.Stock[3] == 0);
        Check(restored.GetRule(1, 3, Good).Enabled && restored.GetRule(1, 3, Good).Minimum == 12);
        Check(restored.GetRule(1, 3, Good).LastCheckTick == 0);
        Check(restored.GetRule(2, 1, Good).LastCheckTick == 40);
        Check(!restored.IsPaused(1) && !restored.IsPaused(2));

        // The other personal choice has no global effect on the first sender.
        var reversed = State();
        world = new World();
        result = new AutoSendPlanner(reversed).Run(world, 40, 1 << 1);
        Check(result.Transfers == 1 && world.Stock[3] == 100 && world.Stock[2] == 100);
        Check(reversed.GetRule(2, 1, Good).LastCheckTick == 0);
    }

    private static void ReenableDoesNotCatchUp()
    {
        var state = State();
        var world = new World();
        var planner = new AutoSendPlanner(state);
        planner.Run(world, 40000, 0);
        planner.Run(world, 80000, 1 << 1);
        Check(world.Calls == 1 && world.Stock[3] == 100);
        world.Stock[1] = 100;
        planner.Run(world, 80000, 1 << 1);
        planner.Run(world, 80039, 1 << 1);
        Check(world.Calls == 1 && world.Stock[1] == 100);
        planner.Run(world, 80040, 1 << 1);
        Check(world.Calls == 2 && world.Stock[3] == 200);
        Check(state.GetRule(1, 3, Good).Enabled && state.GetRule(2, 1, Good).Enabled);
    }

    private static void DisabledParticipantStillAcknowledgesSession()
    {
        const int roster = (1 << 1) | (1 << 2);
        var first = new AutoSendEpochs();
        var second = new AutoSendEpochs();
        Check(first.Bind(1, 11, 1, 11, false, out bool own) && own);
        Check(first.Bind(2, 22, 1, 11, true, out _));
        Check(second.Bind(2, 22, 2, 22, true, out own) && own);
        Check(second.Bind(1, 11, 2, 22, false, out _));
        Check(first.EnabledMask == (1 << 2) && second.EnabledMask == (1 << 2));
        Check(first.AllKnown(roster) && second.AllKnown(roster));
        long proof = first.Fingerprint(roster);
        Check(proof > 0 && proof == second.Fingerprint(roster));
        Check(!first.IsReady(roster));
        Check(first.AcknowledgeReady(2, 22, roster, proof));
        Check(!first.IsReady(roster));
        Check(first.AcknowledgeReady(1, 11, roster, proof));
        Check(first.IsReady(roster));
        Check(second.AcknowledgeReady(1, 11, roster, proof));
        Check(second.AcknowledgeReady(2, 22, roster, proof) && second.IsReady(roster));

        var mismatched = new AutoSendEpochs();
        Check(mismatched.Bind(1, 11, 1, 11, true, out _));
        Check(mismatched.Bind(2, 22, 1, 11, true, out _));
        Check(mismatched.Fingerprint(roster) != proof);
        Check(!mismatched.AcknowledgeReady(1, 11, roster, proof));

        // Nonparticipants cannot alter an already acknowledged roster's proof.
        Check(first.Bind(8, 88, 1, 11, true, out _));
        Check(first.Fingerprint(roster) == proof && first.IsReady(roster));
    }

    private static void PreferenceIsBoundToEpoch()
    {
        var epochs = new AutoSendEpochs();
        Check(epochs.EnabledMask == 0);
        Check(epochs.Bind(1, 11, 1, 11, false, out bool own) && own);
        Check(!epochs.Bind(1, 11, 1, 11, true, out own) && !own);
        Check(epochs.EnabledMask == 0);
        Check(epochs.Bind(1, 11, 1, 11, false, out own) && own);
        Check(!epochs.Bind(1, 12, 1, 11, true, out own) && !own);
        Check(epochs.Get(1) == 11 && epochs.EnabledMask == 0);
        Check(epochs.Bind(2, 22, 1, 11, true, out _));
        Check(!epochs.Bind(2, 22, 1, 11, false, out _) && epochs.EnabledMask == (1 << 2));
        epochs.Reset();
        Check(epochs.EnabledMask == 0 && epochs.Get(1) == 0 && epochs.Get(2) == 0);
        Check(epochs.Bind(1, 33, 1, 33, true, out own) && own && epochs.EnabledMask == (1 << 1));
    }

    private sealed class World : IAutoSendWorld
    {
        internal readonly int[] Stock = new int[9];
        internal int Reads, Calls;
        internal World() { Stock[1] = Stock[2] = 100; }
        public int GetStock(int player, int good) { Reads++; return Stock[player]; }
        public int GetFreeCapacity(int player, int good) { Reads++; return int.MaxValue; }
        public bool IsAlliedLiving(int sender, int recipient) { Reads++; return sender != recipient; }
        public AutoSendTransferResult Transfer(int sender, int recipient, int good, int amount)
        {
            Calls++;
            Stock[sender] -= amount;
            Stock[recipient] += amount;
            return new AutoSendTransferResult(amount, amount, true);
        }
    }
}
