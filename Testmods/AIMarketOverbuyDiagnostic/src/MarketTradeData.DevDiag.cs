#if SERP_DEVDIAG
using System;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace AIMarketOverbuyDiagnostic
{
    internal sealed unsafe partial class MarketTradeTrace
    {
        private const int DecisionKind = 0, RecruitKind = 1, BuyKind = 2, SellKind = 3, CycleKind = 4;
        private const int TickUnavailable = 1, UnitManagerUnavailable = 2, AicUnavailable = 4,
            GoodUnavailable = 8, NonGoodsSellAll = 16;
        private const int DetailLimit = 1024;
        // Reserve one first confirmed buy/sell pair for every valid player/goods slot.
        // Compile-time SDK bounds avoid cross-file static initialization order.
        private const int CycleReserve = PlayerCount * GoodCount;
        private const int DetailRingCapacity = DetailLimit * 2;
        private const int SummaryCapacity = 8;
        private static readonly long SummaryClockInterval = Stopwatch.Frequency * 10;
        private readonly DetailRecord[] details = new DetailRecord[DetailRingCapacity];
        private readonly int[] seenDetails = new int[(PlayerCount + 1) * GoodCount];
        private readonly Attempt[] lastBuy = new Attempt[(PlayerCount + 1) * GoodCount];
        private readonly bool[] hasBuy = new bool[(PlayerCount + 1) * GoodCount];
        private readonly Summary[] summaries = new Summary[SummaryCapacity];
        private long detailWritten, detailRead, summaryWritten, summaryRead;
        private Summary currentSummary;
        private long nextSummaryClock;
        private int normalDetails, cycleDetails;
        private long detailsOmitted, sessionBacklogs;
        private long actualBuys, actualSales, observedCycles;

        private sealed class Frame
        {
            internal long Session, Sequence, EndSequence, Parent, PreCaptureTicks;
            internal int Kind, Player, Good, Requested;
            internal IntPtr Root;
            internal Profile BeforeProfile;
            internal Sample TradeBefore;
            internal readonly Sample[] Before = new Sample[GoodCount];
        }
        private struct Sample
        {
            internal long Utc;
            internal int Tick, Stock, Pending, Gold, Phase, Flags, SuppressSellAllTimer, ExperimentPhase;
            internal long InterventionSequence;
        }
        private struct Profile
        {
            internal long Utc;
            internal int Tick, Gold, Phase, Popularity, LastStand, PurchaseGate, MarketId, CachedMarket,
                AicId, RecruitmentMode, Failure, MissingGood, Flags, BuyWeapons, SellWeapons, TradeMinimum,
                BaseGoldReserve, LowPopularity, BuyBreadAt, BuyCheeseAt, BuyApplesAt, BuyFoodAmount, SellFood, SellWood, SellStone, SellResources, SellFlour,
                SellAle, SellPitch, NonGoodsSellAll;
            internal uint SellAllMask;
        }
        private struct Attempt
        {
            internal long Sequence, EndSequence, Parent;
            internal int Requested, Result;
            internal bool OriginalCompleted;
            internal Sample Before, After;
            internal Profile ProfileBefore, ProfileAfter;
        }
        private struct DetailRecord
        {
            internal long Session;
            internal int Kind, Player, Good;
            internal Attempt Current, Buy;
        }
        private struct Totals
        {
            internal long BuyCalls, BuySuccess, BuyStockGain, BuyRequested, BoughtUnits, BuyDebit,
                SellCalls, SellStockLoss, SellRequested, SoldUnits, SellCredit, Cycles,
                PendingChanges, PendingIncreaseNoStock, OpenPurchasePasses, RecruitmentPendingChanges;
            internal int PendingFirst, PendingLast, PendingMin, PendingMax;
            internal bool PendingSeen;
            internal bool Any => BuyCalls != 0 || SellCalls != 0 || PendingSeen || RecruitmentPendingChanges != 0;
            internal void Pending(int before, int after)
            {
                if (!PendingSeen) { PendingFirst = before; PendingMin = before; PendingMax = before; PendingSeen = true; }
                PendingLast = after;
                PendingMin = Math.Min(PendingMin, Math.Min(before, after));
                PendingMax = Math.Max(PendingMax, Math.Max(before, after));
            }
        }
        private struct PairTotals { internal long Decisions, Recruitments, Incomplete, ResourceFailure; }
        private sealed class Summary
        {
            internal readonly Totals[] Goods = new Totals[(PlayerCount + 1) * GoodCount];
            internal readonly PairTotals[] Players = new PairTotals[PlayerCount + 1];
            internal long Session, FirstSequence, LastSequence, FirstUtc, LastUtc, Extended;
            internal int FirstTick, LastTick, NextPlayer;
            internal bool Final, HeaderWritten;
            internal void Clear(long id, long sequence)
            {
                Array.Clear(Goods, 0, Goods.Length); Array.Clear(Players, 0, Players.Length);
                Session = id; FirstSequence = LastSequence = sequence;
                FirstUtc = LastUtc = DateTime.UtcNow.Ticks;
                FirstTick = LastTick = 0; Extended = 0; NextPlayer = 1; Final = HeaderWritten = false;
            }
        }

        private void InitializeBuffers()
        {
            for (int i = 0; i < summaries.Length; i++) summaries[i] = new Summary();
        }

        private bool BeginSession(long id)
        {
            if (summaryWritten - Volatile.Read(ref summaryRead) >= SummaryCapacity)
            { Interlocked.Increment(ref sessionBacklogs); return false; }
            currentSummary = summaries[(int)(summaryWritten % SummaryCapacity)];
            currentSummary.Clear(id, sequence);
            normalDetails = cycleDetails = 0;
            Array.Clear(seenDetails, 0, seenDetails.Length);
            Array.Clear(hasBuy, 0, hasBuy.Length);
            Array.Clear(lastBuy, 0, lastBuy.Length);
            MarketStaleEquipmentFix.Reset(id);
            nextSummaryClock = Stopwatch.GetTimestamp() + SummaryClockInterval;
            return true;
        }

        private bool PublishSummary(bool final)
        {
            if (currentSummary == null) return true;
            if (!final && summaryWritten - Volatile.Read(ref summaryRead) >= SummaryCapacity - 1)
            {
                currentSummary.Extended++;
                // Continue accumulating all operations; never wait for the output thread.
                nextSummaryClock = Stopwatch.GetTimestamp() + SummaryClockInterval;
                return false;
            }
            Summary next = null;
            if (!final)
            {
                next = summaries[(int)((summaryWritten + 1) % SummaryCapacity)];
                next.Clear(currentSummary.Session, sequence);
                next.FirstTick = lastMapTick;
            }
            currentSummary.Final = final;
            currentSummary.LastTick = lastMapTick;
            currentSummary.LastSequence = sequence;
            currentSummary.LastUtc = DateTime.UtcNow.Ticks;
            Volatile.Write(ref summaryWritten, summaryWritten + 1);
            currentSummary = next;
            return true;
        }

        private void CountPair(Frame frame, Profile profile, bool completed)
        {
            frame.EndSequence = ++sequence;
            if (currentSummary == null) return;
            ref PairTotals p = ref currentSummary.Players[frame.Player];
            if (frame.Kind == DecisionKind) p.Decisions++; else p.Recruitments++;
            if (!completed) p.Incomplete++;
            if (profile.Failure == 2) p.ResourceFailure++; // Raw native failure enum: missing recruitment resource.
            MarkSummary(frame.BeforeProfile.Tick, profile.Tick);
        }

        private void ObservePairGood(Frame frame, int good, Sample after, Profile profile, bool completed)
        {
            Sample before = frame.Before[good];
            if (completed && frame.Kind == RecruitKind)
                MarketStaleEquipmentFix.ObserveRecruitment(frame.Player, good, before.Stock, after.Stock,
                    before.Pending, after.Pending, profile.AicId, profile.BuyWeapons, profile.LastStand, after.Tick, frame.Sequence);
            if (completed) MarketStaleEquipmentFix.ObservePendingState(frame.Player, good, after.Pending);
            if (before.Pending == 0 && after.Pending == 0 && before.Stock == after.Stock) return;
            ref Totals stats = ref currentSummary.Goods[frame.Player * GoodCount + good];
            stats.Pending(before.Pending, after.Pending);
            if (before.Pending != after.Pending)
            {
                stats.PendingChanges++;
                if (after.Pending > before.Pending && after.Stock <= before.Stock) stats.PendingIncreaseNoStock++;
                if (frame.Kind == RecruitKind) stats.RecruitmentPendingChanges++;
                Retain(frame.Kind, frame.Player, good, new Attempt
                {
                    Sequence = frame.Sequence, EndSequence = frame.EndSequence, Parent = frame.Parent,
                    OriginalCompleted = completed, Before = before, After = after,
                    ProfileBefore = frame.BeforeProfile, ProfileAfter = profile
                }, default);
            }
            if (frame.Kind == DecisionKind && before.Pending > 0 && after.Pending > 0) stats.OpenPurchasePasses++;
        }

        private void ObserveTrade(Frame frame, int result, Sample after, Profile profile, bool completed)
        {
            var attempt = new Attempt
            {
                Sequence = frame.Sequence, EndSequence = ++sequence, Parent = frame.Parent,
                Requested = frame.Requested, Result = result, OriginalCompleted = completed,
                Before = frame.TradeBefore, After = after, ProfileBefore = frame.BeforeProfile, ProfileAfter = profile
            };
            MarkSummary(frame.BeforeProfile.Tick, profile.Tick);
            if ((uint)frame.Good >= (uint)GoodCount)
            { Interlocked.Increment(ref invalidContexts); Retain(frame.Kind, frame.Player, frame.Good, attempt, default); return; }
            int index = frame.Player * GoodCount + frame.Good;
            ref Totals stats = ref currentSummary.Goods[index];
            stats.Pending(attempt.Before.Pending, after.Pending);
            long stockDelta = (long)after.Stock - attempt.Before.Stock;
            long goldDelta = (long)after.Gold - attempt.Before.Gold;
            if (frame.Kind == BuyKind)
            {
                stats.BuyCalls++; stats.BuyRequested += frame.Requested;
                if (completed && result != 0) stats.BuySuccess++;
                if (completed) { stats.BoughtUnits += stockDelta; stats.BuyDebit -= goldDelta; }
                if (completed && result != 0 && stockDelta > 0)
                {
                    stats.BuyStockGain++;
                    Interlocked.Increment(ref actualBuys);
                    lastBuy[index] = attempt;
                    hasBuy[index] = true;
                }
            }
            else
            {
                stats.SellCalls++; stats.SellRequested += frame.Requested;
                if (completed) { stats.SoldUnits -= stockDelta; stats.SellCredit += goldDelta; }
                if (completed && stockDelta < 0)
                {
                    stats.SellStockLoss++;
                    Interlocked.Increment(ref actualSales);
                    if (hasBuy[index])
                    {
                        stats.Cycles++;
                        Interlocked.Increment(ref observedCycles);
                        Retain(CycleKind, frame.Player, frame.Good, attempt, lastBuy[index]);
                        hasBuy[index] = false;
                    }
                }
            }
            if (completed)
                MarketBacklogScenario.ObserveTrade(frame.Player, frame.Good, frame.Requested, result,
                    stockDelta, goldDelta, frame.Kind == BuyKind, frame.TradeBefore.Stock, frame.TradeBefore.Tick);
            Retain(frame.Kind, frame.Player, frame.Good, attempt, default);
        }

        private void MarkSummary(int before, int after)
        {
            if (currentSummary.FirstSequence == currentSummary.LastSequence) currentSummary.FirstTick = before;
            currentSummary.LastSequence = sequence;
            currentSummary.LastTick = after;
        }

        private void Retain(int kind, int player, int good, Attempt attempt, Attempt buy)
        {
            bool validGood = (uint)good < (uint)GoodCount;
            int index = validGood ? player * GoodCount + good : -1;
            int mask = 1 << kind;
            if (validGood && (seenDetails[index] & mask) != 0) return;
            // Mark omissions too: these are unique first-example omissions, not a repeat storm.
            if (validGood) seenDetails[index] |= mask;
            if ((kind == CycleKind ? cycleDetails >= CycleReserve : normalDetails >= DetailLimit - CycleReserve) ||
                detailWritten - Volatile.Read(ref detailRead) >= DetailRingCapacity)
            { Interlocked.Increment(ref detailsOmitted); return; }
            details[(int)(detailWritten % DetailRingCapacity)] = new DetailRecord
            { Session = session, Kind = kind, Player = player, Good = good, Current = attempt, Buy = buy };
            if (kind == CycleKind) cycleDetails++; else normalDetails++;
            Volatile.Write(ref detailWritten, detailWritten + 1);
        }

        // The sink may block or throw. Only the dedicated output thread calls Drain;
        // capture never waits for it and never shares mutable native pointers with it.
        internal void Drain(Action<string> sink)
        {
            if (sink == null) throw new ArgumentNullException(nameof(sink));
            while (detailRead < Volatile.Read(ref detailWritten))
            {
                DetailRecord record = details[(int)(detailRead % DetailRingCapacity)];
                sink(FormatDetail(record));
                Volatile.Write(ref detailRead, detailRead + 1);
            }
            while (summaryRead < Volatile.Read(ref summaryWritten))
            {
                Summary summary = summaries[(int)(summaryRead % SummaryCapacity)];
                if (!summary.HeaderWritten)
                {
                    sink("AI_MARKET_TRADE_INTERVAL: session=" + summary.Session + ", seq=" + summary.FirstSequence + ".." + summary.LastSequence +
                        ", mapTick=" + summary.FirstTick + ".." + summary.LastTick + ", utc=" + Utc(summary.FirstUtc) + ".." + Utc(summary.LastUtc) +
                        ", extended=" + summary.Extended + ", final=" + summary.Final + ", nativeCalls=" + Interlocked.Read(ref nativeCalls) +
                        ", captureFailures=" + Interlocked.Read(ref captureFailures) + ", invalidContexts=" + Interlocked.Read(ref invalidContexts) +
                        ", actualBuys=" + Interlocked.Read(ref actualBuys) + ", actualSales=" + Interlocked.Read(ref actualSales) + ", executedPairs=" + Interlocked.Read(ref observedCycles) +
                        ", missingMapTicks=" + Interlocked.Read(ref missingMapTicks) + ", depthOverflows=" + Interlocked.Read(ref depthOverflows) +
                        ", staleFrames=" + Interlocked.Read(ref staleFrames) + ", originalExceptions=" + Interlocked.Read(ref originalExceptions) +
                        ", captureUsProcess=" + (Interlocked.Read(ref captureClockTicks) * 1000000.0 / Stopwatch.Frequency).ToString("F1", System.Globalization.CultureInfo.InvariantCulture) +
                        ", detailLimit=" + DetailLimit + ", cycleReserved=" + CycleReserve + ", detailsOmitted=" + Interlocked.Read(ref detailsOmitted) +
                        ", skippedSessions=" + Interlocked.Read(ref sessionBacklogs) + ", queryIsNotTrade=true; pairIsNotUnnecessaryTradeProof=true.");
                    summary.HeaderWritten = true;
                }
                while (summary.NextPlayer <= PlayerCount)
                {
                    int player = summary.NextPlayer;
                    PairTotals pairs = summary.Players[player];
                    bool any = pairs.Decisions != 0 || pairs.Recruitments != 0;
                    for (int good = 0; !any && good < GoodCount; good++) any |= summary.Goods[player * GoodCount + good].Any;
                    if (any) sink(FormatSummary(summary, player));
                    summary.NextPlayer++;
                }
                Volatile.Write(ref summaryRead, summaryRead + 1);
            }
        }

        private static string FormatSummary(Summary summary, int player)
        {
            PairTotals pairs = summary.Players[player];
            var text = new StringBuilder("AI_MARKET_TRADE_SUMMARY: session=");
            text.Append(summary.Session).Append(", mapTick=").Append(summary.FirstTick).Append("..").Append(summary.LastTick)
                .Append(", player=").Append(player).Append(", decisionCalls=").Append(pairs.Decisions)
                .Append(", recruitmentCalls=").Append(pairs.Recruitments).Append(", incompleteCalls=").Append(pairs.Incomplete)
                .Append(", resourceFailureObserved=").Append(pairs.ResourceFailure).Append(", goods=[");
            bool separator = false;
            for (int good = 0; good < GoodCount; good++)
            {
                Totals g = summary.Goods[player * GoodCount + good];
                if (!g.Any) continue;
                if (separator) text.Append(';'); separator = true;
                text.Append(good).Append(":buy=").Append(g.BuyCalls).Append('/').Append(g.BuySuccess).Append('/').Append(g.BuyStockGain)
                    .Append(",requested=").Append(g.BuyRequested).Append(",actual=").Append(g.BoughtUnits).Append(",debit=").Append(g.BuyDebit)
                    .Append(",sell=").Append(g.SellCalls).Append('/').Append(g.SellStockLoss).Append(",requested=").Append(g.SellRequested)
                    .Append(",actual=").Append(g.SoldUnits).Append(",credit=").Append(g.SellCredit).Append(",pairs=").Append(g.Cycles)
                    .Append(",pending=").Append(g.PendingFirst).Append('>').Append(g.PendingLast).Append('~').Append(g.PendingMin).Append(':').Append(g.PendingMax)
                    .Append(",changes=").Append(g.PendingChanges).Append(",increasesWithoutStock=").Append(g.PendingIncreaseNoStock)
                    .Append(",openBuyPasses=").Append(g.OpenPurchasePasses).Append(",recruitDemandChanges=").Append(g.RecruitmentPendingChanges);
            }
            return text.Append("].").ToString();
        }

        private static string FormatDetail(DetailRecord record)
        {
            var text = new StringBuilder("AI_MARKET_TRADE_DETAIL: session=");
            text.Append(record.Session).Append(", kind=").Append(KindName(record.Kind)).Append(", player=").Append(record.Player)
                .Append(", good=").Append(record.Good).Append(", ");
            if (record.Kind == CycleKind)
            { text.Append("actualBuy={"); AppendAttempt(text, record.Buy); text.Append("}, actualSell={"); AppendAttempt(text, record.Current); text.Append("}, unnecessaryCauseUnconfirmed=true"); }
            else AppendAttempt(text, record.Current);
            return text.Append('.').ToString();
        }
        private static string KindName(int kind) => kind == DecisionKind ? "purchase-decision" : kind == RecruitKind ? "recruitment" :
            kind == BuyKind ? "buy" : kind == SellKind ? "sell" : "executed-buy-then-sell";
        private static void AppendAttempt(StringBuilder b, Attempt a)
        {
            b.Append("seq=").Append(a.Sequence).Append("..").Append(a.EndSequence).Append(",parent=").Append(a.Parent)
                .Append(",requestedSigned=").Append(a.Requested).Append(",return=").Append(a.Result)
                .Append(",originalCompleted=").Append(a.OriginalCompleted).Append(",before=");
            AppendSample(b, a.Before); b.Append(",after="); AppendSample(b, a.After);
            b.Append(",goldDelta=").Append((long)a.After.Gold - a.Before.Gold).Append(",stockDelta=").Append((long)a.After.Stock - a.Before.Stock)
                .Append(",profileBefore="); AppendProfile(b, a.ProfileBefore); b.Append(",profileAfter="); AppendProfile(b, a.ProfileAfter);
        }
        private static void AppendSample(StringBuilder b, Sample s) => b.Append(s.Tick).Append('@').Append(Utc(s.Utc))
            .Append("(gold=").Append(s.Gold).Append(",stock=").Append(s.Stock).Append(",pending=").Append(s.Pending)
            .Append(",suppressSellAllTimerSigned=").Append(s.SuppressSellAllTimer)
            .Append(",experimentPhase=").Append(s.ExperimentPhase).Append(",interventionSequence=").Append(s.InterventionSequence)
            .Append(",phase=").Append(s.Phase).Append(",flags=").Append(s.Flags).Append(')');
        private static void AppendProfile(StringBuilder b, Profile p) => b.Append("(aicNativeIndex=").Append(p.AicId)
            .Append(",buy_weapons=").Append(p.BuyWeapons).Append(",trade_weapons_at=").Append(p.SellWeapons)
            .Append(",trade_minimum=").Append(p.TradeMinimum).Append(",base_gold_reserves=").Append(p.BaseGoldReserve)
            .Append(",buyBreadCheeseApplesAmount=").Append(p.BuyBreadAt).Append('/').Append(p.BuyCheeseAt).Append('/').Append(p.BuyApplesAt).Append('/').Append(p.BuyFoodAmount)
            .Append(",vlow_popularity=").Append(p.LowPopularity).Append(",popularity=").Append(p.Popularity)
            .Append(",lastStand=").Append(p.LastStand).Append(",rawPurchaseGate3974=").Append(p.PurchaseGate)
            .Append(",market=").Append(p.MarketId).Append('/').Append(p.CachedMarket).Append(",recruitmentMode=").Append(p.RecruitmentMode)
            .Append(",recruitmentFailure=").Append(p.Failure).Append(",missingGood=").Append(p.MissingGood)
            .Append(",sellAllGoodsMask=").Append(p.SellAllMask.ToString("X", System.Globalization.CultureInfo.InvariantCulture))
            .Append(",sellAllNonGoodRaw=").Append(p.NonGoodsSellAll).Append(",sellThresholdsFoodWoodStoneResourcesFlourAlePitch=")
            .Append(p.SellFood).Append('/').Append(p.SellWood).Append('/').Append(p.SellStone).Append('/').Append(p.SellResources)
            .Append('/').Append(p.SellFlour).Append('/').Append(p.SellAle).Append('/').Append(p.SellPitch).Append(",flags=").Append(p.Flags).Append(')');
        private static string Utc(long ticks) => ticks == 0 ? "none" : new DateTime(ticks, DateTimeKind.Utc).ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);
    }
}
#endif
