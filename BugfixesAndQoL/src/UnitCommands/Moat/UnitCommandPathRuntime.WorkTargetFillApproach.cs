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
        internal bool TryFindBestFillMoatApproach(
            MoatWorkSelectionScope scope,
            int moatId,
            int moatTileId,
            int moatX,
            int moatY,
            out MoatWorkApproach best)
        {
            best = default;
            byte sourceHeight = nativeHeightLayer[moatTileId]; // Vanilla compares the contact tile, not the worker.
            bool found = false;
            long bestDistance = long.MaxValue;
            RouteProbeSummary observed = new RouteProbeSummary(scope.PlayerId);
            int checkedTiles = 0;
            for (int index = 0; index < MoatWorkNeighbourX.Length; index++)
            {
                int x = moatX + MoatWorkNeighbourX[index];
                int y = moatY + MoatWorkNeighbourY[index];
                if (x < 0 || x >= MapWidth || y < 0 || y >= MapWidth)
                {
                    scope.FillInvalidTileRejected++;
                    continue;
                }
                int tileId = GameTileManagerAPI.Instance.GetTileId(x, y);
                if (!IsValidTileId(tileId))
                {
                    scope.FillInvalidTileRejected++;
                    continue;
                }
                if ((int)nativeHeightLayer[tileId] > sourceHeight + 0x10)
                {
                    scope.FillHeightRejected++;
                    continue;
                }

                bool completedMoat = IsCompletedMoatTile(tileId);
                bool friendlyMoatEndpoint = completedMoat &&
                    IsFriendlyCompletedMoatForWeightedShadow(scope.PlayerId, tileId);
                if (completedMoat && !friendlyMoatEndpoint)
                {
                    scope.FillEnemyOrInvalidMoatRejected++;
                    continue;
                }
                uint flags = tileFlags[tileId];
                if (!completedMoat &&
                    (movementTargetAvailability[y * MapWidth + x] == 0 ||
                     (scope.ImprovedFillSelection
                         ? HasDownstreamMovementBlockingFlags(flags)
                         : (flags & OrdinaryWalkableTileFlag) == 0 ||
                           (flags & CursorSpecialStructureTileFlagMask) != 0)))
                {
                    scope.FillGroundBlockedRejected++;
                    continue;
                }
                if (scope.ImprovedFillSelection &&
                    IsOccupiedByOtherLivingUnit(tileId, scope.UnitId))
                {
                    scope.FillOccupiedRejected++;
                    continue;
                }

                checkedTiles++;
                if (friendlyMoatEndpoint)
                    scope.FillFriendlyMoatEndpoints++;
                RouteProbeSummary summary = new RouteProbeSummary(scope.PlayerId);
                bool vanillaRegionApproach = scope.ImprovedFillSelection && !completedMoat &&
                    pathRegionGrid[tileId] == scope.StartRegion;
                if (!vanillaRegionApproach &&
                    (!ExtensionsEnabled ||
                     !TryFindRequiredFriendlyCompletedMoatRouteToFillEndpoint(
                        scope.UnitId, scope.PlayerId, scope.StartX, scope.StartY,
                        x, y, tileId, out summary)))
                {
                    observed.MergeObservations(summary);
                    scope.FillOwnerRouteRejected++;
                    continue;
                }
                observed.MergeObservations(summary);
                long dx = scope.StartX - x;
                long dy = scope.StartY - y;
                long distance = dx * dx + dy * dy;
                if (!found || distance < bestDistance)
                {
                    found = true;
                    bestDistance = distance;
                    best = new MoatWorkApproach(
                        moatId, moatTileId, x, y, tileId, index,
                        summary);
                }
            }
            scope.CheckedApproachTiles += checkedTiles;
            scope.MergeRoute(observed);
            return found;
        }

        internal int ResolveMoatWorkTileWithOwnerRoute(
            IntPtr tileManager, int moatId, int mode, uint sourceX, uint sourceY)
        {
            PendingDigMoatTarget pendingDig = pendingDigMoatTarget;
            bool pendingDigMatches = pendingDig != null && pendingDig.Matches(
                mapEpoch, tileManager, moatId, sourceX, sourceY);
            if (pendingDig != null && (!pendingDigMatches || mode != 1))
                pendingDigMoatTarget = null;
            PendingFillMoatApproach pending = pendingFillMoatApproach;
            bool pendingMatches = pending != null && pending.Matches(
                mapEpoch, tileManager, moatId, sourceX, sourceY);
            if (pending != null && !pendingMatches)
                pendingFillMoatApproach = null;
            try
            {
                if (tileManager == IntPtr.Zero ||
                    tileManager != GameTileManagerAPI.Instance.GetTileManager())
                {
                    pendingFillMoatApproach = null;
                    return originalResolveMoatWorkTile(
                        tileManager, moatId, mode, sourceX, sourceY);
                }
            }
            catch (Exception ex)
            {
                pendingFillMoatApproach = null;
                TryLogDiagnosticFailure("fill-moat-work-resolver-capture", ex);
                return originalResolveMoatWorkTile(
                    tileManager, moatId, mode, sourceX, sourceY);
            }
            byte* manager = (byte*)tileManager.ToPointer();
            int oldTileId = *(int*)(manager + SelectedMoatTileIdOffset);
            int oldX = *(int*)(manager + SelectedMoatApproachXOffset);
            int oldY = *(int*)(manager + SelectedMoatApproachYOffset);
            int vanillaResult = originalResolveMoatWorkTile(
                tileManager, moatId, mode, sourceX, sourceY);
            // Command 7 first resolves mode 1 to publish the target moat itself and then
            // immediately resolves mode 2 for the approach tile. Preserve an exact hand-off
            // across that mode-1 call, but discard it on every other deviation.
            if (mode == 1)
            {
                if (!pendingMatches || vanillaResult == 0)
                    pendingFillMoatApproach = null;
                if (pendingDigMatches)
                {
                    try
                    {
                        if (vanillaResult == pendingDig.TileId &&
                            *(int*)(manager + SelectedMoatApproachXOffset) == pendingDig.X &&
                            *(int*)(manager + SelectedMoatApproachYOffset) == pendingDig.Y &&
                            ValidatePendingDigTarget(pendingDig))
                        {
                            var plan = new PlanScope(pendingDig.UnitId, pendingDig.X, pendingDig.Y)
                            {
                                PlayerId = pendingDig.PlayerId,
                                FriendlyRouteQualified = true,
                                OwnerRouteProbeCompleted = true,
                                MoatWorkMovement = true,
                                MoatWorkSearch = pendingDig.SearchScope,
                                MoatWorkTargetTileId = pendingDig.TileId
                            };
                            pendingPlan = plan;
                            LogResolvedDigMoatTarget(pendingDig);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (pendingPlan != null && pendingPlan.MoatWorkMovement)
                            pendingPlan = null;
                        TryLogDiagnosticFailure("dig-moat-work-resolver", ex);
                    }
                    finally
                    {
                        pendingDigMoatTarget = null;
                    }
                }
                return vanillaResult;
            }
            pendingFillMoatApproach = null;
            if (mode != 2)
                return vanillaResult;

            if (!pendingMatches)
            {
                return vanillaResult;
            }

            try
            {
                if (!APIShared.UnitAccess.TryGetById(
                        pending.UnitId, out GameUnit* unit, out _) ||
                    unit == null || !APIShared.UnitAccess.IsReallyAlive(unit) ||
                    unit->r_ControllableForPlayerId != pending.PlayerId || !CanDigMoat(unit) ||
                    unit->r_CurrentTilePositionX != pending.StartX ||
                    unit->r_CurrentTilePositionY != pending.StartY ||
                    !ValidatePendingFillApproach(pending))
                {
                    return vanillaResult;
                }

                *(int*)(manager + SelectedMoatTileIdOffset) = pending.Approach.MoatTileId;
                *(int*)(manager + SelectedMoatApproachXOffset) = pending.Approach.X;
                *(int*)(manager + SelectedMoatApproachYOffset) = pending.Approach.Y;
                pendingPlan = new PlanScope(
                    pending.UnitId, pending.Approach.X, pending.Approach.Y)
                {
                    PlayerId = pending.PlayerId,
                    FriendlyRouteQualified = true,
                    OwnerRouteProbeCompleted = true,
                    MoatWorkMovement = true,
                    MoatWorkSearch = pending.SearchScope,
                    MoatWorkTargetTileId = pending.Approach.MoatTileId
                };
                if (pending.Approach.Summary.RouteFound)
                    LogResolvedFillMoatApproach(pending);
                return pending.Approach.TileId;
            }
            catch (Exception ex)
            {
                *(int*)(manager + SelectedMoatTileIdOffset) = oldTileId;
                *(int*)(manager + SelectedMoatApproachXOffset) = oldX;
                *(int*)(manager + SelectedMoatApproachYOffset) = oldY;
                if (pendingPlan != null && pendingPlan.MoatWorkMovement)
                    pendingPlan = null;
                TryLogDiagnosticFailure("fill-moat-work-resolver", ex);
                return vanillaResult;
            }
        }

        internal bool ValidatePendingFillApproach(PendingFillMoatApproach pending)
        {
            if (pending == null || pending.SearchScope == null ||
                pending.SearchScope.CapturedTick != CaptureCurrentGameTick())
                return false;
            MoatWorkApproach approach = pending.Approach;
            int sourceTileId = GameTileManagerAPI.Instance.GetTileId(
                pending.StartX, pending.StartY);
            if (approach.MoatId != pending.MoatId ||
                approach.NativeOrder < 0 || approach.NativeOrder >= MoatWorkNeighbourX.Length ||
                !IsValidTileId(sourceTileId) || !IsValidTileId(approach.MoatTileId) ||
                !IsValidTileId(approach.TileId) ||
                approach.X < 0 || approach.X >= MapWidth ||
                approach.Y < 0 || approach.Y >= MapWidth ||
                GameTileManagerAPI.Instance.GetTileId(approach.X, approach.Y) != approach.TileId ||
                (int)nativeHeightLayer[approach.TileId] >
                    nativeHeightLayer[sourceTileId] + 0x10)
            {
                return false;
            }
            bool completedMoat = IsCompletedMoatTile(approach.TileId);
            if (completedMoat)
            {
                if (!IsFriendlyCompletedMoatForWeightedShadow(
                        pending.PlayerId, approach.TileId))
                {
                    return false;
                }
            }
            else
            {
                uint flags = tileFlags[approach.TileId];
                if (movementTargetAvailability[approach.Y * MapWidth + approach.X] == 0 ||
                    (pending.ImprovedFillSelection
                        ? HasDownstreamMovementBlockingFlags(flags)
                        : (flags & OrdinaryWalkableTileFlag) == 0 ||
                          (flags & CursorSpecialStructureTileFlagMask) != 0))
                {
                    return false;
                }
            }
            if (pending.ImprovedFillSelection &&
                IsOccupiedByOtherLivingUnit(approach.TileId, pending.UnitId))
                return false;
            if (!TryReadMoatRecordTile(
                    pending.TileManager, pending.MoatId,
                    out int moatTileId, out int moatX, out int moatY) ||
                moatTileId != approach.MoatTileId ||
                moatX + MoatWorkNeighbourX[approach.NativeOrder] != approach.X ||
                moatY + MoatWorkNeighbourY[approach.NativeOrder] != approach.Y)
            {
                return false;
            }
            if (pending.ImprovedFillSelection && !completedMoat &&
                pathRegionGrid[approach.TileId] == pathRegionGrid[sourceTileId])
                return true;
            if (!ExtensionsEnabled)
                return false;
            return TryFindRequiredFriendlyCompletedMoatRouteToFillEndpoint(
                pending.UnitId,
                pending.PlayerId,
                pending.StartX,
                pending.StartY,
                approach.X,
                approach.Y,
                approach.TileId,
                out _, pending.SearchScope);
        }

        internal bool TryFindRequiredFriendlyCompletedMoatRouteToFillEndpoint(
            int unitId,
            int playerId,
            int startX,
            int startY,
            int targetX,
            int targetY,
            int targetTileId,
            out RouteProbeSummary summary,
            MoatWorkSelectionScope scope = null)
        {
            summary = new RouteProbeSummary(playerId);
            scope = scope ?? activeMoatWorkSelection;
            if (scope == null || scope.UnitId != unitId || scope.PlayerId != playerId ||
                scope.StartX != startX || scope.StartY != startY ||
                !IsValidTileId(targetTileId) ||
                (IsCompletedMoatTile(targetTileId) &&
                 !IsFriendlyCompletedMoatForWeightedShadow(playerId, targetTileId)))
                return false;
            return TryGetMoatWorkRoute(scope, targetX, targetY, out summary);
        }



        internal sealed class PendingFillMoatApproach
        {
            public MoatWorkSelectionScope SearchScope { get; set; }
            public PendingFillMoatApproach(
                int mapEpoch,
                IntPtr tileManager,
                int unitId,
                int playerId,
                int moatId,
                int startX,
                int startY,
                MoatWorkApproach approach,
                bool improvedFillSelection)
            {
                MapEpoch = mapEpoch;
                TileManager = tileManager;
                UnitId = unitId;
                PlayerId = playerId;
                MoatId = moatId;
                StartX = startX;
                StartY = startY;
                Approach = approach;
                ImprovedFillSelection = improvedFillSelection;
            }

            public int MapEpoch { get; }
            public IntPtr TileManager { get; }
            public int UnitId { get; }
            public int PlayerId { get; }
            public int MoatId { get; }
            public int StartX { get; }
            public int StartY { get; }
            public MoatWorkApproach Approach { get; }
            public bool ImprovedFillSelection { get; }

            public bool Matches(
                int mapEpoch,
                IntPtr tileManager,
                int moatId,
                uint sourceX,
                uint sourceY) =>
                MapEpoch == mapEpoch && TileManager == tileManager && MoatId == moatId &&
                sourceX == unchecked((uint)StartX) && sourceY == unchecked((uint)StartY);
        }
    }
}
