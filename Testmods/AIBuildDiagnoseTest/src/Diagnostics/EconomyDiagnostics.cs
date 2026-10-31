using APIShared.Economy;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Extensions;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace AIBuildDiagnoseTest
{
    // Process-rooted by AIBuildDiagnoseRuntime. Native callbacks capture only; render drains copied logs.
    internal sealed unsafe partial class EconomyDiagnostics
    {
        internal const string TradeControllerCoverageDetail =
            "not-installed-for-release-fixes-compatibility; market-gate=unknown; buy-sell-alternation=unknown; " +
            "aborted-sale-decisions=unknown; missing-controller-observations-are-not-evidence-of-no-trade; " +
            "purchase-decision, ai-buy, ai-sell and price/extender events remain independent partial observations";
        internal static readonly string[] SummaryStages = { "wood-eligibility", "farm-eligibility", "build-resources", "purchase-decision", "ai-buy", "ai-sell", "ai-recruitment", "wood-retirement", "wood-counter-retirement", "accessibility-sweep", "emergency-demolition", "hovel-reduction" };
        [ThreadStatic] private static EconomyOperation current;
        private EconomyOperation Current => active ? current?.ForSession(session) : null;
        private readonly ManualLogSource log;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly ConcurrentQueue<string> output = new ConcurrentQueue<string>();
        private readonly EconomyLogBudget budget = new EconomyLogBudget();
        private readonly EconomyLedger ledger = new EconomyLedger();
        private readonly Dictionary<string, long> counters = new Dictionary<string, long>();
        private readonly Dictionary<string, string> last = new Dictionary<string, string>();
        private readonly Dictionary<string, long> repeats = new Dictionary<string, long>();
        private readonly Dictionary<int, BuildingEvidenceIdentity> deleting = new Dictionary<int, BuildingEvidenceIdentity>();
        private readonly Dictionary<uint, PendingDeletion> delayedDeletion = new Dictionary<uint, PendingDeletion>();
        private readonly Dictionary<int, BuildingEvidenceIdentity> knownBuildings = new Dictionary<int, BuildingEvidenceIdentity>();
        private sealed class PendingDeletion
        {
            internal BuildingEvidenceIdentity Identity;
            internal string Cause;
            internal long Operation;
        }
        private readonly Dictionary<string, Stack<EconomyOperation>> eventFrames = new Dictionary<string, Stack<EconomyOperation>>();
        private readonly Dictionary<long, string> parameters = new Dictionary<long, string>();
        private readonly object sync = new object();
        private readonly long module;
        private readonly int moduleSize;
        private readonly bool nativeLayout;
        private readonly Dictionary<string, string> coverage = new Dictionary<string, string>();
        private readonly SpectatorAvailabilityConnection availability;
        internal bool ResourceOwnerReady => availability?.Ready == true;
        private EconomyNativeHooks native;
        private long session, sequence;
        private int tick, nextSummary;
        private volatile bool active;
        private bool baseline;
        private bool loadedSave;
        private bool overflowReported;
        private long captureFailures;
        private static readonly FieldInfo[] aicFields = typeof(InternalAIC).GetFields(BindingFlags.Public | BindingFlags.Instance);

        internal EconomyDiagnostics(ManualLogSource logger, ulong address, string hash)
        {
            ValidateLayout();
            log = logger; module = unchecked((long)address);
            nativeLayout = string.Equals(hash, NativeDiagnosticContracts.SupportedHash, StringComparison.OrdinalIgnoreCase);
            try { ValidateRemovalLayout(); removalLayout = true; Coverage("removal-layout", true, "field offsets validated; new reads use the supported native hash only"); }
            catch (Exception error) { Coverage("removal-layout", false, error.Message); }
            if (nativeLayout)
            {
                int pe = Marshal.ReadInt32(new IntPtr(module + 0x3C));
                moduleSize = Marshal.ReadInt32(new IntPtr(module + pe + 24 + 56));
            }
            try
            {
                HookEvents();
                Coverage("extender-events", true, "subscriptions=" + subscriptions.Count + "; event phases remain evidence, not transaction acknowledgements");
            }
            catch (Exception error)
            {
                Coverage("extender-events", false, "partialSubscriptions=" + subscriptions.Count + "; remaining event coverage unavailable: " + error);
            }
            availability = new SpectatorAvailabilityConnection((player, mapper, feedback) =>
            {
                var op = SafeBegin("build-resources", player, mapper, feedback);
                return op == null ? null : (Action<int?, bool>)((result, evaluated) =>
                { op.NativeOriginalEvaluated = evaluated; SafeEnd(op, result); });
            });
            Coverage("trade-controller", false, TradeControllerCoverageDetail, expectedUnavailable: true);
            if (Chainloader.PluginInfos.ContainsKey("SpectatorEditorBuildTest_Serp"))
                Coverage("resource-owner-subscriber", availability.Registered, availability.Detail);
            Coverage("protection-decisions", false,
                "ownerVersion=" + (Chainloader.PluginInfos.TryGetValue("BugfixesAndQoL_Serp", out var owner) ? owner.Metadata.Version.ToString() : "unavailable") +
                "; no per-decision publisher connected; emergency/hovel protection decisions and accessibility classification unavailable; hovel removal context unavailable; protection execution is not assessed by this observation", expectedUnavailable: true);
            if (!MarketPriceEvents.TryRegister(AIBuildDiagnosePlugin.Guid, "economy-price-evidence", null,
                args => Safe(() => OnPrice(args)), out string reason))
                Coverage("price-events", false, reason);
            else Coverage("price-events", true, "query-only; no transaction acknowledgement");
        }
        internal void Install(CrusaderLibraryLoadContext context, string hash)
        {
            native = new EconomyNativeHooks(this); native.Install(context, hash);
            InstallWorkerCauses(context, hash);
        }
        internal void Start(long id, bool enabled, bool isSave)
        {
            lock (sync)
            {
                session = id; active = enabled; loadedSave = isSave; baseline = false;
                sequence = 0; tick = -1; nextSummary = 0; current = null; overflowReported = false;
                ledger.Reset(); budget.Reset(); counters.Clear(); last.Clear(); repeats.Clear();
                deleting.Clear(); delayedDeletion.Clear(); knownBuildings.Clear(); eventFrames.Clear(); parameters.Clear(); captureFailures = 0;
                removals.Reset(id);
                workerCauses.Reset(id);
                workerHooks?.SetActive(enabled);
                Array.Clear(constructionContext, 0, constructionContext.Length);
                Array.Clear(workerDeathSourceSeen, 0, workerDeathSourceSeen.Length);
                Array.Clear(woodBuildDecisions, 0, woodBuildDecisions.Length);
                Array.Clear(resourceBuildDecisions, 0, resourceBuildDecisions.Length);
                if (!active) return;
                if (Chainloader.PluginInfos.ContainsKey("SpectatorEditorBuildTest_Serp"))
                    Coverage("build-resource-native-observation", ResourceOwnerReady,
                        "Spectator owner; nativeOriginalEvaluated=false means owner override, not a native decision");
                Emit("COVERAGE", 0, 0, "capabilities=" + string.Join("|", coverage));
                Emit("SETTINGS", 0, 0, ProtectionSnapshot.Capture());
                var versions = new StringBuilder();
                foreach (var p in Chainloader.PluginInfos)
                    versions.Append(p.Key).Append('=').Append(p.Value.Metadata.Version).Append(';');
                Emit("VERSIONS", 0, 0, versions.ToString());
            }
        }
        internal void Tick(int now)
        {
            Safe(() =>
            {
                lock (sync)
                {
                    if (!active) return;
                    tick = now;
                    MonitorDeletions();
                    if (eventFrames.Count != 0)
                    {
                        Emit("EVENT_GAP", 0, 0, "openEventFrames=" + eventFrames.Count + "; missingPost=true; no-booking; context-cleared-at-next-tick");
                        eventFrames.Clear(); current = null; parameters.Clear();
                    }
                    if (!baseline) { Snapshot("baseline"); baseline = true; }
                    if (now >= nextSummary) { Summary("periodic"); nextSummary = now + 500; }
                }
            });
        }
        internal void End()
        {
            Safe(() =>
            {
                lock (sync)
                {
                    if (!active) return;
                    try
                    {
                        Summary("end");
                        Emit("COVERAGE", 0, 0, $"phase=end, captureFailures={captureFailures}, protectionCallbackFailures={availability?.CallbackFailures ?? 0}, " +
                            $"priceCallbackFailures={MarketPriceEvents.CallbackFailures}, woodSafetyCallbackFailuresProcess={WoodSafetyBridge.CallbackFailures}, pendingDeletionCalls={delayedDeletion.Count}, openEventFrames={eventFrames.Count}, workerStateOmitted={workerCauses.Omitted}, duplicateDeathEvents={workerCauses.DuplicateDeaths}", final: true);
                    }
                    finally
                    {
                        active = false; current = null; eventFrames.Clear(); deleting.Clear(); parameters.Clear();
                        workerHooks?.SetActive(false);
                        workerCauses.Reset(0);
                        removals.Reset(0);
                        string finalBudget = $"AI_ECONOMY_BUDGET: session={session}, tick={tick}, bytesBeforeBudgetLine={budget.Bytes}, reservedBytes={EconomyLogBudget.ReservedBytes}, routineBytes={budget.RoutineBytes}, causeBytes={budget.CauseBytes}, finalBytes={budget.FinalBytes}, omitted={string.Join("|", budget.Omitted)}.";
                        if (budget.Accept("FINAL_BUDGET", finalBudget)) output.Enqueue(finalBudget);
                    }
                }
            });
        }
        internal void Drain()
        {
            for (int i = 0; i < 2048 && output.TryDequeue(out string line); i++)
                WriteLine(line);
        }
        internal void DrainOnProcessExit()
        {
            // End uses copied counters only; shutdown must never query unloaded game state.
            End();
            while (output.TryDequeue(out string line)) WriteLine(line);
        }
        private void WriteLine(string line)
        {
            var level = GetLogLevel(line);
            if (level == LogLevel.Error)
                Shared.DebugLogHelper.LogError(log, line);
            else if (level == LogLevel.Warning)
                log.LogWarning("[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + "] " + line);
            else Shared.DebugLogHelper.LogInfo(log, line);
        }
        internal static LogLevel GetLogLevel(string line)
        {
            if (line.StartsWith("AI_ECONOMY_CAPTURE_FAILED:", StringComparison.Ordinal)) return LogLevel.Error;
            if (!line.StartsWith("AI_ECONOMY_CAPABILITY:", StringComparison.Ordinal) || !line.Contains("available=False")) return LogLevel.Info;
            return line.Contains(", status=unsupported, detail=") ? LogLevel.Warning : LogLevel.Error;
        }
        internal void Coverage(string feature, bool available, string detail, bool expectedUnavailable = false)
        {
            lock (sync) coverage[feature] = $"available={available}/{detail}";
            // Initialization diagnostics are independent of an active mission.
            output.Enqueue($"AI_ECONOMY_CAPABILITY: feature={feature}, available={available}, status={(available ? "available" : expectedUnavailable ? "unsupported" : "failed")}, detail={detail}.");
        }
        private bool Observe(int player) => active && player >= 1 && player <= 8 && GamePlayerManagerAPI.Instance.IsAIPlayer(player);
        private void Safe(Action action)
        {
            try { action(); }
            catch (Exception ex)
            {
                lock (sync)
                {
                    captureFailures++;
                    if ((captureFailures & (captureFailures - 1)) == 0)
                        output.Enqueue($"AI_ECONOMY_CAPTURE_FAILED: session={session}, tick={tick}, count={captureFailures}, error={ex.GetType().Name}:{ex.Message}.");
                }
            }
        }
        private void Count(string key)
        {
            if (!counters.ContainsKey(key) && counters.Count >= 8192)
            { Omit("counter-keys"); return; }
            counters[key] = counters.TryGetValue(key, out long count) ? count + 1 : 1;
        }
        private void Omit(string category) => budget.Omitted[category] =
            (budget.Omitted.TryGetValue(category, out int count) ? count : 0) + 1;
        internal static void ValidateLayout()
        {
            if (Marshal.SizeOf(typeof(GameBuilding)) != 0x32C ||
                Marshal.SizeOf(typeof(GamePlayerResources)) != 0x583C || Marshal.SizeOf(typeof(InternalAIC)) != 0x5E4 ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_GlobalId)).ToInt32() != 0xD8 ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.r_AliveState)).ToInt32() != 0xD0 ||
                Marshal.OffsetOf(typeof(GameBuilding), nameof(GameBuilding.N0000178A)).ToInt32() != 0x2A8 ||
                Marshal.OffsetOf(typeof(GamePlayerResources), nameof(GamePlayerResources.r_TotalGoodsGold)).ToInt32() != 0x50C ||
                Marshal.OffsetOf(typeof(GamePlayerResources), nameof(GamePlayerResources.r_TotalGoodsNull)).ToInt32() != 0x4D0)
                throw new InvalidOperationException("Economy observation interop layout differs from the audited native image");
        }
        private void Emit(string kind, int player, long operation, string detail, bool final = false)
        {
            string line = $"AI_ECONOMY_{kind}: observedAt={DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}, session={session}, tick={tick}, player={player}, operation={operation}, {detail}.";
            if (budget.Accept(final ? "FINAL_" + kind : kind, line)) output.Enqueue(line);
            else if (!overflowReported)
            {
                overflowReported = true;
                string warning = $"AI_ECONOMY_OVERFLOW: session={session}, tick={tick}, reservedBytes={EconomyLogBudget.ReservedBytes}, firstOmittedCategory={kind}; independentRoutineCauseFinalBudgets=true; subsequentDetailsIncomplete=true.";
                if (budget.Accept("FINAL_OVERFLOW", warning)) output.Enqueue(warning);
            }
        }
        private void Change(string key, string kind, int player, long op, string detail)
        {
            if (!last.ContainsKey(key) && last.Count >= 8192)
            { Omit("state-keys"); Emit(kind, player, op, detail); return; }
            repeats[key] = repeats.TryGetValue(key, out long n) ? n + 1 : 1;
            if (last.TryGetValue(key, out string old) && old == detail) return;
            last[key] = detail;
            Emit(kind, player, op, detail);
        }
        internal EconomyOperation SafeBegin(string kind, int player, int a, int b, bool countEntry = true)
        {
            EconomyOperation op = null;
            Safe(() =>
            {
                lock (sync)
                {
                    if (!Observe(player)) return;
                    op = new EconomyOperation(++sequence, session, kind, player, Gold(player), Current);
                    op.ArgumentA = a; op.ArgumentB = b;
                    parameters[op.Id] = $"a={a}, b={b}";
                    current = op;
                    if (countEntry) Count(player + ":" + kind + ":entered");
                    if (kind == "wood-retirement" || kind == "wood-counter-retirement") Safe(() => CaptureRemovalInputs(op));
                    if (kind == "ai-buy" || kind == "ai-sell")
                    {
                        op.StockBefore = Stock(player, a);
                        Emit("TRANSACTION_BEGIN", player, op.Id, $"kind={kind}, good={(eGoods)a}, requestedAmount={b}, goldBefore={op.GoldBefore}, stockBefore={Stock(player, a)}");
                    }
                    if (kind == "wood-eligibility" && nativeLayout) op.RawWoodPrecondition = Read(0x3505470);
                    if (kind == "build-resources")
                        Change(player + ":cost:" + a, "COSTS", player, op.Id, Costs(player, (eMappers)a));
                    if (kind == "purchase-decision" || kind == "build-resources" || kind.EndsWith("eligibility", StringComparison.Ordinal))
                    {
                        op.DecisionInputs = NativeState(player);
                        Change(player + ":" + kind + ":state", "DECISION_INPUT", player, op.Id, $"kind={kind}, {op.DecisionInputs}");
                    }
                }
            });
            return op;
        }
        internal EconomyOperation[] BeginSettlement()
        {
            if (!active) return null;
            var operations = new EconomyOperation[8];
            for (int player = 1; player <= 8; player++) operations[player - 1] = SafeBegin("tax-settlement", player, 0, 0);
            return operations;
        }
        internal void EndSettlement(EconomyOperation[] operations)
        {
            if (operations == null) return;
            for (int i = operations.Length - 1; i >= 0; i--) SafeEnd(operations[i], null);
        }
        internal void SafeEnd(EconomyOperation op, int? result)
        {
            if (op == null) return;
            // Context restoration must also happen when capture fails or a session was replaced.
            try
            {
                if (op.Kind == "wood-retirement" || op.Kind == "wood-counter-retirement") Safe(() => FinishRemovalDecision(op, result));
                Safe(() =>
                {
                    lock (sync)
                    {
                        if (!active || op.Session != session) return;
                        int after = Gold(op.Player); long exclusive = op.ExclusiveDelta(after);
                        bool attributed = EconomyDecisionEvidence.HasMeasuredBookingContext(op.Kind);
                        if (attributed) ledger.Book(op.Player, exclusive);
                        // Unknown children are accounted as unknown; never reassigned to a broad parent.
                        op.Close(after);
                        Count(op.Player + ":" + op.Kind + ":result:" + (result?.ToString() ?? "void"));
                        RememberBuildDecision(op, result);
                        if (!EconomyDecisionEvidence.EmitGenericResult(op.Kind))
                        { parameters.Remove(op.Id); return; } // Dedicated counter record already owns the decision; no duplicate formatting.
                        string detail = $"kind={op.Kind}, result={result?.ToString() ?? "unavailable"}, " +
                            $"goldBefore={op.GoldBefore}, goldAfter={after}, actualDelta={(long)after - op.GoldBefore}, exclusiveDelta={exclusive}, " +
                            $"bookingAttributed={attributed}, {parameters[op.Id]}, nativeOriginalEvaluated={op.NativeOriginalEvaluated?.ToString() ?? "see-native-context"}, reason={(op.NativeOriginalEvaluated == false ? "owner-override; native-predicate-not-evaluated" : EconomyDecisionEvidence.Reason(op.Kind, result, op.RawWoodPrecondition))}, nativeReturnIsNotSpawn=true";
                        if (op.Kind == "tax-settlement") detail += ", measuredNetIncludesZeroFloor=true; not-a-gross-tax-income-amount";
                        if (op.Kind == "ai-buy" || op.Kind == "ai-sell")
                            Emit("TRANSACTION_END", op.Player, op.Id, detail + $", good={(eGoods)op.ArgumentA}, requestedAmount={op.ArgumentB}, " +
                                $"stockBefore={op.StockBefore}, stockAfter={Stock(op.Player, op.ArgumentA)}, outcome={EconomyDecisionEvidence.TradeOutcome(op.Kind, result, op.StockBefore, Stock(op.Player, op.ArgumentA))}");
                        else
                            Change(EconomyDecisionEvidence.DecisionKey(op), "DECISION_RESULT", op.Player, op.Id, detail);
                        parameters.Remove(op.Id);
                    }
                });
            }
            finally { if (current == op) current = op.Session == session ? op.Parent?.ForSession(session) : null; }
        }
        private static int Gold(int player)
        {
            if (!GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(player, out GamePlayerResources* r) || r == null)
                throw new InvalidOperationException("player resources unavailable: " + player);
            return r->r_TotalGoodsGold;
        }
        private static int Stock(int player, int good) => good >= 0 && good < (int)eGoods.Count && Enum.IsDefined(typeof(eGoods), (eGoods)good)
            ? GamePlayerManagerAPI.Instance.GetGoodAmount(player, (eGoods)good) : int.MinValue;
        private string NativeState(int player)
        {
            if (!nativeLayout) return "nativeOperands=unavailable; unknown-native-hash";
            long p = (long)player * 0x583C;
            var s = new StringBuilder();
            // Exact native operand RVAs; unconfirmed meanings remain numeric.
            foreach (int rva in new[] { 0x379D0D0, 0x379D838, 0x379D83C, 0x379E630, 0x379E6BC, 0x379E6C0, 0x379E744, 0x379CF50, 0x379D8EC, 0x379D8F0 })
                s.Append("raw_").Append(rva.ToString("X")).Append('=').Append(Read(rva + p)).Append(';');
            s.Append("raw_3505470=").Append(Read(0x3505470)).Append(";demand=");
            for (int i = 0; i < 25; i++) s.Append(i).Append(':').Append(Read(0x379D840 + p + i * 4L)).Append('/');
            return s.ToString();
        }
        private int Read(long offset)
        {
            if (!nativeLayout || offset < 0 || offset + 4 > moduleSize) throw new InvalidOperationException("native field outside image");
            return Marshal.ReadInt32(new IntPtr(checked(module + offset)));
        }
        private string Costs(int player, eMappers mapper)
        {
            eStructs type = mapper.ConvertToEStructs();
            var api = GameBuildingManagerAPI.Instance;
            return $"mapper={mapper}, structure={type}, requiredWood={api.GetWoodCost(type)}, requiredStone={api.GetStoneCost(type)}, " +
                $"requiredIron={api.GetIronIngotCost(type)}, requiredPitch={api.GetRawPitchCost(type)}, requiredGold={api.GetGoldCost(type)}, " +
                $"availableWood={Stock(player, (int)eGoods.STORED_WOOD_PLANKS)}, availableStone={Stock(player, (int)eGoods.STORED_STONE_BLOCKS)}, " +
                $"availableIron={Stock(player, (int)eGoods.STORED_IRON_INGOTS)}, availablePitch={Stock(player, (int)eGoods.STORED_PITCH_RAW)}, availableGold={Gold(player)}, " +
                "costsAreEvidenceNotEligibilityResult=true";
        }
        private void Snapshot(string phase)
        {
            var counts = new Dictionary<string, int>();
            var inventoryIds = new Dictionary<int, BuildingEvidenceIdentity>();
            Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
            if (buildings.Length == 0) throw new InvalidOperationException("building inventory unavailable; no removals inferred");
            for (int index = 0; index < buildings.Length; index++)
            {
                ref GameBuilding b = ref buildings[index];
                if (b.r_AliveState == AliveState.None || !Observe(b.r_PlayerIdOwner)) continue;
                inventoryIds[index + 1] = new BuildingEvidenceIdentity(index + 1, b.r_GlobalId, b.r_PlayerIdOwner,
                    (int)b.r_BuildingType, b.r_TilePositionXBegin, b.r_TilePositionYBegin, (int)b.r_AliveState, b.N0000178A);
                string key = b.r_PlayerIdOwner + ":" + b.r_BuildingType + ":state=" + b.r_AliveState + ":sleep=" + b.r_IsSleeping;
                counts[key] = counts.TryGetValue(key, out int n) ? n + 1 : 1;
            }
            foreach (var old in knownBuildings)
                if (!inventoryIds.TryGetValue(old.Key, out var found) || !old.Value.Matches(found.Slot, found.GlobalId))
                {
                    Emit("INVENTORY_REMOVAL", old.Value.Player, 0, $"{old.Value}, observedBetweenSnapshots=true, cause=unknown; protectionDecisionCoverage=unavailable");
                }
            knownBuildings.Clear();
            foreach (var pair in inventoryIds) knownBuildings.Add(pair.Key, pair.Value);
            for (int player = 1; player <= 8; player++)
            {
                if (!Observe(player)) continue;
                GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(player, out GamePlayerResources* r);
                if (r == null) continue;
                Change(player + ":population", "PLAYER", player, 0,
                    $"phase={phase}, loadedSave={loadedSave}, lord={GamePlayerManagerAPI.Instance.GetAILord(player)}, population={r->r_TotalPopulation}, " +
                    $"readyPeasants={r->r_ReadyPeasants}, totalPeasants={r->r_TotalPeasants}, housing={r->r_CivilianHousingSpace}, " +
                    $"popularityRaw={r->r_CurrentPopularity}, taxes={r->r_TaxesMode}, marketId={r->r_TradePostId}, " +
                    $"granaryId={r->r_FirstGranaryId}, goodsyardId={r->r_FirstGoodsyardId}; historicalEvents=unknown");
                Change(player + ":fixes", "FIXES_PURCHASE_STATE", player, 0, FixesEconomySnapshot.Capture(player));
                Emit("BALANCE", player, 0, $"phase={phase}, {ledger.Reconcile(player, r->r_TotalGoodsGold)}");
                var goods = new StringBuilder();
                foreach (eGoods good in Enum.GetValues(typeof(eGoods)))
                    if ((int)good >= 0 && good < eGoods.Count) goods.Append(good).Append('=').Append(Stock(player, (int)good)).Append(';');
                Change(player + ":goods", "STOCK", player, 0, goods.ToString());
                var inventory = new StringBuilder();
                foreach (var pair in counts) if (pair.Key.StartsWith(player + ":", StringComparison.Ordinal)) inventory.Append(pair.Key).Append('=').Append(pair.Value).Append(';');
                Change(player + ":buildings", "BUILDINGS", player, 0, "inventory=" + inventory);
                Profile(player);
            }
        }
        private void Profile(int player)
        {
            if (!nativeLayout) { Change(player + ":aic", "AIC", player, 0, "effectiveProfile=unavailable; unknown-native-hash"); return; }
            int profile = Read(0x379D0D0 + player * 0x583CL);
            long offset = checked(0x404C950L + (profile - 1L) * 0x5E4);
            if (profile <= 0 || offset < 0 || offset + Marshal.SizeOf(typeof(InternalAIC)) > moduleSize)
            { Change(player + ":aic", "AIC", player, 0, "profileIndex=" + profile + ", data=unavailable; invalid-image-range"); return; }
            object config = Marshal.PtrToStructure(new IntPtr(module + offset), typeof(InternalAIC));
            var lord = GamePlayerManagerAPI.Instance.GetAILord(player);
            string customName = GameAIManagerAPI.Instance.IsCustomLord(lord)
                ? GameAIManagerAPI.Instance.GetCustomAILordNameByPlayerId(player) : lord.ToString();
            var text = new StringBuilder("profileIndex=" + profile + ", lordId=" + lord + ", customName=" + customName + ", values=");
            foreach (FieldInfo f in aicFields)
                if (Marshal.OffsetOf(typeof(InternalAIC), f.Name).ToInt32() <= 0x110)
                    text.Append(f.Name).Append('=').Append(f.GetValue(config)).Append(';');
            Change(player + ":aic", "AIC", player, 0, text.ToString());
        }
        private void Summary(string phase)
        {
            bool final = phase == "end";
            if (!final)
            {
                Change("protection-settings", "SETTINGS", 0, 0, ProtectionSnapshot.Capture());
                Snapshot(phase);
            }
            for (int player = 1; player <= 8; player++)
            {
                if (!final && !Observe(player)) continue;
                var totals = new StringBuilder();
                int omittedKeys = 0;
                foreach (string stage in SummaryStages)
                {
                    string key = player + ":" + stage + ":entered";
                    totals.Append(stage).Append("Calls=").Append(counters.TryGetValue(key, out long n) ? n : 0).Append(';');
                }
                // Prioritize the cause totals so routine keys cannot push them out of the final record.
                foreach (bool important in new[] { true, false })
                    foreach (var pair in counters)
                        if (pair.Key.StartsWith(player + ":", StringComparison.Ordinal) &&
                            (pair.Key.Contains("worker-") || pair.Key.Contains("removal") || pair.Key.Contains("rebuild")) == important)
                        {
                            string entry = pair.Key + "=" + pair.Value + ";";
                            if (totals.Length + entry.Length > 4096) omittedKeys++; else totals.Append(entry);
                        }
                Emit("SUMMARY", player, 0, $"phase={phase}, counters={totals}, summaryCounterKeysOmitted={omittedKeys}, omitted={(final ? "see-final-budget" : string.Join("|", budget.Omitted))}, removalStateOmitted={removals.Omitted}, workerStateOmitted={workerCauses.Omitted}, pendingRemovalMarks={removals.PendingCount}; zeroCallsOnlyMeaningfulWithAvailableHook=true; searchAndSpawnCounters=AI_BUILD_SUMMARY; unknownCausesRemainUnknown=true; captureFailures={captureFailures}; observerCallbackFailures={availability?.CallbackFailures ?? 0}; priceCallbackFailures={MarketPriceEvents.CallbackFailures}", final);
            }
        }
        private void OnPrice(MarketPricePostEventArgs args)
        {
            lock (sync)
            {
                if (!Observe(args.PlayerId)) return;
                Count(args.PlayerId + ":price-query");
                string detail = $"direction={args.Direction}, good={args.Good}, amount={args.Amount}, total={args.Result}, replaced={args.WasSkipped}, context={Current?.Kind ?? "unknown"}, transactionConfirmed=false";
                Change(args.PlayerId + ":price:" + args.Good + ":" + args.Direction, "PRICE_QUERY", args.PlayerId, Current?.Id ?? 0, detail);
                if (Current != null && Current.Kind == "purchase-decision")
                    Change(args.PlayerId + ":affordability:" + args.Good, "PURCHASE_CONDITION", args.PlayerId, Current.Id,
                        $"good={args.Good}, amount={args.Amount}, nativePrice={args.Result}, gold={Gold(args.PlayerId)}, comparisonGoldLessThanPrice={Gold(args.PlayerId) < args.Result}, comparisonComputedFromObservedInputs=true; nativeBranchOutcome=not-observed; transactionConfirmed=false");
            }
        }
        private static BuildingEvidenceIdentity Identity(int slot)
        {
            if (slot <= 0 || !GameBuildingManagerAPI.Instance.TryGetBuildingById(slot, out GameBuilding* b) || b == null) return null;
            return new BuildingEvidenceIdentity(slot, b->r_GlobalId, b->r_PlayerIdOwner, (int)b->r_BuildingType,
                b->r_TilePositionXBegin, b->r_TilePositionYBegin, (int)b->r_AliveState, b->N0000178A);
        }
        private void Event(string key, int player, EventHookPhase phase, string kind, int a = 0, int b = 0)
        {
            lock (sync)
            {
                if (!Observe(player)) return;
                if (phase == EventHookPhase.Pre)
                {
                    var op = SafeBegin(kind, player, a, b);
                    if (op == null) return;
                    if (!eventFrames.TryGetValue(key, out var stack)) eventFrames[key] = stack = new Stack<EconomyOperation>();
                    stack.Push(op);
                }
                else if (eventFrames.TryGetValue(key, out var stack) && stack.Count != 0)
                { var op = stack.Pop(); if (stack.Count == 0) eventFrames.Remove(key); SafeEnd(op, null); }
            }
        }
        private void Deletion(int slot, EventHookPhase phase)
        {
            lock (sync)
            {
                if (!active) return;
                if (phase == EventHookPhase.Pre)
                {
                    var id = Identity(slot); if (id == null || !Observe(id.Player)) return;
                    deleting[slot] = id;
                    string cause = Current?.Kind == "emergency-demolition" || Current?.Kind == "wood-retirement" || Current?.Kind == "hovel-reduction" || Current?.Kind == "bulldoze" || Current?.Kind == "damage" ? Current.Kind : "unknown";
                    delayedDeletion[id.GlobalId] = new PendingDeletion { Identity = id, Cause = cause, Operation = Current?.Id ?? 0 };
                    Emit("DELETE_BEGIN", id.Player, Current?.Id ?? 0, $"{id}, cause={cause}; protectionDecisionCoverage=unavailable");
                }
                else if (deleting.TryGetValue(slot, out var id))
                {
                    var after = Identity(slot);
                    bool removed = BuildingEvidenceIdentity.IsRemoved(id, after);
                    Emit("DELETE_END", id.Player, Current?.Id ?? 0, $"{id}, removalObserved={removed}, after={(after == null ? "unavailable" : after.ToString())}; postReturnIsNotSuccess=true");
                    Count(id.Player + ":removed:" + id.Type + ":" + (Current?.Kind ?? "unknown") + ":" + removed);
                    deleting.Remove(slot);
                    if (removed) ConfirmRemoval(delayedDeletion[id.GlobalId]);
                }
            }
        }
        private void ConfirmRemoval(PendingDeletion record)
        {
            var id = record.Identity;
            ConfirmMarkedRemoval(id, Identity(id.Slot));
            Emit("REMOVAL_CONFIRMED", id.Player, record.Operation, $"{id}, deletionCallContext={record.Cause}, " +
                "callContextIsNotUniversalCauseProof=true");
            Count(id.Player + ":confirmed-removal:" + id.Type + ":" + record.Cause);
            delayedDeletion.Remove(id.GlobalId);
            if (knownBuildings.TryGetValue(id.Slot, out var old) && old.GlobalId == id.GlobalId) knownBuildings.Remove(id.Slot);
        }
        private void MonitorDeletions()
        {
            if (removals.PendingCount != 0)
                foreach (var mark in removals.PendingSnapshot())
                    ConfirmMarkedRemoval(mark.Identity, Identity(mark.Identity.Slot));
            foreach (var pending in new List<PendingDeletion>(delayedDeletion.Values))
            {
                var now = Identity(pending.Identity.Slot);
                if (BuildingEvidenceIdentity.IsRemoved(pending.Identity, now))
                    ConfirmRemoval(pending);
            }
        }
        private void BuildingEvent(string key, int slot, EventHookPhase phase, string kind)
        {
            lock (sync)
            {
                // The deleted object's owner may already be zero in Post.
                if (phase == EventHookPhase.Post && eventFrames.TryGetValue(key, out var stack) && stack.Count != 0)
                    Event(key, stack.Peek().Player, phase, kind);
                else
                {
                    var id = Identity(slot);
                    if (id != null) Event(key, id.Player, phase, kind);
                }
            }
        }
        private void HookEvents()
        {
            subscriptions.Add(BuildingR3EventHooks.OnBuildingDelete.Observable.Subscribe(e => Safe(() => Deletion(e.BuildingId, e.Phase))));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingBulldoze.Observable.Subscribe(e => Safe(() =>
            { BuildingEvent("bulldoze:" + e.BuildingId, e.BuildingId, e.Phase, "bulldoze"); })));
            subscriptions.Add(BuildingR3EventHooks.OnBuildStructure.Observable.Subscribe(e => Safe(() => Event("build:" + e.PlayerId, e.PlayerId, e.Phase, "build", (int)e.Mappers))));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingRefund.Observable.Subscribe(e => Safe(() =>
            {
                lock (sync)
                {
                    if (e.Phase == EventHookPhase.Pre)
                    {
                        var context = RemovalContext();
                        var id = Identity(e.BuildingId);
                        if (context != null && id != null && context.Player == id.Player) context.RemovalTarget = id;
                    }
                    Event("refund:" + e.BuildingId, e.PlayerId, e.Phase, "refund", e.BuildingId, e.Percentage);
                }
            })));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingSpawn.Observable.Subscribe(e => Safe(() =>
            {
                if (e.Phase != EventHookPhase.Post || e.ReturnValue <= 0 || e.ReturnValue > int.MaxValue) return;
                lock (sync)
                {
                    var id = Identity((int)e.ReturnValue);
                    if (id == null || !Observe(id.Player)) return;
                    MonitorDeletions(); // Includes terminal slot reuse before correlating this new identity.
                    knownBuildings[id.Slot] = id;
                    ObserveRebuild(id);
                    Emit("SPAWN", id.Player, Current?.Id ?? 0, $"{id}, source={Current?.Kind ?? "unknown"}; spawnIsNotHistoricalRebuildProof=true");
                    Count(id.Player + ":spawn:" + id.Type);
                }
            })));
            subscriptions.Add(BuildingR3EventHooks.OnTogglePause.Observable.Subscribe(e => Safe(() =>
            { var id = Identity(e.BuildingId); if (id != null && Observe(id.Player)) Emit("PAUSE", id.Player, Current?.Id ?? 0, $"phase={e.Phase}, {id}, wasSleeping={e.WasPreviouslySleeping}"); })));
            subscriptions.Add(BuildingR3EventHooks.OnBuildingTileTakeDamage.Observable.Subscribe(e => Safe(() =>
            {
                if (!active) return;
                Span<ushort> structures = GameTileManagerAPI.Instance.GetStructureLayer();
                if ((uint)e.TileId >= (uint)structures.Length) return;
                BuildingEvent("damage:" + e.TileId, structures[e.TileId], e.Phase, "damage");
                lock (sync) Count("damage-events:source=" + e.PlayerIdSource);
            })));
            subscriptions.Add(PlayerR3EventHooks.OnPlayerAddResource.Observable.Subscribe(e => Safe(() =>
            { lock (sync) if (Observe(e.PlayerId)) Count(e.PlayerId + ":resource-add:" + e.Good + ":" + e.Phase + ":amount=" + e.Amount); })));
            subscriptions.Add(PlayerR3EventHooks.OnPlayerSubtractResource.Observable.Subscribe(e => Safe(() =>
            { lock (sync) if (Observe(e.PlayerId)) Count(e.PlayerId + ":resource-subtract:" + e.Good + ":" + e.Phase + ":amount=" + e.Amount); })));
            subscriptions.Add(PlayerR3EventHooks.OnPlayerTaxIncome.Observable.Subscribe(e => Safe(() =>
            { lock (sync) if (Observe(e.PlayerId)) Emit("TAX_EVENT", e.PlayerId, Current?.ForPlayer(e.PlayerId)?.Id ?? 0, $"phase={e.Phase}, reportedAmount={e.Income}, goldAtEvent={Gold(e.PlayerId)}, bookingConfirmed=false; rawEventOnly=true"); })));
            subscriptions.Add(PlayerR3EventHooks.OnPlayerTaxExpenditure.Observable.Subscribe(e => Safe(() =>
            { lock (sync) if (Observe(e.PlayerId)) Emit("DONATION_EVENT", e.PlayerId, Current?.ForPlayer(e.PlayerId)?.Id ?? 0, $"phase={e.Phase}, reportedAmount={e.Expenditure}, goldAtEvent={Gold(e.PlayerId)}, bookingConfirmed=false; rawEventOnly=true"); })));
            subscriptions.Add(PlayerR3EventHooks.OnPlayerMarketInteraction.Observable.Subscribe(e => Safe(() => Event("market:" + e.PlayerId, e.PlayerId, e.Phase, "market-action", (int)e.Good, e.Selling ? 1 : 0))));
        }
    }
}
