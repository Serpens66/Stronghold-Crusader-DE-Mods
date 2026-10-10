using BepInEx.Logging;
using HarmonyLib;
using SHCDESE.API;
using SHCDESE.API.LowLevel;
using SHCDESE.Interop;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace BugfixesAndQoL
{
    // Consumer-owned presentation fix. Vanilla and our native marker publishers converge here.
    // Static roots survive startup cleanup; settings/fail-open never remove a published patch.
    internal static unsafe class GatehouseTargetMarkerHeightHook
    {
        private const int DetailedRenderingRva = 0x60AD43C;
        private const int FlattenedRva = 0x60AD444;
        private const int FlattenAllowedRva = 0x60AD44C;
        private static readonly Harmony harmony = new Harmony("BugfixesAndQoL_Serp.GatehouseTargetMarkerHeight");
        private static BugfixesAndQoLViewModel settings;
        private static ManualLogSource log;
        private static IntPtr nativeBase;
        private static int attempted;
        private static int enabled;
        private static int failed;
        private static int postStartupLogged;
        private static int correctionLogged;
        private static int clickCorrectionLogged;

        internal static void Install(CrusaderLibraryLoadContext context,
            ManualLogSource logger, BugfixesAndQoLViewModel viewModel)
        {
            if (Interlocked.Exchange(ref attempted, 1) != 0) return;
            log = logger;
            try
            {
                if (context.ModuleHandle == IntPtr.Zero ||
                    Shared.DebugLogHelper.CurrentNativeSha256 != GatehouseTargetMarkerHeightPolicy.NativeSha256 ||
                    !Shared.DebugLogHelper.IsCurrentNativeLibraryVersion())
                    throw new InvalidOperationException("Unaudited or unavailable native library.");
                Type[] signature = { typeof(int), typeof(int), typeof(int), typeof(int), typeof(int),
                    typeof(float), typeof(int), typeof(int), typeof(bool), typeof(int), typeof(int), typeof(int) };
                MethodInfo target = typeof(GameMap).GetMethod(nameof(GameMap.addUpdatePixie),
                    BindingFlags.Instance | BindingFlags.Public, null, signature, null);
                if (target == null || target.ReturnType != typeof(void))
                    throw new MissingMethodException("GameMap.addUpdatePixie signature differs from the audit.");
                settings = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
                nativeBase = context.ModuleHandle;
                // No competing APIShared/SE site exists. Harmony retains the original call chain.
                harmony.Patch(target, prefix: new HarmonyMethod(typeof(GatehouseTargetMarkerHeightHook), nameof(BeforeAddUpdatePixie)));
                settings.SettingChanged += OnSettingChanged;
                RefreshEnabled();
                Shared.DebugLogHelper.LogInfo(log,
                    "GATEHOUSE_TARGET_MARKER_INSTALLED: permanent GameMap prefix; enabled=" +
                    (Volatile.Read(ref enabled) != 0));
            }
            catch (Exception exception) { FailOpen(exception); }
        }

        private static void OnSettingChanged(string propertyName)
        {
            if (propertyName == nameof(BugfixesAndQoLViewModel.EnableClientFeatures) ||
                propertyName == nameof(BugfixesAndQoLViewModel.EnableGatehouseTargetMarkerHeightFix))
                RefreshEnabled();
        }

        private static void RefreshEnabled() => Volatile.Write(ref enabled,
            Volatile.Read(ref failed) == 0 && settings.EnableClientFeatures &&
            settings.EnableGatehouseTargetMarkerHeightFix ? 1 : 0);

        // The full managed signature is checked before publication. This prefix only changes tile_y;
        // identities, map coordinates, transparency, sorting and the original's execution are preserved.
        private static void BeforeAddUpdatePixie(GameMap __instance, int x, int y, int tile_x, ref int tile_y,
            int file, int image, bool hiMode, int _layerDelay)
        {
            try
            {
                if (file == 107 && Interlocked.Exchange(ref postStartupLogged, 1) == 0)
                    Shared.DebugLogHelper.LogInfo(log,
                        "GATEHOUSE_TARGET_MARKER_POST_STARTUP: first cursor pixie; image=" + image +
                        ", tile_x=" + tile_x + ", tile_y=" + tile_y + ", layerDelay=" + _layerDelay +
                        ", hiMode=" + hiMode + ", enabled=" + (Volatile.Read(ref enabled) != 0));
                if (Volatile.Read(ref enabled) == 0 ||
                    !GatehouseTargetMarkerHeightPolicy.IsMoveTarget(file, image, tile_x, hiMode)) return;
                bool detailed = Marshal.ReadInt32(nativeBase + DetailedRenderingRva) != 0;
                bool flattened = Marshal.ReadInt32(nativeBase + FlattenedRva) != 0;
                bool flattenAllowed = Marshal.ReadInt32(nativeBase + FlattenAllowedRva) != 0;
                if (!GatehouseTargetMarkerHeightPolicy.UsesStructureHeight(detailed, flattened, flattenAllowed)) return;
                GameMapTile tile = __instance?.getMapTile(x, y);
                var tiles = GameTileManagerAPI.Instance;
                var buildings = GameBuildingManagerAPI.Instance;
                if (tile == null || tiles == null || buildings == null ||
                    !tiles.IsTileInsideMapBounds(tile.gameMapX, tile.gameMapY)) return;
                int tileId = tiles.GetTileId(tile.gameMapX, tile.gameMapY);
                if (!tiles.IsValidTileId(tileId)) return;
                uint flags = (uint)tiles.GetTilePropertyFlag(tileId);
                if ((flags & GatehouseTargetMarkerHeightPolicy.WallFlag) == 0 ||
                    (flags & GatehouseTargetMarkerHeightPolicy.ElevatedFlag) != 0) return;
                int buildingId = tiles.GetTileBuildingId(tileId); // Already a 1-based game ID.
                if (buildingId == 0 || !buildings.IsValidId(buildingId) ||
                    !buildings.TryGetBuildingById(buildingId, out GameBuilding* building) || building == null) return;
                int originalHeight = tile_y;
                if (GatehouseTargetMarkerHeightPolicy.TryAdjust(Volatile.Read(ref enabled) != 0,
                    file, image, tile_x, hiMode, detailed, flattened, flattenAllowed,
                    flags, (int)building->r_BuildingType, ref tile_y))
                {
                    bool clickAnimation = tile_x == 10;
                    bool firstCorrection = clickAnimation
                        ? Interlocked.Exchange(ref clickCorrectionLogged, 1) == 0
                        : Interlocked.Exchange(ref correctionLogged, 1) == 0;
                    if (firstCorrection)
                        Shared.DebugLogHelper.LogInfo(log,
                            "GATEHOUSE_TARGET_MARKER_APPLIED: family=" + (clickAnimation ? "ClickAnimation" : "Persistent") +
                            ", buildingId=" + buildingId +
                            ", type=" + (int)building->r_BuildingType + ", tile=" + tile.gameMapX + "/" + tile.gameMapY +
                            ", image=" + image + ", tile_x=" + tile_x + ", layerDelay=" + _layerDelay +
                            ", tile_y=" + originalHeight + " -> " + tile_y);
                }
            }
            catch (Exception exception) { FailOpen(exception); }
        }

        private static void FailOpen(Exception exception)
        {
            Volatile.Write(ref enabled, 0);
            if (Interlocked.Exchange(ref failed, 1) == 0)
                Shared.DebugLogHelper.LogError(log,
                    "GATEHOUSE_TARGET_MARKER_DISABLED: Vanilla rendering retained; " + exception);
        }
    }
}
