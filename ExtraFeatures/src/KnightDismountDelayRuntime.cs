using CrusaderDE;
using MonoMod.RuntimeDetour;
using R3;
using SHCDESE.API;
using SHCDESE.API.Components.Timer;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ExtraFeatures
{
    internal sealed unsafe partial class KnightDismountRuntime
    {
        private const string MissingGoldSpeechFileName = "Units_Warning3.wav";
        private const float ProgressBarVerticalGap = 2f / 32f;

        private static readonly object PersistentInfrastructureLock = new object();
        private static readonly List<IDisposable> PersistentSubscriptions = new List<IDisposable>();
        private static KnightDismountRuntime activeRuntime;
        private static Hook persistentGameActionHook;
        private static Hook persistentSaveHook;
        private static EngineInterfaceGameActionDelegate persistentGameActionTrampoline;
        private static EngineInterfaceSaveDelegate persistentSaveTrampoline;
        private static Texture2D progressTexture;
        private static Sprite progressSprite;

        private readonly Dictionary<int, PendingKnightTransformation> pendingTransformations =
            new Dictionary<int, PendingKnightTransformation>();
        private readonly HashSet<int> pendingSelectionRequestIds = new HashSet<int>();
        private readonly HashSet<int> pendingSelectionTransferIds = new HashSet<int>();
        private int pendingSelectionTransferTicks;
        private readonly Dictionary<int, ProgressVisual> progressVisuals =
            new Dictionary<int, ProgressVisual>();
        private DeferredSaveRequest deferredSave;
        private long deferredSaveReadyAfterTick = long.MaxValue;

        private delegate int EngineInterfaceGameActionDelegate(
            Enums.GameActionCommand command,
            int structureId,
            int state,
            int value2);

        private delegate bool EngineInterfaceSaveDelegate(
            string path,
            int screenCentreX,
            int screenCentreY,
            int realScreenCentreX,
            int realScreenCentreY,
            bool lockMap,
            bool tempLockOnly,
            bool mapSave);

        private void InitializePersistentInfrastructure()
        {
            lock (PersistentInfrastructureLock)
            {
                if (persistentGameActionHook != null && persistentSaveHook != null)
                {
                    activeRuntime = this;
                    return;
                }

                MethodInfo gameActionMethod = typeof(EngineInterface).GetMethod(
                    nameof(EngineInterface.GameAction),
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(Enums.GameActionCommand), typeof(int), typeof(int), typeof(int) },
                    null);
                MethodInfo saveMethod = typeof(EngineInterface).GetMethod(
                    nameof(EngineInterface.SaveSaveGame),
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[]
                    {
                        typeof(string), typeof(int), typeof(int), typeof(int), typeof(int),
                        typeof(bool), typeof(bool), typeof(bool)
                    },
                    null);

                if (gameActionMethod == null)
                    throw new MissingMethodException(typeof(EngineInterface).FullName, nameof(EngineInterface.GameAction));
                if (saveMethod == null)
                    throw new MissingMethodException(typeof(EngineInterface).FullName, nameof(EngineInterface.SaveSaveGame));

                Hook gameActionCandidate = null;
                Hook saveCandidate = null;
                var subscriptionCandidates = new List<IDisposable>();
                bool tickSubscribed = false;
                try
                {
                    gameActionCandidate = new Hook(gameActionMethod, (EngineInterfaceGameActionDelegate)PersistentGameActionHook);
                    saveCandidate = new Hook(saveMethod, (EngineInterfaceSaveDelegate)PersistentSaveHook);
                    persistentGameActionTrampoline = gameActionCandidate.GenerateTrampoline<EngineInterfaceGameActionDelegate>();
                    persistentSaveTrampoline = saveCandidate.GenerateTrampoline<EngineInterfaceSaveDelegate>();

                    subscriptionCandidates.Add(UnitR3EventHooks.OnUnitMoveHere.Observable.Subscribe(PersistentUnitMoveHere));
                    subscriptionCandidates.Add(UnitR3EventHooks.OnUnitUnityVisualInterpolate.Observable.Subscribe(PersistentVisualInterpolate));
                    subscriptionCandidates.Add(UnitR3EventHooks.OnUnitUnityVisualRemove.Observable.Subscribe(PersistentVisualRemove));
                    subscriptionCandidates.Add(Shared.MissionEvents.Ended.Subscribe(PersistentMapUnload));
                    GameTimeManagerAPI.Instance.OnTick += PersistentGameTick;
                    tickSubscribed = true;

                    persistentGameActionHook = gameActionCandidate;
                    persistentSaveHook = saveCandidate;
                    PersistentSubscriptions.AddRange(subscriptionCandidates);
                    activeRuntime = this;
                    LogDebug("Knight transformation persistent Stop/Save/movement/visual infrastructure installed.");
                }
                catch
                {
                    // A candidate which was never published may be rolled back safely.
                    if (tickSubscribed)
                        GameTimeManagerAPI.Instance.OnTick -= PersistentGameTick;
                    for (int index = subscriptionCandidates.Count - 1; index >= 0; index--)
                        subscriptionCandidates[index]?.Dispose();
                    saveCandidate?.Dispose();
                    gameActionCandidate?.Dispose();
                    persistentGameActionTrampoline = null;
                    persistentSaveTrampoline = null;
                    throw;
                }
            }
        }

        private static int PersistentGameActionHook(
            Enums.GameActionCommand command,
            int structureId,
            int state,
            int value2)
        {
            KnightDismountRuntime runtime = activeRuntime;
            if (command == Enums.GameActionCommand.Troops_Stop && runtime != null && runtime.IsPendingRuntimeActive())
                runtime.OnPlayerStopCommand();

            return persistentGameActionTrampoline(command, structureId, state, value2);
        }

        private static bool PersistentSaveHook(
            string path,
            int screenCentreX,
            int screenCentreY,
            int realScreenCentreX,
            int realScreenCentreY,
            bool lockMap,
            bool tempLockOnly,
            bool mapSave)
        {
            KnightDismountRuntime runtime = activeRuntime;
            if (runtime == null || !runtime.IsPendingRuntimeActive() || mapSave ||
                (runtime.pendingTransformations.Count == 0 && runtime.deferredSave == null))
            {
                return persistentSaveTrampoline(
                    path, screenCentreX, screenCentreY, realScreenCentreX, realScreenCentreY,
                    lockMap, tempLockOnly, mapSave);
            }

            return runtime.PreparePendingSave(
                path, screenCentreX, screenCentreY, realScreenCentreX, realScreenCentreY,
                lockMap, tempLockOnly, mapSave);
        }

        private static void PersistentGameTick(int tick)
        {
            KnightDismountRuntime runtime = activeRuntime;
            if (runtime != null && runtime.IsPendingRuntimeActive())
            {
                runtime.UpdatePendingTransformations();
                runtime.TryCompleteDeferredSave(tick);
            }
        }

        private static void PersistentUnitMoveHere(UnitMoveHereEventArgs args)
        {
            KnightDismountRuntime runtime = activeRuntime;
            if (runtime != null && runtime.IsPendingRuntimeActive() &&
                args != null && args.Phase == EventHookPhase.Pre && runtime.IsPendingUnitId(args.UnitId))
            {
                args.SkipOriginalFunction = true;
            }
        }

        private static void PersistentVisualInterpolate(UnitUnityVisualInterpolateEventArgs args)
        {
            KnightDismountRuntime runtime = activeRuntime;
            if (runtime != null && runtime.IsPendingRuntimeActive() && args?.Chimp != null)
                runtime.UpdateProgressVisual(args.Chimp);
        }

        private static void PersistentVisualRemove(UnitUnityVisualRemoveEventArgs args)
        {
            KnightDismountRuntime runtime = activeRuntime;
            if (runtime != null && args?.Chimp != null)
                runtime.RemoveProgressVisualForUnitId(args.Chimp.objectID);
        }

        private static void PersistentMapUnload(APIShared.MissionLifecycleNotification args)
        {
            KnightDismountRuntime runtime = activeRuntime;
            if (runtime == null || args == null || false)
                return;

            runtime.CancelAllPending("map-unload", refundGold: true, releaseReservedHorse: true);
            runtime.pendingSelectionRequestIds.Clear();
            runtime.pendingSelectionTransferIds.Clear();
            runtime.pendingSelectionTransferTicks = 0;
            runtime.ClearAllProgressVisuals();
            runtime.deferredSave = null;
            runtime.deferredSaveReadyAfterTick = long.MaxValue;
        }

        private bool IsPendingRuntimeActive()
        {
            return initialized && !disposed && Shared.GameplayModActivationGate.IsAllowed;
        }

        private void IssueInternalStop()
        {
            EngineInterfaceGameActionDelegate original = persistentGameActionTrampoline;
            if (original != null)
                original(Enums.GameActionCommand.Troops_Stop, 0, 0, 0);
            else
                EngineInterface.GameAction(Enums.GameActionCommand.Troops_Stop, 0, 0, 0);
        }

        private void StartPendingBatch(
            int playerId,
            int action,
            List<UnitTransformSnapshot> snapshots,
            bool localAction,
            string reason)
        {
            if (snapshots == null || snapshots.Count == 0 ||
                (action != MountAction && action != DismountAction))
            {
                return;
            }

            int goldCost = Math.Max(0, Math.Min(1000, settings.KnightTransformationGoldCost));
            int delaySeconds = Math.Max(0, Math.Min(120, settings.KnightTransformationDelaySeconds));
            var ordered = new List<UnitTransformSnapshot>(snapshots);
            ordered.Sort((left, right) => left.GlobalId.CompareTo(right.GlobalId));

            int affordableCount = goldCost == 0
                ? ordered.Count
                : Math.Min(ordered.Count, Math.Max(0, GamePlayerManagerAPI.Instance.GetPlayerGold(playerId) / goldCost));
            List<HorseAllocation> allocations = action == MountAction
                ? FindHorseAllocations(playerId, affordableCount)
                : null;
            int startLimit = action == MountAction ? Math.Min(affordableCount, allocations.Count) : affordableCount;
            int started = 0;
            eChimps targetType = action == MountAction
                ? eChimps.CHIMP_TYPE_KNIGHT
                : eChimps.CHIMP_TYPE_SWORDSMAN;

            for (int index = 0; index < ordered.Count && started < startLimit; index++)
            {
                UnitTransformSnapshot snapshot = ordered[index];
                if (snapshot.GlobalId <= 0 || pendingTransformations.ContainsKey(snapshot.GlobalId))
                    continue;

                eChimps expectedType = action == MountAction
                    ? eChimps.CHIMP_TYPE_SWORDSMAN
                    : eChimps.CHIMP_TYPE_KNIGHT;
                if (!TryResolveAliveUnitByGlobalId(snapshot, expectedType, out int currentUnitId) ||
                    !GameUnitManagerAPI.Instance.TryGetUnitById(currentUnitId, out GameUnit* currentUnit))
                {
                    continue;
                }

                if (!unitLimitBridge.TryReserveOne(
                        playerId,
                        targetType,
                        out long limitReservationId,
                        out _,
                        out _))
                {
                    break;
                }

                HorseAllocation allocation = default;
                bool horseReserved = false;
                bool pendingAdded = false;
                bool goldRemoved = false;

                try
                {
                    if (goldCost > 0 &&
                        !GamePlayerManagerAPI.Instance.HasGoodsAmount(playerId, eGoods.STORED_GOLD, goldCost))
                    {
                        break;
                    }

                    if (goldCost > 0)
                    {
                        GamePlayerManagerAPI.Instance.RemoveGood(playerId, eGoods.STORED_GOLD, goldCost);
                        goldRemoved = true;
                    }

                    if (action == MountAction)
                    {
                        allocation = allocations[started];
                        if (!TryConsumeStableHorse(allocation, currentUnitId, snapshot.GlobalId, reason + "-reserve"))
                            continue;

                        horseReserved = true;
                    }

                    snapshot = CreateSnapshotFromUnit(currentUnitId, currentUnit);
                    pendingTransformations.Add(snapshot.GlobalId, new PendingKnightTransformation
                    {
                        Snapshot = snapshot,
                        Action = action,
                        PlayerId = playerId,
                        StartTime = GameTimeManagerAPI.Instance.CaptureTimeStamp(),
                        DelayMilliseconds = delaySeconds * 1000,
                        PaidGold = goldCost,
                        LimitReservationId = limitReservationId,
                        HasHorseAllocation = action == MountAction,
                        Allocation = allocation
                    });
                    pendingAdded = true;
                    started++;
                }
                catch (Exception ex)
                {
                    LogError($"Knight transformation could not create its pending state and was rolled back: reason={reason}, globalId={snapshot.GlobalId}, error={ex.GetBaseException().Message}");
                }
                finally
                {
                    if (!pendingAdded)
                    {
                        unitLimitBridge.ReleaseReservation(limitReservationId);
                        if (horseReserved)
                            ReleaseExactHorseLink(allocation, currentUnitId, snapshot.GlobalId, reason + "-pending-add-rollback");
                        if (goldRemoved)
                            RefundGold(playerId, goldCost, reason + "-pending-add-rollback");
                    }
                }
            }

            if (localAction && started > 0)
            {
                PlayRandomLocalSpeech(action == MountAction ? MountSpeechFileNames : DismountSpeechFileNames,
                    action == MountAction ? "mount" : "dismount");
            }
            else if (localAction && started == 0 && goldCost > 0 && affordableCount == 0)
            {
                PlayMissingGoldSpeech();
            }

            if (localAction && action == MountAction && affordableCount > 0 && allocations.Count < affordableCount)
                PlayMissingWeaponsSpeech();

            if (delaySeconds == 0 && started > 0)
                UpdatePendingTransformations();

            LogDebug($"Knight transformation pending batch: reason={reason}, action={action}, requested={ordered.Count}, affordable={affordableCount}, started={started}, goldCost={goldCost}, delaySeconds={delaySeconds}.");
        }

        private void PlayMissingGoldSpeech()
        {
            try
            {
                SFXManager.instance?.playSpeech(1, MissingGoldSpeechFileName, 1f);
            }
            catch (Exception ex)
            {
                LogError($"Could not play knight transformation missing-gold speech: {ex}");
            }
        }

        private void UpdatePendingTransformations()
        {
            if (pendingSelectionTransferIds.Count == 0)
                PrunePendingSelectionRequest();
            else
                TryPublishReadySelectionTransfers();
            if (pendingTransformations.Count == 0)
                return;

            int[] globalIds = new int[pendingTransformations.Count];
            pendingTransformations.Keys.CopyTo(globalIds, 0);
            Array.Sort(globalIds);
            int readyCount = 0;
            int completedCount = 0;
            int cancelledCount = 0;
            var selectionTransfers = new List<int>();
            for (int index = 0; index < globalIds.Length; index++)
            {
                int globalId = globalIds[index];
                if (!pendingTransformations.TryGetValue(globalId, out PendingKnightTransformation pending))
                    continue;

                int currentUnitId = FindAliveUnitIdByGlobalId(globalId);
                if (currentUnitId <= 0)
                {
                    // Vanilla death/deletion owns the low-level link invalidation and stable recount.
                    CancelPending(pending, "unit-death-or-delete", refundGold: true, releaseReservedHorse: false);
                    cancelledCount++;
                    continue;
                }

                if (!GameUnitManagerAPI.Instance.TryGetUnitById(currentUnitId, out GameUnit* unit) ||
                    unit->r_ControllableForPlayerId != pending.PlayerId ||
                    unit->r_UnitChimp != pending.ExpectedType)
                {
                    CancelPending(pending, "unit-replaced-or-disbanded", refundGold: true, releaseReservedHorse: true);
                    cancelledCount++;
                    continue;
                }

                pending.Snapshot = CreateSnapshotFromUnit(currentUnitId, unit);
                pendingTransformations[globalId] = pending;
                if (pending.HasHorseAllocation && !ReservationMatches(pending, currentUnitId, unit))
                {
                    CancelPending(pending, "horse-reservation-invalid", refundGold: true, releaseReservedHorse: true);
                    cancelledCount++;
                    continue;
                }

                if (GameTimeManagerAPI.Instance.GetElapsedMilliseconds(pending.StartTime) < pending.DelayMilliseconds)
                    continue;

                readyCount++;
                int selectedReplacementUnitId;
                bool completed = pending.Action == MountAction
                    ? CompleteReservedMount(pending, "delay-complete", out selectedReplacementUnitId)
                    : ApplyDismount(pending.Snapshot, "delay-complete", pending.LimitReservationId, out selectedReplacementUnitId);
                if (completed)
                {
                    FinishPending(pending);
                    completedCount++;
                    if (selectedReplacementUnitId > 0)
                        selectionTransfers.Add(selectedReplacementUnitId);
                }
                else
                {
                    CancelPending(pending, "completion-failed", refundGold: true, releaseReservedHorse: true);
                    cancelledCount++;
                }
            }

            PublishSelectionTransfers(selectionTransfers);

            if (readyCount > 0 || cancelledCount > 0)
            {
                LogDebug(
                    $"Knight transformation pending update: examined={globalIds.Length}, ready={readyCount}, " +
                    $"completed={completedCount}, cancelled={cancelledCount}, remaining={pendingTransformations.Count}.");
            }
        }

        private bool CompleteReservedMount(PendingKnightTransformation pending, string reason, out int selectedReplacementUnitId)
        {
            selectedReplacementUnitId = 0;
            if (!TryResolveAliveUnitByGlobalId(pending.Snapshot, eChimps.CHIMP_TYPE_SWORDSMAN, out int swordsmanUnitId) ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(swordsmanUnitId, out GameUnit* swordsman) ||
                !ReservationMatches(pending, swordsmanUnitId, swordsman))
            {
                return false;
            }

            UnitTransformSnapshot currentSnapshot = CreateSnapshotFromUnit(swordsmanUnitId, swordsman);
            int knightUnitId = CreateUnitFromSnapshot(
                currentSnapshot,
                eChimps.CHIMP_TYPE_KNIGHT,
                "mount",
                reason,
                pending.LimitReservationId);
            if (knightUnitId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(knightUnitId, out GameUnit* knight))
            {
                if (knightUnitId > 0)
                    GameUnitManagerAPI.Instance.DeleteUnit(knightUnitId);
                return false;
            }

            HorseAllocation allocation = pending.Allocation;
            if (!TryTransferReservationToKnight(
                    pending, swordsmanUnitId, swordsman, knightUnitId, knight, reason + "-transfer"))
            {
                GameUnitManagerAPI.Instance.DeleteUnit(knightUnitId);
                return false;
            }

            bool transferSelection = ShouldTransferSelection(currentSnapshot.OwnerPlayerId, swordsman);
            if (!GameUnitManagerAPI.Instance.DeleteUnitSafe(swordsmanUnitId))
            {
                ReleaseExactHorseLink(allocation, knightUnitId, (int)knight->r_GlobalId, reason + "-delete-rollback");
                RestoreReservationToSwordsman(pending, swordsmanUnitId, reason + "-delete-rollback");
                GameUnitManagerAPI.Instance.DeleteUnit(knightUnitId);
                return false;
            }

            if (transferSelection)
                selectedReplacementUnitId = knightUnitId;

            LogDebug(
                $"Knight mount completed: reason={reason}, sourceGlobalId={pending.Snapshot.GlobalId}, " +
                $"knightUnitId={knightUnitId}, stableId={allocation.StableId}, slot={allocation.Slot}.");
            return true;
        }

        private static bool ShouldTransferSelection(int ownerPlayerId, GameUnit* source)
        {
            return source != null && source->r_AliveState == AliveState.IsAlive &&
                IsSelected(source) && ownerPlayerId == GetControlledPlayerId();
        }

        private void PrunePendingSelectionRequest()
        {
            if (pendingSelectionRequestIds.Count == 0)
                return;

            int localPlayerId = GetControlledPlayerId();
            var invalidIds = new List<int>();
            foreach (int unitId in pendingSelectionRequestIds)
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_ControllableForPlayerId != localPlayerId)
                {
                    invalidIds.Add(unitId);
                }
            }
            foreach (int unitId in invalidIds)
                pendingSelectionRequestIds.Remove(unitId);

            foreach (int unitId in pendingSelectionRequestIds)
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    !IsSelected(unit))
                {
                    return;
                }
            }

            // Every member of the last request is now reflected in Vanilla's selection.
            pendingSelectionRequestIds.Clear();
        }

        private void PublishSelectionTransfers(List<int> newUnitIds)
        {
            if (newUnitIds.Count == 0)
                return;

            foreach (int unitId in newUnitIds)
            {
                pendingSelectionRequestIds.Add(unitId);
                pendingSelectionTransferIds.Add(unitId);
            }

            try
            {
                int localPlayerId = GetControlledPlayerId();
                GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
                foreach (int unitId in unitApi.GetAllAliveUnits())
                {
                    if (unitApi.TryGetUnitById(unitId, out GameUnit* unit) &&
                        unit->r_ControllableForPlayerId == localPlayerId &&
                        IsSelected(unit))
                        pendingSelectionRequestIds.Add(unitId);
                }

                TryPublishReadySelectionTransfers();
            }
            catch (Exception ex)
            {
                LogError($"Knight transformation selection transfer failed: {ex}");
            }
        }

        private void TryPublishReadySelectionTransfers()
        {
            if (pendingSelectionTransferIds.Count == 0)
                return;

            try
            {
                int localPlayerId = GetControlledPlayerId();
                GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
                foreach (int unitId in pendingSelectionTransferIds)
                {
                    bool found = unitApi.TryGetUnitById(unitId, out GameUnit* unit);
                    if (!found || unit->r_AliveState != AliveState.IsAlive ||
                        unit->r_ControllableForPlayerId != localPlayerId)
                    {
                        pendingSelectionTransferTicks++;
                        if (pendingSelectionTransferTicks < 100)
                            return;
                        LogError($"Knight selection replacement did not become selectable: unitId={unitId}.");
                        pendingSelectionTransferIds.Clear();
                        pendingSelectionRequestIds.Clear();
                        pendingSelectionTransferTicks = 0;
                        return;
                    }
                }

                var selectedUnitIds = new List<int>();
                var seen = new HashSet<int>();
                foreach (int unitId in unitApi.GetAllAliveUnits())
                {
                    if (unitApi.TryGetUnitById(unitId, out GameUnit* unit) &&
                        unit->r_ControllableForPlayerId == localPlayerId &&
                        IsSelected(unit) && seen.Add(unitId))
                        selectedUnitIds.Add(unitId);
                }

                // Keep the previous request and replacements until the new gesture commits.
                foreach (int unitId in pendingSelectionRequestIds)
                {
                    if (unitApi.TryGetUnitById(unitId, out GameUnit* unit) &&
                        unit->r_AliveState == AliveState.IsAlive &&
                        unit->r_ControllableForPlayerId == localPlayerId && seen.Add(unitId))
                    {
                        selectedUnitIds.Add(unitId);
                    }
                }

                pendingSelectionRequestIds.Clear();
                pendingSelectionRequestIds.UnionWith(selectedUnitIds);
                if (!GamePlayerManagerAPI.Instance.SetSelectedChimps(selectedUnitIds))
                    LogError("Knight transformation selection transfer was rejected by the Script Extender.");
                pendingSelectionTransferIds.Clear();
                pendingSelectionTransferTicks = 0;
            }
            catch (Exception ex)
            {
                LogError($"Knight transformation selection transfer failed: {ex}");
            }
        }

        private bool TryTransferReservationToKnight(
            PendingKnightTransformation pending,
            int swordsmanUnitId,
            GameUnit* swordsman,
            int knightUnitId,
            GameUnit* knight,
            string reason)
        {
            HorseAllocation allocation = pending.Allocation;
            int knightGlobalId = knight == null ? 0 : (int)knight->r_GlobalId;
            if (knightGlobalId <= 0 || knightUnitId <= 0 ||
                knight->r_LinkedStableBuildingId != 0 || knight->r_LinkedStableGlobalId != 0 ||
                !ReservationMatches(pending, swordsmanUnitId, swordsman) ||
                !GameBuildingManagerAPI.Instance.TryGetBuildingById(allocation.StableId, out GameBuilding* stable) ||
                !IsUsableStable(stable, allocation.OwnerPlayerId) ||
                (int)stable->r_GlobalId != allocation.StableGlobalId)
            {
                return false;
            }

            int totalBefore = stable->r_TotalHorses;
            int usedBefore = stable->r_UsedHorses;
            int rechargeBefore = stable->r_HorseRechargeTimer;
            GameBuildingManagerAPI.Instance.UnlinkStablesUnitIdLink(
                allocation.StableId, allocation.Slot, bidirectional: true);

            if (!IsStableHorseSlotFree(stable, allocation.Slot) ||
                swordsman->r_LinkedStableBuildingId != 0 || swordsman->r_LinkedStableGlobalId != 0)
            {
                RestoreReservationToSwordsman(pending, swordsmanUnitId, reason + "-unlink-rollback");
                LogError(
                    $"Knight mount could not cleanly unlink its reservation: reason={reason}, " +
                    $"stableId={allocation.StableId}, slot={allocation.Slot}, sourceUnitId={swordsmanUnitId}.");
                return false;
            }

            GameBuildingManagerAPI.Instance.SetStablesUnitIdLink(
                allocation.StableId,
                allocation.Slot,
                knightUnitId,
                knightGlobalId,
                bidirectional: true);

            bool transferMatches =
                GetStableHorseSlotUnitId(stable, allocation.Slot) == knightUnitId &&
                GetStableHorseSlotGlobalId(stable, allocation.Slot) == knightGlobalId &&
                knight->r_LinkedStableBuildingId == allocation.StableId &&
                knight->r_LinkedStableGlobalId == (uint)allocation.StableGlobalId &&
                swordsman->r_LinkedStableBuildingId == 0 && swordsman->r_LinkedStableGlobalId == 0 &&
                stable->r_TotalHorses == totalBefore &&
                stable->r_UsedHorses == usedBefore &&
                stable->r_HorseRechargeTimer == rechargeBefore;
            if (transferMatches)
                return true;

            if (GetStableHorseSlotUnitId(stable, allocation.Slot) == knightUnitId &&
                GetStableHorseSlotGlobalId(stable, allocation.Slot) == knightGlobalId &&
                knight->r_LinkedStableBuildingId == allocation.StableId &&
                knight->r_LinkedStableGlobalId == (uint)allocation.StableGlobalId)
            {
                GameBuildingManagerAPI.Instance.UnlinkStablesUnitIdLink(
                    allocation.StableId, allocation.Slot, bidirectional: true);
            }

            bool restored = RestoreReservationToSwordsman(
                pending, swordsmanUnitId, reason + "-validation-rollback");
            LogError(
                $"Knight mount rolled back an invalid direct reservation transfer: reason={reason}, " +
                $"stableId={allocation.StableId}, slot={allocation.Slot}, sourceUnitId={swordsmanUnitId}, " +
                $"knightUnitId={knightUnitId}, restored={restored}.");
            return false;
        }

        private bool ReservationMatches(PendingKnightTransformation pending, int unitId, GameUnit* unit)
        {
            HorseAllocation allocation = pending.Allocation;
            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(allocation.StableId, out GameBuilding* stable) ||
                !IsUsableStable(stable, allocation.OwnerPlayerId) ||
                (int)stable->r_GlobalId != allocation.StableGlobalId)
            {
                return false;
            }

            return GetStableHorseSlotUnitId(stable, allocation.Slot) == unitId &&
                GetStableHorseSlotGlobalId(stable, allocation.Slot) == pending.Snapshot.GlobalId &&
                unit->r_LinkedStableBuildingId == allocation.StableId &&
                unit->r_LinkedStableGlobalId == (uint)allocation.StableGlobalId;
        }

        private static bool ReservationSlotIsFree(HorseAllocation allocation)
        {
            return GameBuildingManagerAPI.Instance.TryGetBuildingById(allocation.StableId, out GameBuilding* stable) &&
                IsUsableStable(stable, allocation.OwnerPlayerId) &&
                (int)stable->r_GlobalId == allocation.StableGlobalId &&
                IsStableHorseSlotFree(stable, allocation.Slot);
        }

        private bool RestoreReservationToSwordsman(PendingKnightTransformation pending, int unitId, string reason)
        {
            HorseAllocation allocation = pending.Allocation;
            if (!ReservationSlotIsFree(allocation) ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* swordsman) ||
                (int)swordsman->r_GlobalId != pending.Snapshot.GlobalId ||
                swordsman->r_LinkedStableBuildingId != 0 || swordsman->r_LinkedStableGlobalId != 0)
            {
                LogError(
                    $"Knight mount could not restore its swordsman reservation: reason={reason}, " +
                    $"stableId={allocation.StableId}, slot={allocation.Slot}, unitId={unitId}.");
                return false;
            }

            GameBuildingManagerAPI.Instance.SetStablesUnitIdLink(
                allocation.StableId,
                allocation.Slot,
                unitId,
                pending.Snapshot.GlobalId,
                bidirectional: true);

            bool restored = ReservationMatches(pending, unitId, swordsman);
            if (!restored)
            {
                LogError(
                    $"Knight mount restored an incomplete swordsman reservation: reason={reason}, " +
                    $"stableId={allocation.StableId}, slot={allocation.Slot}, unitId={unitId}.");
            }

            return restored;
        }

        private bool ReleaseExactHorseLink(
            HorseAllocation allocation,
            int unitId,
            int unitGlobalId,
            string reason)
        {
            if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(allocation.StableId, out GameBuilding* stable) ||
                !IsUsableStable(stable, allocation.OwnerPlayerId) ||
                (int)stable->r_GlobalId != allocation.StableGlobalId)
            {
                ClearMatchingUnitBacklink(unitId, unitGlobalId, allocation);
                return false;
            }

            if (GetStableHorseSlotUnitId(stable, allocation.Slot) != unitId ||
                GetStableHorseSlotGlobalId(stable, allocation.Slot) != unitGlobalId ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                (int)unit->r_GlobalId != unitGlobalId ||
                unit->r_LinkedStableBuildingId != allocation.StableId ||
                unit->r_LinkedStableGlobalId != (uint)allocation.StableGlobalId)
            {
                LogError($"Skipped unsafe horse unlink: reason={reason}, stableId={allocation.StableId}, slot={allocation.Slot}, unitId={unitId}, globalId={unitGlobalId}.");
                return false;
            }

            GameBuildingManagerAPI.Instance.UnlinkStablesUnitIdLink(allocation.StableId, allocation.Slot, bidirectional: true);
            return true;
        }

        private static void ClearMatchingUnitBacklink(
            int unitId,
            int unitGlobalId,
            HorseAllocation allocation)
        {
            if (GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) &&
                (int)unit->r_GlobalId == unitGlobalId &&
                unit->r_LinkedStableBuildingId == allocation.StableId &&
                unit->r_LinkedStableGlobalId == (uint)allocation.StableGlobalId)
            {
                unit->r_LinkedStableBuildingId = 0;
                unit->r_LinkedStableGlobalId = 0;
            }
        }

        private void FinishPending(PendingKnightTransformation pending)
        {
            pendingTransformations.Remove(pending.Snapshot.GlobalId);
            RemoveProgressVisual(pending.Snapshot.GlobalId);
            unitLimitBridge.ReleaseReservation(pending.LimitReservationId);
        }

        private void CancelPending(
            PendingKnightTransformation pending,
            string reason,
            bool refundGold,
            bool releaseReservedHorse)
        {
            pendingTransformations.Remove(pending.Snapshot.GlobalId);
            RemoveProgressVisual(pending.Snapshot.GlobalId);
            unitLimitBridge.ReleaseReservation(pending.LimitReservationId);

            if (releaseReservedHorse && pending.HasHorseAllocation)
            {
                int unitId = FindAliveUnitIdByGlobalId(pending.Snapshot.GlobalId);
                if (unitId > 0)
                    ReleaseExactHorseLink(pending.Allocation, unitId, pending.Snapshot.GlobalId, reason);
            }

            if (refundGold)
                RefundGold(pending.PlayerId, pending.PaidGold, reason);
        }

        private void RefundGold(int playerId, int amount, string reason)
        {
            if (amount <= 0)
                return;

            if (!GamePlayerManagerAPI.Instance.TryAddGood(playerId, eGoods.STORED_GOLD, amount))
                LogError($"Knight transformation gold refund failed: reason={reason}, playerId={playerId}, amount={amount}.");
        }

        private void CancelAllPending(string reason, bool refundGold, bool releaseReservedHorse)
        {
            if (pendingTransformations.Count == 0)
            {
                ClearAllProgressVisuals();
                return;
            }

            PendingKnightTransformation[] pending = new PendingKnightTransformation[pendingTransformations.Count];
            pendingTransformations.Values.CopyTo(pending, 0);
            for (int index = 0; index < pending.Length; index++)
                CancelPending(pending[index], reason, refundGold, releaseReservedHorse);
            ClearAllProgressVisuals();
        }

        private void CancelPendingByGlobalIds(int playerId, int[] globalIds, string reason)
        {
            if (globalIds == null)
                return;

            var seen = new HashSet<int>();
            for (int index = 0; index < globalIds.Length; index++)
            {
                int globalId = globalIds[index];
                if (!seen.Add(globalId) ||
                    !pendingTransformations.TryGetValue(globalId, out PendingKnightTransformation pending) ||
                    pending.PlayerId != playerId)
                {
                    continue;
                }

                CancelPending(pending, reason, refundGold: true, releaseReservedHorse: true);
            }
        }

        private void OnPlayerStopCommand()
        {
            int playerId = GetControlledPlayerId();
            int[] globalIds = CaptureSelectedPendingGlobalIds(playerId);
            if (globalIds.Length == 0)
                return;

            if (RequiresChoreTransport())
            {
                if (IsChoreTransportReady())
                    TrySendTransformationPacket(playerId, KnightTransformationPacket.CancelSelectedAction, globalIds);
            }
            else
            {
                CancelPendingByGlobalIds(playerId, globalIds, "local-stop");
            }
        }

        private int[] CaptureSelectedPendingGlobalIds(int playerId)
        {
            var result = new List<int>();
            var seen = new HashSet<int>();
            int[] selectedIds = GetSelectedChimpsSafe();
            for (int index = 0; index < selectedIds.Length; index++)
                AddSelectedPendingGlobalId(playerId, selectedIds[index], result, seen);

            int[] aliveIds = GameUnitManagerAPI.Instance.GetAllAliveUnits();
            for (int index = 0; index < aliveIds.Length; index++)
            {
                int unitId = aliveIds[index];
                if (unitId > 0 && GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) && IsSelected(unit))
                    AddSelectedPendingGlobalId(playerId, unitId, result, seen);
            }

            result.Sort();
            return result.ToArray();
        }

        private void AddSelectedPendingGlobalId(
            int playerId,
            int unitId,
            List<int> result,
            HashSet<int> seen)
        {
            if (unitId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit))
                return;

            int globalId = (int)unit->r_GlobalId;
            if (globalId > 0 && seen.Add(globalId) &&
                pendingTransformations.TryGetValue(globalId, out PendingKnightTransformation pending) &&
                pending.PlayerId == playerId)
            {
                result.Add(globalId);
            }
        }

        private bool IsPendingUnitId(int unitId)
        {
            if (unitId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit))
                return false;

            int globalId = (int)unit->r_GlobalId;
            return globalId > 0 && pendingTransformations.ContainsKey(globalId);
        }

        private bool PreparePendingSave(
            string path,
            int screenCentreX,
            int screenCentreY,
            int realScreenCentreX,
            int realScreenCentreY,
            bool lockMap,
            bool tempLockOnly,
            bool mapSave)
        {
            if (!RequiresChoreTransport())
            {
                CancelAllPending("singleplayer-save", refundGold: true, releaseReservedHorse: true);
                return persistentSaveTrampoline(
                    path, screenCentreX, screenCentreY, realScreenCentreX, realScreenCentreY,
                    lockMap, tempLockOnly, mapSave);
            }

            if (deferredSave != null)
            {
                LogError("A save was refused while another Knight transformation cleanup save is pending.");
                return false;
            }

            deferredSave = new DeferredSaveRequest
            {
                Path = path,
                ScreenCentreX = screenCentreX,
                ScreenCentreY = screenCentreY,
                RealScreenCentreX = realScreenCentreX,
                RealScreenCentreY = realScreenCentreY,
                LockMap = lockMap,
                TempLockOnly = tempLockOnly,
                MapSave = mapSave
            };
            deferredSaveReadyAfterTick = long.MaxValue;

            int playerId = GetControlledPlayerId();
            if (!IsChoreTransportReady() ||
                !TrySendTransformationPacket(playerId, KnightTransformationPacket.CancelAllAction, Array.Empty<int>()))
            {
                deferredSave = null;
                LogError("Save was not started because synchronized Knight transformation cleanup could not be queued.");
            }

            return false;
        }

        private void ScheduleDeferredSaveCompletion()
        {
            if (deferredSave == null)
                return;

            int currentTick = GameTimeManagerAPI.Instance.CaptureTimeStamp().CapturedGameTick;
            deferredSaveReadyAfterTick = (long)currentTick + 1L;
            LogDebug($"Knight transformation save cleanup completed; save deferred until tick {deferredSaveReadyAfterTick}.");
        }

        private void TryCompleteDeferredSave(int currentTick)
        {
            if (deferredSave == null || currentTick < deferredSaveReadyAfterTick)
                return;

            deferredSaveReadyAfterTick = long.MaxValue;
            CompleteDeferredSave();
        }

        private void CompleteDeferredSave()
        {
            DeferredSaveRequest request = deferredSave;
            deferredSave = null;
            if (request == null || persistentSaveTrampoline == null)
                return;

            persistentSaveTrampoline(
                request.Path,
                request.ScreenCentreX,
                request.ScreenCentreY,
                request.RealScreenCentreX,
                request.RealScreenCentreY,
                request.LockMap,
                request.TempLockOnly,
                request.MapSave);
        }

        private void UpdateProgressVisual(Chimp chimp)
        {
            int unitId = chimp.objectID;
            if (unitId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit))
                return;

            int globalId = (int)unit->r_GlobalId;
            if (!pendingTransformations.TryGetValue(globalId, out PendingKnightTransformation pending) ||
                chimp.gameObject == null || chimp.gameObject5 == null || chimp.sprRenderer5?.sprite == null)
            {
                RemoveProgressVisual(globalId);
                return;
            }

            EnsureProgressSprite();
            if (!progressVisuals.TryGetValue(globalId, out ProgressVisual visual) || visual.GameObject == null)
            {
                var progressObject = new GameObject("ExtraFeatures_KnightTransformationProgress");
                progressObject.transform.SetParent(chimp.gameObject.transform, false);
                var renderer = progressObject.AddComponent<SpriteRenderer>();
                renderer.sprite = progressSprite;
                renderer.color = Color.white;
                visual = new ProgressVisual { GameObject = progressObject, Renderer = renderer };
                progressVisuals[globalId] = visual;
            }

            Bounds hpBounds = chimp.sprRenderer5.sprite.bounds;
            Vector3 hpLocal = chimp.gameObject.transform.InverseTransformPoint(chimp.gameObject5.transform.position);
            visual.GameObject.transform.localPosition = new Vector3(
                hpLocal.x + hpBounds.min.x,
                hpLocal.y + hpBounds.min.y - ProgressBarVerticalGap,
                hpLocal.z);
            float progress = pending.DelayMilliseconds <= 0
                ? 1f
                : Mathf.Clamp01((float)GameTimeManagerAPI.Instance.GetElapsedMilliseconds(pending.StartTime) /
                    pending.DelayMilliseconds);
            visual.GameObject.transform.localScale = new Vector3(hpBounds.size.x * 32f * progress, 2f, 1f);
            visual.Renderer.sortingLayerID = chimp.sprRenderer5.sortingLayerID;
            visual.Renderer.sortingOrder = Math.Min(32767, chimp.sprRenderer5.sortingOrder + 1);
            visual.GameObject.SetActive(true);
        }

        private static void EnsureProgressSprite()
        {
            if (progressSprite != null)
                return;

            progressTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            progressTexture.name = "ExtraFeatures_KnightTransformationProgressTexture";
            progressTexture.SetPixel(0, 0, Color.white);
            progressTexture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            progressSprite = Sprite.Create(
                progressTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0f, 0.5f),
                32f);
            progressSprite.name = "ExtraFeatures_KnightTransformationProgressSprite";
        }

        private void RemoveProgressVisualForUnitId(int unitId)
        {
            if (unitId <= 0 || !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit))
                return;

            RemoveProgressVisual((int)unit->r_GlobalId);
        }

        private void RemoveProgressVisual(int globalId)
        {
            if (!progressVisuals.TryGetValue(globalId, out ProgressVisual visual))
                return;

            progressVisuals.Remove(globalId);
            if (visual.GameObject != null)
                UnityEngine.Object.Destroy(visual.GameObject);
        }

        private void ClearAllProgressVisuals()
        {
            ProgressVisual[] visuals = new ProgressVisual[progressVisuals.Count];
            progressVisuals.Values.CopyTo(visuals, 0);
            progressVisuals.Clear();
            for (int index = 0; index < visuals.Length; index++)
            {
                if (visuals[index].GameObject != null)
                    UnityEngine.Object.Destroy(visuals[index].GameObject);
            }
        }

        private sealed class PendingKnightTransformation
        {
            public UnitTransformSnapshot Snapshot;
            public int Action;
            public int PlayerId;
            public GameTimeStamp StartTime;
            public int DelayMilliseconds;
            public int PaidGold;
            public long LimitReservationId;
            public bool HasHorseAllocation;
            public HorseAllocation Allocation;

            public eChimps ExpectedType => Action == MountAction
                ? eChimps.CHIMP_TYPE_SWORDSMAN
                : eChimps.CHIMP_TYPE_KNIGHT;
        }

        private sealed class ProgressVisual
        {
            public GameObject GameObject;
            public SpriteRenderer Renderer;
        }

        private sealed class DeferredSaveRequest
        {
            public string Path;
            public int ScreenCentreX;
            public int ScreenCentreY;
            public int RealScreenCentreX;
            public int RealScreenCentreY;
            public bool LockMap;
            public bool TempLockOnly;
            public bool MapSave;
        }
    }
}
