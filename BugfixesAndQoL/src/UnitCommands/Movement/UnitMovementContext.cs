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

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime
    {
        internal void DispatchUnitMoveEvent(UnitMoveHereEventArgs args)
        {
            if (args.Phase == EventHookPhase.Pre)
            {
                formationRuntime?.OnUnitMoveHere(args);
                ObserveUnitMoveOrder(args);
            }
            else
            {
                try { ObserveUnitMoveOrder(args); }
                finally { formationRuntime?.OnUnitMoveHere(args); }
            }
        }

        internal void ObserveUnitMoveOrder(UnitMoveHereEventArgs args)
        {
            if (disposed)
                return;
            if (args.Phase == EventHookPhase.Pre)
            {
                UnitMoveFrame parent = GetCurrentUnitMoveFrame();
                if (parent != null)
                {
                    InvalidateMovementSearchData();
                }
                unitMoveFrame = new UnitMoveFrame(args, parent, mapEpoch,
                    CaptureCurrentGameTick(), activeMoveCommand,
                    MovementOptionsSnapshot.Capture(settings));
#if SERP_DEVDIAG
                var freezeContext = FreezeUnitContext(args.UnitId);
                freezeContext.TargetX = args.TileX; freezeContext.TargetY = args.TileY;
                unitMoveFrame.FreezeToken = GateAttackFreezeCapture.Enter(GateAttackFreezeCapture.Move, freezeContext);
#endif
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
#if SERP_DEVDIAG
            GateAttackFreezeCapture.Leave(frame.FreezeToken, args.ReturnValue, true);
            frame.FreezeToken = 0;
#endif
            GetCurrentUnitMoveFrame();
        }

        internal UnitMoveFrame GetCurrentUnitMoveFrame()
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

        internal void AbandonUnitMoveFrame(UnitMoveFrame frame)
        {
#if SERP_DEVDIAG
            GateAttackFreezeCapture.Leave(frame.FreezeToken, 0, false);
            frame.FreezeToken = 0;
#endif
            FinishPlacement(frame.Placement, false);
            if (frame.Command != null)
                frame.Command.UnitMoveAbandoned++;
        }

        internal void ClearUnitMoveFrames()
        {
            while (unitMoveFrame != null)
            {
                AbandonUnitMoveFrame(unitMoveFrame);
                unitMoveFrame = unitMoveFrame.Parent;
            }
        }

        internal PlanScope GetUnitMovePlan(UnitMoveFrame frame, int unitId)
        {
            SynchronizePlacement(frame);
            if (frame == null || frame.Args.UnitId != unitId || unitId <= 0 ||
                unitId > MaximumUnitCount ||
                !APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) || unit == null ||
                !APIShared.UnitAccess.IsReallyAlive(unit) || !CanDigMoat(unit))
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

        internal static PlanScope CopyMovementPlan(PlanScope source, int unitId, int targetX, int targetY)
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
            result.VanillaFailureProven = source.VanillaFailureProven;
            result.AttackMovementQualified = source.AttackMovementQualified;
            result.PostCombatRepath = source.PostCombatRepath;
            result.MoatWorkMovement = source.MoatWorkMovement;
            result.MoatWorkSearch = source.MoatWorkSearch;
            result.MoatWorkTargetTileId = source.MoatWorkTargetTileId;
            if (source.TargetX == targetX && source.TargetY == targetY)
                result.QualifiedRoute = source.QualifiedRoute;
            return result;
        }

        internal static void GetNativeMovementStart(GameUnit* unit, out int startX, out int startY)
        {
            bool current = unit->r_PathPlanStateBitFlags == 0 && unit->r_MovementSubstep == 8;
            startX = current ? unit->r_CurrentTilePositionX : unit->r_NextTilePositionX2;
            startY = current ? unit->r_CurrentTilePositionY : unit->r_NextTilePositionY2;
        }

        internal int EnableCompletedMoatModeForScopedMovement(IntPtr unitManager, int unitId)
        {
            int vanillaResult = originalUnitStandingOnCompletedMoat(unitManager, unitId);
            // Native detour barrier: no managed exception may unwind into the caller.
            // Vanilla already ran exactly once, so its result is the safe fallback.
            try { return ResolveScopedCompletedMoatMode(unitManager, unitId, vanillaResult); }
            catch (Exception ex)
            {
                ReportNativeDetourBarrier("mode-context", ex);
                return vanillaResult;
            }
        }

        private int ResolveScopedCompletedMoatMode(IntPtr unitManager, int unitId, int vanillaResult)
        {
            if (nativeManualProbe) return vanillaResult;
            UnitMoveFrame moveFrame = GetCurrentUnitMoveFrame();
            SynchronizePlacement(moveFrame);
            if (!TraversalEnabled) return vanillaResult;
            return Traversal.ResolveScopedCompletedMoatModeTraversal(unitManager, unitId, vanillaResult, moveFrame);
        }

    }
}
