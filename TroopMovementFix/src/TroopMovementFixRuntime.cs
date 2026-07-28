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
    internal sealed unsafe class TroopMovementFixRuntime : IDisposable
    {
        private const int ExpectedMaximumTrackedUnits = 10000;

        private readonly ManualLogSource log;
        private readonly List<IDisposable> subscriptions = new List<IDisposable>();
        private readonly Dictionary<int, UnitMovementDirective> movementDirectiveByUnitId =
            new Dictionary<int, UnitMovementDirective>(ExpectedMaximumTrackedUnits);
        private readonly List<int> unitIds = new List<int>(ExpectedMaximumTrackedUnits);
        private readonly List<int> activeUnitIds = new List<int>(ExpectedMaximumTrackedUnits);

        private UnitMovementSpeedHook movementSpeedHook;
        private bool inputFailureLogged;
        private bool applied;

        public TroopMovementFixRuntime(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public void Apply(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            if (applied)
                return;

            movementSpeedHook = new UnitMovementSpeedHook(
                log,
                memory,
                unchecked((ulong)libraryHandle.ToInt64()),
                TryGetMovementDirective);

            subscriptions.Add(TribeR3EventHooks.OnTribeIssueOrderMoveHere.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(OnTribeIssueOrderMoveHere));

            subscriptions.Add(TribeR3EventHooks.OnTribeIssueOrderWithTarget.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(args => RemoveSynchronization(args.TribeId)));

            subscriptions.Add(UnitR3EventHooks.OnUnitDelete.Observable
                .Where(args => args.Phase == EventHookPhase.Pre)
                .Subscribe(args => movementDirectiveByUnitId.Remove(checked((int)args.UnitId))));

            subscriptions.Add(MapLoaderR3EventHooks.OnUnloadMap.Observable
                .Where(args => args.Phase == EventHookPhase.Post)
                .Subscribe(_ => ClearSynchronization()));

            applied = true;
            Shared.DebugLogHelper.LogDebug(
                log,
                "Troop Movement Fix active: vanilla movement without a modifier; Alt synchronizes at the slowest member's maximum speed; Ctrl lets every member use its own maximum speed. Unit-bound directives survive later selection and tribe reassignment until the affected unit's next order.");
        }

        public void Dispose()
        {
            if (!applied)
                return;

            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();

            subscriptions.Clear();
            ClearSynchronization();
            movementSpeedHook?.Dispose();
            movementSpeedHook = null;
            applied = false;
        }

        private void OnTribeIssueOrderMoveHere(TribeIssueOrderMoveHereEventArgs args)
        {
            // NoChange is used extensively by internal AI and animal movement. It is not a
            // newly synchronized player move order and must not be rewritten to Fast.
            if (args.MoveType == TribeMoveType.NoChange)
                return;

            bool altModifierHeld = false;
            bool ctrlModifierHeld = false;
            if (args.IsNewOrder)
                ReadMovementModifiers(out altModifierHeld, out ctrlModifierHeld);

            bool debugLoggingEnabled = Shared.DebugLogHelper.IsDebugEnabled();
            if (debugLoggingEnabled)
            {
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Move order received: tribeId={args.TribeId}, moveType={args.MoveType}, " +
                    $"target=({args.TileX},{args.TileY}), patrol={args.IsPatrolPath != 0}, newOrder={args.IsNewOrder}, " +
                    $"altModifierHeld={altModifierHeld}, ctrlModifierHeld={ctrlModifierHeld}.");
            }

            if (!altModifierHeld && !ctrlModifierHeld)
            {
                RemoveSynchronization(args.TribeId);

                if (debugLoggingEnabled)
                {
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        $"Vanilla movement order retained: tribeId={args.TribeId}, unchangedMoveType={args.MoveType}.");
                }

                return;
            }

            if (altModifierHeld && ctrlModifierHeld)
            {
                RemoveSynchronization(args.TribeId);

                if (debugLoggingEnabled)
                {
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        $"Alt and Ctrl were both held; ambiguous movement modifier keeps vanilla behavior: " +
                        $"tribeId={args.TribeId}, unchangedMoveType={args.MoveType}.");
                }

                return;
            }

            if (ctrlModifierHeld)
            {
                RemoveSynchronization(args.TribeId);
                if (!TryCollectActiveUnits(args.TribeId))
                {
                    Shared.DebugLogHelper.LogWarning(
                        log,
                        $"Ctrl was held, but no active members were found for maximum-speed movement of tribeId={args.TribeId}; " +
                        "the order is still rewritten to Fast without a persistent unit directive.");
                }
                else
                {
                    ApplyMovementDirectiveToActiveUnits(
                        MovementCadenceMode.UncappedRunning,
                        synchronizedSpeed: 0);
                }

                // Fast lets the game calculate every member at its own maximum speed.
                // The cadence hook additionally selects the native running state only
                // for types for which such a state was discovered.
                args.MoveType = TribeMoveType.Fast;

                if (debugLoggingEnabled)
                {
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        $"Ctrl uncapped-running order applied: tribeId={args.TribeId}, " +
                        $"members={activeUnitIds.Count}, rewrittenMoveType={args.MoveType}.");
                }

                return;
            }

            if (!TryCalculateSlowestMaximumSpeed(
                args.TribeId,
                out ushort slowestMaximumSpeed,
                out int memberCount,
                out int nativeRunningMemberCount,
                out bool synchronizeRunning))
            {
                RemoveSynchronization(args.TribeId);
                Shared.DebugLogHelper.LogWarning(
                    log,
                    $"Alt synchronization could not calculate a group speed for tribeId={args.TribeId}; " +
                    "the order keeps its incoming vanilla movement behavior.");
                return;
            }

            MovementCadenceMode movementMode = synchronizeRunning
                ? MovementCadenceMode.SynchronizedRunning
                : MovementCadenceMode.SynchronizedWalking;
            ApplyMovementDirectiveToActiveUnits(
                movementMode,
                synchronizedSpeed: slowestMaximumSpeed);

            if (!synchronizeRunning)
            {
                // Explicitly undo an incoming Fast mode as Alt requests synchronized
                // formation movement. The cadence hook suppresses any late native
                // running override for unit types such as Improved Spearmen.
                args.MoveType = TribeMoveType.DefaultInSync;

                if (debugLoggingEnabled)
                {
                    Shared.DebugLogHelper.LogDebug(
                        log,
                        $"Alt synchronized-walking order applied: tribeId={args.TribeId}, members={memberCount}, " +
                        $"membersWithNativeRunningAnimation={nativeRunningMemberCount}, " +
                        $"slowestMaximumSpeedLevel={slowestMaximumSpeed}, rewrittenMoveType={args.MoveType}.");
                }

                return;
            }

            // Fast selects every compatible unit's native running/free-speed movement
            // state. The native speed calculation hook then prevents faster members
            // from exceeding the slowest member's normal maximum speed.
            args.MoveType = TribeMoveType.Fast;

            if (debugLoggingEnabled)
            {
                Shared.DebugLogHelper.LogDebug(
                    log,
                    $"Alt synchronized-running order applied: tribeId={args.TribeId}, members={memberCount}, " +
                    $"membersWithNativeRunningAnimation={nativeRunningMemberCount}, " +
                    $"slowestMaximumSpeedLevel={slowestMaximumSpeed}, rewrittenMoveType={args.MoveType}.");
            }
        }

        private bool TryCollectActiveUnits(int tribeId)
        {
            unitIds.Clear();
            activeUnitIds.Clear();

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
            }

            return activeUnitIds.Count > 0;
        }

        private bool TryCalculateSlowestMaximumSpeed(
            int tribeId,
            out ushort slowestMaximumSpeed,
            out int memberCount,
            out int nativeRunningMemberCount,
            out bool synchronizeRunning)
        {
            slowestMaximumSpeed = 0;
            memberCount = 0;
            nativeRunningMemberCount = 0;
            synchronizeRunning = true;
            unitIds.Clear();
            activeUnitIds.Clear();

            if (!GameTribeManagerAPI.Instance.GetUnits(tribeId, unitIds))
                return false;

            bool improvedSpearmen = GamePlayerManagerAPI.Instance.IsImprovedSpearman();

            foreach (int unitId in unitIds)
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null ||
                    unit->r_AliveState != AliveState.IsAlive)
                {
                    continue;
                }

                activeUnitIds.Add(unitId);

                ushort maximumSpeedLevel = unit->r_CurrentSpeed;
                if (maximumSpeedLevel > slowestMaximumSpeed)
                    slowestMaximumSpeed = maximumSpeedLevel;

                // With the advanced option disabled, spearmen deliberately cannot run.
                // They are consequently the group's maximum-speed limiter. Vanilla already
                // synchronizes such a group correctly, so its walking cadence must remain intact.
                bool supportsRunningAnimation =
                    movementSpeedHook.SupportsSynchronizedRunning(unit->r_UnitChimp);
                if (supportsRunningAnimation)
                    nativeRunningMemberCount++;

                if (!supportsRunningAnimation ||
                    (unit->r_UnitChimp == eChimps.CHIMP_TYPE_SPEARMAN && !improvedSpearmen))
                {
                    synchronizeRunning = false;
                }

                memberCount++;
            }

            if (memberCount == 0)
                return false;

            return true;
        }

        private void RemoveSynchronization(int tribeId)
        {
            unitIds.Clear();
            if (!GameTribeManagerAPI.Instance.GetUnits(tribeId, unitIds))
                return;

            foreach (int unitId in unitIds)
                movementDirectiveByUnitId.Remove(unitId);
        }

        private void ClearSynchronization()
        {
            movementDirectiveByUnitId.Clear();
        }

        private void ApplyMovementDirectiveToActiveUnits(
            MovementCadenceMode movementMode,
            ushort synchronizedSpeed)
        {
            UnitMovementDirective movementDirective = new UnitMovementDirective(
                movementMode,
                synchronizedSpeed);

            foreach (int unitId in activeUnitIds)
                movementDirectiveByUnitId[unitId] = movementDirective;
        }

        private bool TryGetMovementDirective(
            int unitId,
            out UnitMovementDirective movementDirective)
        {
            return movementDirectiveByUnitId.TryGetValue(unitId, out movementDirective);
        }

        private void ReadMovementModifiers(
            out bool altModifierHeld,
            out bool ctrlModifierHeld)
        {
            altModifierHeld = false;
            ctrlModifierHeld = false;

            try
            {
                altModifierHeld =
                    Input.GetKey(KeyCode.LeftAlt) ||
                    Input.GetKey(KeyCode.RightAlt);
                ctrlModifierHeld =
                    Input.GetKey(KeyCode.LeftControl) ||
                    Input.GetKey(KeyCode.RightControl);
            }
            catch (Exception ex)
            {
                if (!inputFailureLogged)
                {
                    inputFailureLogged = true;
                    Shared.DebugLogHelper.LogError(
                        log,
                        $"Could not read the Alt/Ctrl movement modifiers; this move order keeps vanilla behavior: {ex}");
                }
            }
        }
    }
}
