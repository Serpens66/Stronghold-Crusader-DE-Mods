using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TroopMovementFix
{
    internal sealed unsafe class TroopMovementFix3Runtime
    {
        private const int ExpectedMaximumTrackedUnits = 10000;

        // The complete native tribe record stores freeUnitSpeeds at +0x56C.
        // Script Extender's GameTribe* begins +0x2A into that record.
        private const int TribeFreeUnitSpeedsOffset = 0x542;
        private const int TribeMovementSpeedOffset = 0x54E;

        private readonly ManualLogSource log;
        private readonly Dictionary<int, TribeSynchronization>
            synchronizationByTribeId =
                new Dictionary<int, TribeSynchronization>();
        private readonly HashSet<int> activeMoveOrderTribeIds =
            new HashSet<int>();
        private readonly List<int> unitIds =
            new List<int>(ExpectedMaximumTrackedUnits);
        private readonly HashSet<eChimps> unitTypes =
            new HashSet<eChimps>();
        private readonly Dictionary<eChimps, ushort>
            runningSpeedBonusByUnitType =
                new Dictionary<eChimps, ushort>();
        private readonly List<IDisposable> subscriptions =
            new List<IDisposable>();

        private SpearmanMovementPatch spearmanMovementPatch;
        private SynchronizedMovementCadencePatch cadencePatch;
        private bool inputFailureLogged;

        public TroopMovementFix3Runtime(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public void Apply(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            if (spearmanMovementPatch != null ||
                cadencePatch != null ||
                subscriptions.Count != 0)
            {
                return;
            }

            SpearmanMovementPatch newSpearmanMovementPatch = null;
            SynchronizedMovementCadencePatch newCadencePatch = null;
            List<IDisposable> newSubscriptions =
                new List<IDisposable>();

            try
            {
                newSpearmanMovementPatch = new SpearmanMovementPatch(
                    log,
                    memory,
                    unchecked((ulong)libraryHandle.ToInt64()));

                newCadencePatch =
                    new SynchronizedMovementCadencePatch(
                        log,
                        memory,
                        unchecked((ulong)libraryHandle.ToInt64()),
                        TryGetCadence);

                newSubscriptions.Add(
                    TribeR3EventHooks.OnTribeIssueOrderMoveHere.Observable
                        .Subscribe(OnTribeIssueOrderMoveHere));
                newSubscriptions.Add(
                    TribeR3EventHooks.OnTribeIssueOrderWithTarget.Observable
                        .Subscribe(OnTribeIssueOrderWithTarget));
                newSubscriptions.Add(
                    TribeR3EventHooks.OnTribeAssignUnit.Observable
                        .Subscribe(OnTribeAssignUnit));
                newSubscriptions.Add(
                    MapLoaderR3EventHooks.OnUnloadMap.Observable
                        .Subscribe(args =>
                        {
                            if (args.Phase == EventHookPhase.Post)
                                ClearSynchronization();
                        }));

                spearmanMovementPatch = newSpearmanMovementPatch;
                cadencePatch = newCadencePatch;
                subscriptions.AddRange(newSubscriptions);
            }
            catch
            {
                foreach (IDisposable subscription in newSubscriptions)
                    subscription.Dispose();

                newCadencePatch?.Dispose();
                newSpearmanMovementPatch?.Dispose();
                throw;
            }

            ModLog.Debug(
                log,
                "Troop Movement Fix 3 active: mixed DefaultInSync groups " +
                "use the slowest member's Vanilla maximum speed and a " +
                "matching shared cadence; " +
                "Spearmen use the Archer walk/run decision instead of the " +
                "Improved-Spearman movement override; Ctrl enables Vanilla " +
                "free-unit-speeds.");
        }

        private void OnTribeIssueOrderMoveHere(
            TribeIssueOrderMoveHereEventArgs args)
        {
            if (!args.IsNewOrder ||
                args.MoveType == TribeMoveType.NoChange)
            {
                return;
            }

            if (args.Phase == EventHookPhase.Post)
            {
                activeMoveOrderTribeIds.Remove(args.TribeId);
                if (args.ReturnValue != 1)
                    RemoveSynchronization(args.TribeId, restoreSpeed: true);
                return;
            }

            if (args.Phase != EventHookPhase.Pre)
                return;

            activeMoveOrderTribeIds.Add(args.TribeId);
            RemoveSynchronization(args.TribeId, restoreSpeed: true);

            if (ReadCtrlModifier())
            {
                TryEnableVanillaFreeUnitSpeeds(args.TribeId);
                return;
            }

            if (args.MoveType != TribeMoveType.DefaultInSync)
                return;

            TryApplyMixedGroupSynchronization(args.TribeId);
        }

        private void OnTribeIssueOrderWithTarget(
            TribeIssueOrderWithTargetEventArgs args)
        {
            if (args.Phase == EventHookPhase.Pre)
                RemoveSynchronization(args.TribeId, restoreSpeed: true);
        }

        private void OnTribeAssignUnit(TribeAssignUnitEventArgs args)
        {
            if (args.Phase != EventHookPhase.Pre ||
                activeMoveOrderTribeIds.Contains(args.TribeId))
            {
                return;
            }

            int previousTribeId = 0;
            if (GameUnitManagerAPI.Instance.TryGetUnitById(
                    args.UnitId,
                    out GameUnit* unit) &&
                unit != null)
            {
                previousTribeId = unit->r_TribeId;
            }

            RemoveSynchronization(previousTribeId, restoreSpeed: false);
            if (args.TribeId != previousTribeId)
                RemoveSynchronization(args.TribeId, restoreSpeed: false);
        }

        private bool TryApplyMixedGroupSynchronization(int tribeId)
        {
            if (!TryGetTribe(tribeId, out GameTribe* tribe))
            {
                ModLog.Warning(
                    log,
                    $"Mixed-group synchronization could not access " +
                    $"tribeId={tribeId}; this order remains Vanilla.");
                return false;
            }

            unitIds.Clear();
            unitTypes.Clear();
            runningSpeedBonusByUnitType.Clear();
            if (!GameTribeManagerAPI.Instance.GetUnits(tribeId, unitIds))
                return false;

            ushort slowestMaximumSpeed = 0;
            ushort sharedRunningSpeedBonus = 0;
            bool hasLimitingRunningSpeedBonus = false;
            int activeUnitCount = 0;
            bool synchronizeRunning = true;
            bool improvedSpearmen =
                GamePlayerManagerAPI.Instance.IsImprovedSpearman();

            foreach (int unitId in unitIds)
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(
                        unitId,
                        out GameUnit* unit) ||
                    unit == null ||
                    unit->r_AliveState != AliveState.IsAlive)
                {
                    continue;
                }

                activeUnitCount++;
                unitTypes.Add(unit->r_UnitChimp);

                bool supportsSynchronizedRunning =
                    cadencePatch.SupportsSynchronizedRunning(
                        unit->r_UnitChimp);
                if (!runningSpeedBonusByUnitType.ContainsKey(
                        unit->r_UnitChimp))
                {
                    runningSpeedBonusByUnitType[unit->r_UnitChimp] =
                        cadencePatch.GetNativeRunningSpeedBonus(
                            unit->r_UnitChimp,
                            improvedSpearmen);
                }

                ushort nativeRunningSpeedBonus =
                    runningSpeedBonusByUnitType[unit->r_UnitChimp];
                ushort maximumSpeed = unit->r_CurrentSpeed;
                if (maximumSpeed > slowestMaximumSpeed)
                {
                    slowestMaximumSpeed = maximumSpeed;
                    sharedRunningSpeedBonus =
                        nativeRunningSpeedBonus;
                    hasLimitingRunningSpeedBonus = true;
                }
                else if (maximumSpeed == slowestMaximumSpeed &&
                         (!hasLimitingRunningSpeedBonus ||
                          nativeRunningSpeedBonus <
                              sharedRunningSpeedBonus))
                {
                    sharedRunningSpeedBonus =
                        nativeRunningSpeedBonus;
                    hasLimitingRunningSpeedBonus = true;
                }

                if (!supportsSynchronizedRunning ||
                    (unit->r_UnitChimp == eChimps.CHIMP_TYPE_SPEARMAN &&
                     !improvedSpearmen))
                {
                    synchronizeRunning = false;
                }
            }

            if (activeUnitCount == 0 || unitTypes.Count < 2)
                return false;

            byte* tribeBytes = (byte*)tribe;
            ushort* freeUnitSpeeds =
                (ushort*)(tribeBytes + TribeFreeUnitSpeedsOffset);
            ushort* movementSpeed =
                (ushort*)(tribeBytes + TribeMovementSpeedOffset);

            TribeSynchronization synchronization =
                new TribeSynchronization(
                    synchronizeRunning
                        ? SynchronizedMovementCadence.Running
                        : SynchronizedMovementCadence.Walking,
                    runningSpeedBonus: synchronizeRunning
                        ? sharedRunningSpeedBonus
                        : (ushort)0,
                    previousMovementSpeed: *movementSpeed,
                    appliedMovementSpeed: slowestMaximumSpeed);

            *freeUnitSpeeds = 0;
            *movementSpeed = slowestMaximumSpeed;
            synchronizationByTribeId[tribeId] = synchronization;

            ModLog.Debug(
                log,
                $"Mixed-group synchronization prepared: " +
                $"tribeId={tribeId}, members={activeUnitCount}, " +
                $"unitTypes={unitTypes.Count}, " +
                $"slowestMaximumSpeedLevel={slowestMaximumSpeed}, " +
                $"cadence={synchronization.Cadence}, " +
                $"sharedRunningSpeedBonus=" +
                $"{synchronization.RunningSpeedBonus}.");
            return true;
        }

        private bool TryEnableVanillaFreeUnitSpeeds(int tribeId)
        {
            if (!TryGetTribe(tribeId, out GameTribe* tribe))
            {
                ModLog.Warning(
                    log,
                    $"Ctrl movement could not enable Vanilla free-unit-speeds: " +
                    $"tribeId={tribeId} was not available.");
                return false;
            }

            ushort* freeUnitSpeeds =
                (ushort*)((byte*)tribe + TribeFreeUnitSpeedsOffset);
            *freeUnitSpeeds = 1;
            return true;
        }

        private bool TryGetCadence(
            int tribeId,
            out SynchronizedMovementCadence cadence,
            out ushort runningSpeedBonus)
        {
            if (synchronizationByTribeId.TryGetValue(
                    tribeId,
                    out TribeSynchronization synchronization))
            {
                cadence = synchronization.Cadence;
                runningSpeedBonus =
                    synchronization.RunningSpeedBonus;
                return true;
            }

            cadence = default;
            runningSpeedBonus = 0;
            return false;
        }

        private void RemoveSynchronization(
            int tribeId,
            bool restoreSpeed)
        {
            if (tribeId <= 0 ||
                !synchronizationByTribeId.TryGetValue(
                    tribeId,
                    out TribeSynchronization synchronization))
            {
                return;
            }

            synchronizationByTribeId.Remove(tribeId);
            if (!restoreSpeed ||
                !TryGetTribe(tribeId, out GameTribe* tribe))
            {
                return;
            }

            ushort* movementSpeed =
                (ushort*)((byte*)tribe + TribeMovementSpeedOffset);
            if (*movementSpeed == synchronization.AppliedMovementSpeed)
                *movementSpeed = synchronization.PreviousMovementSpeed;
        }

        private void ClearSynchronization()
        {
            synchronizationByTribeId.Clear();
            activeMoveOrderTribeIds.Clear();
            unitIds.Clear();
            unitTypes.Clear();
            runningSpeedBonusByUnitType.Clear();
        }

        private static bool TryGetTribe(
            int tribeId,
            out GameTribe* tribe)
        {
            tribe = null;
            return tribeId > 0 &&
                   GameTribeManagerAPI.Instance.TryGetTribeById(
                       tribeId,
                       out tribe) &&
                   tribe != null;
        }

        private bool ReadCtrlModifier()
        {
            try
            {
                return Input.GetKey(KeyCode.LeftControl) ||
                       Input.GetKey(KeyCode.RightControl);
            }
            catch (Exception ex)
            {
                if (!inputFailureLogged)
                {
                    inputFailureLogged = true;
                    ModLog.Error(
                        log,
                        $"Could not read the Ctrl movement modifier; " +
                        $"this order remains completely Vanilla: {ex}");
                }

                return false;
            }
        }

        private sealed class TribeSynchronization
        {
            public TribeSynchronization(
                SynchronizedMovementCadence cadence,
                ushort runningSpeedBonus,
                ushort previousMovementSpeed,
                ushort appliedMovementSpeed)
            {
                Cadence = cadence;
                RunningSpeedBonus = runningSpeedBonus;
                PreviousMovementSpeed = previousMovementSpeed;
                AppliedMovementSpeed = appliedMovementSpeed;
            }

            public SynchronizedMovementCadence Cadence { get; }
            public ushort RunningSpeedBonus { get; }
            public ushort PreviousMovementSpeed { get; }
            public ushort AppliedMovementSpeed { get; }
        }
    }
}
