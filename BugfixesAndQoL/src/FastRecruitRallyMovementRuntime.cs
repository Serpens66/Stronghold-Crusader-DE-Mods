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
        // RALLY_ANIMATION_DIAGNOSTICS_BEGIN
        private readonly ManualLogSource rallyAnimationDiagnosticsLog;
        // RALLY_ANIMATION_DIAGNOSTICS_END
        private readonly IMovementCadenceServices movementPatch;
        private readonly List<int> tribeUnitIds = new List<int>();
        private readonly List<IDisposable> subscriptions =
            new List<IDisposable>(5);
        private readonly GameUnit* unitArray;
        private readonly int unitArrayLength;
        private bool enabled = true;
        private bool disposed;

        public FastRecruitRallyMovementRuntime(
            ManualLogSource log,
            IMovementCadenceServices movementPatch)
        {
            if (log == null)
                throw new ArgumentNullException(nameof(log));
            // RALLY_ANIMATION_DIAGNOSTICS_BEGIN
            rallyAnimationDiagnosticsLog = log;
            // RALLY_ANIMATION_DIAGNOSTICS_END
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

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            foreach (IDisposable subscription in subscriptions)
                subscription.Dispose();

            subscriptions.Clear();
            ClearTracking();
        }

        public void SetEnabled(bool value)
        {
            enabled = value;
            if (!enabled)
                ClearTracking();
        }

        private void OnTribeIssueOrderMoveHere(
            TribeIssueOrderMoveHereEventArgs args)
        {
            if (enabled &&
                args.Phase == EventHookPhase.Pre &&
                args.IsNewOrder &&
                args.MoveType != TribeMoveType.NoChange)
            {
                RemoveTrackingForTribe(args.TribeId);
            }
        }

        private void OnTribeIssueOrderWithTarget(
            TribeIssueOrderWithTargetEventArgs args)
        {
            // RALLY_ANIMATION_DIAGNOSTICS_BEGIN
            if (args.AICommand == TribeAICommand.UnitStop)
                LogRallyAnimationDiagnosticsUnitStop(args);
            // RALLY_ANIMATION_DIAGNOSTICS_END
            if (enabled && args.Phase == EventHookPhase.Pre)
            {
                RemoveTrackingForTribe(args.TribeId);
            }
        }

        // RALLY_ANIMATION_DIAGNOSTICS_BEGIN
        private void LogRallyAnimationDiagnosticsUnitStop(
            TribeIssueOrderWithTargetEventArgs args)
        {
            tribeUnitIds.Clear();
            bool resolved = args.TribeId > 0 &&
                GameTribeManagerAPI.Instance.GetUnits(
                    args.TribeId,
                    tribeUnitIds);
            var units = new System.Text.StringBuilder();
            if (resolved)
            {
                foreach (int unitId in tribeUnitIds)
                {
                    if (units.Length != 0)
                        units.Append(';');
                    if (unitId <= 0 || unitId > unitArrayLength)
                    {
                        units.Append("id=").Append(unitId).Append(",invalid");
                        continue;
                    }

                    GameUnit* unit = unitArray + unitId - 1;
                    units.Append("id=").Append(unitId)
                        .Append(",alive=").Append((ushort)unit->r_AliveState)
                        .Append(",type=").Append((ushort)unit->r_UnitChimp)
                        .Append(",owner=").Append(unit->r_ControllableForPlayerId)
                        .Append(",global=").Append(unit->r_GlobalId)
                        .Append(",ai=").Append(unit->r_AIState)
                        .Append(",transform=").Append(
                            (ushort)unit->r_TransformIntoUnitOfType)
                        .Append(",path=0x").Append(
                            unit->r_PathPlanStateBitFlags.ToString("X"))
                        .Append(",pathIndex=").Append(
                            unit->r_CurrentPathPlanIndex)
                        .Append(",pathLength=").Append(unit->r_PathPlanLength)
                        .Append(",target=(").Append(
                            unit->r_TargetTilePositionX)
                        .Append(',').Append(unit->r_TargetTilePositionY)
                        .Append("),animation=0x").Append(
                            unit->r_SpriteAnimationGroup.ToString("X"))
                        .Append(",bonus=").Append(unit->r_SpeedBonus);
                }
            }

            string returnValue = args.Phase == EventHookPhase.Post
                ? args.ReturnValue.ToString()
                : "pending";
            Shared.DebugLogHelper.LogInfo(
                rallyAnimationDiagnosticsLog,
                "RALLY_ANIMATION_DIAGNOSTICS UnitStop " +
                $"phase={args.Phase}, command={args.AICommand}/" +
                $"{(int)args.AICommand}, tribe={args.TribeId}, " +
                $"target1={args.TargetValue1}, target2={args.TargetValue2}, " +
                $"a6={args.a6}, return={returnValue}, " +
                $"resolved={resolved}, units=[{units}].");
            tribeUnitIds.Clear();
        }
        // RALLY_ANIMATION_DIAGNOSTICS_END

        private void OnUnitDelete(UnitDeleteEventArgs args)
        {
            if (enabled && args.Phase == EventHookPhase.Pre)
            {
                RemoveTracking(unchecked((int)args.UnitId));
            }
        }

        private void OnUnitTransition(UnitTransitionEventArgs args)
        {
            if (!enabled ||
                args.Phase != EventHookPhase.Pre ||
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
                ClearTracking();
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

            int ownerPlayerId = unit->r_ControllableForPlayerId;
            GamePlayerManagerAPI players = GamePlayerManagerAPI.Instance;
            if (!players.IsPlayerIdValid(ownerPlayerId) ||
                players.IsAIPlayer(ownerPlayerId))
            {
                return;
            }

            RemoveTracking(unitId);
            movementPatch.SetRallyTracking(
                unitId,
                unit->r_GlobalId,
                ownerPlayerId,
                expectedUnitType);
        }

        private void RemoveTrackingForTribe(int tribeId)
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
                RemoveTracking(unitId);

            tribeUnitIds.Clear();
        }

        private void RemoveTracking(int unitId)
        {
            movementPatch.ClearRallyTracking(unitId);
        }

        private void ClearTracking()
        {
            movementPatch.ClearAllRallyTracking();
            tribeUnitIds.Clear();
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

    }
}
