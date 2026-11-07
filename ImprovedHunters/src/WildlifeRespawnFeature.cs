using BepInEx.Logging;
using RedBird.Core.Memory;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Runtime.InteropServices;

namespace ImprovedHunters
{
    /// <summary>
    /// Brings new herds of deer or goats when the map runs low, in the game modes where Vanilla's own
    /// respawn does not run. Uses Vanilla's herd creation, so herd sizes and behaviour stay Vanilla's.
    /// Everything is derived from the map tick and simulation data, so all peers act alike and a
    /// saved game keeps the rhythm without any saved state.
    /// </summary>
    internal sealed unsafe class WildlifeRespawnFeature
    {
        private const int DeerHerdKind = 0xDC;
        private const int GoatHerdKind = 0xE2;
        private const int PlacesTried = 32;
        private const int MinimumDistanceToLord = 40;
        private const int MaximumLords = 8;
        private const int MapCenter = 400;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate int SpawnHerdDelegate(IntPtr herdManager, int kind, uint tileX, uint tileY, int height);

        private readonly ManualLogSource log;
        private readonly ImprovedHuntersViewModel settings;
        private readonly WildlifeNativeAddresses addresses;
        private readonly SpawnHerdDelegate spawnHerd;
        private bool failureLogged;

        public WildlifeRespawnFeature(ManualLogSource log, ImprovedHuntersViewModel settings, WildlifeNativeAddresses addresses)
        {
            this.log = log;
            this.settings = settings;
            this.addresses = addresses;
            spawnHerd = Marshal.GetDelegateForFunctionPointer<SpawnHerdDelegate>((IntPtr)(long)addresses.SpawnHerd);
        }

        public void OnTick(int mapTick)
        {
            if (!settings.RespawnWildlife ||
                unchecked((uint)mapTick) % (uint)(settings.RespawnIntervalSeconds * HunterSimulationTime.MapTicksPerSecond) != 0 ||
                *(int*)addresses.MultiplayerMode == 0)
            {
                return;
            }

            try
            {
                Respawn(mapTick);
            }
            catch (Exception exception)
            {
                if (failureLogged)
                    return;

                failureLogged = true;
                Shared.DebugLogHelper.LogError(log, $"Improved Hunters wildlife respawn failed: {exception}");
            }
        }

        private void Respawn(int mapTick)
        {
            bool deerAllowed = settings.HuntDeer, goatsAllowed = settings.HuntGoat;
            if (!deerAllowed && !goatsAllowed)
                return;

            CountLivingPrey(out int deer, out int goats);
            int counted = (deerAllowed ? deer : 0) + (goatsAllowed ? goats : 0);
            if (counted >= settings.RespawnMinimum)
                return;

            bool spawnDeer = deerAllowed && (!goatsAllowed || deer <= goats);
            int kind = spawnDeer ? DeerHerdKind : GoatHerdKind;
            if (TrySpawnAtVanillaPoint(kind, spawnDeer) || TrySpawnAtFreePlace(kind, mapTick))
                return;

            Shared.DebugLogHelper.LogInfo(log, $"Improved Hunters found no place for new wildlife at map tick {mapTick}.");
        }

        private static void CountLivingPrey(out int deer, out int goats)
        {
            deer = 0;
            goats = 0;
            SimpleNativeArray<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitArray();
            for (int index = 0; index < units.Length; index++)
            {
                GameUnit* unit = units.GetValuePointer(index);
                if (unit->r_AliveState != AliveState.IsAlive || unit->r_CurrentHealth == 0)
                    continue;
                if (unit->r_UnitChimp == eChimps.CHIMP_TYPE_DEER)
                    deer++;
                else if (unit->r_UnitChimp == eChimps.CHIMP_TYPE_GOAT)
                    goats++;
            }
        }

        // Where this map's first herds of this kind appeared, Vanilla's own respawn places.
        private bool TrySpawnAtVanillaPoint(int kind, bool isDeer)
        {
            short* pointsX = (short*)(isDeer ? addresses.DeerPointsX : addresses.GoatPointsX);
            short* pointsY = (short*)(isDeer ? addresses.DeerPointsY : addresses.GoatPointsY);
            for (int index = 0; index < WildlifeNativeAddresses.SpawnPointsPerKind; index++)
            {
                // The table holds an X and a Y word per entry, four bytes apart.
                int x = pointsX[index * 2], y = pointsY[index * 2];
                if (x > 0 && y > 0 && TrySpawn(kind, x, y))
                    return true;
            }

            return false;
        }

        private bool TrySpawnAtFreePlace(int kind, int mapTick)
        {
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            // The map is a diamond of this radius around the middle of the 800 x 800 grid.
            int radius = tiles.GetCurrentMapSize() / 2;
            if (radius <= 0)
                return false;

            uint span = (uint)(radius * 2 + 1);
            uint seed = unchecked((uint)mapTick * 2654435761u + 1u);
            for (int attempt = 0; attempt < PlacesTried; attempt++)
            {
                seed = unchecked(seed * 1664525u + 1013904223u);
                int x = MapCenter - radius + (int)((seed >> 8) % span);
                seed = unchecked(seed * 1664525u + 1013904223u);
                int y = MapCenter - radius + (int)((seed >> 8) % span);
                if (tiles.IsTileInsideMapBounds(x, y) && IsReachableFromALord(tiles, x, y) && TrySpawn(kind, x, y))
                    return true;
            }

            return false;
        }

        // Herds nobody can reach are of no use, and a herd beside a keep would run away at once.
        private static bool IsReachableFromALord(GameTileManagerAPI tiles, int x, int y)
        {
            Span<ushort> components = GamePathingManagerAPI.Instance.GetPathComponentGrid();
            int tile = tiles.GetTileId(x, y);
            if (!tiles.IsValidTileId(tile) || (uint)tile >= (uint)components.Length)
                return false;

            bool reachable = false;
            for (int player = 1; player <= MaximumLords; player++)
            {
                if (!APIShared.UnitAccess.TryGetById(GamePlayerManagerAPI.Instance.GetLordUnitId(player), out GameUnit* lord, out _) || lord == null)
                    continue;

                if (Math.Max(Math.Abs(lord->r_CurrentTilePositionX - x), Math.Abs(lord->r_CurrentTilePositionY - y)) < MinimumDistanceToLord)
                    return false;

                int lordTile = tiles.GetTileId(lord->r_CurrentTilePositionX, lord->r_CurrentTilePositionY);
                reachable |= tiles.IsValidTileId(lordTile) &&
                    (uint)lordTile < (uint)components.Length &&
                    components[lordTile] == components[tile];
            }

            return reachable;
        }

        private bool TrySpawn(int kind, int x, int y)
        {
            GameTileManagerAPI tiles = GameTileManagerAPI.Instance;
            int tile = tiles.GetTileId(x, y);
            if (!tiles.IsValidTileId(tile))
                return false;

            int herd = spawnHerd((IntPtr)(long)addresses.HerdManager, kind, (uint)x, (uint)y, tiles.GetTileHeight(tile));
            if (herd <= 0)
                return false;

            Shared.DebugLogHelper.LogInfo(log, $"Improved Hunters new wildlife: kind=0x{kind:X}, herd={herd}, tile=({x},{y}).");
            return true;
        }
    }
}
