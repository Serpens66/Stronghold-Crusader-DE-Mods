#if SERP_DEVDIAG
using System;
using System.Collections.Generic;
using APIShared;
using APIShared.Economy;
using APIShared.Multiplayer;
using APIShared.Savegames;
using APIShared.Simulation;
using SHCDESE.API;
using SHCDESE.Interop;

namespace AIMarketOverbuyDiagnostic
{
    internal static unsafe partial class MarketPendingDemandFix
    {
        private static AiMarketRegistration marketRegistration;
        private static VersionedSaveDataHandle saveHandle;
        private static MissionContext attempt;
        private static long preparedSession, restoredInvocation;
        private static MarketDemandSaveRow[] preparedRows;
        private static Exception preparedFailure;
        private static bool consumersReady, resyncRegistered;
        private static MarketDemandMissionState worldState;
        internal static bool FeatureReady => Ready && consumersReady && saveHandle != null && CorrectionFailures == 0 && AiMarketEvents.IsAvailable &&
            VersionedSaveData.IsAtomicWriteAvailable && NativeWorldRestoreEvents.IsAvailable;
        private static bool MultiplayerReady => FeatureReady && resyncRegistered && MultiplayerResyncEvents.IsAvailable && MultiplayerResyncEvents.CheckIntegrity();

        internal static void InitializeConsumers(BepInEx.Logging.ManualLogSource log)
        {
            if (consumersReady) return;
            if (!AiMarketEvents.TryRegister(Owner, "pending-demand", MarketBegin, MarketEnd,
                out marketRegistration, out string reason, -100, FailCorrection)) throw new InvalidOperationException(reason);
            marketRegistration.SetEnabled(true);
            var registration = new VersionedSaveDataRegistration(Owner, "AIMarketPendingDemand", 1, 1,
                new[] { "ledger" }, CaptureLedger, _ => { })
            { Prepared = PrepareLedger, SimulationRelevant = true, RequireAtomicWrite = true };
            if (!VersionedSaveData.TryRegister(registration, out saveHandle, out reason)) throw new InvalidOperationException(reason);
            if (!NativeWorldRestoreEvents.TryRegister(Owner, "market-ledger", RestoreLedger, out reason, -100)) throw new InvalidOperationException(reason);
            resyncRegistered = MultiplayerResyncEvents.TryRegister(Owner, "market-ledger", SuspendForRepair, ResumeAfterRepair, out reason, -100);
            if (!resyncRegistered) Shared.DebugLogHelper.LogError(log, "AI_MARKET_MP_UNAVAILABLE: " + reason);
            SimulationAgreement.Register(Owner, "pending-demand-v1", true, () => MultiplayerReady, SetAgreement);
            if (!ApiShared.ForMod(Owner).TryGetMissionLifecycle(out var missions, out var diagnostic) ||
                !missions.TryRegisterObserver("market-demand", OnDemandStarted, note => EndDemandAttempt(note.Context.SessionId), OnDemandInitialization, out diagnostic))
                throw new InvalidOperationException(diagnostic?.Reason ?? "Market demand lifecycle unavailable.");
            consumersReady = true;
            lock (gate) PublishWorldGate();
        }

        private static long MarketBegin(AiMarketContext context)
        {
            if (context.Operation != AiMarketOperation.PurchaseDecision) return 0;
            int tick = SimulationTickEvents.TryGetCurrentTick(out int current) ? current : 0;
            BeforeDecision(context.PlayerId, tick);
            EnterDecision(context.PlayerId);
            return 1;
        }
        private static void MarketEnd(AiMarketContext context, long token, bool completed, int rawResult)
        {
            if (token == 0) return;
            LeaveDecision(context.PlayerId); AfterDecision(context.PlayerId);
        }
        private static void SetAgreement(bool enabled)
        { lock (gate) { worldState.Agreed = enabled; PublishWorldGate(); } }
        private static void PublishWorldGate()
        {
            bool allowed = worldState.CanObserve && FeatureReady && !MultiplayerResyncEvents.IsInProgress;
            sessionActive = allowed ? 1 : 0;
            if (writerGate != IntPtr.Zero) *(int*)writerGate = sessionActive;
            if (receiptGate != IntPtr.Zero) *(int*)receiptGate = allowed ? corrective : 0;
        }
        private static void OnDemandInitialization(MissionLifecycleNotification note)
        {
            if (!note.IsBeforeInitialization) return;
            lock (gate)
            {
                BeginDemandAttempt(note.Context);
                worldState.NativeArmed = note.Phase == MissionInitializationPhase.BeforeNativeStart;
                PublishWorldGate();
            }
        }
        private static void OnDemandStarted(MissionLifecycleNotification note)
        { lock (gate) { BeginDemandAttempt(note.Context); worldState.NativeArmed = true; PublishWorldGate(); } }
        private static void BeginDemandAttempt(MissionContext context)
        {
            lock (gate)
            {
                bool changed = worldState.Begin(context.SessionId, context.IsEditor, context.Mode.IsRealMultiplayer);
                if (!changed) { attempt = context; PublishWorldGate(); return; }
                End(); attempt = context;
                preparedSession = restoredInvocation = 0; preparedRows = null; preparedFailure = null;
                if (!context.IsEditor) Reset(context.SessionId);
                PublishWorldGate();
            }
        }
        private static void EndDemandAttempt(long id)
        { lock (gate) { if (attempt?.SessionId != id) return; End(); worldState.End(); attempt = null; preparedSession = 0; preparedRows = null; preparedFailure = null; } }
        private static void SuspendForRepair()
        { lock (gate) { worldState.InRepair = true; Array.Clear(requests, 0, requests.Length); PublishWorldGate(); } }
        private static void ResumeAfterRepair()
        { lock (gate) { Array.Clear(requests, 0, requests.Length); Array.Clear(decisionDepth, 0, decisionDepth.Length); worldState.InRepair = false; PublishWorldGate(); } }

        private static bool CaptureLedger(VersionedSaveWriter writer)
        {
            lock (gate)
            {
                if (worldState.InRepair || MultiplayerResyncEvents.IsInProgress) throw new InvalidOperationException("Cannot save market ledger during repair.");
                for (int player = 1; player <= Players; player++)
                    if (decisionDepth[player] != 0) throw new InvalidOperationException("Cannot save market ledger inside an unfinished native purchase decision.");
                var rows = new List<MarketDemandSaveRow>(MarketDemandSaveCodec.MaximumRows);
                for (int player = 1; player <= Players; player++)
                    for (int good = 1; good < Goods; good++)
                    {
                        Request r = requests[player * Goods + good];
                        if (!r.Target.Active) continue;
                        if (!ValidateRestoredRequest(player, good, r))
                        {
                            // Policy/ownership can legitimately end before the next market call
                            // (for example LastStand1->0). Omit that detached snapshot row;
                            // capture must not change live authority, even if saving later fails.
                            continue;
                        }
                        rows.Add(new MarketDemandSaveRow { Player = player, Good = good, SourceRva = r.SourceRva,
                            PolicySourceRva = r.PolicySourceRva, SourceStock = r.SourceStock, GoalAmount = r.GoalAmount, Target = r.Target, UnitId = r.AmmoOrigin.UnitId,
                            UnitOwner = r.AmmoOrigin.Owner, UnitGlobalId = r.AmmoOrigin.GlobalId, UnitCreationTick = r.AmmoOrigin.CreationTick });
                    }
                writer.Write("ledger", MarketDemandSaveCodec.Encode(rows.ToArray())); return true;
            }
        }
        private static void PrepareLedger(VersionedSaveLoad load)
        {
            MarketDemandSaveRow[] rows = null; Exception error = null;
            try
            {
                if (load.Status == VersionedSaveLoadStatus.Restored)
                {
                    if (!load.Entries.TryGetValue("ledger", out byte[] bytes)) throw new InvalidOperationException("Missing market ledger entry.");
                    rows = MarketDemandSaveCodec.Decode(bytes);
                }
                else if (load.Status == VersionedSaveLoadStatus.NoRecord || load.Status == VersionedSaveLoadStatus.OutOfScope)
                    rows = Array.Empty<MarketDemandSaveRow>();
                else throw new InvalidOperationException("Market ledger cannot be restored: " + load.Status);
            }
            catch (Exception failure) { error = failure; }
            lock (gate)
            {
                preparedSession = attempt?.SessionId ?? 0; preparedRows = rows; preparedFailure = error;
            }
        }
        private static bool RestoreLedger(NativeWorldRestoreContext context)
        {
            try
            {
                bool accepted = RestoreLedgerCore(context);
                if (!accepted) SimulationAgreement.ReportFault("AI market ledger cannot validate the restored native world.");
                return accepted;
            }
            catch (Exception failure)
            {
                SimulationAgreement.ReportFault("AI market ledger restore failed: " + failure.Message);
                throw; // The shared restore owner catches this and rejects initialization once.
            }
        }
        private static bool RestoreLedgerCore(NativeWorldRestoreContext context)
        {
            lock (gate)
            {
                if (attempt == null || attempt.IsEditor) return true; // No active consumer; never borrow previous-world candidates.
                if (context.Mission == null || attempt.SessionId != context.Mission.SessionId) return false;
                if (attempt.Mode.IsRealMultiplayer && !worldState.Agreed) return false;
                if (restoredInvocation == context.InvocationId) return true;
                if (preparedFailure != null) throw new InvalidOperationException("AI market ledger preparation failed.", preparedFailure);
                if (preparedSession != attempt.SessionId || preparedRows == null)
                {
                    if (attempt.IsSave) return false;
                    Array.Clear(requests, 0, requests.Length);
                    worldState.NativeArmed = true; PublishWorldGate(); return true;
                }
                var restored = new Request[requests.Length];
                foreach (var row in preparedRows)
                {
                    var request = new Request { Target = row.Target, SourceRva = row.SourceRva, PolicySourceRva = row.PolicySourceRva,
                        GoalSession = attempt.SessionId, SourceStock = row.SourceStock, GoalAmount = row.GoalAmount,
                        AmmoOrigin = new MarketAmmoDemandSource.Origin { UnitId = row.UnitId, Owner = row.UnitOwner,
                            GlobalId = row.UnitGlobalId, CreationTick = row.UnitCreationTick } };
                    if (!ValidateRestoredRequest(row.Player, row.Good, request)) return false;
                    restored[row.Player * Goods + row.Good] = request;
                }
                Array.Copy(restored, requests, requests.Length);
                restoredInvocation = context.InvocationId; worldState.NativeArmed = true; PublishWorldGate(); return true;
            }
        }
        private static bool ValidateRestoredRequest(int player, int good, Request request)
        {
            if (!ValidCell(player, good) || !GamePlayerManagerAPI.Instance.IsAIPlayer(player) ||
                !GamePlayerManagerAPI.Instance.TryGetPlayerResourcesById(player, out GamePlayerResources* resources) || resources == null ||
                !DescribeSource(request.SourceRva, good, out bool material, out MarketDemandSourceFamily family, out int padding) ||
                (material ? MarketDemandPolicy.MaterialStockGoal : family == MarketDemandSourceFamily.Ammo ? MarketDemandPolicy.AutoAmmo : MarketDemandPolicy.FixedBatch) != request.Target.Policy ||
                !DescribeSource(request.PolicySourceRva, good, out bool policyMaterial, out MarketDemandSourceFamily policyFamily, out _) ||
                policyMaterial != material || material && request.Target.MaterialPadding != padding || !material && (family != request.Target.Family || policyFamily != family) ||
                ((int*)&resources->r_AIPendingMarketPurchaseAmountNull)[good] != request.Target.ExpectedPending ||
                ((int*)&resources->r_TotalGoodsNull)[good] != request.Target.LastObservedStock ||
                unchecked((int)resources->r_AILordMinusOne) != request.Target.AicId || !CorrectionCovered(good) ||
                !TryPolicy(player, good, resources, request, out long policy) || policy != request.Target.PolicyToken) return false;
            return request.Target.Policy != MarketDemandPolicy.AutoAmmo ||
                request.AmmoOrigin.TryDesired(player, request.Target.LastObservedStock, out _) != MarketAmmoDemandSource.Result.Unknown;
        }
    }
}
#endif
