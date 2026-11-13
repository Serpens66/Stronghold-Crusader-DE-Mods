using System;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;

namespace Pathfinding.UnitCommands
{
    // Private route-core ABI. Surface classes restrict entry, never the starting tile.
    internal enum RouteSurfaceClass { Any = 0, LargeSearch = 1, RestrictedMovement = 2 }

    internal static class MovementSurfacePolicy
    {
        internal const byte Valid = 0x80, LargeAllowed = 1, RestrictedAllowed = 2;
        // Native machine masks shared by F32B0/DB650 and DCD60 (FBCB9319).
        private const uint WallOrElevated = 0x10000100u, Goodsyard = 2u;

        internal static byte EncodeTile(uint flags, int buildingId, bool whitelisted, bool valid = true)
        {
            if (!valid) return 0;
            bool ground = (flags & WallOrElevated) == 0;
            bool large = ground || (flags & Goodsyard) != 0 || whitelisted;
            // DCD60's goodsyard exception requires both bits and no linked building.
            bool restricted = ground || ((flags & 0x102u) == 0x102u && buildingId == 0) || whitelisted;
            return (byte)(Valid | (large ? LargeAllowed : 0) | (restricted ? RestrictedAllowed : 0));
        }

        internal static bool Allows(RouteSurfaceClass surface, byte metadata) =>
            surface == RouteSurfaceClass.Any ||
            ((metadata & Valid) != 0 &&
             (surface == RouteSurfaceClass.LargeSearch ? (metadata & LargeAllowed) != 0 :
              surface == RouteSurfaceClass.RestrictedMovement && (metadata & RestrictedAllowed) != 0));

        internal static RouteSurfaceClass Resolve(int unitType, int profile, bool bypass)
        {
            if (!bypass)
            {
                // DCD60's complete switch. Profile and the independent SE override are live.
                switch (unitType)
                {
                    case (int)eChimps.CHIMP_TYPE_KNIGHT:
                    case (int)eChimps.CHIMP_TYPE_LADDERMAN:
                    case (int)eChimps.CHIMP_TYPE_CATAPULT:
                    case (int)eChimps.CHIMP_TYPE_TREBUCHET:
                    case (int)eChimps.CHIMP_TYPE_SIEGE_TOWER:
                    case (int)eChimps.CHIMP_TYPE_BATTERING_RAM:
                    case (int)eChimps.CHIMP_TYPE_PORTABLE_SHIELD:
                    case (int)eChimps.CHIMP_TYPE_ARAB_HORSEMAN:
                    case (int)eChimps.CHIMP_TYPE_ARAB_BALLISTA:
                    case (int)eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER:
                    case (int)eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL:
                        return RouteSurfaceClass.RestrictedMovement;
                }
            }
            return profile == (int)PathfindingProfile.Large ? RouteSurfaceClass.LargeSearch : RouteSurfaceClass.Any;
        }
    }

    internal sealed unsafe partial class UnitCommandPathRuntime
    {
        private const int MovementSurfaceBuildingWhitelistRva = 0x2E68D0;
        private int* movementSurfaceBuildingWhitelist;
        private byte* movementSurfaceBuildingTypes;
        private int movementSurfaceBuildingTypeCount;

        // The owning constructor already requires the complete audited DLL hash. These two
        // independent readers pin the same whitelist and building-type addressing contract.
        private void InitializeMovementSurfacePolicy(ReadOnlySpan<byte> memory, ulong libraryBase)
        {
            ValidateExactBytes(memory, 0xF34E9,
                new byte[] { 0x44,0x39,0x94,0x8A,0xD0,0x68,0x2E,0x00 }, "F32B0 surface building whitelist");
            ValidateExactBytes(memory, 0xDCDFE,
                new byte[] { 0x41,0x83,0xBC,0x83,0xD0,0x68,0x2E,0x00,0x00 }, "DCD60 surface building whitelist");
            ValidateExactBytes(memory, 0xDCDF5,
                new byte[] { 0x4A,0x0F,0xBF,0x84,0x19,0xDE,0xCC,0x4C,0x06 }, "DCD60 building type");
            foreach (eStructs value in Enum.GetValues(typeof(eStructs)))
                movementSurfaceBuildingTypeCount = Math.Max(movementSurfaceBuildingTypeCount, (int)value + 1);
            if (movementSurfaceBuildingTypeCount <= 0 ||
                MovementSurfaceBuildingWhitelistRva > memory.Length - movementSurfaceBuildingTypeCount * sizeof(int))
                throw new InvalidOperationException("Movement surface whitelist is outside the validated image.");
            movementSurfaceBuildingWhitelist = (int*)(libraryBase + MovementSurfaceBuildingWhitelistRva);
            movementSurfaceBuildingTypes = (byte*)(libraryBase + NativeBuildingTypeBiasRva);
            Shared.DebugLogHelper.LogInfo(log,
                "Pathfinding movement surface rules ready; method=reference-rva whitelist=0x2E68D0 classes=3.");
        }

        internal RouteSurfaceClass ResolveMovementSurface(GameUnit* unit, int? effectiveProfile = null)
        {
            if (unit == null) return RouteSurfaceClass.RestrictedMovement;
            int type = (int)unit->r_UnitChimp;
            int profile;
            if (effectiveProfile.HasValue) profile = effectiveProfile.Value;
            else
            {
                var profiles = APIShared.ScriptExtenderReplacements.GamePathingManagerAPI.Instance.GetUnitTypePathfindingProfiles();
                if (!APIShared.IndexBounds.IsValidIndex(type, profiles.Length))
                    return RouteSurfaceClass.RestrictedMovement;
                profile = (int)profiles[type];
            }
            bool bypass = GamePathingManagerAPI.Instance.GetUnitTypeNextTileSurfaceValidationBypass(unit->r_UnitChimp);
            return MovementSurfacePolicy.Resolve(type, profile, bypass);
        }

        internal byte ReadMovementSurfaceTile(int nativeTileId)
        {
            if (!IsValidTileId(nativeTileId) || tileFlags == null || nativeBuildingLayer == null ||
                movementSurfaceBuildingWhitelist == null || movementSurfaceBuildingTypes == null) return 0;
            uint flags = tileFlags[nativeTileId];
            int building = nativeBuildingLayer[nativeTileId];
            bool whitelist = false;
            if ((flags & 0x10000100u) != 0 && building != 0)
            {
                // FBCB9319: tile layer stores a one-based ID; native building stride is 0x32C.
                // Only bounds needed for the actual dereference are imposed; dead records retain types.
                if (building <= 10000)
                {
                    int type = *(short*)(movementSurfaceBuildingTypes + building * WeightedMoatRoutePlanner.NativeBuildingRecordStride);
                    if ((uint)type < (uint)movementSurfaceBuildingTypeCount)
                        whitelist = movementSurfaceBuildingWhitelist[type] != 0;
                }
                // An unreadable linked type supplies no whitelist permission. A large-search
                // goodsyard remains allowed by its independently sufficient tile flag.
            }
            return MovementSurfacePolicy.EncodeTile(flags, building, whitelist);
        }

        internal bool IsMovementSurfaceAllowed(RouteSurfaceClass surface, int nativeTileId) =>
            MovementSurfacePolicy.Allows(surface, surface == RouteSurfaceClass.Any ? (byte)0 : ReadMovementSurfaceTile(nativeTileId));

        // Tiny type table only, not a map scan. A changed native whitelist requests the existing
        // chunked surface resweep; live publication checks already read the current values.
        internal ulong GetMovementSurfaceBuildingPolicyFingerprint()
        {
            ulong hash = 14695981039346656037UL;
            if (movementSurfaceBuildingWhitelist == null) return 0;
            for (int i = 0; i < movementSurfaceBuildingTypeCount; i++)
                hash = unchecked((hash ^ (movementSurfaceBuildingWhitelist[i] == 0 ? 0UL : 1UL)) * 1099511628211UL);
            return hash;
        }

        // Last boundary before 196280 consumes a builder/reconstruction result. It also covers
        // Vanilla's DA590 fallback after DB650 found no legal route and exception-barrier returns.
        internal int FilterPublishedMovementSurface(IntPtr pathManager, int result, int? effectiveProfile = null)
        {
            if (result <= 0 || nativeManualProbe || !EngineEnabled || pathManager == IntPtr.Zero || pathManager != nativePathManager ||
                nativeUnitManager == null) return result;
            byte* manager = (byte*)pathManager.ToPointer();
            byte* path = null;
            bool bound = false;
            try
            {
                path = *(byte**)(manager + PathManagerOutputBufferOffset);
                long offset = path - (nativeUnitManager + NativeUnitPathBufferOffset);
                if (path == null || offset <= 0 || offset % NativeUnitPathBufferStride != 0 ||
                    offset / NativeUnitPathBufferStride > MaximumUnitCount) return result;
                int unitId = (int)(offset / NativeUnitPathBufferStride);
                if (!APIShared.UnitAccess.TryGetById(unitId, out GameUnit* unit, out _) || unit == null ||
                    !APIShared.UnitAccess.IsReallyAlive(unit)) return result;
                bound = true;
                RouteSurfaceClass surface = ResolveMovementSurface(unit, effectiveProfile);
                if (surface == RouteSurfaceClass.Any) return result;
                GetNativeMovementStart(unit, out int startX, out int startY);
                if (*(int*)(manager + 8) != startX || *(int*)(manager + 12) != startY)
                    return RejectPublishedMovementSurface(manager, path);
                int x = startX, y = startY;
                bool allowed = result <= WeightedMoatRoutePlanner.MaximumRouteEdges &&
                    *(int*)(manager + PathManagerOutputLengthOffset) == result;
                for (int i = 0; allowed && i < result; i++)
                {
                    int direction = (path[i >> 1] >> ((i & 1) * 4)) & 15;
                    if (direction >= WeightedMoatRoutePlanner.DirectionX.Length) { allowed = false; break; }
                    x += WeightedMoatRoutePlanner.DirectionX[direction];
                    y += WeightedMoatRoutePlanner.DirectionY[direction];
                    if ((uint)x >= MapWidth || (uint)y >= MapWidth ||
                        !IsMovementSurfaceAllowed(surface, GameTileManagerAPI.Instance.GetTileId(x, y))) allowed = false;
                }
                if (allowed && x == *(int*)(manager + 16) && y == *(int*)(manager + 20)) return result;
                return RejectPublishedMovementSurface(manager, path);
            }
            catch (Exception exception)
            {
                try { ReportNativeDetourBarrier("movement-surface-output", exception); } catch { }
                // Only an identified, unit-owned output may be reset. Probes and foreign buffers retain Vanilla.
                return bound ? RejectPublishedMovementSurface(manager, path) : result;
            }
        }

        private static int RejectPublishedMovementSurface(byte* manager, byte* path)
        {
            for (int i = 0; i < NativeUnitPathBufferStride; i++) path[i] = 0;
            *(int*)(manager + PathManagerOutputLengthOffset) = 0;
            return 0;
        }
    }
}
