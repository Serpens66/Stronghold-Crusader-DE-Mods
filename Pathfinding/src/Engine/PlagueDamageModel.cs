using System;
using SHCDESE.Interop;

namespace Pathfinding.Engine
{
    // Terrain reads of the plague damage model (native packed tile ids, FBCB9319; UpdateToNewDLL.md,
    // "Plague clouds"). Production: PlagueRouteRules.NativeTerrain; tests: arrays.
    internal interface IPlagueTerrain
    {
        uint Flags(int tile);
        int RawHeight(int tile);
        int DefaultHeight(int tile);
        // The signed short building layer (0 none, negative or above 3999 invalid).
        int BuildingId(int tile);
        bool TryGetBuilding(int buildingId, out eStructs type, out int baseHeight);
    }

    // APIShared's plague height policy as its native stub applies it: per lane the tolerance + 1, 0 when the
    // lane is off (PlagueHeightAssembly: walls, gates, towers, keeps). Inactive (all 0) means Vanilla rules.
    internal struct PlagueHeightRules
    {
        public int Walls, Gates, Towers, Keeps;
        public bool Active => (Walls | Gates | Towers | Keeps) != 0;
    }

    // Whether a unit standing on a tile takes damage from a type-22 plague cloud on another tile (audit
    // 02.11.2026, FBCB9319): 0x9C730 damages the cloud tile (ring 0), its 8 neighbours (ring 1) and the
    // 16-tile ring (ring 2), every 31 ticks while the cloud is in state 2, fade included.
    // - Vanilla: neighbours and ring only with |H(cloud) - H(tile)| < 120 (0x6B910); 0x192750 spares tiles
    //   with IsElevated|IsWall, keeps, gates (types 40..47) and towers (74..78).
    // - APIShared policy active: no 120 gate; the tile's lane (keep, gate, tower building, wall or stairs
    //   flag, elevated, ground) decides: a lane that is off (elevated always) keeps Vanilla's exclusion,
    //   otherwise the standing planes must differ by at most the lane's tolerance (ground: the largest).
    // The unit's standing height is taken as the tile's plane (exact for standing units on ordinary tiles,
    // see UpdateToNewDLL.md). Unit-type immunity (0x32BC40) and healer resistance are per unit, not here.
    // Members are public: the test harness turns 8-space "internal" members private.
    internal sealed class PlagueDamageModel
    {
        public const int NativeTileCount = 320800, MaximumBuildingId = 3999, VanillaHeightGate = 120;
        // Tile flags (0x48F71B0): IsWall 0x100, building 0x400, stairs 0x800, IsElevated 0x10000000.
        public const uint WallFlag = 0x100u, BuildingFlag = 0x400u, StairsFlag = 0x800u, ElevatedFlag = 0x10000000u;
        public const uint VanillaImmuneFlags = WallFlag | ElevatedFlag;
        // GenerateSurface offsets of the APIShared stub (stairs, wall backed by a building, building tile).
        public const int StairsPlane = 12, WallPlane = 40, BuildingPlane = 2;
        public enum Lane { Ground, Walls, Gates, Towers, Keeps, Elevated }

        private readonly IPlagueTerrain terrain;
        public PlagueDamageModel(IPlagueTerrain terrain) { this.terrain = terrain; }

        public bool WouldDamage(int cloudTile, int tile, int ring, PlagueHeightRules rules)
        {
            if ((uint)tile >= NativeTileCount || (uint)cloudTile >= NativeTileCount || ring < 0 || ring > 2) return false;
            if (!rules.Active)
            {
                if (ring > 0 && Math.Abs(Surface(cloudTile) - Surface(tile)) >= VanillaHeightGate) return false;
                return VanillaGate(tile);
            }
            int cloudPlane = Plane(cloudTile);
            if (cloudPlane < 0) return false;
            if (!TryGetLane(tile, out Lane lane)) return false;
            if (lane == Lane.Elevated) return VanillaGate(tile);
            int tolerance = lane switch
            {
                Lane.Walls => rules.Walls, Lane.Gates => rules.Gates, Lane.Towers => rules.Towers, Lane.Keeps => rules.Keeps,
                _ => Math.Max(Math.Max(rules.Walls, rules.Gates), Math.Max(rules.Towers, rules.Keeps)),
            };
            if (tolerance == 0) return VanillaGate(tile);
            int plane = Plane(tile);
            return plane >= 0 && Math.Abs(plane - cloudPlane) <= tolerance - 1;
        }

        // A tile on walls, stairs, gates, towers, keeps or other raised structures (EscapePlagueRaised).
        public bool IsRaised(int tile) => (uint)tile < NativeTileCount && TryGetLane(tile, out Lane lane) && lane != Lane.Ground;

        // 0x192750's tile and building exclusions.
        public bool VanillaGate(int tile)
        {
            if ((terrain.Flags(tile) & VanillaImmuneFlags) != 0) return false;
            int building = terrain.BuildingId(tile);
            if (building != 0 && terrain.TryGetBuilding(building, out eStructs type, out _) &&
                (type >= eStructs.STRUCT_KEEP_ONE && type <= eStructs.STRUCT_GATE_WOOD || type >= eStructs.STRUCT_TOWER1 && type <= eStructs.STRUCT_TOWER5)) return false;
            return true;
        }

        // 0x6B910: raw height, or a building's roof (0xC07C0) on its base (DefaultHeight for main and inner gates).
        public int Surface(int tile)
        {
            int building = terrain.BuildingId(tile);
            if (building == 0) return terrain.RawHeight(tile);
            if (!terrain.TryGetBuilding(building, out eStructs type, out _)) return terrain.RawHeight(tile);
            bool gate = type == eStructs.STRUCT_GATE_MAIN || type == eStructs.STRUCT_GATE_INNER;
            return Roof(type) + (gate ? terrain.DefaultHeight(tile) : terrain.RawHeight(tile));
        }

        // The APIShared stub's standing plane (GenerateSurface), -1 when invalid.
        public int Plane(int tile)
        {
            if ((uint)tile >= NativeTileCount) return -1;
            int height = terrain.RawHeight(tile);
            uint flags = terrain.Flags(tile);
            if ((flags & StairsFlag) != 0) return height + StairsPlane;
            if ((flags & WallFlag) != 0)
            {
                int wallBuilding = terrain.BuildingId(tile);
                if (wallBuilding == 0) return height;
                return wallBuilding < 1 || wallBuilding > MaximumBuildingId ? -1 : height + WallPlane;
            }
            if ((flags & BuildingFlag) != 0) return height + BuildingPlane;
            if ((flags & ElevatedFlag) != 0)
            {
                int building = terrain.BuildingId(tile);
                if (building < 1 || building > MaximumBuildingId || !terrain.TryGetBuilding(building, out eStructs type, out int baseHeight)) return -1;
                return baseHeight + Roof(type);
            }
            return height;
        }

        // The stub's lane: keep, gate or tower building (incl. destroyed towers), else wall or stairs flag,
        // else elevated (Vanilla rules), else ground. False for an invalid building id.
        public bool TryGetLane(int tile, out Lane lane)
        {
            lane = Lane.Ground;
            int building = terrain.BuildingId(tile);
            if (building != 0)
            {
                if (building < 1 || building > MaximumBuildingId) return false;
                if (terrain.TryGetBuilding(building, out eStructs type, out _) && TryGetBuildingLane(type, out lane)) return true;
            }
            uint flags = terrain.Flags(tile);
            if ((flags & (WallFlag | StairsFlag)) != 0) lane = Lane.Walls;
            else if ((flags & ElevatedFlag) != 0) lane = Lane.Elevated;
            else lane = Lane.Ground;
            return true;
        }

        // PlagueHeightAssembly.BuildingCategories.
        public static bool TryGetBuildingLane(eStructs type, out Lane lane)
        {
            switch (type)
            {
                case eStructs.STRUCT_KEEP_ONE: case eStructs.STRUCT_KEEP_TWO: case eStructs.STRUCT_KEEP_THREE: case eStructs.STRUCT_KEEP_FOUR:
                case eStructs.STRUCT_KEEP_FIVE: case eStructs.STRUCT_KEEPDOOR_LEFT: case eStructs.STRUCT_KEEPDOOR_RIGHT: case eStructs.STRUCT_KEEPDOOR:
                    lane = Lane.Keeps; return true;
                case eStructs.STRUCT_GATE_MAIN: case eStructs.STRUCT_GATE_INNER: case eStructs.STRUCT_GATE_WOOD: case eStructs.STRUCT_GATE_POSTERN:
                case eStructs.STRUCT_GATEHOUSE: case eStructs.STRUCT_GATE_WOOD1A: case eStructs.STRUCT_GATE_WOOD1B: case eStructs.STRUCT_GATE_WOOD1C:
                case eStructs.STRUCT_GATE_WOOD1D: case eStructs.STRUCT_GATE_STONE1A: case eStructs.STRUCT_GATE_STONE1B: case eStructs.STRUCT_GATE_STONE2A:
                case eStructs.STRUCT_GATE_STONE2B:
                    lane = Lane.Gates; return true;
                case eStructs.STRUCT_TOWER: case eStructs.STRUCT_TOWER1: case eStructs.STRUCT_TOWER2: case eStructs.STRUCT_TOWER3: case eStructs.STRUCT_TOWER4:
                case eStructs.STRUCT_TOWER5: case eStructs.STRUCT_TOWER5_DESTROYED: case eStructs.STRUCT_TOWER1_DESTROYED: case eStructs.STRUCT_TOWER2_DESTROYED:
                case eStructs.STRUCT_TOWER3_DESTROYED: case eStructs.STRUCT_TOWER4_DESTROYED:
                    lane = Lane.Towers; return true;
                default: lane = Lane.Ground; return false;
            }
        }

        // 0xC07C0 (roof height of a building type), all other types 0.
        public static int Roof(eStructs type)
        {
            switch (type)
            {
                case eStructs.STRUCT_KEEP_ONE: return 64;
                case eStructs.STRUCT_KEEP_TWO: return 92;
                case eStructs.STRUCT_KEEP_THREE: return 190;
                case eStructs.STRUCT_GATE_MAIN: case eStructs.STRUCT_GATE_INNER: return 128;
                case eStructs.STRUCT_SIEGE_TOWER: return 118;
                case eStructs.STRUCT_TOWER1: return 296;
                case eStructs.STRUCT_TOWER2: return 148;
                case eStructs.STRUCT_TOWER3: return 180;
                case eStructs.STRUCT_TOWER4: case eStructs.STRUCT_TOWER5: return 192;
                default: return 0;
            }
        }
    }
}
