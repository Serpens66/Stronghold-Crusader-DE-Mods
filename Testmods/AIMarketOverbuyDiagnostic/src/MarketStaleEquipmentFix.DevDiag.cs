#if SERP_DEVDIAG
using System;
using System.Threading;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;

namespace AIMarketOverbuyDiagnostic
{
    // Provenance first: only an observed native 40740 request from zero stock
    // qualifies. No generic interpretation of construction/material deficits.
    internal static unsafe class MarketStaleEquipmentFix
    {
        private const int Players = GamePlayerManagerAPI.MAX_PLAYERS, Goods = (int)eGoods.Count;
        private const int Capacity = 256;
        private static readonly object gate = new object();
        private static readonly Request[] requests = new Request[(Players + 1) * Goods];
        private static readonly bool[] firstRecorded = new bool[(Players + 1) * Goods];
        private static readonly long[] reductionsByGood = new long[(Players + 1) * Goods];
        private static readonly Evidence[] records = new Evidence[Capacity];
        private static long session, written, read;
        private static int enabled;
        internal static long Reductions, CancelledOrders, OmittedEvidence, ZeroStockPassed, CorrectionFailures;
        private static Exception correctionFailure;

        private struct Request
        {
            internal StaleEquipmentTarget Target;
            internal int Tick;
            internal long Sequence;
        }
        private struct Evidence
        {
            internal long Session, Sequence;
            internal int Player, Good, Tick, Amount, AicId, Kind, Stock, Adjusted, Target, SourceTick;
            internal long SourceSequence;
        }

        internal static bool OutputDrained => Volatile.Read(ref read) == Volatile.Read(ref written);

        internal static void Reset(long nextSession)
        {
            lock (gate)
            {
                session = nextSession;
                Volatile.Write(ref enabled, 0);
                Array.Clear(requests, 0, requests.Length);
                Array.Clear(firstRecorded, 0, firstRecorded.Length);
            }
        }

        internal static void ObserveRecruitment(int player, int good, int beforeStock, int afterStock,
            int beforePending, int afterPending, int aicId, int buyWeapons, int lastStand, int tick, long sequence)
        {
            if (player <= 0 || player > Players || good < (int)eGoods.STORED_BOWS || good > (int)eGoods.STORED_METAL_ARMOUR ||
                !StaleEquipmentTarget.TryCreate(beforeStock, afterStock, beforePending, afterPending,
                    aicId, buyWeapons, lastStand, out StaleEquipmentTarget target)) return;
            lock (gate)
            {
                int index = player * Goods + good;
                requests[index] = new Request { Target = target, Tick = tick, Sequence = sequence };
                if (firstRecorded[index]) return;
                long output = written;
                if (output - Volatile.Read(ref read) == Capacity) { Interlocked.Increment(ref OmittedEvidence); return; }
                records[(int)(output % Capacity)] = new Evidence { Session = session, Player = player, Good = good,
                    Tick = tick, Amount = afterPending, AicId = aicId, Sequence = sequence };
                firstRecorded[index] = true;
                Volatile.Write(ref written, output + 1);
            }
        }

        internal static bool TryGetTrackedRequest(int player, int good, out int amount)
        {
            amount = 0;
            if (player <= 0 || player > Players || good < (int)eGoods.STORED_BOWS || good > (int)eGoods.STORED_METAL_ARMOUR) return false;
            lock (gate)
            {
                Request request = requests[player * Goods + good];
                if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(player, out GamePlayerResources* r) || r == null) return false;
                request.Target.ObservePending(((int*)&r->r_AIPendingMarketPurchaseAmountNull)[good]);
                if (r->r_AILordMinusOne != request.Target.AicId) request.Target.Active = false;
                requests[player * Goods + good] = request;
                if (!request.Target.Active) return false;
                amount = request.Target.ExpectedPending;
                return true;
            }
        }

        internal static bool TryGetProvenance(int player, int good, out int tick, out long sequence, out int target)
        {
            tick = target = 0; sequence = 0;
            if (!TryGetTrackedRequest(player, good, out _)) return false;
            lock (gate)
            {
                Request request = requests[player * Goods + good];
                tick = request.Tick; sequence = request.Sequence; target = request.Target.OriginalTarget;
                return request.Target.Active;
            }
        }

        internal static void ObservePendingState(int player, int good, int value)
        {
            if (player <= 0 || player > Players || good < (int)eGoods.STORED_BOWS || good > (int)eGoods.STORED_METAL_ARMOUR) return;
            lock (gate) requests[player * Goods + good].Target.ObservePending(value);
        }

        internal static void EnableAfterGift()
        {
            if (MarketAutomation.Scenario != "market-backlog-fixed" || !MarketBacklogScenario.Active || MarketBacklogScenario.GiftUnits <= 0)
                throw new InvalidOperationException("Experimental equipment correction requires its owned fulfilled-demand test.");
            Volatile.Write(ref enabled, 1);
        }

        internal static void DeactivateExperiment() => Volatile.Write(ref enabled, 0);

        internal static long GetReductionCount(int player, int good) => player > 0 && player <= Players && good >= 0 && good < Goods
            ? Interlocked.Read(ref reductionsByGood[player * Goods + good]) : 0;

        internal static void BeforeDecision(int player, int aicId, int buyWeapons, int lastStand, int tick)
        {
            if (Volatile.Read(ref enabled) == 0) return;
            if (player <= 0 || player > Players || !GamePlayerManagerAPI.Instance.IsAIPlayer(player) ||
                !GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(player, out GamePlayerResources* r) || r == null) return;
            lock (gate)
            {
                int* pending = (int*)&r->r_AIPendingMarketPurchaseAmountNull;
                int* stock = (int*)&r->r_TotalGoodsNull;
                for (int good = (int)eGoods.STORED_BOWS; good <= (int)eGoods.STORED_METAL_ARMOUR; good++)
                {
                    int index = player * Goods + good, original = pending[good];
                    Request request = requests[index];
                    bool wasActive = request.Target.Active;
                    if (!request.Target.TryReduce(original, stock[good], aicId, buyWeapons, lastStand, out int adjusted))
                    {
                        requests[index] = request;
                        if (wasActive && request.Target.Active && stock[good] == 0) Interlocked.Increment(ref ZeroStockPassed);
                        continue;
                    }
                    pending[good] = adjusted;
                    if (pending[good] != adjusted) throw new InvalidOperationException("Equipment correction readback failed.");
                    requests[index] = request;
                    Interlocked.Increment(ref Reductions);
                    Interlocked.Increment(ref reductionsByGood[index]);
                    if (adjusted == 0) Interlocked.Increment(ref CancelledOrders);
                    long output = written;
                    if (output - Volatile.Read(ref read) == Capacity) { Interlocked.Increment(ref OmittedEvidence); continue; }
                    records[(int)(output % Capacity)] = new Evidence { Kind = 1, Session = session, Player = player,
                        Good = good, Tick = tick, Amount = original, Adjusted = adjusted, Stock = stock[good],
                        Target = request.Target.OriginalTarget, AicId = aicId, SourceTick = request.Tick, SourceSequence = request.Sequence };
                    Volatile.Write(ref written, output + 1);
                }
            }
        }

        internal static void FailCorrection(Exception failure)
        {
            DeactivateExperiment();
            Interlocked.Increment(ref CorrectionFailures);
            Volatile.Write(ref correctionFailure, failure);
        }

        internal static void Drain(ManualLogSource log)
        {
            long cursor = read;
            while (cursor < Volatile.Read(ref written))
            {
                Evidence item = records[(int)(cursor % Capacity)];
                if (item.Kind == 1)
                    Shared.DebugLogHelper.LogInfo(log, "MARKET_STALE_FIX_REDUCED: session=" + item.Session + " player=" + item.Player +
                        " good=" + item.Good + " nativeTick=" + item.Tick + " sourceRva=40740 sourceTick=" + item.SourceTick +
                        " sourceSequence=" + item.SourceSequence + " initialStock=0 originalTarget=" + item.Target +
                        " currentStock=" + item.Stock + " oldPending=" + item.Amount + " newPending=" + item.Adjusted +
                        " aicNativeIndex=" + item.AicId + " originalDecisionCalledOnce=True");
                else Shared.DebugLogHelper.LogInfo(log, "MARKET_STALE_NATIVE_REQUEST: session=" + item.Session + " player=" + item.Player +
                    " good=" + item.Good + " nativeTick=" + item.Tick + " sequence=" + item.Sequence +
                    " sourceRva=40740 initialStock=0 amount=" + item.Amount + " aicNativeIndex=" + item.AicId + " pendingWrites=0");
                Volatile.Write(ref read, ++cursor);
            }
            Exception failure = Interlocked.Exchange(ref correctionFailure, null);
            if (failure != null) Shared.DebugLogHelper.LogError(log, "MARKET_STALE_FIX_ERROR: correction disabled; " + failure);
        }
    }
}
#endif
