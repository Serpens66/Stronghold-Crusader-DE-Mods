using System;
using System.Globalization;

namespace AutoSendTest
{
    // Passive evidence collector. This file is explicitly compiled only by the
    // diagnostic runtime build (and the pure regression project).
    internal sealed class AutoSendProbe
    {
        private readonly int[] minimum = new int[9], maximum = new int[9], initialStock = new int[9], emergency = new int[9];
        private readonly bool[] eligible = new bool[9], target = new bool[9];
        private readonly long[] credited = new long[9], requestedTotal = new long[9];
        private int startTick, reserve, ownStock, batch, rounds, attempts, transfers, originalCalls;
        private int balanceViolations, reserveViolations, ceilingViolations, batchViolations, eligibilityViolations;
        private int unknown, vetoed, fairCases, fairViolations, fairInconclusive, emergencyCases, emergencyViolations;
        private int emergencyInconclusive, incompleteRounds, invalidRounds;
        private long units, roundCredits, roundDebits, fairExpected;
        private bool inRound, roundUncertain, fairCandidate, emergencyCandidate;
        internal bool Active { get; private set; }
        internal int Sender { get; private set; }
        internal int Good { get; private set; }

        internal void Start(int sender, int good, int tick)
        {
            if (sender < 1 || sender > AutoSendState.PlayerCount || good <= 0)
                throw new ArgumentException("A valid sender and selected good are required.");
            if (Active) throw new InvalidOperationException("Finish the current probe before starting another.");
            Sender = sender; Good = good; startTick = tick;
            rounds = attempts = transfers = originalCalls = 0;
            balanceViolations = reserveViolations = ceilingViolations = batchViolations = eligibilityViolations = 0;
            unknown = vetoed = fairCases = fairViolations = fairInconclusive = 0;
            emergencyCases = emergencyViolations = emergencyInconclusive = incompleteRounds = invalidRounds = 0;
            units = 0; inRound = false; Active = true;
        }

        internal bool HasElapsed(int tick, int durationTicks = 2400)
        {
            uint elapsed = unchecked((uint)(tick - startTick));
            return Active && durationTicks > 0 && elapsed <= int.MaxValue && elapsed >= (uint)durationTicks;
        }

        internal bool BeginRound(int tick, int senderReserve, int senderStock, int[] minimums, int[] maxima,
            int[] recipientStocks, int[] freeCapacities, bool[] recipientEligible, int batchLimit)
        {
            if (!Active) return false;
            if (inRound) Incomplete();
            if (senderReserve < 0 || senderStock < 0 || batchLimit <= 0 || batchLimit > short.MaxValue ||
                !Slots(minimums) || !Slots(maxima) || !Slots(recipientStocks) || !Slots(freeCapacities) ||
                recipientEligible == null || recipientEligible.Length != 9 || unchecked((uint)(tick - startTick)) > int.MaxValue)
            { invalidRounds++; return false; }
            for (int player = 1; player <= AutoSendState.PlayerCount; player++)
                if (minimums[player] < 0 || maxima[player] < minimums[player] || recipientStocks[player] < 0)
                { invalidRounds++; return false; }
            reserve = senderReserve; ownStock = senderStock; batch = batchLimit;
            Array.Copy(minimums, minimum, 9); Array.Copy(maxima, maximum, 9); Array.Copy(recipientStocks, initialStock, 9);
            Array.Copy(recipientEligible, eligible, 9);
            Array.Clear(credited, 0, credited.Length); Array.Clear(requestedTotal, 0, requestedTotal.Length);
            Array.Clear(emergency, 0, emergency.Length);
            Array.Clear(target, 0, target.Length);
            roundCredits = roundDebits = 0; roundUncertain = false;
            int candidates = 0; bool allMinimumsZero = true; long emergencyTotal = 0;
            for (int player = 1; player <= AutoSendState.PlayerCount; player++)
            {
                if (player == Sender || !eligible[player]) continue;
                int headroom = Math.Max(0, Math.Min(freeCapacities[player], maximum[player] - initialStock[player]));
                if (headroom == 0) continue;
                target[player] = true;
                candidates++; allMinimumsZero &= minimum[player] == 0;
                emergency[player] = Math.Min(Math.Min(batch, headroom), Math.Max(0, minimum[player] - initialStock[player]));
                emergencyTotal += emergency[player];
            }
            long surplus = Math.Max(0L, (long)ownStock - reserve);
            fairExpected = Math.Min(surplus, (long)candidates * batch);
            fairCandidate = candidates >= 2 && allMinimumsZero && fairExpected > 0;
            if (fairCandidate)
            {
                long equalCeiling = (fairExpected + candidates - 1) / candidates;
                for (int player = 1; player <= AutoSendState.PlayerCount; player++)
                {
                    if (player == Sender || !eligible[player]) continue;
                    int headroom = Math.Max(0, Math.Min(freeCapacities[player], maximum[player] - initialStock[player]));
                    if (headroom > 0 && headroom < equalCeiling) fairCandidate = false;
                }
            }
            emergencyCandidate = emergencyTotal > 0 && surplus > 0;
            rounds++; inRound = true; return true;
        }

        internal void ObserveTransfer(int recipient, int requested, bool originalCalled, bool known,
            long? debit, long? credit, int sourceBefore, int recipientBefore)
        {
            if (!Active) return;
            if (!inRound) { invalidRounds++; return; }
            attempts++;
            if (recipient < 1 || recipient > AutoSendState.PlayerCount)
            { invalidRounds++; roundUncertain = true; return; }
            if (originalCalled) originalCalls++; else { vetoed++; roundUncertain = true; }
            if (!target[recipient]) roundUncertain = true;
            requestedTotal[recipient] += requested;
            bool badBatch = requested <= 0 || requested > batch || requestedTotal[recipient] > batch;
            if (sourceBefore != (long)ownStock - roundDebits || recipientBefore != (long)initialStock[recipient] + credited[recipient])
                roundUncertain = true;
            if (!known || !debit.HasValue || !credit.HasValue)
            {
                unknown++; roundUncertain = true;
                if (badBatch) batchViolations++;
                return;
            }
            long spent = debit.Value, received = credit.Value;
            if (spent < 0 || received < 0 || spent != received || !originalCalled && (spent != 0 || received != 0)) balanceViolations++;
            if (spent > 0 && (long)sourceBefore - spent < reserve) reserveViolations++;
            if (received > 0 && (long)recipientBefore + received > maximum[recipient]) ceilingViolations++;
            if (originalCalled && (spent > 0 || received > 0) && (recipient == Sender || !eligible[recipient])) eligibilityViolations++;
            if (spent > requested || received > requested || spent > batch || received > batch || credited[recipient] + received > batch) badBatch = true;
            if (badBatch) batchViolations++;
            if (spent < 0 || received < 0 || spent != received || spent != requested || !originalCalled) roundUncertain = true;
            if (originalCalled && spent > 0 && spent == received)
            { transfers++; units += received; }
            roundDebits += spent; credited[recipient] += received; roundCredits += received;
        }

        // External donors, production or consumption can invalidate the initial
        // allocation comparison without invalidating observed transfer bounds.
        internal void InvalidateRound() { if (Active && inRound) roundUncertain = true; }

        internal void EndRound()
        {
            if (!Active || !inRound) return;
            inRound = false;
            if (fairCandidate)
            {
                if (roundUncertain || roundCredits != fairExpected) fairInconclusive++;
                else
                {
                    fairCases++; long smallest = long.MaxValue, largest = long.MinValue;
                    for (int player = 1; player <= AutoSendState.PlayerCount; player++)
                    {
                        if (!target[player]) continue;
                        smallest = Math.Min(smallest, credited[player]); largest = Math.Max(largest, credited[player]);
                    }
                    if (largest - smallest > 1) fairViolations++;
                }
            }
            if (emergencyCandidate)
            {
                if (roundUncertain || roundCredits <= 0) emergencyInconclusive++;
                else
                {
                    emergencyCases++; bool fullySupplied = true; long ordinary = 0;
                    for (int player = 1; player <= AutoSendState.PlayerCount; player++)
                    {
                        if (player == Sender || !eligible[player]) continue;
                        if (credited[player] < emergency[player]) fullySupplied = false;
                        ordinary += Math.Max(0, credited[player] - emergency[player]);
                    }
                    if (ordinary > 0 && !fullySupplied) emergencyViolations++;
                }
            }
        }

        internal AutoSendProbeSummary Finish(string reason)
        {
            if (inRound) Incomplete();
            Active = false;
            return new AutoSendProbeSummary(reason ?? string.Empty, rounds, attempts, transfers, originalCalls, units,
                balanceViolations, reserveViolations, ceilingViolations, batchViolations, eligibilityViolations,
                unknown, vetoed, fairCases, fairViolations, fairInconclusive, emergencyCases, emergencyViolations,
                emergencyInconclusive, incompleteRounds, invalidRounds);
        }
        private void Incomplete()
        {
            incompleteRounds++; inRound = false;
            if (fairCandidate) fairInconclusive++;
            if (emergencyCandidate) emergencyInconclusive++;
        }
        private static bool Slots(int[] values) => values != null && values.Length == 9;
    }

    internal sealed class AutoSendProbeSummary
    {
        internal AutoSendProbeSummary(string reason, int rounds, int attempts, int transfers, int originalCalls, long units,
            int balance, int reserve, int ceiling, int batch, int eligibility, int unknown, int vetoed,
            int fairCases, int fairViolations, int fairInconclusive, int emergencyCases, int emergencyViolations,
            int emergencyInconclusive, int incomplete, int invalid)
        {
            Reason = reason; Rounds = rounds; Attempts = attempts; Transfers = transfers; OriginalCalls = originalCalls; Units = units;
            BalanceViolations = balance; ReserveViolations = reserve; CeilingViolations = ceiling; BatchViolations = batch;
            EligibilityViolations = eligibility; Unknown = unknown; Vetoed = vetoed; FairCases = fairCases;
            FairViolations = fairViolations; FairInconclusive = fairInconclusive; EmergencyCases = emergencyCases;
            EmergencyViolations = emergencyViolations; EmergencyInconclusive = emergencyInconclusive;
            IncompleteRounds = incomplete; InvalidRounds = invalid;
        }
        internal string Reason { get; }
        internal int Rounds { get; }
        internal int Attempts { get; }
        internal int Transfers { get; }
        internal int OriginalCalls { get; }
        internal long Units { get; }
        internal int BalanceViolations { get; }
        internal int ReserveViolations { get; }
        internal int CeilingViolations { get; }
        internal int BatchViolations { get; }
        internal int EligibilityViolations { get; }
        internal int Unknown { get; }
        internal int Vetoed { get; }
        internal int FairCases { get; }
        internal int FairViolations { get; }
        internal int FairInconclusive { get; }
        internal int EmergencyCases { get; }
        internal int EmergencyViolations { get; }
        internal int EmergencyInconclusive { get; }
        internal int IncompleteRounds { get; }
        internal int InvalidRounds { get; }
        internal string BaseStatus => BalanceViolations + ReserveViolations + CeilingViolations + BatchViolations + EligibilityViolations > 0
            ? "FAIL" : Unknown + IncompleteRounds + InvalidRounds > 0 ? "INCONCLUSIVE" : Transfers == 0 ? "UNTESTED" : "PASS";
        internal string Status => FairViolations + EmergencyViolations > 0 ? "FAIL" : BaseStatus;
        internal string FairStatus => CaseStatus(FairCases, FairViolations, FairInconclusive);
        internal string EmergencyStatus => CaseStatus(EmergencyCases, EmergencyViolations, EmergencyInconclusive);
        private static string CaseStatus(int cases, int violations, int inconclusive) =>
            violations > 0 ? "FAIL" : inconclusive > 0 ? "INCONCLUSIVE" : cases == 0 ? "UNTESTED" : "PASS";
        public override string ToString() => string.Format(CultureInfo.InvariantCulture,
            "result={0} base={24} rounds={1} attempts={2} originals={3} transfers={4} units={5}; balance={6} reserve={7} ceiling={8} batch={9} eligibility={10}; unknown={11} vetoed={12} incomplete={13} invalid={14}; fair={15}(cases={16},violations={17},inconclusive={18}); emergency={19}(cases={20},violations={21},inconclusive={22}); reason={23}",
            Status, Rounds, Attempts, OriginalCalls, Transfers, Units, BalanceViolations, ReserveViolations, CeilingViolations,
            BatchViolations, EligibilityViolations, Unknown, Vetoed, IncompleteRounds, InvalidRounds,
            FairStatus, FairCases, FairViolations, FairInconclusive, EmergencyStatus, EmergencyCases,
            EmergencyViolations, EmergencyInconclusive, Reason, BaseStatus);
    }
}
