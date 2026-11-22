#if SERP_DEVDIAG
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using APIShared;
using APIShared.Automation;
using APIShared.Simulation;
using SHCDESE.API;
using SHCDESE.Interop;

namespace AIMarketOverbuyDiagnostic
{
    // Deliberately mutating experiment, separate from the read-only diagnostic.
    // Only the owning automation scenario calls Run after readiness validation.
    internal static class MarketBacklogScenario
    {
        internal const int FundedGold = 100000;
        internal static int Phase, InterventionActive, LastNativeTick;
        internal static long InterventionSequence, GoldAdded, RemovedUnits, RemovalCalls, MaxSliceClockTicks;
        internal static readonly long[] RemovedByGood = new long[(int)eGoods.Count];
        internal static readonly long[] RemovedByPlayer = new long[GamePlayerManagerAPI.MAX_PLAYERS + 1];
        private static readonly Intervention[] recent = new Intervention[256];
        private static readonly object eventGate = new object();
        private static bool started;
        private static bool tickRegistered;
        private static volatile bool active;
        internal static bool Active => active;
        internal static readonly long[] FoodSingleBuysByPhase = new long[5];
        internal static readonly long[] FoodBuyUnitsByPhase = new long[5];
        internal static readonly long[] EquipmentBuysByPhase = new long[5];
        internal static readonly long[] EquipmentSalesByPhase = new long[5];
        internal static readonly long[] BuySpendByPhase = new long[5];
        internal static readonly long[,] FoodSingleBuysByPhasePlayer = new long[5, GamePlayerManagerAPI.MAX_PLAYERS + 1];
        internal static readonly long[,] EquipmentBuysByPhasePlayer = new long[5, GamePlayerManagerAPI.MAX_PLAYERS + 1];
        internal static readonly long[,] PersistentEquipmentPendingBuyPhases = new long[5, GamePlayerManagerAPI.MAX_PLAYERS + 1];
        internal static readonly long[,] PurchasePhases = new long[5, GamePlayerManagerAPI.MAX_PLAYERS + 1];
        private static bool[] targetActors;
        private static MissionContext activeMission;
        private static Exception tickFailure;
        private static readonly int[] previousBuyPhase = new int[GamePlayerManagerAPI.MAX_PLAYERS + 1];
        private static readonly eGoods[] Foods = { eGoods.STORED_FOOD_BREAD, eGoods.STORED_FOOD_CHEESE, eGoods.STORED_FOOD_FRUIT };
        internal static long CompletedBuyPhases, StressRemovedUnits, StressTickCalls, MaxStressTickClockTicks;
        internal static int GiftPlayer, GiftGood, GiftTrackedAmount, GiftTick, GiftUnits, GiftOriginalTarget;
        internal static int GiftFirstBuyTick, GiftFirstBuyStock, GiftFirstBuyAmount;
        internal static long GiftGoodBuysAfterDelivery, GiftExcessUnitsBought;
        internal static int TerminalProbeTick;

        internal static void ObserveTrade(int player, int good, int requested, int result,
            long stockDelta, long goldDelta, bool isBuy, int beforeStock, int nativeTick)
        {
            if (!active || targetActors == null || player <= 0 || player >= targetActors.Length || !targetActors[player]) return;
            int phase = Volatile.Read(ref Phase);
            if (phase < 1 || phase > 4) return;
            bool equipment = good >= (int)eGoods.STORED_BOWS && good <= (int)eGoods.STORED_METAL_ARMOUR;
            bool food = good == (int)eGoods.STORED_FOOD_BREAD || good == (int)eGoods.STORED_FOOD_CHEESE || good == (int)eGoods.STORED_FOOD_FRUIT;
            if (isBuy && result == 1 && stockDelta > 0 && goldDelta < 0)
            {
                Interlocked.Add(ref BuySpendByPhase[phase], -goldDelta);
                if (equipment)
                {
                    Interlocked.Increment(ref EquipmentBuysByPhase[phase]);
                    Interlocked.Increment(ref EquipmentBuysByPhasePlayer[phase, player]);
                }
                if (GiftUnits > 0 && player == GiftPlayer && good == GiftGood)
                {
                    Interlocked.Increment(ref GiftGoodBuysAfterDelivery);
                    long excess = Math.Min(stockDelta, Math.Max(0L, (long)beforeStock + stockDelta - GiftOriginalTarget));
                    Interlocked.Add(ref GiftExcessUnitsBought, excess);
                    if (GiftFirstBuyTick == 0)
                    {
                        GiftFirstBuyStock = beforeStock; GiftFirstBuyAmount = checked((int)stockDelta);
                        Volatile.Write(ref GiftFirstBuyTick, nativeTick);
                    }
                }
                if (food)
                {
                    Interlocked.Add(ref FoodBuyUnitsByPhase[phase], stockDelta);
                    if (requested == 1 && stockDelta == 1)
                    {
                        Interlocked.Increment(ref FoodSingleBuysByPhase[phase]);
                        Interlocked.Increment(ref FoodSingleBuysByPhasePlayer[phase, player]);
                    }
                }
            }
            if (!isBuy && equipment && stockDelta < 0 && goldDelta > 0)
                Interlocked.Increment(ref EquipmentSalesByPhase[phase]);
        }

        // Numeric markers distinguish deliberate removals from native recruitment/trades.
        // 0=setup, 1=control, 2=single-batch stress, 3=batch-eight control, 4=recovery, 5=done.
        internal struct Intervention
        {
            internal long Sequence;
            internal int Tick, Phase, Player, Good, Before, After, GoldBefore, GoldAfter;
        }

        internal static bool TryCopyIntervention(long sequence, out Intervention value)
        {
            lock (eventGate)
            {
                value = default;
                if (sequence <= 0 || sequence > InterventionSequence || InterventionSequence - sequence >= recent.Length) return false;
                value = recent[(int)((sequence - 1) % recent.Length)];
                return value.Sequence == sequence;
            }
        }

        internal static IEnumerable<AutomationWait> Run(AutomationRun run,
            int controlSeconds = 60, int stressSeconds = 120, int batchControlSeconds = 60, int recoverySeconds = 60)
        {
            if (started) throw new InvalidOperationException("Backlog experiment is process-single-use.");
            if (run == null || controlSeconds < 10 || stressSeconds < 10 || batchControlSeconds < 10 || recoverySeconds < 10)
                throw new ArgumentException("Explicit valid phase durations required.");
            var goods = Foods;
            MissionContext mission = run.CurrentMission;
            if (mission == null || mission.IsEditor || mission.Mode.IsRealMultiplayer)
                throw new InvalidOperationException("Backlog experiment requires a ready local gameplay mission.");
            var actors = new bool[GamePlayerManagerAPI.MAX_PLAYERS + 1];
            int actorCount = run.InSimulation(mission, () => SelectActors(actors, goods));
            if (actorCount == 0) throw new InvalidOperationException("No living AI with a cached market and trade-post ID.");
            started = true;
            var clock = Stopwatch.StartNew();
            double statusAt = 0, progressAt = 0;
            int previousTick = 0, previousPhase = -1, goodIndex = 0;
            bool tickSeen = false;
            Func<int> slice = () => ApplySlice(mission, actors, goods[goodIndex]);
            Shared.DevDiag.Log(() => "MARKET_BACKLOG_BEGIN run=" + MarketAutomation.RunId + " actors=" + actorCount +
                " controlSeconds=" + controlSeconds + " stressSeconds=" + stressSeconds + " batchControlSeconds=" + batchControlSeconds +
                " recoverySeconds=" + recoverySeconds + " fundedGold=" + FundedGold +
                " goods=" + string.Join(",", goods) + " pendingWrites=0 setupTargetStock=1 stressMaxRemoval=1 recoveryAdds=0");
            try
            {
                // AIC rules are identical throughout all three measurement phases.
                run.InSimulation(mission, () => {
                    for (int player = 1; player < actors.Length; player++) if (actors[player]) MarketBacklogAic.Configure(player);
                    return true;
                });
                Volatile.Write(ref Phase, 0);
                for (goodIndex = 0; goodIndex < goods.Length; goodIndex++)
                {
                    run.InSimulation(mission, slice);
                    yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(250));
                }
                goodIndex = 0;
                targetActors = actors; activeMission = mission;
                for (int player = 0; player < previousBuyPhase.Length; player++) previousBuyPhase[player] = -1;
                if (!tickRegistered) { GameTimeManagerAPI.Instance.OnTick += OnTick; tickRegistered = true; }
                active = true;
                clock.Restart();
                while (true)
                {
                    Exception failure = Volatile.Read(ref tickFailure);
                    if (failure != null) throw new InvalidOperationException("Backlog simulation intervention failed.", failure);
                    int phase = clock.Elapsed.TotalSeconds < controlSeconds ? 1 :
                        clock.Elapsed.TotalSeconds < controlSeconds + stressSeconds ? 2 :
                        clock.Elapsed.TotalSeconds < controlSeconds + stressSeconds + batchControlSeconds ? 3 : 4;
                    if (phase != previousPhase)
                    {
                        run.InSimulation(mission, () => {
                            MarketBacklogAic.SetBatchAmount(phase == 3 ? 8 : 1);
                            Volatile.Write(ref Phase, phase); // Publish with the AIC change while simulation is locked.
                            if (phase == 2) PrepareEquipmentDeficit(mission, actors);
                            return true;
                        });
                        previousPhase = phase;
                        LogStatus("phase-start");
                    }
                    int tick = run.InSimulation(mission, slice);
                    if (phase == 2 && GiftUnits == 0 && clock.Elapsed.TotalSeconds >= controlSeconds + stressSeconds * (2.0 / 3.0))
                        run.InSimulation(mission, () => TryGiftTrackedEquipment(mission, actors));
                    if (MarketAutomation.Scenario == "market-backlog-fixed" && GiftUnits > 0 && TerminalProbeTick == 0)
                        run.InSimulation(mission, () => TryTerminalSupplyProbe(mission));
                    if (!tickSeen || tick != previousTick) { progressAt = clock.Elapsed.TotalSeconds; previousTick = tick; tickSeen = true; }
                    if (clock.Elapsed.TotalSeconds - progressAt >= 120) throw new InvalidOperationException("Backlog experiment native clock stalled.");
                    goodIndex = (goodIndex + 1) % goods.Length;
                    if (clock.Elapsed.TotalSeconds >= statusAt)
                    {
                        LogStatus("interval");
                        statusAt = clock.Elapsed.TotalSeconds + 10;
                    }
                    if (clock.Elapsed.TotalSeconds >= controlSeconds + stressSeconds + batchControlSeconds + recoverySeconds) break;
                    yield return AutomationWait.Delay(TimeSpan.FromMilliseconds(250));
                }
                if (StressRemovedUnits == 0) throw new InvalidOperationException("Stress phase produced no confirmed recurring stock removals.");
                bool confirmed = false;
                for (int player = 1; player < actors.Length; player++)
                    if (actors[player] && FoodSingleBuysByPhasePlayer[2, player] >= 10 && PersistentEquipmentPendingBuyPhases[2, player] >= 10)
                        confirmed = true;
                if (!confirmed) throw new InvalidOperationException("No same target AI has ten real one-unit food buys and ten equipment-pending purchase phases during stress.");
                if (GiftUnits <= 0 || GiftTrackedAmount <= 0)
                    throw new InvalidOperationException("No physically fulfilled genuinely tracked native equipment request was tested.");
                if (MarketAutomation.Scenario == "market-backlog-fixed")
                {
                    if (MarketPendingDemandFix.GetReductionCount(GiftPlayer, GiftGood) == 0 || GiftExcessUnitsBought != 0)
                        throw new InvalidOperationException("The correction did not reduce the fulfilled request or still overbought its original stock target.");
                    if (TerminalProbeTick == 0)
                        throw new InvalidOperationException("No fresh native order was fully supplied and immediately consumed to verify terminal closure.");
                }
                else if (GiftExcessUnitsBought == 0)
                    throw new InvalidOperationException("The stale-request excess purchase was not reproduced; conditions are insufficient.");
            }
            finally
            {
                active = false;
                MarketPendingDemandFix.DeactivateExperiment();
                try { run.InSimulation(mission, () => { MarketBacklogAic.RestoreAll(); return true; }); }
                finally { Volatile.Write(ref Phase, 5); LogStatus("end"); }
            }
        }

        private static unsafe int SelectActors(bool[] actors, eGoods[] goods)
        {
            var players = GamePlayerManagerAPI.Instance;
            foreach (eGoods good in goods)
                if (!players.IsTradeGoodAllowed(good)) throw new InvalidOperationException("Deficit good cannot be traded: " + good);
            int count = 0;
            for (int player = 1; player < actors.Length; player++)
            {
                if (!players.IsAIPlayer(player) || !players.TryGetPlayerResourcesById(player, out GamePlayerResources* r)) continue;
                if (players.GetAILord(player) < Enums.AILords.SK_X1 || players.GetAILord(player) >= Enums.AILords.SK_TEMP) continue;
                if (r->r_ExistingPeasants == 0 || r->r_bCachedTradePostExists == 0 || r->r_TradePostId == 0 || r->r_FirstGranaryId == 0) continue;
                if (!MarketBacklogAic.CanTestEquipmentBuyer(player)) continue;
                actors[player] = true; count++;
            }
            return count;
        }

        private static unsafe void PrepareEquipmentDeficit(MissionContext mission, bool[] actors)
        {
            if (!SimulationClock.TryGetCurrentTick(mission, out int tick)) throw new InvalidOperationException("Equipment setup clock unavailable.");
            var players = GamePlayerManagerAPI.Instance;
            for (int player = 1; player < actors.Length; player++)
            {
                if (!actors[player]) continue;
                for (int good = (int)eGoods.STORED_BOWS; good <= (int)eGoods.STORED_METAL_ARMOUR; good++)
                {
                    int before = players.GetGoodAmount(player, (eGoods)good), gold = players.GetPlayerGold(player);
                    if (before <= 0) continue;
                    Volatile.Write(ref InterventionActive, 1);
                    try
                    {
                        players.RemoveGood(player, (eGoods)good, before);
                        int after = players.GetGoodAmount(player, (eGoods)good), afterGold = players.GetPlayerGold(player);
                        Record(tick, player, good, before, after, gold, afterGold);
                        if (after != 0 || afterGold != gold) throw new InvalidOperationException("Equipment deficit setup accounting failed.");
                        RemovedUnits += before; RemovalCalls++; RemovedByGood[good] += before; RemovedByPlayer[player] += before;
                        int target = player, item = good;
                        Shared.DevDiag.Log(() => "MARKET_STALE_SETUP run=" + MarketAutomation.RunId + " nativeTick=" + tick +
                            " phase=" + Phase + " player=" + target + " good=" + item + " stockBefore=" + before + " stockAfter=0 gold=" + gold + " pendingWrites=0");
                    }
                    finally { Volatile.Write(ref InterventionActive, 0); }
                }
            }
        }

        // A second, separately marked probe leaves the original matched gift intact.
        // All SDK operations share one simulation lock. First consume a partial
        // delivery, then fulfill and consume the rest without an intervening AI run.
        private static unsafe bool TryTerminalSupplyProbe(MissionContext mission)
        {
            var players = GamePlayerManagerAPI.Instance;
            int player = GiftPlayer, good = GiftGood;
            if (player <= 0 || !MarketPendingDemandFix.TryGetTrackedRequest(player, good, out int amount) || amount <= 1 ||
                !MarketPendingDemandFix.TryGetProvenance(player, good, out int sourceTick, out long sourceSequence, out int target) || target <= 0 ||
                sourceTick <= GiftTick || players.GetGoodAmount(player, (eGoods)good) != 0) return false;
            if (!players.TryGetPlayerResourcesById(player, out GamePlayerResources* resources) || resources == null) return false;
            int* pending = (int*)&resources->r_AIPendingMarketPurchaseAmountNull;
            if (pending[good] != amount) return false;
            if (!SimulationClock.TryGetCurrentTick(mission, out int tick)) throw new InvalidOperationException("Terminal probe clock unavailable.");
            int gold = players.GetPlayerGold(player);
            Volatile.Write(ref InterventionActive, 1);
            try
            {
                bool partialAdded = players.TryAddGood(player, (eGoods)good, 1);
                int partialStock = players.GetGoodAmount(player, (eGoods)good), afterPartialPending = pending[good];
                Record(tick, player, good, 0, partialStock, gold, players.GetPlayerGold(player));
                if (!partialAdded || partialStock != 1 || afterPartialPending != amount - 1 || players.GetPlayerGold(player) != gold)
                    throw new InvalidOperationException("Partial delivery was not credited to its owned package.");
                players.RemoveGood(player, (eGoods)good, 1);
                int partialConsumedStock = players.GetGoodAmount(player, (eGoods)good), afterPartialConsumptionPending = pending[good];
                Record(tick, player, good, partialStock, partialConsumedStock, gold, players.GetPlayerGold(player));
                if (partialConsumedStock != 0 || afterPartialConsumptionPending != amount - 1 || players.GetPlayerGold(player) != gold)
                    throw new InvalidOperationException("Consumption refunded a delivery already credited to the package.");
                int finalDelivery = afterPartialConsumptionPending;
                bool added = players.TryAddGood(player, (eGoods)good, finalDelivery);
                int supplied = players.GetGoodAmount(player, (eGoods)good), afterSupplyPending = pending[good];
                Record(tick, player, good, 0, supplied, gold, players.GetPlayerGold(player));
                if (!added || supplied != finalDelivery || afterSupplyPending != 0 || players.GetPlayerGold(player) != gold)
                    throw new InvalidOperationException("Completed supply did not close its owned package before consumption.");
                players.RemoveGood(player, (eGoods)good, finalDelivery);
                int remaining = players.GetGoodAmount(player, (eGoods)good), afterConsumptionPending = pending[good];
                int afterGold = players.GetPlayerGold(player);
                Record(tick, player, good, supplied, remaining, gold, afterGold);
                if (remaining != 0 || afterConsumptionPending != 0 || afterGold != gold ||
                    MarketPendingDemandFix.TryGetTrackedRequest(player, good, out _))
                    throw new InvalidOperationException("Immediate consumption reopened an already fulfilled package.");
                TerminalProbeTick = tick;
                Shared.DevDiag.Log(() => "MARKET_TERMINAL_SUPPLY_PASS run=" + MarketAutomation.RunId +
                    " nativeTick=" + tick + " sourceTick=" + sourceTick + " sourceSequence=" + sourceSequence +
                    " player=" + player + " good=" + good + " originalPending=" + amount + " package=" + target +
                    " partialDelivery=1 pendingAfterPartial=" + afterPartialPending +
                    " pendingAfterPartialConsumption=" + afterPartialConsumptionPending +
                    " supplied=" + supplied + " pendingAfterSupply=" + afterSupplyPending +
                    " stockAfterConsumption=" + remaining + " pendingAfterConsumption=" + afterConsumptionPending +
                    " goldBefore=" + gold + " goldAfter=" + afterGold + " fixturePendingWrites=0");
                return true;
            }
            finally { Volatile.Write(ref InterventionActive, 0); }
        }

        private static unsafe bool TryGiftTrackedEquipment(MissionContext mission, bool[] actors)
        {
            if (GiftUnits != 0) return true;
            if (!SimulationClock.TryGetCurrentTick(mission, out int tick)) throw new InvalidOperationException("Equipment gift clock unavailable.");
            var players = GamePlayerManagerAPI.Instance;
            for (int player = 1; player < actors.Length; player++)
            {
                if (!actors[player] || FoodSingleBuysByPhasePlayer[2, player] < 10 || PersistentEquipmentPendingBuyPhases[2, player] < 10) continue;
                if (!players.TryGetPlayerResourcesById(player, out GamePlayerResources* resources)) continue;
                uint* pending = &resources->r_AIPendingMarketPurchaseAmountNull;
                for (int good = (int)eGoods.STORED_BOWS; good <= (int)eGoods.STORED_METAL_ARMOUR; good++)
                {
                    if (!MarketPendingDemandFix.TryGetTrackedRequest(player, good, out int amount) || amount <= 0 || pending[good] != (uint)amount) continue;
                    if (!MarketPendingDemandFix.TryGetProvenance(player, good, out int sourceTick, out long sourceSequence, out int originalTarget)) continue;
                    int gift = checked(amount * 2);
                    int before = players.GetGoodAmount(player, (eGoods)good), gold = players.GetPlayerGold(player);
                    uint pendingBefore = pending[good];
                    Volatile.Write(ref InterventionActive, 1);
                    try
                    {
                        bool added = players.TryAddGood(player, (eGoods)good, gift);
                        int after = players.GetGoodAmount(player, (eGoods)good), afterGold = players.GetPlayerGold(player);
                        Record(tick, player, good, before, after, gold, afterGold);
                        if (!added || after != checked(before + gift) || afterGold != gold || pending[good] != pendingBefore)
                            throw new InvalidOperationException("Tracked equipment gift failed stock/gold/pending invariants.");
                        uint pendingAfter = pending[good];
                        GiftPlayer = player; GiftGood = good; GiftTick = tick; GiftTrackedAmount = amount;
                        GiftOriginalTarget = originalTarget; GiftUnits = gift;
                        Shared.DevDiag.Log(() => "MARKET_STALE_GIFT run=" + MarketAutomation.RunId + " nativeTick=" + GiftTick +
                            " phase=" + Phase + " player=" + GiftPlayer + " good=" + GiftGood + " trackedAmount=" + GiftTrackedAmount +
                            " trackedInitialStock=0 sourceTick=" + sourceTick + " sourceSequence=" + sourceSequence +
                            " originalTarget=" + originalTarget + " gift=" + GiftUnits + " stockBefore=" + before + " stockAfter=" + after +
                            " goldBefore=" + gold + " goldAfter=" + afterGold + " pendingBefore=" + pendingBefore +
                            " pendingAfter=" + pendingAfter + " pendingWrites=0");
                        if (MarketAutomation.Scenario == "market-backlog-fixed")
                        {
                            MarketPendingDemandFix.EnableAfterGift();
                            Shared.DevDiag.Log(() => "MARKET_STALE_FIX_ENABLED run=" + MarketAutomation.RunId +
                                " nativeTick=" + tick + " scope=native40740-empty-stock-equipment originalTargetRetained=True");
                        }
                        return true;
                    }
                    finally { Volatile.Write(ref InterventionActive, 0); }
                }
            }
            return false;
        }

        private static unsafe int ApplySlice(MissionContext mission, bool[] actors, eGoods good)
        {
            long startedAt = Stopwatch.GetTimestamp();
            if (!SimulationClock.TryGetCurrentTick(mission, out int tick)) throw new InvalidOperationException("Native clock unavailable in backlog slice.");
            if (InterventionSequence != 0 && !SimulationClock.TryGetElapsedTicks(LastNativeTick, tick, out _))
                throw new InvalidOperationException("Native clock reset during backlog experiment.");
            LastNativeTick = tick;
            var players = GamePlayerManagerAPI.Instance;
            for (int player = 1; player < actors.Length; player++)
            {
                if (!actors[player]) continue;
                if (!players.IsAIPlayer(player) || !players.TryGetPlayerResourcesById(player, out GamePlayerResources* r) ||
                    r->r_ExistingPeasants == 0 || r->r_bCachedTradePostExists == 0 || r->r_TradePostId == 0 || r->r_FirstGranaryId == 0)
                    throw new InvalidOperationException("Backlog participant lost its living market state: " + player);
                int gold = players.GetPlayerGold(player);
                if (gold < 0) throw new InvalidOperationException("Negative gold before intervention.");
                if (gold < FundedGold)
                {
                    Volatile.Write(ref InterventionActive, 1);
                    try
                    {
                        if (!players.SetPlayerGold(player, FundedGold) || players.GetPlayerGold(player) != FundedGold)
                            throw new InvalidOperationException("Funded gold assignment failed.");
                        GoldAdded += FundedGold - gold;
                        Record(tick, player, (int)eGoods.STORED_GOLD, gold, FundedGold, gold, FundedGold);
                    }
                    finally { Volatile.Write(ref InterventionActive, 0); }
                }
                if (!MarketBacklogAic.ReadEligibility(player)) throw new InvalidOperationException("Backlog purchase gates are no longer eligible.");
                if (Phase != 0) continue;
                int before = players.GetGoodAmount(player, good);
                if (before < 0) throw new InvalidOperationException("Negative stock before deficit intervention.");
                int amount = Math.Max(0, before - 1);
                if (amount == 0) continue;
                int beforeGold = players.GetPlayerGold(player);
                Volatile.Write(ref InterventionActive, 1);
                try
                {
                    players.RemoveGood(player, good, amount);
                    int after = players.GetGoodAmount(player, good), afterGold = players.GetPlayerGold(player);
                    Record(tick, player, (int)good, before, after, beforeGold, afterGold);
                    if (after != before - amount || afterGold != beforeGold)
                        throw new InvalidOperationException("Native stock withdrawal did not preserve expected stock/gold accounting.");
                    RemovedUnits += amount; RemovalCalls++; RemovedByGood[(int)good] += amount; RemovedByPlayer[player] += amount;
                }
                finally { Volatile.Write(ref InterventionActive, 0); }
            }
            MaxSliceClockTicks = Math.Max(MaxSliceClockTicks, Stopwatch.GetTimestamp() - startedAt);
            return tick;
        }

        private static unsafe void OnTick(int localTick)
        {
            if (!active) return;
            long startedAt = Stopwatch.GetTimestamp();
            try
            {
                if (!SimulationClock.TryGetCurrentTick(activeMission, out int tick))
                    throw new InvalidOperationException("Backlog simulation tick belongs to another mission.");
                var players = GamePlayerManagerAPI.Instance;
                for (int player = 1; player < targetActors.Length; player++)
                {
                    if (!targetActors[player]) continue;
                    if (!players.TryGetPlayerResourcesById(player, out GamePlayerResources* r))
                        throw new InvalidOperationException("Backlog simulation participant disappeared.");
                    int phase = (int)r->r_AISellOrBuyPhase;
                    bool completed = previousBuyPhase[player] == 0 && phase == 1;
                    previousBuyPhase[player] = phase;
                    if (!completed) continue;
                    CompletedBuyPhases++;
                    int experimentPhase = Volatile.Read(ref Phase);
                    if (experimentPhase < 1 || experimentPhase > 4) continue;
                    PurchasePhases[experimentPhase, player]++;
                    uint* pending = &r->r_AIPendingMarketPurchaseAmountNull;
                    for (int good = (int)eGoods.STORED_BOWS; good <= (int)eGoods.STORED_METAL_ARMOUR; good++)
                        if (pending[good] != 0) { PersistentEquipmentPendingBuyPhases[experimentPhase, player]++; break; }
                    if (experimentPhase != 2 && experimentPhase != 3) continue;
                    if (r->r_bCachedTradePostExists == 0 || r->r_TradePostId == 0 || r->r_FirstGranaryId == 0 ||
                        players.GetPlayerGold(player) <= 0 || !MarketBacklogAic.ReadEligibility(player))
                        throw new InvalidOperationException("Stress participant lost funded eligible market/granary state.");
                    // The ordinary scheduler has completed a purchase phase. Only physical
                    // food is removed here; native 2A420 must create subsequent demand itself.
                    foreach (eGoods good in Foods)
                    {
                        int before = players.GetGoodAmount(player, good);
                        if (before <= 0) continue;
                        int beforeGold = players.GetPlayerGold(player);
                        Volatile.Write(ref InterventionActive, 1);
                        try
                        {
                            players.RemoveGood(player, good, 1);
                            int after = players.GetGoodAmount(player, good), afterGold = players.GetPlayerGold(player);
                            Record(tick, player, (int)good, before, after, beforeGold, afterGold);
                            if (after != before - 1 || afterGold != beforeGold)
                                throw new InvalidOperationException("Stress withdrawal failed native stock/gold accounting.");
                            RemovedUnits++; RemovalCalls++; StressRemovedUnits++;
                            RemovedByGood[(int)good]++; RemovedByPlayer[player]++;
                        }
                        finally { Volatile.Write(ref InterventionActive, 0); }
                    }
                }
            }
            catch (Exception error) { Volatile.Write(ref tickFailure, error); active = false; }
            finally
            {
                StressTickCalls++;
                MaxStressTickClockTicks = Math.Max(MaxStressTickClockTicks, Stopwatch.GetTimestamp() - startedAt);
            }
        }

        private static void Record(int tick, int player, int good, int before, int after, int goldBefore, int goldAfter)
        {
            lock (eventGate)
            {
                long sequence = InterventionSequence + 1;
                recent[(int)((sequence - 1) % recent.Length)] = new Intervention { Sequence = sequence, Tick = tick,
                    Phase = Phase, Player = player, Good = good, Before = before, After = after, GoldBefore = goldBefore, GoldAfter = goldAfter };
                Interlocked.Exchange(ref InterventionSequence, sequence);
            }
        }

        private static void LogStatus(string marker)
        {
            var text = new StringBuilder("MARKET_BACKLOG run=").Append(MarketAutomation.RunId).Append(" marker=").Append(marker)
                .Append(" phase=").Append(Phase).Append(" nativeTick=").Append(LastNativeTick)
                .Append(" sequence=").Append(InterventionSequence).Append(" goldAdded=").Append(GoldAdded)
                .Append(" removedUnits=").Append(RemovedUnits).Append(" removalCalls=").Append(RemovalCalls)
                .Append(" completedBuyPhases=").Append(CompletedBuyPhases).Append(" stressRemovedUnits=").Append(StressRemovedUnits)
                .Append(" correctionReductions=").Append(Interlocked.Read(ref MarketPendingDemandFix.Reductions))
                .Append(" correctionCancellations=").Append(Interlocked.Read(ref MarketPendingDemandFix.CancelledOrders))
                .Append(" correctionZeroStockPassed=").Append(Interlocked.Read(ref MarketPendingDemandFix.ZeroStockPassed))
                .Append(" giftGoodBuysAfterDelivery=").Append(Interlocked.Read(ref GiftGoodBuysAfterDelivery))
                .Append(" giftExcessUnitsBought=").Append(Interlocked.Read(ref GiftExcessUnitsBought))
                .Append(" terminalProbeTick=").Append(TerminalProbeTick)
                .Append(" giftFirstBuy=").Append(GiftFirstBuyTick).Append(':').Append(GiftFirstBuyStock).Append('+').Append(GiftFirstBuyAmount)
                .Append(" staleGift=").Append(GiftPlayer).Append('/').Append(GiftGood).Append(':').Append(GiftUnits)
                .Append(" stressTickCalls=").Append(StressTickCalls)
                .Append(" maxStressTickMs=").Append((MaxStressTickClockTicks * 1000.0 / Stopwatch.Frequency).ToString("F3", System.Globalization.CultureInfo.InvariantCulture))
                .Append(" maxSliceMs=").Append((MaxSliceClockTicks * 1000.0 / Stopwatch.Frequency).ToString("F3", System.Globalization.CultureInfo.InvariantCulture))
                .Append(" removedByGood=");
            for (int good = 0; good < RemovedByGood.Length; good++) if (RemovedByGood[good] != 0)
                text.Append(good).Append(':').Append(RemovedByGood[good]).Append(';');
            text.Append(" actualTradesByPhase=");
            for (int phase = 1; phase <= 4; phase++)
                text.Append(phase).Append(":foodSingle=").Append(Interlocked.Read(ref FoodSingleBuysByPhase[phase]))
                    .Append(",foodUnits=").Append(Interlocked.Read(ref FoodBuyUnitsByPhase[phase]))
                    .Append(",equipmentBuys=").Append(Interlocked.Read(ref EquipmentBuysByPhase[phase]))
                    .Append(",equipmentSales=").Append(Interlocked.Read(ref EquipmentSalesByPhase[phase]))
                    .Append(",buySpend=").Append(Interlocked.Read(ref BuySpendByPhase[phase])).Append(';');
            text.Append(" playerPhases=");
            for (int phase = 1; phase <= 4; phase++)
                for (int player = 1; player <= GamePlayerManagerAPI.MAX_PLAYERS; player++)
                    if (targetActors != null && targetActors[player])
                        text.Append(phase).Append('/').Append(player).Append(":buys=").Append(PurchasePhases[phase, player])
                            .Append(",foodSingle=").Append(FoodSingleBuysByPhasePlayer[phase, player])
                            .Append(",equipment=").Append(EquipmentBuysByPhasePlayer[phase, player])
                            .Append(",equipmentPendingPhases=").Append(PersistentEquipmentPendingBuyPhases[phase, player])
                            .Append(",starvedObserved=").Append(phase == 2 && FoodSingleBuysByPhasePlayer[phase, player] >= 10 &&
                                PersistentEquipmentPendingBuyPhases[phase, player] >= 10 && EquipmentBuysByPhasePlayer[phase, player] == 0).Append(';');
            Shared.DevDiag.Log(() => text.ToString());
        }
    }
}
#endif
