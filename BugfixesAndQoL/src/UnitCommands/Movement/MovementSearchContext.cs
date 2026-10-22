using APIShared;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Diagnostics;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate int PathReconstructionDelegate(IntPtr pathManager);
        internal PathReconstructionDelegate originalPathReconstruction, rootedPathReconstruction;
        internal RedBirdDetour<PathReconstructionDelegate> pathReconstructionDetour;
        internal readonly Dictionary<long, bool> nativeGroundDecisions = new Dictionary<long, bool>();
        internal object nativeGroundOwner;
        internal int nativeGroundEpoch, nativeGroundTick, nativeGroundPlayer;
        internal long nativeGroundQueries, nativeGroundCacheHits;

        internal int BuildReconstructedUnitPath(IntPtr pathManager)
        {
            PlanScope plan = GetBuilderPlan(pathManager, reportMismatch: true);
            IEnemyGatePathPolicy gate = BeginEnemyGateSearch(
                plan?.PlayerId ?? -1, EnemyGateSearchKind.Builder, out object gateScope);
            object bridgeSearch = EnemyBridgeDiagnosticBridge.BeginSearch("reconstructed-builder", plan?.PlayerId ?? -1);
            // TEMP_GATE_ROUTE_ACCEPTANCE
#if SERP_DEVDIAG
            object temporaryRoute = BeginTemporaryRouteReport(pathManager, "E32B0-reconstruction");
#endif

            int gateResult = 0;
            bool gateCompleted = false;
            BuilderWeightedScope shadow = null;
            try
            {
                shadow = TryCaptureBuilderWeightedScope(pathManager, plan);
                int result = BuildPathWithCompletedMoatRouteVariantCore(pathManager, 0, 0, plan, true, true);
            if (shadow != null)
            {
                AttackCommandScope measuredCommand = activeAttackCommand;
                long weightedStarted = (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L);
                bool previousWeightedTiming = weightedPhaseTimingActive;
                weightedPhaseTimingActive = true;
                try { ObserveWeightedMoatShadowResult(pathManager, result, shadow); }
                finally
                {
                    weightedPhaseTimingActive = previousWeightedTiming;
                    if (measuredCommand != null)
                        measuredCommand.WeightedPhaseTicks +=
                            (DetailedDiagnosticsEnabled ? Stopwatch.GetTimestamp() : 0L) - weightedStarted;
                }
            }
                gateResult = shadow != null && shadow.PublishedBuilderResult >= 0
                    ? shadow.PublishedBuilderResult : result;
                gateCompleted = true;
                return gateResult;
            }
            finally
            {
#if SERP_DEVDIAG
                EndTemporaryRouteReport(pathManager, temporaryRoute, gateCompleted, gateResult);
#endif

                EndEnemyGateSearch(gate, gateScope, EnemyGateSearchKind.Builder,
                    gateCompleted, gateResult > 0);
                EnemyBridgeDiagnosticBridge.EndSearch(bridgeSearch, gateCompleted, gateResult);
                PlanScope handoff = GetCurrentUnitMoveFrame()?.InheritedPlan ?? plan;
                if (handoff != null && handoff.MoatWorkMovement && ReferenceEquals(pendingPlan, handoff)) pendingPlan = null;
            }
        }

        internal void PrepareMovementSearch(PlanScope plan, int playerId, object explicitOwner = null)
        {
            // Work callbacks may mutate terrain between two selections in the same tick.
            // Their exact synchronous scope takes precedence over a surrounding command.
            object commandSession = RequiredOnlyMode
                ? (object)activeMoveCommand?.RequiredCache ?? activeAttackCommand?.RequiredCache
                : (object)activeMoveCommand ?? activeAttackCommand;
            object session = explicitOwner ?? (object)activeMoatWorkSelection ?? plan?.MoatWorkSearch ??
                commandSession ??
                GetCurrentUnitMoveFrame() ?? (object)plan;
            int tick = CaptureCurrentGameTick();
            if (!ReferenceEquals(session, nativeGroundOwner) || nativeGroundEpoch != mapEpoch ||
                nativeGroundTick != tick || nativeGroundPlayer != playerId)
            {
                nativeGroundDecisions.Clear(); nativeGroundOwner = session;
                nativeGroundEpoch = mapEpoch; nativeGroundTick = tick; nativeGroundPlayer = playerId;
            }
            weightedMoatRoutePlanner.SetSearchSession(session, playerId, mapEpoch, tick);
        }

        internal bool TryAllowUnitMoveRegion(IntPtr pathManager, int playerId, int targetRegion,
            int startX, int startY, int vanilla, out int result)
        {
            result = vanilla;
            UnitMoveFrame frame = GetCurrentUnitMoveFrame();
            if (frame == null || activeMoatWorkSelection != null || activeAttackApproachDiagnostic != null) return false;
            frame.RegionReached = true;
            PlanScope plan = GetUnitMovePlan(frame, frame.Args.UnitId);
            if (plan == null || pathManager != nativePathManager || vanilla != 0) return true;
            if (!APIShared.UnitAccess.TryGetById(plan.UnitId, out GameUnit* unit, out _) || unit == null)
                return true;
            GetNativeMovementStart(unit, out int actualX, out int actualY);
            int tile = GameTileManagerAPI.Instance.GetTileId(plan.TargetX, plan.TargetY);
            bool valid = plan.ModeObserved && plan.FriendlyRouteQualified &&
                plan.PlayerId == playerId && *moatPathMode == 1 &&
                actualX == startX && actualY == startY && IsValidTileId(tile) &&
                targetRegion > 0 && targetRegion <= MaximumRegionId && pathRegionGrid[tile] == targetRegion;
            if (!valid) return false;
            result = targetRegion;
            return true;
        }
    }
}
