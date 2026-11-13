// Feature: Stable horse slots consumed by mounting and released by dismounting.
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace ExtraFeatures
{
    internal sealed unsafe partial class KnightDismountRuntime
    {
        private int CountAvailableHorseSlots(int playerId)
        {
            return FindHorseAllocations(playerId, int.MaxValue).Count;
        }

        private List<HorseAllocation> FindHorseAllocations(int playerId, int maxCount)
        {
            List<HorseAllocation> allocations = new List<HorseAllocation>();
            if (maxCount <= 0)
                return allocations;

            List<int> stableIds = new List<int>();
            GameBuildingManagerAPI.Instance.GetAllBuildings(stableIds, AliveState.IsAlive, eStructs.STRUCT_STABLES, PlayerRelationship.Self, playerId);
            stableIds.Sort();

            for (int i = 0; i < stableIds.Count && allocations.Count < maxCount; i++)
            {
                int stableId = stableIds[i];
                if (!global::APIShared.BuildingAccess.TryGetById(stableId, out GameBuilding* stable, out _))
                    continue;

                if (!IsUsableStable(stable, playerId))
                    continue;

                int availableAtStable = GetAvailableStableHorseCount(stable);
                for (int slot = 0; slot < StableHorseSlotCount && availableAtStable > 0 && allocations.Count < maxCount; slot++)
                {
                    if (!IsStableHorseSlotFree(stable, slot))
                        continue;

                    HorseAllocation allocation = new HorseAllocation
                    {
                        StableId = stableId,
                        StableGlobalId = (int)stable->r_GlobalId,
                        OwnerPlayerId = playerId,
                        Slot = slot
                    };

                    allocations.Add(allocation);
                    availableAtStable--;
                }
            }

            return allocations;
        }

        private bool TryConsumeStableHorse(HorseAllocation allocation, int unitId, int unitGlobalId, string reason)
        {
            if (unitId <= 0 || unitId > ushort.MaxValue || unitGlobalId <= 0)
                return false;

            if (allocation.StableId <= 0 || allocation.StableId > ushort.MaxValue)
                return false;

            if (allocation.Slot < 0 || allocation.Slot >= StableHorseSlotCount)
                return false;

            if (!APIShared.UnitAccess.TryGetById(unitId, out GameUnit* mountedUnit, out _))
                return false;

            if (!global::APIShared.BuildingAccess.TryGetById(allocation.StableId, out GameBuilding* stable, out _))
                return false;

            if (!IsUsableStable(stable, allocation.OwnerPlayerId))
                return false;

            if (allocation.StableGlobalId > 0 && (int)stable->r_GlobalId != allocation.StableGlobalId)
                return false;

            if (GetAvailableStableHorseCount(stable) <= 0 || !IsStableHorseSlotFree(stable, allocation.Slot))
                return false;

            // Only establish the bidirectional ownership link here. Vanilla's regular stable update
            // recounts r_UsedHorses from the four valid slot links; r_TotalHorses remains game-owned.
            // Updating either counter ourselves would duplicate Vanilla accounting.
            GameBuildingManagerAPI.Instance.SetStablesUnitIdLink(
                allocation.StableId,
                allocation.Slot,
                unitId,
                unitGlobalId,
                bidirectional: true);

            bool slotMatches = GetStableHorseSlotUnitId(stable, allocation.Slot) == unitId &&
                GetStableHorseSlotGlobalId(stable, allocation.Slot) == unitGlobalId;
            bool backlinkMatches = mountedUnit->r_LinkedStableBuildingId == allocation.StableId &&
                mountedUnit->r_LinkedStableGlobalId == (uint)allocation.StableGlobalId;

            if (!slotMatches || !backlinkMatches)
            {
                // Only the just-written link must be rolled back; no accounting transition should
                // survive. The full Vanilla release routine would additionally consume a horse.
                GameBuildingManagerAPI.Instance.UnlinkStablesUnitIdLink(
                    allocation.StableId,
                    allocation.Slot,
                    bidirectional: true);
                LogError(
                    $"Knight mount rolled back an incomplete bidirectional stable link: reason={reason}, " +
                    $"stableId={allocation.StableId}, slot={allocation.Slot}, unitId={unitId}, " +
                    $"slotMatches={slotMatches}, backlinkMatches={backlinkMatches}.");
                return false;
            }

            return true;
        }

        private static bool IsUsableStable(GameBuilding* stable, int ownerPlayerId)
        {
            return stable != null &&
                stable->r_AliveState == AliveState.IsAlive &&
                stable->r_BuildingType == eStructs.STRUCT_STABLES &&
                (ownerPlayerId <= 0 || stable->r_PlayerIdOwner == ownerPlayerId);
        }

        private static int ClampStableHorseCount(byte value)
        {
            return Math.Max(0, Math.Min(StableHorseSlotCount, (int)value));
        }

        private static int GetAvailableStableHorseCount(GameBuilding* stable)
        {
            int total = ClampStableHorseCount(stable->r_TotalHorses);
            int used = ClampStableHorseCount(stable->r_UsedHorses);
            int freeSlots = CountFreeStableHorseSlots(stable);
            return Math.Max(0, Math.Min(freeSlots, total - used));
        }

        private static int CountFreeStableHorseSlots(GameBuilding* stable)
        {
            int count = 0;
            for (int slot = 0; slot < StableHorseSlotCount; slot++)
            {
                if (IsStableHorseSlotFree(stable, slot))
                    count++;
            }

            return count;
        }

        private static bool TryValidateStableHorseSlots(int stableId, GameBuilding* stable, out int occupiedSlotCount)
        {
            occupiedSlotCount = 0;
            if (stable == null || stableId <= 0 || stable->r_GlobalId == 0)
                return false;

            var seenUnitIds = new HashSet<int>();
            var seenGlobalIds = new HashSet<int>();
            int stableGlobalId = (int)stable->r_GlobalId;
            for (int slot = 0; slot < StableHorseSlotCount; slot++)
            {
                int unitId = GetStableHorseSlotUnitId(stable, slot);
                int unitGlobalId = GetStableHorseSlotGlobalId(stable, slot);
                if (unitId == 0 && unitGlobalId == 0)
                    continue;

                if (unitId <= 0 || unitGlobalId <= 0 ||
                    !seenUnitIds.Add(unitId) || !seenGlobalIds.Add(unitGlobalId) ||
                    !APIShared.UnitAccess.TryGetById(unitId, out GameUnit* linkedUnit, out _) ||
                    (int)linkedUnit->r_GlobalId != unitGlobalId ||
                    linkedUnit->r_LinkedStableBuildingId != stableId ||
                    linkedUnit->r_LinkedStableGlobalId != (uint)stableGlobalId)
                {
                    return false;
                }

                occupiedSlotCount++;
            }

            return true;
        }

        private static bool IsStableHorseSlotFree(GameBuilding* stable, int slot)
        {
            return GetStableHorseSlotUnitId(stable, slot) == 0 &&
                GetStableHorseSlotGlobalId(stable, slot) == 0;
        }

        private static int GetStableHorseSlotUnitId(GameBuilding* stable, int slot)
        {
            switch (slot)
            {
                case 0: return stable->r_UsedHorse1UnitId;
                case 1: return stable->r_UsedHorse2UnitId;
                case 2: return stable->r_UsedHorse3UnitId;
                case 3: return stable->r_UsedHorse4UnitId;
                default: return -1;
            }
        }

        private static int GetStableHorseSlotGlobalId(GameBuilding* stable, int slot)
        {
            switch (slot)
            {
                case 0: return (int)stable->r_UsedHorse1GlobalId;
                case 1: return (int)stable->r_UsedHorse2GlobalId;
                case 2: return (int)stable->r_UsedHorse3GlobalId;
                case 3: return (int)stable->r_UsedHorse4GlobalId;
                default: return -1;
            }
        }

        private static int GetKnightStableBuildingId(GameUnit* unit)
        {
            return unit == null ? 0 : unit->r_LinkedStableBuildingId;
        }

        private static int GetKnightStableBuildingGlobalId(GameUnit* unit)
        {
            return unit == null ? 0 : (int)unit->r_LinkedStableGlobalId;
        }

        private bool TryConsumeLinkedStableHorse(
            int unitId,
            GameUnit* unit,
            string reason,
            out ConsumedStableHorse consumedHorse)
        {
            consumedHorse = default;
            if (unit == null || unitId <= 0 || unit->r_GlobalId == 0)
                return false;

            int unitGlobalId = (int)unit->r_GlobalId;
            int linkedStableId = GetKnightStableBuildingId(unit);
            int linkedStableGlobalId = GetKnightStableBuildingGlobalId(unit);
            bool hasSlotLink = TryFindStableHorseLink(unitId, unitGlobalId, out int slotStableId, out int slot);

            // Imported or scripted knights can legitimately have no stable. Dismounting them must
            // not invent accounting, but any partial or contradictory link is unsafe to consume.
            if (linkedStableId <= 0 && linkedStableGlobalId <= 0 && !hasSlotLink)
                return true;

            if (linkedStableId <= 0 || linkedStableGlobalId <= 0 || !hasSlotLink ||
                slotStableId != linkedStableId || slot < 0 || slot >= StableHorseSlotCount ||
                !global::APIShared.BuildingAccess.TryGetById(linkedStableId, out GameBuilding* stable, out _) ||
                stable->r_AliveState != AliveState.IsAlive ||
                stable->r_BuildingType != eStructs.STRUCT_STABLES ||
                (int)stable->r_GlobalId != linkedStableGlobalId ||
                GetStableHorseSlotUnitId(stable, slot) != unitId ||
                GetStableHorseSlotGlobalId(stable, slot) != unitGlobalId)
            {
                LogError(
                    $"Knight dismount refused an inconsistent stable link: reason={reason}, unitId={unitId}, " +
                    $"unitGlobalId={unitGlobalId}, linkedStableId={linkedStableId}, " +
                    $"linkedStableGlobalId={linkedStableGlobalId}, slotStableId={slotStableId}, slot={slot}.");
                return false;
            }

            int totalBefore = stable->r_TotalHorses;
            int usedBefore = stable->r_UsedHorses;
            int rechargeBefore = stable->r_HorseRechargeTimer;
            bool slotsStructurallyValid =
                TryValidateStableHorseSlots(linkedStableId, stable, out int occupiedSlotCount);
            if (!StableHorseConsumptionPolicy.TryGetTotalAfterConsumption(
                    totalBefore,
                    usedBefore,
                    occupiedSlotCount,
                    slotsStructurallyValid,
                    settings.InstantHorse,
                    out int totalAfter))
            {
                LogError(
                    $"Knight dismount refused invalid stable accounting: reason={reason}, unitId={unitId}, " +
                    $"unitGlobalId={unitGlobalId}, {FormatStableState(linkedStableId, stable, slot)}.");
                return false;
            }

            consumedHorse = new ConsumedStableHorse
            {
                IsActive = true,
                StableId = linkedStableId,
                StableGlobalId = linkedStableGlobalId,
                Slot = slot,
                UnitId = unitId,
                UnitGlobalId = unitGlobalId,
                TotalBefore = totalBefore,
                UsedBefore = usedBefore,
                RechargeBefore = rechargeBefore
            };

            // TODO(SHCDE-SE): Replace this managed Vanilla-equivalent accounting block when the
            // Script Extender exposes a direct "consume linked stable horse" API. Unlink alone
            // returns the horse immediately. Unless Instant Horse is enabled, decrementing
            // TotalHorses makes the existing stable recharge it. Vanilla owns UsedHorses and
            // HorseRechargeTimer, so never write them here.
            stable->r_TotalHorses = (byte)totalAfter;
            GameBuildingManagerAPI.Instance.UnlinkStablesUnitIdLink(linkedStableId, slot, bidirectional: true);

            bool transitionMatches = IsStableHorseSlotFree(stable, slot) &&
                unit->r_LinkedStableBuildingId == 0 && unit->r_LinkedStableGlobalId == 0 &&
                stable->r_TotalHorses == totalAfter &&
                stable->r_UsedHorses == usedBefore &&
                stable->r_HorseRechargeTimer == rechargeBefore;
            if (transitionMatches)
                return true;

            LogError(
                $"Knight dismount stable transition was incomplete and will be rolled back: reason={reason}, " +
                $"unitId={unitId}, unitGlobalId={unitGlobalId}, {FormatStableState(linkedStableId, stable, slot)}.");
            RollbackConsumedStableHorse(consumedHorse, reason);
            consumedHorse = default;
            return false;
        }

        private void RollbackConsumedStableHorse(ConsumedStableHorse consumedHorse, string reason)
        {
            if (!consumedHorse.IsActive)
                return;

            if (!global::APIShared.BuildingAccess.TryGetById(consumedHorse.StableId, out GameBuilding* stable, out _) ||
                stable->r_AliveState != AliveState.IsAlive ||
                stable->r_BuildingType != eStructs.STRUCT_STABLES ||
                (int)stable->r_GlobalId != consumedHorse.StableGlobalId ||
                !APIShared.UnitAccess.TryGetById(consumedHorse.UnitId, out GameUnit* unit, out _) ||
                (int)unit->r_GlobalId != consumedHorse.UnitGlobalId)
            {
                LogError(
                    $"Knight dismount could not reacquire objects for stable rollback: reason={reason}, " +
                    $"stableId={consumedHorse.StableId}, unitId={consumedHorse.UnitId}.");
                return;
            }

            bool slotIsFree = IsStableHorseSlotFree(stable, consumedHorse.Slot);
            bool slotAlreadyRestored = GetStableHorseSlotUnitId(stable, consumedHorse.Slot) == consumedHorse.UnitId &&
                GetStableHorseSlotGlobalId(stable, consumedHorse.Slot) == consumedHorse.UnitGlobalId;
            bool totalCanRestore = stable->r_TotalHorses == consumedHorse.TotalBefore - 1 ||
                stable->r_TotalHorses == consumedHorse.TotalBefore;
            if ((!slotIsFree && !slotAlreadyRestored) || !totalCanRestore)
            {
                LogError(
                    $"Knight dismount skipped an unsafe stable rollback: reason={reason}, unitId={consumedHorse.UnitId}, " +
                    $"expectedTotal={consumedHorse.TotalBefore - 1}/{consumedHorse.TotalBefore}, " +
                    $"{FormatStableState(consumedHorse.StableId, stable, consumedHorse.Slot)}.");
                return;
            }

            stable->r_TotalHorses = (byte)consumedHorse.TotalBefore;
            if (slotIsFree)
            {
                GameBuildingManagerAPI.Instance.SetStablesUnitIdLink(
                    consumedHorse.StableId,
                    consumedHorse.Slot,
                    consumedHorse.UnitId,
                    consumedHorse.UnitGlobalId,
                    bidirectional: true);
            }

            bool rollbackMatches =
                GetStableHorseSlotUnitId(stable, consumedHorse.Slot) == consumedHorse.UnitId &&
                GetStableHorseSlotGlobalId(stable, consumedHorse.Slot) == consumedHorse.UnitGlobalId &&
                unit->r_LinkedStableBuildingId == consumedHorse.StableId &&
                unit->r_LinkedStableGlobalId == (uint)consumedHorse.StableGlobalId &&
                stable->r_TotalHorses == consumedHorse.TotalBefore &&
                stable->r_UsedHorses == consumedHorse.UsedBefore &&
                stable->r_HorseRechargeTimer == consumedHorse.RechargeBefore;
            if (!rollbackMatches)
            {
                LogError(
                    $"Knight dismount stable rollback was incomplete: reason={reason}, unitId={consumedHorse.UnitId}, " +
                    $"unitGlobalId={consumedHorse.UnitGlobalId}, {FormatStableState(consumedHorse.StableId, stable, consumedHorse.Slot)}.");
            }
        }

        private static bool TryFindStableHorseLink(int unitId, int unitGlobalId, out int stableId, out int slot)
        {
            stableId = 0;
            slot = -1;
            List<int> stableIds = new List<int>();
            GameBuildingManagerAPI.Instance.GetAllBuildings(stableIds, AliveState.IsAlive, eStructs.STRUCT_STABLES);

            for (int i = 0; i < stableIds.Count; i++)
            {
                int candidateStableId = stableIds[i];
                if (!global::APIShared.BuildingAccess.TryGetById(candidateStableId, out GameBuilding* stable, out _))
                    continue;

                for (int candidateSlot = 0; candidateSlot < StableHorseSlotCount; candidateSlot++)
                {
                    int linkedUnitId = GetStableHorseSlotUnitId(stable, candidateSlot);
                    int linkedGlobalId = GetStableHorseSlotGlobalId(stable, candidateSlot);
                    if ((unitId > 0 && linkedUnitId == unitId) || (unitGlobalId > 0 && linkedGlobalId == unitGlobalId))
                    {
                        stableId = candidateStableId;
                        slot = candidateSlot;
                        return true;
                    }
                }
            }

            return false;
        }

        private static string FormatStableState(int stableId, GameBuilding* stable, int slot)
        {
            return $"stableId={stableId}, stableGlobalId={stable->r_GlobalId}, total={stable->r_TotalHorses}, used={stable->r_UsedHorses}, recharge={stable->r_HorseRechargeTimer}, observedSlot={slot}, slots=[{stable->r_UsedHorse1UnitId}/{stable->r_UsedHorse1GlobalId},{stable->r_UsedHorse2UnitId}/{stable->r_UsedHorse2GlobalId},{stable->r_UsedHorse3UnitId}/{stable->r_UsedHorse3GlobalId},{stable->r_UsedHorse4UnitId}/{stable->r_UsedHorse4GlobalId}]";
        }

        private struct HorseAllocation
        {
            public int StableId;
            public int StableGlobalId;
            public int OwnerPlayerId;
            public int Slot;
        }

        private struct ConsumedStableHorse
        {
            public bool IsActive;
            public int StableId;
            public int StableGlobalId;
            public int Slot;
            public int UnitId;
            public int UnitGlobalId;
            public int TotalBefore;
            public int UsedBefore;
            public int RechargeBefore;
        }
    }
}
