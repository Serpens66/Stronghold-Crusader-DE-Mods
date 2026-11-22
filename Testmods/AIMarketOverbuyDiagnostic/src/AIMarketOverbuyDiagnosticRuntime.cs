using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using APIShared.Simulation;

namespace AIMarketOverbuyDiagnostic
{
    internal sealed unsafe class AIMarketOverbuyDiagnosticRuntime
    {
        private readonly ManualLogSource log;
        private readonly bool fixesLoaded;
        private readonly MarketObservation capture;
        private readonly Thread writer;
        private readonly Action<string> sink;
        // Lifecycle notifications can arrive on Unity while a numeric simulation sample
        // is in flight. Only producers share this gate; the disk writer never takes it.
        private readonly object captureGate = new object();
        private bool active;
        private long sessionId;
        private long nextWriterError;
        private bool hasMapTick;
        private int lastMapTick;
        internal static long ObservedTicks;
        internal static int LastObservedMapTick;
        internal static long CaptureFailures;
        internal static long WriterPasses, WriterFailures, IncompleteSessions;
        internal static long ObservedBuySellCandidates;
#if SERP_DEVDIAG
        private MarketTradeTrace tradeTrace;
        private bool causalRequested;
        internal void InstallTrace(SHCDESE.API.LowLevel.CrusaderLibraryLoadContext context)
        {
            causalRequested = MarketAutomation.NeedsCausalTrace();
            MarketPendingDemandFix.Install(context, log);
            MarketPendingDemandFix.InitializeConsumers(log);
            // The decision wrapper also drives correction in ordinary test games.
            // Detailed trade capture remains confined to explicitly owned short runs.
            tradeTrace = MarketTradeTrace.Install(context, log);
            Shared.DevDiag.Log(() => "AI_MARKET_CAUSAL_SCOPE: detailedCapture=" + causalRequested +
                "; pendingCorrectionReady=" + (MarketPendingDemandFix.Ready && tradeTrace.Ready));
        }
        internal void CheckTraceHealthForAutomation(bool requireCalls)
        {
            if (!MarketPendingDemandFix.FeatureReady || MarketPendingDemandFix.CorrectionFailures != 0)
                throw new InvalidOperationException("Native demand correction is unavailable or its runtime capture/correction failed.");
            if (!causalRequested) return;
            if (tradeTrace == null || !tradeTrace.Ready || tradeTrace.CaptureFailures != 0 || tradeTrace.InvalidContexts != 0)
                throw new InvalidOperationException("Requested native trade trace is unavailable or its capture failed.");
            if (requireCalls && tradeTrace.ObservedNativeCalls == 0)
                throw new InvalidOperationException("The native trade trace received no real native callbacks.");
        }
        internal bool TraceOutputDrainedForAutomation => MarketPendingDemandFix.OutputDrained &&
            (!causalRequested || (tradeTrace != null && tradeTrace.OutputDrained));
#endif

        internal AIMarketOverbuyDiagnosticRuntime(ManualLogSource log, bool fixesLoaded)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.fixesLoaded = fixesLoaded;
            ValidateInteropLayout();
            capture = new MarketObservation((int)eGoods.Count, (int)eGoods.STORED_WOOD_LOGS, (int)eGoods.STORED_GOLD, Stopwatch.Frequency);
            sink = WriteLine;
            writer = new Thread(WriteLoop) { IsBackground = true, Name = "AI market diagnostic writer", Priority = ThreadPriority.BelowNormal };
        }

        // Start only after all candidate validation and registrations. Once started,
        // this process-lifetime worker is never joined or disposed by Unity cleanup.
        internal void StartWriter() => writer.Start();

        internal void OnSessionStarted(Shared.GameplaySessionStartedContext session)
        {
            bool started;
            lock (captureGate)
            {
                sessionId = session.SessionId;
                hasMapTick = false;
                active = capture.Start(session.SessionId, session.IsEditor, session.IsReplay);
                Interlocked.Exchange(ref ObservedBuySellCandidates, 0);
                started = active;
#if SERP_DEVDIAG
                if (started && causalRequested) tradeTrace?.Start(sessionId);
                else tradeTrace?.End();
#endif
            }
#if SERP_DEVDIAG
            Shared.NativePatchGuard.Recheck();
#endif
            Shared.DebugLogHelper.LogInfo(log, $"AI_MARKET_SESSION: session={session.SessionId}, kind={session.Kind}, loadedSave={session.IsLoadedSave}, editor={session.IsEditor}, replayNotification={session.IsReplay}, fixesLoaded={fixesLoaded}, observationActive={started}, mode={session.Mode.ToDiagnosticString()}.");
        }

        internal void OnSessionEnded()
        {
            lock (captureGate)
            {
                active = false;
                capture.End();
#if SERP_DEVDIAG
                tradeTrace?.End();
#endif
            }
        }

        internal void OnTick(int localRun)
        {
            long started = Stopwatch.GetTimestamp();
            Exception failure = null;
            long failedSession = 0;
            lock (captureGate)
            {
                if (!active) return;
                try { CaptureTick(localRun, started); }
                catch (Exception ex)
                {
                    active = false;
                    Interlocked.Increment(ref CaptureFailures);
                    capture.FailTick();
                    failure = ex;
                    failedSession = sessionId;
                }
            }
            if (failure != null)
                Shared.DebugLogHelper.LogError(log, $"AI_MARKET_OBSERVATION_FAILED: session={failedSession}, localRun={localRun}; observation stopped: {failure}");
        }

        private void CaptureTick(int localRun, long started)
        {
            int tick = localRun; // APIShared publishes the native map tick, never Director's local run count.
            if (hasMapTick && tick == lastMapTick) return;
            hasMapTick = true;
            lastMapTick = tick;
            LastObservedMapTick = tick;
            Interlocked.Increment(ref ObservedTicks);
            if (!capture.BeginTick(tick, DateTime.UtcNow.Ticks, started, localRun)) { active = false; return; }
#if SERP_DEVDIAG
            tradeTrace?.OnTick(tick);
#endif
            var players = GamePlayerManagerAPI.Instance;
            for (int playerId = 1; playerId <= MarketObservation.Players; playerId++)
            {
                if (!players.IsAIPlayer(playerId) ||
                    !players.TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources)) continue;
                uint* stock = (uint*)&resources->r_TotalGoodsNull;
                uint* pending = (uint*)&resources->r_AIPendingMarketPurchaseAmountNull;
                int gold = resources->r_TotalGoodsGold;
                int phase = (int)resources->r_AISellOrBuyPhase;
                capture.Player(playerId, (int)players.GetAILord(playerId), gold, phase);
                for (int goodIndex = (int)eGoods.STORED_WOOD_LOGS; goodIndex < (int)eGoods.Count; goodIndex++)
                {
                    if (goodIndex == (int)eGoods.STORED_GOLD) continue;
                    capture.Good(playerId, goodIndex, pending[goodIndex], stock[goodIndex], gold, phase);
                }
            }
            long finished = Stopwatch.GetTimestamp();
            Interlocked.Exchange(ref ObservedBuySellCandidates, capture.BuySellCandidates);
            capture.EndTick(finished - started, finished);
        }

        private void WriteLine(string text) => Shared.DebugLogHelper.LogInfo(log, text);

        private void WriteLoop()
        {
            bool legendWritten = false;
            long lastSkipped = 0;
            while (true)
            {
                try
                {
                    if (!legendWritten)
                    {
                        var legend = new StringBuilder("AI_MARKET_LEGEND: interval=10 real seconds; each tick sampled; sourceUnconfirmed=true; player changes=gold,phase; goods=id:pendingRange/stockRange/pendingChanges,stockChanges/kind=count@firstTickOffset:lastTickOffset; stockChanges '=' repeats pendingChanges; candidate counts/offsets are base36 (0-9,a-z), '^kind' repeats that earlier kind's count/first/last; other values decimal; range=first[>last][~min:max], omitted last=first and omitted extrema=endpoint extrema; kinds=0 increase,1 persistent,2 buy,3 buy-sell; evidence=tick@UTC(pending,stock,gold,phase), buy-sell=buyBefore>buyAfter|sellBefore>sellAfter; examples=first per player/good/kind/session; fixesLoaded=");
                        legend.Append(fixesLoaded).Append("; goods=");
                        for (int goodIndex = (int)eGoods.STORED_WOOD_LOGS; goodIndex < (int)eGoods.Count; goodIndex++)
                            if (goodIndex != (int)eGoods.STORED_GOLD) legend.Append(goodIndex).Append('=').Append((eGoods)goodIndex).Append(';');
                        WriteLine(legend.ToString());
                        legendWritten = true;
                    }
                    while (capture.DrainOne(sink)) { }
#if SERP_DEVDIAG
                    tradeTrace?.Drain(sink);
                    MarketPendingDemandFix.Drain(log);
#endif
                    long skipped = Interlocked.Read(ref capture.SkippedSessions);
                    if (skipped != lastSkipped)
                    {
                        Interlocked.Exchange(ref IncompleteSessions, skipped);
                        Shared.DebugLogHelper.LogError(log, $"AI_MARKET_CAPTURE_INCOMPLETE: writer backlog exhausted at session/tick reset boundary; affectedSessions={skipped}. No retained summaries were overwritten.");
                        lastSkipped = skipped;
                    }
                    Interlocked.Increment(ref WriterPasses);
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref capture.WriterFailures);
                    Interlocked.Increment(ref WriterFailures);
                    long now = Stopwatch.GetTimestamp();
                    if (now >= nextWriterError)
                    {
                        nextWriterError = now + Stopwatch.Frequency * 30;
                        try { Shared.DebugLogHelper.LogError(log, "AI_MARKET_WRITER_FAILED: summaries retained for retry; " + ex); } catch { }
                    }
                    Thread.Sleep(1000);
                }
                Thread.Sleep(250);
            }
        }

        private static void ValidateInteropLayout()
        {
            if ((int)eGoods.STORED_NULL != 0 || (int)eGoods.STORED_WOOD_LOGS != 1 ||
                (int)eGoods.STORED_GOLD != 15 || (int)eGoods.STORED_STONE_BLOCKS != 4 || GamePlayerManagerAPI.MAX_PLAYERS != 8 ||
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

    }
}
