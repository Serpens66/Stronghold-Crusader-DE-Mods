using SHCDESE.API;
using SHCDESE.Interop;

namespace Shared
{
    internal readonly struct GameBuildingFootprintBounds
    {
        public GameBuildingFootprintBounds(int minX, int minY, int maxX, int maxY)
        {
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public int MinX { get; }
        public int MinY { get; }
        public int MaxX { get; }
        public int MaxY { get; }
        public int CenterXTimesTwo => MinX + MaxX;
        public int CenterYTimesTwo => MinY + MaxY;
    }

    internal static unsafe class GameBuildingFootprint
    {
        public const int MaximumGridSize = 6;
        public const int MaximumTileCount = MaximumGridSize * MaximumGridSize;

        public static bool TryGetBounds(GameBuilding* building, out GameBuildingFootprintBounds bounds)
        {
            bounds = default(GameBuildingFootprintBounds);
            if (building == null)
                return false;

            uint gridSize = building->r_OccupyTileGridSize;
            if (gridSize == 0 || gridSize > MaximumGridSize)
                return false;

            int tileCount = checked((int)(gridSize * gridSize));
            uint* occupiedTileIds = &building->r_OccupiedTileIdsArrayBegin;
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            for (int index = 0; index < tileCount; index++)
            {
                uint rawTileId = occupiedTileIds[index];
                if (rawTileId > int.MaxValue)
                    return false;
                int tileId = (int)rawTileId;
                if (!tiles.IsValidTileId(tileId))
                    return false;

                UnmanagedVector2<ushort> position = tiles.GetTileVectorFromId(tileId);
                if (position.X < minX) minX = position.X;
                if (position.Y < minY) minY = position.Y;
                if (position.X > maxX) maxX = position.X;
                if (position.Y > maxY) maxY = position.Y;
            }

            bounds = new GameBuildingFootprintBounds(minX, minY, maxX, maxY);
            return true;
        }

        public static bool TryGetBounds(ref GameBuilding building, out GameBuildingFootprintBounds bounds)
        {
            fixed (GameBuilding* buildingPointer = &building)
                return TryGetBounds(buildingPointer, out bounds);
        }

        public static bool TryGetBounds(GameBuilding building, out GameBuildingFootprintBounds bounds)
        {
            return TryGetBounds(ref building, out bounds);
        }

        public static bool ContainsTileId(GameBuilding* building, int tileId)
        {
            if (building == null || tileId < 0)
                return false;

            uint gridSize = building->r_OccupyTileGridSize;
            if (gridSize == 0 || gridSize > MaximumGridSize)
                return false;

            int tileCount = checked((int)(gridSize * gridSize));
            uint* occupiedTileIds = &building->r_OccupiedTileIdsArrayBegin;
            for (int index = 0; index < tileCount; index++)
            {
                if (occupiedTileIds[index] == (uint)tileId)
                    return true;
            }

            return false;
        }

        public static bool ContainsTileId(ref GameBuilding building, int tileId)
        {
            fixed (GameBuilding* buildingPointer = &building)
                return ContainsTileId(buildingPointer, tileId);
        }

        public static bool ContainsTileId(GameBuilding building, int tileId)
        {
            return ContainsTileId(ref building, tileId);
        }
    }
}
