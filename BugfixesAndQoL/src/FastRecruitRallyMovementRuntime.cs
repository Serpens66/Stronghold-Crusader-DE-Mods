// Feature: Make newly recruited units run to their rally points.
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
    /// <summary>
    /// Keeps recruits associated with their rally flag until the player gives
    /// them an explicit order and applies their native individual run cadence.
    /// </summary>
    internal sealed unsafe class FastRecruitRallyMovementRuntime : IDisposable
    {
        private const ushort UnitInitializationAiState = 109;
        private const ushort ActivePathPlanState = 2;

        private readonly ManualLogSource log;
        private readonly IMovementCadenceServices movementPatch;
        private readonly Dictionary<ulong, RecruitRallyTracking>
            trackingByUnitAddress =
                new Dictionary<ulong, RecruitRallyTracking>();
        private readonly Dictionary<int, RecruitRallyTracking>
            trackingByUnitId =
                new Dictionary<int, RecruitRallyTracking>();
        private readonly List<int> tribeUnitIds = new List<int>();
        private readonly List<IDisposable> subscriptions =
            new List<IDisposable>(5);
        private readonly GameUnit* unitArray;
        private readonly int unitArrayLength;
        private bool disposed;

        public FastRecruitRallyMovementRuntime(
            ManualLogSource log,
            IMovementCadenceServices movementPatch)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.movementPatch = movementPatch ??
                throw new ArgumentNullException(nameof(movementPatch));

            var units = GameUnitManagerAPI.Instance.GetUnitArray();
            unitArray = units._array;
            unitArrayLength = units.Length;
            if (unitArray == null || unitArrayLength <= 0)
            {
                throw new InvalidOperationException(
                    "The native unit array is unavailable.");
            }

            try
            {
                // Do not use OnUnitMoveHere here: Vanilla also emits it for
                // the automatic barracks/outpost rally route itself.
                subscriptions.Add(
                    TribeR3EventHooks.OnTribeIssueOrderMoveHere.Observable
                        .Subscribe(OnTribeIssueOrderMoveHere));
                subscriptions.Add(
                    TribeR3EventHooks.OnTribeIssueOrderWithTarget.Observable
                        .Subscribe(OnTribeIssueOrderWithTarget));
                subscriptions.Add(
                    UnitR3EventHooks.OnUnitDelete.Observable
                        .Subscribe(OnUnitDelete));
                subscriptions.Add(
                    UnitR3EventHooks.OnUnitTransition.Observable
                        .Subscribe(OnUnitTransition));
                subscriptions.Add(
                    MapLoaderR3EventHooks.OnUnloadMap.Observable
                        .Subscribe(OnUnloadMap));
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void ApplyMaximumSpeed(IntPtr unitAddress)
        {
            GameUnit* unit = (GameUnit*)unitAddress.ToPointer();
            if (disposed || unit == null ||
                !trackingByUnitAddress.TryGetValue(
                    unchecked((ulong)unit),
                    out RecruitRallyTracking tracking) ||
                !IsMatchingTrackedUnit(unit, tracking) ||
                (unit->r_PathPlanStateBitFlags & ActivePathPlanState) == 0)
            {
                return;
            }

            // This callback runs before Vanilla's late terrain/status stage.
            // Reset only the preceding rally/group cap; later modifiers remain.
            unit->r_CurrentSpeed2 = unit->r_CurrentSpeed;
        }

        public bool TryApplyRunningCadence(IntPtr unitAddress)
        {
            return TryApplyRunningCadenceNative((GameUnit*)unitAddress.ToPointer());
        }

        private bool TryApplyRunningCadenceNative(GameUnit* unit)
        {
            if (disposed || unit == null)
                return false;

            ulong unitAddress = unchecked((ulong)unit);
            if (!trackingByUnitAddress.TryGetValue(
                    unitAddress,
                    out RecruitRallyTracking tracking))
            {
                return false;
            }

            if (tracking.GlobalId != 0 &&
                unit->r_GlobalId != tracking.GlobalId)
            {
                RemoveTracking(tracking.UnitId, "unit slot reused");
                return false;
            }

            if (unit->r_UnitChimp != tracking.ExpectedUnitType)
            {
                // The transition event precedes Vanilla's pooled-unit
                // transformation. Initialization is not rally movement.
                if (unit->r_AIState == UnitInitializationAiState ||
                    unit->r_TransformIntoUnitOfType ==
                        tracking.ExpectedUnitType)
                {
                    return true;
                }

                FastRecruitRallyMovementModLog.Debug(
                    log,
                    $"Fast recruit rally tracking cancelled: " +
                    $"unitId={tracking.UnitId}, " +
                    $"unitType={unit->r_UnitChimp}, " +
                    $"expectedUnitType={tracking.ExpectedUnitType}.");
                RemoveTracking(tracking.UnitId, reason: null);
                return false;
            }

            bool hasActivePath =
                (unit->r_PathPlanStateBitFlags & ActivePathPlanState) != 0;
            if (!hasActivePath)
            {
                if (tracking.IsMovingToRally)
                {
                    tracking.IsMovingToRally = false;
                    FastRecruitRallyMovementModLog.Debug(
                        log,
                        $"Fast recruit waiting at rally flag: " +
                        $"unitId={tracking.UnitId}, " +
                        $"unitType={unit->r_UnitChimp}.");
                }

                return true;
            }

            ushort targetTileX = unit->r_TargetTilePositionX;
            ushort targetTileY = unit->r_TargetTilePositionY;
            if (!tracking.HasObservedRallyMovement)
            {
                tracking.HasObservedRallyMovement = true;
                tracking.GlobalId = unit->r_GlobalId;
                FastRecruitRallyMovementModLog.Debug(
                    log,
                    $"Fast recruit rally movement started: " +
                    $"unitId={tracking.UnitId}, " +
                    $"unitType={unit->r_UnitChimp}, " +
                    $"targetTile={targetTileX},{targetTileY}.");
            }
            else if (!tracking.IsMovingToRally)
            {
                if (targetTileX != tracking.TargetTileX ||
                    targetTileY != tracking.TargetTileY)
                {
                    RemoveTracking(
                        tracking.UnitId,
                        "movement restarted with a different target");
                    return false;
                }

                FastRecruitRallyMovementModLog.Debug(
                    log,
                    $"Fast recruit rally movement restarted: " +
                    $"unitId={tracking.UnitId}, " +
                    $"targetTile={targetTileX},{targetTileY}.");
            }
            else if (targetTileX != tracking.TargetTileX ||
                     targetTileY != tracking.TargetTileY)
            {
                FastRecruitRallyMovementModLog.Debug(
                    log,
                    $"Fast recruit rally movement retargeted: " +
                    $"unitId={tracking.UnitId}, " +
                    $"targetTile={targetTileX},{targetTileY}.");
            }

            tracking.IsMovingToRally = true;
            tracking.TargetTileX = targetTileX;
            tracking.TargetTileY = targetTileY;

            bool improvedSpearmen =
                unit->r_UnitChimp == eChimps.CHIMP_TYPE_SPEARMAN &&
                GamePlayerManagerAPI.Instance.IsImprovedSpearman();
            bool hasRunningSpeedBonus =
                movementPatch.TryGetNativeRunningSpeedBonus(
                    unit->r_UnitChimp,
                    improvedSpearmen,
                    out ushort runningSpeedBonus);
            bool hasRunningState =
                movementPatch.TryGetNativeRunningState(
                    unit->r_UnitChimp,
                    unit->N000000F4,
                    out uint runningState);

            // Both values belong to the same decoded native fast-move case;
            // applying only one would desynchronize speed and animation.
            if (hasRunningSpeedBonus && hasRunningState)
            {
                unit->r_SpeedBonus = runningSpeedBonus;
                unit->N000000F4 = runningState;
            }

            return true;
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();

            subscriptions.Clear();
            ClearTracking("option disabled");
        }

        private void OnTribeIssueOrderMoveHere(
            TribeIssueOrderMoveHereEventArgs args)
        {
            if (args.Phase == EventHookPhase.Pre &&
                args.IsNewOrder &&
                args.MoveType != TribeMoveType.NoChange)
            {
                RemoveTrackingForTribe(
                    args.TribeId,
                    "new movement order");
            }
        }

        private void OnTribeIssueOrderWithTarget(
            TribeIssueOrderWithTargetEventArgs args)
        {
            if (args.Phase == EventHookPhase.Pre)
            {
                RemoveTrackingForTribe(
                    args.TribeId,
                    $"target order {args.AICommand}");
            }
        }

        private void OnUnitDelete(UnitDeleteEventArgs args)
        {
            if (args.Phase == EventHookPhase.Pre)
            {
                RemoveTracking(
                    unchecked((int)args.UnitId),
                    "unit deleted");
            }
        }

        private void OnUnitTransition(UnitTransitionEventArgs args)
        {
            if (args.Phase != EventHookPhase.Pre ||
                (args.Source != UnitTransitionSource.MercenaryOutpost &&
                 args.Source != UnitTransitionSource.EuropeanBarracks))
            {
                return;
            }

            // Worker assignments and disbanding share this transition event,
            // so only the complete set of recruitable troop types is tracked.
            TrackRecruit(args.UnitId, args.NextUnitType);
        }

        private void OnUnloadMap(MapUnloadEventArgs args)
        {
            if (args.Phase == EventHookPhase.Post)
                ClearTracking("map unloaded");
        }

        private void TrackRecruit(int unitId, eChimps expectedUnitType)
        {
            if (unitId <= 0 ||
                unitId > unitArrayLength ||
                !IsRecruitableUnitType(expectedUnitType))
            {
                return;
            }

            GameUnit* unit = unitArray + unitId - 1;
            if (unit->r_AliveState != AliveState.IsAlive)
                return;

            ulong unitAddress = unchecked((ulong)unit);
            RemoveTracking(unitId, reason: null);
            if (trackingByUnitAddress.TryGetValue(
                    unitAddress,
                    out RecruitRallyTracking addressCollision))
            {
                RemoveTracking(addressCollision.UnitId, reason: null);
            }

            RecruitRallyTracking tracking = new RecruitRallyTracking(
                unitId,
                unitAddress,
                unit->r_GlobalId,
                expectedUnitType);
            trackingByUnitAddress[unitAddress] = tracking;
            trackingByUnitId[unitId] = tracking;

            FastRecruitRallyMovementModLog.Debug(
                log,
                $"Fast recruit rally tracking added: unitId={unitId}, " +
                $"unitType={expectedUnitType}.");
        }

        private void RemoveTrackingForTribe(int tribeId, string reason)
        {
            tribeUnitIds.Clear();
            if (tribeId <= 0 ||
                !GameTribeManagerAPI.Instance.GetUnits(
                    tribeId,
                    tribeUnitIds))
            {
                return;
            }

            foreach (int unitId in tribeUnitIds)
                RemoveTracking(unitId, reason);

            tribeUnitIds.Clear();
        }

        private void RemoveTracking(int unitId, string reason)
        {
            if (!trackingByUnitId.TryGetValue(
                    unitId,
                    out RecruitRallyTracking tracking))
            {
                return;
            }

            trackingByUnitId.Remove(unitId);
            if (trackingByUnitAddress.TryGetValue(
                    tracking.UnitAddress,
                    out RecruitRallyTracking addressTracking) &&
                ReferenceEquals(addressTracking, tracking))
            {
                trackingByUnitAddress.Remove(tracking.UnitAddress);
            }

            if (reason != null)
            {
                FastRecruitRallyMovementModLog.Debug(
                    log,
                    $"Fast recruit rally tracking removed: " +
                    $"unitId={tracking.UnitId}, reason={reason}.");
            }
        }

        private void ClearTracking(string reason)
        {
            int removedCount = trackingByUnitId.Count;
            trackingByUnitId.Clear();
            trackingByUnitAddress.Clear();
            tribeUnitIds.Clear();

            if (removedCount != 0)
            {
                FastRecruitRallyMovementModLog.Debug(
                    log,
                    $"Fast recruit rally tracking cleared: " +
                    $"units={removedCount}, reason={reason}.");
            }
        }

        private static bool IsMatchingTrackedUnit(
            GameUnit* unit,
            RecruitRallyTracking tracking)
        {
            return unit != null &&
                   unit->r_AliveState == AliveState.IsAlive &&
                   unchecked((ulong)unit) == tracking.UnitAddress &&
                   unit->r_UnitChimp == tracking.ExpectedUnitType &&
                   (tracking.GlobalId == 0 ||
                    unit->r_GlobalId == tracking.GlobalId);
        }

        private static bool IsRecruitableUnitType(eChimps unitType)
        {
            switch (unitType)
            {
                case eChimps.CHIMP_TYPE_ENGINEER:
                case eChimps.CHIMP_TYPE_TUNNELER:
                case eChimps.CHIMP_TYPE_LADDERMAN:
                case eChimps.CHIMP_TYPE_MONK:
                case eChimps.CHIMP_TYPE_ARCHER:
                case eChimps.CHIMP_TYPE_XBOWMAN:
                case eChimps.CHIMP_TYPE_SPEARMAN:
                case eChimps.CHIMP_TYPE_PIKEMAN:
                case eChimps.CHIMP_TYPE_MACEMAN:
                case eChimps.CHIMP_TYPE_SWORDSMAN:
                case eChimps.CHIMP_TYPE_KNIGHT:
                case eChimps.CHIMP_TYPE_ARAB_BOW:
                case eChimps.CHIMP_TYPE_ARAB_SLAVE:
                case eChimps.CHIMP_TYPE_ARAB_SLINGER:
                case eChimps.CHIMP_TYPE_ARAB_ASSASIN:
                case eChimps.CHIMP_TYPE_ARAB_HORSEMAN:
                case eChimps.CHIMP_TYPE_ARAB_SWORDSMAN:
                case eChimps.CHIMP_TYPE_ARAB_GRENADIER:
                case eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER:
                case eChimps.CHIMP_TYPE_BEDOUIN_HEALER:
                case eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH:
                case eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER:
                case eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER:
                case eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL:
                case eChimps.CHIMP_TYPE_BEDOUIN_SAPPER:
                case eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER:
                    return true;
                default:
                    return false;
            }
        }

        private sealed class RecruitRallyTracking
        {
            public RecruitRallyTracking(
                int unitId,
                ulong unitAddress,
                uint globalId,
                eChimps expectedUnitType)
            {
                UnitId = unitId;
                UnitAddress = unitAddress;
                GlobalId = globalId;
                ExpectedUnitType = expectedUnitType;
            }

            public int UnitId { get; }
            public ulong UnitAddress { get; }
            public uint GlobalId { get; set; }
            public eChimps ExpectedUnitType { get; }
            public bool HasObservedRallyMovement { get; set; }
            public bool IsMovingToRally { get; set; }
            public ushort TargetTileX { get; set; }
            public ushort TargetTileY { get; set; }
        }
    }
}
