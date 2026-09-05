using SHCDESE.API;
using SHCDESE.EventAPI.Tribes;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace MoveMoatTest
{
    internal sealed unsafe partial class MoveMoatPathTest
    {
        [ThreadStatic]
        private static DirectCursorMoveScope activeDirectCursorMove;
        [ThreadStatic]
        private static DirectFillCommandScope activeDirectFillCommand;

        private string lastDirectCursorMoveDecision;
        private string lastDirectFillDecision;

        private void StageDirectCursorMoveWithOwnerRoute(
            IntPtr unitManager,
            int tribeId,
            int targetX,
            int targetY,
            int targetContext,
            int actionFlags)
        {
            DirectCursorMoveScope previous = activeDirectCursorMove;
            DirectCursorMoveScope scope = null;
            activeDirectCursorMove = null;
            try
            {
                try
                {
                    if (TryCreateDirectCursorMoveScope(unitManager, tribeId, targetX, targetY, out scope))
                        activeDirectCursorMove = scope;
                }
                catch (Exception ex) { TryLogDiagnosticFailure("direct-cursor-move-stager-scope", ex); }
                // 195E30 queues opcode 0x11 BEFORE its moat gate and voice feedback.
                // Never disguise real terrain to change a feedback-only branch.
                originalCursorMoveStager(unitManager, tribeId, targetX, targetY, targetContext, actionFlags);
            }
            finally { activeDirectCursorMove = previous; }

            if (scope != null)
            {
                try
                {
                    LogDirectCursorMove(scope);
                }
                catch
                {
                    // Logging after the native call must never escape through the detour ABI.
                }
            }
        }

        private bool TryCreateDirectCursorMoveScope(
            IntPtr unitManager,
            int tribeId,
            int targetX,
            int targetY,
            out DirectCursorMoveScope scope)
        {
            scope = null;
            if (disposed || unitManager == IntPtr.Zero ||
                unitManager != (IntPtr)nativeUnitManager ||
                tribeId < 0 || tribeId >= MaximumTribeCount ||
                targetX < 0 || targetX >= MapWidth || targetY < 0 || targetY >= MapWidth ||
                !TryCaptureOrderedActiveGroupUnits(
                    nativeTribeManager, tribeId, out int[] groupUnitIds))
            {
                return false;
            }

            int targetTileId = GameTileManagerAPI.Instance.GetTileId(targetX, targetY);
            if (!IsValidTileId(targetTileId))
                return false;
            bool targetIsMoat = IsCompletedMoatTile(targetTileId);

            int playerId = -1;
            var qualifyingUnits = new List<int>();
            RouteProbeSummary observed = default;
            bool relevantFriendlyMoat = targetIsMoat;
            foreach (int unitId in groupUnitIds)
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null || unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_TribeId != tribeId || !CanDigMoat(unit))
                {
                    continue;
                }
                int unitPlayerId = unit->r_ControllableForPlayerId;
                if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(unitPlayerId))
                    continue;
                if (playerId < 0)
                {
                    playerId = unitPlayerId;
                    observed = new RouteProbeSummary(playerId);
                    if (targetIsMoat &&
                        ResolveCompletedMoatRelationship(playerId, targetTileId) !=
                            CompletedMoatRelationship.Friendly)
                    {
                        return false;
                    }
                }
                if (unitPlayerId != playerId)
                    continue;

                int startX = unit->r_CurrentTilePositionX;
                int startY = unit->r_CurrentTilePositionY;
                if (startX < 0 || startX >= MapWidth || startY < 0 || startY >= MapWidth ||
                    (startX == targetX && startY == targetY))
                {
                    continue;
                }
                int startTileId = GameTileManagerAPI.Instance.GetTileId(startX, startY);
                if (!IsValidTileId(startTileId))
                    continue;

                if (!ProbeCursorConnectivity(playerId, startTileId, targetTileId, out RouteProbeSummary summary))
                    continue;
                observed.MergeObservations(summary);
                if (!summary.ReachedWithMoat)
                    continue;

                qualifyingUnits.Add(unitId);
                relevantFriendlyMoat |= summary.FriendlyMoatTiles > 0 ||
                    IsFriendlyCompletedMoatForWeightedShadow(playerId, startTileId);
                // Command staging only needs one legal group member. Per-unit planning later
                // applies the capability and owner checks again, so further full-map probes
                // would only multiply cursor-click latency for large groups.
                break;
            }

            if (playerId < 0 || qualifyingUnits.Count == 0 || !relevantFriendlyMoat)
                return false;
            observed.RouteFound = true;
            scope = new DirectCursorMoveScope(
                mapEpoch, tribeId, playerId, targetX, targetY, targetTileId,
                pathRegionGrid[targetTileId], qualifyingUnits.ToArray(), observed);
            return true;
        }

        private bool TryAllowDirectCursorMoveRegionPair(
            IntPtr pathManager,
            int playerId,
            int sourceRegion,
            int targetRegion,
            int vanillaResult)
        {
            DirectCursorMoveScope scope = activeDirectCursorMove;
            if (vanillaResult != 0 || scope == null || scope.MapEpoch != mapEpoch ||
                activeMoveCommand != null || unitMoveFrame != null || activePlan != null ||
                activeMoatWorkSelection != null || activeAttackCommand != null || activeAttackApproachDiagnostic != null ||
                pathManager != nativePathManager || playerId != scope.PlayerId ||
                sourceRegion != scope.TargetRegion ||
                targetRegion < 0 || targetRegion > MaximumRegionId)
            {
                return false;
            }
            scope.RegionFallbackCalls++;
            return true;
        }

        private void BeginDirectFillCommand(TribeIssueOrderWithTargetEventArgs args)
        {
            activeDirectFillCommand = null;
            if (args.AICommand != TribeAICommand.Unknown7 ||
                args.TribeId < 0 || args.TribeId >= MaximumTribeCount ||
                args.TargetValue1 < 0 || args.TargetValue1 >= MapWidth ||
                args.TargetValue2 < 0 || args.TargetValue2 >= MapWidth ||
                !TryCaptureOrderedActiveGroupUnits(
                    nativeTribeManager, args.TribeId, out int[] groupUnitIds))
            {
                return;
            }

            int targetTileId = GameTileManagerAPI.Instance.GetTileId(
                args.TargetValue1, args.TargetValue2);
            if (!IsValidTileId(targetTileId) || !IsCompletedMoatTile(targetTileId))
                return;

            int playerId = -1;
            var units = new List<DirectFillUnitStart>();
            foreach (int unitId in groupUnitIds)
            {
                if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) ||
                    unit == null || unit->r_AliveState != AliveState.IsAlive ||
                    unit->r_TribeId != args.TribeId || !CanDigMoat(unit))
                {
                    continue;
                }
                int unitPlayerId = unit->r_ControllableForPlayerId;
                int startX = unit->r_CurrentTilePositionX;
                int startY = unit->r_CurrentTilePositionY;
                if (!GamePlayerManagerAPI.Instance.IsPlayerIdValid(unitPlayerId) ||
                    startX < 0 || startX >= MapWidth || startY < 0 || startY >= MapWidth)
                {
                    continue;
                }
                if (playerId < 0)
                    playerId = unitPlayerId;
                if (unitPlayerId != playerId)
                    continue;
                int startTileId = GameTileManagerAPI.Instance.GetTileId(startX, startY);
                if (!IsValidTileId(startTileId))
                    continue;
                units.Add(new DirectFillUnitStart(
                    unitId, startX, startY, pathRegionGrid[startTileId]));
            }

            if (playerId < 0 || units.Count == 0 ||
                ResolveCompletedMoatRelationship(playerId, targetTileId) !=
                    CompletedMoatRelationship.Enemy)
            {
                return;
            }

            activeDirectFillCommand = new DirectFillCommandScope(
                mapEpoch, args.TribeId, playerId, args.TargetValue1, args.TargetValue2,
                targetTileId, units.ToArray());
        }

        private bool TryAllowDirectFillRegionPair(
            IntPtr pathManager,
            int playerId,
            int candidateRegion,
            int startRegion,
            int vanillaResult)
        {
            DirectFillCommandScope scope = activeDirectFillCommand;
            if (vanillaResult != 0 || scope == null || scope.MapEpoch != mapEpoch ||
                pathManager != nativePathManager || playerId != scope.PlayerId ||
                candidateRegion <= 0 || candidateRegion > MaximumRegionId ||
                startRegion < 0 || startRegion > MaximumRegionId)
            {
                return false;
            }

            string key = $"{candidateRegion}:{startRegion}";
            if (scope.RegionDecisions.TryGetValue(key, out bool cached))
                return cached;

            bool allowed = false;
            RouteProbeSummary observed = new RouteProbeSummary(scope.PlayerId);
            int selectedUnitId = 0;
            var probedStarts = new HashSet<int>();
            foreach (DirectFillUnitStart start in scope.Units)
            {
                if (start.Region != startRegion)
                    continue;
                int startKey = start.Region > 0
                    ? start.Region
                    : -GameTileManagerAPI.Instance.GetTileId(start.X, start.Y) - 1;
                if (!probedStarts.Add(startKey))
                    continue;
                EnsureReachabilityMap(scope.PlayerId, start.X, start.Y);
                RouteProbeSummary summary = GetCachedRouteSummaryForRegion(candidateRegion);
                observed.MergeObservations(summary);
                if (summary.ReachedWithMoat && !summary.ReachedWithoutMoat &&
                    summary.FriendlyMoatTiles > 0)
                {
                    allowed = true;
                    selectedUnitId = start.UnitId;
                    break;
                }
            }
            scope.RegionDecisions[key] = allowed;
            scope.RegionFallbackEvaluations++;
            if (allowed)
            {
                scope.RegionFallbackAllowed++;
                scope.SelectedUnitId = selectedUnitId;
                scope.Route.MergeObservations(observed);
            }
            return allowed;
        }

        private void EndDirectFillCommand(TribeIssueOrderWithTargetEventArgs args)
        {
            DirectFillCommandScope scope = activeDirectFillCommand;
            activeDirectFillCommand = null;
            if (scope == null)
                return;
            try
            {
                string signature = $"{scope.MapEpoch}:{scope.TribeId}:{scope.TargetTileId}:" +
                    $"{scope.RegionFallbackAllowed}:{scope.RegionFallbackEvaluations}:" +
                    $"{scope.SelectedUnitId}:{args.ReturnValue}";
                if (string.Equals(signature, lastDirectFillDecision, StringComparison.Ordinal))
                    return;
                lastDirectFillDecision = signature;
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"MoveMoat stage=direct-fill-command tribe={scope.TribeId} " +
                    $"player={scope.PlayerId} target=({scope.TargetX},{scope.TargetY})/" +
                    $"{scope.TargetTileId} diggers={scope.Units.Length} " +
                    $"regionFallbacks={scope.RegionFallbackAllowed}/" +
                    $"{scope.RegionFallbackEvaluations} selectedUnit={scope.SelectedUnitId} " +
                    $"return={args.ReturnValue} {scope.Route.ToLogFields()}.");
            }
            catch
            {
                // The synchronous scope is already cleared; diagnostics must not affect dispatch.
            }
        }

        private void ClearIncompleteDirectFillScopeAtTick()
        {
            DirectFillCommandScope scope = activeDirectFillCommand;
            activeDirectFillCommand = null;
            if (scope == null)
                return;
            try
            {
                Shared.DebugLogHelper.LogInfo(
                    log,
                    $"MoveMoat stage=direct-fill-command-incomplete tribe={scope.TribeId} " +
                    $"player={scope.PlayerId} target=({scope.TargetX},{scope.TargetY})/" +
                    $"{scope.TargetTileId} reason=no-post-event-before-next-tick.");
            }
            catch
            {
                // Cleanup remains mandatory even if diagnostics fail.
            }
        }

        private void LogDirectCursorMove(DirectCursorMoveScope scope)
        {
            string signature = $"{scope.MapEpoch}:{scope.TribeId}:{scope.TargetTileId}:" +
                $"{scope.QualifyingUnitIds.Length}:{scope.RegionFallbackCalls}";
            if (string.Equals(signature, lastDirectCursorMoveDecision, StringComparison.Ordinal))
                return;
            lastDirectCursorMoveDecision = signature;
            Shared.DebugLogHelper.LogInfo(
                log,
                $"MoveMoat stage=direct-cursor-move tribe={scope.TribeId} " +
                $"player={scope.PlayerId} target=({scope.TargetX},{scope.TargetY})/" +
                $"{scope.TargetTileId} targetRegion={scope.TargetRegion} " +
                $"qualifyingDiggers={scope.QualifyingUnitIds.Length} " +
                $"regionFallbackCalls={scope.RegionFallbackCalls} " +
                $"terrainUnchanged=True feedbackOnly=True " +
                $"{scope.Route.ToLogFields()}.");
        }

        private void ResetDirectMoatCommandScopes()
        {
            activeDirectCursorMove = null;
            activeDirectFillCommand = null;
            lastDirectCursorMoveDecision = null;
            lastDirectFillDecision = null;
        }

        private sealed class DirectCursorMoveScope
        {
            public DirectCursorMoveScope(
                int mapEpoch,
                int tribeId,
                int playerId,
                int targetX,
                int targetY,
                int targetTileId,
                int targetRegion,
                int[] qualifyingUnitIds,
                RouteProbeSummary route)
            {
                MapEpoch = mapEpoch;
                TribeId = tribeId;
                PlayerId = playerId;
                TargetX = targetX;
                TargetY = targetY;
                TargetTileId = targetTileId;
                TargetRegion = targetRegion;
                QualifyingUnitIds = qualifyingUnitIds;
                Route = route;
            }

            public int MapEpoch { get; }
            public int TribeId { get; }
            public int PlayerId { get; }
            public int TargetX { get; }
            public int TargetY { get; }
            public int TargetTileId { get; }
            public int TargetRegion { get; }
            public int[] QualifyingUnitIds { get; }
            public RouteProbeSummary Route { get; }
            public int RegionFallbackCalls { get; set; }
        }

        private readonly struct DirectFillUnitStart
        {
            public DirectFillUnitStart(int unitId, int x, int y, int region)
            {
                UnitId = unitId;
                X = x;
                Y = y;
                Region = region;
            }

            public int UnitId { get; }
            public int X { get; }
            public int Y { get; }
            public int Region { get; }
        }

        private sealed class DirectFillCommandScope
        {
            public DirectFillCommandScope(
                int mapEpoch,
                int tribeId,
                int playerId,
                int targetX,
                int targetY,
                int targetTileId,
                DirectFillUnitStart[] units)
            {
                MapEpoch = mapEpoch;
                TribeId = tribeId;
                PlayerId = playerId;
                TargetX = targetX;
                TargetY = targetY;
                TargetTileId = targetTileId;
                Units = units;
                Route = new RouteProbeSummary(playerId);
            }

            public int MapEpoch { get; }
            public int TribeId { get; }
            public int PlayerId { get; }
            public int TargetX { get; }
            public int TargetY { get; }
            public int TargetTileId { get; }
            public DirectFillUnitStart[] Units { get; }
            public Dictionary<string, bool> RegionDecisions { get; } =
                new Dictionary<string, bool>(StringComparer.Ordinal);
            public int RegionFallbackEvaluations { get; set; }
            public int RegionFallbackAllowed { get; set; }
            public int SelectedUnitId { get; set; }
            public RouteProbeSummary Route;
        }
    }
}
