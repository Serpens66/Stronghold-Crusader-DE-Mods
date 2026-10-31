using System;
using System.Runtime.InteropServices;
using APIShared;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.Interop;

namespace AIBuildDiagnoseTest
{
    internal sealed unsafe partial class EconomyDiagnostics
    {
        private readonly WorkerCauseLedger workerCauses = new WorkerCauseLedger();
        private readonly string[] constructionContext = new string[9];
        private readonly int[] constructionTick = new int[9];
        private readonly bool[] workerDeathSourceSeen = new bool[2];
        private readonly string[] woodBuildDecisions = new string[9], resourceBuildDecisions = new string[9];
        private WorkerCauseNativeHooks workerHooks;
        private bool workerLayout;
        internal bool WorkerRootsMatch(ulong unitRoot, ulong buildingRoot) => nativeLayout &&
            unitRoot == unchecked((ulong)(module + 0x67E8400)) &&
            (buildingRoot == 0 || buildingRoot == unchecked((ulong)(module + 0x64CCBB0)));
        internal static void ValidateWorkerLayout()
        {
            if (Marshal.SizeOf(typeof(GameUnit)) != 0x490 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_AliveState)).ToInt32() != 0x88 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_GlobalId)).ToInt32() != 0x94 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_AIState)).ToInt32() != 0x2BC ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_LinkedProductionBuildingId)).ToInt32() != 0x334 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_LinkedProductionBuildingGlobalId)).ToInt32() != 0x364 ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_IsKilledByProjectile)).ToInt32() != 0x29C ||
                Marshal.OffsetOf(typeof(GameUnit), nameof(GameUnit.r_CurrentHealth)).ToInt32() != 0x3C4)
                throw new InvalidOperationException("Worker identity/state/assignment layout differs");
        }
        private void InstallWorkerCauses(CrusaderLibraryLoadContext context, string hash)
        {
            Safe(() =>
            {
                ValidateWorkerLayout(); workerLayout = true;
                subscriptions.Add(UnitR3EventHooks.OnUnitKilledByMelee.Observable.Subscribe(e => Safe(() =>
                {
                    if (e.Phase != EventHookPhase.Pre) return;
                    lock (sync)
                    {
                        if (!active) return;
                        ObserveWorkerEventSource(0, "melee");
                        var victim = CopyWorker(e.AttackedUnitId, true);
                        if (victim == null) return;
                        var attacker = CopyWorker(e.AttackingUnitId, false);
                        string diplomacy = "unavailable";
                        if (attacker != null && attacker.Player >= 1 && attacker.Player <= 8 && victim.Player >= 1 && victim.Player <= 8)
                            diplomacy = "samePlayer=" + (attacker.Player == victim.Player) + ", teamAttacker=" +
                                GamePlayerManagerAPI.Instance.GetPlayerTeam(attacker.Player) + ", teamVictim=" + GamePlayerManagerAPI.Instance.GetPlayerTeam(victim.Player);
                        RecordWorkerTrigger(victim, "killed-melee", $"attacker=[{attacker?.ToString() ?? "unavailable"}], diplomacy=[{diplomacy}]; eventIsPre=true", true);
                    }
                })));
                Coverage("worker-melee-events", true, "subscriber registered; owner execution unverified until WORKER_EVENT_SOURCE; no event writes");
            });
            Safe(() =>
            {
                if (!workerLayout) return;
                subscriptions.Add(UnitR3EventHooks.OnUnitKilledByProjectile.Observable.Subscribe(e => Safe(() =>
                {
                    if (e.Phase != EventHookPhase.Pre) return;
                    lock (sync)
                    {
                        if (!active) return;
                        ObserveWorkerEventSource(1, "projectile");
                        if (e.AttackedUnitId < 1 || e.AttackedUnitId > int.MaxValue)
                        { Emit("WORKER_EVENT_GAP", 0, 0, $"victimRaw={e.AttackedUnitId}, projectileRaw={e.ProjectileId}; victim-not-representable-as-unit-id"); return; }
                        var victim = CopyWorker((int)e.AttackedUnitId, true);
                        if (victim == null) return;
                        string source = "projectile-unavailable; attacker/diplomacy=unknown";
                        // Projectile IDs use the projectile manager's own index contract, not unit ID arithmetic.
                        if (e.ProjectileId >= 0 && e.ProjectileId <= int.MaxValue &&
                            GameProjectileManagerAPI.Instance.TryGetProjectileById((int)e.ProjectileId, out GameProjectile* p) && p != null)
                            source = $"projectile={e.ProjectileId}, projectileGlobalRaw={p->r_GlobalId}, sourcePlayer40Raw={p->r_PlayerSourceId}, sourcePlayer1CRaw={p->r_UnitPlayerSourceId}, sourceUnitSlot={p->r_SourceUnitId}; historicalSourceUnitIdentity/diplomacy=not-established";
                        RecordWorkerTrigger(victim, "killed-projectile", source, true);
                    }
                })));
                Coverage("worker-projectile-events", true, "subscriber registered; first WORKER_EVENT_SOURCE required; source slot is not historical identity proof");
            });
            Safe(() =>
            {
                if (!workerLayout || !removalLayout) return;
                workerHooks = new WorkerCauseNativeHooks(this);
                workerHooks.Install(context, hash);
                WoodSafetyBridge.SetCounterReady(workerHooks.CounterReady);
                workerHooks.SetActive(active);
            });
        }

        private void ObserveWorkerEventSource(int index, string source)
        {
            if (workerDeathSourceSeen[index]) return;
            workerDeathSourceSeen[index] = true;
            Emit("WORKER_EVENT_SOURCE", 0, 0, "source=" + source + "; first-callback=true; source-unit-is-not-necessarily-a-woodcutter");
        }

        private WorkerSnapshot CopyWorker(int slot, bool onlyWood, int? nativeBuilding = null)
        {
            if (!workerLayout || !UnitAccess.TryGetById(slot, out GameUnit* u, out _) || u == null) return null;
            if (onlyWood && u->r_UnitChimp != eChimps.CHIMP_TYPE_WOODCUTTER) return null;
            var building = Identity(nativeBuilding ?? u->r_LinkedProductionBuildingId);
            if (onlyWood && !Observe(u->r_ControllableForPlayerId) && (building == null || !Observe(building.Player))) return null;
            return new WorkerSnapshot(slot, u->r_GlobalId, u->r_ControllableForPlayerId, (int)u->r_UnitChimp,
                u->r_AIState, (int)u->r_AliveState, u->r_CurrentHealth, u->r_CurrentTilePositionX, u->r_CurrentTilePositionY,
                u->r_TargetTilePositionX, u->r_TargetTilePositionY, u->r_LinkedProductionBuildingId,
                u->r_LinkedProductionBuildingGlobalId, building, u->r_IsKilledByProjectile);
        }
        private void RecordWorkerTrigger(WorkerSnapshot worker, string kind, string source, bool death)
        {
            var record = workerCauses.Trigger(session, worker, tick, kind, source, death);
            if (record == null) return;
            int player = worker.Building?.Player ?? worker.Player;
            Count(player + ":worker-trigger:" + kind);
            Emit(death ? "WORKER_DEATH" : "WORKER_RETURN_FAILURE", player, 0, record.ToString());
        }
        internal void ObserveWorkerNative(bool returning, int unitId, int nativeBuilding)
        {
            Safe(() =>
            {
                lock (sync)
                {
                    if (!active || !workerLayout || !nativeLayout) return;
                    short? nativeAfter = null;
                    WoodCounterCorrection correction = default;
                    if (!returning)
                    {
                        // The optional fix is isolated from all later capture/logging failures.
                        Safe(() =>
                        {
                            nativeAfter = ReadWorkerCounter(nativeBuilding);
                            uint unitGlobal = unchecked((uint)Read(0x67E8400L + unitId * 0x490L + 0x6F0));
                            uint buildingGlobal = unchecked((uint)Read(0x64CCBB0L + nativeBuilding * 0x32CL + 0x134));
                            correction = WoodSafetyBridge.CorrectDeathCounter(new WoodCounterPoint(session, tick, unitId, unitGlobal, nativeBuilding, buildingGlobal, nativeAfter.Value));
                        });
                    }
                    var worker = CopyWorker(unitId, false, returning ? (int?)null : nativeBuilding);
                    if (worker == null)
                    { Emit("WORKER_EVENT_GAP", 0, 0, $"unitRaw={unitId}, nativeBuildingRaw={nativeBuilding}, returning={returning}; context-unresolved"); return; }
                    if (!Observe(worker.Player) && (worker.Building == null || !Observe(worker.Building.Player))) return;
                    if (returning)
                    {
                        // The audited shared store reaches value 110 only after the helper returned zero.
                        string kind = worker.Building == null ? "return-target-unresolved" :
                            worker.BuildingMatches ? "return-accessibility-effective-rejected" : "return-building-identity-mismatch";
                        RecordWorkerTrigger(worker, kind, "observed-branch=return-helper-zero; category-derived-from-audited-control-flow; original-accessibility-details=unknown", false);
                    }
                    else
                    {
                        short after = ReadWorkerCounter(nativeBuilding);
                        bool corrected = nativeAfter.HasValue && correction.Applied &&
                            correction.UnitGlobal == worker.GlobalId && correction.BuildingGlobal == worker.Building?.GlobalId &&
                            after == unchecked((short)(nativeAfter.Value - 10));
                        if (corrected || nativeAfter.HasValue && after != nativeAfter.Value)
                        {
                            Count((worker.Building?.Player ?? worker.Player) + ":worker-counter-correction:" + corrected);
                            Emit("WORKER_COUNTER_CORRECTION", worker.Building?.Player ?? worker.Player, 0,
                                $"{worker}, nativeBuildingSlot={nativeBuilding}, nativeAfter={nativeAfter}, remainingCounter={after}, nativeAdd=10, correctionConfirmed={corrected}, netAdd={(corrected ? "0" : "unknown")}, woodSafetyCallbackFailures={WoodSafetyBridge.CallbackFailures}; loadedHistoryUnchanged=true");
                            return; // A removed/unknown contribution is never counted as an effective +10.
                        }
                        var increment = workerCauses.Increment(session, worker, after);
                        if (increment == null) return;
                        int player = worker.Building?.Player ?? worker.Player;
                        Count(player + ":worker-increment:" + increment.Cause);
                        Emit("WORKER_COUNTER_INCREMENT", player, 0,
                            $"workerEvent={increment.Id}, {worker}, nativeBuildingSlot={nativeBuilding}, counterAfter={after}, nativeAdd=10, counterBeforeDerived={unchecked((short)(after - 10))}, cause={increment.Cause}, repeatedUnit={increment.RepeatedUnit}, trigger=[{increment.Trigger?.ToString() ?? "unobserved"}]; beforeIsDerivedNotSampled=true");
                    }
                }
            });
        }
        private short ReadWorkerCounter(int nativeBuilding)
        {
            long offset = 0x64CCBB0L + nativeBuilding * 0x32CL + 0x338;
            if (offset < 0 || offset + 2 > moduleSize) throw new InvalidOperationException("Native counter destination outside image: " + nativeBuilding);
            return Marshal.ReadInt16(new IntPtr(module + offset));
        }
        internal void RecordConstructionContext(int player, string detail)
        {
            lock (sync)
                if (active && player >= 1 && player <= 8) { constructionContext[player] = detail; constructionTick[player] = tick; }
        }
        private void RememberBuildDecision(EconomyOperation op, int? result)
        {
            if (op.Player < 1 || op.Player > 8 || op.Session != session) return;
            string[] destination = op.Kind == "wood-eligibility" ? woodBuildDecisions :
                op.Kind == "build-resources" ? resourceBuildDecisions : null;
            if (destination == null) return;
            destination[op.Player] = $"operation={op.Id}, observedTick={tick}, kind={op.Kind}, a={op.ArgumentA}, b={op.ArgumentB}, result={result?.ToString() ?? "unavailable"}, nativeOriginalEvaluated={op.NativeOriginalEvaluated?.ToString() ?? "see-native-context"}, inputs=[{op.DecisionInputs ?? "unavailable"}]";
        }
        private string CurrentConstruction(int player) =>
            "frame=[" + (constructionContext[player] != null && constructionTick[player] == tick
                ? constructionContext[player] : "unavailable-or-older-tick; see-AI_BUILD_GENERAL_OUTCOME") +
            "], lastWoodEligibility=[" + (woodBuildDecisions[player] ?? "unobserved") +
            "], lastResourceDecision=[" + (resourceBuildDecisions[player] ?? "unobserved") +
            "]; lastDecisionsAreNotProofOfCurrentCallAttribution=true";
    }
}
