// Feature: Fix synchronized movement speed for mixed troop groups.
using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    internal sealed unsafe class TroopMovementFix3Runtime : IDisposable
    {
        private const int ExpectedMaximumTrackedUnits = 10000;

        // The complete native tribe record stores freeUnitSpeeds at +0x56C.
        // Script Extender's GameTribe* begins +0x2A into that record.
        private const int TribeFreeUnitSpeedsOffset = 0x542;
        private const int TribeMovementSpeedOffset = 0x54E;

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly Dictionary<int, TribeSynchronization>
            synchronizationByTribeId =
                new Dictionary<int, TribeSynchronization>();
        private readonly HashSet<int> activeMoveOrderTribeIds =
            new HashSet<int>();
        private readonly List<int> unitIds =
            new List<int>(ExpectedMaximumTrackedUnits);
        private readonly Dictionary<eChimps, UnitTypeMovementInfo>
            unitTypeMovementInfoByType =
                new Dictionary<eChimps, UnitTypeMovementInfo>(
                    (int)eChimps.CHIMP_NUM_TYPES);
        private readonly List<IDisposable> troopSubscriptions =
            new List<IDisposable>(4);

        private SpearmanMovementPatch spearmanMovementPatch;
        private SynchronizedMovementCadencePatch cadencePatch;
        private IntPtr libraryHandle;
        private int libraryLength;
        private bool nativeLibraryAvailable;
        private bool fixedLayoutHashValidated;
        private bool fixedLayoutErrorLogged;
        private bool initializationFailed;

        public TroopMovementFix3Runtime(
            ManualLogSource log,
            BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings =
                settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public void InitializeNative(
            IntPtr newLibraryHandle,
            ReadOnlySpan<byte> memory,
            bool isFixedLayoutHashValidated)
        {
            if (nativeLibraryAvailable)
                return;

            if (newLibraryHandle == IntPtr.Zero || memory.Length == 0)
                throw new ArgumentException("The Crusader library is unavailable.");

            libraryHandle = newLibraryHandle;
            libraryLength = memory.Length;
            fixedLayoutHashValidated = isFixedLayoutHashValidated;
            nativeLibraryAvailable = true;
            MovementCadenceIntegration.RegistrationChanged += ApplySetting;
            try
            {
                ApplySetting();
            }
            catch (Exception ex)
            {
                // A conflicting native hook must disable only the movement feature.
                initializationFailed = true;
                MovementCadenceIntegration.RegistrationChanged -= ApplySetting;
                Disable();
                Shared.DebugLogHelper.LogError(
                    log,
                    $"Bugfixes and QoL Troop Movement Fix could not be initialized and remains inactive: {ex}");
            }
        }

        public void ApplySetting()
        {
            if (!nativeLibraryAvailable || initializationFailed)
                return;

            bool shouldEnableTroopMovementFix =
                fixedLayoutHashValidated &&
                settings.EnableMod &&
                settings.EnableTroopMovementFix;
            bool shouldEnableCadencePatch =
                fixedLayoutHashValidated &&
                ((settings.EnableMod && settings.EnableTroopMovementFix) ||
                 MovementCadenceIntegration.HasFastRecruitCallbacks);

            if (((settings.EnableMod &&
                  settings.EnableTroopMovementFix) ||
                 MovementCadenceIntegration.HasFastRecruitCallbacks) &&
                !fixedLayoutHashValidated &&
                !fixedLayoutErrorLogged)
            {
                fixedLayoutErrorLogged = true;
                Shared.DebugLogHelper.LogError(
                    log,
                    "Bugfixes and QoL movement hooks remain inactive because their fixed native tribe and unit field layouts are not validated for this CrusaderDE.dll.");
            }

            if (shouldEnableCadencePatch && cadencePatch == null)
                EnableCadencePatch();

            try
            {
                if (shouldEnableTroopMovementFix &&
                    !AreTroopMovementFixComponentsActive)
                {
                    EnableTroopMovementFixComponents();
                }
                else if (!shouldEnableTroopMovementFix &&
                         AreTroopMovementFixComponentsActive)
                {
                    DisableTroopMovementFixComponents();
                }
            }
            catch
            {
                if (!MovementCadenceIntegration.HasFastRecruitCallbacks)
                    DisableCadencePatch();

                throw;
            }

            if (!shouldEnableCadencePatch)
                DisableCadencePatch();

            TroopMovementFix3ModLog.Debug(
                log,
                $"Movement options reconciled: " +
                $"troopSpeedFixRequested={shouldEnableTroopMovementFix}, " +
                $"externalFastRecruitRallyRequested=" +
                $"{MovementCadenceIntegration.HasFastRecruitCallbacks}, " +
                $"cadenceHookActive={cadencePatch != null}, " +
                $"troopFixComponentsActive=" +
                $"{AreTroopMovementFixComponentsActive}.");
        }

        public void Dispose()
        {
            MovementCadenceIntegration.RegistrationChanged -= ApplySetting;
            Disable();
            nativeLibraryAvailable = false;
            fixedLayoutHashValidated = false;
            libraryHandle = IntPtr.Zero;
            libraryLength = 0;
        }

        private bool AreTroopMovementFixComponentsActive =>
            spearmanMovementPatch != null &&
            troopSubscriptions.Count != 0;

        private bool IsFeatureEnabled =>
            settings.EnableMod &&
            settings.EnableTroopMovementFix &&
            cadencePatch != null &&
            AreTroopMovementFixComponentsActive;

        private ReadOnlySpan<byte> GetNativeLibraryMemory()
        {
            // The module stays loaded for the process lifetime, so this span
            // remains valid when an option installs a native patch later.
            return new ReadOnlySpan<byte>(
                libraryHandle.ToPointer(),
                libraryLength);
        }

        private void EnableCadencePatch()
        {
            SynchronizedMovementCadencePatch newCadencePatch = null;

            try
            {
                newCadencePatch =
                new SynchronizedMovementCadencePatch(
                        log,
                        GetNativeLibraryMemory(),
                        unchecked((ulong)libraryHandle.ToInt64()),
                        TryGetCadence,
                        MovementCadenceIntegration.ApplyFastRecruitMaximumSpeed,
                        MovementCadenceIntegration.TryApplyFastRecruitCadence,
                        fixedLayoutHashValidated);

                cadencePatch = newCadencePatch;
                MovementCadenceIntegration.SetCadencePatch(newCadencePatch);
            }
            catch
            {
                newCadencePatch?.Dispose();
                throw;
            }
        }

        private void EnableTroopMovementFixComponents()
        {
            ReadOnlySpan<byte> memory =
                GetNativeLibraryMemory();

            SpearmanMovementPatch newSpearmanMovementPatch = null;
            List<IDisposable> newSubscriptions =
                new List<IDisposable>(4);

            try
            {
                newSpearmanMovementPatch = new SpearmanMovementPatch(
                    log,
                    memory,
                    unchecked((ulong)libraryHandle.ToInt64()),
                    fixedLayoutHashValidated);

                newSubscriptions.Add(
                    TribeR3EventHooks.OnTribeAssignUnit.Observable
                        .Subscribe(OnTribeAssignUnit));
                newSubscriptions.Add(
                    TribeR3EventHooks.OnTribeIssueOrderMoveHere.Observable
                        .Subscribe(OnTribeIssueOrderMoveHere));
                newSubscriptions.Add(
                    TribeR3EventHooks.OnTribeIssueOrderWithTarget.Observable
                        .Subscribe(OnTribeIssueOrderWithTarget));
                newSubscriptions.Add(
                    MapLoaderR3EventHooks.OnUnloadMap.Observable
                        .Subscribe(OnUnloadMap));

                spearmanMovementPatch = newSpearmanMovementPatch;
                troopSubscriptions.AddRange(newSubscriptions);
            }
            catch
            {
                foreach (IDisposable subscription in newSubscriptions)
                    subscription.Dispose();

                newSpearmanMovementPatch?.Dispose();
                throw;
            }

            TroopMovementFix3ModLog.Debug(
                log,
                "Troop Movement Fix 3 active: mixed DefaultInSync groups " +
                "use the slowest member's Vanilla maximum speed and a " +
                "matching shared cadence; " +
                "Spearmen use the Archer walk/run decision instead of the " +
                "Improved-Spearman movement override.");
        }

        private void DisableTroopMovementFixComponents()
        {
            foreach (IDisposable subscription in troopSubscriptions)
                subscription.Dispose();

            troopSubscriptions.Clear();

            // Restore only values still owned by the troop fix before its
            // dedicated hooks and remembered state are removed.
            foreach (int tribeId in
                     new List<int>(synchronizationByTribeId.Keys))
            {
                RemoveSynchronization(tribeId, restoreSpeed: true);
            }

            ClearSynchronization();
            spearmanMovementPatch?.Dispose();
            spearmanMovementPatch = null;

            TroopMovementFix3ModLog.Debug(
                log,
                "Troop Movement Fix 3 inactive; its Spearman patch and event subscriptions were removed.");
        }

        private void DisableCadencePatch()
        {
            MovementCadenceIntegration.SetCadencePatch(null);
            cadencePatch?.Dispose();
            cadencePatch = null;
        }

        private void Disable()
        {
            if (AreTroopMovementFixComponentsActive ||
                synchronizationByTribeId.Count != 0)
            {
                DisableTroopMovementFixComponents();
            }

            DisableCadencePatch();
        }

        private void OnTribeIssueOrderMoveHere(
            TribeIssueOrderMoveHereEventArgs args)
        {
            if (!IsFeatureEnabled ||
                !args.IsNewOrder ||
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

            if (args.MoveType != TribeMoveType.DefaultInSync)
                return;

            TryApplyMixedGroupSynchronization(args.TribeId);
        }

        private void OnUnloadMap(MapUnloadEventArgs args)
        {
            if (args.Phase != EventHookPhase.Post)
                return;

            ClearSynchronization();
        }

        private void OnTribeIssueOrderWithTarget(
            TribeIssueOrderWithTargetEventArgs args)
        {
            if (IsFeatureEnabled && args.Phase == EventHookPhase.Pre)
                RemoveSynchronization(args.TribeId, restoreSpeed: true);
        }

        private void OnTribeAssignUnit(TribeAssignUnitEventArgs args)
        {
            if (!IsFeatureEnabled ||
                args.Phase != EventHookPhase.Pre ||
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
                TroopMovementFix3ModLog.Warning(
                    log,
                    $"Mixed-group synchronization could not access " +
                    $"tribeId={tribeId}; this order remains Vanilla.");
                return false;
            }

            unitIds.Clear();
            unitTypeMovementInfoByType.Clear();
            if (!GameTribeManagerAPI.Instance.GetUnits(tribeId, unitIds))
                return false;

            ushort slowestMaximumSpeed = 0;
            ushort sharedRunningSpeedBonus = 0;
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

                bool isFirstActiveUnit = activeUnitCount == 0;
                activeUnitCount++;
                eChimps unitType = unit->r_UnitChimp;
                if (!unitTypeMovementInfoByType.TryGetValue(
                        unitType,
                        out UnitTypeMovementInfo movementInfo))
                {
                    bool supportsSynchronizedRunning =
                        cadencePatch.SupportsSynchronizedRunning(
                            unitType) &&
                        (unitType != eChimps.CHIMP_TYPE_SPEARMAN ||
                         improvedSpearmen);
                    movementInfo = new UnitTypeMovementInfo(
                        supportsSynchronizedRunning,
                        supportsSynchronizedRunning
                            ? cadencePatch.GetNativeRunningSpeedBonus(
                                unitType,
                                improvedSpearmen)
                            : (ushort)0);
                    unitTypeMovementInfoByType.Add(
                        unitType,
                        movementInfo);
                }

                ushort maximumSpeed = unit->r_CurrentSpeed;
                if (isFirstActiveUnit ||
                    maximumSpeed > slowestMaximumSpeed)
                {
                    slowestMaximumSpeed = maximumSpeed;
                    sharedRunningSpeedBonus =
                        movementInfo.NativeRunningSpeedBonus;
                }
                else if (maximumSpeed == slowestMaximumSpeed &&
                         movementInfo.NativeRunningSpeedBonus <
                             sharedRunningSpeedBonus)
                {
                    sharedRunningSpeedBonus =
                        movementInfo.NativeRunningSpeedBonus;
                }

                if (!movementInfo.SupportsSynchronizedRunning)
                    synchronizeRunning = false;
            }

            if (unitTypeMovementInfoByType.Count < 2)
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

            TroopMovementFix3ModLog.Debug(
                log,
                $"Mixed-group synchronization prepared: " +
                $"tribeId={tribeId}, members={activeUnitCount}, " +
                $"unitTypes={unitTypeMovementInfoByType.Count}, " +
                $"slowestMaximumSpeedLevel={slowestMaximumSpeed}, " +
                $"cadence={synchronization.Cadence}, " +
                $"sharedRunningSpeedBonus=" +
                $"{synchronization.RunningSpeedBonus}.");
            return true;
        }

        private bool TryGetCadence(
            int tribeId,
            out SynchronizedMovementCadence cadence,
            out ushort runningSpeedBonus)
        {
            if (IsFeatureEnabled &&
                synchronizationByTribeId.TryGetValue(
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
            unitTypeMovementInfoByType.Clear();
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

        private readonly struct UnitTypeMovementInfo
        {
            public UnitTypeMovementInfo(
                bool supportsSynchronizedRunning,
                ushort nativeRunningSpeedBonus)
            {
                SupportsSynchronizedRunning =
                    supportsSynchronizedRunning;
                NativeRunningSpeedBonus =
                    nativeRunningSpeedBonus;
            }

            public bool SupportsSynchronizedRunning { get; }
            public ushort NativeRunningSpeedBonus { get; }
        }

        private readonly struct TribeSynchronization
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
