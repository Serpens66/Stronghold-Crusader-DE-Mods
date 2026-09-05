using BepInEx.Logging;
using MonoMod.RuntimeDetour;
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
using Zhuqiaomon.Assembly;

namespace MoveMoatTest
{
    internal sealed unsafe partial class MoveMoatPathTest
    {
        private void ObserveUnitMoveOrder(UnitMoveHereEventArgs args)
        {
            if (disposed)
                return;
            if (args.Phase == EventHookPhase.Pre)
            {
                UnitMoveFrame parent = GetCurrentUnitMoveFrame();
                if (parent != null)
                {
                    weightedMoatRoutePlanner.SetSearchSession(null, -1, mapEpoch, CaptureCurrentGameTick());
                    nativeGroundDecisions.Clear();
                    activeMoveCommand?.TargetedRouteDecisions.Clear();
                }
                unitMoveFrame = new UnitMoveFrame(args, parent, mapEpoch,
                    CaptureCurrentGameTick(), activeMoveCommand);
                try { PreparePlacement(unitMoveFrame); }
                catch (Exception ex) { TryLogDiagnosticFailure("placement-pre", ex); }
                if (activeMoveCommand != null)
                    activeMoveCommand.UnitMoveCalls++;
                return;
            }
            if (args.Phase != EventHookPhase.Post)
                return;

            // Post contains the ORIGINAL arguments, even if a Pre subscriber changed
            // them. Only synchronous LIFO identifies the invocation: matching coordinates
            // could accidentally close a parent whose input equals a child's original input.
            // Skipped originals emit no Post; discard those frames before restoring a parent.
            UnitMoveFrame frame = GetCurrentUnitMoveFrame();
            if (frame == null)
            {
                unitMoveFrame = null;
                return;
            }
            RestoreFailedRecovery(frame, args.ReturnValue);
            SynchronizePlacement(frame);
            FinishPlacement(frame.Placement, args.ReturnValue > 0);
            if (frame.Command != null)
            {
                frame.Command.UnitMoveCompleted++;
                if (args.ReturnValue > 0)
                    frame.Command.UnitMovePositive++;
                if (!frame.BuilderReached)
                {
                    frame.Command.UnitMoveWithoutBuilder++;
                    LogUnitWithoutBuilder(frame, args.ReturnValue);
                    if (args.ReturnValue > 0 && frame.Plan != null &&
                        frame.Plan.RouteStartX == frame.Args.TileX && frame.Plan.RouteStartY == frame.Args.TileY)
                        frame.Command.UnitMoveAlreadyArrived++;
                }
            }
            unitMoveFrame = frame.Parent;
            GetCurrentUnitMoveFrame();
        }

        private UnitMoveFrame GetCurrentUnitMoveFrame()
        {
            while (unitMoveFrame != null &&
                (unitMoveFrame.Args.SkipOriginalFunction || unitMoveFrame.MapEpoch != mapEpoch ||
                 unitMoveFrame.Tick != CaptureCurrentGameTick() ||
                 !ReferenceEquals(unitMoveFrame.Command, activeMoveCommand)))
            {
                AbandonUnitMoveFrame(unitMoveFrame);
                unitMoveFrame = unitMoveFrame.Parent;
            }
            return unitMoveFrame;
        }

        private void AbandonUnitMoveFrame(UnitMoveFrame frame)
        {
            FinishPlacement(frame.Placement, false);
            if (frame.Command != null)
                frame.Command.UnitMoveAbandoned++;
        }

        private void ClearUnitMoveFrames()
        {
            while (unitMoveFrame != null)
            {
                AbandonUnitMoveFrame(unitMoveFrame);
                unitMoveFrame = unitMoveFrame.Parent;
            }
        }

        private PlanScope GetUnitMovePlan(UnitMoveFrame frame, int unitId)
        {
            SynchronizePlacement(frame);
            if (frame == null || frame.Args.UnitId != unitId || unitId <= 0 ||
                unitId > MaximumUnitCount ||
                !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) || unit == null ||
                unit->r_AliveState != AliveState.IsAlive || !CanDigMoat(unit))
                return null;
            if (frame.Plan != null && frame.Plan.IdentityBound &&
                (frame.Plan.UnitGlobalId != unit->r_GlobalId || frame.Plan.PlayerId != unit->r_ControllableForPlayerId))
                return null;
            if (frame.Plan == null || frame.Plan.UnitId != unitId ||
                frame.Plan.TargetX != frame.Args.TileX || frame.Plan.TargetY != frame.Args.TileY)
            {
                PlanScope inherited = activePlan != null && activePlan.UnitId == unitId
                    ? activePlan : pendingPlan;
                if (inherited != null && (inherited.UnitId != unitId ||
                    (inherited.IdentityBound && (inherited.UnitGlobalId != unit->r_GlobalId ||
                     inherited.PlayerId != unit->r_ControllableForPlayerId)))) inherited = null;
                bool liveWorkHandoff = inherited != null && inherited.UnitId == unitId && inherited.MoatWorkMovement &&
                    inherited.MoatWorkSearch != null && inherited.MoatWorkSearch.CapturedTick == CaptureCurrentGameTick() &&
                    inherited.MoatWorkSearch.MapEpoch == mapEpoch && inherited.MoatWorkSearch.PlayerId == unit->r_ControllableForPlayerId;
                if (inherited != null && !liveWorkHandoff && (inherited.TargetX != frame.Args.TileX ||
                    inherited.TargetY != frame.Args.TileY))
                    inherited = null;
                frame.InheritedPlan = inherited;
                frame.Plan = CopyMovementPlan(inherited, unitId, frame.Args.TileX, frame.Args.TileY);
                // A previous call's mode/qualification never authorizes a new call.
                frame.Plan.ModeObserved = false;
                frame.Plan.NativeGroundPrecheck = false;
                frame.Plan.FriendlyRouteQualified = false;
                GetNativeMovementStart(unit, out int startX, out int startY);
                frame.Plan.UnitGlobalId = unit->r_GlobalId;
                frame.Plan.IdentityBound = true;
                frame.Plan.PlayerId = unit->r_ControllableForPlayerId;
                frame.Plan.RouteStartX = startX;
                frame.Plan.RouteStartY = startY;
            }
            return frame.Plan;
        }

        private static PlanScope CopyMovementPlan(PlanScope source, int unitId, int targetX, int targetY)
        {
            var result = new PlanScope(unitId, targetX, targetY);
            if (source == null || source.UnitId != unitId)
                return result;
            result.UnitGlobalId = source.UnitGlobalId;
            result.IdentityBound = source.IdentityBound;
            result.PlayerId = source.PlayerId;
            result.ModeObserved = source.ModeObserved;
            result.VanillaModeDetected = source.VanillaModeDetected;
            result.NativeGroundPrecheck = source.NativeGroundPrecheck;
            result.AttackMovementQualified = source.AttackMovementQualified;
            result.PostCombatRepath = source.PostCombatRepath;
            result.MoatWorkMovement = source.MoatWorkMovement;
            result.MoatWorkSearch = source.MoatWorkSearch;
            result.MoatWorkTargetTileId = source.MoatWorkTargetTileId;
            if (source.TargetX == targetX && source.TargetY == targetY)
                result.QualifiedRoute = source.QualifiedRoute;
            return result;
        }

        private static void GetNativeMovementStart(GameUnit* unit, out int startX, out int startY)
        {
            bool current = unit->r_PathPlanStateBitFlags == 0 && unit->r_MovingRelevant == 8;
            startX = current ? unit->r_CurrentTilePositionX : unit->r_NextTilePositionX2;
            startY = current ? unit->r_CurrentTilePositionY : unit->r_NextTilePositionY2;
        }

        private int EnableCompletedMoatModeForScopedMovement(IntPtr unitManager, int unitId)
        {
            int vanillaResult = originalUnitStandingOnCompletedMoat(unitManager, unitId);
            UnitMoveFrame moveFrame = GetCurrentUnitMoveFrame();
            ObserveNativeModeEntry(moveFrame, unitId);
            PlanScope requestPlan = GetUnitMovePlan(moveFrame, unitId);
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
            if (disposed || unitManager == IntPtr.Zero || unitId <= 0)
                return vanillaResult;

            try
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) || unit == null)
                    return vanillaResult;
                if (!CanDigMoat(unit))
                {
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

                    if (!TryFindRequiredFriendlyCompletedMoatRouteForPlan(
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
                        if (ShouldLogUnitPipeline)
                            LogPipelineDiagnostic(
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

                MarkTrackedAttackPipeline(
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
                if (vanillaResult == 0 && ShouldLogUnitPipeline)
                    LogMovementContext($"stage=mode unit={unitId} vanilla=0 effective=1");
                return 1;
            }
            catch (Exception ex)
            {
                LogFailure("mode", ex);
                return vanillaResult;
            }
        }

    }
}
