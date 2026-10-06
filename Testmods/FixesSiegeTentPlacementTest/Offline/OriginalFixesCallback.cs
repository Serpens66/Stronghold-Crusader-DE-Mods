using System;
namespace Offline;
internal static unsafe class OriginalFixesCallback { internal static int Evaluate(int footprintTileId, int centerTileId, int playerId) {
                    GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
                    GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;

                    int footprintY = tileApi.MapColumnLookupTable[footprintTileId];
                    int centerY = tileApi.MapColumnLookupTable[centerTileId];
                    if ((uint)footprintY >= GameTileManagerAPI.MAX_WIDTH || (uint)centerY >= GameTileManagerAPI.MAX_WIDTH)
                        return 0;

                    int footprintX = footprintTileId - tileApi.MapRowLookupTable[3 * footprintY];
                    int centerX = centerTileId - tileApi.MapRowLookupTable[3 * centerY];
                    if ((uint)footprintX >= GameTileManagerAPI.MAX_WIDTH || (uint)centerX >= GameTileManagerAPI.MAX_WIDTH)
                        return 0;

                    // The original 5x5 gap check is around each footprint tile
                    for (int y = Math.Max(0, footprintY - 2); y <= Math.Min(GameTileManagerAPI.MAX_WIDTH - 1, footprintY + 2); y++)
                    {
                        int row = tileApi.MapRowLookupTable[3 * y];
                        for (int x = Math.Max(0, footprintX - 2); x <= Math.Min(GameTileManagerAPI.MAX_WIDTH - 1, footprintX + 2); x++)
                            if (tileApi.TileManager.StructureGrid[row + x] != 0)
                                return 0;
                    }

                    if (playerApi.IsPlayerIdValid(playerId))
                        return 1;

                    if (!playerApi.TryGetPlayerResourcesById(playerId, out GamePlayerResources* attacker))
                        return 1;

                    int targetId = (int)attacker->r_SiegeAttackTargetPlayerId;
                    if (playerApi.IsPlayerIdValid(targetId) || targetId == playerId ||
                        !playerApi.TryGetPlayerResourcesById(targetId, out GamePlayerResources* target))
                        return 1;

                    int targetX = (int)target->r_KeepDoorTilePositionX;
                    int targetY = (int)target->r_KeepDoorTilePositionY;
                    if ((uint)targetX >= GameTileManagerAPI.MAX_WIDTH || (uint)targetY >= GameTileManagerAPI.MAX_WIDTH || (targetX | targetY) == 0)
                        return 1;

                    // Look only near the candidate. Distant sites use the games original search if terrain prevents a local line.
                    Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
                    const int nearby = 8;
                    for (int y = Math.Max(0, centerY - nearby); y <= Math.Min(GameTileManagerAPI.MAX_WIDTH - 1, centerY + nearby); y++)
                    {
                        int row = tileApi.MapRowLookupTable[3 * y];
                        for (int x = Math.Max(0, centerX - nearby); x <= Math.Min(GameTileManagerAPI.MAX_WIDTH - 1, centerX + nearby); x++)
                        {
                            int buildingId = tileApi.TileManager.StructureGrid[row + x];
                            if (buildingId == 0 || buildingId > buildings.Length)
                                continue;

                            ref GameBuilding building = ref buildings[buildingId - 1];
                            if (building.r_PlayerIdOwner != playerId)
                                continue;

                            eStructs type = building.r_BuildingType;
                            if (type != eStructs.STRUCT_SIEGE_TENT && type != eStructs.STRUCT_SIEGE_TENT_ARAB_BALLISTA && (type < eStructs.STRUCT_SIEGE_TENT_CATAPULT || type > eStructs.STRUCT_SIEGE_TENT_PORTABLE_SHIELD))
                                continue;

                            int tentX = building.r_TilePositionXBegin + 1;
                            int tentY = building.r_TilePositionYBegin + 1;
                            int towardX = targetX - tentX;
                            int towardY = targetY - tentY;
                            int scale = Math.Max(Math.Abs(towardX), Math.Abs(towardY));
                            if (scale >= 4 && (centerX - tentX) * towardX + (centerY - tentY) * towardY > scale)
                                return 0;
                        }
                    }
                    return 1;
} }
