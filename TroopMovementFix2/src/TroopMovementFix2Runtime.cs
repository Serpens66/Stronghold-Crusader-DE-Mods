using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TroopMovementFix
{
    internal sealed unsafe class TroopMovementFix2Runtime : IDisposable
    {
        private const int ExpectedMaximumTrackedUnits = 10000;
        private const int MaximumDetailedDiagnosticLinesPerSelection = 64;

        // The native wrapper addresses the field from the raw 0x688-byte tribe
        // record at +0x56C. Script Extender's GameTribe* begins 0x2A bytes into
        // that record, so the correct pointer-relative offset is 0x56C - 0x2A.
        private const int TribeFreeUnitSpeedsOffset = 0x542;

        private readonly ManualLogSource log;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();

        // Observations are diagnostic only. Unlike the working legacy mod, Fix2
        // never writes these captured speed, cadence, or animation values back.
        private readonly Dictionary<int, MovementObservation> movementObservationByUnitId =
            new Dictionary<int, MovementObservation>(ExpectedMaximumTrackedUnits);

        // Only Improved Spearmen in mixed DefaultInSync groups receive a native
        // speed/cadence directive. Every other unit is rejected by the hook lookup.
        private readonly Dictionary<int, UnitMovementDirective> spearmanDirectiveByUnitId =
            new Dictionary<int, UnitMovementDirective>();

        private readonly Dictionary<int, TribeMoveType> pendingMoveTypeByTribeId =
            new Dictionary<int, TribeMoveType>();
        private readonly List<int> unitIds = new List<int>(ExpectedMaximumTrackedUnits);
        private readonly List<int> activeUnitIds = new List<int>(ExpectedMaximumTrackedUnits);
        private readonly HashSet<eChimps> activeUnitTypes = new HashSet<eChimps>();

        private UnitMovementSpeedHook movementSpeedHook;
        private SelectionDiagnosticsHook selectionDiagnosticsHook;
        private int selectionDiagnosticGeneration;
        private int pendingDiagnosticUnits;
        private int observedDiagnosticUnits;
        private int changedDiagnosticUnits;
        private int detailedDiagnosticLines;
        private int assignmentDiagnosticLines;
        private string currentSelectionDiagnosticSource;
        private bool inputFailureLogged;
        private bool applied;

        public TroopMovementFix2Runtime(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public void Apply(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            if (applied)
                return;

            UnitMovementSpeedHook newMovementSpeedHook = null;
            SelectionDiagnosticsHook newSelectionDiagnosticsHook = null;
            var newSubscriptions = new List<IDisposable>();

            try
            {
                newMovementSpeedHook = new UnitMovementSpeedHook(
                    log,
                    memory,
                    unchecked((ulong)libraryHandle.ToInt64()),
                    TryGetSpearmanDirective);

                newSelectionDiagnosticsHook = new SelectionDiagnosticsHook(
                    log,
                    memory,
                    unchecked((ulong)libraryHandle.ToInt64()),
                    OnSelectionChangedForDiagnostics);

                newSubscriptions.Add(TribeR3EventHooks.OnTribeIssueOrderMoveHere.Observable
                    .Subscribe(OnTribeIssueOrderMoveHere));

                newSubscriptions.Add(TribeR3EventHooks.OnTribeIssueOrderWithTarget.Observable
                    .Subscribe(OnTribeIssueOrderWithTarget));

                newSubscriptions.Add(TribeR3EventHooks.OnTribeAssignUnit.Observable
                    .Subscribe(OnTribeAssignUnit));

                newSubscriptions.Add(UnitR3EventHooks.OnUnitDelete.Observable
                    .Where(args => args.Phase == EventHookPhase.Pre)
                    .Subscribe(args => RemoveUnitState(checked((int)args.UnitId))));

                newSubscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                    .Where(args => args.Phase == EventHookPhase.Post)
                    .Subscribe(_ => ClearMovementState()));

                movementSpeedHook = newMovementSpeedHook;
                selectionDiagnosticsHook = newSelectionDiagnosticsHook;
                subscriptions.AddRange(newSubscriptions);
                applied = true;
            }
            catch
            {
                foreach (IDisposable subscription in newSubscriptions)
                    subscription.Dispose();

                newSelectionDiagnosticsHook?.Dispose();
                newMovementSpeedHook?.Dispose();
                ClearMovementState();
                throw;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                "Troop Movement Fix 2 active: normal orders remain unchanged; Ctrl enables Vanilla free-unit-speeds for the ordered tribe; selection hooks are diagnostic only and never restore speed or animation values; the native correction remains restricted to Improved Spearmen in mixed synchronized groups.");
        }

        public void Dispose()
        {
            if (!applied)
                return;

            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();

            subscriptions.Clear();
            ClearMovementState();
            selectionDiagnosticsHook?.Dispose();
            selectionDiagnosticsHook = null;
            movementSpeedHook?.Dispose();
            movementSpeedHook = null;
            applied = false;
        }

        private void OnTribeIssueOrderMoveHere(TribeIssueOrderMoveHereEventArgs args)
        {
            if (args.Phase == EventHookPhase.Post)
            {
                if (!pendingMoveTypeByTribeId.TryGetValue(
                    args.TribeId,
                    out TribeMoveType completedMoveType))
                {
                    return;
                }

                pendingMoveTypeByTribeId.Remove(args.TribeId);
                CaptureVanillaMovementObservations(args.TribeId, completedMoveType);

                // The final tribe composition is authoritative. Re-evaluate only the
                // Spearman exception after Vanilla has completed assignments.
                RemoveSpearmanDirectives(args.TribeId);
                if (completedMoveType == TribeMoveType.DefaultInSync)
                    TryApplyImprovedSpearmanFix(args.TribeId, logResult: true);

                return;
            }

            if (!args.IsNewOrder)
                return;

            if (args.MoveType == TribeMoveType.NoChange)
                return;

            bool ctrlModifierHeld = ReadCtrlModifier();
            TribeMoveType incomingMoveType = args.MoveType;
            TribeMoveType effectiveMoveType = ctrlModifierHeld
                ? TribeMoveType.Fast
                : incomingMoveType;

            LogPendingDiagnosticSummary("new real movement order");
            RemoveMovementState(args.TribeId);
            pendingMoveTypeByTribeId[args.TribeId] = effectiveMoveType;

            // Vanilla's no-matched-speed wrapper does not pass a special speed value
            // to the normal order function. It sets GameTribe.freeUnitSpeeds and then
            // keeps using the ordinary movement value. Reproduce precisely that state
            // transition so all speed, terrain, cadence, and animation work stays in
            // Vanilla.
            bool vanillaFreeUnitSpeedsEnabled = false;
            ushort previousVanillaFreeUnitSpeeds = 0;
            if (ctrlModifierHeld)
            {
                vanillaFreeUnitSpeedsEnabled = TrySetVanillaFreeUnitSpeeds(
                    args.TribeId,
                    out previousVanillaFreeUnitSpeeds);
            }

            // The native Spearman bonus can run during the original order, before the
            // Post event. Install only that narrowly scoped correction in advance.
            if (effectiveMoveType == TribeMoveType.DefaultInSync)
                TryApplyImprovedSpearmanFix(args.TribeId, logResult: false);

            Shared.DebugLogHelper.LogInfo(
                log,
                $"New move order: tribeId={args.TribeId}, target=({args.TileX},{args.TileY}), " +
                $"patrol={args.IsPatrolPath != 0}, ctrlHeld={ctrlModifierHeld}, " +
                $"incomingMoveType={incomingMoveType}, rememberedMode={effectiveMoveType}, " +
                $"vanillaFreeUnitSpeedsEnabled={vanillaFreeUnitSpeedsEnabled}, " +
                $"previousFreeUnitSpeeds={previousVanillaFreeUnitSpeeds}.");
        }

        private void OnTribeIssueOrderWithTarget(TribeIssueOrderWithTargetEventArgs args)
        {
            if (args.Phase != EventHookPhase.Pre && args.Phase != EventHookPhase.Post)
                return;

            pendingMoveTypeByTribeId.Remove(args.TribeId);
            LogPendingDiagnosticSummary("target, attack, or stop order");
            int removedUnitCount = RemoveMovementState(args.TribeId);

            if (removedUnitCount > 0)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Target/attack/stop order cleared remembered movement: " +
                    $"tribeId={args.TribeId}, phase={args.Phase}, affectedUnits={removedUnitCount}.");
            }
        }

        private void OnTribeAssignUnit(TribeAssignUnitEventArgs args)
        {
            bool isRealOrderAssignment =
                pendingMoveTypeByTribeId.ContainsKey(args.TribeId);

            if (isRealOrderAssignment)
            {
                if (args.Phase == EventHookPhase.Pre)
                    movementObservationByUnitId.Remove(args.UnitId);
                return;
            }

            if (!movementObservationByUnitId.TryGetValue(
                    args.UnitId,
                    out MovementObservation observation) ||
                assignmentDiagnosticLines >= MaximumDetailedDiagnosticLinesPerSelection)
            {
                return;
            }

            ushort unitTribeId = 0;
            eChimps unitType = 0;
            if (GameUnitManagerAPI.Instance.TryGetUnitById(
                    args.UnitId,
                    out GameUnit* unit) &&
                unit != null)
            {
                unitTribeId = unit->r_TribeId;
                unitType = unit->r_UnitChimp;
            }

            assignmentDiagnosticLines++;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Tracked unit tribe assignment outside a real order: " +
                $"selectionGeneration={selectionDiagnosticGeneration}, phase={args.Phase}, " +
                $"unitId={args.UnitId}, unitType={unitType}, eventTribeId={args.TribeId}, " +
                $"unitFieldTribeId={unitTribeId}, lastOrderedMode={observation.MoveType}.");
        }

        private void CaptureVanillaMovementObservations(
            int tribeId,
            TribeMoveType moveType)
        {
            if (moveType != TribeMoveType.DefaultInSync &&
                moveType != TribeMoveType.Fast)
            {
                return;
            }

            unitIds.Clear();
            if (!GameTribeManagerAPI.Instance.GetUnits(tribeId, unitIds))
                return;

            int capturedUnitCount = 0;
            foreach (int unitId in unitIds)
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null ||
                    unit->r_AliveState != AliveState.IsAlive)
                {
                    continue;
                }

                movementObservationByUnitId[unitId] =
                    new MovementObservation(moveType, CaptureSnapshot(unit));
                capturedUnitCount++;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Captured Vanilla movement observations after real order: tribeId={tribeId}, " +
                $"moveType={moveType}, units={capturedUnitCount}.");
        }

        private void OnSelectionChangedForDiagnostics(
            string source,
            int selectedUnitCount)
        {
            LogPendingDiagnosticSummary("superseded by another selection change");

            selectionDiagnosticGeneration++;
            currentSelectionDiagnosticSource = source;
            pendingDiagnosticUnits = 0;
            observedDiagnosticUnits = 0;
            changedDiagnosticUnits = 0;
            detailedDiagnosticLines = 0;
            assignmentDiagnosticLines = 0;

            foreach (KeyValuePair<int, MovementObservation> entry in
                movementObservationByUnitId)
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(
                        entry.Key,
                        out GameUnit* unit) ||
                    unit == null ||
                    unit->r_AliveState != AliveState.IsAlive)
                {
                    continue;
                }

                entry.Value.PreSelectionSnapshot = CaptureSnapshot(unit);
                entry.Value.PendingSelectionGeneration =
                    selectionDiagnosticGeneration;
                pendingDiagnosticUnits++;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Selection diagnostic armed: generation={selectionDiagnosticGeneration}, " +
                $"source={source}, selectedUnits={selectedUnitCount}, " +
                $"trackedMovingUnits={pendingDiagnosticUnits}. No movement value was changed.");
        }

        private bool TryApplyImprovedSpearmanFix(int tribeId, bool logResult)
        {
            if (!GamePlayerManagerAPI.Instance.IsImprovedSpearman())
                return false;

            if (!TryCollectActiveUnits(tribeId) || activeUnitTypes.Count < 2)
                return false;

            ushort slowestMaximumSpeed = 0;
            bool synchronizeRunning = true;
            int spearmanCount = 0;

            foreach (int unitId in activeUnitIds)
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null ||
                    unit->r_AliveState != AliveState.IsAlive)
                {
                    continue;
                }

                if (unit->r_CurrentSpeed > slowestMaximumSpeed)
                    slowestMaximumSpeed = unit->r_CurrentSpeed;

                if (!movementSpeedHook.SupportsSynchronizedRunning(unit->r_UnitChimp))
                    synchronizeRunning = false;

                if (unit->r_UnitChimp == eChimps.CHIMP_TYPE_SPEARMAN)
                    spearmanCount++;
            }

            if (spearmanCount == 0)
                return false;

            MovementCadenceMode movementMode = synchronizeRunning
                ? MovementCadenceMode.SynchronizedRunning
                : MovementCadenceMode.SynchronizedWalking;

            UnitMovementDirective spearmanDirective = new UnitMovementDirective(
                movementMode,
                slowestMaximumSpeed,
                runningSpeedBonus: synchronizeRunning ? (ushort)1 : (ushort)0);

            foreach (int unitId in activeUnitIds)
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null ||
                    unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_UnitChimp != eChimps.CHIMP_TYPE_SPEARMAN)
                {
                    continue;
                }

                spearmanDirectiveByUnitId[unitId] = spearmanDirective;
            }

            if (logResult)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Spearman synchronized-group fix active: tribeId={tribeId}, " +
                    $"members={activeUnitIds.Count}, unitTypes={activeUnitTypes.Count}, " +
                    $"spearmen={spearmanCount}, slowestMaximumSpeedLevel={slowestMaximumSpeed}, " +
                    $"cadence={movementMode}.");
            }

            return true;
        }

        private bool TryCollectActiveUnits(int tribeId)
        {
            unitIds.Clear();
            activeUnitIds.Clear();
            activeUnitTypes.Clear();

            if (!GameTribeManagerAPI.Instance.GetUnits(tribeId, unitIds))
                return false;

            foreach (int unitId in unitIds)
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null ||
                    unit->r_AliveState != AliveState.IsAlive)
                {
                    continue;
                }

                activeUnitIds.Add(unitId);
                activeUnitTypes.Add(unit->r_UnitChimp);
            }

            return activeUnitIds.Count > 0;
        }

        private int RemoveMovementState(int tribeId)
        {
            unitIds.Clear();
            if (!GameTribeManagerAPI.Instance.GetUnits(tribeId, unitIds))
                return 0;

            int removedUnitCount = 0;
            foreach (int unitId in unitIds)
            {
                bool removedObservation =
                    movementObservationByUnitId.Remove(unitId);
                bool removedDirective = spearmanDirectiveByUnitId.Remove(unitId);
                if (removedObservation || removedDirective)
                    removedUnitCount++;
            }

            return removedUnitCount;
        }

        private void RemoveSpearmanDirectives(int tribeId)
        {
            unitIds.Clear();
            if (!GameTribeManagerAPI.Instance.GetUnits(tribeId, unitIds))
                return;

            foreach (int unitId in unitIds)
                spearmanDirectiveByUnitId.Remove(unitId);
        }

        private void RemoveUnitState(int unitId)
        {
            movementObservationByUnitId.Remove(unitId);
            spearmanDirectiveByUnitId.Remove(unitId);
        }

        private bool TryGetSpearmanDirective(
            int unitId,
            out UnitMovementDirective movementDirective)
        {
            LogVanillaMovementCalculationIfPending(unitId);
            return spearmanDirectiveByUnitId.TryGetValue(unitId, out movementDirective);
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
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Could not read the Ctrl movement modifier; movement remains Vanilla: {ex}");
                }

                return false;
            }
        }

        private bool TrySetVanillaFreeUnitSpeeds(
            int tribeId,
            out ushort previousValue)
        {
            previousValue = 0;
            if (!GameTribeManagerAPI.Instance.TryGetTribeById(
                    tribeId,
                    out GameTribe* tribe) ||
                tribe == null)
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Ctrl movement could not enable Vanilla free-unit-speeds: " +
                    $"tribeId={tribeId} was not available.");
                return false;
            }

            ushort* freeUnitSpeeds =
                (ushort*)((byte*)tribe + TribeFreeUnitSpeedsOffset);
            previousValue = *freeUnitSpeeds;
            *freeUnitSpeeds = 1;
            return true;
        }

        private void LogVanillaMovementCalculationIfPending(int unitId)
        {
            if (pendingDiagnosticUnits <= 0 ||
                !movementObservationByUnitId.TryGetValue(
                    unitId,
                    out MovementObservation observation) ||
                observation.PendingSelectionGeneration !=
                    selectionDiagnosticGeneration)
            {
                return;
            }

            observation.PendingSelectionGeneration = 0;
            observedDiagnosticUnits++;

            if (!GameUnitManagerAPI.Instance.TryGetUnitById(
                    unitId,
                    out GameUnit* unit) ||
                unit == null ||
                unit->r_AliveState != AliveState.IsAlive)
            {
                TryCompleteSelectionDiagnostic();
                return;
            }

            MovementSnapshot afterVanillaCalculation = CaptureSnapshot(unit);
            MovementSnapshot beforeSelection = observation.PreSelectionSnapshot;
            if (!beforeSelection.HasMovementDifference(afterVanillaCalculation))
            {
                TryCompleteSelectionDiagnostic();
                return;
            }

            changedDiagnosticUnits++;
            if (detailedDiagnosticLines <
                MaximumDetailedDiagnosticLinesPerSelection)
            {
                detailedDiagnosticLines++;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Vanilla movement state changed after selection: " +
                    $"generation={selectionDiagnosticGeneration}, " +
                    $"source={currentSelectionDiagnosticSource}, unitId={unitId}, " +
                    $"unitType={unit->r_UnitChimp}, orderedMode={observation.MoveType}, " +
                    $"tribe={beforeSelection.TribeId}->{afterVanillaCalculation.TribeId}, " +
                    $"maximumSpeed={beforeSelection.MaximumSpeed}->{afterVanillaCalculation.MaximumSpeed}, " +
                    $"effectiveSpeed={beforeSelection.EffectiveSpeed}->{afterVanillaCalculation.EffectiveSpeed}, " +
                    $"speedBonus={beforeSelection.SpeedBonus}->{afterVanillaCalculation.SpeedBonus}, " +
                    $"animation=0x{beforeSelection.AnimationState:X}->0x{afterVanillaCalculation.AnimationState:X}, " +
                    $"lastCommand={beforeSelection.LastIssuedTribeCommand}->{afterVanillaCalculation.LastIssuedTribeCommand}, " +
                    $"effectiveSpeedAtOrder={observation.AfterOrderSnapshot.EffectiveSpeed}.");
            }

            TryCompleteSelectionDiagnostic();
        }

        private void TryCompleteSelectionDiagnostic()
        {
            if (pendingDiagnosticUnits <= 0 ||
                observedDiagnosticUnits < pendingDiagnosticUnits)
            {
                return;
            }

            LogPendingDiagnosticSummary(
                "all tracked units reached Vanilla movement calculation");
        }

        private void LogPendingDiagnosticSummary(string reason)
        {
            if (pendingDiagnosticUnits <= 0)
                return;

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Selection movement diagnostic summary: " +
                $"generation={selectionDiagnosticGeneration}, " +
                $"source={currentSelectionDiagnosticSource}, " +
                $"observedMovementCalculations={observedDiagnosticUnits}/{pendingDiagnosticUnits}, " +
                $"unitsWithChangedMovementState={changedDiagnosticUnits}, " +
                $"assignmentLines={assignmentDiagnosticLines}, " +
                $"detailLines={detailedDiagnosticLines}, reason={reason}.");

            pendingDiagnosticUnits = 0;
        }

        private static MovementSnapshot CaptureSnapshot(GameUnit* unit)
        {
            return new MovementSnapshot(
                unit->r_TribeId,
                unit->r_CurrentSpeed,
                unit->r_CurrentSpeed2,
                unit->r_SpeedBonus,
                unit->N000000F4,
                unit->r_AI_LastIssuedTribeCommand);
        }

        private void ClearMovementState()
        {
            LogPendingDiagnosticSummary("movement state cleared");
            movementObservationByUnitId.Clear();
            spearmanDirectiveByUnitId.Clear();
            pendingMoveTypeByTribeId.Clear();
            unitIds.Clear();
            activeUnitIds.Clear();
            activeUnitTypes.Clear();
            selectionDiagnosticGeneration = 0;
            pendingDiagnosticUnits = 0;
            observedDiagnosticUnits = 0;
            changedDiagnosticUnits = 0;
            detailedDiagnosticLines = 0;
            assignmentDiagnosticLines = 0;
            currentSelectionDiagnosticSource = null;
        }

        private sealed class MovementObservation
        {
            public MovementObservation(
                TribeMoveType moveType,
                MovementSnapshot afterOrderSnapshot)
            {
                MoveType = moveType;
                AfterOrderSnapshot = afterOrderSnapshot;
                PreSelectionSnapshot = afterOrderSnapshot;
            }

            public TribeMoveType MoveType { get; }
            public MovementSnapshot AfterOrderSnapshot { get; }
            public MovementSnapshot PreSelectionSnapshot { get; set; }
            public int PendingSelectionGeneration { get; set; }
        }

        private readonly struct MovementSnapshot
        {
            public MovementSnapshot(
                ushort tribeId,
                ushort maximumSpeed,
                ushort effectiveSpeed,
                ushort speedBonus,
                uint animationState,
                ushort lastIssuedTribeCommand)
            {
                TribeId = tribeId;
                MaximumSpeed = maximumSpeed;
                EffectiveSpeed = effectiveSpeed;
                SpeedBonus = speedBonus;
                AnimationState = animationState;
                LastIssuedTribeCommand = lastIssuedTribeCommand;
            }

            public ushort TribeId { get; }
            public ushort MaximumSpeed { get; }
            public ushort EffectiveSpeed { get; }
            public ushort SpeedBonus { get; }
            public uint AnimationState { get; }
            public ushort LastIssuedTribeCommand { get; }

            public bool HasMovementDifference(MovementSnapshot other)
            {
                return TribeId != other.TribeId ||
                       MaximumSpeed != other.MaximumSpeed ||
                       EffectiveSpeed != other.EffectiveSpeed ||
                       SpeedBonus != other.SpeedBonus ||
                       AnimationState != other.AnimationState ||
                       LastIssuedTribeCommand != other.LastIssuedTribeCommand;
            }
        }

    }
}
