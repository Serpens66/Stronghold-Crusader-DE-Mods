using SHCDESE.Interop;
using Shared;

unsafe
{
    GameBuilding building = default;
    building.r_OccupyTileGridSize = GameBuildingFootprint.MaximumGridSize;
    uint* occupiedTileIds = &building.r_OccupiedTileIdsArrayBegin;
    int index = 0;
    for (int y = 20; y < 26; y++)
    for (int x = 10; x < 16; x++)
        occupiedTileIds[index++] = (uint)(y * 800 + x);

    Check(GameBuildingFootprint.MaximumTileCount == 36, "maximum footprint capacity");
    Check(GameBuildingFootprint.TryGetBounds(ref building, out GameBuildingFootprintBounds bounds), "36-tile bounds resolve");
    Check(bounds.MinX == 10 && bounds.MinY == 20 && bounds.MaxX == 15 && bounds.MaxY == 25, "36-tile bounds");
    Check(bounds.CenterXTimesTwo == 25 && bounds.CenterYTimesTwo == 45, "half-tile-safe center");
    Check(GameBuildingFootprint.ContainsTileId(ref building, 25 * 800 + 15), "last occupied tile included");
    Check(!GameBuildingFootprint.ContainsTileId(ref building, 25 * 800 + 16), "non-footprint tile excluded");

    building.r_OccupyTileGridSize = 7;
    Check(!GameBuildingFootprint.TryGetBounds(ref building, out _), "oversized footprint rejected");
    building.r_OccupyTileGridSize = 1;
    building.r_OccupiedTileIdsArrayBegin = 800u * 800u;
    Check(!GameBuildingFootprint.TryGetBounds(ref building, out _), "invalid tile ID rejected");
}

Console.WriteLine("PASS: occupied-tile footprint bounds, exact membership, 36-tile capacity, center, and fail-closed validation.");

static void Check(bool condition, string contract)
{
    if (!condition) throw new InvalidOperationException("Failed contract: " + contract);
}
