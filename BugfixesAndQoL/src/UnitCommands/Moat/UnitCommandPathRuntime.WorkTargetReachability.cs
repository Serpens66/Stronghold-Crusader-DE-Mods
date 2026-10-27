using RedBird.X64.Hooks.Transaction;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace BugfixesAndQoL.UnitCommands
{
    internal sealed unsafe partial class UnitCommandPathRuntime
    {

        internal bool TryGetMoatWorkRoute(
            MoatWorkSelectionScope scope, int targetX, int targetY,
            out RouteProbeSummary summary)
        {
#if SERP_DEVDIAG
            using var probeOperation = ManualProbeCapture.BeginOperation(ManualProbeCapture.MoatWork);
#endif
            summary = default;
            if (scope == null || !scope.Matches(mapEpoch, GameTileManagerAPI.Instance.GetTileManager()) ||
                scope.CapturedTick != CaptureCurrentGameTick() ||
                (uint)targetX >= MapWidth || (uint)targetY >= MapWidth ||
                !APIShared.UnitAccess.TryGetById(scope.UnitId, out GameUnit* unit, out _) ||
                unit == null || !APIShared.UnitAccess.IsReallyAlive(unit) || !CanDigMoat(unit) ||
                unit->r_ControllableForPlayerId != scope.PlayerId ||
                unit->r_CurrentTilePositionX != scope.StartX ||
                unit->r_CurrentTilePositionY != scope.StartY)
                return false;

            if (scope.RelationshipMode == 1)
                return TraversalEnabled && Traversal.TryGetDigWorkRoute(scope, targetX, targetY, out summary);

            if (!TraversalEnabled)
            {
                if (!ManualCommandsEnabled || !IsCompletedMoatTile(scope.StartTileId) ||
                    !ProbeNativeManualPath(scope.UnitId, targetX, targetY)) return false;
                summary = new RouteProbeSummary(scope.PlayerId) {
                    RouteFound = true, AttackProbeEvaluated = true, ReachedWithMoat = true,
                    FriendlyMoatTiles = 1, StartRegion = scope.StartRegion,
                    TargetRegion = pathRegionGrid[GameTileManagerAPI.Instance.GetTileId(targetX, targetY)],
                    RouteDistance = Math.Max(Math.Abs(targetX - scope.StartX), Math.Abs(targetY - scope.StartY)) };
                return true;
            }
            return Traversal.TryGetMoatWorkRouteTraversal(scope, targetX, targetY, out summary);
        }

        internal bool TryAllowDigWorkRegionPair(
            IntPtr pathManager,
            int playerId,
            int sourceRegion,
            int targetRegion,
            int movementProfile,
            int vanillaResult)
        {
            try
            {
                MoatWorkSelectionScope scope = activeMoatWorkSelection;
                if (vanillaResult != 0 || scope == null || scope.RelationshipMode != 1 ||
                    pathManager != nativePathManager || playerId != scope.PlayerId ||
                    !TraversalEnabled || sourceRegion != scope.StartRegion ||
                    sourceRegion < 0 || sourceRegion > MaximumRegionId ||
                    targetRegion <= 0 || targetRegion > MaximumRegionId)
                {
                    return false;
                }
                return Traversal.TryAllowDigWorkRegion(scope, targetRegion);
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("dig-moat-work-region-pair", ex);
                return false;
            }
        }

        internal bool TryAllowDigWorkRegionSearch(
            IntPtr pathManager,
            int playerId,
            int targetRegion,
            int startX,
            int startY,
            int vanillaResult,
            out int effectiveResult)
        {
            effectiveResult = vanillaResult;
            try
            {
                MoatWorkSelectionScope scope = activeMoatWorkSelection;
                if (scope == null || scope.RelationshipMode != 1 || !TraversalEnabled ||
                    pathManager != nativePathManager || playerId != scope.PlayerId ||
                    targetRegion <= 0 || targetRegion > MaximumRegionId ||
                    startX != scope.StartX || startY != scope.StartY ||
                    vanillaResult != 0)
                {
                    return false;
                }
                if (!Traversal.TryAllowDigWorkRegion(scope, targetRegion))
                    return false;
                effectiveResult = targetRegion;
                return true;
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("dig-moat-work-region-search", ex);
                effectiveResult = vanillaResult;
                return false;
            }
        }

    }
}
