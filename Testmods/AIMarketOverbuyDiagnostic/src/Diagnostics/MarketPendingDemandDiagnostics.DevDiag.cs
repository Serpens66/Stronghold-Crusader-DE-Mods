#if SERP_DEVDIAG
using System;
using System.Diagnostics;
using System.Threading;
using BepInEx.Logging;
using APIShared.Simulation;
using SHCDESE.API;
using SHCDESE.Interop;
using Shared;
using Evidence = AIMarketOverbuyDiagnostic.MarketDemandEvidence;

namespace AIMarketOverbuyDiagnostic
{
    internal static unsafe partial class MarketPendingDemandFix
    {
        private const int Capacity = 512;
        private static readonly bool[] firstSource = new bool[(Players + 1) * Goods], firstAdjustment = new bool[(Players + 1) * Goods];
        private static readonly long[] reductionsByGood = new long[(Players + 1) * Goods];
        private static readonly Evidence[] records = new Evidence[Capacity];
        private static long written, read, nextSummary, nextError;
        private static int forceSummary, summaryInFlight, diagnosticCaptureEnabled = 1;
        private static long lastSourceCount, lastReductionCount, lastIncreaseCount, lastCancellationCount;
        private static Exception diagnosticFailureToWrite;
        internal static long Reductions, Increases, CancelledOrders, OmittedEvidence, ZeroStockPassed, SourceStores, DiagnosticFailures;
        internal static readonly long[] ManagedCalls = new long[4], ManagedClockTicks = new long[4], ManagedMaxClockTicks = new long[4];
        private static readonly long[] timingHistogram = new long[4 * 32];
        // The audited measured bodies make no nested native calls. Thread-local
        // starts keep concurrent publisher threads independent without allocations.
        [ThreadStatic] private static long sourceStart, beforeStart, afterStart, stockStart;
        internal static bool OutputDrained => Volatile.Read(ref read) == Volatile.Read(ref written) &&
            Volatile.Read(ref forceSummary) == 0 && Volatile.Read(ref summaryInFlight) == 0;

        internal static void StopDiagnosticCapture()
        { lock (gate) { Volatile.Write(ref diagnosticCaptureEnabled, 0); Interlocked.Exchange(ref forceSummary, 1); } }
        static partial void DiagReset()
        {
            Array.Clear(firstSource, 0, firstSource.Length);
            Array.Clear(firstAdjustment, 0, firstAdjustment.Length);
            Array.Clear(reductionsByGood, 0, reductionsByGood.Length);
            Volatile.Write(ref diagnosticCaptureEnabled, 1);
            Interlocked.Exchange(ref forceSummary, 1);
        }
        static partial void DiagEnd() => Interlocked.Exchange(ref forceSummary, 1);
        static partial void DiagConfigureCorrection(ref int correction)
        { correction = string.IsNullOrEmpty(MarketAutomation.Scenario) || MarketAutomation.Scenario == "market-fresh-fixed" ? 1 : 0; }
        static partial void DiagBeginTiming(int kind)
        {
            if (Volatile.Read(ref diagnosticCaptureEnabled) == 0) return;
            long now = Stopwatch.GetTimestamp();
            switch (kind) { case 0: sourceStart = now; break; case 1: beforeStart = now; break; case 2: afterStart = now; break; case 3: stockStart = now; break; }
        }
        static partial void DiagEndTiming(int kind)
        {
            if (Volatile.Read(ref diagnosticCaptureEnabled) == 0) return;
            long start = kind == 0 ? sourceStart : kind == 1 ? beforeStart : kind == 2 ? afterStart : stockStart;
            RecordTiming(kind, Stopwatch.GetTimestamp() - start);
        }
        static partial void DiagSourceObserved(int player, int good, int stock, int aicId, bool newGoal, ref Request request)
        {
            if (Volatile.Read(ref diagnosticCaptureEnabled) == 0) return;
            try
            {
                if (newGoal) request.Tick = CurrentTick();
                int index = player * Goods + good;
                var target = request.Target;
                Interlocked.Increment(ref SourceStores);
                if (firstSource[index]) return;
                var evidence = new Evidence { Session = session, Sequence = request.Sequence, Player = player, Good = good,
                    Tick = request.Tick, SourceRva = request.SourceRva, SourceStock = request.SourceStock, After = request.GoalAmount, Goal = target.Goal,
                    OriginalPackage = target.OriginalPackage, Credited = target.Credited, AicId = aicId,
                    IsMaterial = target.Policy == MarketDemandPolicy.MaterialStockGoal, MaterialCost = target.MaterialCost, MaterialPadding = target.MaterialPadding };
                AddAmmoEvidence(ref evidence, request, player, stock);
                if (Enqueue(evidence)) firstSource[index] = true;
            }
            catch (Exception failure) { FailDiagnostics(failure); }
        }
        static partial void DiagAdjusted(int player, int good, int original, int adjusted, int stock, int aicId, int boundaryRva, Request request)
        {
            if (Volatile.Read(ref diagnosticCaptureEnabled) == 0) return;
            try
            {
                int index = player * Goods + good;
                if (adjusted < original)
                { Interlocked.Increment(ref Reductions); Interlocked.Increment(ref reductionsByGood[index]); }
                else Interlocked.Increment(ref Increases);
                if (adjusted == 0) Interlocked.Increment(ref CancelledOrders);
                if (firstAdjustment[index]) return;
                var evidence = new Evidence { Kind = 1, Session = session, Player = player, Good = good,
                    Tick = CurrentTick(), Before = original, After = adjusted, Stock = stock, Goal = request.Target.Goal,
                    OriginalPackage = request.Target.OriginalPackage, Credited = request.Target.Credited,
                    IsMaterial = request.Target.Policy == MarketDemandPolicy.MaterialStockGoal,
                    MaterialCost = request.Target.MaterialCost, MaterialPadding = request.Target.MaterialPadding,
                    AicId = aicId, SourceRva = request.SourceRva, SourceTick = request.Tick,
                    SourceSequence = request.Sequence, SourceStock = request.SourceStock, BoundaryRva = boundaryRva };
                AddAmmoEvidence(ref evidence, request, player, stock);
                if (Enqueue(evidence)) firstAdjustment[index] = true;
            }
            catch (Exception failure) { FailDiagnostics(failure); }
        }
        static partial void DiagUnchanged(int boundaryRva, int stock, bool active)
        { if (Volatile.Read(ref diagnosticCaptureEnabled) != 0 && boundaryRva == 0x3EC90 && active && stock == 0) Interlocked.Increment(ref ZeroStockPassed); }
        private static void FailDiagnostics(Exception failure)
        {
            Interlocked.Increment(ref DiagnosticFailures);
            Interlocked.CompareExchange(ref diagnosticFailureToWrite, failure, null);
            Volatile.Write(ref diagnosticCaptureEnabled, 0);
            Interlocked.Exchange(ref forceSummary, 1);
        }
        private static int CurrentTick() => SimulationTickEvents.TryGetCurrentTick(out int tick) ? tick : 0;

        private static void AddAmmoEvidence(ref Evidence evidence, Request request, int player, int stock)
        {
            if (request.Target.Policy != MarketDemandPolicy.AutoAmmo) return;
            var result = request.AmmoOrigin.TryDesired(player, stock, out _, out int ammo);
            evidence.IsAmmo = true; evidence.UnitId = request.AmmoOrigin.UnitId;
            evidence.UnitGlobalId = request.AmmoOrigin.GlobalId; evidence.UnitCreationTick = request.AmmoOrigin.CreationTick;
            evidence.Ammo = ammo; evidence.AmmoState = (int)result;
        }

        internal static void ObservePendingState(int player, int good, int value)
        { if (ValidCell(player, good)) lock (gate) requests[player * Goods + good].Target.ObservePending(value); }

        internal static bool TryGetTrackedRequest(int player, int good, out int amount, bool allRecruitmentSources = false)
        {
            amount = 0;
            lock (gate)
            {
                if (!ValidCell(player, good) || good < (int)eGoods.STORED_BOWS || good > (int)eGoods.STORED_METAL_ARMOUR) return false;
                int index = player * Goods + good;
                Request request = requests[index];
                bool combat = request.SourceRva == 0x40D62 || request.SourceRva == 0x40D3C;
                bool otherRecruit = request.SourceRva == 0x403A7 || request.SourceRva == 0x40597;
                if (request.SourceStock != 0 || (!combat && !(allRecruitmentSources && otherRecruit))) return false;
                if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(player, out GamePlayerResources* resources) || resources == null) return false;
                request.Target.ObservePending(((int*)&resources->r_AIPendingMarketPurchaseAmountNull)[good]);
                if (unchecked((int)resources->r_AILordMinusOne) != request.Target.AicId ||
                    !TryPolicy(player, good, resources, request, out long policy) || policy != request.Target.PolicyToken) request.Target.Active = false;
                requests[index] = request;
                if (!request.Target.Active) return false;
                amount = request.Target.ExpectedPending;
                return true;
            }
        }

        internal static bool TryGetProvenance(int player, int good, out int tick, out long sourceSequence, out int target, bool allRecruitmentSources = false)
        {
            tick = target = 0; sourceSequence = 0;
            lock (gate)
            {
                if (!TryGetTrackedRequest(player, good, out _, allRecruitmentSources)) return false;
                Request request = requests[player * Goods + good];
                tick = request.Tick; sourceSequence = request.Sequence; target = request.Target.OriginalPackage;
                return true;
            }
        }

        internal static long GetReductionCount(int player, int good) => ValidCell(player, good)
            ? Interlocked.Read(ref reductionsByGood[player * Goods + good]) : 0;

        internal static void EnableAfterGift()
        {
            lock (gate)
            {
                if (MarketAutomation.Scenario != "market-backlog-fixed" || !MarketBacklogScenario.Active ||
                    MarketBacklogScenario.GiftUnits <= 0 || sessionActive == 0 || !Ready)
                    throw new InvalidOperationException("Fixed comparison requires its owned fulfilled-demand scenario.");
                corrective = 1;
                if (receiptGate != IntPtr.Zero) *(int*)receiptGate = 1;
                if (!CorrectionActive) throw new InvalidOperationException("The fixed gift comparison did not enable all correction gates.");
                Shared.DevDiag.Log(() => "AI_MARKET_FIX_ACTIVE: scenario=" + MarketAutomation.Scenario +
                    " run=" + MarketAutomation.RunId + " corrective=1 sessionActive=1 writerGate=1 receiptGate=1 checkpoint=after-gift");
            }
        }

        internal static void DeactivateExperiment()
        { lock (gate) { corrective = 0; if (receiptGate != IntPtr.Zero) *(int*)receiptGate = 0; } }
        private static bool Enqueue(Evidence evidence)
        {
            long output = written;
            if (output - Volatile.Read(ref read) >= Capacity) { Interlocked.Increment(ref OmittedEvidence); return false; }
            records[(int)(output % Capacity)] = evidence;
            Volatile.Write(ref written, output + 1);
            return true;
        }

        private static void RecordTiming(int kind, long elapsed)
        {
            Interlocked.Increment(ref ManagedCalls[kind]);
            Interlocked.Add(ref ManagedClockTicks[kind], elapsed);
            long previous;
            do { previous = Volatile.Read(ref ManagedMaxClockTicks[kind]); if (elapsed <= previous) break; }
            while (Interlocked.CompareExchange(ref ManagedMaxClockTicks[kind], elapsed, previous) != previous);
            long micros = elapsed * 1000000 / Stopwatch.Frequency;
            int bucket = 0;
            while (micros > 1 && bucket < 31) { micros = (micros + 1) >> 1; bucket++; }
            Interlocked.Increment(ref timingHistogram[kind * 32 + bucket]);
        }

        // Conservative histogram upper bound, in microseconds. The worker reads
        // counters concurrently, so this is a measurement summary, not a gate.
        internal static long TimingP99Microseconds(int kind)
        {
            if ((uint)kind >= 4) return 0;
            long total = 0;
            for (int i = 0; i < 32; i++) total += Volatile.Read(ref timingHistogram[kind * 32 + i]);
            if (total == 0) return 0;
            long required = total - total / 100, cumulative = 0;
            for (int i = 0; i < 32; i++)
            {
                cumulative += Volatile.Read(ref timingHistogram[kind * 32 + i]);
                if (cumulative >= required) return 1L << i;
            }
            return 1L << 31;
        }

        internal static void Drain(ManualLogSource log)
        {
            long cursor = read;
            while (cursor < Volatile.Read(ref written))
            {
                Evidence item = records[(int)(cursor % Capacity)];
                DebugLogHelper.LogInfo(log, item.Kind == 1 ? MarketDemandLog.FormatAdjustment(item) : MarketDemandLog.FormatSource(item));
                Volatile.Write(ref read, ++cursor);
            }
            long now = Stopwatch.GetTimestamp();
            // Take the wakeup before the snapshot. End/Reset during formatting
            // publishes another wakeup that this worker must not clear afterward.
            Volatile.Write(ref summaryInFlight, 1);
            bool forced = Interlocked.Exchange(ref forceSummary, 0) != 0;
            try
            {
                if (forced || now >= nextSummary)
                {
                    long stores = Interlocked.Read(ref SourceStores), reduced = Interlocked.Read(ref Reductions),
                        increased = Interlocked.Read(ref Increases), cancelled = Interlocked.Read(ref CancelledOrders);
                    if (forced || stores != lastSourceCount || reduced != lastReductionCount || increased != lastIncreaseCount || cancelled != lastCancellationCount)
                        DebugLogHelper.LogInfo(log, MarketDemandLog.FormatSummary(stores - lastSourceCount,
                            reduced - lastReductionCount, increased - lastIncreaseCount, cancelled - lastCancellationCount,
                            TimingP99Microseconds(0), TimingP99Microseconds(1), TimingP99Microseconds(2), TimingP99Microseconds(3), Interlocked.Read(ref OmittedEvidence)));
                    lastSourceCount = stores; lastReductionCount = reduced; lastIncreaseCount = increased; lastCancellationCount = cancelled;
                    nextSummary = now + Stopwatch.Frequency * 10;
                }
            }
            catch { if (forced) Interlocked.Exchange(ref forceSummary, 1); throw; }
            finally { Volatile.Write(ref summaryInFlight, 0); }
            if (now >= nextError)
            {
                Exception failure = Interlocked.Exchange(ref diagnosticFailureToWrite, null);
                if (failure != null)
                {
                    nextError = now + Stopwatch.Frequency * 30;
                    DebugLogHelper.LogError(log, "AI_MARKET_DIAGNOSTIC_ERROR: diagnostic capture stopped; " + failure);
                }
            }
        }
    }
}
#endif
