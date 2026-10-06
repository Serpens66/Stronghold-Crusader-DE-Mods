using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
namespace Offline;
internal enum eStructs { STRUCT_SIEGE_TENT=29, STRUCT_SIEGE_TENT_ARAB_BALLISTA=54, STRUCT_SIEGE_TENT_CATAPULT=80, STRUCT_SIEGE_TENT_PORTABLE_SHIELD=84 }
internal struct GameBuilding { public int r_PlayerIdOwner; public eStructs r_BuildingType; public int r_TilePositionXBegin, r_TilePositionYBegin; }
internal struct GamePlayerResources { public int r_SiegeAttackTargetPlayerId, r_KeepDoorTilePositionX, r_KeepDoorTilePositionY; }
internal sealed class TileView { public ushort[] StructureGrid; }
internal sealed unsafe class GameTileManagerAPI
{
    internal const int MAX_WIDTH = 800;
    internal static GameTileManagerAPI Instance;
    internal int* MapRowLookupTable;
    internal ushort* MapColumnLookupTable;
    internal TileView TileManager;
}
internal sealed class GameBuildingManagerAPI
{
    internal static GameBuildingManagerAPI Instance = new();
    internal GameBuilding[] Buildings = Array.Empty<GameBuilding>();
    internal Span<GameBuilding> GetBuildingsAsSpan() => Buildings;
}
internal sealed unsafe class GamePlayerManagerAPI
{
    internal static GamePlayerManagerAPI Instance = new();
    internal readonly GamePlayerResources* Players = (GamePlayerResources*)NativeMemory.AllocZeroed(9,(nuint)sizeof(GamePlayerResources));
    internal bool IsPlayerIdValid(int id) => id >= 1 && id <= 8;
    internal bool TryGetPlayerResourcesById(int id, out GamePlayerResources* resources)
    { resources = IsPlayerIdValid(id) ? Players + id : null; return resources != null; }
}
