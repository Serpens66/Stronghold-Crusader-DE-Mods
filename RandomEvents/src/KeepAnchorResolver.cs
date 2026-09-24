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

            if (KeepAnchorGeometry.TryGetReferenceTile(
                    keep->r_TilePositionXBegin,
                    keep->r_TilePositionYBegin,
                    keep->r_OccupyTileGridSize,
                    GameTileManagerAPI.Instance.IsTileInsideMapBounds,
                    out tileX,
                    out tileY))
            {
                geometrySource = "validated-keep-position";
                return true;
            }

            failure =
                $"Keep start tile is outside the playable map: begin=({keep->r_TilePositionXBegin}," +
                $"{keep->r_TilePositionYBegin})";
            return false;
        }
    }
}
