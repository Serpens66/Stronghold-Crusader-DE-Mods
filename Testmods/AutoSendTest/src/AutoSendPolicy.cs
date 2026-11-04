using System;

namespace AutoSendTest
{
    internal readonly struct AutoSendRule
    {
        internal AutoSendRule(int minimum, int maximum, bool enabled)
        { Minimum = minimum; Maximum = maximum; Enabled = enabled; }
        internal int Minimum { get; }
        internal int Maximum { get; }
        internal bool Enabled { get; }
    }

    // Pure simulation state. Goods identifiers and batch limits are supplied from the
    // actual Extender enum at the runtime boundary; no native enum values live here.
    internal sealed class AutoSendState
    {
        // SHCDE's explicit player-slot contract: game IDs 1..8, zero is unused.
        internal const int PlayerCount = 8;
        internal const int MaximumGoods = 256;
        private readonly int[] goods, batchLimits;
        private int senderCursor;
        private readonly int[,] reserves, cursors, activeCounts;
        private readonly AutoSendRule[,,] rules;
        private readonly bool[] paused = new bool[PlayerCount + 1];
        private readonly long[] revisions = new long[PlayerCount + 1];
        private readonly bool[] visited = new bool[PlayerCount + 1];
        internal AutoSendState(int[] allowedGoods, int[] perRecipientBatchLimits)
        {
            if (allowedGoods == null || perRecipientBatchLimits == null || allowedGoods.Length == 0 ||
                allowedGoods.Length > MaximumGoods || allowedGoods.Length != perRecipientBatchLimits.Length)
                throw new ArgumentException("A bounded, nonempty goods list and matching batch limits are required.");
            goods = (int[])allowedGoods.Clone(); batchLimits = (int[])perRecipientBatchLimits.Clone();
            for (int g = 0; g < goods.Length; g++)
            {
                if (goods[g] <= 0 || batchLimits[g] <= 0 || batchLimits[g] > short.MaxValue)
                    throw new ArgumentException("Goods and Int16-safe positive transfer limits are required.");
                for (int earlier = 0; earlier < g; earlier++)
                    if (goods[earlier] == goods[g]) throw new ArgumentException("Duplicate goods identifier.");
            }
            reserves = new int[PlayerCount + 1, goods.Length];
            cursors = new int[PlayerCount + 1, goods.Length];
            activeCounts = new int[PlayerCount + 1, goods.Length];
            rules = new AutoSendRule[PlayerCount + 1, PlayerCount + 1, goods.Length];
        }
        internal int GoodCount => goods.Length;
        internal int SenderCursor
        {
            get => senderCursor;
            set { if (value < 0 || value >= PlayerCount) throw new ArgumentOutOfRangeException(nameof(value)); senderCursor = value; }
        }
        internal bool HasProcessedTick { get; private set; }
        internal int LastProcessedTick { get; private set; }
        internal int GetGood(int goodIndex) => goods[goodIndex];
        internal int GetBatchLimit(int good) => batchLimits[RequireGood(good)];
        internal int GetReserve(int sender, int good) => reserves[RequirePlayer(sender), RequireGood(good)];
        internal AutoSendRule GetRule(int sender, int recipient, int good) =>
            rules[RequirePlayer(sender), RequirePlayer(recipient), RequireGood(good)];
        internal bool IsPaused(int sender) => paused[RequirePlayer(sender)];
        internal bool HasActiveRules(int sender, int good) => activeCounts[RequirePlayer(sender), RequireGood(good)] != 0;
        internal long GetRevision(int sender) => revisions[RequirePlayer(sender)];
        internal int GetCursor(int sender, int good) => cursors[RequirePlayer(sender), RequireGood(good)];
        internal void SetCursor(int sender, int good, int cursor)
        { if (cursor < 0 || cursor >= PlayerCount) throw new ArgumentOutOfRangeException(nameof(cursor)); cursors[RequirePlayer(sender), RequireGood(good)] = cursor; }
        internal void MarkProcessedTick(int tick) { HasProcessedTick = true; LastProcessedTick = tick; }
        internal bool ContainsGood(int good) => FindGood(good) >= 0;

        internal bool TrySetReserve(int sender, int good, int value, out string reason)
        {
            int g = FindGood(good);
            if (!CanChange(sender, g, out reason)) return false;
            if (value < 0) { reason = "Reserve must not be negative."; return false; }
            reserves[sender, g] = value; revisions[sender]++; return true;
        }
        internal bool TrySetPaused(int sender, bool value, out string reason)
        {
            if (!CanChange(sender, 0, out reason)) return false;
            paused[sender] = value; revisions[sender]++; return true;
        }
        internal bool TrySetRule(int sender, int recipient, int good, int minimum, int maximum, bool enabled, out string reason)
        {
            int g = FindGood(good);
            if (!CanChange(sender, g, out reason)) return false;
            if (!ValidPlayer(recipient) || recipient == sender)
            { reason = "A different valid recipient is required."; return false; }
            if (minimum < 0 || maximum < 0 || minimum > maximum || enabled && maximum == 0)
            { reason = "Require 0 <= minimum <= maximum and a positive maximum for active rules."; return false; }
            // A new source->recipient edge closes a circle iff recipient already
            // reaches source. Paused edges remain part of the graph by contract.
            if (enabled && WouldCycle(sender, recipient, g))
            { reason = "This rule would create an automatic delivery circle for this good."; return false; }
            if (rules[sender, recipient, g].Enabled != enabled) activeCounts[sender, g] += enabled ? 1 : -1;
            rules[sender, recipient, g] = new AutoSendRule(minimum, maximum, enabled);
            revisions[sender]++; return true;
        }
        private bool CanChange(int sender, int goodIndex, out string reason)
        {
            reason = string.Empty;
            if (!ValidPlayer(sender) || goodIndex < 0) { reason = "Unknown player or good."; return false; }
            if (revisions[sender] == long.MaxValue) { reason = "Revision range exhausted."; return false; }
            return true;
        }
        private bool WouldCycle(int source, int recipient, int goodIndex)
        {
            Array.Clear(visited, 0, visited.Length);
            return Reaches(recipient, source, goodIndex);
        }
        private bool Reaches(int node, int wanted, int goodIndex)
        {
            if (node == wanted) return true;
            if (visited[node]) return false;
            visited[node] = true;
            for (int next = 1; next <= PlayerCount; next++)
                if (rules[node, next, goodIndex].Enabled && Reaches(next, wanted, goodIndex)) return true;
            return false;
        }
        private int FindGood(int good)
        { for (int g = 0; g < goods.Length; g++) if (goods[g] == good) return g; return -1; }
        private int RequireGood(int good)
        { int index = FindGood(good); if (index < 0) throw new ArgumentOutOfRangeException(nameof(good)); return index; }
        private static int RequirePlayer(int player)
        { if (!ValidPlayer(player)) throw new ArgumentOutOfRangeException(nameof(player)); return player; }
        private static bool ValidPlayer(int player) => player >= 1 && player <= PlayerCount;

        // Codec-only restoration validates values before publishing the new state.
        internal void RestoreMeta(int sender, bool isPaused, long revision)
        { if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision)); paused[RequirePlayer(sender)] = isPaused; revisions[sender] = revision; }
    }

    internal readonly struct AutoSendTransferResult
    {
        internal AutoSendTransferResult(int debited, int credited, bool known)
        { Debited = debited; Credited = credited; Known = known; }
        internal int Debited { get; }
        internal int Credited { get; }
        internal bool Known { get; }
    }
    internal interface IAutoSendWorld
    {
        int GetStock(int player, int good);
        int GetFreeCapacity(int player, int good);
        bool IsAlliedLiving(int sender, int recipient);
        AutoSendTransferResult Transfer(int sender, int recipient, int good, int amount);
    }
    internal readonly struct AutoSendRunResult
    {
        internal AutoSendRunResult(int transfers, long units, int uncertain)
        { Transfers = transfers; Units = units; UncertainTransfers = uncertain; }
        internal int Transfers { get; }
        internal long Units { get; }
        internal int UncertainTransfers { get; }
    }

    // No allocations per run: at most eight players and one bounded batch per
    // sender/recipient/good per run, shared across emergency and ordinary tiers.
    internal sealed class AutoSendPlanner
    {
        private readonly AutoSendState state;
        private readonly int[] demand = new int[AutoSendState.PlayerCount + 1];
        private readonly int[] planned = new int[AutoSendState.PlayerCount + 1];
        private readonly int[] maximum = new int[AutoSendState.PlayerCount + 1];
        internal AutoSendPlanner(AutoSendState state) { this.state = state ?? throw new ArgumentNullException(nameof(state)); }
        internal AutoSendRunResult Run(IAutoSendWorld world)
        {
            if (world == null) throw new ArgumentNullException(nameof(world));
            int transfers = 0, uncertain = 0; long units = 0;
            for (int offset = 0; offset < AutoSendState.PlayerCount; offset++)
            {
                int sender = (state.SenderCursor + offset) % AutoSendState.PlayerCount + 1;
                if (state.IsPaused(sender)) continue;
                for (int g = 0; g < state.GoodCount; g++)
                {
                    int good = state.GetGood(g), reserve = state.GetReserve(sender, good);
                    if (!state.HasActiveRules(sender, good)) continue;
                    int own = world.GetStock(sender, good);
                    if (own <= reserve) continue;
                    int budget = own - reserve;
                    Array.Clear(planned, 0, planned.Length);
                    Array.Clear(maximum, 0, maximum.Length);
                    for (int recipient = 1; recipient <= AutoSendState.PlayerCount; recipient++)
                    {
                        demand[recipient] = 0;
                        AutoSendRule rule = state.GetRule(sender, recipient, good);
                        if (!rule.Enabled || !world.IsAlliedLiving(sender, recipient)) continue;
                        int current = world.GetStock(recipient, good), capacity = world.GetFreeCapacity(recipient, good);
                        if (current < 0 || capacity <= 0 || current >= rule.Maximum) continue;
                        maximum[recipient] = Math.Min(state.GetBatchLimit(good), Math.Min(capacity, rule.Maximum - current));
                        demand[recipient] = Math.Min(maximum[recipient], Math.Max(0, rule.Minimum - current));
                    }
                    int cursor = state.GetCursor(sender, good);
                    budget = AllocateFair(demand, planned, budget, ref cursor);
                    for (int recipient = 1; recipient <= AutoSendState.PlayerCount; recipient++)
                        demand[recipient] = maximum[recipient] - planned[recipient];
                    AllocateFair(demand, planned, budget, ref cursor);
                    state.SetCursor(sender, good, cursor);
                    // Stable rotating order is shared with the remainder allocator.
                    for (int r = 0; r < AutoSendState.PlayerCount; r++)
                    {
                        int recipient = (cursor + r) % AutoSendState.PlayerCount + 1;
                        int amount = planned[recipient];
                        if (amount == 0 || !world.IsAlliedLiving(sender, recipient)) continue;
                        AutoSendRule liveRule = state.GetRule(sender, recipient, good);
                        if (!liveRule.Enabled) continue;
                        int currentSource = world.GetStock(sender, good), currentRecipient = world.GetStock(recipient, good);
                        int capacity = world.GetFreeCapacity(recipient, good);
                        if (currentSource <= reserve || currentRecipient < 0 || currentRecipient >= liveRule.Maximum || capacity <= 0) continue;
                        amount = Math.Min(amount, Math.Min(currentSource - reserve, Math.Min(capacity, liveRule.Maximum - currentRecipient)));
                        if (amount <= 0) continue;
                        AutoSendTransferResult result = world.Transfer(sender, recipient, good, amount);
                        if (!result.Known || result.Debited < 0 || result.Debited > amount || result.Debited != result.Credited)
                            return new AutoSendRunResult(transfers, units, uncertain + 1);
                        // A veto is a definite zero change. Likewise, a smaller,
                        // balanced transfer is known; neither implies corruption.
                        if (result.Debited > 0) { transfers++; units += result.Debited; }
                    }
                }
            }
            state.SenderCursor = (state.SenderCursor + 1) % AutoSendState.PlayerCount;
            return new AutoSendRunResult(transfers, units, uncertain);
        }
        private static int AllocateFair(int[] needs, int[] allocation, int budget, ref int cursor)
        {
            while (budget > 0)
            {
                int active = 0;
                for (int player = 1; player <= AutoSendState.PlayerCount; player++) if (needs[player] > 0) active++;
                if (active == 0) break;
                int equal = budget / active;
                if (equal == 0)
                {
                    int start = cursor;
                    for (int offset = 0; offset < AutoSendState.PlayerCount && budget > 0; offset++)
                    {
                        int player = (start + offset) % AutoSendState.PlayerCount + 1;
                        if (needs[player] == 0) continue;
                        needs[player]--; allocation[player]++; budget--; cursor = player % AutoSendState.PlayerCount;
                    }
                    break;
                }
                for (int player = 1; player <= AutoSendState.PlayerCount; player++)
                {
                    int amount = Math.Min(equal, needs[player]);
                    needs[player] -= amount; allocation[player] += amount; budget -= amount;
                }
            }
            return budget;
        }
    }
}
