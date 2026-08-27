// Feature: Pure tile eligibility rules for Assassin wall-climb transitions.
namespace BugfixesAndQoL
{
    internal static class AssassinClimbTransitionPolicy
    {
        public static bool CanUseStartTile(
            bool allowWalkableReservedStartTiles,
            ushort buildingId,
            byte movementMask)
        {
            // Vanilla accepts only building ID zero. The improved pathfinder may
            // additionally use reservations that the native movement grid marks walkable.
            return buildingId == 0 ||
                (allowWalkableReservedStartTiles && movementMask != 0);
        }

        public static bool CanUseTargetTile(ushort buildingId)
        {
            // Do not relax the destination side: building-backed targets are not
            // proven wall surfaces and remain subject to Vanilla's strict check.
            return buildingId == 0;
        }

        public static bool ShouldRelaxPathReconstruction(
            bool enableMod,
            bool enableImprovedAssassinPathfinding,
            bool pathfinderInstalled)
        {
            return enableMod && enableImprovedAssassinPathfinding && pathfinderInstalled;
        }
    }
}
