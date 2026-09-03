using BepInEx.Logging;
using R3;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.EventAPI.Projectiles;
using SHCDESE.EventAPI.Units;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Zhuqiaomon.Assembly.Stateful;
using Zhuqiaomon.Memory;

namespace ImprovedHunters
{
    internal sealed partial class ImprovedHuntersRuntime
    {
        // All chicken ownership, granary population and compatibility behavior follows HuntChicken.
        private void OnGranarySpawnChicken(GranarySpawnChickenEventArgs args)
        {
            RemoveExpiredPendingGranaryChickenSpawns(Stopwatch.GetTimestamp());
            if (!IsChickenManagementActive ||
                args.Chimp != eChimps.CHIMP_TYPE_CHICKEN ||
                args.PlayerId < 1 ||
                args.PlayerId > MaximumPlayerId)
            {
                return;
            }

            pendingGranaryChickenSpawns.Push(new PendingGranaryChickenSpawn(
                args.PlayerId,
                args.Chimp,
                args.TileX,
                args.TileY,
                args.HeightElevation,
                Stopwatch.GetTimestamp()));
            LogChickenOwnershipDiagnostic(
                $"Improved Hunters granary chicken spawn captured: player={args.PlayerId}, " +
                $"tile={args.TileX},{args.TileY}, height={args.HeightElevation}, " +
                $"pendingDepth={pendingGranaryChickenSpawns.Count}.");
        }

        private void OnUnitCreate(UnitCreateEventArgs args)
        {
            if (args.Phase == EventHookPhase.Pre)
                OnUnitCreatePre(args);
            else if (args.Phase == EventHookPhase.Post)
                OnUnitCreatePost(args);
        }

        private void OnUnitCreatePre(UnitCreateEventArgs args)
        {
            RemoveExpiredPendingGranaryChickenSpawns(Stopwatch.GetTimestamp());
            if (!IsChickenManagementActive || pendingGranaryChickenSpawns.Count == 0)
                return;

            PendingGranaryChickenSpawn pending = pendingGranaryChickenSpawns.Peek();
            // SCRIPT EXTENDER BUG WORKAROUND: the granary event exposes native local Y
            // as TileX and local X as TileY; UnitCreate receives them scaled by 8.
            // Remove this conversion only after the upstream event is demonstrably
            // corrected. Revalidate its coordinate contract after every Extender update.
            if (!GranaryChickenSpawnPolicy.IsMatchingGranaryUnitCreate(
                    IsChickenManagementActive,
                    pending.UnitCreateMatched,
                    pending.SourcePlayerId,
                    (int)pending.UnitType,
                    pending.GranaryTileX,
                    pending.GranaryTileY,
                    pending.HeightElevation,
                    args.PlayerOwnerId,
                    (int)args.UnitType,
                    args.WorldTileX,
                    args.WorldTileY,
                    args.HeightElevation))
            {
                return;
            }

            pending.UnitCreateMatched = true;
            pending.WorldTileX = args.WorldTileX;
            pending.WorldTileY = args.WorldTileY;
            int previousOwner = args.PlayerOwnerId;
            int previousColor = args.PlayerColorId;
            args.PlayerOwnerId = 0;
            args.PlayerColorId = 0;
            LogChickenOwnershipDiagnostic(
                $"Improved Hunters granary chicken spawn neutralized before creation: " +
                $"sourcePlayer={pending.SourcePlayerId}, owner={previousOwner}->0, color={previousColor}->0, " +
                $"worldTile={args.WorldTileX},{args.WorldTileY}, pendingDepth={pendingGranaryChickenSpawns.Count}.");
        }

        private unsafe void OnUnitCreatePost(UnitCreateEventArgs args)
        {
            if (pendingGranaryChickenSpawns.Count == 0)
                return;

            PendingGranaryChickenSpawn pending = pendingGranaryChickenSpawns.Peek();
            if (!pending.UnitCreateMatched ||
                args.UnitType != pending.UnitType ||
                args.PlayerOwnerId != pending.SourcePlayerId)
            {
                return;
            }

            pendingGranaryChickenSpawns.Pop();
            int unitId = args.ReturnValue > 0 && args.ReturnValue <= int.MaxValue
                ? (int)args.ReturnValue
                : 0;
            GameUnit* chicken = null;
            bool unitResolved = unitId != 0 &&
                GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out chicken) &&
                chicken != null;
            if (!GranaryChickenSpawnPolicy.CanAssignCompletedSpawn(
                    IsChickenManagementActive,
                    args.ReturnValue,
                    unitResolved,
                    unitResolved && chicken->r_UnitChimp == eChimps.CHIMP_TYPE_CHICKEN,
                    unitResolved ? chicken->r_GlobalId : 0,
                    unitResolved && chicken->r_ControllableForPlayerId == 0,
                    unitResolved && chicken->r_SpritePlayerColorId == 0,
                    unitResolved && IsChickenLive(chicken)))
            {
                LogChickenOwnershipDiagnostic(
                    $"Improved Hunters granary chicken spawn was not assigned: sourcePlayer={pending.SourcePlayerId}, " +
                    $"unit={unitId}, returnValue={args.ReturnValue}, " +
                    $"outcome={(IsChickenManagementActive ? "post-spawn-validation-failed" : "safety-guard-inactive")}.",
                    warning: true);
                return;
            }

            TrackGranaryChicken(
                unitId,
                chicken->r_GlobalId,
                pending.SourcePlayerId);
            LogChickenOwnershipDiagnostic(
                $"Improved Hunters granary chicken assigned: player={pending.SourcePlayerId}, unit={unitId}, " +
                $"globalId={chicken->r_GlobalId}, owner=0, color=0, " +
                $"worldTile={pending.WorldTileX},{pending.WorldTileY}.");
        }

        private bool IsChickenManagementActive =>
            Shared.GameplayModActivationGate.IsEnabled(settings.EnableMod) &&
            settings.HuntChicken &&
            automaticChickenTargetPatch?.IsApplied == true &&
            granaryChickenLimitPatch?.IsAvailable == true;

        private unsafe int GetLiveTrackedGranaryChickenCount(int playerId)
        {
            CleanupTrackedGranaryChickens();
            return playerId >= 1 && playerId <= MaximumPlayerId
                ? trackedGranaryChickenCounts[playerId]
                : 0;
        }

        private unsafe void CleanupTrackedGranaryChickens()
        {
            long timestamp = Stopwatch.GetTimestamp();
            if (timestamp < nextGranaryChickenCleanupTimestamp)
                return;

            nextGranaryChickenCleanupTimestamp = timestamp + GranaryChickenCleanupInterval;
            staleGranaryChickenUnitIds.Clear();
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            foreach (KeyValuePair<int, TrackedGranaryChicken> pair in trackedGranaryChickens)
            {
                TrackedGranaryChicken tracked = pair.Value;
                if (!unitApi.TryGetUnitById(tracked.UnitId, out GameUnit* chicken) ||
                    chicken == null ||
                    !GranaryChickenSpawnPolicy.IsTrackedIdentityValid(
                        tracked.GlobalId,
                        chicken->r_GlobalId,
                        chicken->r_UnitChimp == eChimps.CHIMP_TYPE_CHICKEN,
                        IsChickenLive(chicken)))
                {
                    staleGranaryChickenUnitIds.Add(pair.Key);
                }
            }

            foreach (int unitId in staleGranaryChickenUnitIds)
                RemoveTrackedGranaryChicken(unitId);
        }

        private void TrackGranaryChicken(int unitId, uint globalId, int sourcePlayerId)
        {
            if (sourcePlayerId < 1 || sourcePlayerId > MaximumPlayerId || globalId == 0)
                return;

            RemoveTrackedGranaryChicken(unitId);
            trackedGranaryChickens[unitId] = new TrackedGranaryChicken(unitId, globalId, sourcePlayerId);
            trackedGranaryChickenCounts[sourcePlayerId]++;
        }

        private void RemoveTrackedGranaryChicken(int unitId)
        {
            if (!trackedGranaryChickens.TryGetValue(unitId, out TrackedGranaryChicken tracked))
                return;

            trackedGranaryChickens.Remove(unitId);
            if (tracked.SourcePlayerId >= 1 &&
                tracked.SourcePlayerId <= MaximumPlayerId &&
                trackedGranaryChickenCounts[tracked.SourcePlayerId] > 0)
            {
                trackedGranaryChickenCounts[tracked.SourcePlayerId]--;
            }
        }

        private void ClearTrackedGranaryChickens()
        {
            trackedGranaryChickens.Clear();
            Array.Clear(trackedGranaryChickenCounts, 0, trackedGranaryChickenCounts.Length);
            nextGranaryChickenCleanupTimestamp = 0;
        }

        private unsafe void TryReconstructLoadedGranaryChickens(SimpleNativeArray<GameUnit> units)
        {
            if (!loadedChickenReconstructionPending || !IsChickenManagementActive)
                return;

            List<ChickenGranaryCandidate> granaries = GetActiveChickenGranaries();
            int eligible = 0;
            int alreadyTracked = 0;
            int assigned = 0;
            int neutralized = 0;
            int unresolved = 0;

            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* chicken = units.GetValuePointer(index);
                if (chicken == null ||
                    chicken->r_UnitChimp != eChimps.CHIMP_TYPE_CHICKEN ||
                    chicken->r_GlobalId == 0 ||
                    !IsChickenLive(chicken))
                {
                    continue;
                }

                eligible++;
                int unitId = index + 1;
                if (trackedGranaryChickens.ContainsKey(unitId))
                {
                    alreadyTracked++;
                    continue;
                }

                int sourcePlayerId = chicken->r_ControllableForPlayerId;
                if (sourcePlayerId < 1 || sourcePlayerId > MaximumPlayerId)
                {
                    if (!TryFindNearestGranaryOwner(
                            granaries,
                            chicken->r_CurrentTilePositionX,
                            chicken->r_CurrentTilePositionY,
                            out sourcePlayerId))
                    {
                        unresolved++;
                        continue;
                    }
                }

                byte previousOwner = chicken->r_ControllableForPlayerId;
                uint previousColor = chicken->r_SpritePlayerColorId;
                if (previousOwner != 0 || previousColor != 0)
                {
                    chicken->r_ControllableForPlayerId = 0;
                    chicken->r_SpritePlayerColorId = 0;
                    neutralized++;
                }

                TrackGranaryChicken(
                    unitId,
                    chicken->r_GlobalId,
                    sourcePlayerId);
                assigned++;
            }

            loadedChickenReconstructionPending = unresolved > 0;
            if (eligible > 0 || granaries.Count > 0)
            {
                LogChickenOwnershipDiagnostic(
                    $"Improved Hunters loaded chicken reconstruction: eligible={eligible}, " +
                    $"alreadyTracked={alreadyTracked}, assigned={assigned}, unresolved={unresolved}, " +
                    $"neutralized={neutralized}, activeGranaries={granaries.Count}, " +
                    $"invariant={eligible == alreadyTracked + assigned + unresolved}, " +
                    $"retryPending={loadedChickenReconstructionPending}.",
                    warning: eligible != alreadyTracked + assigned + unresolved);
            }
        }

        private unsafe List<ChickenGranaryCandidate> GetActiveChickenGranaries()
        {
            List<ChickenGranaryCandidate> granaries = new List<ChickenGranaryCandidate>();
            SimpleNativeArray<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsArray();
            if (buildings._array == null || buildings.Length == 0)
                return granaries;

            for (int index = 0; index < buildings.Length; index++)
            {
                GameBuilding* building = buildings.GetValuePointer(index);
                int playerId = building->r_PlayerIdOwner;
                if (building->r_AliveState != AliveState.IsAlive ||
                    building->r_BuildingType != eStructs.STRUCT_GRANARY ||
                    playerId < 1 ||
                    playerId > MaximumPlayerId)
                {
                    continue;
                }

                granaries.Add(new ChickenGranaryCandidate(
                    index + 1,
                    playerId,
                    building->r_TilePositionXBegin,
                    building->r_TilePositionYBegin));
            }

            return granaries;
        }

        private static bool TryFindNearestGranaryOwner(
            List<ChickenGranaryCandidate> granaries,
            int chickenTileX,
            int chickenTileY,
            out int playerId)
        {
            playerId = 0;
            int bestDistance = int.MaxValue;
            int bestBuildingId = int.MaxValue;
            int bestPlayerId = int.MaxValue;
            foreach (ChickenGranaryCandidate granary in granaries)
            {
                int distance = GranaryChickenSpawnPolicy.ChebyshevDistance(
                    chickenTileX,
                    chickenTileY,
                    granary.TileX,
                    granary.TileY);
                if (!GranaryChickenSpawnPolicy.IsBetterGranaryCandidate(
                        distance,
                        granary.BuildingId,
                        granary.PlayerId,
                        bestDistance,
                        bestBuildingId,
                        bestPlayerId))
                {
                    continue;
                }

                bestDistance = distance;
                bestBuildingId = granary.BuildingId;
                bestPlayerId = granary.PlayerId;
                playerId = granary.PlayerId;
            }

            return playerId != 0;
        }

        private static unsafe bool IsChickenLive(GameUnit* chicken)
        {
            if (chicken == null)
                return false;
            if (chicken->r_AliveState == AliveState.NeedsInit)
                return true;
            if (chicken->r_AliveState != AliveState.IsAlive || chicken->r_CurrentHealth <= 0)
                return false;

            return *(ushort*)((byte*)chicken + 0x29C) == 0;
        }

        private void RemoveExpiredPendingGranaryChickenSpawns(long timestamp)
        {
            while (pendingGranaryChickenSpawns.Count > 0)
            {
                PendingGranaryChickenSpawn pending = pendingGranaryChickenSpawns.Peek();
                if (timestamp - pending.CreatedAt <= PendingGranaryChickenSpawnTimeout)
                    return;

                pendingGranaryChickenSpawns.Pop();
                LogChickenOwnershipDiagnostic(
                    $"Improved Hunters granary chicken spawn tracking expired: " +
                    $"sourcePlayer={pending.SourcePlayerId}, matched={pending.UnitCreateMatched}, " +
                    $"granaryTile={pending.GranaryTileX},{pending.GranaryTileY}, height={pending.HeightElevation}, " +
                    $"pendingDepth={pendingGranaryChickenSpawns.Count}.",
                    warning: true);
            }
        }

    }
}
