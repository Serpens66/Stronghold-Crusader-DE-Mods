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
    internal sealed unsafe class TroopMovementFix3Runtime : IDisposable
    {
        private const int ExpectedMaximumTrackedUnits = 10000;

        // The complete native tribe record stores freeUnitSpeeds at +0x56C.
        // Script Extender's GameTribe* begins +0x2A into that record.
        private const int TribeFreeUnitSpeedsOffset = 0x542;

        private readonly ManualLogSource log;
        private readonly List<IDisposable> subscriptions =
            new List<IDisposable>();
        private readonly Dictionary<int, UnitMovementDirective>
            spearmanDirectiveByUnitId =
                new Dictionary<int, UnitMovementDirective>(
                    ExpectedMaximumTrackedUnits);
        private readonly Dictionary<int, TribeMoveType>
            pendingMoveTypeByTribeId =
                new Dictionary<int, TribeMoveType>();
        private readonly List<int> unitIds =
            new List<int>(ExpectedMaximumTrackedUnits);
        private readonly List<int> activeUnitIds =
            new List<int>(ExpectedMaximumTrackedUnits);
        private readonly HashSet<eChimps> activeUnitTypes =
            new HashSet<eChimps>();

        private UnitMovementSpeedHook movementSpeedHook;
        private bool inputFailureLogged;
        private bool applied;

        public TroopMovementFix3Runtime(ManualLogSource log)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public void Apply(IntPtr libraryHandle, ReadOnlySpan<byte> memory)
        {
            if (applied)
                return;

            UnitMovementSpeedHook newMovementSpeedHook = null;
            List<IDisposable> newSubscriptions = new List<IDisposable>();

            try
            {
                newMovementSpeedHook = new UnitMovementSpeedHook(
                    log,
                    memory,
                    unchecked((ulong)libraryHandle.ToInt64()),
                    TryGetSpearmanDirective);

                newSubscriptions.Add(
                    TribeR3EventHooks.OnTribeIssueOrderMoveHere.Observable
                        .Subscribe(OnTribeIssueOrderMoveHere));

                newSubscriptions.Add(
                    TribeR3EventHooks.OnTribeIssueOrderWithTarget.Observable
                        .Where(args => args.Phase == EventHookPhase.Pre)
                        .Subscribe(OnTribeIssueOrderWithTarget));

                newSubscriptions.Add(
                    UnitR3EventHooks.OnUnitDelete.Observable
                        .Where(args => args.Phase == EventHookPhase.Pre)
                        .Subscribe(args =>
                            spearmanDirectiveByUnitId.Remove(
                                checked((int)args.UnitId))));

                newSubscriptions.Add(
                    MapLoaderR3EventHooks.OnUnloadMap.Observable
                        .Where(args => args.Phase == EventHookPhase.Post)
                        .Subscribe(_ => ClearMovementState()));

                movementSpeedHook = newMovementSpeedHook;
                subscriptions.AddRange(newSubscriptions);
                applied = true;
            }
            catch
            {
                foreach (IDisposable subscription in newSubscriptions)
                    subscription.Dispose();

                newMovementSpeedHook?.Dispose();
                ClearMovementState();
                throw;
            }

            Shared.DebugLogHelper.LogInfo(
                log,
                "Troop Movement Fix 3 active: normal movement remains Vanilla; Ctrl enables Vanilla free-unit-speeds; only Improved Spearmen in mixed DefaultInSync groups receive a speed/cadence correction. Selection and tribe rebuilds are not observed or corrected.");
        }

        public void Dispose()
        {
            if (!applied)
                return;

            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();

            subscriptions.Clear();
            ClearMovementState();
            movementSpeedHook?.Dispose();
            movementSpeedHook = null;
            applied = false;
        }

        private void OnTribeIssueOrderMoveHere(
            TribeIssueOrderMoveHereEventArgs args)
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

                if (completedMoveType == TribeMoveType.DefaultInSync)
                {
                    RemoveSpearmanDirectives(args.TribeId);
                    TryApplyImprovedSpearmanFix(
                        args.TribeId,
                        logResult: true);
                }

                return;
            }

            if (args.Phase != EventHookPhase.Pre ||
                !args.IsNewOrder ||
                args.MoveType == TribeMoveType.NoChange)
            {
                return;
            }

            RemoveSpearmanDirectives(args.TribeId);
            pendingMoveTypeByTribeId.Remove(args.TribeId);

            bool ctrlHeld = ReadCtrlModifier();
            if (ctrlHeld)
            {
                pendingMoveTypeByTribeId[args.TribeId] =
                    TribeMoveType.Fast;

                bool vanillaFreeUnitSpeedsEnabled =
                    TryEnableVanillaFreeUnitSpeeds(
                        args.TribeId,
                        out ushort previousFreeUnitSpeeds);

                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Ctrl move order: tribeId={args.TribeId}, " +
                    $"target=({args.TileX},{args.TileY}), " +
                    $"patrol={args.IsPatrolPath != 0}, " +
                    $"incomingMoveType={args.MoveType}, " +
                    $"vanillaFreeUnitSpeedsEnabled={vanillaFreeUnitSpeedsEnabled}, " +
                    $"previousFreeUnitSpeeds={previousFreeUnitSpeeds}. " +
                    "The incoming move type and all per-unit speeds remain Vanilla.");
                return;
            }

            pendingMoveTypeByTribeId[args.TribeId] = args.MoveType;
            bool provisionalSpearmanFix =
                args.MoveType == TribeMoveType.DefaultInSync &&
                TryApplyImprovedSpearmanFix(
                    args.TribeId,
                    logResult: false);

            Shared.DebugLogHelper.LogInfo(
                log,
                $"Vanilla move order: tribeId={args.TribeId}, " +
                $"target=({args.TileX},{args.TileY}), " +
                $"patrol={args.IsPatrolPath != 0}, " +
                $"moveType={args.MoveType}, " +
                $"provisionalImprovedSpearmanFix={provisionalSpearmanFix}. " +
                "No movement mode was rewritten.");
        }

        private void OnTribeIssueOrderWithTarget(
            TribeIssueOrderWithTargetEventArgs args)
        {
            pendingMoveTypeByTribeId.Remove(args.TribeId);
            int removedCount = RemoveSpearmanDirectives(args.TribeId);

            if (removedCount > 0)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Target, attack, or stop order cleared Improved Spearman directives: " +
                    $"tribeId={args.TribeId}, affectedUnits={removedCount}.");
            }
        }

        private bool TryApplyImprovedSpearmanFix(
            int tribeId,
            bool logResult)
        {
            if (!GamePlayerManagerAPI.Instance.IsImprovedSpearman() ||
                !TryCollectActiveUnits(tribeId) ||
                activeUnitTypes.Count < 2)
            {
                return false;
            }

            ushort slowestMaximumSpeed = 0;
            bool synchronizeRunning = true;
            int spearmanCount = 0;

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

                // Speed levels are delays: the largest value is the slowest
                // normal maximum speed in this group.
                if (unit->r_CurrentSpeed > slowestMaximumSpeed)
                    slowestMaximumSpeed = unit->r_CurrentSpeed;

                if (!movementSpeedHook.SupportsSynchronizedRunning(
                        unit->r_UnitChimp))
                {
                    synchronizeRunning = false;
                }

                if (unit->r_UnitChimp ==
                    eChimps.CHIMP_TYPE_SPEARMAN)
                {
                    spearmanCount++;
                }
            }

            if (spearmanCount == 0)
                return false;

            MovementCadenceMode movementMode = synchronizeRunning
                ? MovementCadenceMode.SynchronizedRunning
                : MovementCadenceMode.SynchronizedWalking;

            UnitMovementDirective directive =
                new UnitMovementDirective(
                    movementMode,
                    slowestMaximumSpeed,
                    runningSpeedBonus:
                        synchronizeRunning ? (ushort)1 : (ushort)0);

            foreach (int unitId in activeUnitIds)
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(
                        unitId,
                        out GameUnit* unit) ||
                    unit == null ||
                    unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_UnitChimp !=
                        eChimps.CHIMP_TYPE_SPEARMAN)
                {
                    continue;
                }

                spearmanDirectiveByUnitId[unitId] = directive;
            }

            if (logResult)
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"Improved Spearman mixed-group fix active: " +
                    $"tribeId={tribeId}, members={activeUnitIds.Count}, " +
                    $"unitTypes={activeUnitTypes.Count}, " +
                    $"spearmen={spearmanCount}, " +
                    $"slowestMaximumSpeedLevel={slowestMaximumSpeed}, " +
                    $"cadence={movementMode}.");
            }

            return true;
        }

        private bool TryCollectActiveUnits(int tribeId)
        {
            unitIds.Clear();
            activeUnitIds.Clear();
            activeUnitTypes.Clear();

            if (tribeId <= 0 ||
                !GameTribeManagerAPI.Instance.GetUnits(
                    tribeId,
                    unitIds))
            {
                return false;
            }

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

                activeUnitIds.Add(unitId);
                activeUnitTypes.Add(unit->r_UnitChimp);
            }

            return activeUnitIds.Count > 0;
        }

        private int RemoveSpearmanDirectives(int tribeId)
        {
            unitIds.Clear();
            if (tribeId <= 0 ||
                !GameTribeManagerAPI.Instance.GetUnits(
                    tribeId,
                    unitIds))
            {
                return 0;
            }

            int removedCount = 0;
            foreach (int unitId in unitIds)
            {
                if (spearmanDirectiveByUnitId.Remove(unitId))
                    removedCount++;
            }

            return removedCount;
        }

        private bool TryGetSpearmanDirective(
            int unitId,
            out UnitMovementDirective directive)
        {
            // This is the native hot-path rejection. No selection state, tribe
            // lookup, allocation, or logging occurs here.
            return spearmanDirectiveByUnitId.TryGetValue(
                unitId,
                out directive);
        }

        private bool TryEnableVanillaFreeUnitSpeeds(
            int tribeId,
            out ushort previousValue)
        {
            previousValue = 0;
            if (tribeId <= 0 ||
                !GameTribeManagerAPI.Instance.TryGetTribeById(
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
                        $"Could not read the Ctrl movement modifier; " +
                        $"this order remains completely Vanilla: {ex}");
                }

                return false;
            }
        }

        private void ClearMovementState()
        {
            spearmanDirectiveByUnitId.Clear();
            pendingMoveTypeByTribeId.Clear();
            unitIds.Clear();
            activeUnitIds.Clear();
            activeUnitTypes.Clear();
        }
    }
}
