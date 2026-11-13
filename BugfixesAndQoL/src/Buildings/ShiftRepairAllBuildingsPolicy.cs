// Feature: Decide whether a building is eligible for an additional Shift-repair action.
namespace BugfixesAndQoL
{
    internal static class ShiftRepairAllBuildingsPolicy
    {
        public static bool ShouldQueueAdditionalRepair(
            int buildingId,
            int selectedBuildingId,
            int ownerPlayerId,
            int controlledPlayerId,
            bool isAlive,
            int currentHealth,
            int maximumHealth,
            uint globalId,
            bool vanillaShowsRepair)
        {
            return buildingId > 0 &&
                buildingId != selectedBuildingId &&
                controlledPlayerId > 0 &&
                ownerPlayerId == controlledPlayerId &&
                isAlive &&
                globalId > 0 &&
                maximumHealth > 0 &&
                currentHealth >= 0 &&
                currentHealth < maximumHealth &&
                vanillaShowsRepair;
        }
    }
}
