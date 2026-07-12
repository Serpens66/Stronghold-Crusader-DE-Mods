using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using Noesis;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace SomeSettings
{
    internal sealed class SingleBuildingPauseHook : IDisposable
    {
        private delegate void ButtonToggleZzzModeDelegate(MainViewModel self, object parameter);
        private delegate void NoesisGuiUpdateChecksInGameDelegate(FatControler self);

        private static readonly bool EnablePeriodicManualSleepOverrideRestore = false;
        private const long DuplicateToggleSuppressMilliseconds = 750;
        private static readonly object ManualSleepOverridesLock = new object();
        private static readonly Dictionary<int, ManualSleepOverride> ManualSleepOverrides = new Dictionary<int, ManualSleepOverride>();
        private static readonly Dictionary<IntPtr, int> ManualSleepOverrideIdsBySleepingAddress = new Dictionary<IntPtr, int>();

        private readonly ManualLogSource log;
        private readonly SomeSettingsViewModel settings;
        private readonly Hook buttonHook;
        private readonly Hook guiUpdateHook;
        private readonly ButtonToggleZzzModeDelegate buttonTrampoline;
        private readonly NoesisGuiUpdateChecksInGameDelegate guiUpdateTrampoline;
        private int lastManualToggleBuildingId;
        private long lastManualToggleTimestamp;
        private bool disposed;

        public SingleBuildingPauseHook(ManualLogSource log, SomeSettingsViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));

            MethodInfo buttonMethod = FindButtonToggleZzzModeMethod();
            MethodInfo guiUpdateMethod = FindNoesisGuiUpdateChecksInGameMethod();
            Hook installedButtonHook = null;
            Hook installedGuiUpdateHook = null;
            try
            {
                installedButtonHook = new Hook(buttonMethod, (ButtonToggleZzzModeDelegate)ButtonToggleZzzModeHook);
                ButtonToggleZzzModeDelegate installedButtonTrampoline = installedButtonHook.GenerateTrampoline<ButtonToggleZzzModeDelegate>();

                installedGuiUpdateHook = new Hook(guiUpdateMethod, (NoesisGuiUpdateChecksInGameDelegate)NoesisGuiUpdateChecksInGameHook);
                NoesisGuiUpdateChecksInGameDelegate installedGuiUpdateTrampoline = installedGuiUpdateHook.GenerateTrampoline<NoesisGuiUpdateChecksInGameDelegate>();

                buttonHook = installedButtonHook;
                buttonTrampoline = installedButtonTrampoline;
                guiUpdateHook = installedGuiUpdateHook;
                guiUpdateTrampoline = installedGuiUpdateTrampoline;
            }
            catch
            {
                installedGuiUpdateHook?.Dispose();
                installedButtonHook?.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            buttonHook?.Undo();
            buttonHook?.Dispose();
            guiUpdateHook?.Undo();
            guiUpdateHook?.Dispose();
            ClearManualSleepOverrides();
        }

        public void ClearOverrides(string reason)
        {
            ClearManualSleepOverrides();
        }

        internal unsafe static bool TryResolveManualOverrideForSleepingAddress(IntPtr sleepingAddress, out ManualSleepOverrideMatch match)
        {
            match = default;
            if (sleepingAddress == IntPtr.Zero)
                return false;

            ManualSleepOverride entry;
            lock (ManualSleepOverridesLock)
            {
                if (!ManualSleepOverrideIdsBySleepingAddress.TryGetValue(sleepingAddress, out int buildingId) ||
                    !ManualSleepOverrides.TryGetValue(buildingId, out entry))
                {
                    ManualSleepOverrideIdsBySleepingAddress.Remove(sleepingAddress);
                    return false;
                }

                if (entry.SleepingAddress != sleepingAddress)
                {
                    ManualSleepOverrideIdsBySleepingAddress.Remove(sleepingAddress);
                    return false;
                }
            }

            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            if (!buildingApi.TryGetBuildingById(entry.BuildingId, out GameBuilding* building) ||
                building->r_AliveState != AliveState.IsAlive ||
                (IntPtr)(&building->r_IsSleeping) != sleepingAddress)
            {
                RemoveManualSleepOverride(entry.BuildingId);
                return false;
            }

            match = new ManualSleepOverrideMatch
            {
                BuildingId = entry.BuildingId,
                IsSleeping = entry.IsSleeping,
                BuildingType = building->r_BuildingType,
                Owner = building->r_PlayerIdOwner,
                CurrentSleeping = building->r_IsSleeping
            };
            return true;
        }

        private static MethodInfo FindButtonToggleZzzModeMethod()
        {
            MethodInfo method = typeof(MainViewModel).GetMethod(
                "ButtonToggleZZZMode",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(object) },
                null);

            if (method == null)
                throw new MissingMethodException(typeof(MainViewModel).FullName, "ButtonToggleZZZMode");

            return method;
        }

        private static MethodInfo FindNoesisGuiUpdateChecksInGameMethod()
        {
            MethodInfo method = typeof(FatControler).GetMethod(
                "NoesisGUIUpdateChecksInGame",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                Type.EmptyTypes,
                null);

            if (method == null)
                throw new MissingMethodException(typeof(FatControler).FullName, "NoesisGUIUpdateChecksInGame");

            return method;
        }

        private void ButtonToggleZzzModeHook(MainViewModel self, object parameter)
        {
            int selectedBuildingId = TryGetSelectedBuildingId();
            bool controlPressed = IsControlPressed();

            if (!settings.EnableMod)
            {
                buttonTrampoline(self, parameter);
                return;
            }

            if (!controlPressed)
            {
                if (IsRecentManualToggle(selectedBuildingId))
                    return;

                ToggleSelectedBuildingTypeFromSelectedState(self, parameter);
                return;
            }

            try
            {
                ToggleSelectedBuildingOnly(self);
            }
            catch (Exception ex)
            {
                LogError($"single-building pause failed: {ex}");
            }
        }

        private void NoesisGuiUpdateChecksInGameHook(FatControler self)
        {
            guiUpdateTrampoline(self);

            if (!settings.EnableMod)
                return;

            try
            {
                if (EnablePeriodicManualSleepOverrideRestore)
                    ApplyManualSleepOverrides();

                RefreshSelectedBuildingSleepButton();
            }
            catch (Exception ex)
            {
                LogError($"single-building pause update failed: {ex}");
            }
        }

        private unsafe void ToggleSelectedBuildingOnly(MainViewModel self)
        {
            int buildingId = TryGetSelectedBuildingId();
            if (buildingId <= 0)
                return;

            if (IsDuplicateManualToggle(buildingId))
                return;

            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            if (!buildingApi.TryGetBuildingById(buildingId, out GameBuilding* building))
                return;

            bool hasOverride = TryGetManualSleepOverride(buildingId, out bool overrideSleeping);
            bool wasSleeping = hasOverride ? overrideSleeping : building->r_IsSleeping == 1;
            bool targetSleeping = !wasSleeping;
            if (!SetManualSleepOverride(buildingId, targetSleeping))
                return;

            // Do not write r_IsSleeping directly. The native sleep-state sync must
            // observe the state change so it can run the game's worker reset and
            // reassignment bookkeeping for this building.
            UpdateSleepButtonVisibility(self, targetSleeping);
            MarkManualToggle(buildingId);
        }

        private unsafe void ToggleSelectedBuildingTypeFromSelectedState(MainViewModel self, object parameter)
        {
            int selectedBuildingId = TryGetSelectedBuildingId();
            if (selectedBuildingId <= 0)
            {
                buttonTrampoline(self, parameter);
                return;
            }

            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            if (!buildingApi.TryGetBuildingById(selectedBuildingId, out GameBuilding* selectedBuilding))
            {
                buttonTrampoline(self, parameter);
                return;
            }

            bool selectedHasOverride = TryGetManualSleepOverride(selectedBuildingId, out bool overrideSleeping);
            bool selectedWasSleeping = selectedHasOverride ? overrideSleeping : selectedBuilding->r_IsSleeping == 1;
            bool targetSleeping = !selectedWasSleeping;
            bool buildingTypeWasSleeping = GameData.Instance.lastGameState.building_type_sleeping != 0;

            ClearManualOverridesForSelectedBuildingType();

            // If the selected building had an individual override opposite to the
            // type-wide state, clearing that override already produces the desired
            // result. Otherwise let the vanilla GameAction toggle the whole type so
            // every affected building runs the native worker bookkeeping.
            if (buildingTypeWasSleeping != targetSleeping)
                buttonTrampoline(self, parameter);

            UpdateSleepButtonVisibility(self, targetSleeping);
        }

        private static bool IsControlPressed()
        {
            bool editorCtrl = EditorDirector.instance != null && EditorDirector.instance.ctrlPressed;
            bool keyManagerCtrl = KeyManager.instance != null &&
                (KeyManager.instance.IsKeyHeldDown(KeyCode.LeftControl, true) ||
                 KeyManager.instance.IsKeyHeldDown(KeyCode.RightControl, true));
            return editorCtrl || keyManagerCtrl;
        }

        private static int TryGetSelectedBuildingId()
        {
            if (GameData.Instance == null || GameData.Instance.lastGameState == null)
                return 0;

            return GameData.Instance.lastGameState.in_structure;
        }

        private unsafe void ClearManualOverridesForSelectedBuildingType()
        {
            if (GetManualSleepOverrideCount() == 0 ||
                GameData.Instance == null ||
                GameData.Instance.lastGameState == null)
                return;

            int selectedBuildingId = GameData.Instance.lastGameState.in_structure;
            if (selectedBuildingId <= 0)
                return;

            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            if (!buildingApi.TryGetBuildingById(selectedBuildingId, out GameBuilding* selectedBuilding))
                return;

            int owner = selectedBuilding->r_PlayerIdOwner;
            eStructs buildingType = selectedBuilding->r_BuildingType;
            List<int> idsToRemove = new List<int>();

            lock (ManualSleepOverridesLock)
            {
                foreach (ManualSleepOverride entry in ManualSleepOverrides.Values)
                {
                    if (entry.Owner == owner && entry.BuildingType == buildingType)
                        idsToRemove.Add(entry.BuildingId);
                }

                foreach (int buildingId in idsToRemove)
                    RemoveManualSleepOverrideUnsafe(buildingId);
            }

        }

        private unsafe void ApplyManualSleepOverrides()
        {
            if (GetManualSleepOverrideCount() == 0)
                return;

            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            List<int> idsToRemove = null;
            List<ManualSleepOverride> overrides;

            lock (ManualSleepOverridesLock)
                overrides = new List<ManualSleepOverride>(ManualSleepOverrides.Values);

            foreach (ManualSleepOverride entry in overrides)
            {
                if (!buildingApi.TryGetBuildingById(entry.BuildingId, out GameBuilding* building) ||
                    building->r_AliveState != AliveState.IsAlive)
                {
                    if (idsToRemove == null)
                        idsToRemove = new List<int>();

                    idsToRemove.Add(entry.BuildingId);
                    continue;
                }

                byte desired = (byte)(entry.IsSleeping ? 1 : 0);
                if (building->r_IsSleeping == desired)
                    continue;

                building->r_IsSleeping = desired;
            }

            if (idsToRemove == null)
                return;

            lock (ManualSleepOverridesLock)
            {
                foreach (int buildingId in idsToRemove)
                    RemoveManualSleepOverrideUnsafe(buildingId);
            }

        }

        private void RefreshSelectedBuildingSleepButton()
        {
            if (GameData.Instance == null || GameData.Instance.lastGameState == null)
                return;

            int selectedBuildingId = GameData.Instance.lastGameState.in_structure;
            if (selectedBuildingId <= 0)
                return;

            if (TryGetManualSleepOverride(selectedBuildingId, out bool isSleeping))
                UpdateSleepButtonVisibility(MainViewModel.Instance, isSleeping);
        }

        private bool IsDuplicateManualToggle(int buildingId)
        {
            return IsRecentManualToggle(buildingId);
        }

        private bool IsRecentManualToggle(int buildingId)
        {
            if (buildingId <= 0)
                return false;

            long now = Stopwatch.GetTimestamp();
            long elapsedMilliseconds = (now - lastManualToggleTimestamp) * 1000 / Stopwatch.Frequency;
            return buildingId == lastManualToggleBuildingId &&
                elapsedMilliseconds >= 0 &&
                elapsedMilliseconds < DuplicateToggleSuppressMilliseconds;
        }

        private void MarkManualToggle(int buildingId)
        {
            lastManualToggleBuildingId = buildingId;
            lastManualToggleTimestamp = Stopwatch.GetTimestamp();
        }

        private static int GetManualSleepOverrideCount()
        {
            lock (ManualSleepOverridesLock)
                return ManualSleepOverrides.Count;
        }

        private unsafe static bool SetManualSleepOverride(int buildingId, bool isSleeping)
        {
            if (buildingId <= 0)
                return false;

            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            if (!buildingApi.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                building->r_AliveState != AliveState.IsAlive)
            {
                return false;
            }

            ManualSleepOverride entry = new ManualSleepOverride
            {
                BuildingId = buildingId,
                IsSleeping = isSleeping,
                SleepingAddress = (IntPtr)(&building->r_IsSleeping),
                BuildingType = building->r_BuildingType,
                Owner = building->r_PlayerIdOwner
            };

            lock (ManualSleepOverridesLock)
            {
                if (ManualSleepOverrides.TryGetValue(buildingId, out ManualSleepOverride oldEntry) &&
                    ManualSleepOverrideIdsBySleepingAddress.TryGetValue(oldEntry.SleepingAddress, out int oldBuildingId) &&
                    oldBuildingId == buildingId)
                {
                    ManualSleepOverrideIdsBySleepingAddress.Remove(oldEntry.SleepingAddress);
                }

                ManualSleepOverrides[buildingId] = entry;
                ManualSleepOverrideIdsBySleepingAddress[entry.SleepingAddress] = buildingId;
            }

            return true;
        }

        private static bool TryGetManualSleepOverride(int buildingId, out bool isSleeping)
        {
            lock (ManualSleepOverridesLock)
            {
                if (ManualSleepOverrides.TryGetValue(buildingId, out ManualSleepOverride entry))
                {
                    isSleeping = entry.IsSleeping;
                    return true;
                }
            }

            isSleeping = false;
            return false;
        }

        private static int ClearManualSleepOverrides()
        {
            lock (ManualSleepOverridesLock)
            {
                int count = ManualSleepOverrides.Count;
                ManualSleepOverrides.Clear();
                ManualSleepOverrideIdsBySleepingAddress.Clear();
                return count;
            }
        }

        private static void RemoveManualSleepOverride(int buildingId)
        {
            lock (ManualSleepOverridesLock)
                RemoveManualSleepOverrideUnsafe(buildingId);
        }

        private static void RemoveManualSleepOverrideUnsafe(int buildingId)
        {
            if (!ManualSleepOverrides.TryGetValue(buildingId, out ManualSleepOverride entry))
                return;

            ManualSleepOverrides.Remove(buildingId);
            if (ManualSleepOverrideIdsBySleepingAddress.TryGetValue(entry.SleepingAddress, out int indexedBuildingId) &&
                indexedBuildingId == buildingId)
            {
                ManualSleepOverrideIdsBySleepingAddress.Remove(entry.SleepingAddress);
            }
        }

        private void UpdateSleepButtonVisibility(MainViewModel self, bool isSleeping)
        {
            try
            {
                if (self == null || self.HUDBuildingPanel == null)
                    return;

                if (self.HUDBuildingPanel.RefBuildingZZZButtonOff != null)
                    self.HUDBuildingPanel.RefBuildingZZZButtonOff.Visibility = isSleeping ? (Visibility)2 : (Visibility)1;

                if (self.HUDBuildingPanel.RefBuildingZZZButtonOn != null)
                    self.HUDBuildingPanel.RefBuildingZZZButtonOn.Visibility = isSleeping ? (Visibility)1 : (Visibility)2;
            }
            catch (Exception ex)
            {
                LogError($"single-building pause UI refresh failed: {ex}");
            }
        }

        private void LogError(string message)
        {
            log.LogError($"[{TimestampNow()}] SomeSettings {message}");
        }

        private static string TimestampNow()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        }

        private struct ManualSleepOverride
        {
            public int BuildingId;
            public bool IsSleeping;
            public IntPtr SleepingAddress;
            public eStructs BuildingType;
            public int Owner;
        }

        internal struct ManualSleepOverrideMatch
        {
            public int BuildingId;
            public bool IsSleeping;
            public eStructs BuildingType;
            public int Owner;
            public byte CurrentSleeping;
        }
    }
}
