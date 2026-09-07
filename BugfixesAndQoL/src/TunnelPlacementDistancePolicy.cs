// Feature: Pure placement policy for the one-tile tunnel clearance ring.
using System;
using SHCDESE.Interop;

namespace BugfixesAndQoL
{
    internal static class TunnelPlacementDistancePolicy
    {
        internal const int TunnelFootprintSize = 3;

        internal static bool IsTargetMapper(eMappers mapper) =>
            mapper == eMappers.MAPPER_TUNNEL ||
            mapper == eMappers.MAPPER_TUNNEL_CONSTRUCTION;

        internal static bool ShouldApply(
            bool modEnabled,
            bool fixEnabled,
            bool isMapEditor,
            eMappers mapper,
            int footprintSize) =>
            modEnabled &&
            fixEnabled &&
            !isMapEditor &&
            IsTargetMapper(mapper) &&
            footprintSize == TunnelFootprintSize;

        internal static bool HasHostileOuterRingTile(
            int anchorX,
            int anchorY,
            int footprintSize,
            Func<int, int, bool> isInsideMap,
            Func<int, int, bool> isHostileTile)
        {
            if (footprintSize != TunnelFootprintSize)
                return false;
            if (isInsideMap == null)
                throw new ArgumentNullException(nameof(isInsideMap));
            if (isHostileTile == null)
                throw new ArgumentNullException(nameof(isHostileTile));

            // Vanilla treats the placement anchor as the top-left of its square footprint.
            for (int offsetY = -1; offsetY <= footprintSize; offsetY++)
            {
                for (int offsetX = -1; offsetX <= footprintSize; offsetX++)
                {
                    bool insideFootprint =
                        offsetX >= 0 && offsetX < footprintSize &&
                        offsetY >= 0 && offsetY < footprintSize;
                    if (insideFootprint)
                        continue;

                    int tileX = anchorX + offsetX;
                    int tileY = anchorY + offsetY;
                    if (isInsideMap(tileX, tileY) && isHostileTile(tileX, tileY))
                        return true;
                }
            }
            return false;
        }

        internal static int WallOwnerToGamePlayerId(byte zeroBasedWallOwner) =>
            zeroBasedWallOwner + 1;

        internal static bool IsHostileOwner(
            int placingPlayerId,
            int ownerId,
            Func<int, int, bool> isAllied) =>
            ownerId != placingPlayerId && !isAllied(placingPlayerId, ownerId);
    }
}
