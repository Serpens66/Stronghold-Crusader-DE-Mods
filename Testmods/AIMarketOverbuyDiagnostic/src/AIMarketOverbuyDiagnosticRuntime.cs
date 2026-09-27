using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace AIMarketOverbuyDiagnostic
{
    internal sealed unsafe class AIMarketOverbuyDiagnosticRuntime
    {
        private const int MaxPlayers = 8; // Verified GamePlayerManagerAPI player-ID range.
        private const int PendingPhaseWarningThreshold = 4;
        private const int RecentBuyWindowTicks = 500;
        private const int MaxDetailLinesPerTick = 24;
        private static readonly TimeSpan SummaryInterval = TimeSpan.FromSeconds(10);

        private readonly ManualLogSource log;
        private readonly bool fixesLoaded;
        private readonly Observation[,] observations =
            new Observation[MaxPlayers + 1, (int)eGoods.Count];
        private bool active;
        private bool firstTickLogged;
        private int lastTick = -1;
        private long sessionId;
        private DateTime nextSummaryUtc;
        private int pendingChanges;
        private int persistentRequests;
        private int increasedRequests;
        private int likelyBuys;
        private int buySellCandidates;

        internal AIMarketOverbuyDiagnosticRuntime(ManualLogSource log, bool fixesLoaded)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.fixesLoaded = fixesLoaded;
            ValidateInteropLayout();
        }

        internal void OnSessionStarted(Shared.GameplaySessionStartedContext session)
        {
            ResetObservations();
            sessionId = session.SessionId;
            active = !session.IsEditor && !session.IsReplay;
            firstTickLogged = false;
            lastTick = -1;
            nextSummaryUtc = DateTime.UtcNow + SummaryInterval;
            LogInfo($"AI_MARKET_SESSION: session={sessionId}, kind={session.Kind}, " +
                $"loadedSave={session.IsLoadedSave}, editor={session.IsEditor}, replay={session.IsReplay}, " +
                $"fixesLoaded={fixesLoaded}, active={active}.");
        }

        internal void OnSessionEnded()
        {
            if (active)
                LogCounters("AI_MARKET_SESSION_END", lastTick);
            active = false;
            ResetObservations();
        }

        internal void OnTick(int tick)
        {
            if (!active) return;
            try
            {
                if (!firstTickLogged)
                {
                    firstTickLogged = true;
                    LogInfo($"AI_MARKET_POST_STARTUP_TICK: session={sessionId}, tick={tick}, " +
                        "publisher=GameTimeManagerAPI.OnTick.");
                }
                if (lastTick >= 0 && tick < lastTick)
                {
                    ResetObservations();
                    LogWarning($"AI_MARKET_TICK_RESET: session={sessionId}, previous={lastTick}, current={tick}; observations reset.");
                }
                lastTick = tick;

                int detailLines = 0;
                int suppressedLines = 0;
                var players = GamePlayerManagerAPI.Instance;
                for (int playerId = 1; playerId <= MaxPlayers; playerId++)
                {
                    if (!players.IsAIPlayer(playerId) ||
                        !players.TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
                        continue;

                    uint* stock = (uint*)&resources->r_TotalGoodsNull;
                    uint* pending = (uint*)&resources->r_AIPendingMarketPurchaseAmountNull;
                    int gold = resources->r_TotalGoodsGold;
                    AISellBuyPhase phase = resources->r_AISellOrBuyPhase;
                    string lord = players.GetAILord(playerId).ToString();

                    for (int goodIndex = (int)eGoods.STORED_WOOD_LOGS;
                         goodIndex < (int)eGoods.Count; goodIndex++)
                    {
                        if (goodIndex == (int)eGoods.STORED_GOLD) continue;
                        string detail = ObserveGood(playerId, (eGoods)goodIndex, lord,
                            pending[goodIndex], stock[goodIndex], gold, phase, tick);
                        if (detail == null) continue;
                        if (detailLines < MaxDetailLinesPerTick)
                        {
                            LogInfo(detail);
                            detailLines++;
                        }
                        else suppressedLines++;
                    }
                }
                if (suppressedLines != 0)
                    LogWarning($"AI_MARKET_DETAIL_LIMIT: session={sessionId}, tick={tick}, suppressed={suppressedLines}.");

                DateTime now = DateTime.UtcNow;
                if (now >= nextSummaryUtc)
                {
                    LogSummary(players, tick);
                    nextSummaryUtc = now + SummaryInterval;
                }
            }
            catch (Exception ex)
            {
                active = false;
                Shared.DebugLogHelper.LogError(log,
                    $"AI_MARKET_OBSERVATION_FAILED: session={sessionId}, tick={tick}; observation stopped for this session: {ex}");
            }
        }

        private string ObserveGood(int playerId, eGoods good, string lord,
            uint pending, uint stock, int gold, AISellBuyPhase phase, int tick)
        {
            int goodIndex = (int)good;
            Observation previous = observations[playerId, goodIndex];
            if (previous == null)
            {
                observations[playerId, goodIndex] = new Observation
                {
                    Pending = pending, Stock = stock, Gold = gold, Phase = phase,
                    PendingSinceTick = pending > 0 ? tick : -1,
                    LastLikelyBuyTick = -1
                };
                if (pending == 0) return null;
                return Format("AI_MARKET_INITIAL_PENDING", playerId, lord, good,
                    tick, phase, pending, pending, stock, stock, gold, gold,
                    "request already present at first sample");
            }

            long stockDelta = (long)stock - previous.Stock;
            long goldDelta = (long)gold - previous.Gold;
            bool pendingChanged = pending != previous.Pending;
            bool equipment = good >= eGoods.STORED_BOWS && good <= eGoods.STORED_METAL_ARMOUR;
            string marker = null;
            string note = null;

            if (pendingChanged)
            {
                pendingChanges++;
                if (pending > previous.Pending && previous.Pending > 0 && stockDelta <= 0)
                {
                    increasedRequests++;
                    marker = "AI_MARKET_PENDING_INCREASE_CANDIDATE";
                    note = "outstanding amount rose without a stock gain; request cause unconfirmed";
                }
                else
                {
                    marker = "AI_MARKET_PENDING_CHANGE";
                    note = pending == 0 ? "request cleared" : "request created or changed";
                }
            }

            if (pending > 0)
            {
                if (previous.Pending == 0)
                {
                    previous.PendingSinceTick = tick;
                    previous.PhaseTransitions = 0;
                    previous.PersistentLogged = false;
                }
                if (phase != previous.Phase)
                {
                    previous.PhaseTransitions++;
                    if (previous.PhaseTransitions >= PendingPhaseWarningThreshold &&
                        !previous.PersistentLogged)
                    {
                        persistentRequests++;
                        previous.PersistentLogged = true;
                        marker = "AI_MARKET_PENDING_PERSISTENT_CANDIDATE";
                        note = $"outstanding across {previous.PhaseTransitions} phase transitions, sinceTick={previous.PendingSinceTick}";
                    }
                }
            }
            else
            {
                previous.PendingSinceTick = -1;
                previous.PhaseTransitions = 0;
                previous.PersistentLogged = false;
            }

            // Stock and gold deltas only suggest trades; production, consumption, and taxes can coincide.
            if (stockDelta > 0 && goldDelta < 0 &&
                (previous.Pending > 0 || pending > 0))
            {
                likelyBuys++;
                previous.LastLikelyBuyTick = tick;
                marker = "AI_MARKET_BUY_CANDIDATE";
                note = "stock rose while gold fell with a tracked request; transaction source unconfirmed";
            }
            else if (stockDelta < 0 && goldDelta > 0 &&
                     previous.LastLikelyBuyTick >= 0 &&
                     (long)tick - previous.LastLikelyBuyTick <= RecentBuyWindowTicks)
            {
                buySellCandidates++;
                marker = "AI_MARKET_BUY_SELL_CANDIDATE";
                note = $"stock fell while gold rose after buy candidate at tick={previous.LastLikelyBuyTick}; source unconfirmed";
                previous.LastLikelyBuyTick = -1;
            }

            if (marker == null && stockDelta != 0 &&
                (pending > 0 || previous.Pending > 0 ||
                 (equipment && previous.LastLikelyBuyTick >= 0 &&
                  (long)tick - previous.LastLikelyBuyTick <= RecentBuyWindowTicks)))
            {
                marker = "AI_MARKET_RELEVANT_STOCK_CHANGE";
                note = "stock changed while request or recent equipment buy is tracked";
            }

            string result = marker == null ? null : Format(marker, playerId, lord, good,
                tick, phase, previous.Pending, pending, previous.Stock, stock,
                previous.Gold, gold, note);
            previous.Pending = pending;
            previous.Stock = stock;
            previous.Gold = gold;
            previous.Phase = phase;
            return result;
        }

        private string Format(string marker, int playerId, string lord, eGoods good,
            int tick, AISellBuyPhase phase, uint oldPending, uint pending,
            uint oldStock, uint stock, int oldGold, int gold, string note)
        {
            return $"{marker}: session={sessionId}, tick={tick}, player={playerId}, lord={lord}, " +
                   $"good={good}, equipment={good >= eGoods.STORED_BOWS && good <= eGoods.STORED_METAL_ARMOUR}, " +
                   $"phase={phase}({(int)phase}), pending={oldPending}->{pending}, " +
                   $"stock={oldStock}->{stock}, gold={oldGold}->{gold}, fixesLoaded={fixesLoaded}, note={note}.";
        }

        private void LogSummary(GamePlayerManagerAPI players, int tick)
        {
            for (int playerId = 1; playerId <= MaxPlayers; playerId++)
            {
                if (!players.IsAIPlayer(playerId) ||
                    !players.TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
                    continue;
                uint* pending = (uint*)&resources->r_AIPendingMarketPurchaseAmountNull;
                var goods = new StringBuilder();
                int count = 0;
                long amount = 0;
                for (int goodIndex = (int)eGoods.STORED_WOOD_LOGS;
                     goodIndex < (int)eGoods.Count; goodIndex++)
                {
                    if (goodIndex == (int)eGoods.STORED_GOLD || pending[goodIndex] == 0) continue;
                    if (goods.Length != 0) goods.Append(',');
                    goods.Append((eGoods)goodIndex).Append(':').Append(pending[goodIndex]);
                    count++;
                    amount += pending[goodIndex];
                }
                LogInfo($"AI_MARKET_SUMMARY: session={sessionId}, tick={tick}, player={playerId}, " +
                    $"lord={players.GetAILord(playerId)}, phase={resources->r_AISellOrBuyPhase}({(int)resources->r_AISellOrBuyPhase}), " +
                    $"gold={resources->r_TotalGoodsGold}, pendingGoods={count}, pendingAmount={amount}, " +
                    $"pending=[{goods}], fixesLoaded={fixesLoaded}.");
            }
            LogCounters("AI_MARKET_COUNTERS", tick);
        }

        private void LogCounters(string marker, int tick)
        {
            LogInfo($"{marker}: session={sessionId}, tick={tick}, pendingChanges={pendingChanges}, " +
                $"persistentRequests={persistentRequests}, increasedRequests={increasedRequests}, " +
                $"buyCandidates={likelyBuys}, buySellCandidates={buySellCandidates}.");
        }

        private void ResetObservations()
        {
            Array.Clear(observations, 0, observations.Length);
            pendingChanges = persistentRequests = increasedRequests = likelyBuys = buySellCandidates = 0;
        }

        private static void ValidateInteropLayout()
        {
            if ((int)eGoods.STORED_NULL != 0 || (int)eGoods.STORED_WOOD_LOGS != 1 ||
                (int)eGoods.Count != 25 ||
                (int)eGoods.STORED_METAL_ARMOUR != (int)eGoods.Count - 1 ||
                Offset(nameof(GamePlayerResources.r_TotalGoodsNull)) != 0x04D0 ||
                Offset(nameof(GamePlayerResources.r_TotalGoodsGold)) != 0x050C ||
                Offset(nameof(GamePlayerResources.r_TotalGoodsMetalArmour)) != 0x0530 ||
                Offset(nameof(GamePlayerResources.r_AISellOrBuyPhase)) != 0x2A6C ||
                Offset(nameof(GamePlayerResources.r_AIPendingMarketPurchaseAmountNull)) != 0x2A70 ||
                Offset(nameof(GamePlayerResources.r_AIPendingMarketPurchaseAmountMetalArmour)) != 0x2AD0)
                throw new InvalidOperationException("Installed Script Extender AI market resource layout differs from audited source.");
        }

        private static int Offset(string member) =>
            Marshal.OffsetOf(typeof(GamePlayerResources), member).ToInt32();

        private void LogInfo(string message) => Shared.DebugLogHelper.LogInfo(log, message);
        private void LogWarning(string message) => Shared.DebugLogHelper.LogWarning(log, message);

        private sealed class Observation
        {
            internal uint Pending;
            internal uint Stock;
            internal int Gold;
            internal AISellBuyPhase Phase;
            internal int PendingSinceTick;
            internal int PhaseTransitions;
            internal bool PersistentLogged;
            internal int LastLikelyBuyTick;
        }
    }
}
