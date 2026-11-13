// Feature: Pure tile eligibility rules for Assassin wall-climb transitions.
using SHCDESE.Interop.Enums;

namespace Pathfinding
{
    internal static class AssassinClimbTransitionPolicy
    {
        // Goodsyard uses IsWall for its walkable platform, not an Assassin climbing
        // surface. Ordinary connections still win before this fallback is evaluated.
        public static bool HasClimbWall(uint sourceFlags, uint targetFlags)
        {
            const uint required = (uint)TilePropertyFlag.IsWall;
            const uint excluded = (uint)TilePropertyFlag.GoodsyardRelated;
            return (sourceFlags & (required | excluded)) == required ||
                (targetFlags & (required | excluded)) == required;
        }

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
