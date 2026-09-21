using SHCDESE.API;
using SHCDESE.Interop;
using Shared;

namespace RandomEvents
{
    internal static unsafe class KeepAnchorResolver
    {
        public static bool TryGetCenter(
            GameBuilding* keep,
            out double tileX,
            out double tileY,
            out string geometrySource,
            out string failure)
        {
            tileX = 0;
            tileY = 0;
            geometrySource = string.Empty;
            failure = string.Empty;
            if (keep == null)
            {
                failure = "the Keep pointer is null";
                return false;
            }

            if (GameBuildingFootprint.TryGetBounds(keep, out GameBuildingFootprintBounds footprint))
            {
                tileX = footprint.CenterXTimesTwo / 2.0;
                tileY = footprint.CenterYTimesTwo / 2.0;
                geometrySource = "occupied-tiles";
                return true;
            }

            if (KeepAnchorGeometry.TryGetGridCenter(
                    keep->r_TilePositionXBegin,
                    keep->r_TilePositionYBegin,
                    keep->r_OccupyTileGridSize,
                    GameTileManagerAPI.Instance.IsTileInsideMapBounds,
                    out tileX,
                    out tileY))
            {
                geometrySource = "validated-grid-fallback";
                return true;
            }

            failure =
                $"occupied tiles and grid fallback are invalid: begin=({keep->r_TilePositionXBegin}," +
                $"{keep->r_TilePositionYBegin}), gridSize={keep->r_OccupyTileGridSize}";
            return false;
        }
    }
}
