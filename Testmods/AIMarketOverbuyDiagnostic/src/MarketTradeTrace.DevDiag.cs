#if SERP_DEVDIAG
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using APIShared.NativeSites;
using APIShared.Simulation;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using Shared;

namespace AIMarketOverbuyDiagnostic
{
    // Diagnostic only: never changes arguments, return values, resources or instructions
    // after publication. The static owner, hooks and delegates survive Unity startup cleanup.
    internal sealed unsafe partial class MarketTradeTrace
    {
        internal const string Feature = "ai-market-trade-trace";
        private const string Owner = "AIMarketOverbuyDiagnostic_Serp";
        private const int ProfileRootRva = 0x404C950;
        private const int FrameCapacity = 16;
        private const int PlayerCount = GamePlayerManagerAPI.MAX_PLAYERS;
        private const int GoodCount = (int)eGoods.Count;
        private static MarketTradeTrace published;

        // Full displaced entries, not AOBs/call sites. Offline tests use these exact bytes.
        internal static readonly int[] EntryRvas = { 0x3EC90, 0x40740, 0x29650, 0x29700 };
        internal static readonly byte[][] EntryBytes =
        {
            new byte[] { 0x40, 0x56, 0x41, 0x54, 0x41, 0x55 },
            new byte[] { 0x40, 0x55, 0x56, 0x41, 0x56, 0x48, 0x81, 0xEC, 0x60, 0x01, 0x00, 0x00 },
            new byte[] { 0x48, 0x89, 0x5C, 0x24, 0x08, 0x48, 0x89, 0x6C, 0x24, 0x10 },
            new byte[] { 0x45, 0x85, 0xC9, 0x0F, 0x88, 0x96, 0x00, 0x00, 0x00 }
        };

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void PairCall(IntPtr profileRoot, int player);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate int BuyCall(IntPtr profileRoot, int player, int good, int signedAmount);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        internal delegate void SellCall(IntPtr profileRoot, int player, int good, int signedAmount);

        private PairCall originalDecision, originalRecruit;
        private BuyCall originalBuy;
        private SellCall originalSell;
        private Func<bool> integrity;
        private readonly Frame[] frames = new Frame[FrameCapacity];
        private readonly long module;
        private readonly int moduleLength;
        private int ready, active, producerThread;
        private readonly object captureGate = new object();
        private TradeSessionState lifecycle;
        private long session, sequence;
        private int lastMapTick;
        private long captureFailures, invalidContexts, missingMapTicks, depthOverflows, staleFrames;
        private long nativeCalls, originalExceptions, captureClockTicks;

        internal bool Ready => Volatile.Read(ref ready) != 0;
        internal long CaptureFailures => Interlocked.Read(ref captureFailures);
        internal long ObservedNativeCalls => Interlocked.Read(ref nativeCalls);
        internal long InvalidContexts => Interlocked.Read(ref invalidContexts);
        internal long ActualBuys => Interlocked.Read(ref actualBuys);
        internal long ActualSales => Interlocked.Read(ref actualSales);
        internal long ObservedCycles => Interlocked.Read(ref observedCycles);
        internal bool OutputDrained
        {
            get
            {
                lock (captureGate)
                    return lifecycle.Depth == 0 && Volatile.Read(ref active) == 0 && currentSummary == null &&
                        Volatile.Read(ref detailRead) == Volatile.Read(ref detailWritten) &&
                        Volatile.Read(ref summaryRead) == Volatile.Read(ref summaryWritten);
            }
        }

        private MarketTradeTrace(CrusaderLibraryLoadContext context)
        {
            module = context.ModuleHandle.ToInt64();
            moduleLength = context.Memory.Length;
            for (int i = 0; i < frames.Length; i++) frames[i] = new Frame();
            InitializeBuffers();
        }

        internal static MarketTradeTrace Install(CrusaderLibraryLoadContext context, ManualLogSource log)
        {
            if (published != null) return published;
            var owner = new MarketTradeTrace(context);
            NativeDetour<PairCall> decision = null, recruit = null;
            NativeDetour<BuyCall> buy = null;
            NativeDetour<SellCall> sell = null;
            bool claimed = false;
            try
            {
                if (!DebugLogHelper.IsCurrentNativeLibraryVersion())
                    throw new InvalidOperationException("Trade trace requires the audited native image; no layout migration fallback.");
                if (Chainloader.PluginInfos.ContainsKey("AIBuildDiagnoseTest_Serp"))
                    throw new InvalidOperationException("AIBuildDiagnoseTest already owns the trade diagnostic entries; do not install both diagnostic owners.");
                ValidateLayout();
                NativePatchGuard.Bind(context, log);
                var spans = new NativeSiteSpan[EntryRvas.Length];
                for (int i = 0; i < spans.Length; i++)
                {
                    spans[i] = new NativeSiteSpan(EntryRvas[i], EntryBytes[i].Length, NativeSiteMode.Exclusive);
                    NativePatchGuard.RequireUnpatched(unchecked((ulong)moduleAddress(context)) + (uint)EntryRvas[i],
                        EntryBytes[i].Length, Feature, Feature);
                }
                var registration = NativeSiteRegistry.Register(Owner, Feature, spans);
                if (!registration.Succeeded) throw new InvalidOperationException(registration.Reason);
                claimed = true;
                PairCall decisionCallback = owner.Decision, recruitCallback = owner.Recruit;
                BuyCall buyCallback = owner.Buy;
                SellCall sellCallback = owner.Sell;
                ulong image = unchecked((ulong)moduleAddress(context));
                // Prepare all entries before publishing any. AuditedEntryDetour checks the real
                // installed backend's Indirect span and trampoline; no copied startup probe.
                decision = AuditedEntryDetour.Prepare(context.Memory, image, EntryRvas[0], EntryBytes[0], decisionCallback, Feature + ".decision");
                recruit = AuditedEntryDetour.Prepare(context.Memory, image, EntryRvas[1], EntryBytes[1], recruitCallback, Feature + ".recruit");
                buy = AuditedEntryDetour.Prepare(context.Memory, image, EntryRvas[2], EntryBytes[2], buyCallback, Feature + ".buy");
                sell = AuditedEntryDetour.Prepare(context.Memory, image, EntryRvas[3], EntryBytes[3], sellCallback, Feature + ".sell");
                owner.originalDecision = decision.Original;
                owner.originalRecruit = recruit.Original;
                owner.originalBuy = buy.Original;
                owner.originalSell = sell.Original;
                published = owner; // Every callback already has a valid original, even on partial Enable failure.
                AuditedEntryDetour.Publish(decision, decisionCallback, Feature + ".decision");
                AuditedEntryDetour.Publish(recruit, recruitCallback, Feature + ".recruit");
                AuditedEntryDetour.Publish(buy, buyCallback, Feature + ".buy");
                AuditedEntryDetour.Publish(sell, sellCallback, Feature + ".sell");
                owner.integrity = NativePatchGuard.CaptureIntegrity(new IHook[] { decision, recruit, buy, sell }, Feature);
                Volatile.Write(ref owner.ready, 1);
                DevDiag.Log(() => "AI_MARKET_TRADE_READY: method=reference-rva; entries=3EC90/6,40740/12,29650/10,29700/9; Indirect; nativeOriginalExactlyOnce=true; controller3F270NotHooked=true.");
            }
            catch (Exception error)
            {
                bool anyPublished = AuditedEntryDetour.WasPublished(decision) || AuditedEntryDetour.WasPublished(recruit) ||
                    AuditedEntryDetour.WasPublished(buy) || AuditedEntryDetour.WasPublished(sell);
                if (!AuditedEntryDetour.WasPublished(decision)) decision?.Dispose();
                if (!AuditedEntryDetour.WasPublished(recruit)) recruit?.Dispose();
                if (!AuditedEntryDetour.WasPublished(buy)) buy?.Dispose();
                if (!AuditedEntryDetour.WasPublished(sell)) sell?.Dispose();
                if (claimed && !anyPublished) NativeSiteRegistry.Unregister(Owner, Feature);
                // Already published callbacks remain original-only, never undone or freed.
                DebugLogHelper.LogError(log, "AI_MARKET_TRADE_UNAVAILABLE: " + error);
            }
            return owner;
        }

        private static long moduleAddress(CrusaderLibraryLoadContext context) => context.ModuleHandle.ToInt64();

        internal void Start(long sessionId)
        {
            NativePatchGuard.Recheck();
            bool enabled = Ready && integrity != null && integrity();
            lock (captureGate)
            {
                lifecycle.RequestStart(sessionId, enabled);
                ApplyPendingLifecycle();
            }
        }

        internal void End()
        {
            lock (captureGate)
            {
                lifecycle.RequestEnd();
                ApplyPendingLifecycle();
            }
        }

        // The gate protects capture and session publication only. In particular it is
        // never held while executing a native original (which can invoke nested entries).
        private void ApplyPendingLifecycle()
        {
            if (!lifecycle.TakePending(out long nextSession, out bool enabled)) return;
            Volatile.Write(ref active, 0);
            PublishSummary(true);
            if (!enabled || !BeginSession(nextSession)) return;
            session = nextSession;
            producerThread = 0;
            Volatile.Write(ref active, 1);
        }

        internal void OnTick(int mapTick)
        {
            lock (captureGate)
            {
                if (Volatile.Read(ref active) == 0) return;
                bool clockReset = lastMapTick != 0 && !SimulationClock.TryGetElapsedTicks(lastMapTick, mapTick, out _);
                lastMapTick = mapTick;
                // A callback chain owns its before/after interval until its outermost
                // original returns; tick/lifecycle notifications cannot split that chain.
                if (lifecycle.Depth != 0) return;
                if (clockReset) MarketStaleEquipmentFix.Reset(session);
                long now = Stopwatch.GetTimestamp();
                if (now >= nextSummaryClock && PublishSummary(false))
                {
                    nextSummaryClock = now + SummaryClockInterval;
                    DevDiag.Count("ai.market.trade.summary");
                }
            }
        }

        private void Decision(IntPtr root, int player)
        {
            Frame frame = Begin(root, player, DecisionKind, -1, 0);
            bool completed = false;
            try
            {
                if (frame != null)
                {
                    try { MarketStaleEquipmentFix.BeforeDecision(player, frame.BeforeProfile.AicId,
                        frame.BeforeProfile.BuyWeapons, frame.BeforeProfile.LastStand, frame.BeforeProfile.Tick); }
                    catch (Exception failure) { MarketStaleEquipmentFix.FailCorrection(failure); }
                }
                originalDecision(root, player); completed = true;
            }
            finally { Finish(frame, 0, completed); }
        }
        private void Recruit(IntPtr root, int player)
        {
            Frame frame = Begin(root, player, RecruitKind, -1, 0);
            bool completed = false;
            try { originalRecruit(root, player); completed = true; }
            finally { Finish(frame, 0, completed); }
        }
        private int Buy(IntPtr root, int player, int good, int amount)
        {
            Frame frame = Begin(root, player, BuyKind, good, amount);
            int result = 0; bool completed = false;
            try { result = originalBuy(root, player, good, amount); completed = true; return result; }
            finally { Finish(frame, result, completed); }
        }
        private void Sell(IntPtr root, int player, int good, int amount)
        {
            Frame frame = Begin(root, player, SellKind, good, amount);
            bool completed = false;
            try { originalSell(root, player, good, amount); completed = true; }
            finally { Finish(frame, 0, completed); }
        }

        // Capture failures cannot skip, retry, replace or swallow the original call.
        private Frame Begin(IntPtr root, int player, int kind, int good, int amount)
        {
            if (Volatile.Read(ref active) == 0) return null;
            long started = Stopwatch.GetTimestamp();
            Frame frame = null;
            lock (captureGate)
            {
              try
              {
                if (Volatile.Read(ref active) == 0) return null;
                int thread = Thread.CurrentThread.ManagedThreadId;
                int expected = Interlocked.CompareExchange(ref producerThread, thread, 0);
                if (expected != 0 && expected != thread) { Interlocked.Increment(ref invalidContexts); return null; }
                if ((uint)(player - 1) >= PlayerCount) { Interlocked.Increment(ref invalidContexts); return null; }
                if (lifecycle.Depth == frames.Length) { Interlocked.Increment(ref depthOverflows); return null; }
                frame = frames[lifecycle.Depth];
                lifecycle.Enter();
                frame.Session = session; frame.Sequence = ++sequence; frame.Kind = kind; frame.Player = player;
                frame.Good = good; frame.Requested = amount; frame.Root = root;
                frame.Parent = lifecycle.Depth > 1 ? frames[lifecycle.Depth - 2].Sequence : 0;
                frame.BeforeProfile = CaptureProfile(root, player);
                if (kind <= RecruitKind)
                {
                    for (int i = 0; i < GoodCount; i++) frame.Before[i] = Capture(player, i, frame.BeforeProfile);
                }
                else frame.TradeBefore = Capture(player, good, frame.BeforeProfile);
                frame.PreCaptureTicks = Stopwatch.GetTimestamp() - started;
                Interlocked.Increment(ref nativeCalls);
                return frame;
              }
              catch
              {
                if (frame != null) lifecycle.Leave();
                Interlocked.Increment(ref captureFailures);
                return null;
              }
            }
        }

        private void Finish(Frame frame, int result, bool completed)
        {
            if (frame == null) return;
            long started = Stopwatch.GetTimestamp();
            lock (captureGate)
            {
              try
              {
                if (!completed) Interlocked.Increment(ref originalExceptions);
                if (frame.Session != session || Volatile.Read(ref active) == 0)
                { Interlocked.Increment(ref staleFrames); return; }
                Profile profile = CaptureProfile(frame.Root, frame.Player);
                if (frame.Kind <= RecruitKind)
                {
                    CountPair(frame, profile, completed);
                    for (int good = 0; good < GoodCount; good++)
                        ObservePairGood(frame, good, Capture(frame.Player, good, profile), profile, completed);
                }
                else ObserveTrade(frame, result, Capture(frame.Player, frame.Good, profile), profile, completed);
              }
              catch { Interlocked.Increment(ref captureFailures); }
              finally
              {
                lifecycle.Leave();
                // Deferred End/Start can now seal the old session. A capture failure
                // during that work must never replace an original exception/return.
                try { ApplyPendingLifecycle(); }
                catch { Volatile.Write(ref active, 0); Interlocked.Increment(ref captureFailures); }
                Interlocked.Add(ref captureClockTicks, frame.PreCaptureTicks + Stopwatch.GetTimestamp() - started);
              }
            }
        }

        private Profile CaptureProfile(IntPtr root, int player)
        {
            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(player, out GamePlayerResources* r) || r == null)
                throw new InvalidOperationException("player resources unavailable");
            var p = new Profile
            {
                Gold = r->r_TotalGoodsGold, Phase = (int)r->r_AISellOrBuyPhase,
                Popularity = unchecked((int)r->r_CurrentPopularity), LastStand = unchecked((int)r->r_AILastStandPhase),
                PurchaseGate = unchecked((int)r->N0000535B), MarketId = unchecked((int)r->r_TradePostId),
                CachedMarket = unchecked((int)r->r_bCachedTradePostExists), AicId = unchecked((int)r->r_AILordMinusOne),
                RecruitmentMode = (int)r->r_AICombatRecruitmentMode, Utc = DateTime.UtcNow.Ticks
            };
            if (!SimulationTickEvents.TryGetCurrentTick(out p.Tick))
            { p.Tick = lastMapTick; p.Flags |= TickUnavailable; Interlocked.Increment(ref missingMapTicks); }
            var units = GameUnitManagerAPI.Instance.GetUnitManager();
            if (units.Pointer != null)
            { p.Failure = units.Pointer->r_RecruitmentResultFailureReason; p.MissingGood = units.Pointer->r_RecruitmentResultMissingGoodId; }
            else p.Flags |= UnitManagerUnavailable;
            long offset = ((long)p.AicId - 1) * sizeof(InternalAIC);
            long address = root.ToInt64() + offset;
            if (root.ToInt64() != module + ProfileRootRva || p.AicId <= 0 ||
                address < module || address > module + moduleLength - sizeof(InternalAIC))
            { p.Flags |= AicUnavailable; return p; }
            InternalAIC* aic = (InternalAIC*)address;
            p.BuyWeapons = aic->buy_weapons; p.SellWeapons = aic->trade_weapons_at;
            p.TradeMinimum = aic->trade_minimum; p.BaseGoldReserve = aic->base_gold_reserves;
            p.BuyBreadAt = aic->buy_bread_at; p.BuyCheeseAt = aic->buy_cheese_at;
            p.BuyApplesAt = aic->buy_apples_at; p.BuyFoodAmount = aic->buy_food_amount;
            p.LowPopularity = aic->vlow_popularity; p.SellFood = aic->sell_food_at;
            p.SellWood = aic->trade_wood_at; p.SellStone = aic->trade_stone_at;
            p.SellResources = aic->trade_resources_at; p.SellFlour = aic->trade_flour_at;
            p.SellAle = aic->trade_ale_at; p.SellPitch = aic->trade_pitch_at;
            int* sellAll = &aic->sell_all1;
            for (int i = 0; i < 15; i++)
            {
                int raw = sellAll[i];
                if ((uint)raw < (uint)GoodCount) p.SellAllMask |= 1U << raw;
                else { p.Flags |= NonGoodsSellAll; p.NonGoodsSellAll = raw; }
            }
            return p;
        }

        private static Sample Capture(int player, int good, Profile profile)
        {
            var result = new Sample { Tick = profile.Tick, Utc = profile.Utc, Gold = profile.Gold, Phase = profile.Phase, Flags = profile.Flags,
                ExperimentPhase = Volatile.Read(ref MarketBacklogScenario.Phase),
                InterventionSequence = Interlocked.Read(ref MarketBacklogScenario.InterventionSequence) };
            if ((uint)good >= (uint)GoodCount) { result.Flags |= GoodUnavailable; return result; }
            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(player, out GamePlayerResources* r) || r == null)
                throw new InvalidOperationException("player resources unavailable");
            result.Stock = ((int*)&r->r_TotalGoodsNull)[good];
            result.Pending = ((int*)&r->r_AIPendingMarketPurchaseAmountNull)[good];
            // Vanilla tests a dword for zero and decrements only signed-positive
            // timers. Preserve its raw signed representation, even though SE exposes uint.
            result.SuppressSellAllTimer = ((int*)&r->r_AISuppressSellAllTimerNull)[good];
            return result;
        }

        private static void ValidateLayout()
        {
            if (GoodCount != 25 || Marshal.SizeOf(typeof(GamePlayerResources)) != 0x583C || Marshal.SizeOf(typeof(InternalAIC)) != 0x5E4)
                throw new InvalidOperationException("Trade trace native array/record layout changed.");
            RequireOffset<GamePlayerResources>(nameof(GamePlayerResources.r_TotalGoodsNull), 0x4D0);
            RequireOffset<GamePlayerResources>(nameof(GamePlayerResources.r_TotalGoodsGold), 0x50C);
            RequireOffset<GamePlayerResources>(nameof(GamePlayerResources.r_AIPendingMarketPurchaseAmountNull), 0x2A70);
            RequireOffset<GamePlayerResources>(nameof(GamePlayerResources.r_AISuppressSellAllTimerNull), 0x4FD0);
            RequireOffset<GamePlayerResources>(nameof(GamePlayerResources.r_AISuppressSellAllTimerMetalArmour), 0x5030);
            RequireOffset<GamePlayerResources>(nameof(GamePlayerResources.r_AISellOrBuyPhase), 0x2A6C);
            RequireOffset<GamePlayerResources>(nameof(GamePlayerResources.r_CurrentPopularity), 0x60);
            RequireOffset<GamePlayerResources>(nameof(GamePlayerResources.r_TradePostId), 0x1AC);
            RequireOffset<GamePlayerResources>(nameof(GamePlayerResources.r_bCachedTradePostExists), 0x2A68);
            RequireOffset<GamePlayerResources>(nameof(GamePlayerResources.r_AILordMinusOne), 0x2300);
            RequireOffset<GamePlayerResources>(nameof(GamePlayerResources.r_AICombatRecruitmentMode), 0x30F8);
            RequireOffset<GamePlayerResources>(nameof(GamePlayerResources.r_AILastStandPhase), 0x38F0);
            RequireOffset<GamePlayerResources>(nameof(GamePlayerResources.N0000535B), 0x3974);
            RequireOffset<GameUnitManager>(nameof(GameUnitManager.r_RecruitmentResultFailureReason), 0x650);
            RequireOffset<GameUnitManager>(nameof(GameUnitManager.r_RecruitmentResultMissingGoodId), 0x654);
            RequireOffset<InternalAIC>(nameof(InternalAIC.buy_weapons), 0x98);
            RequireOffset<InternalAIC>(nameof(InternalAIC.vlow_popularity), 0x18);
            RequireOffset<InternalAIC>(nameof(InternalAIC.sell_food_at), 0x7C);
            RequireOffset<InternalAIC>(nameof(InternalAIC.trade_wood_at), 0xA8);
            RequireOffset<InternalAIC>(nameof(InternalAIC.trade_stone_at), 0xAC);
            RequireOffset<InternalAIC>(nameof(InternalAIC.trade_resources_at), 0xB0);
            RequireOffset<InternalAIC>(nameof(InternalAIC.trade_flour_at), 0xB4);
            RequireOffset<InternalAIC>(nameof(InternalAIC.trade_weapons_at), 0xB8);
            RequireOffset<InternalAIC>(nameof(InternalAIC.trade_ale_at), 0xBC);
            RequireOffset<InternalAIC>(nameof(InternalAIC.trade_pitch_at), 0xC0);
            RequireOffset<InternalAIC>(nameof(InternalAIC.trade_minimum), 0xC4);
            RequireOffset<InternalAIC>(nameof(InternalAIC.base_gold_reserves), 0xC8);
            RequireOffset<InternalAIC>(nameof(InternalAIC.sell_all1), 0xD8);
            RequireOffset<InternalAIC>(nameof(InternalAIC.sell_all15), 0x110);
        }
        private static void RequireOffset<T>(string member, int offset) =>
            _ = Marshal.OffsetOf(typeof(T), member).ToInt32() == offset ? 0 : throw new InvalidOperationException(typeof(T).Name + "." + member + " layout changed.");
    }
}
#endif
