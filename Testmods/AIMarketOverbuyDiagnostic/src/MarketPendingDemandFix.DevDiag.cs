#if SERP_DEVDIAG
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using APIShared.NativeSites;
using APIShared.Simulation;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using Shared;
using Evidence = AIMarketOverbuyDiagnostic.MarketDemandEvidence;

namespace AIMarketOverbuyDiagnostic
{
    // Exact stores establish quantity ownership; source policies distinguish
    // procurement refreshes from current construction resource requirements.
    // All native objects are accessed on the producer, never by the output worker.
    internal static unsafe class MarketPendingDemandFix
    {
        private const string Owner = "AIMarketOverbuyDiagnostic_Serp", Feature = "ai-market-pending-demand";
        private const int Players = GamePlayerManagerAPI.MAX_PLAYERS, Goods = (int)eGoods.Count;
        private const int PlayerStride = 0x583C, PendingRootRva = 0x379D840, TotalGoodsRootRva = 0x379B2A0, AicRootRva = 0x404C950;
        private const int Capacity = 512;
        private static readonly object gate = new object();
        private static readonly Request[] requests = new Request[(Players + 1) * Goods];
        private static readonly bool[] firstSource = new bool[(Players + 1) * Goods], firstAdjustment = new bool[(Players + 1) * Goods];
        private static readonly long[] reductionsByGood = new long[(Players + 1) * Goods];
        private static readonly Evidence[] records = new Evidence[Capacity];
        private static readonly int[] decisionDepth = new int[Players + 1];
        private static MarketDemandWriterHooks.WriterHook[] hooks;
        private static MarketDemandWriterHooks.Observer observerRoot;
        private static readonly Action<int, int, int, int, int> fixesCapture = CaptureFixesStore;
        private static readonly byte[] fixesObserverBytes = { 0x4C, 0x69, 0xC0, 0x3C, 0x58, 0x00, 0x00 };
        private static readonly byte[] recountBytes = { 0x42, 0x89, 0xAC, 0x36, 0xCC, 0xF0, 0x12, 0x00 };
        private static readonly byte[] rebuildBytes = { 0x48, 0x8B, 0x4C, 0x24, 0x28, 0x48, 0x33, 0xCC };
        private static readonly byte[] lossBytes = { 0x45, 0x89, 0x94, 0x8E, 0xA0, 0xB2, 0x79, 0x03 };
        private static IntPtr writerGate, receiptGate;
        private static long module, session, sequence, written, read, nextSummary, nextError;
        private static int moduleLength, installed, corrective, sessionActive, forceSummary, summaryInFlight;
        private static long lastSourceCount, lastReductionCount, lastIncreaseCount, lastCancellationCount;
        private static Exception failureToWrite;
        internal static long Reductions, Increases, CancelledOrders, OmittedEvidence, ZeroStockPassed, CorrectionFailures, SourceStores;
        // 0=source observer, 1=before decision, 2=after decision, 3=stock boundary. Histograms contain
        // only this owner's managed work, never the native original's duration.
        internal static readonly long[] ManagedCalls = new long[4], ManagedClockTicks = new long[4], ManagedMaxClockTicks = new long[4];
        private static readonly long[] timingHistogram = new long[4 * 32];

        private struct Request
        {
            internal MarketDemandTarget Target;
            internal MarketAmmoDemandSource.Origin AmmoOrigin;
            internal int Tick, SourceRva, SourceStock, GoalAmount, PolicySourceRva, WriteRva, WriteAmount;
            internal long Sequence, GoalSession, WriteSession, WriteSequence;
            internal bool WaitingAfter;
        }

        internal static bool Ready => Volatile.Read(ref installed) != 0;
        internal static bool OutputDrained => Volatile.Read(ref read) == Volatile.Read(ref written) &&
            Volatile.Read(ref forceSummary) == 0 && Volatile.Read(ref summaryInFlight) == 0;

        internal static void Install(CrusaderLibraryLoadContext context, ManualLogSource log)
        {
            lock (gate)
            {
                if (hooks != null) return;
                bool claimed = false;
                var candidates = new MarketDemandWriterHooks.WriterHook[MarketDemandSites.All.Length];
                try
                {
                    if (!DebugLogHelper.IsCurrentNativeLibraryVersion())
                        throw new InvalidOperationException("Demand correction requires the audited native hash.");
                    MarketAmmoDemandSource.Initialize(context);
                    MarketFixesDemandSource.Initialize(context, log);
                    bool withFixes = MarketFixesDemandSource.IsCovered;
                    candidates = new MarketDemandWriterHooks.WriterHook[MarketDemandSites.All.Length + (withFixes ? 1 : 0) + 3];
                    if (Goods != 25 || sizeof(GamePlayerResources) != PlayerStride || sizeof(InternalAIC) != 0x5E4 ||
                        Marshal.OffsetOf(typeof(GamePlayerResources), nameof(GamePlayerResources.r_AIPendingMarketPurchaseAmountNull)).ToInt32() != 0x2A70)
                        throw new InvalidOperationException("Demand correction resource/AIC layout differs.");
                    module = context.ModuleHandle.ToInt64(); moduleLength = context.Memory.Length;
                    NativePatchGuard.Bind(context, log);
                    var spans = new NativeSiteSpan[candidates.Length];
                    for (int i = 0; i < spans.Length; i++)
                    {
                        CandidateSite(i, withFixes, out int rva, out byte[] bytes, out _, out _);
                        spans[i] = new NativeSiteSpan(rva, bytes.Length, NativeSiteMode.Exclusive);
                        NativePatchGuard.RequireUnpatched(unchecked((ulong)module) + (uint)rva, bytes.Length, Feature, Feature);
                    }
                    var registration = NativeSiteRegistry.Register(Owner, Feature, spans);
                    if (!registration.Succeeded) throw new InvalidOperationException(registration.Reason);
                    claimed = true;
                    writerGate = Marshal.AllocHGlobal(sizeof(int));
                    receiptGate = Marshal.AllocHGlobal(sizeof(int));
                    *(int*)writerGate = 0; // Data gate only; executable pages are owned by the relay helper.
                    *(int*)receiptGate = 0;
                    observerRoot = ObserveNativeStore;
                    for (int i = 0; i < candidates.Length; i++)
                    {
                        CandidateSite(i, withFixes, out int rva, out byte[] bytes, out int siteIndex, out bool isReceipt);
                        candidates[i] = siteIndex < MarketDemandSites.All.Length
                            ? MarketDemandWriterHooks.PrepareSource(context, rva, bytes, siteIndex, writerGate, observerRoot, receiptGate)
                            : MarketDemandWriterHooks.Prepare(context, rva, bytes, siteIndex,
                                isReceipt ? receiptGate : writerGate, observerRoot);
                    }
                    hooks = candidates; // Root the complete prepared set before the first publication.
                    for (int i = 0; i < candidates.Length; i++) candidates[i].Publish();
                    MarketFixesDemandSource.SealBodyAfterInstallation();
                    Volatile.Write(ref installed, 1);
                    DebugLogHelper.LogInfo(log, "AI_MARKET_DEMAND_READY: nativeDemandStores=22 stockMarkers=3 hooks=" + candidates.Length +
                        " method=reference-rva; gate=session; fixedBatch=permanent-credit; material=current-cost; AUTOammo=atomic-unit-goal; manualAmmo=unowned; A:u=unit/global/creation need=stone-cost a=signed-ammo/result(0unknown,1ended,2live); untrackedOrders=unchanged; MD_SUM delta36=source/down/up/cancel p99us36=source/before/after/stock om36=omitted (base36).");
                }
                catch (Exception failure)
                {
                    bool anyPublished = false;
                    for (int i = 0; i < candidates.Length; i++)
                    {
                        if (candidates[i] == null) continue;
                        anyPublished |= candidates[i].IsPublished;
                        candidates[i].DisposeUnpublished();
                    }
                    if (writerGate != IntPtr.Zero) *(int*)writerGate = 0;
                    if (receiptGate != IntPtr.Zero) *(int*)receiptGate = 0;
                    if (!anyPublished)
                    {
                        if (writerGate != IntPtr.Zero) Marshal.FreeHGlobal(writerGate);
                        if (receiptGate != IntPtr.Zero) Marshal.FreeHGlobal(receiptGate);
                        writerGate = IntPtr.Zero;
                        receiptGate = IntPtr.Zero;
                        if (claimed) NativeSiteRegistry.Unregister(Owner, Feature);
                    }
                    // Partial publications stay rooted and permanently dormant.
                    hooks = candidates;
                    DebugLogHelper.LogError(log, "AI_MARKET_DEMAND_UNAVAILABLE: " + failure);
                }
            }
        }

        // Callback indices stay stable when the optional Fixes relay is absent:
        // native stores0..21 (post callbacks+256), Fixes22, recount23/24, loss25.
        private static void CandidateSite(int position, bool withFixes, out int rva, out byte[] bytes,
            out int siteIndex, out bool isReceipt)
        {
            int nativeCount = MarketDemandSites.All.Length;
            isReceipt = false;
            if (position < nativeCount)
            { MarketDemandSite site = MarketDemandSites.All[position]; rva = site.Rva; bytes = site.Bytes; siteIndex = position; return; }
            if (withFixes && position == nativeCount)
            { rva = MarketFixesDemandSource.ObservationRva; bytes = fixesObserverBytes; siteIndex = nativeCount; return; }
            int receipt = position - nativeCount - (withFixes ? 1 : 0);
            isReceipt = true; siteIndex = nativeCount + 1 + receipt;
            rva = receipt == 0 ? 0xC1F4A : receipt == 1 ? 0xB9A30 : 0xB8122;
            bytes = receipt == 0 ? recountBytes : receipt == 1 ? rebuildBytes : lossBytes;
        }

        internal static void Reset(long nextSession)
        {
            lock (gate)
            {
                if (writerGate != IntPtr.Zero) *(int*)writerGate = 0;
                if (receiptGate != IntPtr.Zero) *(int*)receiptGate = 0;
                sessionActive = corrective = 0;
                session = nextSession;
                Interlocked.Exchange(ref forceSummary, 1);
                Array.Clear(requests, 0, requests.Length);
                Array.Clear(firstSource, 0, firstSource.Length);
                Array.Clear(firstAdjustment, 0, firstAdjustment.Length);
                Array.Clear(reductionsByGood, 0, reductionsByGood.Length);
                Array.Clear(decisionDepth, 0, decisionDepth.Length);
                if (!Ready) return;
                MarketFixesDemandSource.Recheck();
                for (int i = 0; i < hooks.Length; i++)
                    if (!hooks[i].IntegrityReady) { RecordFailure(new InvalidOperationException("Demand writer live integrity differs.")); return; }
                sessionActive = 1;
                // Owned baseline scenarios still observe exact sources. The fixed
                // scenario enables correction after its independently logged gift.
                corrective = string.IsNullOrEmpty(MarketAutomation.Scenario) || MarketAutomation.Scenario == "market-fresh-fixed" ? 1 : 0;
                *(int*)writerGate = 1;
                *(int*)receiptGate = corrective;
            }
        }

        internal static void End()
        {
            lock (gate)
            {
                sessionActive = corrective = 0;
                Interlocked.Exchange(ref forceSummary, 1);
                if (writerGate != IntPtr.Zero) *(int*)writerGate = 0;
                if (receiptGate != IntPtr.Zero) *(int*)receiptGate = 0;
                Array.Clear(requests, 0, requests.Length);
                Array.Clear(decisionDepth, 0, decisionDepth.Length);
            }
        }

        private static void ObserveNativeStore(IntPtr snapshot, int siteIndex)
        {
            long started = Stopwatch.GetTimestamp();
            int nativeCount = MarketDemandSites.All.Length;
            bool after = siteIndex >= 256;
            int index = after ? siteIndex - 256 : siteIndex;
            bool isReceipt = !after && index > nativeCount;
            try
            {
                lock (gate)
                {
                    if (sessionActive == 0 || !Ready) return;
                    if (snapshot == IntPtr.Zero || (after ? (uint)index >= (uint)nativeCount : (uint)index > (uint)(nativeCount + 3)))
                        throw new InvalidOperationException("Demand store snapshot/site is invalid.");
                    if (isReceipt)
                    {
                        if (corrective == 0) return;
                        if (index == nativeCount + 1)
                        {
                            ulong playerOffset = ((ulong*)snapshot)[9]; // RSI is the audited player*583C.
                            // Native storage updates also accept neutral owner0.
                            // That slot has no owned AI package to reconcile.
                            if (playerOffset == 0) return;
                            if (playerOffset % PlayerStride != 0 || playerOffset / PlayerStride > Players)
                                throw new InvalidOperationException("Stock recount player stride is invalid.");
                            ObserveStockBoundary((int)(playerOffset / PlayerStride), 0xC1F4A);
                        }
                        else if (index == nativeCount + 2)
                        { for (int bulkPlayer = 1; bulkPlayer <= Players; bulkPlayer++) ObserveStockBoundary(bulkPlayer, 0xB9A30); }
                        else
                        {
                            ulong* registers = (ulong*)snapshot;
                            if (registers[1] != unchecked((ulong)module) || unchecked((int)registers[5]) != 0)
                                throw new InvalidOperationException("No-storage loss operands differ from the audited zero store.");
                            ulong lossCell = registers[1] + registers[13] * sizeof(int) + TotalGoodsRootRva;
                            if (DecodeCell(lossCell, false, out int lossPlayer, out int lossGood, out GamePlayerResources* lossResources))
                                ObservePlannedLoss(lossPlayer, lossGood, lossResources);
                        }
                        return;
                    }
                    if (index == nativeCount)
                    {
                        int fixesPlayer = unchecked((int)((ulong*)snapshot)[14]); // RAX, before the audited IMUL.
                        MarketFixesDemandSource.CapturePlanned(fixesPlayer, fixesCapture);
                        return;
                    }
                    MarketDemandSite site = MarketDemandSites.All[index];
                    if (!DecodeCell(site.Cell(snapshot), true, out int player, out int good, out GamePlayerResources* resources)) return;
                    if (after) FinishStore(player, good, site.Amount(snapshot), site.Rva, resources);
                    else CaptureStore(player, good, site.Amount(snapshot), site.Rva, resources, true, snapshot);
                }
            }
            catch (Exception failure)
            {
                // Never let a reverse-P/Invoke exception cross the native store.
                lock (gate)
                {
                    sessionActive = corrective = 0;
                    if (writerGate != IntPtr.Zero) *(int*)writerGate = 0;
                    if (receiptGate != IntPtr.Zero) *(int*)receiptGate = 0;
                    RecordFailure(failure);
                }
            }
            finally { RecordTiming(isReceipt ? 3 : 0, Stopwatch.GetTimestamp() - started); }
        }

        private static bool DecodeCell(ulong cell, bool pendingCell, out int player, out int good, out GamePlayerResources* resources)
        {
            player = good = 0; resources = null;
            ulong root = unchecked((ulong)module) + (uint)(pendingCell ? PendingRootRva : TotalGoodsRootRva);
            if (cell < root) throw new InvalidOperationException("Observed goods cell precedes its audited array.");
            ulong offset = cell - root, slot = offset / PlayerStride, within = offset % PlayerStride;
            if (slot > Players || (within & 3) != 0 || within >= Goods * sizeof(int))
                throw new InvalidOperationException("Observed goods cell is outside the audited player/good cells.");
            player = (int)slot; good = (int)(within / sizeof(int));
            if (!ValidCell(player, good) || !GamePlayerManagerAPI.Instance.IsAIPlayer(player)) return false;
            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(player, out resources) || resources == null)
                throw new InvalidOperationException("Observed goods player resources are unavailable.");
            int* field = pendingCell ? (int*)&resources->r_AIPendingMarketPurchaseAmountNull : (int*)&resources->r_TotalGoodsNull;
            if (unchecked((ulong)(field + good)) != cell)
                throw new InvalidOperationException("Observed goods cell disagrees with the SDK resource pointer.");
            return true;
        }

        // Optional native-store adapters use the same source contract. This does
        // not infer a store from a policy callback or a changed snapshot.
        internal static void ObserveExternalStore(int player, int good, int amount, int sourceRva)
        {
            try
            {
                lock (gate)
                {
                    if (sessionActive == 0 || !ValidCell(player, good) || !GamePlayerManagerAPI.Instance.IsAIPlayer(player)) return;
                    if (GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(player, out GamePlayerResources* resources) && resources != null)
                        CaptureStore(player, good, amount, sourceRva, resources, false, IntPtr.Zero);
                }
            }
            catch (Exception failure) { FailCorrection(failure); }
        }

        private static void CaptureFixesStore(int player, int good, int sourceStock, int amount, int sourceRva) =>
            ObserveExternalStore(player, good, amount, sourceRva);

        private static void CaptureStore(int player, int good, int amount, int sourceRva, GamePlayerResources* resources, bool paired, IntPtr sourceFrame)
        {
            int index = player * Goods + good, stock = ((int*)&resources->r_TotalGoodsNull)[good];
            int aicId = unchecked((int)resources->r_AILordMinusOne);
            int before = ((int*)&resources->r_AIPendingMarketPurchaseAmountNull)[good];
            if (amount <= 0 || !DescribeSource(sourceRva, good, out bool material, out MarketDemandSourceFamily family, out int padding) ||
                !TryPolicy(player, good, resources, material, family, amount, sourceRva, out long policy))
            { requests[index] = default; return; }
            var ammoOrigin = default(MarketAmmoDemandSource.Origin);
            bool autoAmmo = family == MarketDemandSourceFamily.Ammo;
            if (autoAmmo && (good != (int)eGoods.STORED_STONE_BLOCKS || amount != MarketAmmoDemandSource.RequiredStone ||
                !MarketAmmoDemandSource.TryCapture(sourceFrame, player, stock, unchecked((ulong)module), out ammoOrigin)))
            { requests[index] = default; return; } // Manual/unknown replaces old provenance but remains Vanilla.
            Request request = requests[index];
            MarketDemandTarget target = request.Target;
            bool keepOrigin;
            if (autoAmmo)
            {
                bool same = request.GoalSession == session && request.AmmoOrigin.SameIdentity(ammoOrigin);
                if (!target.ObserveAutoAmmoStore(stock, before, amount, MarketAmmoDemandSource.RequiredStone,
                    aicId, policy, same, out keepOrigin))
                { requests[index] = default; return; }
                request.AmmoOrigin = ammoOrigin;
            }
            else if (material)
            {
                int oldCost = target.MaterialCost;
                if (!target.ObserveMaterialStore(stock, before, amount, padding, aicId, policy, out bool merged))
                { requests[index] = default; return; }
                keepOrigin = merged && target.MaterialCost == oldCost;
            }
            else
            {
                if (!target.ObserveFixedStore(stock, before, amount, aicId, policy, family, out keepOrigin))
                { requests[index] = default; return; }
            }
            long sourceSequence = ++sequence;
            if (!keepOrigin)
            {
                request.Tick = CurrentTick(); request.Sequence = sourceSequence; request.SourceRva = sourceRva;
                request.SourceStock = stock; request.GoalAmount = amount;
                request.GoalSession = session;
            }
            request.Target = target;
            request.PolicySourceRva = sourceRva;
            request.WriteRva = sourceRva; request.WriteAmount = amount;
            request.WriteSession = session; request.WriteSequence = sourceSequence; request.WaitingAfter = paired;
            requests[index] = request;
            Interlocked.Increment(ref SourceStores);
            if (firstSource[index]) return;
            var evidence = new Evidence { Session = session, Sequence = request.Sequence, Player = player, Good = good,
                Tick = request.Tick, SourceRva = request.SourceRva, SourceStock = request.SourceStock, After = request.GoalAmount, Goal = target.Goal,
                OriginalPackage = target.OriginalPackage, Credited = target.Credited, AicId = aicId,
                IsMaterial = target.Policy == MarketDemandPolicy.MaterialStockGoal, MaterialCost = target.MaterialCost, MaterialPadding = target.MaterialPadding };
            AddAmmoEvidence(ref evidence, request, player, stock);
            if (Enqueue(evidence)) firstSource[index] = true;
        }

        private static bool DescribeSource(int rva, int good, out bool material, out MarketDemandSourceFamily family, out int padding)
        {
            material = false; padding = 0; family = default;
            switch (rva)
            {
                case 0xCC555: case 0xCC5C1: case 0xCC657: case 0xCC692: case 0xCC6E4:
                    material = true; padding = good == (int)eGoods.STORED_WOOD_PLANKS ? 2 : 0; return true;
                case 0x403A7: case 0x40597: case 0x40D3C: case 0x40D62:
                    family = MarketDemandSourceFamily.Recruit; return true;
                case 0x2A464: case 0x2A488: case 0x2A4AC: case 0x2A4D0: case 0x2A4F4:
                    family = MarketDemandSourceFamily.Food; return true;
                case 0x2ADB5: case 0x2ADD7: case 0x2ADF9: case 0x2AE1B:
                    family = MarketDemandSourceFamily.Scarcity; return true;
                case 0x2EB9C: family = MarketDemandSourceFamily.Pitch; return true;
                case 0x2F56E: family = MarketDemandSourceFamily.Emergency; return true;
                case 0x51ADB: family = MarketDemandSourceFamily.Aiv; return true;
                case 0xD5791: family = MarketDemandSourceFamily.Ammo; return true;
                case 0x2A4D8: family = MarketDemandSourceFamily.Fixes; return true;
                default: return false;
            }
        }

        private static bool TryPolicy(int player, int good, GamePlayerResources* resources, bool material,
            MarketDemandSourceFamily family, int package, int policySourceRva, out long policy)
        {
            policy = 0;
            int aicId = unchecked((int)resources->r_AILordMinusOne);
            long address = module + AicRootRva + ((long)aicId - 1) * sizeof(InternalAIC);
            if (aicId <= 0 || address < module || address > module + moduleLength - sizeof(InternalAIC)) return false;
            if (material) { policy = MarketDemandPolicyStamp.Material; return true; }
            InternalAIC* aic = (InternalAIC*)address;
            var snapshot = default(MarketDemandPolicySnapshot);
            switch (family)
            {
                case MarketDemandSourceFamily.Recruit:
                    snapshot.BuyWeapons = aic->buy_weapons; snapshot.LastStandPhase = resources->r_AILastStandPhase;
                    policy = MarketDemandPolicyStamp.Recruit(snapshot, policySourceRva == 0x40D3C || policySourceRva == 0x40D62);
                    return true;
                case MarketDemandSourceFamily.Food:
                    snapshot.BuyFoodAmount = aic->buy_food_amount;
                    switch ((eGoods)good)
                    {
                        case eGoods.STORED_FOOD_FRUIT: snapshot.FoodThreshold = aic->buy_apples_at; break;
                        case eGoods.STORED_FOOD_CHEESE: snapshot.FoodThreshold = aic->buy_cheese_at; break;
                        case eGoods.STORED_FOOD_BREAD: snapshot.FoodThreshold = aic->buy_bread_at; break;
                        case eGoods.STORED_RAW_WHEAT: snapshot.FoodThreshold = aic->buy_wheat_at; break;
                        case eGoods.STORED_RAW_HOPS: snapshot.FoodThreshold = aic->buy_hops_at; break;
                        default: return false;
                    }
                    policy = MarketDemandPolicyStamp.Food(snapshot); return true;
                case MarketDemandSourceFamily.Fixes:
                    if (!MarketFixesDemandSource.TryGetPolicySnapshot(player, good, out snapshot.FixesFlag,
                        out snapshot.FixesThreshold, out snapshot.FixesAmount)) return false;
                    return MarketDemandPolicyStamp.Fixes(snapshot, out policy);
                default:
                    policy = MarketDemandPolicyStamp.Literal(family, package); return true;
            }
        }

        private static bool TryPolicy(int player, int good, GamePlayerResources* resources, Request request, out long policy) =>
            TryPolicy(player, good, resources, request.Target.Policy == MarketDemandPolicy.MaterialStockGoal,
                request.Target.Family, request.Target.OriginalPackage, request.PolicySourceRva, out policy);

        private static void FinishStore(int player, int good, int emitted, int rva, GamePlayerResources* resources)
        {
            if (corrective == 0) return;
            int index = player * Goods + good;
            Request request = requests[index];
            if (!request.WaitingAfter || request.WriteSession != session || request.WriteRva != rva || request.WriteAmount != emitted) return;
            request.WaitingAfter = false;
            int actual = ((int*)&resources->r_AIPendingMarketPurchaseAmountNull)[good];
            if (actual != emitted || request.Target.ExpectedPending != emitted)
            { request.Target.Active = false; requests[index] = request; return; }
            ApplyRequest(player, good, resources, CurrentTick(), rva, ref request);
            requests[index] = request;
        }

        internal static void BeforeDecision(int player, int tick)
        {
            if (Volatile.Read(ref sessionActive) == 0 || Volatile.Read(ref corrective) == 0) return;
            long started = Stopwatch.GetTimestamp();
            try
            {
                lock (gate)
                {
                    if (sessionActive == 0 || corrective == 0 || player <= 0 || player > Players ||
                        !GamePlayerManagerAPI.Instance.IsAIPlayer(player)) return;
                    if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(player, out GamePlayerResources* resources) || resources == null) return;
                    for (int good = (int)eGoods.STORED_WOOD_LOGS; good < Goods; good++)
                    {
                        if (good == (int)eGoods.STORED_GOLD) continue;
                        int index = player * Goods + good;
                        Request request = requests[index];
                        ApplyRequest(player, good, resources, tick, 0x3EC90, ref request);
                        requests[index] = request;
                    }
                }
            }
            catch (Exception failure) { FailCorrection(failure); }
            finally { RecordTiming(1, Stopwatch.GetTimestamp() - started); }
        }

        private static void ApplyRequest(int player, int good, GamePlayerResources* resources, int tick, int boundaryRva, ref Request request)
        {
            if (!request.Target.Active) return;
            if (!CorrectionCovered(good) || !TryPolicy(player, good, resources, request, out long policy))
            { request.Target.Active = false; return; }
            int* cell = (int*)&resources->r_AIPendingMarketPurchaseAmountNull + good;
            int original = *cell, stock = ((int*)&resources->r_TotalGoodsNull)[good];
            int aicId = unchecked((int)resources->r_AILordMinusOne);
            int adjusted;
            bool changed;
            if (request.Target.Policy == MarketDemandPolicy.AutoAmmo)
            {
                if (request.GoalSession != session) { request.Target.Active = false; return; }
                var result = request.AmmoOrigin.TryDesired(player, stock, out int desired, out _);
                if (result == MarketAmmoDemandSource.Result.Unknown) { request.Target.Active = false; return; }
                changed = request.Target.TryReconcileAutoAmmo(original, stock, aicId, policy, desired, out adjusted);
            }
            else changed = request.Target.TryReconcile(original, stock, aicId, policy, out adjusted);
            if (!changed)
            {
                if (boundaryRva == 0x3EC90 && request.Target.Active && stock == 0) Interlocked.Increment(ref ZeroStockPassed);
                return;
            }
            *cell = adjusted;
            if (*cell != adjusted) throw new InvalidOperationException("Demand correction readback differs.");
            int index = player * Goods + good;
            if (adjusted < original)
            { Interlocked.Increment(ref Reductions); Interlocked.Increment(ref reductionsByGood[index]); }
            else Interlocked.Increment(ref Increases);
            if (adjusted == 0) Interlocked.Increment(ref CancelledOrders);
            if (firstAdjustment[index]) return;
            var evidence = new Evidence { Kind = 1, Session = session, Player = player, Good = good,
                Tick = tick, Before = original, After = adjusted, Stock = stock, Goal = request.Target.Goal,
                OriginalPackage = request.Target.OriginalPackage, Credited = request.Target.Credited,
                IsMaterial = request.Target.Policy == MarketDemandPolicy.MaterialStockGoal,
                MaterialCost = request.Target.MaterialCost, MaterialPadding = request.Target.MaterialPadding,
                AicId = aicId, SourceRva = request.SourceRva, SourceTick = request.Tick,
                SourceSequence = request.Sequence, SourceStock = request.SourceStock, BoundaryRva = boundaryRva };
            AddAmmoEvidence(ref evidence, request, player, stock);
            if (Enqueue(evidence)) firstAdjustment[index] = true;
        }

        private static void AddAmmoEvidence(ref Evidence evidence, Request request, int player, int stock)
        {
            if (request.Target.Policy != MarketDemandPolicy.AutoAmmo) return;
            var result = request.AmmoOrigin.TryDesired(player, stock, out _, out int ammo);
            evidence.IsAmmo = true; evidence.UnitId = request.AmmoOrigin.UnitId;
            evidence.UnitGlobalId = request.AmmoOrigin.GlobalId; evidence.UnitCreationTick = request.AmmoOrigin.CreationTick;
            evidence.Ammo = ammo; evidence.AmmoState = (int)result;
        }

        // Root brackets only the actual native decision. Its own purchases call
        // the same recount functions and must not be counted as avoided purchases.
        internal static void EnterDecision(int player)
        {
            if (player <= 0 || player > Players) return;
            lock (gate) decisionDepth[player]++;
        }
        internal static void LeaveDecision(int player)
        {
            if (player <= 0 || player > Players) return;
            lock (gate) { if (decisionDepth[player] > 0) decisionDepth[player]--; }
        }

        private static bool CorrectionCovered(int good) => !MarketFixesDemandSource.DoesSourceExist || MarketFixesDemandSource.IsCovered ||
            (good != (int)eGoods.STORED_WOOD_PLANKS && good != (int)eGoods.STORED_IRON_INGOTS && good != (int)eGoods.STORED_FOOD_ALE);

        // Completed recounts provide positive receipts and intervening losses.
        // Publish changed desired quantities immediately, with only relevant policy
        // reads. Own native purchases remain excluded by decisionDepth.
        private static void ObserveStockBoundary(int player, int boundaryRva)
        {
            if (decisionDepth[player] != 0 || !GamePlayerManagerAPI.Instance.IsAIPlayer(player)) return;
            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(player, out GamePlayerResources* resources) || resources == null) return;
            if (unchecked((ulong)&resources->r_TotalGoodsNull) != unchecked((ulong)module) + TotalGoodsRootRva + (uint)player * PlayerStride ||
                unchecked((ulong)&resources->r_AIPendingMarketPurchaseAmountNull) != unchecked((ulong)module) + PendingRootRva + (uint)player * PlayerStride)
                throw new InvalidOperationException("Stock marker disagrees with the SDK player resource pointer.");
            int* stock = (int*)&resources->r_TotalGoodsNull;
            int* pending = (int*)&resources->r_AIPendingMarketPurchaseAmountNull;
            int aicId = unchecked((int)resources->r_AILordMinusOne);
            for (int good = (int)eGoods.STORED_WOOD_LOGS; good < Goods; good++)
            {
                if (good == (int)eGoods.STORED_GOLD) continue;
                int index = player * Goods + good;
                Request request = requests[index];
                if (!request.Target.Active) continue;
                int currentStock = stock[good];
                if (!CorrectionCovered(good)) request.Target.Active = false;
                if (!request.Target.ObserveStock(pending[good], currentStock, aicId))
                { requests[index] = request; continue; }
                if (request.Target.Policy == MarketDemandPolicy.AutoAmmo || request.Target.NeedsAdjustment(currentStock))
                    ApplyRequest(player, good, resources, CurrentTick(), boundaryRva, ref request);
                requests[index] = request;
            }
        }

        private static void ObservePlannedLoss(int player, int good, GamePlayerResources* resources)
        {
            if (decisionDepth[player] != 0) return;
            int index = player * Goods + good;
            // The audited next instruction publishes exactly zero. Reading the
            // temporary negative total before that clamp would lose valid ownership.
            requests[index].Target.ObserveStock(((int*)&resources->r_AIPendingMarketPurchaseAmountNull)[good], 0,
                unchecked((int)resources->r_AILordMinusOne));
        }

        internal static void AfterDecision(int player)
        {
            if (Volatile.Read(ref sessionActive) == 0) return;
            long started = Stopwatch.GetTimestamp();
            try
            {
                lock (gate)
                {
                    if (sessionActive == 0 || player <= 0 || player > Players) return;
                    if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(player, out GamePlayerResources* resources) || resources == null) return;
                    int* pending = (int*)&resources->r_AIPendingMarketPurchaseAmountNull;
                    int* stock = (int*)&resources->r_TotalGoodsNull;
                    int aicId = unchecked((int)resources->r_AILordMinusOne);
                    for (int good = (int)eGoods.STORED_WOOD_LOGS; good < Goods; good++)
                        if (good != (int)eGoods.STORED_GOLD)
                            requests[player * Goods + good].Target.ObserveDecisionEnd(pending[good], stock[good], aicId);
                }
            }
            catch (Exception failure) { FailCorrection(failure); }
            finally { RecordTiming(2, Stopwatch.GetTimestamp() - started); }
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
            }
        }

        internal static void DeactivateExperiment()
        { lock (gate) { corrective = 0; if (receiptGate != IntPtr.Zero) *(int*)receiptGate = 0; } }
        internal static void FailCorrection(Exception failure)
        { lock (gate) { corrective = 0; if (receiptGate != IntPtr.Zero) *(int*)receiptGate = 0; RecordFailure(failure); } }
        private static void RecordFailure(Exception failure)
        {
            Interlocked.Increment(ref CorrectionFailures);
            Interlocked.CompareExchange(ref failureToWrite, failure, null);
        }
        private static bool ValidCell(int player, int good) => player > 0 && player <= Players && good > (int)eGoods.STORED_NULL &&
            good < Goods && good != (int)eGoods.STORED_GOLD;
        private static int CurrentTick() => SimulationTickEvents.TryGetCurrentTick(out int tick) ? tick : AIMarketOverbuyDiagnosticRuntime.LastObservedMapTick;
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
                Exception failure = Interlocked.Exchange(ref failureToWrite, null);
                if (failure != null)
                {
                    nextError = now + Stopwatch.Frequency * 30;
                    DebugLogHelper.LogError(log, "MARKET_STALE_FIX_ERROR: correction disabled; " + failure);
                }
            }
        }
    }
}
#endif
