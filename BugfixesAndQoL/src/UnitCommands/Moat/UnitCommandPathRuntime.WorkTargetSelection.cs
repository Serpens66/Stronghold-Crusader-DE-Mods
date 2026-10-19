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
        internal int FindMoatWorkTargetWithOwnerRoute(
            IntPtr tileManager, int playerId, int unitId, int relationshipMode)
        {
            // A work hand-off may only survive the exact 0x6AF60 -> 0x196280 chain.
            // A new selector invocation proves that an older hand-off was not consumed.
            if (pendingPlan != null && pendingPlan.MoatWorkMovement)
                pendingPlan = null;
            pendingFillMoatApproach = null;
            pendingDigMoatTarget = null;
            if (relationshipMode == 1)
            {
                // Intermediate failed region probes are not a failed work selection.
                // Suppress an enclosing work scope for the entire native attempt.
                MoatWorkSelectionScope previous = activeMoatWorkSelection;
                int vanillaResult;
                activeMoatWorkSelection = null;
                try { vanillaResult = originalFindMoatWorkTarget(tileManager, playerId, unitId, relationshipMode); }
                finally { activeMoatWorkSelection = previous; }
                if (vanillaResult >= 0 || !TraversalEnabled) return vanillaResult;
                try { return Traversal.FindDigWorkTarget(tileManager, playerId, unitId, vanillaResult); }
                catch (Exception ex)
                {
                    TryLogDiagnosticFailure("dig-moat-provider", ex);
                    pendingDigMoatTarget = null;
                    return vanillaResult;
                }
            }
            MoatWorkSelectionScope scope;
            try
            {
                if (!TryCreateMoatWorkSelectionScope(
                        tileManager, playerId, unitId, relationshipMode, out scope))
                {
                    return originalFindMoatWorkTarget(
                        tileManager, playerId, unitId, relationshipMode);
                }
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("moat-work-selection-capture", ex);
                return originalFindMoatWorkTarget(
                    tileManager, playerId, unitId, relationshipMode);
            }

            long measured = 0;
            Shared.DevDiag.Start(ref measured);
            int result = SelectMoatWorkTarget(scope, tileManager, playerId, unitId, relationshipMode);
            Shared.DevDiag.StopSpike(ref measured, "uc.moatWork.select", 20000, () =>
                $"player={playerId} ai={GamePlayerManagerAPI.Instance.IsAIPlayer(playerId)} unit={unitId} " +
                $"mode={relationshipMode} result={result} start=({scope.StartX},{scope.StartY}) " +
                $"mapBuilds={scope.SearchBuilds} expandedNodes={scope.SearchExpandedNodes} " +
                $"searchMs={scope.SearchMilliseconds:F1} " +
                $"endpointHits={scope.EndpointCacheHits}");

            try
            {
                scope.SelectedMoatId = result;
                if (relationshipMode == 2 && result > 0 &&
                    scope.FillApproaches.TryGetValue(result, out MoatWorkApproach approach))
                    pendingFillMoatApproach = new PendingFillMoatApproach(
                        mapEpoch, tileManager, unitId, playerId, result,
                        scope.StartX, scope.StartY, approach,
                        scope.ImprovedFillSelection) { SearchScope = scope };
                LogMoatWorkSelection(scope);
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("moat-work-selection-post", ex);
                pendingFillMoatApproach = null;
                pendingDigMoatTarget = null;
            }
            return result;
        }

        internal int SelectMoatWorkTarget(MoatWorkSelectionScope scope, IntPtr tileManager,
            int playerId, int unitId, int relationshipMode)
        {
            scope.ImprovedFillSelection = relationshipMode == 2 && IsImprovedMoatFillingEnabled();
            MoatWorkSelectionScope previous = activeMoatWorkSelection;
            activeMoatWorkSelection = scope;
            cacheMapEpoch = -1;
            try
            {
                // Validate in 6C490 before Vanilla's distance comparison and reservation.
                // Re-running the full record scan for rejected winners is quadratic.
                return originalFindMoatWorkTarget(tileManager, playerId, unitId, relationshipMode);
            }
            finally { activeMoatWorkSelection = previous; }
        }

        internal bool IsImprovedMoatFillingEnabled() =>
            settings.EnableMod && settings.EnableImprovedMoatFilling;

        internal bool TryCreatePendingDigMoatTarget(
            MoatWorkSelectionScope scope,
            int moatId,
            out PendingDigMoatTarget pending)
        {
            pending = TraversalEnabled ? Traversal.CreatePendingDigMoatTarget(scope, moatId) : null;
            return pending != null;
        }

        internal bool TryCreateMoatWorkSelectionScope(
            IntPtr tileManager,
            int playerId,
            int unitId,
            int relationshipMode,
            out MoatWorkSelectionScope scope)
        {
            scope = null;
            bool friendlyMovementEnabled = ExtensionsEnabled;
            bool improvedFillEnabled = relationshipMode == 2 && IsImprovedMoatFillingEnabled();
            if ((relationshipMode == 1 && !TraversalEnabled) ||
                (relationshipMode == 2 && !friendlyMovementEnabled && !improvedFillEnabled) ||
                disposed || tileManager == IntPtr.Zero ||
                tileManager != GameTileManagerAPI.Instance.GetTileManager() ||
                (relationshipMode != 1 && relationshipMode != 2) ||
                !GamePlayerManagerAPI.Instance.IsPlayerIdValid(playerId) ||
                !APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) ||
                unit == null || !APIShared.UnitAccess.IsReallyAlive(unit) ||
                unit->r_ControllableForPlayerId != playerId || !CanDigMoat(unit))
            {
                return false;
            }

            int startX = unit->r_CurrentTilePositionX;
            int startY = unit->r_CurrentTilePositionY;
            if (startX < 0 || startX >= MapWidth || startY < 0 || startY >= MapWidth)
                return false;
            int startTileId = GameTileManagerAPI.Instance.GetTileId(startX, startY);
            if (!IsValidTileId(startTileId))
                return false;

            scope = new MoatWorkSelectionScope(
                mapEpoch, tileManager, unitId, playerId, relationshipMode,
                startX, startY, startTileId, pathRegionGrid[startTileId]);
            return true;
        }

        internal int AllowFillMoatApproachThroughFriendlyMoat(
            IntPtr tileManager, int sourceRegion, int moatTileId, int moatY)
        {
            int vanillaResult = originalHasFillMoatApproach(
                tileManager, sourceRegion, moatTileId, moatY);
            MoatWorkSelectionScope scope = activeMoatWorkSelection;
            if (scope == null || (!scope.ImprovedFillSelection && vanillaResult != 0) || scope.RelationshipMode != 2 ||
                !scope.Matches(mapEpoch, tileManager) || moatY < 0 || moatY >= MapWidth)
            {
                return vanillaResult;
            }

            try
            {
                scope.FillFallbackEvaluations++;
                if (!TryGetMoatRecord(
                        tileManager, moatTileId, moatY,
                        out int moatId, out int moatX, out int recordY))
                {
                    return scope != null && scope.ImprovedFillSelection ? 0 : vanillaResult;
                }
                if (!TryFindBestFillMoatApproach(
                        scope, moatId, moatTileId, moatX, recordY,
                        out MoatWorkApproach approach))
                {
                    return scope != null && scope.ImprovedFillSelection ? 0 : vanillaResult;
                }

                scope.FillApproaches[moatId] = approach;
                scope.MergeRoute(approach.Summary);
                scope.FillFallbackAllowed++;
                return 1;
            }
            catch (Exception ex)
            {
                TryLogDiagnosticFailure("fill-moat-work-approach", ex);
                return scope != null && scope.ImprovedFillSelection ? 0 : vanillaResult;
            }
        }

        internal bool TryGetMoatRecord(
            IntPtr tileManager,
            int moatTileId,
            int expectedY,
            out int moatId,
            out int moatX,
            out int moatY)
        {
            moatId = 0;
            moatX = -1;
            moatY = -1;
            if (!IsValidTileId(moatTileId) || getMoatIdAtTile == null)
                return false;
            moatId = getMoatIdAtTile(tileManager, moatTileId);
            int moatCount = *(int*)((byte*)tileManager.ToPointer() + MoatRecordCountOffset);
            if (!IsValidMoatRecordId(moatId, moatCount))
                return false;
            byte* record = (byte*)tileManager.ToPointer() +
                MoatRecordArrayOffset + moatId * MoatRecordSize;
            if (*(int*)(record + MoatRecordTileIdOffset) != moatTileId)
                return false;
            moatX = *(short*)(record + MoatRecordXOffset);
            moatY = *(short*)(record + MoatRecordYOffset);
            if (moatX < 0 || moatX >= MapWidth || moatY != expectedY)
                return false;
            UnmanagedVector2<ushort> tilePosition =
                GameTileManagerAPI.Instance.GetTileVectorFromId(moatTileId);
            return tilePosition.X == moatX && tilePosition.Y == moatY;
        }

        internal sealed class MoatWorkSelectionScope
        {
            public MoatWorkSelectionScope(
                int mapEpoch,
                IntPtr tileManager,
                int unitId,
                int playerId,
                int relationshipMode,
                int startX,
                int startY,
                int startTileId,
                int startRegion)
            {
                MapEpoch = mapEpoch;
                TileManager = tileManager;
                UnitId = unitId;
                PlayerId = playerId;
                RelationshipMode = relationshipMode;
                StartX = startX;
                StartY = startY;
                StartTileId = startTileId;
                StartRegion = startRegion;
                Route = new RouteProbeSummary(playerId);
                CapturedTick = CaptureCurrentGameTick();
                StartTimestamp = Stopwatch.GetTimestamp();
            }

            public int MapEpoch { get; }
            public IntPtr TileManager { get; }
            public int UnitId { get; }
            public int PlayerId { get; }
            public int RelationshipMode { get; }
            public int StartX { get; }
            public int StartY { get; }
            public int StartTileId { get; }
            public int StartRegion { get; }
            public int SelectedMoatId { get; set; }
            public int CapturedTick { get; }
            public long StartTimestamp { get; }
            public int ReachabilityGeneration { get; set; }
            public int SearchBuilds { get; set; }
            public int SearchExpandedNodes { get; set; }
            public double SearchMilliseconds { get; set; }
            public int EndpointCacheHits { get; set; }
            public Dictionary<int, RouteProbeSummary> EndpointRoutes { get; } =
                new Dictionary<int, RouteProbeSummary>();
            public object TraversalWorkState { get; set; }
            public int FillFallbackEvaluations { get; set; }
            public int FillFallbackAllowed { get; set; }
            public int CheckedApproachTiles { get; set; }
            public int FillFriendlyMoatEndpoints { get; set; }
            public int FillInvalidTileRejected { get; set; }
            public int FillHeightRejected { get; set; }
            public int FillEnemyOrInvalidMoatRejected { get; set; }
            public int FillGroundBlockedRejected { get; set; }
            public int FillOwnerRouteRejected { get; set; }
            public int FillOccupiedRejected { get; set; }
            public bool ImprovedFillSelection { get; set; }
            public RouteProbeSummary Route;
            public Dictionary<int, MoatWorkApproach> FillApproaches { get; } =
                new Dictionary<int, MoatWorkApproach>();

            public bool Matches(int mapEpoch, IntPtr tileManager) =>
                MapEpoch == mapEpoch && TileManager == tileManager;

            public void MergeRoute(RouteProbeSummary summary) => Route.MergeObservations(summary);
        }

        internal readonly struct MoatWorkApproach
        {
            public MoatWorkApproach(
                int moatId,
                int moatTileId,
                int x,
                int y,
                int tileId,
                int nativeOrder,
                RouteProbeSummary summary)
            {
                MoatId = moatId;
                MoatTileId = moatTileId;
                X = x;
                Y = y;
                TileId = tileId;
                NativeOrder = nativeOrder;
                Summary = summary;
            }

            public int MoatId { get; }
            public int MoatTileId { get; }
            public int X { get; }
            public int Y { get; }
            public int TileId { get; }
            public int NativeOrder { get; }
            public RouteProbeSummary Summary { get; }
        }
    }
}
