// Feature: Pure placement policy for hostile one-tile clearance rings.
using System;
using SHCDESE.Extensions;
using SHCDESE.Interop;

namespace BugfixesAndQoL
{
    internal static class TunnelPlacementDistancePolicy
    {
        internal const int TunnelFootprintSize = 3;
        internal const int MaximumMoatRecordId = 63999;

        internal static bool IsTunnelMapper(eMappers mapper) =>
            mapper == eMappers.MAPPER_TUNNEL ||
            mapper == eMappers.MAPPER_TUNNEL_CONSTRUCTION;

        internal static bool IsPlaceableBuildingMapper(eMappers mapper, int footprintSize) =>
            footprintSize > 0 && mapper.ConvertToEStructs() != eStructs.STRUCT_NULL;

        internal static bool ShouldApply(
            bool modEnabled,
            bool fixEnabled,
            bool isMapEditor,
            bool isAiPlayer,
            eMappers mapper,
            int footprintSize) =>
            modEnabled &&
            fixEnabled &&
            !isMapEditor &&
            !isAiPlayer &&
            IsPlaceableBuildingMapper(mapper, footprintSize);

        internal static bool HasHostileOuterRingTile(
            int anchorX,
            int anchorY,
            int footprintSize,
            Func<int, int, bool> isInsideMap,
            Func<int, int, bool> isHostileTile)
        {
            if (footprintSize <= 0)
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

        internal static bool TryResolveCompletedMoatOwner(
            int tileId,
            int moatId,
            int moatRecordCount,
            int recordTileId,
            int recordOwnerId,
            Func<int, bool> isPlayerIdValid,
            out int ownerId)
        {
            if (isPlayerIdValid == null)
                throw new ArgumentNullException(nameof(isPlayerIdValid));

            ownerId = 0;
            if (moatId <= 0 || moatId > MaximumMoatRecordId ||
                moatRecordCount <= 0 || moatRecordCount > MaximumMoatRecordId + 1 ||
                moatId >= moatRecordCount || recordTileId != tileId ||
                !isPlayerIdValid(recordOwnerId))
            {
                return false;
            }

            ownerId = recordOwnerId;
            return true;
        }

        internal static bool IsHostileOwner(
            int placingPlayerId,
            int ownerId,
            Func<int, int, bool> isAllied) =>
            ownerId != placingPlayerId && !isAllied(placingPlayerId, ownerId);
    }
}
