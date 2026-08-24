// Temporary diagnostic: observe every relevant display transition without modifying game state.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using System;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;

namespace BugfixesAndQoL
{
    internal sealed class DisplayResolutionDiagnostic : IDisposable
    {
        private delegate void LoadSettingsDelegate();
        private delegate void MonitorDelegate(FatControler self);
        private delegate void OptionsButtonDelegate(HUD_Options self, int parameter);
        private delegate void SaveSettingsDelegate(bool onlyWhenAlreadyExists);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private readonly string sessionId = Guid.NewGuid().ToString("N").Substring(0, 8);
        private readonly FieldInfo settingsDirtyField;
        private readonly FieldInfo firstScreenChangeField;
        private readonly FieldInfo screenModeSetField;
        private readonly FieldInfo lastScreenWidthField;
        private readonly FieldInfo lastScreenHeightField;
        private readonly FieldInfo lastFullscreenModeField;
        private readonly FieldInfo saveWindowSizeChangeField;
        private Hook loadSettingsHook;
        private Hook monitorHook;
        private Hook optionsButtonHook;
        private Hook saveSettingsHook;
        private LoadSettingsDelegate loadSettingsOriginal;
        private MonitorDelegate monitorOriginal;
        private OptionsButtonDelegate optionsButtonOriginal;
        private SaveSettingsDelegate saveSettingsOriginal;
        private DisplaySnapshot lastObserved;
        private DisplaySnapshot lastSaveCall;
        private int manualApplyDepth;
        private int manualApplySequence;
        private int lastRenderFrame = -1;
        private int lastMonitorFrame = -1;
        private long lastMonitorTimestamp;
        private FatControler lastMonitorController;
        private bool observationActive;
        private bool failureLogged;
        private bool disposed;

        public DisplayResolutionDiagnostic(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            settingsDirtyField = FindField(typeof(ConfigSettings), "settingsDirty", true);
            firstScreenChangeField = FindField(typeof(FatControler), "firstScreenChange", false);
            screenModeSetField = FindField(typeof(FatControler), "screenModeSet", false);
            lastScreenWidthField = FindField(typeof(FatControler), "lastScreenWidth", false);
            lastScreenHeightField = FindField(typeof(FatControler), "lastScreenHeight", false);
            lastFullscreenModeField = FindField(typeof(FatControler), "lastFullscreenMode", false);
            saveWindowSizeChangeField = FindField(typeof(FatControler), "saveWindowSizeChange", false);

            try
            {
                loadSettingsHook = new Hook(
                    FindMethod(typeof(ConfigSettings), nameof(ConfigSettings.LoadSettings), true, Type.EmptyTypes),
                    (LoadSettingsDelegate)LoadSettingsHook);
                loadSettingsOriginal = loadSettingsHook.GenerateTrampoline<LoadSettingsDelegate>();
                monitorHook = new Hook(
                    FindMethod(typeof(FatControler), nameof(FatControler.MonitorScreenResolutions), false, Type.EmptyTypes),
                    (MonitorDelegate)MonitorHook);
                monitorOriginal = monitorHook.GenerateTrampoline<MonitorDelegate>();
                optionsButtonHook = new Hook(
                    FindMethod(typeof(HUD_Options), nameof(HUD_Options.ButtonClicked), false, new[] { typeof(int) }),
                    (OptionsButtonDelegate)OptionsButtonHook);
                optionsButtonOriginal = optionsButtonHook.GenerateTrampoline<OptionsButtonDelegate>();
                saveSettingsHook = new Hook(
                    FindMethod(typeof(ConfigSettings), nameof(ConfigSettings.SaveSettings), true, new[] { typeof(bool) }),
                    (SaveSettingsDelegate)SaveSettingsHook);
                saveSettingsOriginal = saveSettingsHook.GenerateTrampoline<SaveSettingsDelegate>();
                settings.SettingChanged += OnSettingChanged;
                Application.focusChanged += ObserveFocusChanged;
                RefreshObservationState("diagnostic initialized");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            StopObservation("diagnostic disposed");
            settings.SettingChanged -= OnSettingChanged;
            Application.focusChanged -= ObserveFocusChanged;
            UndoAndDispose(ref saveSettingsHook);
            UndoAndDispose(ref optionsButtonHook);
            UndoAndDispose(ref monitorHook);
            UndoAndDispose(ref loadSettingsHook);
        }

        private bool IsEnabled =>
            settings.EnableClientFeatures && settings.PreserveDisplayResolution;

        private void OnSettingChanged(string propertyName)
        {
            if (propertyName == nameof(BugfixesAndQoLViewModel.PreserveDisplayResolution) ||
                propertyName == nameof(BugfixesAndQoLViewModel.EnableClientFeatures))
                RefreshObservationState($"setting changed: {propertyName}");
        }

        private void RefreshObservationState(string reason)
        {
            if (IsEnabled)
            {
                if (observationActive)
                    return;

                observationActive = true;
                lastObserved = SafeCapture("activation");
                lastSaveCall = lastObserved;
                lastRenderFrame = -1;
                Application.onBeforeRender -= ObserveBeforeRender;
                Application.onBeforeRender += ObserveBeforeRender;
                LogInfo($"diagnostic-start reason={reason}, focused={Application.isFocused}, initial={lastObserved}");
                return;
            }

            StopObservation(reason);
        }

        private void StopObservation(string reason)
        {
            Application.onBeforeRender -= ObserveBeforeRender;
            if (observationActive)
                LogInfo($"diagnostic-stopped reason={reason}");
            observationActive = false;
            lastRenderFrame = -1;
        }

        private void LoadSettingsHook()
        {
            if (!IsEnabled)
            {
                loadSettingsOriginal();
                return;
            }

            DisplaySnapshot before = SafeCapture("load-before");
            LogInfo($"load-settings-begin focused={Application.isFocused}, state={before}");
            loadSettingsOriginal();
            DisplaySnapshot after = SafeCapture("load-after");
            LogInfo($"load-settings-end focused={Application.isFocused}, changed={!before.Equals(after)}, state={after}");
            lastObserved = after;
            lastSaveCall = after;
        }

        private void MonitorHook(FatControler self)
        {
            if (!IsEnabled)
            {
                monitorOriginal(self);
                return;
            }

            DisplaySnapshot before = SafeCapture("monitor-before");
            LogTransitionIfChanged("monitor-entry", lastObserved, before, self);
            lastMonitorFrame = Time.frameCount;
            lastMonitorTimestamp = Stopwatch.GetTimestamp();
            lastMonitorController = self;
            monitorOriginal(self);
            DisplaySnapshot after = SafeCapture("monitor-after");
            LogTransitionIfChanged("monitor-vanilla", before, after, self);
            lastObserved = after;
        }

        private void OptionsButtonHook(HUD_Options self, int parameter)
        {
            if (parameter != -2 || !IsEnabled)
            {
                optionsButtonOriginal(self, parameter);
                return;
            }

            int sequence = ++manualApplySequence;
            DisplaySnapshot before = SafeCapture("options-apply-before");
            LogInfo($"manual-apply-begin sequence={sequence}, state={before}");
            manualApplyDepth++;
            try
            {
                optionsButtonOriginal(self, parameter);
            }
            finally
            {
                manualApplyDepth--;
                DisplaySnapshot after = SafeCapture("options-apply-after");
                LogInfo($"manual-apply-end sequence={sequence}, changed={!before.Equals(after)}, state={after}");
                lastObserved = after;
            }
        }

        private void SaveSettingsHook(bool onlyWhenAlreadyExists)
        {
            if (!IsEnabled)
            {
                saveSettingsOriginal(onlyWhenAlreadyExists);
                return;
            }

            DisplaySnapshot before = SafeCapture("save-before");
            bool dirtyBefore = ReadBoolean(settingsDirtyField, null);
            bool changedSinceLastSave = !before.Equals(lastSaveCall);
            bool interestingBefore = manualApplyDepth > 0 || dirtyBefore || changedSinceLastSave;
            if (interestingBefore)
            {
                LogInfo(
                    $"save-settings-begin manualApply={manualApplyDepth > 0}, onlyWhenAlreadyExists={onlyWhenAlreadyExists}, " +
                    $"settingsDirty={dirtyBefore}, changedSinceLastSave={changedSinceLastSave}, state={before}");
            }

            saveSettingsOriginal(onlyWhenAlreadyExists);
            DisplaySnapshot after = SafeCapture("save-after");
            bool changedDuringSave = !before.Equals(after);
            if (interestingBefore || changedDuringSave)
            {
                LogInfo(
                    $"save-settings-end manualApply={manualApplyDepth > 0}, settingsDirty={ReadBoolean(settingsDirtyField, null)}, " +
                    $"changedDuringSave={changedDuringSave}, state={after}");
            }

            lastSaveCall = after;
            lastObserved = after;
        }

        private void ObserveBeforeRender()
        {
            if (!IsEnabled)
            {
                StopObservation("feature disabled");
                return;
            }
            if (lastRenderFrame == Time.frameCount)
                return;

            lastRenderFrame = Time.frameCount;
            DisplaySnapshot current = SafeCapture("before-render");
            if (!lastObserved.Equals(current))
            {
                LogInfo(
                    $"display-transition source=outside-observed-hooks, focused={Application.isFocused}, " +
                    $"manualApply={manualApplyDepth > 0}, frame={Time.frameCount}, " +
                    $"lastMonitorFrame={lastMonitorFrame}, msSinceMonitor={ElapsedMilliseconds(lastMonitorTimestamp)}, " +
                    $"lastMonitorController={DescribeController(lastMonitorController)}, from={lastObserved}, to={current}");
                lastObserved = current;
            }
        }

        private void ObserveFocusChanged(bool focused)
        {
            if (!IsEnabled)
                return;

            DisplaySnapshot current = SafeCapture("focus-change");
            LogInfo($"application-focus-changed focused={focused}, frame={Time.frameCount}, state={current}");
            lastObserved = current;
        }

        private void LogTransitionIfChanged(
            string phase,
            DisplaySnapshot before,
            DisplaySnapshot after,
            FatControler controller)
        {
            if (before.Equals(after))
                return;

            LogInfo(
                $"display-transition source={phase}, focused={Application.isFocused}, manualApply={manualApplyDepth > 0}, " +
                $"frame={Time.frameCount}, from={before}, to={after}, controller={DescribeController(controller)}");
        }

        private DisplaySnapshot SafeCapture(string phase)
        {
            try
            {
                return DisplaySnapshot.Capture();
            }
            catch (Exception ex)
            {
                LogFailureOnce(phase, ex);
                return lastObserved;
            }
        }

        private string DescribeController(FatControler controller)
        {
            if (controller == null)
                return "null";

            try
            {
                return
                    $"firstScreenChange={firstScreenChangeField.GetValue(controller)}, " +
                    $"screenModeSet={screenModeSetField.GetValue(controller)}, " +
                    $"lastScreen={lastScreenWidthField.GetValue(controller)}x{lastScreenHeightField.GetValue(controller)}, " +
                    $"lastMode={lastFullscreenModeField.GetValue(controller)}, " +
                    $"saveDueUtc={saveWindowSizeChangeField.GetValue(controller)}";
            }
            catch (Exception ex)
            {
                LogFailureOnce("controller-state", ex);
                return "unavailable";
            }
        }

        private static bool ReadBoolean(FieldInfo field, object instance)
        {
            try
            {
                return field != null && field.GetValue(instance) is bool value && value;
            }
            catch
            {
                return false;
            }
        }

        private void LogFailureOnce(string phase, Exception ex)
        {
            if (failureLogged)
                return;
            failureLogged = true;
            Shared.DebugLogHelper.LogError(
                log,
                $"[DISPLAY_RESOLUTION_DIAGNOSTIC session={sessionId}] capture-failed phase={phase}: {ex}");
        }

        private void LogInfo(string message)
        {
            Shared.DebugLogHelper.LogInfo(
                log,
                $"[DISPLAY_RESOLUTION_DIAGNOSTIC session={sessionId}] {message}");
        }

        private static long ElapsedMilliseconds(long start)
        {
            if (start == 0)
                return -1;
            return (Stopwatch.GetTimestamp() - start) * 1000L / Stopwatch.Frequency;
        }

        private static MethodInfo FindMethod(Type type, string name, bool isStatic, Type[] parameters)
        {
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                (isStatic ? BindingFlags.Static : BindingFlags.Instance);
            MethodInfo method = type.GetMethod(name, flags, null, parameters, null);
            if (method == null || method.ReturnType != typeof(void))
                throw new MissingMethodException(type.FullName, name);
            return method;
        }

        private static FieldInfo FindField(Type type, string name, bool isStatic)
        {
            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                (isStatic ? BindingFlags.Static : BindingFlags.Instance);
            FieldInfo field = type.GetField(name, flags);
            if (field == null)
                throw new MissingFieldException(type.FullName, name);
            return field;
        }

        private static void UndoAndDispose(ref Hook hook)
        {
            hook?.Undo();
            hook?.Dispose();
            hook = null;
        }

        private readonly struct DisplaySnapshot : IEquatable<DisplaySnapshot>
        {
            private DisplaySnapshot(
                int width,
                int height,
                int currentWidth,
                int currentHeight,
                int refresh,
                FullScreenMode mode,
                int configWidth,
                int configHeight,
                int configRefresh,
                int configType,
                int windowWidth,
                int windowHeight)
            {
                Width = width;
                Height = height;
                CurrentWidth = currentWidth;
                CurrentHeight = currentHeight;
                Refresh = refresh;
                Mode = mode;
                ConfigWidth = configWidth;
                ConfigHeight = configHeight;
                ConfigRefresh = configRefresh;
                ConfigType = configType;
                WindowWidth = windowWidth;
                WindowHeight = windowHeight;
            }

            public int Width { get; }
            public int Height { get; }
            public int CurrentWidth { get; }
            public int CurrentHeight { get; }
            public int Refresh { get; }
            public FullScreenMode Mode { get; }
            public int ConfigWidth { get; }
            public int ConfigHeight { get; }
            public int ConfigRefresh { get; }
            public int ConfigType { get; }
            public int WindowWidth { get; }
            public int WindowHeight { get; }

            public static DisplaySnapshot Capture()
            {
                Resolution current = Screen.currentResolution;
                return new DisplaySnapshot(
                    Screen.width,
                    Screen.height,
                    current.width,
                    current.height,
                    current.refreshRate,
                    Screen.fullScreenMode,
                    ConfigSettings.Settings_LastFullscreenWidth,
                    ConfigSettings.Settings_LastFullscreenHeight,
                    ConfigSettings.Settings_LastFullscreenRefresh,
                    ConfigSettings.Settings_LastFullscreenType,
                    ConfigSettings.Settings_LastWindowWidth,
                    ConfigSettings.Settings_LastWindowHeight);
            }

            public bool Equals(DisplaySnapshot other)
            {
                return Width == other.Width && Height == other.Height &&
                    CurrentWidth == other.CurrentWidth && CurrentHeight == other.CurrentHeight &&
                    Refresh == other.Refresh && Mode == other.Mode &&
                    ConfigWidth == other.ConfigWidth && ConfigHeight == other.ConfigHeight &&
                    ConfigRefresh == other.ConfigRefresh && ConfigType == other.ConfigType &&
                    WindowWidth == other.WindowWidth && WindowHeight == other.WindowHeight;
            }

            public override bool Equals(object obj) => obj is DisplaySnapshot other && Equals(other);
            public override int GetHashCode() => Width ^ Height ^ ConfigWidth ^ ConfigHeight ^ (int)Mode;

            public override string ToString()
            {
                return
                    $"screen={Width}x{Height}@{Refresh}Hz/{Mode}, " +
                    $"currentResolution={CurrentWidth}x{CurrentHeight}@{Refresh}Hz, " +
                    $"configFullscreen={ConfigWidth}x{ConfigHeight}@{ConfigRefresh}Hz/type{ConfigType}, " +
                    $"configWindow={WindowWidth}x{WindowHeight}";
            }
        }
    }
}
