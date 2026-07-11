using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.Detours;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace SomeSettings
{
    internal sealed class EnemyProximityBulldozeCursorHook : IDisposable
    {
        private delegate void SetCursorDelegate(Director self, int cursorType, bool force);

        private const int DeleteAction = 6;
        private const int AlternateDeleteAction = 3;
        private const int AlternateDeleteSubAction = 349;
        private const int DeleteCursor = 2;
        private const int DeleteNotAllowedCursor = 3;

        // c_game_repairs_allowed reads this ChoreManager flag to select one of its two range constants.
        // The offset belongs to the current CrusaderDE.dll layout; backwards compatibility is not required.
        private const int ProximityModeFlagOffset = 0x870;
        private const int DefaultProximityRange = 30;
        private const int DefaultReducedProximityRange = 15;

        private static readonly FieldInfo WaitCursorSetField = FindDirectorField("waitCursorSet");

        private readonly ManualLogSource log;
        private readonly SomeSettingsViewModel settings;
        private readonly Hook hook;
        private readonly SetCursorDelegate trampoline;
        private bool queryFailureLogged;
        private bool disposed;

        public EnemyProximityBulldozeCursorHook(ManualLogSource log, SomeSettingsViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            hook = new Hook(FindSetCursorMethod(), (SetCursorDelegate)SetCursorHook);
            trampoline = hook.GenerateTrampoline<SetCursorDelegate>();
            log.LogDebug("SomeSettings enemy-proximity bulldoze cursor hook installed.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            hook.Undo();
            hook.Dispose();
            log.LogDebug("SomeSettings enemy-proximity bulldoze cursor hook disposed.");
        }

        private static MethodInfo FindSetCursorMethod()
        {
            MethodInfo method = typeof(Director).GetMethod(
                "setCursor",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(int), typeof(bool) },
                null);

            if (method == null)
                throw new MissingMethodException(typeof(Director).FullName, "setCursor");

            return method;
        }

        private static FieldInfo FindDirectorField(string fieldName)
        {
            FieldInfo field = typeof(Director).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field == null)
                throw new MissingFieldException(typeof(Director).FullName, fieldName);

            return field;
        }

        private void SetCursorHook(Director self, int cursorType, bool force)
        {
            if (cursorType == DeleteCursor && ShouldOverrideDeleteCursor(self))
            {
                try
                {
                    if (IsBulldozeBlockedByEnemy())
                        cursorType = DeleteNotAllowedCursor;
                }
                catch (Exception ex)
                {
                    if (!queryFailureLogged)
                    {
                        queryFailureLogged = true;
                        log.LogError($"SomeSettings enemy-proximity bulldoze cursor query failed: {ex}");
                    }
                }
            }

            trampoline(self, cursorType, force);
        }

        private static bool IsBulldozeBlockedByEnemy()
        {
            GamePlayerManagerAPI playerApi = GamePlayerManagerAPI.Instance;
            int playerId = playerApi.GetLocalPlayerId();
            if (!playerApi.IsPlayerIdValid(playerId))
                return false;

            ResolveProximityCheckPosition(playerApi, out int tileX, out int tileY);
            int proximity = GetCurrentProximityRange();

            // The first parameter is unused by this native function in the current game build.
            return BulkBuildingDetours.c_game_allow_repair_for_building_proximity_hook_impl(
                IntPtr.Zero,
                playerId,
                tileX,
                tileY,
                proximity,
                0) != 0;
        }

        private static void ResolveProximityCheckPosition(
            GamePlayerManagerAPI playerApi,
            out int tileX,
            out int tileY)
        {
            int hoveredBuildingId = playerApi.GetHoveredBuildingId();
            if (hoveredBuildingId > 0)
            {
                // Do not use GetHoveredBuildingTileId here. Its cursor-manager value does not
                // represent the map position used by the native enemy-proximity check and caused
                // false negatives over buildings. c_game_repairs_allowed reads GameBuilding at
                // offsets 0x14A/0x14C, exposed as r_TilePositionXBegin/YBegin, so mirror that logic.
                UnmanagedVector2<ushort> buildingPosition =
                    GameBuildingManagerAPI.Instance.GetBeginPosition(hoveredBuildingId);
                GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
                if (tileApi.IsTileInsideMapBounds(buildingPosition.X, buildingPosition.Y))
                {
                    tileX = buildingPosition.X;
                    tileY = buildingPosition.Y;
                    return;
                }
            }

            UnmanagedVector2<uint> mousePosition = playerApi.GetMousePosition();
            tileX = checked((int)mousePosition.X);
            tileY = checked((int)mousePosition.Y);
        }

        private bool ShouldOverrideDeleteCursor(Director self)
        {
            if (!settings.EnableMod || self == null || GetWaitCursorSet(self))
                return false;

            if (FatControler.currentScene != Enums.SceneIDS.ActualMainGame)
                return false;

            if (MainControls.instance == null || !IsDeleteAction(MainControls.instance))
                return false;

            if (MainViewModel.Instance == null || MainViewModel.Instance.IsMapEditorMode)
                return false;

            if (GameData.Instance == null || GameData.Instance.lastGameState == null)
                return false;

            return true;
        }

        private static bool GetWaitCursorSet(Director self)
        {
            return (bool)WaitCursorSetField.GetValue(self);
        }

        private static bool IsDeleteAction(MainControls controls)
        {
            return controls.CurrentAction == DeleteAction ||
                (controls.CurrentAction == AlternateDeleteAction &&
                 controls.CurrentSubAction == AlternateDeleteSubAction);
        }

        private static int GetCurrentProximityRange()
        {
            GameGlobalsManager globals = GameGlobalsManager.Instance;
            bool useReducedRange = IsReducedProximityMode(globals);

            if (useReducedRange)
            {
                return globals.BuildingRepairProximityCheckExRange == null
                    ? DefaultReducedProximityRange
                    : globals.BuildingRepairProximityCheckExRange.GetValue();
            }

            return globals.BuildingRepairProximityCheckRange == null
                ? DefaultProximityRange
                : globals.BuildingRepairProximityCheckRange.GetValue();
        }

        private static bool IsReducedProximityMode(GameGlobalsManager globals)
        {
            if (globals.ChoreManagerVA == 0)
                return false;

            long flagAddress = checked((long)globals.ChoreManagerVA + ProximityModeFlagOffset);
            return Marshal.ReadInt32(new IntPtr(flagAddress)) != 0;
        }
    }
}
