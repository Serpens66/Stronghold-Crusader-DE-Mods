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
        private const long RestoreInfoLogIntervalMilliseconds = 5000;
        private static readonly object ManualSleepOverridesLock = new object();
        private static readonly Dictionary<int, bool> ManualSleepOverrides = new Dictionary<int, bool>();

        private readonly ManualLogSource log;
        private readonly SomeSettingsViewModel settings;
        private readonly Dictionary<int, OverrideRestoreLogState> overrideRestoreLogStates = new Dictionary<int, OverrideRestoreLogState>();
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

            buttonHook = new Hook(FindButtonToggleZzzModeMethod(), (ButtonToggleZzzModeDelegate)ButtonToggleZzzModeHook);
            buttonTrampoline = buttonHook.GenerateTrampoline<ButtonToggleZzzModeDelegate>();

            guiUpdateHook = new Hook(FindNoesisGuiUpdateChecksInGameMethod(), (NoesisGuiUpdateChecksInGameDelegate)NoesisGuiUpdateChecksInGameHook);
            guiUpdateTrampoline = guiUpdateHook.GenerateTrampoline<NoesisGuiUpdateChecksInGameDelegate>();

            LogInfo("single-building pause hook installed.");
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
            overrideRestoreLogStates.Clear();
            LogInfo("single-building pause hook disposed.");
        }

        public void ClearOverrides(string reason)
        {
            int cleared = ClearManualSleepOverrides();
            if (cleared == 0)
                return;

            overrideRestoreLogStates.Clear();
            LogInfo($"single-building pause cleared all overrides: reason={reason}, cleared={cleared}.");
        }

        internal unsafe static bool TryResolveManualOverrideForSleepingAddress(IntPtr sleepingAddress, out ManualSleepOverrideMatch match)
        {
            match = default;
            if (sleepingAddress == IntPtr.Zero)
                return false;

            lock (ManualSleepOverridesLock)
            {
                if (ManualSleepOverrides.Count == 0)
                    return false;

                GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
                foreach (KeyValuePair<int, bool> entry in ManualSleepOverrides)
                {
                    if (!buildingApi.TryGetBuildingById(entry.Key, out GameBuilding* building) ||
                        building->r_AliveState != AliveState.IsAlive)
                    {
                        continue;
                    }

                    if ((IntPtr)(&building->r_IsSleeping) != sleepingAddress)
                        continue;

                    match = new ManualSleepOverrideMatch
                    {
                        BuildingId = entry.Key,
                        IsSleeping = entry.Value,
                        BuildingType = building->r_BuildingType,
                        Owner = building->r_PlayerIdOwner,
                        CurrentSleeping = building->r_IsSleeping
                    };
                    return true;
                }
            }

            return false;
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
            LogInfo($"single-building pause command: enabled={settings.EnableMod}, ctrl={controlPressed}, selected={BuildBuildingStateSummary(selectedBuildingId)}, overrides={GetManualSleepOverrideCount()}.");

            if (!settings.EnableMod)
            {
                buttonTrampoline(self, parameter);
                return;
            }

            if (!controlPressed)
            {
                if (IsRecentManualToggle(selectedBuildingId))
                {
                    LogInfo($"single-building pause suppressed follow-up vanilla command: selected={BuildBuildingStateSummary(selectedBuildingId)}.");
                    return;
                }

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
            {
                LogInfo($"single-building pause skipped: invalid selected building id {buildingId}.");
                return;
            }

            if (IsDuplicateManualToggle(buildingId))
            {
                LogInfo($"single-building pause duplicate ctrl command suppressed: selected={BuildBuildingStateSummary(buildingId)}.");
                return;
            }

            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            if (!buildingApi.TryGetBuildingById(buildingId, out GameBuilding* building))
            {
                LogInfo($"single-building pause skipped: building id {buildingId} could not be resolved.");
                return;
            }

            bool wasSleeping = building->r_IsSleeping == 1;
            bool isSleeping = !wasSleeping;
            SetManualSleepOverride(buildingId, isSleeping);
            overrideRestoreLogStates.Remove(buildingId);
            buildingApi.SetSleeping(buildingId, isSleeping);
            MarkManualToggle(buildingId);

            LogInfo(
                $"single-building pause toggled: buildingId={buildingId}, type={building->r_BuildingType}, owner={building->r_PlayerIdOwner}, wasSleeping={wasSleeping}, isSleeping={isSleeping}, overrides={GetManualSleepOverrideCount()}, periodicRestore={EnablePeriodicManualSleepOverrideRestore}.");
        }

        private unsafe void ToggleSelectedBuildingTypeFromSelectedState(MainViewModel self, object parameter)
        {
            int selectedBuildingId = TryGetSelectedBuildingId();
            if (selectedBuildingId <= 0)
            {
                LogInfo($"single-building pause vanilla-type toggle fallback: invalid selected building id {selectedBuildingId}.");
                buttonTrampoline(self, parameter);
                return;
            }

            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            if (!buildingApi.TryGetBuildingById(selectedBuildingId, out GameBuilding* selectedBuilding))
            {
                LogInfo($"single-building pause vanilla-type toggle fallback: building id {selectedBuildingId} could not be resolved.");
                buttonTrampoline(self, parameter);
                return;
            }

            bool selectedHasOverride = TryGetManualSleepOverride(selectedBuildingId, out bool overrideSleeping);
            bool selectedWasSleeping = selectedHasOverride ? overrideSleeping : selectedBuilding->r_IsSleeping == 1;
            bool targetSleeping = !selectedWasSleeping;
            byte targetState = (byte)(targetSleeping ? 1 : 0);
            int owner = selectedBuilding->r_PlayerIdOwner;
            eStructs buildingType = selectedBuilding->r_BuildingType;
            int overridesBefore = GetManualSleepOverrideCount();

            ClearManualOverridesForSelectedBuildingType();

            int matched = 0;
            int corrected = 0;
            int overridesAdded = 0;
            Span<GameBuilding> buildings = buildingApi.GetBuildingsAsSpan();
            for (int i = 0; i < buildings.Length; i++)
            {
                ref GameBuilding building = ref buildings[i];
                if (building.r_AliveState != AliveState.IsAlive ||
                    building.r_PlayerIdOwner != owner ||
                    building.r_BuildingType != buildingType)
                {
                    continue;
                }

                matched++;
                int buildingId = i + 1;
                SetManualSleepOverride(buildingId, targetSleeping);
                overrideRestoreLogStates.Remove(buildingId);
                overridesAdded++;

                if (building.r_IsSleeping == targetState)
                    continue;

                building.r_IsSleeping = targetState;
                corrected++;
            }

            UpdateSleepButtonVisibility(self, targetSleeping);
            LogInfo($"single-building pause type toggle from selected state: selectedBuildingId={selectedBuildingId}, type={buildingType}, owner={owner}, selectedWasSleeping={selectedWasSleeping}, targetSleeping={targetSleeping}, selectedOverride={selectedHasOverride}, overridesBefore={overridesBefore}, overridesAfter={GetManualSleepOverrideCount()}, matched={matched}, corrected={corrected}, overridesAdded={overridesAdded}, vanillaSuppressed=True.");
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
                foreach (int buildingId in ManualSleepOverrides.Keys)
                {
                    if (!buildingApi.TryGetBuildingById(buildingId, out GameBuilding* building) ||
                        building->r_PlayerIdOwner == owner && building->r_BuildingType == buildingType)
                    {
                        idsToRemove.Add(buildingId);
                    }
                }

                foreach (int buildingId in idsToRemove)
                    ManualSleepOverrides.Remove(buildingId);
            }

            foreach (int buildingId in idsToRemove)
                overrideRestoreLogStates.Remove(buildingId);

            if (idsToRemove.Count > 0)
                LogInfo($"single-building pause cleared overrides for vanilla toggle: owner={owner}, type={buildingType}, cleared={idsToRemove.Count}, remaining={GetManualSleepOverrideCount()}.");
        }

        private unsafe void ApplyManualSleepOverrides()
        {
            if (GetManualSleepOverrideCount() == 0)
                return;

            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            List<int> idsToRemove = null;
            List<KeyValuePair<int, bool>> overrides;

            lock (ManualSleepOverridesLock)
                overrides = new List<KeyValuePair<int, bool>>(ManualSleepOverrides);

            foreach (KeyValuePair<int, bool> entry in overrides)
            {
                if (!buildingApi.TryGetBuildingById(entry.Key, out GameBuilding* building) ||
                    building->r_AliveState != AliveState.IsAlive)
                {
                    if (idsToRemove == null)
                        idsToRemove = new List<int>();

                    idsToRemove.Add(entry.Key);
                    continue;
                }

                byte desired = (byte)(entry.Value ? 1 : 0);
                if (building->r_IsSleeping == desired)
                    continue;

                building->r_IsSleeping = desired;
                LogOverrideRestored(entry.Key, building->r_BuildingType, entry.Value);
            }

            if (idsToRemove == null)
                return;

            lock (ManualSleepOverridesLock)
            {
                foreach (int buildingId in idsToRemove)
                    ManualSleepOverrides.Remove(buildingId);
            }

            foreach (int buildingId in idsToRemove)
                overrideRestoreLogStates.Remove(buildingId);

            LogInfo($"single-building pause removed stale overrides: removed={idsToRemove.Count}, remaining={GetManualSleepOverrideCount()}.");
        }

        private void LogOverrideRestored(int buildingId, eStructs buildingType, bool desiredSleeping)
        {
            long now = Stopwatch.GetTimestamp();
            if (!overrideRestoreLogStates.TryGetValue(buildingId, out OverrideRestoreLogState state))
            {
                state = new OverrideRestoreLogState();
                overrideRestoreLogStates[buildingId] = state;
            }

            long elapsedMilliseconds = state.LastInfoLogTimestamp == 0
                ? RestoreInfoLogIntervalMilliseconds
                : (now - state.LastInfoLogTimestamp) * 1000 / Stopwatch.Frequency;

            if (elapsedMilliseconds >= RestoreInfoLogIntervalMilliseconds)
            {
                string suppressedSummary = state.SuppressedRepeats > 0
                    ? $", suppressedRepeats={state.SuppressedRepeats}"
                    : string.Empty;

                LogInfo($"single-building pause override restored: buildingId={buildingId}, type={buildingType}, desiredSleeping={desiredSleeping}{suppressedSummary}.");
                state.LastInfoLogTimestamp = now;
                state.SuppressedRepeats = 0;
                return;
            }

            state.SuppressedRepeats++;
            LogDebug($"single-building pause override restored: buildingId={buildingId}, type={buildingType}, desiredSleeping={desiredSleeping}, suppressedUntilInfo=True.");
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

        private unsafe string BuildBuildingStateSummary(int buildingId)
        {
            if (buildingId <= 0)
                return $"id={buildingId}";

            GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
            if (!buildingApi.TryGetBuildingById(buildingId, out GameBuilding* building))
                return $"id={buildingId}, resolved=false";

            bool hasOverride = TryGetManualSleepOverride(buildingId, out bool overrideSleeping);
            return $"id={buildingId}, resolved=true, type={building->r_BuildingType}, owner={building->r_PlayerIdOwner}, sleeping={building->r_IsSleeping}, override={(hasOverride ? overrideSleeping.ToString() : "none")}";
        }

        private static int GetManualSleepOverrideCount()
        {
            lock (ManualSleepOverridesLock)
                return ManualSleepOverrides.Count;
        }

        private static void SetManualSleepOverride(int buildingId, bool isSleeping)
        {
            lock (ManualSleepOverridesLock)
                ManualSleepOverrides[buildingId] = isSleeping;
        }

        private static bool TryGetManualSleepOverride(int buildingId, out bool isSleeping)
        {
            lock (ManualSleepOverridesLock)
                return ManualSleepOverrides.TryGetValue(buildingId, out isSleeping);
        }

        private static int ClearManualSleepOverrides()
        {
            lock (ManualSleepOverridesLock)
            {
                int count = ManualSleepOverrides.Count;
                ManualSleepOverrides.Clear();
                return count;
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

        private void LogDebug(string message)
        {
            log.LogDebug($"[{TimestampNow()}] SomeSettings {message}");
        }

        private void LogInfo(string message)
        {
            log.LogInfo($"[{TimestampNow()}] SomeSettings {message}");
        }

        private void LogError(string message)
        {
            log.LogError($"[{TimestampNow()}] SomeSettings {message}");
        }

        private static string TimestampNow()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
        }

        private sealed class OverrideRestoreLogState
        {
            public long LastInfoLogTimestamp;
            public int SuppressedRepeats;
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
