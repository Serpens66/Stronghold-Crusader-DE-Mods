namespace ExtraFeatures
{
    internal static class StableHorseConsumptionPolicy
    {
        internal const int SlotCount = 4;

        internal static bool TryGetTotalAfterConsumption(
            int totalHorses,
            int usedHorses,
            int occupiedSlotCount,
            bool slotsStructurallyValid,
            bool instantHorse,
            out int totalAfterConsumption)
        {
            totalAfterConsumption = totalHorses;
            if (!slotsStructurallyValid ||
                totalHorses < 1 || totalHorses > SlotCount ||
                usedHorses < 0 || usedHorses > SlotCount ||
                occupiedSlotCount < 1 || occupiedSlotCount > totalHorses)
            {
                return false;
            }

            totalAfterConsumption = instantHorse ? totalHorses : totalHorses - 1;
            return true;
        }
    }
}
