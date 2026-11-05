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

        public static bool CanUseTargetTile(
            bool allowWalkableReservedTargetTiles,
            ushort buildingId,
            byte movementMask)
        {
            // A descent may land on the same kind of natively walkable reservation
            // that an ascent may start on. Tile and wall checks remain separate.
            return buildingId == 0 ||
                (allowWalkableReservedTargetTiles && movementMask != 0);
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
