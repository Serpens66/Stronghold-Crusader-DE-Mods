using BepInEx.Logging;
using APIShared;
using RedBird.Backends.NativeX64;
using RedBird.Abstractions.Hooks;
using RedBird.Abstractions.Hooks.Transaction;
using RedBird.Core.Memory;
using RedBird.X64.Assembly;
using RedBird.X64.Hooks;
using RedBird.X64.Hooks.Transaction;
using R3;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.EventAPI.Units;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime : IDisposable
    {
        internal int RunCentralMovementPlanWithContext(
            IntPtr unitManager, int unitId, int targetX, int targetY)
        {
            if (disposed || unitManager == IntPtr.Zero || unitId <= 0)
                return originalCentralMovementPlan(unitManager, unitId, targetX, targetY);

            MarkTrackedAttackPipeline(unitId, AttackPipelineStage.Planner, targetX, targetY, false);
            if (!APIShared.UnitAccess.TryGetById(unitId, out GameUnit* plannerUnit, out _) ||
                plannerUnit == null || !CanDigMoat(plannerUnit))
            {
                return originalCentralMovementPlan(unitManager, unitId, targetX, targetY);
            }

            PlanScope previous = activePlan;
            PlanScope previousPending = pendingPlan;
            PlanScope inherited = previous ?? pendingPlan;
            PlanScope plan = inherited != null &&
                (inherited.PostCombatRepath || inherited.MoatWorkMovement) &&
                inherited.UnitId == unitId && inherited.TargetX == targetX &&
                inherited.TargetY == targetY
                    ? inherited
                    : new PlanScope(unitId, targetX, targetY);
            if (activeMoveCommand != null)
                activeMoveCommand.CentralPlannerCalls++;
            if (!RequiredOnlyMode && activeMoveCommand == null && !plan.FriendlyRouteQualified &&
                !plan.OwnerRouteProbeCompleted)
            {
                try
                {
                    if (!TryFindRequiredFriendlyCompletedMoatRouteForPlan(
                        plan, out RouteProbeSummary summary))
                    {
                        LogRejectedPlannerRoute(plan, summary);
                        return originalCentralMovementPlan(unitManager, unitId, targetX, targetY);
                    }

                    plan.FriendlyRouteQualified = true;
                    try
                    {
                        if (ShouldLogUnitPipeline)
                            LogPipelineDiagnostic(
                                $"stage=planner-owner-qualified unit={unitId} player={plan.PlayerId} " +
                                $"target=({targetX},{targetY}) {summary.ToLogFields()}");
                    }
                    catch
                    {
                        // Diagnostics must not reject an otherwise qualified planner scope.
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        LogFailure("planner-owner-qualification", ex);
                    }
                    catch
                    {
                        // Never let diagnostics escape across the native planner callback.
                    }
                    return originalCentralMovementPlan(unitManager, unitId, targetX, targetY);
                }
            }

            activePlan = plan;
            try
            {
                return originalCentralMovementPlan(unitManager, unitId, targetX, targetY);
            }
            finally
            {
                activePlan = previous;
                pendingPlan = previous != null ? previousPending : null;
            }
        }

        internal void ResumeMovementAfterCombatWithMoatContext(
            IntPtr unitManager, int unitId)
        {
            GameUnit* unit = null;
            try
            {
                if (disposed || unitManager == IntPtr.Zero || unitId <= 0 ||
                    unitManager != (IntPtr)nativeUnitManager ||
                    !APIShared.UnitAccess.TryGetById(unitId, out unit, out _) ||
                    unit == null || !APIShared.UnitAccess.IsReallyAlive(unit) ||
                    !CanDigMoat(unit) ||
                    *(short*)((byte*)unit + UnitCombatFinishGateOffset) != 0 ||
                    *(short*)((byte*)unit + UnitGroupInactiveStateOffset) != 0 ||
                    *(short*)((byte*)unit + UnitPostCombatMovementStateOffset) == 3)
                {
                    originalCombatFinishResume(unitManager, unitId);
                    return;
                }
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("post-combat-context", ex);
                originalCombatFinishResume(unitManager, unitId);
                return;
            }

            int targetX = unit->r_TargetTilePositionX2;
            int targetY = unit->r_TargetTilePositionY2;
            if ((uint)targetX >= MapWidth || (uint)targetY >= MapWidth ||
                (targetX == unit->r_CurrentTilePositionX &&
                 targetY == unit->r_CurrentTilePositionY))
            {
                originalCombatFinishResume(unitManager, unitId);
                return;
            }

            var plan = new PlanScope(unitId, targetX, targetY)
            {
                PlayerId = unit->r_ControllableForPlayerId,
                PostCombatRepath = true
            };
            RouteProbeSummary requiredRouteSummary = default;
            bool requiredFriendlyMoat = false;
            try
            {
                requiredFriendlyMoat =
                    TryFindRequiredFriendlyCompletedMoatRouteForPlan(
                        plan, out requiredRouteSummary);
                plan.OwnerRouteProbeCompleted = true;
                plan.FriendlyRouteQualified = requiredFriendlyMoat;
            }
            catch (Exception ex)
            {
                // A failed owner probe must never suppress Vanilla's post-combat resume.
                TryLogDiagnosticFailure("post-combat-owner-probe", ex);
            }

            PlanScope previousActivePlan = activePlan;
            PlanScope previousPendingPlan = pendingPlan;
            activePlan = plan;
            pendingPlan = plan;
            try
            {
                MarkPostCombatRepathEntered(unitId, unit, plan, requiredFriendlyMoat);
                LogDetailedInfo(
                    $"Bugfixes and QoL stage=friendly-moat-movement-post-combat-repath-entered unit={unitId} " +
                    $"type={unit->r_UnitChimp} player={unit->r_ControllableForPlayerId} " +
                    $"tribe={unit->r_TribeId} aiState={unit->r_AIState} " +
                    $"current=({unit->r_CurrentTilePositionX},{unit->r_CurrentTilePositionY}) " +
                    $"savedTarget=({targetX},{targetY}) " +
                    $"requiredFriendlyMoat={requiredFriendlyMoat} " +
                    $"routeProbe={requiredRouteSummary.ToLogFields()}.");
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("post-combat-enter-diagnostic", ex);
            }

            try
            {
                // Vanilla restores the saved state and calls MoveHere itself. Keeping this
                // scope alive around that call supports both required and merely faster
                // friendly-moat routes without blocking an otherwise valid ground route.
                originalCombatFinishResume(unitManager, unitId);

                try
                {
                    // If the original tracker no longer existed, the synchronous builder may
                    // have created it during the repath. Attach the same continuation marker.
                    MarkPostCombatRepathEntered(
                        unitId, unit, plan, requiredFriendlyMoat);
                    LogDetailedInfo(
                        $"Bugfixes and QoL stage=friendly-moat-movement-post-combat-repath-result unit={unitId} " +
                        $"target=({targetX},{targetY}) modeObserved={plan.ModeObserved} " +
                        $"friendlyRouteQualified={plan.FriendlyRouteQualified} " +
                        $"path={unit->r_CurrentPathPlanIndex}/{unit->r_PathPlanLength} " +
                        $"currentTarget=({unit->r_TargetTilePositionX},{unit->r_TargetTilePositionY}).");
                }
                catch (Exception ex)
                {
                    TryLogDiagnosticFailure("post-combat-result-diagnostic", ex);
                }
            }
            finally
            {
                activePlan = previousActivePlan;
                pendingPlan = previousPendingPlan;
            }
        }

        internal QualifiedMovementRoute GetReusableQualifiedRoute(PlanScope plan, GameUnit* unit)
        {
            QualifiedMovementRoute saved = plan?.QualifiedRoute;
            if (saved == null || unit == null || !saved.Route.IsValid || saved.Epoch != mapEpoch ||
                saved.Tick != CaptureCurrentGameTick() || saved.Revision != placementRevision ||
                saved.Player != unit->r_ControllableForPlayerId || saved.TargetX != plan.TargetX || saved.TargetY != plan.TargetY ||
                !APIShared.UnitAccess.TryGetById(plan.UnitId, out GameUnit* bound, out _) || bound != unit ||
                (plan.IdentityBound && plan.UnitGlobalId != unit->r_GlobalId))
                return null;
            GetNativeMovementStart(unit,out int x,out int y);
            return saved.StartX == x && saved.StartY == y ? saved : null;
        }

    }
}
