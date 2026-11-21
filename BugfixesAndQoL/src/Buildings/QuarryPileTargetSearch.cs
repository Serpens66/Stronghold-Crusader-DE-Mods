// Feature: Search Vanilla's quarry-pile candidate positions for a target the game accepts.
using APIShared.Buildings;
using BepInEx.Logging;
using SHCDESE.API;
using SHCDESE.Detours;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace BugfixesAndQoL
{
    internal sealed unsafe class QuarryPileTargetSearch
    {
        // Vanilla relocation uses size 6 for the quarry, size 2 for its pile and tries 1..9.
        private const int VanillaQuarryScale = 6;
        private const int VanillaPileScale = 2;
        private const int VanillaCandidateCount = VanillaQuarryScale * 4;
        private const int VanillaMinimumPlacementTry = 1;
        private const int VanillaMaximumPlacementTry = 9;
        private readonly ManualLogSource log;
        private readonly Dictionary<int, FailedRotationTargets> failedRotationTargetsByQuarry = new Dictionary<int, FailedRotationTargets>();
        private bool layoutValidated;

        internal QuarryPileTargetSearch(ManualLogSource log)
        {
            this.log = log;
        }

        internal bool NativeHelperAvailable => layoutValidated && QuarryPilePlacement.RelocationAvailable;

        internal void InitializeLayout() => layoutValidated = ValidateVanillaBuildingLayout();

        private bool ValidateVanillaBuildingLayout()
        {
            bool compatible = Shared.NativeBuildingCompoundGroup.HasExpectedLayout(
                out int buildingSize, out int pileIdOffset);
            if (!compatible)
            {
                Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/QuarryPileRelocationRuntime/ValidateVanillaBuildingLayout", $"Bugfixes and QoL quarry-pile relocation was disabled because the GameBuilding layout is incompatible with Vanilla's structure-group deletion: size=0x{buildingSize:X}, pileIdOffset=0x{pileIdOffset:X}.");
                return false;
            }

            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                LogInfo($"validated Vanilla GameBuilding structure-group layout: size=0x{buildingSize:X}, pileIdOffset=0x{pileIdOffset:X}, nativeGroupOffset=0x{Shared.NativeBuildingCompoundGroup.GroupOffset:X}.");
            return true;
        }

        internal bool TryFindNextRotationTarget(
            int playerId,
            GameBuilding* quarry,
            GameBuilding* oldPile,
            int operationId,
            out PlacementPosition target)
        {
            target = default;
            if (!NativeHelperAvailable)
                return false;

            int quarryScale = GetBuildingScale(quarry);
            int buildingScale = GetBuildingScale(oldPile);
            if (quarryScale != VanillaQuarryScale || buildingScale != VanillaPileScale)
                return false;

            int quarryGlobalId = (int)quarry->r_GlobalId;
            int oldPileGlobalId = (int)oldPile->r_GlobalId;
            int oldX = oldPile->r_TilePositionXBegin;
            int oldY = oldPile->r_TilePositionYBegin;
            if (!TryResolveVanillaCursor(
                quarry,
                oldX,
                oldY,
                out int currentIndex))
                return false;

            HashSet<long> failedTargets = GetFailedRotationTargets(quarryGlobalId, oldPileGlobalId);

            // Vanilla's perimeter indexes increase clockwise. Always exhaust the closest Vanilla distance first,
            // but begin immediately after the current angular index so a vacated position is not selected again.
            for (int placementTry = VanillaMinimumPlacementTry;
                placementTry <= VanillaMaximumPlacementTry;
                placementTry++)
            {
                for (int clockwiseOffset = 1; clockwiseOffset <= VanillaCandidateCount; clockwiseOffset++)
                {
                    int candidateIndex = (currentIndex + clockwiseOffset) % VanillaCandidateCount;
                    if (!TryGetVanillaCandidate(quarry, candidateIndex, placementTry, out PlacementPosition candidate))
                        return false;

                    if (candidate.X == oldX && candidate.Y == oldY)
                        continue;

                    if (failedTargets != null && failedTargets.Contains(GetPositionKey(candidate)))
                        continue;

                    if (!ValidateCandidateWithGame(
                        playerId,
                        candidate,
                        buildingScale,
                        operationId,
                        candidateIndex,
                        placementTry))
                    {
                        continue;
                    }

                    target = candidate;
                    return true;
                }
            }

            return false;
        }

        internal bool TryFindNearestKeepTarget(
            int playerId,
            GameBuilding* quarry,
            GameBuilding* oldPile,
            int keepCenterXTimesTwo,
            int keepCenterYTimesTwo,
            int operationId,
            out PlacementPosition target,
            out bool currentPositionIsBest)
        {
            target = default;
            currentPositionIsBest = false;
            if (!NativeHelperAvailable || quarry == null || oldPile == null)
                return false;

            int quarryScale = GetBuildingScale(quarry);
            int pileScale = GetBuildingScale(oldPile);
            if (quarryScale != VanillaQuarryScale || pileScale != VanillaPileScale)
                return false;

            int oldX = oldPile->r_TilePositionXBegin;
            int oldY = oldPile->r_TilePositionYBegin;
            var candidates = new List<QuarryPileTargetCandidate>();
            var seenPositions = new HashSet<long>();
            var currentPosition = new PlacementPosition(oldX, oldY);
            seenPositions.Add(GetPositionKey(currentPosition));

            HashSet<long> failedTargets = GetFailedRotationTargets(
                (int)quarry->r_GlobalId,
                (int)oldPile->r_GlobalId);
            int placementTry = VanillaMinimumPlacementTry;
            for (int candidateIndex = 0; candidateIndex < VanillaCandidateCount; candidateIndex++)
            {
                if (!TryGetVanillaCandidate(quarry, candidateIndex, placementTry, out PlacementPosition candidate))
                    return false;

                long positionKey = GetPositionKey(candidate);
                if (candidate.X == oldX && candidate.Y == oldY)
                {
                    candidates.Add(new QuarryPileTargetCandidate(
                        oldX,
                        oldY,
                        placementTry,
                        candidateIndex,
                        isCurrentPosition: true));
                    continue;
                }

                if (!seenPositions.Add(positionKey) ||
                    (failedTargets != null && failedTargets.Contains(positionKey)) ||
                    !ValidateCandidateWithGame(
                        playerId,
                        candidate,
                        pileScale,
                        operationId,
                        candidateIndex,
                        placementTry))
                {
                    continue;
                }

                candidates.Add(new QuarryPileTargetCandidate(
                    candidate.X,
                    candidate.Y,
                    placementTry,
                    candidateIndex,
                    isCurrentPosition: false));
            }

            if (!QuarryPileTargetSelectionPolicy.TrySelectNearestAtPlacementTry(
                candidates,
                VanillaMinimumPlacementTry,
                keepCenterXTimesTwo,
                keepCenterYTimesTwo,
                out QuarryPileTargetCandidate selected))
            {
                target = currentPosition;
                currentPositionIsBest = true;
                if (Shared.DebugLogHelper.DiagnosticsEnabled)
                    LogInfo(
                        $"AI Keep-facing pile remains unchanged: playerId={playerId}, quarryGlobalId={quarry->r_GlobalId}, " +
                        $"reason=no-valid-placementTry-{VanillaMinimumPlacementTry}-position.");
                return true;
            }

            target = new PlacementPosition(selected.X, selected.Y);
            currentPositionIsBest = selected.IsCurrentPosition;
            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                LogInfo(
                    $"AI Keep-facing target selected: playerId={playerId}, quarryGlobalId={quarry->r_GlobalId}, " +
                    $"target={selected.X},{selected.Y}, vanillaTry={selected.PlacementTry}, candidateIndex={selected.CandidateIndex}, currentPositionIsBest={currentPositionIsBest}.");
            return true;
        }

        internal bool ValidateRequestedRotationTarget(
            int playerId,
            GameBuilding* quarry,
            GameBuilding* oldPile,
            PlacementPosition target,
            int operationId)
        {
            if (!NativeHelperAvailable)
                return false;

            int quarryScale = GetBuildingScale(quarry);
            int buildingScale = GetBuildingScale(oldPile);
            if (quarryScale != VanillaQuarryScale || buildingScale != VanillaPileScale)
                return false;

            int targetCandidateIndex = -1;
            int targetPlacementTry = -1;
            for (int placementTry = VanillaMinimumPlacementTry;
                placementTry <= VanillaMaximumPlacementTry && targetPlacementTry < 0;
                placementTry++)
            {
                for (int candidateIndex = 0; candidateIndex < VanillaCandidateCount; candidateIndex++)
                {
                    if (!TryGetVanillaCandidate(quarry, candidateIndex, placementTry, out PlacementPosition candidate))
                        return false;

                    if (candidate.X != target.X || candidate.Y != target.Y)
                        continue;

                    targetCandidateIndex = candidateIndex;
                    targetPlacementTry = placementTry;
                    break;
                }
            }

            if (targetPlacementTry < 0)
                return false;
            return ValidateCandidateWithGame(
                playerId,
                target,
                buildingScale,
                operationId,
                targetCandidateIndex,
                targetPlacementTry);
        }

        private bool TryResolveVanillaCursor(
            GameBuilding* quarry,
            int oldX,
            int oldY,
            out int currentIndex)
        {
            currentIndex = VanillaCandidateCount / 4;
            long nearestDistanceSquared = long.MaxValue;

            for (int placementTry = VanillaMinimumPlacementTry;
                placementTry <= VanillaMaximumPlacementTry;
                placementTry++)
            {
                for (int candidateIndex = 0; candidateIndex < VanillaCandidateCount; candidateIndex++)
                {
                    if (!TryGetVanillaCandidate(quarry, candidateIndex, placementTry, out PlacementPosition candidate))
                        return false;

                    if (candidate.X == oldX && candidate.Y == oldY)
                    {
                        currentIndex = candidateIndex;
                        return true;
                    }

                    if (placementTry != VanillaMinimumPlacementTry)
                        continue;

                    long dx = candidate.X - (long)oldX;
                    long dy = candidate.Y - (long)oldY;
                    long distanceSquared = dx * dx + dy * dy;
                    if (distanceSquared >= nearestDistanceSquared)
                        continue;

                    currentIndex = candidateIndex;
                    nearestDistanceSquared = distanceSquared;
                }
            }

            return true;
        }

        private bool TryGetVanillaCandidate(
            GameBuilding* quarry,
            int candidateIndex,
            int placementTry,
            out PlacementPosition candidate)
        {
            candidate = default;
            if (quarry == null)
                return false;
            try
            {
                if (!QuarryPilePlacement.TryGetRelocationCandidate(quarry->r_TilePositionXBegin, quarry->r_TilePositionYBegin,
                    candidateIndex, placementTry, out int x, out int y))
                    return false;
                candidate = new PlacementPosition(x, y);
                return true;
            }
            catch (Exception ex)
            {
                LogErrorThreadSafe(
                    $"Bugfixes and QoL quarry-pile Vanilla candidate generation failed: candidateIndex={candidateIndex}, " +
                    $"placementTry={placementTry}: {ex}");
                return false;
            }
        }

        private bool ValidateCandidateWithGame(
            int playerId,
            PlacementPosition candidate,
            int buildingScale,
            int operationId,
            int candidateIndex,
            int placementTry)
        {
            GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
            if (!TryGetSafeTileId(tileApi, candidate.X, candidate.Y, out _))
                return false;

            bool previousBlockedState = tileApi.TileManager.IsPlacementBlocked;
            try
            {
                tileApi.TileManager.IsPlacementBlocked = false;
                BulkBuildingDetours.c_game_player_build_placement_validator_hook_impl(
                    tileApi.GetTileManager(),
                    playerId,
                    candidate.X,
                    candidate.Y,
                    eMappers.MAPPER_QUARRYPILE,
                    buildingScale,
                    0);
                bool blocked = tileApi.TileManager.IsPlacementBlocked;
                return !blocked;
            }
            catch (Exception ex)
            {
                LogErrorThreadSafe(
                    $"Bugfixes and QoL quarry-pile native placement validation failed: operationId={operationId}, vanillaTry={placementTry}, candidateIndex={candidateIndex}, target={candidate.X},{candidate.Y}: {ex}");
                return false;
            }
            finally
            {
                tileApi.TileManager.IsPlacementBlocked = previousBlockedState;
            }
        }

        internal static bool TryGetSafeTileId(
            GameTileManagerAPI tileApi,
            int tileX,
            int tileY,
            out int tileId)
        {
            tileId = -1;
            if (tileApi == null || !tileApi.IsTileInsideMapBounds(tileX, tileY))
                return false;

            int resolvedTileId = tileApi.GetTileId(tileX, tileY);
            if (!tileApi.IsValidTileId(resolvedTileId))
                return false;

            tileId = resolvedTileId;
            return true;
        }

        internal static int GetBuildingScale(GameBuilding* building)
        {
            uint gridSize = building->r_OccupyTileGridSize;
            return gridSize > 0 && gridSize <= int.MaxValue ? (int)gridSize : 0;
        }

        private HashSet<long> GetFailedRotationTargets(int quarryGlobalId, int oldPileGlobalId)
        {
            if (!failedRotationTargetsByQuarry.TryGetValue(quarryGlobalId, out FailedRotationTargets state))
                return null;

            if (state.OldPileGlobalId == oldPileGlobalId)
                return state.Targets;

            failedRotationTargetsByQuarry.Remove(quarryGlobalId);
            return null;
        }

        internal void RememberFailedRotationTarget(QuarryPileRelocationOperation operation)
        {
            if (operation == null || operation.QuarryGlobalId <= 0 || operation.OldPileGlobalId <= 0)
                return;

            if (!failedRotationTargetsByQuarry.TryGetValue(operation.QuarryGlobalId, out FailedRotationTargets state) ||
                state.OldPileGlobalId != operation.OldPileGlobalId)
            {
                state = new FailedRotationTargets(operation.OldPileGlobalId);
                failedRotationTargetsByQuarry[operation.QuarryGlobalId] = state;
            }

            PlacementPosition target = new PlacementPosition(operation.TargetTileX, operation.TargetTileY);
            state.Targets.Add(GetPositionKey(target));
        }

        internal void ClearFailedRotationTargets(int quarryGlobalId)
        {
            if (quarryGlobalId > 0)
                failedRotationTargetsByQuarry.Remove(quarryGlobalId);
        }

        internal void ClearAllFailedRotationTargets()
        {
            failedRotationTargetsByQuarry.Clear();
        }

        private static long GetPositionKey(PlacementPosition position)
        {
            return ((long)position.X << 32) | (uint)position.Y;
        }

        private void LogInfo(string message, [System.Runtime.CompilerServices.CallerFilePath] string callerFile = "", [System.Runtime.CompilerServices.CallerLineNumber] int callerLine = 0)
        {
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(() =>
                Shared.DebugLogHelper.LogDebugLimited(log, callerFile + ":" + callerLine, message));
        }

        private void LogErrorThreadSafe(string message) =>
            Shared.UnityMainThreadDispatch.TryRunInlineOrEnqueue(
                () => Shared.DebugLogHelper.LogErrorLimited(log, "BugfixesAndQoL/QuarryPileRelocationRuntime/LogErrorThreadSafe", message));

        private sealed class FailedRotationTargets
        {
            public FailedRotationTargets(int oldPileGlobalId)
            {
                OldPileGlobalId = oldPileGlobalId;
            }

            public int OldPileGlobalId { get; }
            public HashSet<long> Targets { get; } = new HashSet<long>();
        }

        internal readonly struct PlacementPosition
        {
            public PlacementPosition(int x, int y)
            {
                X = x;
                Y = y;
            }

            public int X { get; }
            public int Y { get; }
        }
    }
}
