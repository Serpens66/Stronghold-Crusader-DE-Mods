extern alias BugfixesRuntime;
using BepInEx.Logging;
using R3;
using SHCDESE.API;
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
using BugfixesRuntime::BugfixesAndQoL.UnitCommands;
using static BugfixesRuntime::BugfixesAndQoL.UnitCommands.UnitCommandPathRuntime;

namespace MoatMove
{
    // Moved from BugfixesAndQoL UnitMovementContext.cs: friendly-moat code runs only with MoatMove.
    internal sealed unsafe partial class FriendlyMoatTraversalProvider
    {
        internal override int ResolveScopedCompletedMoatModeTraversal(
            IntPtr unitManager, int unitId, int vanillaResult, UnitMoveFrame moveFrame)
        {
            ObserveNativeModeEntry(moveFrame, unitId);
            PlanScope requestPlan = runtime.GetUnitMovePlan(moveFrame, unitId);
            if (moveFrame != null && requestPlan == null) return vanillaResult;
            PlanScope plan = requestPlan ?? (activePlan != null && activePlan.UnitId == unitId
                ? activePlan : pendingPlan);
            // A qualification belongs to one unit. A shared command cache must never
            // turn the preceding unit's plan into the next unit's buffer owner.
            if (plan != null && (plan.UnitId != unitId ||
                (requestPlan == null && !ReferenceEquals(plan, activePlan) && !plan.MoatWorkMovement && activeMoveCommand != null &&
                 (plan.TargetX != activeMoveCommand.TargetX ||
                  plan.TargetY != activeMoveCommand.TargetY))))
            {
                if (ReferenceEquals(pendingPlan, plan))
                    pendingPlan = null;
                plan = null;
            }
            bool plannerQualified = plan != null && plan.FriendlyRouteQualified;
            if (runtime.disposed || unitManager == IntPtr.Zero || unitId <= 0)
                return vanillaResult;

            try
            {
                if (!APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) || unit == null)
                    return vanillaResult;
                if (!runtime.CanDigMoat(unit))
                {
                    return vanillaResult;
                }

                // Fast is strictly Vanilla-first. Merely entering the native mode probe is
                // not evidence that the command needs a friendly-moat route; the validated
                // pre-builder failure adapter or an earlier failed group/work gate supplies
                // that evidence later.
                // Anchor dispatch asks the core first: a cheaper moat route qualifies exactly as
                // after a Vanilla failure; a ground decision keeps the Vanilla-first state below.
                if (runtime.RequiredOnlyMode && !plannerQualified && requestPlan != null && anchorDispatchTask != 0 &&
                    TryAnchorQualifiesMoat(requestPlan, unit))
                {
                    requestPlan.FriendlyRouteQualified = true;
                    plan = requestPlan;
                    plannerQualified = true;
                }
                if (runtime.RequiredOnlyMode && !plannerQualified && requestPlan != null)
                {
                    requestPlan.ModeObserved = true;
                    requestPlan.VanillaModeDetected = vanillaResult != 0;
                    requestPlan.PlayerId = unit->r_ControllableForPlayerId;
                    if (activeMoveCommand != null)
                        activeMoveCommand.ModeCalls++;
                    fastVanillaBypasses++;
                    return vanillaResult;
                }

                // Preserve attack endpoint qualification before the generic unit branch can return.
                if (!plannerQualified && TryQualifyAttackMovementPlan(unitId, unit, vanillaResult,
                    out PlanScope attackPlan, out _, out _) &&
                    (requestPlan == null || (attackPlan.TargetX == requestPlan.TargetX && attackPlan.TargetY == requestPlan.TargetY)))
                {
                    plan = attackPlan;
                    GetNativeMovementStart(unit, out int attackStartX, out int attackStartY);
                    plan.RouteStartX = attackStartX; plan.RouteStartY = attackStartY;
                    plannerQualified = true;
                }

                if (!plannerQualified && requestPlan != null && vanillaResult == 0 &&
                    TryDeferToNativeGroundPlan(requestPlan, unit))
                {
                    requestPlan.NativeGroundPrecheck = true;
                    requestPlan.ModeObserved = true;
                    requestPlan.VanillaModeDetected = false;
                    return vanillaResult;
                }
                if (!plannerQualified && (requestPlan != null || activeMoveCommand != null))
                {
                    // 0x196280 receives each formation target; 0x18E1E0 is only a probe.
                    int targetX = (requestPlan != null || ReferenceEquals(plan, activePlan)) && plan != null
                        ? plan.TargetX : activeMoveCommand.TargetX;
                    int targetY = (requestPlan != null || ReferenceEquals(plan, activePlan)) && plan != null
                        ? plan.TargetY : activeMoveCommand.TargetY;
                    PlanScope movePlan = plan;
                    if (movePlan == null || movePlan.UnitId != unitId ||
                        movePlan.TargetX != targetX || movePlan.TargetY != targetY)
                    {
                        movePlan = new PlanScope(unitId, targetX, targetY);
                    }

                    if (!runtime.TryFindRequiredFriendlyCompletedMoatRouteForPlan(
                        movePlan, out RouteProbeSummary moveSummary))
                    {
                        return vanillaResult;
                    }

                    movePlan.FriendlyRouteQualified = true;
                    plan = movePlan;
                    plannerQualified = true;
                    if (requestPlan == null)
                        pendingPlan = movePlan;
                    if (requestPlan == null && activePlan != null && activePlan.UnitId == unitId)
                        activePlan = movePlan;
                    try
                    {
                        if (runtime.ShouldLogUnitPipeline)
                            runtime.LogPipelineDiagnostic(
                                $"stage=mode-move-owner-qualified unit={unitId} player={movePlan.PlayerId} " +
                                $"target=({movePlan.TargetX},{movePlan.TargetY}) " +
                                moveSummary.ToLogFields());
                    }
                    catch
                    {
                        // Qualification remains valid even if diagnostics fail.
                    }
                }

                if (!plannerQualified)
                {
                    if (!TryQualifyAttackMovementPlan(
                        unitId, unit, vanillaResult, out plan, out RouteProbeSummary attackSummary,
                        out string rejectionReason))
                    {
                        if (activeAttackCommand != null || IsAttackCommand(
                            (TribeAICommand)unit->r_AI_LastIssuedTribeCommand))
                        {
                            try
                            {
                                LogAttackScopeDecision(
                                    "attack-scope-rejected", unitId, unit, vanillaResult,
                                    rejectionReason, attackSummary);
                            }
                            catch
                            {
                                // Rejection diagnostics must not affect Vanilla behavior.
                            }
                        }
                        LogUnscopedAttackMode(unitId, unit, vanillaResult);
                        return vanillaResult;
                    }

                    plannerQualified = true;
                    if (requestPlan == null)
                        pendingPlan = plan;
                    try
                    {
                        LogAttackScopeDecision(
                            "attack-scope-qualified", unitId, unit, vanillaResult,
                            rejectionReason, attackSummary);
                    }
                    catch
                    {
                        // Qualification remains valid even if diagnostics fail.
                    }
                }

                runtime.MarkTrackedAttackPipeline(
                    unitId, AttackPipelineStage.Mode, -1, -1, vanillaResult != 0);

                if (plan == null)
                    return vanillaResult;
                if (requestPlan != null)
                    moveFrame.Plan = plan;
                plan.ModeObserved = true;
                plan.VanillaModeDetected = vanillaResult != 0;
                plan.PlayerId = unit->r_ControllableForPlayerId;
                if (activeMoveCommand != null)
                    activeMoveCommand.ModeCalls++;
                try
                {
                    LogModeContext(plan, unit, vanillaResult);
                }
                catch
                {
                    // Context logging must not change the native mode decision.
                }
                if (vanillaResult == 0 && runtime.ShouldLogUnitPipeline)
                    runtime.LogMovementContext($"stage=mode unit={unitId} vanilla=0 effective=1");
                return 1;
            }
            catch (Exception ex)
            {
                runtime.LogFailure("mode", ex);
                return vanillaResult;
            }
        }
    }
}
