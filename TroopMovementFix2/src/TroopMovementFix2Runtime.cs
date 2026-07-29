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
        private const int TribeLeaderSpeedSeed1Offset = 0x1E;
        private const int TribeLeaderSpeedSeed2Offset = 0x20;
        private const int TribeLeaderSpeedSeed3Offset = 0x22;
        private const int TribeLeaderTransitionTimerOffset = 0x24;
        private const int TribeFreeUnitSpeedsOffset = 0x542;
        private const int TribeMinimumSpeedOffset = 0x54C;
        private const int TribeMovementSpeedOffset = 0x54E;
        private const int TribeMaximumSpeedOffset = 0x550;
        private const int TribeMovementState1Offset = 0x552;
        private const int TribeMovementState2Offset = 0x556;
        private const int TribePatrolModeOffset = 0x558;
        private const int TribeMovementState3Offset = 0x55A;
        private const int TribeAverageSpeedOffset = 0x55C;
        private const int TribeMovementState4Offset = 0x55E;

        private readonly ManualLogSource log;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();

        // A real Vanilla order establishes the authoritative movement state. The
        // observations let Fix2 recognize the later per-unit recalculation caused
        // by selection, without scanning units continuously.
        private readonly Dictionary<int, MovementObservation> movementObservationByUnitId =
            new Dictionary<int, MovementObservation>(ExpectedMaximumTrackedUnits);
        private readonly Dictionary<int, SelectionTribePreservation> selectionTribeById =
            new Dictionary<int, SelectionTribePreservation>();
        private readonly Dictionary<int, ExistingSelectionTribeCandidate> existingSelectionTribeById =
            new Dictionary<int, ExistingSelectionTribeCandidate>();

        // Improved Spearmen receive a permanent directive for their real mixed-group
        // order. Other DefaultInSync members carry only a dormant fallback which is
        // visible while Vanilla temporarily assigns the concrete unit to tribe 0
        // during selection rebuilding. One registration table keeps the native
        // movement hot path at a single Unit-ID dictionary lookup.
        private readonly Dictionary<int, MovementDirectiveRegistration> movementDirectiveByUnitId =
            new Dictionary<int, MovementDirectiveRegistration>(ExpectedMaximumTrackedUnits);

        private readonly Dictionary<int, TribeMoveType> pendingMoveTypeByTribeId =
            new Dictionary<int, TribeMoveType>();
        private readonly List<int> unitIds = new List<int>(ExpectedMaximumTrackedUnits);
        private readonly List<int> activeUnitIds = new List<int>(ExpectedMaximumTrackedUnits);
        private readonly HashSet<eChimps> activeUnitTypes = new HashSet<eChimps>();

        private UnitMovementSpeedHook movementSpeedHook;
        private SelectionDiagnosticsHook selectionDiagnosticsHook;
        private TribeSelectionSpeedHook tribeSelectionSpeedHook;
        private SelectionUnitTypeMovementGuard selectionUnitTypeMovementGuard;
        private int selectionDiagnosticGeneration;
        private int pendingDiagnosticUnits;
        private int observedDiagnosticUnits;
        private int changedDiagnosticUnits;
        private int tribeZeroFallbackUnits;
        private int tribeViewGuardUnits;
        private int detailedDiagnosticLines;
        private int assignmentDiagnosticLines;
        private string currentSelectionDiagnosticSource;
        private bool selectionPreservationArmed;
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
            TribeSelectionSpeedHook newTribeSelectionSpeedHook = null;
            SelectionUnitTypeMovementGuard newSelectionUnitTypeMovementGuard = null;
            var newSubscriptions = new List<IDisposable>();

            try
            {
                newMovementSpeedHook = new UnitMovementSpeedHook(
                    log,
                    memory,
                    unchecked((ulong)libraryHandle.ToInt64()),
                    TryGetMovementDirective);

                newSelectionDiagnosticsHook = new SelectionDiagnosticsHook(
                    log,
                    memory,
                    unchecked((ulong)libraryHandle.ToInt64()),
                    OnSelectionChangedForDiagnostics);

                newTribeSelectionSpeedHook = new TribeSelectionSpeedHook(
                    log,
                    memory,
                    unchecked((ulong)libraryHandle.ToInt64()),
                    OnSelectionTribeRecalculated,
                    OnSelectionTribeStateCopied);

                newSelectionUnitTypeMovementGuard =
                    new SelectionUnitTypeMovementGuard(
                        log,
                        memory,
                        unchecked((ulong)libraryHandle.ToInt64()),
                        TryGetSelectionTribeView);

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
                tribeSelectionSpeedHook = newTribeSelectionSpeedHook;
                selectionUnitTypeMovementGuard =
                    newSelectionUnitTypeMovementGuard;
                subscriptions.AddRange(newSubscriptions);
                applied = true;
            }
            catch
            {
                foreach (IDisposable subscription in newSubscriptions)
                    subscription.Dispose();

                newSelectionUnitTypeMovementGuard?.Dispose();
                newTribeSelectionSpeedHook?.Dispose();
                newSelectionDiagnosticsHook?.Dispose();
                newMovementSpeedHook?.Dispose();
                ClearMovementState();
                throw;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                "Troop Movement Fix 2 active: normal orders remain unchanged; Ctrl enables Vanilla free-unit-speeds; a transient Vanilla tribe view prevents type handlers from treating selection-only tribe 0 as free running; DefaultInSync cadence fallback is exposed only while Vanilla temporarily leaves a unit in tribe 0 during selection rebuilding; compatible rebuilt tribes inherit the current Vanilla movement-continuation state during final native initialization; Improved Spearmen remain synchronized in mixed groups.");
        }

        public void Dispose()
        {
            if (!applied)
                return;

            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();

            subscriptions.Clear();
            ClearMovementState();
            selectionUnitTypeMovementGuard?.Dispose();
            selectionUnitTypeMovementGuard = null;
            tribeSelectionSpeedHook?.Dispose();
            tribeSelectionSpeedHook = null;
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

                // The final tribe composition and Vanilla movement fields are
                // authoritative. Replace all provisional directives with a dormant
                // tribe-0 fallback derived from this completed real order, then
                // reapply the permanent Improved Spearman exception if needed.
                RemoveMovementDirectives(args.TribeId);
                if (completedMoveType == TribeMoveType.DefaultInSync)
                {
                    TryInstallDefaultInSyncTribeZeroFallback(args.TribeId);
                    TryApplyImprovedSpearmanFix(args.TribeId, logResult: true);
                }

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
            ResetSelectionTribePreservation("new real movement order");
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
            ResetSelectionTribePreservation("target, attack, or stop order");
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

            RegisterSelectionTribeAssignment(args);

            if (args.Phase == EventHookPhase.Post &&
                args.TribeId > 0 &&
                movementObservationByUnitId.TryGetValue(
                    args.UnitId,
                    out MovementObservation assignedObservation))
            {
                assignedObservation.LastKnownValidTribeId =
                    checked((ushort)args.TribeId);
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

            if (!TryCaptureTribeMovementSnapshot(
                    tribeId,
                    out TribeMovementSnapshot tribeMovementSnapshot))
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Could not capture Vanilla tribe movement fields after a real order: " +
                    $"tribeId={tribeId}, moveType={moveType}. Selection preservation " +
                    "will remain disabled for these units.");
                return;
            }

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
                    new MovementObservation(
                        moveType,
                        CaptureSnapshot(unit),
                        tribeMovementSnapshot);
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
            ResetSelectionTribePreservation(
                "superseded by another selection change");
            existingSelectionTribeById.Clear();

            selectionDiagnosticGeneration++;
            selectionPreservationArmed = true;
            currentSelectionDiagnosticSource = source;
            pendingDiagnosticUnits = 0;
            observedDiagnosticUnits = 0;
            changedDiagnosticUnits = 0;
            tribeZeroFallbackUnits = 0;
            tribeViewGuardUnits = 0;
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
                entry.Value.HasPreSelectionTribeSnapshot = false;
                pendingDiagnosticUnits++;

                if (unit->r_TribeId != 0)
                    entry.Value.LastKnownValidTribeId = unit->r_TribeId;

                if (TryCaptureTribeMovementSnapshot(
                        unit->r_TribeId,
                        out TribeMovementSnapshot currentTribeSnapshot))
                {
                    entry.Value.PreSelectionTribeSnapshot =
                        currentTribeSnapshot;
                    entry.Value.HasPreSelectionTribeSnapshot = true;
                    RegisterExistingSelectionTribeCandidate(
                        unit->r_TribeId,
                        currentTribeSnapshot);
                }
            }

            PreserveExistingSelectionTribes();

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Selection diagnostic armed: generation={selectionDiagnosticGeneration}, " +
                $"source={source}, selectedUnits={selectedUnitCount}, " +
                $"trackedMovingUnits={pendingDiagnosticUnits}. Per-unit movement values were not changed.");
        }

        private void RegisterExistingSelectionTribeCandidate(
            int tribeId,
            TribeMovementSnapshot currentTribeSnapshot)
        {
            if (tribeId <= 0)
                return;

            if (!existingSelectionTribeById.TryGetValue(
                    tribeId,
                    out ExistingSelectionTribeCandidate candidate))
            {
                candidate = new ExistingSelectionTribeCandidate(
                    currentTribeSnapshot);
                existingSelectionTribeById[tribeId] = candidate;
            }

            candidate.ObservedUnitCount++;
            if (!candidate.MovementSnapshot.Equals(
                    currentTribeSnapshot))
            {
                candidate.DifferentMovementStateCount++;
                candidate.Compatible = false;
            }
        }

        private void PreserveExistingSelectionTribes()
        {
            int candidateCount = existingSelectionTribeById.Count;
            int restoredCount = 0;
            int unchangedCount = 0;
            int rejectedCount = 0;

            foreach (KeyValuePair<int, ExistingSelectionTribeCandidate> entry in
                existingSelectionTribeById)
            {
                int tribeId = entry.Key;
                ExistingSelectionTribeCandidate candidate = entry.Value;
                if (!candidate.Compatible)
                {
                    rejectedCount++;
                    continue;
                }

                unitIds.Clear();
                if (!GameTribeManagerAPI.Instance.GetUnits(tribeId, unitIds))
                {
                    rejectedCount++;
                    continue;
                }

                int aliveMemberCount = 0;
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

                    aliveMemberCount++;
                    if (!movementObservationByUnitId.TryGetValue(
                            unitId,
                            out MovementObservation observation) ||
                        !observation.HasPreSelectionTribeSnapshot)
                    {
                        candidate.UnknownMemberCount++;
                        candidate.Compatible = false;
                        break;
                    }

                    if (!candidate.MovementSnapshot.Equals(
                            observation.PreSelectionTribeSnapshot))
                    {
                        candidate.DifferentMovementStateCount++;
                        candidate.Compatible = false;
                        break;
                    }
                }

                if (!candidate.Compatible ||
                    aliveMemberCount == 0 ||
                    aliveMemberCount != candidate.ObservedUnitCount ||
                    !TryCaptureTribeMovementSnapshot(
                        tribeId,
                        out TribeMovementSnapshot recalculatedSnapshot))
                {
                    rejectedCount++;
                    continue;
                }

                if (recalculatedSnapshot.Equals(candidate.MovementSnapshot))
                {
                    unchangedCount++;
                    continue;
                }

                if (!TryWriteTribeMovementSnapshot(
                        tribeId,
                        candidate.MovementSnapshot))
                {
                    rejectedCount++;
                    continue;
                }

                restoredCount++;
                TribeMovementSnapshot restored = candidate.MovementSnapshot;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Preserved existing Vanilla tribe movement at selection boundary: " +
                    $"generation={selectionDiagnosticGeneration}, tribeId={tribeId}, " +
                    $"verifiedMembers={aliveMemberCount}, " +
                    $"freeUnitSpeeds={recalculatedSnapshot.FreeUnitSpeeds}->{restored.FreeUnitSpeeds}, " +
                    $"minimumSpeed={recalculatedSnapshot.MinimumSpeed}->{restored.MinimumSpeed}, " +
                    $"movementSpeed={recalculatedSnapshot.MovementSpeed}->{restored.MovementSpeed}, " +
                    $"maximumSpeed={recalculatedSnapshot.MaximumSpeed}->{restored.MaximumSpeed}, " +
                    $"averageSpeed={recalculatedSnapshot.AverageSpeed}->{restored.AverageSpeed}.");
            }

            existingSelectionTribeById.Clear();
            if (candidateCount > 0)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Existing selection tribe preservation summary: " +
                    $"generation={selectionDiagnosticGeneration}, candidates={candidateCount}, " +
                    $"restored={restoredCount}, unchanged={unchangedCount}, " +
                    $"rejected={rejectedCount}.");
            }
        }

        private void RegisterSelectionTribeAssignment(
            TribeAssignUnitEventArgs args)
        {
            if (!selectionPreservationArmed ||
                args.Phase != EventHookPhase.Pre)
            {
                return;
            }

            if (!selectionTribeById.TryGetValue(
                    args.TribeId,
                    out SelectionTribePreservation preservation))
            {
                preservation = new SelectionTribePreservation(
                    selectionDiagnosticGeneration);
                selectionTribeById[args.TribeId] = preservation;
            }

            preservation.AssignedUnitCount++;
            if (!movementObservationByUnitId.TryGetValue(
                    args.UnitId,
                    out MovementObservation observation) ||
                !observation.HasPreSelectionTribeSnapshot)
            {
                preservation.UntrackedUnitCount++;
                preservation.Compatible = false;
                return;
            }

            preservation.TrackedUnitCount++;
            if (!preservation.HasMovementSnapshot)
            {
                preservation.MovementSnapshot =
                    observation.PreSelectionTribeSnapshot;
                preservation.HasMovementSnapshot = true;
                return;
            }

            if (!preservation.MovementSnapshot.Equals(
                    observation.PreSelectionTribeSnapshot))
            {
                preservation.DifferentMovementStateCount++;
                preservation.Compatible = false;
            }
        }

        private void OnSelectionTribeRecalculated(
            int tribeId,
            string source)
        {
            if (!selectionPreservationArmed ||
                !selectionTribeById.TryGetValue(
                    tribeId,
                    out SelectionTribePreservation preservation) ||
                preservation.Generation != selectionDiagnosticGeneration ||
                !preservation.Compatible ||
                !preservation.HasMovementSnapshot)
            {
                return;
            }

            if (!TryCaptureTribeMovementSnapshot(
                    tribeId,
                    out TribeMovementSnapshot recalculatedSnapshot) ||
                !TryWriteTribeMovementSnapshot(
                    tribeId,
                    preservation.MovementSnapshot))
            {
                preservation.WriteFailureCount++;
                preservation.Compatible = false;
                return;
            }

            preservation.RestoreCount++;
            preservation.LastRecalculatedSnapshot = recalculatedSnapshot;
            preservation.LastSource = source;

            // Logarithmic diagnostics remain useful even for very large selections
            // without producing one line per assigned unit.
            if ((preservation.RestoreCount &
                 (preservation.RestoreCount - 1)) == 0)
            {
                TribeMovementSnapshot restored =
                    preservation.MovementSnapshot;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Preserved Vanilla tribe movement after selection rebuild: " +
                    $"generation={selectionDiagnosticGeneration}, source={source}, " +
                    $"tribeId={tribeId}, restorations={preservation.RestoreCount}, " +
                    $"assignedTrackedUnits={preservation.TrackedUnitCount}, " +
                    $"freeUnitSpeeds={recalculatedSnapshot.FreeUnitSpeeds}->{restored.FreeUnitSpeeds}, " +
                    $"minimumSpeed={recalculatedSnapshot.MinimumSpeed}->{restored.MinimumSpeed}, " +
                    $"movementSpeed={recalculatedSnapshot.MovementSpeed}->{restored.MovementSpeed}, " +
                    $"maximumSpeed={recalculatedSnapshot.MaximumSpeed}->{restored.MaximumSpeed}, " +
                    $"averageSpeed={recalculatedSnapshot.AverageSpeed}->{restored.AverageSpeed}.");
            }
        }

        private void OnSelectionTribeStateCopied(int tribeId)
        {
            if (!selectionPreservationArmed ||
                !selectionTribeById.TryGetValue(
                    tribeId,
                    out SelectionTribePreservation preservation) ||
                preservation.Generation != selectionDiagnosticGeneration ||
                !preservation.Compatible ||
                !preservation.HasMovementSnapshot)
            {
                return;
            }

            if (!TryCaptureTribeMovementSnapshot(
                    tribeId,
                    out TribeMovementSnapshot templateSnapshot) ||
                !TryWriteTribeMovementSnapshot(
                    tribeId,
                    preservation.MovementSnapshot))
            {
                preservation.WriteFailureCount++;
                preservation.Compatible = false;
                return;
            }

            preservation.FinalStateCopyCount++;
            preservation.LastSource = "CopySelectionTribeState";
            TribeMovementSnapshot inherited = preservation.MovementSnapshot;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"Inherited previous Vanilla movement state during final selection-tribe initialization: " +
                $"generation={selectionDiagnosticGeneration}, tribeId={tribeId}, " +
                $"assignedTrackedUnits={preservation.TrackedUnitCount}, " +
                $"freeUnitSpeeds={templateSnapshot.FreeUnitSpeeds}->{inherited.FreeUnitSpeeds}, " +
                $"movementSpeed={templateSnapshot.MovementSpeed}->{inherited.MovementSpeed}, " +
                $"patrolMode={templateSnapshot.PatrolMode}->{inherited.PatrolMode}, " +
                $"leaderTransitionTimer={templateSnapshot.LeaderTransitionTimer}->{inherited.LeaderTransitionTimer}, " +
                $"nativeMovementStateChanged={!templateSnapshot.Equals(inherited)}.");
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

                movementDirectiveByUnitId[unitId] =
                    new MovementDirectiveRegistration(
                        spearmanDirective,
                        unit,
                        onlyWhileUnitHasNoTribe: false);
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

        private bool TryInstallDefaultInSyncTribeZeroFallback(int tribeId)
        {
            if (!TryCaptureTribeMovementSnapshot(
                    tribeId,
                    out TribeMovementSnapshot tribeMovementSnapshot) ||
                !TryCollectActiveUnits(tribeId))
            {
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Could not prepare DefaultInSync tribe-zero fallback: tribeId={tribeId}.");
                return false;
            }

            bool improvedSpearmen =
                GamePlayerManagerAPI.Instance.IsImprovedSpearman();
            bool synchronizeRunning = true;
            foreach (int unitId in activeUnitIds)
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(
                        unitId,
                        out GameUnit* unit) ||
                    unit == null ||
                    unit->r_AliveState != AliveState.IsAlive)
                {
                    continue;
                }

                bool supportsRunning =
                    movementSpeedHook.SupportsSynchronizedRunning(
                        unit->r_UnitChimp);
                if (!supportsRunning ||
                    (unit->r_UnitChimp == eChimps.CHIMP_TYPE_SPEARMAN &&
                     !improvedSpearmen))
                {
                    synchronizeRunning = false;
                }
            }

            MovementCadenceMode movementMode = synchronizeRunning
                ? MovementCadenceMode.SynchronizedRunning
                : MovementCadenceMode.SynchronizedWalking;

            int registeredUnitCount = 0;
            foreach (int unitId in activeUnitIds)
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(
                        unitId,
                        out GameUnit* unit) ||
                    unit == null ||
                    unit->r_AliveState != AliveState.IsAlive)
                {
                    continue;
                }

                ushort runningSpeedBonus = synchronizeRunning
                    ? movementSpeedHook.GetNativeRunningSpeedBonus(
                        unit->r_UnitChimp,
                        improvedSpearmen)
                    : (ushort)0;
                UnitMovementDirective fallbackDirective =
                    new UnitMovementDirective(
                        movementMode,
                        tribeMovementSnapshot.MovementSpeed,
                        runningSpeedBonus);

                movementDirectiveByUnitId[unitId] =
                    new MovementDirectiveRegistration(
                        fallbackDirective,
                        unit,
                        onlyWhileUnitHasNoTribe: true);
                registeredUnitCount++;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Prepared DefaultInSync tribe-zero fallback from completed Vanilla order: " +
                $"tribeId={tribeId}, members={registeredUnitCount}, " +
                $"synchronizedSpeedLevel={tribeMovementSnapshot.MovementSpeed}, " +
                $"cadence={movementMode}, onlyWhileUnitHasNoTribe=true.");
            return registeredUnitCount > 0;
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
                bool removedDirective =
                    movementDirectiveByUnitId.Remove(unitId);
                if (removedObservation || removedDirective)
                {
                    removedUnitCount++;
                }
            }

            return removedUnitCount;
        }

        private void RemoveMovementDirectives(int tribeId)
        {
            unitIds.Clear();
            if (!GameTribeManagerAPI.Instance.GetUnits(tribeId, unitIds))
                return;

            foreach (int unitId in unitIds)
                movementDirectiveByUnitId.Remove(unitId);
        }

        private void RemoveUnitState(int unitId)
        {
            movementObservationByUnitId.Remove(unitId);
            movementDirectiveByUnitId.Remove(unitId);
        }

        private bool TryGetMovementDirective(
            int unitId,
            out UnitMovementDirective movementDirective)
        {
            if (movementDirectiveByUnitId.TryGetValue(
                    unitId,
                    out MovementDirectiveRegistration registration))
            {
                bool registrationIsActive =
                    !registration.OnlyWhileUnitHasNoTribe ||
                    registration.UnitPointer != IntPtr.Zero &&
                    ((GameUnit*)registration.UnitPointer)->r_AliveState ==
                        AliveState.IsAlive &&
                    ((GameUnit*)registration.UnitPointer)->r_TribeId == 0;

                if (registrationIsActive)
                {
                    if (registration.OnlyWhileUnitHasNoTribe &&
                        selectionDiagnosticGeneration > 0 &&
                        registration.LastSelectionGeneration !=
                            selectionDiagnosticGeneration)
                    {
                        registration.LastSelectionGeneration =
                            selectionDiagnosticGeneration;
                        tribeZeroFallbackUnits++;
                    }

                    movementDirective = registration.Directive;
                    LogVanillaMovementCalculationIfPending(unitId);
                    return true;
                }
            }

            LogVanillaMovementCalculationIfPending(unitId);
            movementDirective = default;
            return false;
        }

        private bool TryGetSelectionTribeView(
            int unitId,
            out ushort tribeId)
        {
            tribeId = 0;
            if (!selectionPreservationArmed ||
                !movementObservationByUnitId.TryGetValue(
                    unitId,
                    out MovementObservation observation) ||
                observation.LastKnownValidTribeId == 0)
            {
                return false;
            }

            tribeId = observation.LastKnownValidTribeId;
            if (observation.LastTribeViewGuardGeneration !=
                selectionDiagnosticGeneration)
            {
                observation.LastTribeViewGuardGeneration =
                    selectionDiagnosticGeneration;
                tribeViewGuardUnits++;
            }

            return true;
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

        private bool TryCaptureTribeMovementSnapshot(
            int tribeId,
            out TribeMovementSnapshot snapshot)
        {
            snapshot = default;
            if (!GameTribeManagerAPI.Instance.TryGetTribeById(
                    tribeId,
                    out GameTribe* tribe) ||
                tribe == null)
            {
                return false;
            }

            byte* tribeBytes = (byte*)tribe;
            snapshot = new TribeMovementSnapshot(
                *(ushort*)(tribeBytes + TribeLeaderSpeedSeed1Offset),
                *(ushort*)(tribeBytes + TribeLeaderSpeedSeed2Offset),
                *(ushort*)(tribeBytes + TribeLeaderSpeedSeed3Offset),
                *(ushort*)(tribeBytes + TribeLeaderTransitionTimerOffset),
                *(ushort*)(tribeBytes + TribeFreeUnitSpeedsOffset),
                *(ushort*)(tribeBytes + TribeMinimumSpeedOffset),
                *(ushort*)(tribeBytes + TribeMovementSpeedOffset),
                *(ushort*)(tribeBytes + TribeMaximumSpeedOffset),
                *(uint*)(tribeBytes + TribeMovementState1Offset),
                *(ushort*)(tribeBytes + TribeMovementState2Offset),
                *(ushort*)(tribeBytes + TribePatrolModeOffset),
                *(ushort*)(tribeBytes + TribeMovementState3Offset),
                *(ushort*)(tribeBytes + TribeAverageSpeedOffset),
                *(ushort*)(tribeBytes + TribeMovementState4Offset));
            return true;
        }

        private bool TryWriteTribeMovementSnapshot(
            int tribeId,
            TribeMovementSnapshot snapshot)
        {
            if (!GameTribeManagerAPI.Instance.TryGetTribeById(
                    tribeId,
                    out GameTribe* tribe) ||
                tribe == null)
            {
                return false;
            }

            byte* tribeBytes = (byte*)tribe;
            *(ushort*)(tribeBytes + TribeLeaderSpeedSeed1Offset) =
                snapshot.LeaderSpeedSeed1;
            *(ushort*)(tribeBytes + TribeLeaderSpeedSeed2Offset) =
                snapshot.LeaderSpeedSeed2;
            *(ushort*)(tribeBytes + TribeLeaderSpeedSeed3Offset) =
                snapshot.LeaderSpeedSeed3;
            *(ushort*)(tribeBytes + TribeLeaderTransitionTimerOffset) =
                snapshot.LeaderTransitionTimer;
            *(ushort*)(tribeBytes + TribeFreeUnitSpeedsOffset) =
                snapshot.FreeUnitSpeeds;
            *(ushort*)(tribeBytes + TribeMinimumSpeedOffset) =
                snapshot.MinimumSpeed;
            *(ushort*)(tribeBytes + TribeMovementSpeedOffset) =
                snapshot.MovementSpeed;
            *(ushort*)(tribeBytes + TribeMaximumSpeedOffset) =
                snapshot.MaximumSpeed;
            *(uint*)(tribeBytes + TribeMovementState1Offset) =
                snapshot.MovementState1;
            *(ushort*)(tribeBytes + TribeMovementState2Offset) =
                snapshot.MovementState2;
            *(ushort*)(tribeBytes + TribePatrolModeOffset) =
                snapshot.PatrolMode;
            *(ushort*)(tribeBytes + TribeMovementState3Offset) =
                snapshot.MovementState3;
            *(ushort*)(tribeBytes + TribeAverageSpeedOffset) =
                snapshot.AverageSpeed;
            *(ushort*)(tribeBytes + TribeMovementState4Offset) =
                snapshot.MovementState4;
            return true;
        }

        private void ResetSelectionTribePreservation(string reason)
        {
            if (selectionTribeById.Count > 0)
            {
                foreach (KeyValuePair<int, SelectionTribePreservation> entry in
                    selectionTribeById)
                {
                    SelectionTribePreservation preservation = entry.Value;
                    Shared.DebugLogHelper.LogInfo(
                        log,
                        $"Selection tribe preservation summary: " +
                        $"generation={preservation.Generation}, tribeId={entry.Key}, " +
                        $"assignedUnits={preservation.AssignedUnitCount}, " +
                        $"trackedUnits={preservation.TrackedUnitCount}, " +
                        $"untrackedUnits={preservation.UntrackedUnitCount}, " +
                        $"differentMovementStates={preservation.DifferentMovementStateCount}, " +
                        $"restorations={preservation.RestoreCount}, " +
                        $"finalStateCopies={preservation.FinalStateCopyCount}, " +
                        $"writeFailures={preservation.WriteFailureCount}, " +
                        $"compatible={preservation.Compatible}, " +
                        $"lastSource={preservation.LastSource ?? "none"}, reason={reason}.");
                }
            }

            selectionTribeById.Clear();
            selectionPreservationArmed = false;
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
                    $"transitionTimer={beforeSelection.TransitionTimer}->{afterVanillaCalculation.TransitionTimer}, " +
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
                $"tribeZeroFallbackUnits={tribeZeroFallbackUnits}, " +
                $"tribeViewGuardUnits={tribeViewGuardUnits}, " +
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
                unit->N000000AA,
                unit->r_SpeedBonus,
                unit->N000000F4,
                unit->r_AI_LastIssuedTribeCommand);
        }

        private void ClearMovementState()
        {
            LogPendingDiagnosticSummary("movement state cleared");
            ResetSelectionTribePreservation("movement state cleared");
            movementObservationByUnitId.Clear();
            movementDirectiveByUnitId.Clear();
            existingSelectionTribeById.Clear();
            pendingMoveTypeByTribeId.Clear();
            unitIds.Clear();
            activeUnitIds.Clear();
            activeUnitTypes.Clear();
            selectionDiagnosticGeneration = 0;
            pendingDiagnosticUnits = 0;
            observedDiagnosticUnits = 0;
            changedDiagnosticUnits = 0;
            tribeZeroFallbackUnits = 0;
            tribeViewGuardUnits = 0;
            detailedDiagnosticLines = 0;
            assignmentDiagnosticLines = 0;
            currentSelectionDiagnosticSource = null;
            selectionPreservationArmed = false;
        }

        private sealed class MovementObservation
        {
            public MovementObservation(
                TribeMoveType moveType,
                MovementSnapshot afterOrderSnapshot,
                TribeMovementSnapshot afterOrderTribeSnapshot)
            {
                MoveType = moveType;
                AfterOrderSnapshot = afterOrderSnapshot;
                AfterOrderTribeSnapshot = afterOrderTribeSnapshot;
                PreSelectionSnapshot = afterOrderSnapshot;
                LastKnownValidTribeId = afterOrderSnapshot.TribeId;
            }

            public TribeMoveType MoveType { get; }
            public MovementSnapshot AfterOrderSnapshot { get; }
            public TribeMovementSnapshot AfterOrderTribeSnapshot { get; }
            public MovementSnapshot PreSelectionSnapshot { get; set; }
            public TribeMovementSnapshot PreSelectionTribeSnapshot { get; set; }
            public bool HasPreSelectionTribeSnapshot { get; set; }
            public int PendingSelectionGeneration { get; set; }
            public ushort LastKnownValidTribeId { get; set; }
            public int LastTribeViewGuardGeneration { get; set; }
        }

        private sealed class MovementDirectiveRegistration
        {
            public MovementDirectiveRegistration(
                UnitMovementDirective directive,
                GameUnit* unit,
                bool onlyWhileUnitHasNoTribe)
            {
                Directive = directive;
                UnitPointer = (IntPtr)unit;
                OnlyWhileUnitHasNoTribe = onlyWhileUnitHasNoTribe;
            }

            public UnitMovementDirective Directive { get; }
            public IntPtr UnitPointer { get; }
            public bool OnlyWhileUnitHasNoTribe { get; }
            public int LastSelectionGeneration { get; set; }
        }

        private sealed class SelectionTribePreservation
        {
            public SelectionTribePreservation(int generation)
            {
                Generation = generation;
                Compatible = true;
            }

            public int Generation { get; }
            public bool Compatible { get; set; }
            public bool HasMovementSnapshot { get; set; }
            public TribeMovementSnapshot MovementSnapshot { get; set; }
            public TribeMovementSnapshot LastRecalculatedSnapshot { get; set; }
            public int AssignedUnitCount { get; set; }
            public int TrackedUnitCount { get; set; }
            public int UntrackedUnitCount { get; set; }
            public int DifferentMovementStateCount { get; set; }
            public int RestoreCount { get; set; }
            public int FinalStateCopyCount { get; set; }
            public int WriteFailureCount { get; set; }
            public string LastSource { get; set; }
        }

        private sealed class ExistingSelectionTribeCandidate
        {
            public ExistingSelectionTribeCandidate(
                TribeMovementSnapshot movementSnapshot)
            {
                MovementSnapshot = movementSnapshot;
                Compatible = true;
            }

            public TribeMovementSnapshot MovementSnapshot { get; }
            public bool Compatible { get; set; }
            public int ObservedUnitCount { get; set; }
            public int UnknownMemberCount { get; set; }
            public int DifferentMovementStateCount { get; set; }
        }

        private readonly struct TribeMovementSnapshot : IEquatable<TribeMovementSnapshot>
        {
            public TribeMovementSnapshot(
                ushort leaderSpeedSeed1,
                ushort leaderSpeedSeed2,
                ushort leaderSpeedSeed3,
                ushort leaderTransitionTimer,
                ushort freeUnitSpeeds,
                ushort minimumSpeed,
                ushort movementSpeed,
                ushort maximumSpeed,
                uint movementState1,
                ushort movementState2,
                ushort patrolMode,
                ushort movementState3,
                ushort averageSpeed,
                ushort movementState4)
            {
                LeaderSpeedSeed1 = leaderSpeedSeed1;
                LeaderSpeedSeed2 = leaderSpeedSeed2;
                LeaderSpeedSeed3 = leaderSpeedSeed3;
                LeaderTransitionTimer = leaderTransitionTimer;
                FreeUnitSpeeds = freeUnitSpeeds;
                MinimumSpeed = minimumSpeed;
                MovementSpeed = movementSpeed;
                MaximumSpeed = maximumSpeed;
                MovementState1 = movementState1;
                MovementState2 = movementState2;
                PatrolMode = patrolMode;
                MovementState3 = movementState3;
                AverageSpeed = averageSpeed;
                MovementState4 = movementState4;
            }

            public ushort LeaderSpeedSeed1 { get; }
            public ushort LeaderSpeedSeed2 { get; }
            public ushort LeaderSpeedSeed3 { get; }
            public ushort LeaderTransitionTimer { get; }
            public ushort FreeUnitSpeeds { get; }
            public ushort MinimumSpeed { get; }
            public ushort MovementSpeed { get; }
            public ushort MaximumSpeed { get; }
            public uint MovementState1 { get; }
            public ushort MovementState2 { get; }
            public ushort PatrolMode { get; }
            public ushort MovementState3 { get; }
            public ushort AverageSpeed { get; }
            public ushort MovementState4 { get; }

            public bool Equals(TribeMovementSnapshot other)
            {
                return LeaderSpeedSeed1 == other.LeaderSpeedSeed1 &&
                       LeaderSpeedSeed2 == other.LeaderSpeedSeed2 &&
                       LeaderSpeedSeed3 == other.LeaderSpeedSeed3 &&
                       LeaderTransitionTimer == other.LeaderTransitionTimer &&
                       FreeUnitSpeeds == other.FreeUnitSpeeds &&
                       MinimumSpeed == other.MinimumSpeed &&
                       MovementSpeed == other.MovementSpeed &&
                       MaximumSpeed == other.MaximumSpeed &&
                       MovementState1 == other.MovementState1 &&
                       MovementState2 == other.MovementState2 &&
                       PatrolMode == other.PatrolMode &&
                       MovementState3 == other.MovementState3 &&
                       AverageSpeed == other.AverageSpeed &&
                       MovementState4 == other.MovementState4;
            }

            public override bool Equals(object obj)
            {
                return obj is TribeMovementSnapshot other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = LeaderSpeedSeed1;
                    hashCode = (hashCode * 397) ^ LeaderSpeedSeed2;
                    hashCode = (hashCode * 397) ^ LeaderSpeedSeed3;
                    hashCode = (hashCode * 397) ^ LeaderTransitionTimer;
                    hashCode = (hashCode * 397) ^ FreeUnitSpeeds;
                    hashCode = (hashCode * 397) ^ MinimumSpeed;
                    hashCode = (hashCode * 397) ^ MovementSpeed;
                    hashCode = (hashCode * 397) ^ MaximumSpeed;
                    hashCode = (hashCode * 397) ^ (int)MovementState1;
                    hashCode = (hashCode * 397) ^ MovementState2;
                    hashCode = (hashCode * 397) ^ PatrolMode;
                    hashCode = (hashCode * 397) ^ MovementState3;
                    hashCode = (hashCode * 397) ^ AverageSpeed;
                    hashCode = (hashCode * 397) ^ MovementState4;
                    return hashCode;
                }
            }
        }

        private readonly struct MovementSnapshot
        {
            public MovementSnapshot(
                ushort tribeId,
                ushort maximumSpeed,
                ushort effectiveSpeed,
                ushort transitionTimer,
                ushort speedBonus,
                uint animationState,
                ushort lastIssuedTribeCommand)
            {
                TribeId = tribeId;
                MaximumSpeed = maximumSpeed;
                EffectiveSpeed = effectiveSpeed;
                TransitionTimer = transitionTimer;
                SpeedBonus = speedBonus;
                AnimationState = animationState;
                LastIssuedTribeCommand = lastIssuedTribeCommand;
            }

            public ushort TribeId { get; }
            public ushort MaximumSpeed { get; }
            public ushort EffectiveSpeed { get; }
            public ushort TransitionTimer { get; }
            public ushort SpeedBonus { get; }
            public uint AnimationState { get; }
            public ushort LastIssuedTribeCommand { get; }

            public bool HasMovementDifference(MovementSnapshot other)
            {
                return TribeId != other.TribeId ||
                       MaximumSpeed != other.MaximumSpeed ||
                       EffectiveSpeed != other.EffectiveSpeed ||
                       TransitionTimer != other.TransitionTimer ||
                       SpeedBonus != other.SpeedBonus ||
                       AnimationState != other.AnimationState ||
                       LastIssuedTribeCommand != other.LastIssuedTribeCommand;
            }
        }

    }
}
