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
    }
}
