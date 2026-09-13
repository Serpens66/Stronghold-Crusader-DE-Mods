using System;

namespace UnitCosts
{
    internal static class UnitExtraHorseCostPolicy
    {
        public static bool NormalizeHorseRequirement(int serializedValue, bool supported) =>
            supported && serializedValue > 0;

        public static int CalculateAvailableHorseSlots(int totalHorses, int usedHorses, int freeSlots)
        {
            int total = Math.Max(0, Math.Min(4, totalHorses));
            int used = Math.Max(0, Math.Min(4, usedHorses));
            int free = Math.Max(0, Math.Min(4, freeSlots));
            return Math.Min(Math.Max(0, total - used), free);
        }

        public static int ApplyHorseAffordabilityLimit(int currentLimit, int availableHorses) =>
            Math.Min(currentLimit, Math.Max(0, availableHorses));

        public static bool TryCalculateConsumedHorseTotal(
            int totalHorses,
            int usedHorses,
            out int totalAfterConsumption)
        {
            totalAfterConsumption = totalHorses;
            if (totalHorses < 1 || totalHorses > 4 ||
                usedHorses < 0 || usedHorses > 4)
            {
                return false;
            }

            totalAfterConsumption = totalHorses - 1;
            return true;
        }

        public static bool IsStableHorseSlotPairComplete(int unitId, long unitGlobalId) =>
            (unitId == 0) == (unitGlobalId == 0);

        public static bool IsOccupiedStableHorseSlotCountValid(int totalHorses, int occupiedSlots) =>
            totalHorses >= 0 && totalHorses <= 4 &&
            occupiedSlots >= 0 && occupiedSlots <= 4 &&
            occupiedSlots <= totalHorses;
    }
}
