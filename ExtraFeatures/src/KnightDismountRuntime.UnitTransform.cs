// Feature: Snapshots of selected units and their replacement by the transformed unit type.
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace ExtraFeatures
{
    internal sealed unsafe partial class KnightDismountRuntime
    {
        private List<UnitTransformSnapshot> CaptureSelectedUnitSnapshots(int localPlayerId, eChimps unitType)
        {
            List<UnitTransformSnapshot> snapshots = new List<UnitTransformSnapshot>();
            if (localPlayerId < 1 || localPlayerId > GamePlayerManagerAPI.MAX_PLAYERS)
                return snapshots;
            TryCaptureSelectedUnits(localPlayerId, out APIShared.LocalSelectionSnapshot selection);
            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            HashSet<int> seenGlobalIds = new HashSet<int>();

            for (int i = 0; selection != null && i < selection.Count; i++)
            {
                int unitId = selection[i].UnitId;
                if (unitId <= 0 || !APIShared.UnitAccess.TryGetById(unitApi, unitId, out GameUnit* unit, out _))
                    continue;

                if (!IsOwnAliveUnit(unit, localPlayerId, unitType))
                    continue;

                AddSnapshot(snapshots, seenGlobalIds, unitId, unit);
            }

            int[] aliveUnits = APIShared.UnitAccess.GetAllReallyAliveUnits();
            for (int i = 0; i < aliveUnits.Length; i++)
            {
                int unitId = aliveUnits[i];
                if (unitId <= 0 || !APIShared.UnitAccess.TryGetById(unitApi, unitId, out GameUnit* unit, out _))
                    continue;

                if (!IsSelected(unit) || !IsOwnAliveUnit(unit, localPlayerId, unitType))
                    continue;

                AddSnapshot(snapshots, seenGlobalIds, unitId, unit);
            }

            return snapshots;
        }

        private void AddSnapshot(List<UnitTransformSnapshot> snapshots, HashSet<int> seenGlobalIds, int unitId, GameUnit* unit)
        {
            int globalId = (int)unit->r_GlobalId;
            int snapshotKey = globalId > 0 ? globalId : -unitId;
            if (!seenGlobalIds.Add(snapshotKey))
                return;

            snapshots.Add(CreateSnapshotFromUnit(unitId, unit));
        }

        private static UnitTransformSnapshot CreateSnapshotFromUnit(int unitId, GameUnit* unit)
        {
            return new UnitTransformSnapshot
            {
                UnitId = unitId,
                GlobalId = (int)unit->r_GlobalId,
                OwnerPlayerId = unit->r_ControllableForPlayerId,
                ColorPlayerId = (int)unit->r_SpritePlayerColorId,
                TileX = unit->r_CurrentTilePositionX,
                TileY = unit->r_CurrentTilePositionY,
                Height = unit->r_HeightElevation,
                CurrentHealth = (int)unit->r_CurrentHealth,
                MaxHealth = (int)unit->r_MaxHealth,
                LinkedProductionBuildingId = unit->r_LinkedProductionBuildingId
            };
        }

        private bool ApplyDismount(UnitTransformSnapshot snapshot, string reason, long limitReservationId, out int selectedReplacementUnitId)
        {
            selectedReplacementUnitId = 0;
            if (!TryResolveAliveUnitByGlobalId(snapshot, eChimps.CHIMP_TYPE_KNIGHT, out int currentUnitId))
                return false;

            if (!APIShared.UnitAccess.TryGetById(currentUnitId, out GameUnit* currentUnit, out _))
                return false;

            UnitTransformSnapshot currentSnapshot = CreateSnapshotFromUnit(currentUnitId, currentUnit);
            int swordsmanUnitId = CreateUnitFromSnapshot(
                currentSnapshot,
                eChimps.CHIMP_TYPE_SWORDSMAN,
                "dismount",
                reason,
                limitReservationId);
            if (swordsmanUnitId <= 0)
                return false;

            if (!TryResolveAliveUnitByGlobalId(currentSnapshot, eChimps.CHIMP_TYPE_KNIGHT, out int currentKnightId) ||
                !APIShared.UnitAccess.TryGetById(currentKnightId, out GameUnit* currentKnight, out _))
            {
                LogError($"Knight dismount could not reacquire the original knight after spawning its replacement: reason={reason}, originalUnitId={currentUnitId}, globalId={currentSnapshot.GlobalId}.");
                if (APIShared.UnitAccess.TryGetById(swordsmanUnitId, out _, out _)) GameUnitManagerAPI.Instance.DeleteUnit(swordsmanUnitId);
                return false;
            }

            if (!TryConsumeLinkedStableHorse(currentKnightId, currentKnight, reason, out ConsumedStableHorse consumedHorse))
            {
                if (APIShared.UnitAccess.TryGetById(swordsmanUnitId, out _, out _)) GameUnitManagerAPI.Instance.DeleteUnit(swordsmanUnitId);
                return false;
            }

            bool transferSelection = ShouldTransferSelection(currentSnapshot, currentKnight);
            if (!(APIShared.UnitAccess.TryGetById(currentKnightId, out _, out _) && GameUnitManagerAPI.Instance.DeleteUnitSafe(currentKnightId)))
            {
                RollbackConsumedStableHorse(consumedHorse, reason);
                LogError($"Knight dismount could not mark the original knight for Vanilla deletion: reason={reason}, unitId={currentKnightId}, globalId={currentSnapshot.GlobalId}.");
                if (APIShared.UnitAccess.TryGetById(swordsmanUnitId, out _, out _)) GameUnitManagerAPI.Instance.DeleteUnit(swordsmanUnitId);
                return false;
            }

            if (transferSelection)
                selectedReplacementUnitId = swordsmanUnitId;

            if (Shared.DebugLogHelper.DiagnosticsEnabled)
                LogDebug(
                    $"Knight dismount completed: reason={reason}, sourceGlobalId={currentSnapshot.GlobalId}, " +
                    $"swordsmanUnitId={swordsmanUnitId}, linkedHorseConsumed={consumedHorse.IsActive}.");
            return true;
        }

        private bool TryResolveAliveUnitByGlobalId(UnitTransformSnapshot snapshot, eChimps expectedType, out int currentUnitId)
        {
            currentUnitId = snapshot.GlobalId > 0 ? FindAliveUnitIdByGlobalId(snapshot.GlobalId, snapshot.UnitId) : snapshot.UnitId;
            return ValidateAliveUnit(snapshot, expectedType, currentUnitId);
        }

        private bool ValidateAliveUnit(UnitTransformSnapshot snapshot, eChimps expectedType, int currentUnitId)
        {
            if (currentUnitId <= 0 || !APIShared.UnitAccess.TryGetById(currentUnitId, out GameUnit* unit, out _))
                return false;

            if (!APIShared.UnitAccess.IsReallyAlive(unit) ||
                unit->r_UnitChimp != expectedType ||
                unit->r_ControllableForPlayerId != snapshot.OwnerPlayerId)
                return false;

            return true;
        }

        private int CreateUnitFromSnapshot(
            UnitTransformSnapshot snapshot,
            eChimps unitType,
            string label,
            string reason,
            long limitReservationId = 0)
        {
            if (!unitLimitBridge.ArmReservation(limitReservationId))
            {
                if (Shared.DebugLogHelper.DiagnosticsEnabled)
                    LogDebug(
                        $"Knight {label} stopped because its UnitLimit reservation is no longer valid: " +
                        $"reason={reason}, originalUnitId={snapshot.UnitId}, globalId={snapshot.GlobalId}, " +
                        $"reservationId={limitReservationId}.");
                return -1;
            }

            long createdId;
            try
            {
                createdId = GameUnitManagerAPI.Instance.CreateUnitLocal(
                    playerOwnerId: snapshot.OwnerPlayerId,
                    playerColorId: snapshot.ColorPlayerId,
                    localTileX: snapshot.TileX,
                    localTileY: snapshot.TileY,
                    heightElevation: snapshot.Height,
                    chimp: unitType);
            }
            finally
            {
                unitLimitBridge.ReleaseReservation(limitReservationId);
            }

            if (createdId <= 0 || createdId > int.MaxValue)
            {
                LogError($"Knight {label} spawned invalid {unitType} id: reason={reason}, originalUnitId={snapshot.UnitId}, globalId={snapshot.GlobalId}, createdId={createdId}.");
                return -1;
            }

            if (snapshot.LinkedProductionBuildingId > 0 &&
                snapshot.LinkedProductionBuildingId <= ushort.MaxValue &&
                APIShared.UnitAccess.TryGetById((int)createdId, out GameUnit* createdUnit, out _))
            {
                // Preserve the barracks/production link; the horse stable has separate hidden fields.
                createdUnit->r_LinkedProductionBuildingId = (ushort)snapshot.LinkedProductionBuildingId;
            }

            ApplyHealthRatio((int)createdId, snapshot.CurrentHealth, snapshot.MaxHealth, label);
            return (int)createdId;
        }

        // Finds the really-alive unit carrying this native global ID. A unit keeps its game ID for its
        // whole life and global IDs are unique per unit, so a still-matching hint is exactly the unit the
        // scan would find; this path runs per tick for every pending transformation. Otherwise one
        // allocation-free pass in ascending game-ID order (the order of GetAllReallyAliveUnits).
        private static int FindAliveUnitIdByGlobalId(int globalId, int hintUnitId = 0)
        {
            if (globalId <= 0)
                return -1;

            GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
            if (hintUnitId > 0 && APIShared.UnitAccess.TryGetById(unitApi, hintUnitId, out GameUnit* hinted, out _) &&
                (int)hinted->r_GlobalId == globalId && APIShared.UnitAccess.IsReallyAlive(hinted))
                return hintUnitId;

            Span<GameUnit> units = unitApi.GetUnitsAsSpan();
            for (int spanIndex = 0; spanIndex < units.Length; spanIndex++)
            {
                ref GameUnit candidate = ref units[spanIndex];
                if ((int)candidate.r_GlobalId == globalId && APIShared.UnitAccess.IsReallyAlive(in candidate))
                    return spanIndex + 1;
            }

            return -1;
        }

        private void ApplyHealthRatio(int targetUnitId, int sourceCurrentHealth, int sourceMaxHealth, string label)
        {
            if (!APIShared.UnitAccess.TryGetById(targetUnitId, out GameUnit* unit, out _))
            {
                LogError($"Knight {label} could not set target health, unit not found: targetUnitId={targetUnitId}.");
                return;
            }

            int targetMaxHealth = Math.Max(1, (int)unit->r_MaxHealth);
            double ratio = sourceMaxHealth > 0 ? Math.Max(0.0, Math.Min(1.0, sourceCurrentHealth / (double)sourceMaxHealth)) : 1.0;
            int targetHealth = Math.Max(1, Math.Min(targetMaxHealth, (int)Math.Round(targetMaxHealth * ratio, MidpointRounding.AwayFromZero)));
            ushort targetPercent = (ushort)Math.Max(0, Math.Min(100, (int)Math.Round(100.0 * targetHealth / targetMaxHealth, MidpointRounding.AwayFromZero)));

            unit->r_CurrentHealth = (uint)targetHealth;
            unit->r_CurrentHealthPercentage = targetPercent;
            unit->r_HealthBarBlocks = (uint)(targetPercent / 10);
        }

        private struct UnitTransformSnapshot
        {
            public int UnitId;
            public int GlobalId;
            public int OwnerPlayerId;
            public int ColorPlayerId;
            public int TileX;
            public int TileY;
            public int Height;
            public int CurrentHealth;
            public int MaxHealth;
            public int LinkedProductionBuildingId;
        }
    }
}
