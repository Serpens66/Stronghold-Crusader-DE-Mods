// Feature: Protect the display resolution loaded from settings.cfg during startup only.
using BepInEx.Logging;
using CrusaderDE;
using MonoMod.RuntimeDetour;
using System;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;

namespace BugfixesAndQoL
{
    internal sealed class DisplayResolutionPersistenceHook : IDisposable
    {
        private const long RetryMilliseconds = 500;
        private const long MinimumGuardMilliseconds = 5000;
        private const long StableTargetMilliseconds = 2000;
        private const long MaximumGuardMilliseconds = 30000;

        private delegate void LoadSettingsDelegate();
        private delegate void MonitorDelegate(FatControler self);

        private readonly ManualLogSource log;
        private readonly BugfixesAndQoLViewModel settings;
        private Hook loadHook;
        private Hook monitorHook;
        private LoadSettingsDelegate loadOriginal;
        private MonitorDelegate monitorOriginal;
        private ResolutionConfiguration configured;
        private long guardStarted;
        private long targetStableSince;
        private long lastRequest;
        private string lastRequestedTarget;
        private bool failureLogged;
        private bool detachScheduled;
        private bool disposed;

        public DisplayResolutionPersistenceHook(ManualLogSource log, BugfixesAndQoLViewModel settings)
        {
            this.log = log ?? throw new ArgumentNullException(nameof(log));
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            try
            {
                loadHook = new Hook(FindMethod(typeof(ConfigSettings), nameof(ConfigSettings.LoadSettings), true, Type.EmptyTypes), (LoadSettingsDelegate)LoadSettingsHook);
                loadOriginal = loadHook.GenerateTrampoline<LoadSettingsDelegate>();
                monitorHook = new Hook(FindMethod(typeof(FatControler), nameof(FatControler.MonitorScreenResolutions), false, Type.EmptyTypes), (MonitorDelegate)MonitorHook);
                monitorOriginal = monitorHook.GenerateTrampoline<MonitorDelegate>();
            }
            catch
            {
                Dispose();
                throw;
            }

            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL startup display-resolution guard installed.");
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            Application.onBeforeRender -= DetachAfterCallback;
            UndoAndDispose(ref monitorHook);
            UndoAndDispose(ref loadHook);
            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL startup display-resolution guard disposed.");
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

        private static void UndoAndDispose(ref Hook hook)
        {
            hook?.Undo();
            hook?.Dispose();
            hook = null;
        }

        private void LoadSettingsHook()
        {
            loadOriginal();
            try
            {
                configured = new ResolutionConfiguration(
                    ConfigSettings.Settings_LastWindowWidth,
                    ConfigSettings.Settings_LastWindowHeight,
                    ConfigSettings.Settings_LastFullscreenWidth,
                    ConfigSettings.Settings_LastFullscreenHeight,
                    ConfigSettings.Settings_LastFullscreenRefresh,
                    ConfigSettings.Settings_LastFullscreenType);
                Shared.DebugLogHelper.LogInfo(log,
                    "Bugfixes and QoL captured startup display settings from settings.cfg: " +
                    $"window={configured.WindowWidth}x{configured.WindowHeight}, " +
                    $"fullscreen={configured.FullscreenWidth}x{configured.FullscreenHeight}@{configured.FullscreenRefreshRate}Hz, " +
                    $"fullscreenType={configured.FullscreenType}, actual={DescribeActual()}.");
            }
            catch (Exception ex)
            {
                Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL could not capture startup display settings: {ex}");
                ScheduleDetach("settings capture failed");
            }
        }

        private void MonitorHook(FatControler self)
        {
            if (!detachScheduled)
            {
                try
                {
                    if (guardStarted == 0)
                    {
                        guardStarted = Stopwatch.GetTimestamp();
                        Shared.DebugLogHelper.LogDebug(log, () =>
                            $"Bugfixes and QoL startup display guard confirmed: enabled={IsEnabled}, captured={configured.IsCaptured}, actual={DescribeActual()}.");
                    }
                    if (IsEnabled && configured.IsCaptured)
                        RestoreConfiguredValues();
                }
                catch (Exception ex)
                {
                    LogFailureOnce("before Vanilla monitoring", ex);
                }
            }

            // Vanilla must run exactly once even if our startup correction fails.
            monitorOriginal(self);
            if (detachScheduled)
                return;

            try
            {
                if (!IsEnabled)
                {
                    ScheduleDetach("feature disabled");
                    return;
                }
                if (!configured.IsCaptured)
                {
                    ScheduleDetach("settings.cfg contained no usable display target");
                    return;
                }

                // Preserve the loaded fields only across Vanilla's asynchronous startup race.
                RestoreConfiguredValues();
                EvaluateStartupTarget();
            }
            catch (Exception ex)
            {
                LogFailureOnce("after Vanilla monitoring", ex);
                ScheduleDetach("startup reconciliation failed");
            }
        }

        private void EvaluateStartupTarget()
        {
            long now = Stopwatch.GetTimestamp();
            long elapsed = ElapsedMilliseconds(guardStarted, now);
            if (elapsed >= MaximumGuardMilliseconds)
            {
                Shared.DebugLogHelper.LogWarning(log,
                    $"Bugfixes and QoL ended the startup display guard at its {MaximumGuardMilliseconds} ms safety limit: actual={DescribeActual()}.");
                ScheduleDetach("safety limit reached");
                return;
            }

            if (!TryGetTarget(out int width, out int height, out int refresh, out FullScreenMode mode))
            {
                ScheduleDetach("current display mode has no applicable settings.cfg target");
                return;
            }

            string target = $"{width}x{height}@{refresh}Hz/{mode}";
            if (mode != FullScreenMode.Windowed && !IsSupported(width, height))
            {
                Shared.DebugLogHelper.LogWarning(log,
                    $"Bugfixes and QoL ended the startup display guard because this PC does not report the settings.cfg target: target={target}.");
                ScheduleDetach("fullscreen target unsupported");
                return;
            }

            if (Matches(width, height, refresh, mode))
            {
                if (targetStableSince == 0)
                    targetStableSince = now;
                if (MainViewModel.viewModelLoaded && elapsed >= MinimumGuardMilliseconds &&
                    ElapsedMilliseconds(targetStableSince, now) >= StableTargetMilliseconds)
                    ScheduleDetach("frontend loaded and settings.cfg target stable");
                return;
            }

            targetStableSince = 0;
            if (lastRequest != 0 && ElapsedMilliseconds(lastRequest, now) < RetryMilliseconds)
                return;

            string actual = DescribeActual();
            lastRequest = now;
            Screen.SetResolution(width, height, mode, refresh);
            if (!string.Equals(lastRequestedTarget, target, StringComparison.Ordinal))
            {
                lastRequestedTarget = target;
                Shared.DebugLogHelper.LogWarning(log,
                    $"Bugfixes and QoL reapplied the startup display target loaded from settings.cfg: target={target}, observedBeforeRequest={actual}.");
            }
        }

        private bool TryGetTarget(out int width, out int height, out int refresh, out FullScreenMode mode)
        {
            if (Screen.fullScreenMode == FullScreenMode.Windowed && configured.HasWindowResolution)
            {
                width = configured.WindowWidth;
                height = configured.WindowHeight;
                refresh = 0;
                mode = FullScreenMode.Windowed;
                return true;
            }
            if ((Screen.fullScreenMode == FullScreenMode.ExclusiveFullScreen || Screen.fullScreenMode == FullScreenMode.FullScreenWindow) && configured.HasFullscreenResolution)
            {
                width = configured.FullscreenWidth;
                height = configured.FullscreenHeight;
                refresh = configured.FullscreenRefreshRate > 0 ? configured.FullscreenRefreshRate : 0;
                mode = configured.FullscreenType == 0 ? FullScreenMode.ExclusiveFullScreen : FullScreenMode.FullScreenWindow;
                return true;
            }
            width = height = refresh = 0;
            mode = Screen.fullScreenMode;
            return false;
        }

        private static bool Matches(int width, int height, int refresh, FullScreenMode mode)
        {
            bool refreshMatches = mode != FullScreenMode.ExclusiveFullScreen || refresh <= 0 ||
                Math.Abs(Screen.currentResolution.refreshRate - refresh) < 2;
            return Screen.width == width && Screen.height == height && Screen.fullScreenMode == mode && refreshMatches;
        }

        private void RestoreConfiguredValues()
        {
            if (configured.HasWindowResolution)
            {
                ConfigSettings.Settings_LastWindowWidth = configured.WindowWidth;
                ConfigSettings.Settings_LastWindowHeight = configured.WindowHeight;
            }
            if (configured.HasFullscreenResolution)
            {
                ConfigSettings.Settings_LastFullscreenWidth = configured.FullscreenWidth;
                ConfigSettings.Settings_LastFullscreenHeight = configured.FullscreenHeight;
                ConfigSettings.Settings_LastFullscreenRefresh = configured.FullscreenRefreshRate;
                ConfigSettings.Settings_LastFullscreenType = configured.FullscreenType;
            }
        }

        private void ScheduleDetach(string reason)
        {
            if (detachScheduled)
                return;
            detachScheduled = true;
            Shared.DebugLogHelper.LogInfo(log,
                $"Bugfixes and QoL startup display guard completed ({reason}); later display changes are left to Vanilla. actual={DescribeActual()}.");
            // Removing a detour inside its own callback is unsafe. This executes after it returns.
            Application.onBeforeRender += DetachAfterCallback;
        }

        private void DetachAfterCallback()
        {
            Application.onBeforeRender -= DetachAfterCallback;
            UndoAndDispose(ref monitorHook);
            UndoAndDispose(ref loadHook);
            Shared.DebugLogHelper.LogDebug(log, "Bugfixes and QoL startup display-resolution hooks fully detached.");
        }

        private void LogFailureOnce(string phase, Exception ex)
        {
            if (failureLogged)
                return;
            failureLogged = true;
            Shared.DebugLogHelper.LogError(log, $"Bugfixes and QoL startup display reconciliation failed {phase}: {ex}");
        }

        private static bool IsSupported(int width, int height)
        {
            Resolution[] resolutions = Screen.resolutions;
            if (resolutions == null || resolutions.Length == 0)
                return true;
            foreach (Resolution resolution in resolutions)
                if (resolution.width == width && resolution.height == height)
                    return true;
            return false;
        }

        private bool IsEnabled => settings.EnableClientFeatures && settings.PreserveDisplayResolution;
        private static long ElapsedMilliseconds(long start, long end) => (end - start) * 1000L / Stopwatch.Frequency;
        private static string DescribeActual() => $"{Screen.width}x{Screen.height}@{Screen.currentResolution.refreshRate}Hz/{Screen.fullScreenMode}";

        private readonly struct ResolutionConfiguration
        {
            public ResolutionConfiguration(int windowWidth, int windowHeight, int fullscreenWidth, int fullscreenHeight, int fullscreenRefreshRate, int fullscreenType)
            {
                WindowWidth = windowWidth;
                WindowHeight = windowHeight;
                FullscreenWidth = fullscreenWidth;
                FullscreenHeight = fullscreenHeight;
                FullscreenRefreshRate = fullscreenRefreshRate;
                FullscreenType = fullscreenType;
            }
            public int WindowWidth { get; }
            public int WindowHeight { get; }
            public int FullscreenWidth { get; }
            public int FullscreenHeight { get; }
            public int FullscreenRefreshRate { get; }
            public int FullscreenType { get; }
            public bool HasWindowResolution => WindowWidth > 0 && WindowHeight > 0;
            public bool HasFullscreenResolution => FullscreenWidth > 0 && FullscreenHeight > 0 && (FullscreenType == 0 || FullscreenType == 1);
            public bool IsCaptured => HasWindowResolution || HasFullscreenResolution;
        }
    }
}
